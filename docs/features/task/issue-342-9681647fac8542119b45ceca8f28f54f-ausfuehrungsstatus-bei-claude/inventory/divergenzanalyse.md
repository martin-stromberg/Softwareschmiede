# Divergenzanalyse — Verifikation der Hypothesen aus `requirement.md`

Die Anforderung nennt fünf Kandidaten für den Divergenzpunkt (Fußzeile „CLI-Status: Ausführung läuft" vs. Kachel „✓ Bereit"). Verifikationsstand am Code (Commit `a5b9695`): **Alle fünf Mechanismen existieren wie beschrieben.** Zusätzlich wurden zwei weitere konkrete Pfade gefunden (F1, F2). Welcher im Kundenfall griff, ist aus dem Code allein nicht entscheidbar — die `requirement.md` verweist dafür zu Recht auf das App-Log (`CliProcessManager`-Warnungen, `KiAusfuehrungsService`-„zweiter Start abgewiesen").

Gemeinsame Vorbedingung aller Pfade: `KiAusfuehrungsStatusKlassifikation.Klassifiziere` (`AppConverters.cs` Z. 146–196) wertet **ausschließlich persistierte Felder** aus. „▶ Läuft"/„⏸ Wartet" entsteht nur bei `AusfuehrungsStatus == Aktiv` **und** `AufgabeLaufAktivitaet.IstAktiv(AktiveRunId, LastHeartbeatUtc)` (beide nicht null, Heartbeat < 5 min). Jede Konstellation `AusfuehrungsStatus=Aktiv` + `AktiveRunId=null` + `Status=Gestartet` erzeugt bei laufender Session exakt das gemeldete Bild.

## H1 — Startpfade ohne `Gestartet`-Ereignis — **bestätigt**

| Pfad | Stelle | Verhalten |
|------|--------|-----------|
| „Zweiter Start abgewiesen" (klassisch) | `KiAusfuehrungsService.StartCliCoreAsync` Z. 129–139 | `return existing` ohne `CliProcessStatusChanged`-Invoke |
| „Zweiter Start abgewiesen" (Terminal) | `KiAusfuehrungsService.StartTerminalSessionCoreAsync` Z. 224–235 | identisch |
| Session-Alive-Check | `EntwicklungsprozessService.CliNeustartenCoordinatedAsync` Z. 263–268 | `return`, bevor `StartTerminalSessionAsync` überhaupt aufgerufen wird — **keinerlei Persistenz** |
| Pre-Wiring-Exits | `StartTerminalSessionCoreAsync` Z. 322–345 | `Failure`/`HasExited`/Handle-entfernt vor Event-Verdrahtung → kein `Gestartet` (prozessseitig korrekt) |

Konsequenz: `CliProcessManager.OnCliProcessStatusChanged` wird nie aufgerufen → `AktivenLaufSetzenAsync`, `StartHeartbeat`, `SubscribeRuntimeStatus` bleiben aus. Die Fußzeile bindet die laufende Session dennoch (`LadenAsync` Z. 736–737 / `StartenAsync` Z. 1621–1626) → Fußzeile live, Kachel „✓ Bereit".

Erreichbare Aufrufketten:
- `StartenAsync` (AusfuehrungsStatus==Beendet) bzw. `PluginWechselAsync` → `StartCliAndUpdateStateAsync` → `CliNeustartenAsync` → Early-Return.
- `StartenAsync` (normal) → `ProzessUndCliCoordinatedAsync` → `LokalerKlonPfad` gesetzt → `CliNeustartenAsync` (Z. 157) → Early-Return.
- `ScmEventOrchestrator` Z. 161 → `CliNeustartenAsync` → Early-Return; auch `ProzessStartenUndCliStartenAsync` Z. 158 kann über Z. 155–159 in denselben Pfad laufen.

Verschärfung: `StartCliAndUpdateStateAsync` persistiert im Reuse-Fall zusätzlich `AusfuehrungAktivSetzenAsync` (Z. 1712) → `AusfuehrungsStatus=Aktiv` bei `AktiveRunId=null` — exakt das „Aktiv ohne Run"-Eingangsbild, das `Klassifiziere` zu `Bereit` auflöst (Zweige 3→4→6).

## H2 — verschluckter Fehler in `AktivenLaufSetzenAsync` — **bestätigt (Mechanismus)**

- `CliProcessManager.ExecuteWithAufgabeServiceAsync` (Z. 256–284) fängt **alle** Exceptions → `LogWarning` „Fehler bei {Operation}". `ObjectDisposedException` → `LogDebug`.
- `NotifyLaufdatenChanged` feuert nur im Erfolgszweig (Z. 266) → bei Fehlschlag kein Kachel-Refresh-Anstoß (5-s-Timer-Fallback bleibt).
- Mögliche Wurfquellen in `AufgabeService.AktivenLaufSetzenAsync` (Z. 976–995): `InvalidOperationException` bei `Status Beendet/Archiviert` (Z. 982–985), Aufgabe nicht gefunden (Z. 978–979), DB-Fehler (SQLite).
- Aufruf ist `SafeFireAndForget` (Z. 136): fehlgeschlagen = endgültig, kein Retry.
- Selbstheilung ausgeschlossen: `AktualisiereLaufStatusAsync` ist No-Op bei `AktiveRunId == null` (`AufgabeService.cs` Z. 1037–1038); `UpdateHeartbeatAsync` setzt zwar `LastHeartbeatUtc`, `IstAktiv` verlangt aber `AktiveRunId != null` → Kachel bleibt „✓ Bereit".

## H3 — `AktivenLaufBeendenAsync` bei weiterlebendem Prozess — **bestätigt (Mechanismus), Symptom-Passung eingeschränkt**

- `PseudoConsoleSession.ReadLoopAsync` ruft bei jedem Lesefehler `RaiseFailed` (Z. 300–306); `RaiseFailed` prüft **nicht**, ob der Prozess noch läuft (Z. 425–445). Ebenso `WriteInputAsync`-/`WritePromptAsync`-Schreibfehler (Z. 595, 546).
- `KiAusfuehrungsService.HandleSessionFailedAsync` (Z. 607–611) → `HandleExitedCoreAsync`: Handle-Entfernung + `DisposeSessionResourcesAsync` + `PersistAusfuehrungBeendetAsync` (`AktivenLaufBeendenAsync`) + `CliProcessStatusChanged(Fehler)` → `CliProcessManager` ruft `AktivenLaufBeendenAsync` erneut (idempotent). Ein Kill des evtl. weiterlebenden Kindprozesses erfolgt nicht.
- Einschränkung: `TaskDetailViewModel.OnCliProcessStatusChanged(Fehler)` setzt die Fußzeile auf „CLI-Status: Fehler" (Z. 1546–1548) — das gemeldete Bild „Ausführung läuft" entsteht hier nur, wenn das Fehler-Event die anzeigende Instanz nicht erreicht (z. B. andere Detail-Instanz, Event vor ViewModel-Abonnement, oder Session-Generationswechsel).
- Zweite Variante (Alt-Event bei Neu-Generation): `HandleExitedCoreAsync` legt ein Fremd-Handle zurück (Z. 627–631) — Race „Exit alter Prozess vs. Neustart" ist abgesichert.

## H4 — Heartbeat-Kette bricht bei lebendem Prozess — **bestätigt**

- `CliProcessManager.AktualisierungAsync` (Z. 95–98): `IsRunning` false → `StopHeartbeat` endgültig. `IsRunning` fängt alle `Process.HasExited`-Exceptions mit `catch { return false; }` (`KiAusfuehrungsService.cs` Z. 75–76) → ein einziger Auswurf stoppt den Timer.
- `AufgabeService.UpdateHeartbeatAsync`-Fehler (DB) werden in `AktualisierungAsync` nur geloggt (Z. 107–110) — `LastHeartbeatUtc` veraltet.
- Folge nach > 5 min: `IstAktiv` false → `Status=Gestartet` → „✓ Bereit" (Kachel) bei weiterhin lebender Session (Fußzeile live). Die Aufgabe wird **nicht** Recovery-Kandidat (`IsRunning` true filtert sie in `ScanForRecoveryCandidatesAsync` Z. 55 heraus — stiller Zustand ohne Reparaturpfad).

## H5 — Laufdaten auf separatem Lebenszyklus-Pfad zurückgesetzt — **bestätigt (Pfade existieren), Symptom-Passung eingeschränkt**

| Pfad | Stelle | Effekt | Fußzeilen-Passung |
|------|--------|--------|-------------------|
| `AbschliessenAsync` | `AufgabeService.cs` Z. 873–891 | Laufdaten + `Status=Beendet`, `AusfuehrungsStatus=Beendet` | `SollCliAnzeigen` false → Fußzeile „CLI inaktiv" — passt **nicht** |
| `StartZuruecksetzenAsync` | Z. 924–940 | Laufdaten + `Status=Neu`, `AusfuehrungsStatus=NichtGestartet` | ebenso — passt **nicht** |
| `StartenAsync` | Z. 763–785 | `Status=Gestartet`, `AusfuehrungsStatus=Aktiv`, **`AktiveRunId=null` explizit** | Fußzeile live — erzeugt das „Aktiv ohne Run"-Fenster zwischen `FinalizeStartAsync` und `Gestartet`-Persistierung; bleibt der `Gestartet`-Pfad aus (H1/H2), **dauerhaft** „✓ Bereit" |

## Zusatzfunde

### F1 — Manueller Stopp ohne tatsächliches Prozessende (`TaskDetailViewModel.CliStoppenAsync`, Z. 910–933)

`CliStoppenAsync` ruft **unbedingt** `AufgabeService.AktivenLaufBeendenAsync` nach `StopCliAsync`. `StopCliAsync` selbst schluckt Beendigungsfehler (`KiAusfuehrungsService.cs` Z. 474–477, nur `LogError`). Kann der Prozess nicht beendet werden (z. B. `Kill` schlägt fehl), feuert nie ein `Exited`/`Gestoppt`: Die Session (und damit die Fußzeile inkl. `RuntimeStatus`-Timer) lebt weiter, `IsCliRunning` wird zwar lokal auf `false` gesetzt, `AttachCliStatusSession` aber nicht abgemeldet — `RuntimeStatusChanged` der weiterlaufenden Session schreibt weiter „CLI-Status: Ausführung läuft"/„Wartet auf Eingabe". Persistiert: `AktiveRunId=null`, `AusfuehrungsStatus=Beendet` → Kachel „✓ Bereit". Der Heartbeat-Timer läuft sogar weiter (`IsRunning` true), aktualisiert aber nur `LastHeartbeatUtc` — `AktiveRunId=null` hält `IstAktiv` dauerhaft false.

### F2 — `AusfuehrungAktivSetzenAsync` als unvollständige „Halb-Persistierung" (`TaskDetailViewModel.StartCliAndUpdateStateAsync`, Z. 1704–1712)

Bei jedem Neustartpfad wird nach `CliNeustartenAsync` bei laufendem Prozess `AusfuehrungsStatus=Aktiv` persistiert — **ohne** `AktiveRunId`/`Heartbeat`. Das ist der einzige Produktivpfad außer `StartenAsync`, der „Aktiv ohne Run" schreibt, und er trifft **immer** dann voll ins Symptom, wenn `CliNeustartenAsync` in einen der Early-Returns lief (H1) — die Kachel geht deterministisch auf „✓ Bereit", während die Fußzeile die wiederverwendete Session anzeigt.

## Ergebnisbild

| Pfad | Fußzeile „Ausführung läuft" plausibel | Kachel „✓ Bereit" plausibel | Deckt sich mit Voraussetzung |
|------|--------------------------------------|-----------------------------|------------------------------|
| H1 Session-Reuse/Early-Return + F2 | ja | ja | vollständig |
| H2 Persist-Fehler bei `AktivenLaufSetzenAsync` | ja | ja | vollständig |
| H3 Session-Failed bei lebendem Prozess | bedingt (Fußzeile → „CLI-Status: Fehler") | ja | teilweise |
| H4 Heartbeat-Abbruch/-Ausfall | ja | ja | vollständig |
| H5 Reset-Pfade | nur `StartenAsync`-Fenster | ja | teilweise |
| F1 Stopp ohne Prozessende | ja | ja | vollständig |
