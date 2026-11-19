# Übersetzte Anforderung: Ausführungsbegrenzung muss pausierte Ausführungen freigeben (Fehlerkorrektur zu Issue #410)

**Quelle:** Kundenanforderung (Fehlerbeobachtung aus dem ersten produktiven Durchlauf der Ausführungsbegrenzung).
**Basisbranch (Diff-Basis):** `task/adbc30eab810488387fa4d4c42d44762-ausfuehrungsbegrenzung-fehlerh`
**Vorarbeiten (bereits gemergt):** Issue #410, Schritte 1–3 — `ScmEventExecutionLimitService` mit atomarer `PruefeUndReserviereStartPlatzAsync`-Platzreservierung, `ScmDispatchPrioritaet`, priorisierte Batch-Auswahl im `ScmEventMonitoringService`, ereignisgesteuerte Startprüfung über `ScmStartPruefungsService`/`ScmStartPruefungsAusloeser`.

## Fachliche Zusammenfassung

Die Belegungszählung der Ausführungsbegrenzung behandelt pausierte Aufgaben fälschlich weiterhin als belegten Ausführungsplatz. `ScmEventExecutionLimitService` zählt aktuell alle Aufgaben mit lebendem CLI-Prozess-Handle (`ICliExecutionLimitSource.GetLaufendeAufgabenIds` → `KiAusfuehrungsService._handles`, Kriterium `!Process.HasExited`); ein Platz wird damit faktisch erst frei, wenn die Ausführung endet (`AufgabeAusfuehrungsStatus.Beendet` → Prozessende). Da eine Pause (`Aufgabe.PausiertBisUtc` in der Zukunft) den laufenden CLI-Prozess bewusst nicht unterbricht, bleibt der Platz über die gesamte Pausendauer belegt. Beobachtete Fehlwirkung des Kunden: Bei erreichtem Limit startete die zweite Aufgabe erst nach vollständigem Abschluss der ersten (PR → Merge → Pre-Release → Promotion-Merge → Issue-Schließung → konfigurierte Wartezeit → `Beendet`), statt sofort beim Setzen des Pausenstatus nach dem ersten Pull Request.

