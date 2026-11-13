# Datenmodell — Shutdown bei Abschluss

## `AppEinstellung`
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

Generische Key-Value-Entität für nicht-geheime Anwendungseinstellungen; Tabelle `AppEinstellungen` (`SoftwareschmiededDbContext.AppEinstellungen`, `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` Z. 61). Zugriff ausschließlich über `AppEinstellungService`. Für die Anforderung sind zwei neue Schlüssel (`autoshutdown.force`, `autoshutdown.toleranz.minuten`) vorgesehen — keine neue Entität/Migration nötig.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Einstellung. |
| `Schluessel` | `string` | Maschinenlesbarer Schlüssel, z. B. `window.position.x`, `scm.taskcompletion.wait.minutes`. |
| `Wert` | `string?` | Optionaler Wert; `null`/leer = Default verwenden (Default-Logik liegt in den typsicheren Get-Methoden des Services). |
| `AktualisiertAm` | `DateTimeOffset` | Zeitpunkt der letzten Aktualisierung; wird bei jedem `SetSettingAsync` auf `UtcNow` gesetzt. |

## `Aufgabe` (nur laufbezogene Eigenschaften)
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Vollständige Entität siehe Quelldatei; hier nur die für die Lauf-/Statusklassifikation relevanten Eigenschaften.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aufgaben-ID; Schlüssel der Handle-Map im `KiAusfuehrungsService`. |
| `Status` | `AufgabeStatus` | Lebenszyklusstatus (`Neu`/`Gestartet`/`Wartend`/`Beendet`/`Archiviert`); `Wartend` = Rate-Limit-Warten, zählt in der Klassifikation als „Wartet". |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Status der KI-Ausführung (`NichtGestartet`/`Aktiv`/`Beendet`); `Aktiv` ist Voraussetzung für den Persistenz-Zweig der Klassifikation. |
| `AktiveRunId` | `string?` | Aktive Lauf-ID; gesetzt durch `CliProcessManager`/`AufgabeService.AktivenLaufSetzenAsync` beim CLI-Start. |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat; Aktivitätsbeleg zusammen mit `AktiveRunId` (`AufgabeLaufAktivitaet.IstAktiv`, Timeout 5 min). |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Letzter echter CLI-Start; Sortierkriterium der aktiven Liste. |
| `LaufStatus` | `AufgabeLaufStatus?` | Persistierter Laufzeit-Substatus (`Laeuft`/`WartetAufEingabe`), geschrieben von `CliProcessManager` aus `ITerminalSession.RuntimeStatusChanged`. |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausenende; Zukunft → Klassifikation `Pausiert` (überlagert alle übrigen Stati). |
| `ScmAbschlussErkanntUtc` | `DateTimeOffset?` | Persistierter Countdown-Startpunkt der SCM-Abschlussüberwachung — Vorbild für „Toleranzzeit-Countdown überlebt Neustart" (für den Shutdown-Countdown nicht gefordert). |
| `RecoveryVersion` | `int` | Concurrency-Token für recovery-relevante Statusänderungen. |

## `AktiveAufgabePanelItem`
Datei: `src/Softwareschmiede/App/ViewModels/AktiveAufgabePanelItem.cs`

UI-Modell eines Seitenleisten-Eintrags; wird in `MainWindowViewModel.MapAktiveAufgabePanelItem` (Z. 366–411) aus `Aufgabe` + Live-Daten befüllt. `KiAusfuehrungsStatusKlassifikation.Klassifiziere` akzeptiert diesen Typ direkt — die fachliche „läuft"-Zählung ist damit über die vorhandene Liste ableitbar.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aufgaben-ID. |
| `Status` | `AufgabeStatus` | Aktueller Aufgabenstatus. |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Ausführungsstatus (Default `NichtGestartet`). |
| `AktiveRunId` | `string?` | Aktive Lauf-ID. |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat. |
| `LaufStatus` | `AufgabeLaufStatus?` | Persistierter Laufzeit-Substatus. |
| `LiveLaufStatus` | `AufgabeLaufStatus?` | Live-Substatus aus `IRunningAutomationStatusSource.GetLiveLaufStatus`; `null` = kein laufender gehandelter Prozess. |
| `PausiertBisUtc` | `DateTimeOffset?` | Pausenende (Zukunft = pausiert). |
| `IstPausiert` | `bool` | Abgeleitet: `PausiertBisUtc > DateTimeOffset.UtcNow`. |
| `HasScheduledPrompt` | `bool` | Zeitgesteuerter Prompt in Wartestellung (aus `PromptZeitVersandService.GetScheduledPromptStatus`). |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Sortierzeitpunkt-Bestandteil. |
| `SortierZeitpunkt` | `DateTimeOffset` | `LetzterCliStartUtc ?? ErstellungsDatum`. |
| `IsAktiv` | `bool` (settable) | Markierung der aktuell in der Detailansicht geöffneten Aufgabe. |
| `OffeneTodoCount` / `OffeneTodoLabelText` / `OffeneTodosAnzeigenCommand` | `int` / `string` / `ICommand?` | To-do-Badge der Kachel. |
| `Titel`, `RepositoryName`, `RepositoryGruppenName`, `KiGruppenName`, `ScmPluginName/Symbol`, `KiPluginName/Symbol`, `*IstFallback` | div. | Anzeige-/Gruppierungsdaten der Kachel (für die Anforderung nicht direkt relevant). |

