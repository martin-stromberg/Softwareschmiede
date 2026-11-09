# Umsetzungsplan: Ausführungsstatus bei Claude — Synchronisation Aufgaben-Fußzeile ↔ Aufgabenliste

## Übersicht

Korrektur des Status-Synchronisationsdefekts aus Issue #342: Fußzeile der Aufgabendetailansicht (Live-`CliRuntimeStatus` der `ITerminalSession`) und Aufgabenkachel (persistierte Laufdaten via `KiAusfuehrungsStatusKlassifikation`) speisen sich aus zwei getrennten Datenquellen, die ohne Selbstheilung divergieren können. Die Korrektur setzt an der strukturellen Ursache an und deckt damit alle sieben verifizierten Divergenzpfade (H1–H5, F1–F2 aus `inventory/divergenzanalyse.md`) ab, statt einen Einzelpfad zu flicken: (1) Die Kachel-Klassifikation erhält die Live-Laufquelle als zusätzliche, nur-aufwärts wirkende Wahrheit — „✓ Bereit" bei nachweislich laufendem, gehandletem Prozess ist damit ausgeschlossen. (2) Die Laufdaten-Persistenz wird selbstheilend: Eine neue idempotente `AktivenLaufSicherstellenAsync`-Operation stellt fehlende Laufdaten bei jedem beobachteten laufenden Prozess wieder her — ereignisgetrieben (neues `CliLaufAktiv`-Signal auf allen Wiederverwendungspfaden, `RuntimeStatusChanged`, Heartbeat-Tick) und periodisch (Abgleich-Timer im `CliProcessManager`). (3) Die zwei Pfade, die den Divergenzzustand aktiv herstellen (`CliStoppenAsync` bei nicht beendbarem Prozess = F1, `AusfuehrungAktivSetzenAsync`-Halb-Persistierung = F2), werden an der Quelle korrigiert.