Gefordert ist: Eine pausierte Ausführung belegt keinen Platz mehr — ihr Slot gilt ab dem Setzen der Pause als frei, sodass das nächste startbare Ereignis unmittelbar (ereignisgesteuert über den bereits vorhandenen `ScmStartPruefungsAusloeser`) bzw. spätestens im nächsten Poll zugestellt wird. Endet die Pause bei weiterlaufendem Prozess, zählt die Aufgabe wieder als belegt.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Belegungsbewertung in `ScmEventExecutionLimitService` (globaler Zähler `CliAusfuehrungsBelegung.Global` und Plugin-Zähler `CliAusfuehrungsBelegung.ProPlugin`), die `PruefeUndReserviereStartPlatzAsync` und `GetStartBlockReasonAsync` zugrunde liegt.
- **Auslösende Ereignisse:** Bereits vorhanden und ausreichend — alle Pausen-Setzpfade (`AufgabeService.SetPauseAsync`, `ScmEventStore.PauseForDelayAsync` für die Verzögerungsdirektive, `KiPluginLimitService.VerarbeiteRateLimitAsync` für Session-Limit-Pausen) melden bereits an `ScmStartPruefungsAusloeser.StartpruefungAnfordern()`; zusätzlich feuert `RunningCountChanged` bei Prozessende und der 60-s-Poll bleibt Sicherheitsnetz. Es ist kein neuer Trigger erforderlich; die ausgelöste Prüfung muss den frei werdenden Platz nur korrekt sehen.
- **Erwartete Wirkung:**
  - Aufgaben mit `PausiertBisUtc` in der Zukunft zählen weder ins globale Limit (`scm.execution.maxparallel`) noch in das Plugin-Limit (`plugins.maxparallel.<prefix>`) — in beiden Zählern konsequent.
  - Pausenkriterium identisch zur bestehenden Pausen-Klassifikation: `PausiertBisUtc > UtcNow` (wie `KiAusfuehrungsZustand.Pausiert` in `KiAusfuehrungsStatusKlassifikation` und der `BlockReason`-Text „Aufgabe pausiert; Start gesperrt." im `ScmEventOrchestrator`).
  - Abgelaufene, aber noch gesetzte `PausiertBisUtc`-Werte zählen wieder als belegt (Konsistenz mit der Fortsetzungs-Semantik aus `ScmDispatchPrioritaet`: gesetzt-auch-abgelaufen markiert Fortsetzung, keine Pause).
  - Reservierungsmechanik (`StartPlatzReservierung`, `ZulassungsGate`, `ReservierungsLebensdauer`), Wartegrund-Texte und Priorisierung bleiben unverändert.
- **Umgang mit fehlenden Objekten:**
  - Pausierte Aufgabe ohne auflösbare Plugin-Attribution: entfällt aus beiden Zählern (kein Restbeleg im Plugin-Zähler).
  - Ungesetztes Limit bleibt unbegrenzt — unverändert.
- **Scope-Ausschlüsse:**
  - Kein Unterbrechen, Stoppen oder Drosseln des CLI-Prozesses einer pausierten Aufgabe — der Prozess läuft weiter und bleibt für `IsRunning`, `CliLaufAktiv`, Recovery und die Fortsetzungs-Zustellung sichtbar. *(Status: bestätigt durch die Anforderung — nur die Limitbewertung ändert sich.)*
  - Keine Änderung an `GetLaufendeAufgabenIds`/`GetRunningCount`/`RunningCountChanged` selbst — diese Signale werden gemeinsam von `CliProcessManager.AbgleichAsync` (Laufdaten-Sicherstellung auch für pausierte, aber laufende Prozesse), `ScmStartPruefungsService`, `AutoShutdownOrchestrator` und der Statusanzeige genutzt. *(Status: verbindliche Abgrenzung — die Pausen-Filterung gehört in die Limit-Bewertung, nicht in die Handle-Quelle.)*
  - Keine UI-Änderung, keine neue Einstellung. *(Status: aus der Anforderung ableitbar.)*
  - Keine Änderung an `BlockReason`-Sperren (`Beendet`/`Archiviert`), an der Dispatch-Priorisierung oder an der Semantik „Limit gilt nur für neue CLI-Starts". *(Status: bestätigt.)*

## Betroffene Klassen und Komponenten

- `ScmEventExecutionLimitService` (`src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`): `GetBelegungAsync` (Z. 204–225) lädt bereits eine `Aufgaben`-Projektion (`Id`, `KiPluginPrefix`) — um `PausiertBisUtc` erweitern und Aufgaben mit `PausiertBisUtc > UtcNow` aus `Global` und `ProPlugin` herausfiltern. `TimeProvider` ist bereits injiziert (Konstruktorparameter `time`). `PruefeUndReserviereStartPlatzAsync`, `GetStartBlockReasonAsync`, `WirksameBelegungAsync` und `GetStartBlockReason` bleiben unverändert.
- `ICliExecutionLimitSource` (`src/Softwareschmiede/Application/Services/ICliExecutionLimitSource.cs`) und `KiAusfuehrungsService.GetLaufendeAufgabenIds` (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` Z. 135–143): Vertrag „laufender Prozess" bewusst **nicht** verändern (geteilte Nutzung, s. Scope-Ausschlüsse). Alternativ kann der Vertrag um eine pausbewusste Abfrage erweitert werden — Entscheidung in `plan.md`.
- `Aufgabe` (`src/Softwareschmiede/Domain/Entities/Aufgabe.cs` Z. 103): `PausiertBisUtc` als Pausenkriterium; keine Schemaänderung.
- `ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`): Zulassungsaufrufe `PruefeUndReserviereStartPlatzAsync` (Z. 245, 698) unverändert; `BlockReason` (Z. 992–993) liefert die Referenz-Pausendefinition.
- `ScmStartPruefungsService`/`ScmStartPruefungsAusloeser` (`src/Softwareschmiede/Application/Services/ScmStartPruefungsService.cs`): kein Code-Change erwartet — die Auslösepfade für Pausen existieren bereits; zu verifizieren ist nur, dass der Kunden-Pausenpfad (Pause nach PR-Stellung über SCM-Direktive) tatsächlich über einen der meldenden Pfade läuft.
- `ScmDispatchPrioritaet` und `ScmEventMonitoringService`: unverändert; die priorisierte Auswahl nutzt den frei werdenden Platz automatisch.
- Tests (`src/Softwareschmiede.Tests`, ggf. `src/Softwareschmiede.IntegrationTests`): `ScmEventExecutionLimitServiceTests` um Pausenfälle erweitern (pausiert → Platz frei global und pro Plugin; abgelaufene Pause → belegt; Pause während offener Reservierung), Dispatch-Integrationsfall im `ScmEventDispatchTests_Limits`-Muster (z. B. `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts`), Trigger-Nachweis „Pausen-Setzen führt zum sofortigen Folgestart".

## Implementierungsansatz

- **Zentrale Änderung:** In `GetBelegungAsync` die Projektion um `PausiertBisUtc` erweitern und laufende Aufgaben mit `PausiertBisUtc > time.GetUtcNow()` vor der Zählung und der Plugin-Attribution ausfiltern. Die DB-Abfrage lädt die Aufgaben ohnehin — der Filter kostet keinen zusätzlichen Zugriff. Kein Eingriff in `_handles`, `GetLaufendeAufgabenIds` oder `RunningCountChanged`.
- **Fortsetzbarkeit bleibt erhalten:** Die Fortsetzung einer pausierten Aufgabe über eine noch lebende SCM-Sitzung (`adapter.IsAlive` im `ScmEventOrchestrator`) durchläuft keine Zulassungsprüfung — die Platzfreigabe entzieht der pausierten Aufgabe also nicht die Fortsetzungsfähigkeit; sie benötigt beim Wiederanlaufen keinen neuen Startplatz. *(Annahme, zu verifizieren: Der Fortsetzungspfad für den Kundenfall läuft tatsächlich über eine lebende Sitzung und nicht über einen Neustart.)*
- **Nebenläufigkeit:** Die Pausen-Setzpfade committen die Pause vor `StartpruefungAnfordern` (vgl. `ScmEventStore` Z. 126–128), und die Belegung wird erst innerhalb von `ZulassungsGate` gelesen — ein gleichzeitig laufender Dispatch sieht den persistierten Pausenzustand unter der Sperre konsistent. Umgekehrt kann eine kurz vor der Pause erteilte Reservierung den Platz regulär belegen; sie verfällt wie bisher nach `ReservierungsLebensdauer`.
- **Grenzfall „Pausenende":** Läuft der Prozess einer Aufgabe nach Ablauf von `PausiertBisUtc` weiter, zählt sie ab dann wieder als belegt; die Zahl tatsächlich laufender Prozesse kann das konfigurierte Limit dadurch zeitweise überschreiten (fortgesetzte Sitzung braucht keinen neuen Platz). Das ist die fachlich geforderte Semantik — das Limit begrenzt Starts, nicht laufende Prozesse — und wird als verbindliche Annahme dokumentiert.
- **Alternativoption:** Statt des DB-Filters kann `ICliExecutionLimitSource` um eine pausbewusste Belegungsabfrage erweitert werden; empfohlen ist der Filter im Service, da er die geteilte Handle-Semantik unangetastet lässt.

## Konfiguration

Keine neue Konfiguration. Das Verhalten wirkt unter den bestehenden Einstellungen `scm.execution.maxparallel` (global) und `plugins.maxparallel.<prefix>` (je KI-Plugin) sowie dem Laufzeitschalter „Aufgaben ausführen" (`GetScmMonitoringEnabledAsync`). Leer/ungesetzt bleibt unbegrenzt.

## Offene Fragen

- **Pausenpfad des Kundenfalls:** Über welchen Mechanismus wird die Pause nach dem PR-Stellen konkret gesetzt (SCM-Direktive, `SetPauseAsync` oder ein weiterer Pfad)? Falls ein Pfad existiert, der `PausiertBisUtc` setzt, ohne `ScmStartPruefungsAusloeser` zu benachrichtigen, müsste dieser zusätzlich angebunden werden — sonst greift die Korrektur erst beim nächsten Poll. *(Vor der Implementierung im Bestand verifizieren.)*
- **Pausendefinition:** Zählt ausschließlich `PausiertBisUtc > UtcNow` als „pausiert" (Annahme: ja, konsistent zu `KiAusfuehrungsZustand.Pausiert`)? `AufgabeStatus.Wartend` ohne gesetzte Pausenzeit würde weiter belegen — tritt der Kundenfall immer mit gesetztem `PausiertBisUtc` auf?
- **Überschreitung nach Fortsetzung:** Ist akzeptabel, dass nach Wiederanlauf einer pausierten Aufgabe über die noch lebende Sitzung die Zahl laufender Prozesse das Limit zeitweise überschreiten kann (Annahme: ja — das Limit gilt nur für neue Starts)?