## `ScheduledPromptInfo`
Datei: `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs`

Record für den Wartestellungs-Status eines geplanten Prompts (`AufgabeId`, `PromptText`, `TargetTime`, `WartetAufSession`). Relevant als Muster, wie der `MainWindowViewModel` pro Aufgabe einen geplanten Zustand abfragt (`GetScheduledPromptStatus` → `HasScheduledPrompt` → Klassifikation `PromptInWartestellung`).

## `ZeitgesteuerterPrompt`
Datei: `src/Softwareschmiede/Domain/Entities/ZeitgesteuerterPrompt.cs`

Persistente Entität für geplante Prompts (`Id`, `AufgabeId`, `PromptText`, `ZielZeitpunktUtc`, `ErstelltAmUtc`, `AktualisiertAmUtc`), Store: `ZeitgesteuerterPromptStore`. Belegt, dass fachliche Timer-Zustände im Projekt per EF-Tabelle persistiert werden — für Force/Toleranz ist dagegen `AppEinstellung` vorgesehen (Anforderung, Konfiguration-Abschnitt).

## `CliProcessHandle`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (ab Z. 848)

Laufzeit-Handle pro Aufgabe im `KiAusfuehrungsService` (`_handles`-Map).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `CliRunId` | `Guid` | Neue Identität pro tatsächlichem Prozessstart. |
| `AufgabeId` | `Guid` | Zugehörige Aufgabe. |
| `Process` | `Process` | Verwalteter Prozess (`!HasExited` = „läuft" im `GetRunningCount`-Sinne). |
| `LastHeartbeat` | `DateTimeOffset` | Letzter Heartbeat (via `UpdateHeartbeat`). |
| `AbsichtlichGestoppt` | `bool` | Status-Mapping beim Prozessende (`Gestoppt` vs. `Fehler`). |
| `Session` | `ITerminalSession?` | Terminal-Session bei interaktivem Start (Quelle des Live-Substatus). |
| `OutputSink` | `ITerminalOutputSink?` | Ausgabe-Senke. |
| `ScmPromptSession` | `ScmPromptSessionAdapter?` | SCM-Vertrag des interaktiven Starts. |

## `MainWindowOptionaleDienste` / `MainWindowUpdateDienste`
Dateien: `src/Softwareschmiede.App/ViewModels/MainWindowOptionaleDienste.cs`, `src/Softwareschmiede.App/ViewModels/MainWindowUpdateDienste.cs`

Records zum Bündeln optionaler ViewModel-Abhängigkeiten, damit Konstruktor-Parameterlisten schmal bleiben und Tests gezielt injizieren können. `MainWindowOptionaleDienste` enthält `DispatcherInvoke`, `DialogService`, `VersionProvider`, `LaufdatenChangedNotifier`, `UpdateDienste`, `AufgabenListenModusService`; `MainWindowUpdateDienste` enthält `UpdateService`, `CliUpdateSafetyService`, `UpdateProgressDialogService`, `VersuchProtokoll`. Beide werden in `App.xaml.cs` (Z. 321–331) als `AddTransient`-Factory-Aggregate aus dem Container befüllt — das etablierte Erweiterungsmuster für neue VM-Abhängigkeiten (z. B. Orchestrator/Einstellungsdienst).

## `UpdateE2ETestKontext` / `UpdateE2ETestConfiguration`
Dateien: `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs`, `UpdateE2ETestConfiguration.cs`

Laufzeitkontext der Update-E2E-Umgebung: statische Konfiguration (`Testwurzel`, `SzenarioDateiPfad`, `ProtokollDateiPfad`, `SzenarioId`, `GatesVerzeichnis`), JSONL-Protokoll (`UpdateE2EProtokoll` mit Ereignis-Konstanten in `UpdateE2EEreignisse`, u. a. `ShutdownRequested`, Z. 69), dateibasierte Freigabe-Gates. Aktivierung ausschließlich über zwei Umgebungsvariablen: `SOFTWARESCHMIEDE_TEST_DB_PATH` + `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` (JSON-Pfad). Registrierung der Fakes in `App.xaml.cs` Z. 385–412. Referenzmuster für einen aufzeichnenden `ISystemShutdownService`-Fake.