Betroffen: `KiAusfuehrungsService`, `IRunningAutomationStatusSource`, `AufgabeService`, `CliProcessManager`, `EntwicklungsprozessService`, `TaskDetailViewModel`, `MainWindowViewModel`, `AktiveAufgabePanelItem`, `KiAusfuehrungsStatusKlassifikation` (in `AppConverters.cs`).

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Der *KI-Ausführungslauf einer Aufgabe* — Ereignisbündel zwischen `CliProcessStatus.Gestartet` und `CliProcessStatus.Gestoppt`/`Fehler`, inklusive seiner zwei Beobachtungswege: In-Memory-`CliRuntimeStatus` der `ITerminalSession` (Fußzeile) und persistierte Laufdaten der `Aufgabe` (Kachel). |
| Auslösende Ereignisse | Nutzeraktion „Aufgabe starten" / „Prompt absenden" / „CLI stoppen" / „CLI neu starten" / „Plugin wechseln"; Systemereignisse `CliProcessStatusChanged`, `RuntimeStatusChanged`, `RunningCountChanged`, `AufgabeLaufdatenChangedNotifier.LaufdatenChanged`, das neue `CliLaufAktiv`-Signal (Session-/Prozess-Wiederverwendung), Heartbeat-Tick, 5-s-`DispatcherTimer`-Fallback, SCM-Automatik (`ScmEventOrchestrator`). |
| Erwartete Wirkung | Semantische Übereinstimmung beider Anzeigen ohne manuelles Neuladen: Fußzeile „CLI-Status: Ausführung läuft"/„Wartet auf Eingabe" ↔ Kachel „▶ Läuft"/„⏸ Wartet"; „✓ Bereit" nur, wenn kein gehandelter Prozess läuft. Gilt in beiden Verwendungen der gemeinsamen `AktiveAufgabenListe` (Seitenleiste und Dashboard). |
| Umgang mit fehlenden Objekten | *Laufende Session ohne persistierte Laufdaten* (`AktiveRunId`/`LastHeartbeatUtc`/`LaufStatus` fehlen oder `AusfuehrungsStatus != Aktiv`, Prozess läuft): fachlich ein aktiver Lauf — Laufdaten werden per `AktivenLaufSicherstellenAsync` wiederhergestellt (wiederverwenden/aktivieren, nicht verwerfen); die Kachel zeigt bereits über den Live-Zweig sofort „▶ Läuft"/„⏸ Wartet". *Persistierter aktiver Lauf ohne laufenden Prozess*: unverändert vom bisherigen Modell abgedeckt (Heartbeat-Timeout `AufgabeRecoveryService.HeartbeatTimeoutMinutes` = 5 min → „✓ Bereit" + Recovery-Kandidat); der Live-Zweig greift hier nicht (`GetLiveLaufStatus` liefert `null`, weil kein Prozess läuft). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Weiterlebender Kindprozess bei `ITerminalSession.Failed` (Divergenzpfad H3-Rest) wird nicht beendet: `HandleExitedCoreAsync` entfernt Handle und räumt Laufdaten ab, ein ggf. überlebender OS-Prozess bleibt. | Betrifft den Prozess-Lebenszyklus, nicht die Status-Synchronisation: Nach Handle-Entfernung liefert `IsRunning`/`GetLiveLaufStatus` `false`/`null` und die Fußzeile zeigt „CLI-Status: Fehler" — das gemeldete Divergenzbild kann hier nicht entstehen. | bestätigt (2026-11-09, Anwender: „weiter so") |
| 2 | `Pausiert`- und `PromptInWartestellung`-Zweige der Klassifikation behalten Vorrang vor dem neuen Live-Zweig. | Bestehende Überlagerungssemantik (Pause/Plan überlagert Laufzeitanzeige) ist nicht Teil der Anforderung und bleibt unverändert. | bestätigt (2026-11-09, Anwender: „weiter so") |
| 3 | Der `Aufgabe`-Entity-Eingang von `Klassifiziere` erhält keinen Live-Fallback; nur `AktiveAufgabePanelItem` trägt `LiveLaufStatus`. | Die Entity-Variante wird nicht in der Aufgabenliste verwendet; der Live-Zustand ist ein UI-Projektionsdetail des Snapshot-Items. | bestätigt (2026-11-09, Anwender: „weiter so") |
| 4 | Persistenzfehler (`ExecuteWithAufgabeServiceAsync`) werden weiterhin nur geloggt — ohne neue Fehler-UI oder `ProtokollTyp.SystemMeldung`-Eintrag. | Die strukturelle Ursache des Verschluckens (kein Retry, kein Heilungspfad) wird durch die Selbstheilung (Heartbeat-Tick, Abgleich, Events) behoben; eine zusätzliche Fehleroberfläche ist aus der Anforderung nicht ableitbar. | bestätigt (2026-11-09, Anwender: „weiter so") |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Kachel-Projektion (Sync-Garantie) | Live-Quelle in `KiAusfuehrungsStatusKlassifikation` als *nur-aufwärts*-Zweig: `AktiveAufgabePanelItem.LiveLaufStatus` (`AufgabeLaufStatus?`, aus `IRunningAutomationStatusSource.GetLiveLaufStatus`) schlägt vor der `AusfuehrungsStatus`-Prüfung auf `Laeuft`/`Wartet` durch, niemals auf `Bereit`. | Deckt alle sieben Divergenzpfade anzeigeseitig sofort ab — unabhängig davon, welcher konkrete Pfad beim Kunden griff. Nur-Upgrade-Semantik erfüllt die Nebenbedingung: ein verwaister `AktiveRunId`-Zustand erscheint nicht wieder aktiv, weil `GetLiveLaufStatus` ausschließlich `null`-frei ist, wenn ein Prozess im Handle-Registry nachweislich läuft (`!HasExited`) — nicht aus persistierten Daten. |
| Live-Quelle bereitstellen | `IRunningAutomationStatusSource` um `AufgabeLaufStatus? GetLiveLaufStatus(Guid)` erweitert (Implementierung in `KiAusfuehrungsService`: nicht laufend → `null`; Session-`RuntimeStatus == WartetAufEingabe` → `WartetAufEingabe`; sonst → `Laeuft`). | Hält die DI-Abstraktion (Interface statt `KiAusfuehrungsService` direkt ins `MainWindowViewModel`); `AufgabeLaufStatus` als Rückgabetyp bildet direkt das persistierte Substatus-Vokabular ab (keine Infrastructure-Enum in der Domain-Projektion). |
| Wiederverwendungs-Signal | Neues dediziertes Event `CliLaufAktiv` (`Action<Guid>`) auf `KiAusfuehrungsService`, gefeuert in den beiden Early-Returns (`StartCliCoreAsync`, `StartTerminalSessionCoreAsync`) sowie über öffentliche `NotifyLaufAktiv(Guid)` aus `EntwicklungsprozessService.CliNeustartenCoordinatedAsync`. | `CliProcessStatusChanged(Gestartet)` auf Reuse-Pfaden wurde verworfen: Es würde `PromptZeitVersandService.ZustellungNachSessionStartAsync` auslösen (verändert Prompt-Zustellung jenseits der Anforderung), die Fußzeile kurz auf „Gestartet" flackern lassen und — weil `CliProcessManager` dann `AktivenLaufSetzenAsync` aufriefe — `AktiveRunId`/`LetzterCliStartUtc`/`LaufStatus` bei jeder Wiederverwendung neu schreiben. |
| Persistenz-Selbstheilung | Neue idempotente `AufgabeService.AktivenLaufSicherstellenAsync(Guid, AufgabeLaufStatus?)`: setzt vollständige Laufdaten nur, wenn `AktiveRunId` fehlt; sonst nur Heartbeat-Auffrischung (+ optional `LaufStatus`); terminale `Status` (`Beendet`/`Archiviert`) → Log + Return statt Wurf. | Ein einziger idempotenter Sicherstellungspunkt statt Reparatur an jedem der sieben Pfade; wird von `CliProcessManager` aus `CliLaufAktiv`-Handler, `OnRuntimeStatusChanged`, Heartbeat-Tick und Abgleich sowie von `TaskDetailViewModel.StartCliAndUpdateStateAsync` aufgerufen. |
| Letztes Sicherheitsnetz | Periodischer Abgleich-Timer im `CliProcessManager` (`AbgleichIntervall` = 30 s, wie `HeartbeatInterval`): iteriert `KiAusfuehrungsService.GetLaufendeAufgabenIds()` → `LaufdatenSicherstellenAsync` pro laufender Aufgabe. | Einziger Mechanismus, der unabhängig von jeglicher Event-Zustellung „Prozess läuft ⇒ Laufdaten persistiert" garantiert; schließt Restlücken (z. B. H4 nach endgültig gestopptem Heartbeat-Timer ohne weitere `RuntimeStatusChanged`-Wechsel). |
| Fehlerheilung | Kein expliziter Retry/`ProtokollTyp.SystemMeldung` für `ExecuteWithAufgabeServiceAsync`-Fehler — Heilung strukturell über die Selbstheilungspfade (nächster Tick/Abgleich/Event wiederholt die Sicherstellung automatisch). | Entspricht Scope-Ausschluss 4; einmalig verschluckte Fehler sind nicht mehr endgültig. |

## Programmabläufe

### Kachel-Projektion mit Live-Zweig (Anzeige-Konsistenz)

1. `MainWindowViewModel.AktiveAufgabenAktualisierenAsync` lädt wie bisher `GetAktiveAufgabenAsync` und mappt via `MapAktiveAufgabePanelItem`.
2. `MapAktiveAufgabePanelItem` fragt zusätzlich `_runningStatusSource.GetLiveLaufStatus(aufgabe.Id)` ab und befüllt `AktiveAufgabePanelItem.LiveLaufStatus` (`null`, wenn kein gehandelter Prozess läuft).
3. `KiAusfuehrungsStatusKlassifikation.Klassifiziere` wertet `StatusDaten` in dieser Reihenfolge aus: `Pausiert` → `PromptInWartestellung` → **`LiveLaufStatus` gesetzt → `Wartet` (bei `WartetAufEingabe`) bzw. `Laeuft`** → `AusfuehrungsStatus != Aktiv` → `Bereit` → `IstAktiv(...)` → `Laeuft`/`Wartet` → `Status == Wartend` → `Wartet` → `Bereit`.
4. Beide Converter (`KiAusfuehrungsStatusConverter`, `KiAusfuehrungsStatusSymbolConverter`) bleiben unverändert — sie teilen die Klassifikation.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `AktiveAufgabePanelItem`, `IRunningAutomationStatusSource`, `KiAusfuehrungsService`, `KiAusfuehrungsStatusKlassifikation`, `ActiveTasksListControl` (unverändert), `DashboardViewModel` (unverändert, gemeinsame Liste).

### Wiederverwendungs-Signal `CliLaufAktiv` (Startpfad-Lücke H1)

1. `KiAusfuehrungsService.StartCliCoreAsync` (~Z. 129–139) und `StartTerminalSessionCoreAsync` (~Z. 224–235): Trifft die Prüfung „Handle vorhanden + `!HasExited`" zu, wird vor `return existing` `CliLaufAktiv` mit der Aufgaben-ID gefeuert (kein `Gestartet`-Event, kein `RunningCountChanged`).
2. `EntwicklungsprozessService.CliNeustartenCoordinatedAsync` (~Z. 263–268): Im Session-Alive-Early-Return wird `_options.KiAusfuehrungsService.NotifyLaufAktiv(aufgabeId)` aufgerufen; `NotifyLaufAktiv` feuert `CliLaufAktiv` nur, wenn `IsRunning(aufgabeId)` zutrifft. Deckt auch die Aufrufer `ProzessUndCliCoordinatedAsync` (~Z. 155–159) und `ScmEventOrchestrator` (~Z. 161) ab.
3. `CliProcessManager.OnCliLaufAktiv(aufgabeId)` (neues Abonnement im Konstruktor, Abmeldung in `Dispose`) ruft `LaufdatenSicherstellenAsync(aufgabeId)` via `SafeFireAndForget`.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `EntwicklungsprozessService`, `CliProcessManager`, `ScmEventOrchestrator` (indirekt), `TaskDetailViewModel` (indirekt über `CliNeustartenAsync`).

### `CliProcessManager.LaufdatenSicherstellenAsync` (zentraler Selbstheilungspunkt)

1. `_kiService.IsRunning(aufgabeId)` `false` → Rückkehr ohne Aktion (kein falsches „Heilen" bei beendetem Prozess).
2. `StartHeartbeat(aufgabeId)` nur aufrufen, wenn für die Aufgabe kein Timer in `_heartbeatTimers` läuft (Konditionalisierung statt des bisherigen Stop+Start, damit der Abgleich den Takt nicht zurücksetzt).
3. `SubscribeRuntimeStatus(aufgabeId)` aufrufen — zuvor wird `SubscribeRuntimeStatus` idempotent gemacht: Rückkehr ohne erneute Registrierung, wenn `_runtimeStatusSubscriptions` für die Aufgabe bereits dieselbe Session enthält.
4. `liveLaufStatus` aus `_kiService.GetTerminalSession(aufgabeId)?.RuntimeStatus` mappen (`WartetAufEingabe`→`AufgabeLaufStatus.WartetAufEingabe`, `Laeuft`→`Laeuft`, `Inaktiv`/keine Session→`null`).
5. `ExecuteWithAufgabeServiceAsync(aufgabeId, "Sicherstellen der Laufdaten", s => s.AktivenLaufSicherstellenAsync(aufgabeId, liveLaufStatus))` — bei Erfolg feuert der bestehende `NotifyLaufdatenChanged`-Zweig den Kachel-Refresh an.

Beteiligte Klassen/Komponenten: `CliProcessManager`, `KiAusfuehrungsService`, `AufgabeService`, `AufgabeLaufdatenChangedNotifier`.

### `AufgabeService.AktivenLaufSicherstellenAsync` (idempotente Persistenz)

1. Aufgabe laden; nicht gefunden → `InvalidOperationException` (konsistent zu `AktivenLaufSetzenAsync`).
2. `Status` `Beendet`/`Archiviert` → Log-Debug + Rückkehr ohne Änderung (kein Wiederbeleben terminaler Aufgaben; sie erscheinen ohnehin nicht in `GetAktiveAufgabenAsync`).
3. `AktiveRunId == null` → `AusfuehrungsStatus = Aktiv`, `AktiveRunId = <neue ID>`, `LastHeartbeatUtc = now`, `LetzterCliStartUtc ??= now` (ein bekannter echter Startzeitpunkt bleibt erhalten — Sortierstabilität), `LaufStatus = laufStatus ?? Laeuft`.
4. `AktiveRunId != null` → `LastHeartbeatUtc = now`; `LaufStatus` nur überschreiben, wenn der Parameter gesetzt ist.
5. `SaveChangesAsync`.

Beteiligte Klassen/Komponenten: `AufgabeService`, `Aufgabe`, `SoftwareschmiededDbContext`.

### Periodischer Abgleich (letztes Sicherheitsnetz)

1. `CliProcessManager`-Konstruktor startet `_abgleichTimer` (`AbgleichIntervall` = `HeartbeatInterval` = 30 s, erster Tick nach Intervall).
2. `AbgleichAsync` iteriert `_kiService.GetLaufendeAufgabenIds()` und ruft je ID `LaufdatenSicherstellenAsync` auf (fehlerisoliert pro Aufgabe; einzelne Fehlschläge nur geloggt).
3. `Dispose` räumt den Timer mit ab.

Beteiligte Klassen/Komponenten: `CliProcessManager`, `KiAusfuehrungsService`.

### Heartbeat-Tick und Laufzeit-Substatus (Heilung H2/H4/H5, No-Op-Lücke)

1. `AktualisierungAsync` (Heartbeat-Tick): bei `IsRunning` → `_kiService.UpdateHeartbeat` + `ExecuteWithAufgabeServiceAsync(..., s => s.AktivenLaufSicherstellenAsync(aufgabeId))` **statt** `UpdateHeartbeatAsync` — heilt gleichzeitig gelöschte/fehlende Laufdaten (H4/F1/H5) und frischt den Heartbeat auf.
2. `OnRuntimeStatusChanged`: `Inaktiv` ignoriert weiterhin; bei `Laeuft`/`WartetAufEingabe` wird `ExecuteWithAufgabeServiceAsync(..., s => s.AktivenLaufSicherstellenAsync(aufgabeId, laufStatus))` aufgerufen — ersetzt den `AktualisiereLaufStatusAsync`-Aufruf und hebt dessen No-Op-bei-`AktiveRunId == null`-Falle auf.

Beteiligte Klassen/Komponenten: `CliProcessManager`, `AufgabeService`, `ITerminalSession`.

### `TaskDetailViewModel.StartCliAndUpdateStateAsync` (F2: Halb-Persistierung schließen)

1. Nach `CliNeustartenAsync` und `IsRunning == true` wird statt `AusfuehrungAktivSetzenAsync` die neue `AufgabeService.AktivenLaufSicherstellenAsync(_aufgabeId, liveLaufStatus)` aufgerufen (Lauf-Substatus aus `GetTerminalSession` wie oben), danach `_laufdatenChangedNotifier.NotifyLaufdatenChanged(_aufgabeId)`.
2. Damit persistiert der Pfad denselben vollständigen Lauf-Zustand wie ein echter `Gestartet`-Lauf — das „Aktiv ohne Run"-Eingangsbild entsteht hier nicht mehr.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `AufgabeService`, `AufgabeLaufdatenChangedNotifier`, `KiAusfuehrungsService`.

### `TaskDetailViewModel.CliStoppenAsync` (F1: Stopp ohne Prozessende)

1. Nach `StopCliAsync` wird `_kiService.IsRunning(_aufgabeId)` geprüft.
2. Prozess läuft noch → `FehlerMeldung` setzen („CLI-Prozess konnte nicht beendet werden…"), `IsCliRunning` bleibt `true`, Session bleibt angebunden (Fußzeile weiterhin live), **kein** `AktivenLaufBeendenAsync`, kein `AusfuehrungsStatus = Beendet` auf der geladenen Entity.
3. Prozess beendet → bisheriges Verhalten (`AktivenLaufBeendenAsync` + UI-Update).

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `KiAusfuehrungsService`, `AufgabeService`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `IRunningAutomationStatusSource` (Interface)

- **Neue Methoden:** `GetLiveLaufStatus(Guid aufgabeId)` — liefert `AufgabeLaufStatus?`: `null`, wenn kein gehandelter Prozess für die Aufgabe läuft; `WartetAufEingabe`, wenn die Session `CliRuntimeStatus.WartetAufEingabe` meldet; sonst `Laeuft` (inkl. laufendem Handle ohne Session / `RuntimeStatus.Inaktiv`).

### `KiAusfuehrungsService` (Klasse)

- **Neue Events:** `CliLaufAktiv` (`Action<Guid>`) — gefeuert, wenn eine Startanforderung einen bereits laufenden Prozess zurückgibt bzw. ein laufender Prozess über `NotifyLaufAktiv` bestätigt wird.
- **Neue Methoden:**
  - `NotifyLaufAktiv(Guid aufgabeId)` — feuert `CliLaufAktiv`, sofern `IsRunning(aufgabeId)`; sonst No-Op. Für Aufrufer außerhalb des Service (Early-Return in `EntwicklungsprozessService`).
  - `GetLaufendeAufgabenIds()` — `IReadOnlyCollection<Guid>` der Aufgaben-IDs mit laufendem Handle (`!HasExited`, Exception-tolerant wie `GetRunningCount`); Datenquelle des Abgleich-Timers.
  - `GetLiveLaufStatus(Guid aufgabeId)` — Interface-Implementierung (Semantik siehe oben).
- **Geänderte Methoden:** `StartCliCoreAsync` — Early-Return „zweiter Start abgewiesen" (~Z. 129–139) feuert `CliLaufAktiv`; `StartTerminalSessionCoreAsync` — analog (~Z. 224–235).

### `AufgabeService` (Klasse)

- **Neue Methoden:** `AktivenLaufSicherstellenAsync(Guid id, AufgabeLaufStatus? laufStatus = null, CancellationToken ct = default)` — idempotente Sicherstellung der Laufdaten (Ablauf siehe oben).

### `CliProcessManager` (Klasse)

- **Neue Methoden:**
  - `LaufdatenSicherstellenAsync(Guid aufgabeId)` — zentraler, privater Selbstheilungspunkt (Ablauf siehe oben).
  - `OnCliLaufAktiv(Guid aufgabeId)` — Event-Handler für `CliLaufAktiv` → `LaufdatenSicherstellenAsync` (fire-and-forget).
  - `AbgleichAsync()` — periodische Iteration über `GetLaufendeAufgabenIds` → `LaufdatenSicherstellenAsync`.
- **Neue Felder:** `_abgleichTimer` (`Timer`, Intervall `AbgleichIntervall` = 30 s).
- **Geänderte Methoden:**
  - Konstruktor — zusätzliches Abonnement `CliLaufAktiv` + Start des `_abgleichTimer`.
  - `AktualisierungAsync` — persistiert per `AktivenLaufSicherstellenAsync` statt `UpdateHeartbeatAsync`.
  - `OnRuntimeStatusChanged` — persistiert per `AktivenLaufSicherstellenAsync(aufgabeId, laufStatus)` statt `AktualisiereLaufStatusAsync`.
  - `SubscribeRuntimeStatus` — idempotent: keine erneute Registrierung bei bereits abonnierter Session.
  - `Dispose` — meldet `CliLaufAktiv` ab und räumt `_abgleichTimer`.
- **Entfernte Methoden:** private `AktualisiereLaufStatusAsync` (durch `AktivenLaufSicherstellenAsync` abgelöst); `AufgabeService.AktualisiereLaufStatusAsync` (public) bleibt bestehen.

### `EntwicklungsprozessService` (Klasse)

- **Geänderte Methoden:** `CliNeustartenCoordinatedAsync` — Session-Alive-Early-Return (~Z. 263–268) ruft vor `return` `_options.KiAusfuehrungsService.NotifyLaufAktiv(aufgabeId)` auf.

### `TaskDetailViewModel` (Klasse)

- **Geänderte Methoden:**
  - `StartCliAndUpdateStateAsync` — `IsRunning`-Zweig persistiert via `AufgabeService.AktivenLaufSicherstellenAsync` (+ `NotifyLaufdatenChanged`) statt `AusfuehrungAktivSetzenAsync` (F2).
  - `CliStoppenAsync` — `AktivenLaufBeendenAsync` und die „beendet"-UI-Zustände nur noch, wenn `_kiService.IsRunning(_aufgabeId)` `false` ist; sonst `FehlerMeldung` + unveränderter Lauf-Zustand (F1).

### `AktiveAufgabePanelItem` (Datenmodellklasse)

- **Neue Eigenschaften:** `LiveLaufStatus` (`AufgabeLaufStatus?`, `init`) — Live-Substatus aus `GetLiveLaufStatus`; `null` = kein laufender gehandelter Prozess.

### `MainWindowViewModel` (Klasse)

- **Geänderte Methoden:** `MapAktiveAufgabePanelItem` — befüllt `LiveLaufStatus` aus `_runningStatusSource.GetLiveLaufStatus(aufgabe.Id)`.

### `KiAusfuehrungsStatusKlassifikation` (interne Klasse in `AppConverters.cs`)

- **Geänderte Methoden:** `Klassifiziere` — `StatusDaten` erhält das Feld `LiveLaufStatus` (`AufgabeLaufStatus?`; nur aus `AktiveAufgabePanelItem` befüllt, `Aufgabe`-Pfad → `null`); neuer Zweig nach `HasScheduledPrompt` und vor der `AusfuehrungsStatus`-Prüfung: `LiveLaufStatus == WartetAufEingabe → Wartet`, `LiveLaufStatus` sonst gesetzt → `Laeuft`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Eingabe-Validierungen. Verhaltensregeln der neuen Persistenz-Operation: `AktivenLaufSicherstellenAsync` wirft bei unbekannter Aufgabe (konsistent zu den Geschwistermethoden) und überspringt terminale Stati (`Beendet`/`Archiviert`) ohne Wurf, damit der periodische Abgleich keine Warnungsflut erzeugt.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Interface-Erweiterung `IRunningAutomationStatusSource`:** Alle handgeschriebenen Test-Fakes müssen `GetLiveLaufStatus` implementieren (8 Fakes in 4 Testdateien, siehe „Betroffene bestehende Tests"); Moq-basierte Mocks liefern implizit `null` (kein Live-Zustand) — Verhalten unverändert.
- **Klassifikations-Reihenfolge:** Der Live-Zweig kann bei persistiertem `AusfuehrungsStatus = Beendet` + laufendem Prozess „▶ Läuft"/„⏸ Wartet" anzeigen (F1-Bild) — das ist fachlich beabsichtigt (der Prozess läuft; Fußzeile und Kachel werden synchron). Ein Downgrade auf `Bereit` bei `LiveLaufStatus == null` findet nicht statt — die Heartbeat-Regel bleibt einzige Lebendigkeits-Prüfung für persistierte Läufe.
- **`Status == Wartend` + laufender Prozess:** Bisherige Kette ergab `Wartet`; mit Live-Zweig bei `RuntimeStatus == Laeuft` kann kurzzeitig `Laeuft` angezeigt werden — semantisch zutreffender (Prozess läuft aktiv) und zeitlich identisch zum Fußzeilen-Zustand.
- **`CliLaufAktiv`-Abonnentenkreis:** Nur `CliProcessManager` — `PromptZeitVersandService` und `TaskDetailViewModel.OnCliProcessStatusChanged` bleiben unberührt (keine Prompt-Zustellung, kein „Gestartet"-Flackern auf Wiederverwendung).
- **Fenster Live-Anzeige vs. Persistenz:** Die Kachel kann „▶ Läuft" über den Live-Zweig zeigen, bevor `AktiveRunId` persistiert ist (Heilung ereignisgetrieben binnen ms bis zum 30-s-Abgleich). Recovery-Fachlichkeit ist unbeeinträchtigt: `ScanForRecoveryCandidatesAsync` filtert Aufgaben mit `IsRunning` ohnehin heraus.
- **`CliStoppenAsync`-Semantik:** Bei nicht beendbarem Prozess zeigt die Detailansicht weiterhin „läuft" + Fehlermeldung statt falsch „Bereit" — gewollte Verhaltenskorrektur; `KannCliStoppen`/`CliStoppenCommand` bleiben erneut auslösbar.
- **`AktualisiereLaufStatusAsync` (public, `AufgabeService`)** verliert den letzten Produktivaufrufer; die Methode bleibt bestehen (bestehende Tests, kohärente API).
- **`LetzterCliStartUtc`-Erhalt in `AktivenLaufSicherstellenAsync`** verhindert Umordnungsflackern der Liste bei jeder Heilung.
- **H3-Rest (verwaister OS-Prozess nach `Failed`):** Unverändert — nach Handle-Entfernung ist der Prozess für `IsRunning`/`GetLiveLaufStatus` unsichtbar; Fußzeile zeigt „CLI-Status: Fehler" und die Kachel „✓ Bereit" — kein Divergenzbild (Scope-Ausschluss 1).

## Umsetzungsreihenfolge

1. **`IRunningAutomationStatusSource.GetLiveLaufStatus` + `KiAusfuehrungsService`-Erweiterungen** (`CliLaufAktiv`-Event, `NotifyLaufAktiv`, `GetLaufendeAufgabenIds`, `GetLiveLaufStatus`, Event-Feuer in beiden Early-Returns)
   - Voraussetzungen: Keine.
   - Beschreibung: Neue Live-/Signal-API der Laufzeitquelle anlegen; `CliLaufAktiv` in `StartCliCoreAsync` (~Z. 138) und `StartTerminalSessionCoreAsync` (~Z. 233) vor `return existing` feuern.

2. **`AufgabeService.AktivenLaufSicherstellenAsync`** (idempotente Laufdaten-Sicherstellung)
   - Voraussetzungen: Keine.
   - Beschreibung: Neue Service-Methode nach dem Muster von `AktivenLaufSetzenAsync`/`AktualisiereLaufStatusAsync` implementieren.

3. **`CliProcessManager`: Selbstheilung einbauen**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `CliLaufAktiv`-Abonnement + `OnCliLaufAktiv`, `LaufdatenSicherstellenAsync`, `AbgleichAsync` + `_abgleichTimer` (Start im Konstruktor, Dispose), `AktualisierungAsync` und `OnRuntimeStatusChanged` auf `AktivenLaufSicherstellenAsync` umstellen, `SubscribeRuntimeStatus` idempotent machen, private `AktualisiereLaufStatusAsync` entfernen.

4. **`EntwicklungsprozessService.CliNeustartenCoordinatedAsync`: `NotifyLaufAktiv` im Early-Return**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Session-Alive-Early-Return (~Z. 263–268) um den `NotifyLaufAktiv`-Aufruf ergänzen (deckt `ProzessUndCliCoordinatedAsync` ~Z. 157 und `ScmEventOrchestrator` ~Z. 161 mit ab).

5. **Kachel-Projektion: `AktiveAufgabePanelItem.LiveLaufStatus`, `MapAktiveAufgabePanelItem`, `KiAusfuehrungsStatusKlassifikation`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `LiveLaufStatus`-Property, Befüllung im Mapping, `StatusDaten`-Feld + neuer Klassifikationszweig.

6. **`TaskDetailViewModel`: `StartCliAndUpdateStateAsync` (F2) und `CliStoppenAsync` (F1)**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Halb-Persistierung durch `AktivenLaufSicherstellenAsync` ersetzen; `IsRunning`-Guard vor `AktivenLaufBeendenAsync` einbauen.

7. **Test-Fakes `IRunningAutomationStatusSource` erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `GetLiveLaufStatus` in allen 8 handgeschriebenen Fakes implementieren (laufende Fakes → konsistenter `AufgabeLaufStatus`, sonst `null`).

8. **Neue Tests schreiben + bestehende anpassen**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Testmatrix aus dem Abschnitt „Tests" umsetzen.

9. **Vollbuild + Testläufe**
   - Voraussetzungen: Schritte 1–8. CLAUDE.md-Regeln: `dotnet test` niemals im Hintergrund; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (und `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` für die OsInterface-Lane) in dieser Sandbox inline setzen.
   - Beschreibung: `dotnet build Softwareschmiede.slnx`, dann beide Testprojekte in beiden Kategorie-Lanes.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AktivenLaufSicherstellenAsync_SetztVolleLaufdaten_BeiFehlenderAktiveRunId` | `AufgabeServiceTests_AktiverLauf` | `AktiveRunId`, `LastHeartbeatUtc`, `AusfuehrungsStatus = Aktiv`, `LaufStatus` (Parameter bzw. `Laeuft`) werden gesetzt |
| `AktivenLaufSicherstellenAsync_BehaeltAktiveRunId_UndFrischtHeartbeat_BeiBestehendemLauf` | `AufgabeServiceTests_AktiverLauf` | Bestehende `AktiveRunId`/`LetzterCliStartUtc` bleiben; `LastHeartbeatUtc` wird aktualisiert; `LaufStatus` nur bei Parameter geändert |
| `AktivenLaufSicherstellenAsync_SkipptOhneWurf_BeiTerminalemStatus` | `AufgabeServiceTests_AktiverLauf` | `Status = Beendet`/`Archiviert` → keine Änderung, keine Exception |
| `ZweiterStart_LeuftetCliLaufAktiv_StattGestartet` | `KiAusfuehrungsServiceTests` | Zweiter `StartTerminalSessionAsync`/`StartCliAsync` bei laufendem Handle (deterministischer Launcher) → `CliLaufAktiv` gefeuert, kein zweites `Gestartet`, gleiches Handle |
| `NotifyLaufAktiv_FeuertNur_BeiLaufendemProzess` | `KiAusfuehrungsServiceTests` | `NotifyLaufAktiv` ohne Handle → kein Event; mit laufendem Handle → `CliLaufAktiv` |
| `GetLiveLaufStatus_BildetSessionStatusAb` | `KiAusfuehrungsServiceTests` | Ohne Handle → `null`; laufend ohne/mit Session → `Laeuft` bzw. `WartetAufEingabe`-Mapping |
| `GetLaufendeAufgabenIds_ListetNurLaufendeHandles` | `KiAusfuehrungsServiceTests` | Aufgaben ohne/mit beendetem Prozess erscheinen nicht |
| `CliLaufAktiv_StelltLaufdatenSicher_BeiWiederverwendung` | `CliProcessManagerTests_AktiverLauf` | Aufgabe ohne `AktiveRunId` + `CliLaufAktiv` (via zweitem Start o. Reflexion) → Laufdaten persistiert, `LaufdatenChanged` gefeuert |
| `AktualisierungAsync_HeiltGeloeschteLaufdaten_BeiLaufendemProzess` | `CliProcessManagerTests_AktiverLauf` | Laufender Lauf + per DB gelöschte `AktiveRunId` → nächster Tick stellt Laufdaten wieder her (H4/F1/H5) |
| `OnRuntimeStatusChanged_HeiltFehlendeAktiveRunId` | `CliProcessManagerTests_LaufStatus` | `RuntimeStatusChanged` bei fehlender `AktiveRunId` → vollständige Laufdaten + `LaufStatus` (Schließt die No-Op-Falle) |
| `AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben` | `CliProcessManagerTests_AktiverLauf` | Laufendes Handle ohne Heartbeat/Subscription → nach `AbgleichAsync` (Reflexion oder Timer) sind Laufdaten + Heartbeat + Subscription etabliert |
| `Klassifiziere_ZeigtLaeuft_BeiLiveLaufStatus_OhneLaufdaten` | `KiAusfuehrungsStatusConverterTests` | `AktiveAufgabePanelItem` mit `LiveLaufStatus = Laeuft` + `AktiveRunId = null` + `AusfuehrungsStatus = Beendet` → „▶ Läuft" (F1-Bild) |
| `Klassifiziere_ZeigtWartet_BeiLiveLaufStatusWartetAufEingabe` | `KiAusfuehrungsStatusConverterTests` (+ Symbol-Pendant) | `LiveLaufStatus = WartetAufEingabe` → „⏸ Wartet" |
| `Klassifiziere_LaesstLiveNichtVorrangVorPausiertUndWartestellung` | `KiAusfuehrungsStatusConverterTests` | `LiveLaufStatus` gesetzt + `PausiertBisUtc`/`HasScheduledPrompt` → `Pausiert`/`PromptInWartestellung` bleibt vorrangig |
| `Klassifiziere_ZeigtBereit_BeiLiveLaufStatusNull` | `KiAusfuehrungsStatusConverterTests` | `LiveLaufStatus = null` ändert nichts an bestehendem `Bereit`-Verhalten (kein Downgrade-Pfad nötig) |
| `MapAktiveAufgabePanelItem_BefuelltLiveLaufStatus` | `MainWindowViewModelTests` | Mock `GetLiveLaufStatus` → Item trägt den Wert |
| `CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess` | `TaskDetailViewModelTests` | `StopCliAsync` mit anschließend noch laufendem Prozess → kein `AktivenLaufBeendenAsync`, `FehlerMeldung` gesetzt, `IsCliRunning` bleibt (ggf. OsInterface/Integration mit echtem, nicht beendbar simulierbarem Prozess — sonst als Code-Review-Nachweis, da `KiAusfuehrungsService` sealed ist) |
| `StartCliAndUpdateStateAsync_SchreibtVolleLaufdaten` | `TaskDetailViewModelTests` | Reuse-Pfad (`IsRunning` true nach `CliNeustartenAsync`) → `AktiveRunId`/`LastHeartbeatUtc`/`LaufStatus` persistiert statt nur `AusfuehrungsStatus` (F2) |
| `CliNeustartenCoordinated_MeldetLaufAktiv_BeiLebenderSession` | `EntwicklungsprozessServiceTests` | Early-Return bei lebender Session → `CliLaufAktiv` gefeuert (via `TestKiAusfuehrungsServiceFactory`-Instanz mit abonniertem Event) |
| `LaufdatenSicherstellenAsync_WithDisposedScopeFactory_Completes` | `CliProcessManagerTests` | Shutdown-Race-Pendant für den neuen Persistenzpfad (ersetzt den angepassten `AktualisiereLaufStatusAsync_WithDisposedScopeFactory_Completes`, siehe „Betroffene bestehende Tests"): `LaufdatenSicherstellenAsync` muss die `ObjectDisposedException` der disposed `IServiceScopeFactory` in `ExecuteWithAufgabeServiceAsync` schlucken → Task completed. Erfordert vorab ein laufendes Handle in `KiAusfuehrungsService._handles` (z. B. `CliProcessHandle` mit `Process.GetCurrentProcess()`, Muster siehe `PromptZeitVersandServiceTests.RegisterSession`/`CliProcessManagerTests_LaufStatus.RegisterFakeSessionForAufgabe`), da `LaufdatenSicherstellenAsync` sonst am `IsRunning`-Guard früh zurückkehrt und der Persistenzpfad nie erreicht wird |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2EFakeRunningStatusSource` (`RecoveryServiceIntegrationTests.cs`), `FakeRunningAutomationStatusSource` + `ThrowingRunningAutomationStatusSource` (`AufgabeRecoveryServiceTests.cs`), `FakeRunningAutomationStatusSource` (`AutoShutdownOrchestratorTests.cs`), `AlwaysNotRunningAutomationStatusSource`/`AlwaysRunningAutomationStatusSource`/`MutatingNotRunningAutomationStatusSource`/`BarrieredNotRunningStatusSource` (`Softwareschmiede.IntegrationTests/Services/AufgabeRecoveryServiceTests.cs`) | Interface-Erweiterung: `GetLiveLaufStatus` implementieren (laufende Fakes → `Laeuft`, übrige → `null`) |
| `CliProcessManagerTests_LaufStatus.VollerZyklus_GestartetWartetGestoppt_PersistiertLaufStatusUeberDieSitzung` | `OnRuntimeStatusChanged` ruft nun `AktivenLaufSicherstellenAsync` — Sollverhalten identisch (LaufStatus-Persistenz), aber auf geänderte Aufrufkette/Erwartungen prüfen |
| `CliProcessManagerTests.AktualisiereLaufStatusAsync_WithDisposedScopeFactory_Completes` (`CliProcessManagerTests.cs:106–114`) | Ruft die zu entfernende private `CliProcessManager.AktualisiereLaufStatusAsync` per Reflection auf (`GetMethod` → `null` → `NullReferenceException`). Durch das Shutdown-Race-Pendant `LaufdatenSicherstellenAsync_WithDisposedScopeFactory_Completes` ersetzen (siehe „Neue Tests"): Hilfsmethode `InvokePersistierungsMethodeMitDisposedScopeFactoryAsync` (Z. 116–133) bleibt für `AktivenLaufSetzenAsync`/`AktivenLaufBeendenAsync` nutzbar und wird um das Pflanzen eines laufenden Handles in `_handles` erweitert (notwendig, damit der `IsRunning`-Guard von `LaufdatenSicherstellenAsync` den Persistenzpfad erreicht) |
| `CliProcessManagerTests_LaufStatus.RaisesRuntimeStatusChanged_AfterGestoppt_AktualisiertLaufStatusNichtMehr` | Mitprüfen bei der Umsetzung: sollte unverändert grün bleiben (nach `Gestoppt` ist die Session abgemeldet und `IsRunning` `false` → `LaufdatenSicherstellenAsync` No-Op), aber die geänderte Aufrufkette in `OnRuntimeStatusChanged` ist zu verifizieren |
| `MainWindowViewModelTests` (bestehende Mapping-Assertions) | `MapAktiveAufgabePanelItem` befüllt `LiveLaufStatus` (Mock-Default `null` → unverändert; nur bei neuem Assertion-Umfang anpassen) |

**Reflection-Abgleich (verifiziert, keine weiteren Anpassungen nötig):** Alle übrigen Reflection-Zugriffe der Testsuite auf `CliProcessManager`/`KiAusfuehrungsService`/`TaskDetailViewModel` zielen auf Member, deren Name und Signatur unverändert bleiben: `OnCliProcessStatusChanged` (`CliProcessManagerTests_LaufStatus.cs:129`, `CliProcessManagerTests_AktiverLauf.cs:92`), `_updateSemaphores` + `AktualisierungAsync` (`CliProcessManagerTests.cs:48–82` — die Semaphore-Serialisierungs-Assertions sind vom Methodenrumpf unabhängig), `KiAusfuehrungsService._handles` + `CliProcessStatusChanged`-Backing-Field (`PromptZeitVersandServiceTests.cs:64,72`), `TaskDetailViewModel.OnCliProcessStatusChanged`/`_isCliRunning`/`_aufgabeId`/`LadeProtokolleAsync` (`TaskDetailViewModelTests*.cs`), `PseudoConsoleSession.RefreshRuntimeStatus`/`_readLoopTask` (Terminal-Tests).

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Konsolidierte Erweiterung von `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E` (`E2E_ArbeitsstatusAktualisierung.cs`, eingebunden im `ConPty_WeitereLebenszyklusSzenarien_E2E`): Nach „▶ Läuft" wird die Aufgabendetailansicht verlassen und erneut geöffnet (Session-Wiederanbindung über `LadenAsync`/`AttachCliStatusSession` mit lebender Session) → Fußzeile „CLI-Status: Ausführung läuft" **und** Kachel „▶ Läuft" bleiben synchron; erst danach Stopp → „✓ Bereit". | `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs` | Fußzeile ↔ Kachel semantisch synchron über Wiederanbindung und Stopp hinweg (Akzeptanzszenarien 1, 2, 6) | Nur mit echtem laufendem CLI-Prozess + echter Seitenleisten-Liste ist der komplette Live-↔-Persistenz-Abgleich sichtbar; Unit-Tests prüfen die Ketten getrennt |
| Optional (Sandbox-limitiert) | Divergenz-Nachspiel: Kachel „▶ Läuft" obwohl `AktiveRunId`/`LastHeartbeatUtc` per direktem DB-Zugriff während des Laufs entfernt wurden (F1/H4-Bild) — Live-Zweig + Selbstheilung müssen synchron bleiben bzw. heilen. | `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs` (gleiche konsolidierte Methode, zwischen „▶ Läuft" und Stopp) | Akzeptanzszenarien 3–5 | Nachweis, dass Anzeige auch bei korruptem Persistenzzustand die Realität abbildet; nur in einer Sitzung mit lebender Session sinnvoll auslösbar |

**Hinweis zur Ausführbarkeit:** Der relevante E2E hängt im ConPTY-Bundle und läuft in dieser Agent-Sandbox nicht (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` → sauberer „Skipped"). Der E2E-Nachweis ist interaktiv (Visual Studio / lokale Desktop-Session) zu führen; in der Sandbox dienen Unit-/Integrationstests als Nachweis.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `End2EndTest` / `E2E_ArbeitsstatusAktualisierung.cs` | Erweiterung der bestehenden konsolidierten Methode um die Szenarien oben (keine neue Testmethode — CLAUDE.md FlaUI-Konsolidierung) |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Start ↔ Stopp synchron | Nutzer klickt „Aufgabe starten" (Claude-CLI) | Aufgabe `Neu`/`Beendet-Ausführung`, kein Handle, keine Laufdaten | `StartenAsync` → `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync` → `Gestartet`-Event → `AktivenLaufSetzenAsync` + `LaufdatenChanged` → Kachel-Refresh; `RuntimeStatusChanged` → `LaufStatus` | Fußzeile „CLI-Status: Ausführung läuft"/„Wartet auf Eingabe" ↔ Kachel „▶ Läuft"/„⏸ Wartet"; nach Stopp beide „✓ Bereit"/„CLI inaktiv" | Unit (`CliProcessManagerTests_AktiverLauf`, `AufgabeServiceTests_AktiverLauf`) + E2E (konsolidierte Methode) |
| 2 | Session-Wiederverwendung ohne `Gestartet` (H1) | Zweiter Startimpuls bei lebender Session (Nutzer-Aktion, `ScmEventOrchestrator`, `CliNeustartenCoordinatedAsync`-Alive-Check) | Handle lebt, Session läuft; `AktiveRunId` fehlt/veraltet | Early-Return → `CliLaufAktiv` (bzw. `NotifyLaufAktiv`) → `LaufdatenSicherstellenAsync` → `AktivenLaufSicherstellenAsync`; parallel greift der Live-Zweig der Klassifikation | Kachel „▶ Läuft"/„⏸ Wartet" (sofort über Live-Zweig, persistiert binnen Sekunden); Fußzeile unverändert live | `CliLaufAktiv_StelltLaufdatenSicher_BeiWiederverwendung`, `CliNeustartenCoordinated_MeldetLaufAktiv_BeiLebenderSession`, `ZweiterStart_LeuftetCliLaufAktiv_StattGestartet` |
| 3 | Verschluckter Persistenzfehler (H2) | `AktivenLaufSetzenAsync` wirft (z. B. DB-Fehler) im `Gestartet`-Zweig | Prozess läuft, Laufdaten fehlen | Fehler wird geloggt; nächster Heartbeat-Tick/`RuntimeStatusChanged`/30-s-Abgleich → `LaufdatenSicherstellenAsync` heilt | Kachel zeigt „▶ Läuft" spätestens am nächsten Refresh (Live-Zweig) — nie dauerhaft „✓ Bereit" | `AktualisierungAsync_HeiltGeloeschteLaufdaten_BeiLaufendemProzess`, `OnRuntimeStatusChanged_HeiltFehlendeAktiveRunId` |
| 4 | Stopp ohne Prozessende (F1) | Nutzer klickt „CLI stoppen", Prozess überlebt | Laufende Session, Laufdaten gesetzt | `StopCliAsync` → `IsRunning` noch `true` → `FehlerMeldung`, Laufdaten/Fußzeile bleiben | Fehlermeldung in Detailansicht; Kachel + Fußzeile bleiben synchron „▶ Läuft"/„Ausführung läuft" | `CliStoppenAsync_BehaeltLaufdaten_BeiWeiterlaufendemProzess` + `Klassifiziere_ZeigtLaeuft_BeiLiveLaufStatus_OhneLaufdaten` |
| 5 | Heartbeat-Ausfall bei lebendem Prozess (H4) | Transiente `HasExited`-Exception stoppt Heartbeat-Timer | Prozess lebt, `LastHeartbeatUtc` altert > 5 min, `AktiveRunId` gesetzt | `IstAktiv` false → ohne Fix „✓ Bereit"; mit Fix: Live-Zweig „▶ Läuft"; Abgleich/`RuntimeStatusChanged` stellt Laufdaten + Timer wieder her | Kachel bleibt „▶ Läuft"/„⏸ Wartet"; Laufdaten konvergieren | `AbgleichAsync_StelltLaufdatenSicher_FuerLaufendeAufgaben`, `Klassifiziere`-Live-Tests |
| 6 | Tatsächlich beendeter/verwaister Lauf (Gegenrichtung) | Prozess endet (Exit/`Failed`) bzw. verwaiste Laufdaten ohne Prozess | Handle entfernt bzw. nie vorhanden; `AktiveRunId` evtl. alt | `Gestoppt`/`Fehler` → `AktivenLaufBeendenAsync`; oder Heartbeat > 5 min bei `IsRunning` false → `IstAktiv` false; `GetLiveLaufStatus` `null` | Kachel „✓ Bereit", Fußzeile „CLI inaktiv"/„CLI-Status: Fehler"; verwaister Zustand wird **nicht** wieder aktiv | Bestehende `CliProcessManagerTests_AktiverLauf` (Gestoppt/Fehler), `AufgabeLaufAktivitaetTests`, `Klassifiziere_ZeigtBereit_BeiLiveLaufStatusNull` |

## Offene Punkte

Keine. Die in `requirement.md` gelisteten Annahmen wurden in die Planung übernommen: Live-Session ist die führende Wahrheit für „läuft" (Frage 2), der Feinsubstatus wird ebenfalls synchronisiert (Frage 3), „synchron" bedeutet semantische Übereinstimmung ohne Textidentität (Frage 4), Dashboard- und Seitenleisten-Kachel teilen die `AktiveAufgabenListe` und sind damit gleichermaßen abgedeckt (Frage 6). Der nicht entscheidbare Reproduktionspfad (Frage 1) ist durch die strukturelle Behebung aller identifizierten Pfade abgedeckt; die Fehler-Heilung erfolgt strukturell über Wiederholung statt neuer Fehler-UI (Frage 5, Scope-Ausschluss 4).
