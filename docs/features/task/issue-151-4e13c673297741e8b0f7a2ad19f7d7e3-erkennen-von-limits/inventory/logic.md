# Logik

## `ProtokollService`
Datei: `src/Softwareschmiede/Application/Services/ProtokollService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByAufgabeAsync` | public | Protokolleinträge einer Aufgabe laden (Z. 25) |
| `AddEintragAsync` | public | Generischen Eintrag anlegen (Z. 37) |
| `AddTestErgebnisseAsync` | public | Testergebnis-Eintrag anlegen (Z. 63) |
| `AddStatusUebergangAsync` | public | Statusübergangs-Eintrag anlegen (Z. 104) |
| `AddCliOutputAsync` | public | CLI-Ausgabezeile als `ProtokollTyp.CliOutput` speichern; bei Rate-Limit-Marker zusätzlich `ProtokollTyp.RateLimit`-Eintrag (Z. 128) |
| `ParseRateLimitMarker` | public static | Parst Marker; Rückgabe `(bool Found, string? Prompt, DateTimeOffset? ResetUtc)` (Z. 167) |
| `TryParseRateLimitMarker` | public static | Parst `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO>]]`; `true` bei gefundenem Marker, `resetUtc` nullable bei fehlendem/ungültigem Zeitstempel (Z. 182) |
| `SuchenAsync` | public | Volltextsuche in `Inhalt`/`AgentName` (Z. 217) |

Details zu `AddCliOutputAsync` (Z. 128–161):
- Legt immer einen `CliOutput`-Eintrag an.
- Bei `TryParseRateLimitMarker(...) == true` wird ein zweiter Eintrag mit `Typ = ProtokollTyp.RateLimit` und Inhalt „Rate-Limit erkannt. Weiter ab: {resetUtc:O}" bzw. „Rate-Limit erkannt (kein Zeitstempel)." angelegt; Log-Information mit `ResetUtc`.
- **Keine** Plugin-weite Persistenz des Reset-Zeitpunkts, **keine** Statusänderung der Aufgabe, **keine** Pausierung anderer Aufgaben.

Details zu `TryParseRateLimitMarker` (Z. 182–213): Sucht `RateLimitMarkerPrefix` (`[[SOFTWARESCHMIEDE_RATE_LIMIT`), dann schließendes `]]`; Payload mit führendem `:` wird via `DateTimeOffset.TryParse` (`InvariantCulture`, `AssumeUniversal|AdjustToUniversal`) geparst. Rückgabe `true` auch ohne gültigen Zeitstempel.

Publizierte Events: keine. Abonnierte Events: keine.

## `CliOutputProtokollWriter`
Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `QueueCapacity` | internal const | Pufferkapazität 4096 Chunks (Z. 12) |
| Konstruktor | public | `CliOutputProtokollWriter(Guid aufgabeId, IServiceScopeFactory, ILogger)` (Z. 37) |
| `OnOutputChunk` | public | Nimmt rohe Ausgabe-Bytes entgegen (`ITerminalOutputSink`), queued zeilenbasiert (Z. 46) |
| `Complete` | public | Idempotenter Abschluss, Flush der Restdaten (Z. 59) |
| `CompleteAsync` | public | Abschluss mit Drain-Timeout (Z. 68) |

Datenfluss: `PseudoConsoleSession` → `CliOutputProtokollWriter` (ITerminalOutputSink) → asynchroner Persistenz-Loop → `ProtokollService.AddCliOutputAsync` pro Zeile. Der Rate-Limit-Marker wird also zeilenweise auf dem Persistenzpfad erkannt — nicht im Prozess-Handle selbst.

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Zentrale, anforderungsrelevante Member:

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IstAktivOderWartendPredicate` | private static readonly | `Expression<Func<Aufgabe,bool>>` auf Basis von `AufgabeStatusExtensions.AktivOderWartendStatus` (Z. 20) |
| `GetAktiveUndWartendeCountAsync` | public | Zählt aktive bzw. wartende Aufgaben; wird u. a. von `DashboardViewModel` genutzt (Z. 64) |
| `StartenAsync` | public | Setzt `Status = Gestartet`, `BranchName`, `LokalerKlonPfad`, `BasisBranchName` (Z. 696) |
| `SavePromptVorschlagAsync` | public | Speichert `VorschlagPrompt`/`VorschlagAusfuehrenAbUtc` (Z. 721) |
| `ClearPromptVorschlagAsync` | public | Leert Vorschlagsprompt (Z. 741) |
| `AbschliessenAsync` | public | Setzt `Status = Beendet` (Z. 755) |
| `SetStatusAsync` | public | Statuswechsel **mit** `ValidateStatusTransition` (Z. 776) |
| `StatusSetzenAsync` | public | Statuswechsel **ohne** Transitions-Validierung (Z. 792) |
| `StartZuruecksetzenAsync` | public | Setzt Aufgabe auf `Neu` zurück (Z. 806) |
| `UpdateHeartbeatAsync` | public | Setzt `LastHeartbeatUtc = UtcNow` (Z. 825) |
| `AusfuehrungAktivSetzenAsync` | public | Setzt `AusfuehrungsStatus = Aktiv` (Z. 835) |
| `AktivenLaufSetzenAsync` | public | Setzt `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc`; von `CliProcessManager` aufgerufen (Z. 858) |
| `AktivenLaufBeendenAsync` | public | Leert `AktiveRunId`/`LaufStatus`; von `CliProcessManager` aufgerufen (Z. 886) |
| `AktualisiereLaufStatusAsync` | public | Persistiert `LaufStatus` (Z. 911) |
| `GetHeartbeatAgeMinutesAsync` | public | Heartbeat-Alter in Minuten (Z. 927) |
| `GetAktiveAufgabenAsync` | public | Aktive/wartende Aufgaben für die Seitenleiste (`IstAktivOderWartendPredicate`, `Take(20)`, sortiert nach `LetzterCliStartUtc`/`ErstellungsDatum`); implementiert `IAktiveAufgabenService` (Z. 946) |
| `CanCompleteTaskAsync` | public | `true` wenn keine offenen To-Dos (Z. 964) |
| `ValidateStatusTransition` | private static | Erlaubte Übergänge: `Neu→Gestartet`; `Gestartet→Wartend|Beendet`; `Wartend→Gestartet|Beendet`; `Beendet→—`; `Archiviert→—`; zusätzlich `→Archiviert` von jedem Status; wirft `InvalidStatusTransitionException` (Z. 970) |

**Anforderungsrelevanz:** `Wartend` ist bereits ein gültiger Zwischenstatus mit Rückweg nach `Gestartet`. Es existiert keine Methode, die Aufgaben anhand eines Zeitstempels automatisch pausiert/reaktiviert, und keine Methode, die Aufgaben nach `KiPluginPrefix` gruppiert abfragt.

## `AufgabeLaufAktivitaet`
Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufAktivitaet.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IstAktiv` | public static | `true`, wenn `aktiveRunId != null` und `lastHeartbeatUtc != null` und Heartbeat-Alter < `AufgabeRecoveryService.HeartbeatTimeoutMinutes` (5 Min.) (Z. 14) |

Verwendet von: `CliUpdateSafetyService.CheckAsync`, `KiAusfuehrungsStatusConverter`, `MainWindowViewModel`-Anzeigepfad (indirekt via Converter).

## `AufgabeRecoveryService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `HeartbeatTimeoutMinutes` | public const | `= 5` (Z. 19) |
| `ScanForRecoveryCandidatesAsync` | public | Findet aktive/wartende Aufgaben mit `AusfuehrungsStatus == Aktiv`, `LastHeartbeatUtc < now - 5min`, ohne `AutonomKonfiguration` und ohne laufenden Prozess (`IRunningAutomationStatusSource.IsRunning`) (Z. 39) |
| `RecoverManuellAsync` | public | Manuelle Recovery, nur aus `Gestartet`/`Wartend` mit aktivem Ausführungsstatus, nicht-autonom (Z. 60) |
| `IstRecoveryStatus` | internal static | `status.IstAktivOderWartend() && AusfuehrungsStatus == Aktiv && !istAutonom` (Z. 223) |

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning` | public | Prüft, ob für `aufgabeId` ein Prozess-Handle existiert und läuft (Z. 49) |
| `GetRunningCount` | public | Anzahl laufender CLI-Prozesse (Z. 72) |
| `StartCliAsync` | public | Startet CLI-Prozess (`aufgabeId`, `IKiPlugin`, `localRepoPath`, `optionalParameters`, `startConfig`, `gitPlugin`); `_startLock`-Serialisierung; liefert `CliProcessHandle` (Z. 93) |
| `StartWithPseudoConsoleAsync` | public | Startet CLI über ConPTY-`PseudoConsoleSession` (Z. 182) |
| `GetPseudoConsoleSession` | public | Liefert die ConPTY-Session einer Aufgabe (Z. 286) |
| `StopCliAsync` | public | Beendet Prozess: `AbsichtlichGestoppt = true`, `CloseMainWindow()`, nach 5 s `Kill(entireProcessTree: true)` (Z. 296) |
| `GetLastExitCode` | public | Letzter Exit-Code (Z. 333) |
| `UpdateHeartbeat` | public | In-memory Heartbeat-Update (Z. 345) |
| `Dispose` | public | Beendet alle verwalteten Prozesse (Z. 354) |

Publizierte Events: `CliProcessStatusChanged` (`CliProcessStatus.Gestartet/Gestoppt/Fehler`) — abonniert von `CliProcessManager` und `TaskDetailViewModel`.
Implementiert `IRunningAutomationStatusSource` (`RunningCountChanged`, `GetRunningCount`, `IsRunning`).
Innerer Typ `CliProcessHandle` (Z. 678 ff.): `AufgabeId`, `Process`, `AbsichtlichGestoppt`, `PseudoConsoleSession`.

**Anforderungsrelevanz:** Es gibt keinen „Pausieren"-Pfad — nur Start/Stop. Laufende Prozesse werden ausschließlich über `StopCliAsync`/`Dispose` beendet; die Rate-Limit-Erkennung greift nicht in den Prozess ein (Anforderung „kein Unterbrechen laufender CLI-Prozesse" ist damit faktisch Ist-Zustand).

## `CliProcessManager`
Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `HeartbeatInterval` | private const | 30 s (Z. 21) |
| `StartHeartbeat` | public | Startet Heartbeat-Timer pro Aufgabe (Z. 52) |
| `StopHeartbeat` | public | Stoppt Timer, disposed Semaphore (Z. 70) |
| `AktualisierungAsync` | private | Timer-Tick: `IsRunning`-Prüfung, `UpdateHeartbeat`, `AufgabeService.UpdateHeartbeatAsync` (Z. 84) |
| `OnCliProcessStatusChanged` | private | Bei `Gestartet`: Heartbeat + `AktivenLaufSetzenAsync` + `SubscribeRuntimeStatus`; bei `Gestoppt`/`Fehler`: `StopHeartbeat` + `AktivenLaufBeendenAsync` (Z. 125) |
| `SubscribeRuntimeStatus`/`UnsubscribeRuntimeStatus` | private | Abonniert `PseudoConsoleSession.RuntimeStatusChanged` (Z. 191/216) |
| `OnRuntimeStatusChanged` | private | Übersetzt `CliRuntimeStatus` in `AufgabeLaufStatus` und persistiert via `AktualisiereLaufStatusAsync`; `Inaktiv` wird ignoriert (Z. 233) |
| `ExecuteWithAufgabeServiceAsync` | private | Scoped `AufgabeService`-Aufruf + `AufgabeLaufdatenChangedNotifier.NotifyLaufdatenChanged` (Z. 256) |
| `Dispose` | public | Meldet Event ab, räumt Timer/Semaphores/Subscriptions auf (Z. 287) |

Abonnierte Events: `KiAusfuehrungsService.CliProcessStatusChanged`, `PseudoConsoleSession.RuntimeStatusChanged`.
Publizierte Events: keine eigenen (notifiziert `AufgabeLaufdatenChangedNotifier`).

## `CliUpdateSafetyService`
Datei: `src/Softwareschmiede/Application/Services/Updates/CliUpdateSafetyService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckAsync` | public | Lädt `GetAktiveAufgabenAsync`, filtert per `AufgabeLaufAktivitaet.IstAktiv` auf „riskante" Aufgaben, liefert `CliUpdateSafetyResult(count, titelListe)` (Z. 19) |

Implementiert `ICliUpdateSafetyService`. Wartende (nicht aktiv laufende) Aufgaben werden bereits heute nicht als riskant gewertet, da `IstAktiv` frischen Heartbeat + `AktiveRunId` verlangt.

## `SessionManagementService` (Referenz: Autonome Aufgaben)
Datei: `src/Softwareschmiede/Application/Services/SessionManagementService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PauseAufgabeBeiBudgetLimitAsync` | public | Setzt `AutonomKonfiguration.SessionPauseUtc = UtcNow`, speichert, schreibt `pausedUtc` in `state.json` (Z. 24) |
| `SetzeFortAsync` | public | Leert `SessionPauseUtc`, setzt `AusfuehrungsStatus = Aktiv`, erzeugt „Weitermachen"-`VorschlagPrompt` mit `VorschlagAusfuehrenAbUtc = UtcNow` (Z. 50) |
| `PruefeAusfuehrungAsync` | public | `SessionPauseUtc != null` → gilt als pausiert/valide (`return true`); sonst Heartbeat-Timeout-Prüfung mit Unterbrechungs-Prompt (Z. 80) |
| `AktualisierePausedUtcInStateJsonAsync` | private | Schreibt `pausedUtc` in die state.json des Arbeitsverzeichnisses (Z. 130) |

**Anforderungsrelevanz:** Referenzmechanismus für persistierte Session-Pause — jedoch nur für Autonome Aufgaben (`AutonomKonfiguration`-Pflicht), kein regulärer `Aufgabe`-Pausenpfad.

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStoredDefaultPluginPrefixAsync` | public | Gespeicherter Default-Prefix je `PluginType` (Z. 35) |
| `SaveDefaultPluginPrefixAsync` | public | Default-Prefix speichern (Z. 39) |
| `SaveProjectDefaultPluginPrefixAsync` | public | Projekt-Default-Prefix speichern (Z. 43) |
| `ResolveSourceCodeManagementPluginAsync` | public | SCM-Plugin auflösen (Z. 47) |
| `GetAvailableKiPluginPrefixesAsync` | public | Prefixes aller **aktivierten** KI-Plugins via `PluginActivationService` (Z. 62) |
| `ResolveDevelopmentAutomationPluginAsync` | public | KI-Plugin auflösen: expliziter Prefix → gespeicherter Default → Fallback `GetDefaultDevelopmentAutomationPlugin` (Z. 73) |
| `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` | public | Wie oben, zusätzlich Projekt-Default (Z. 92) |
| `ResolveIdePluginAsync` | public | IDE-Plugin für Repository-Pfad (Z. 124) |
| `ResolveAlleKompatiblenIdePluginsAsync` | public | Alle kompatiblen IDE-Plugins (Z. 160) |

## `PluginActivationService`
Datei: `src/Softwareschmiede/Application/Services/PluginActivationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnabledKeyPrefix` | private const | `"plugins.enabled."` — `AppEinstellung`-Schlüsselpräfix (Z. 10) |
| `IsPluginEnabledAsync` | public | Liest `plugins.enabled.<prefix>`; fehlender Eintrag = aktiviert (Z. 30) |
| `SetPluginEnabledAsync` | public | Schreibt Enabled-Flag (Z. 40) |
| `GetEnabledSourceCodeManagementPluginsAsync` | public | Gefilterte SCM-Liste (Z. 53) |
| `GetEnabledDevelopmentAutomationPluginsAsync` | public | Gefilterte KI-Liste (Z. 59) |
| `GetEnabledIdePluginsAsync` | public | Gefilterte IDE-Liste (Z. 65) |

**Anforderungsrelevanz:** Zeigt die etablierte Konvention, Plugin-Laufzeit-/Konfigurationszustand über `AppEinstellung`-Schlüssel mit `plugins.*`-Prefix zu persistieren.

## `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSourceCodeManagementPlugins` / `GetDevelopmentAutomationPlugins` / `GetIdePlugins` | public | Lazy Discovery + Listen (Z. 40/47/79) |
| `GetDefaultSourceCodeManagementPlugin` / `GetDefaultIdePlugin` | public | Erstes Plugin der Liste (Z. 54/86) |
| `GetDefaultDevelopmentAutomationPlugin` | public | KI-Default mit Priorität `ProviderDateiPraefix == "copilot"` zuerst (Z. 62, `GetKiPluginPriority` Z. 72) |
| `IsTestMode` / `IsAllowedInTestMode` | private static | Testmodus bei gesetzter `SOFTWARESCHMIEDE_TEST_DB_PATH`; erlaubte DLLs u. a. `Softwareschmiede.Plugin.KiSimulator`, `LocalDirectory`, `ClaudeCli`, `Codex`, `Devin`, `GitHubCopilot` (Z. 112–124) |
| `DiscoverPlugins` / `LoadPluginsFromDll` / `TryCreateAndRegister` | private | DLL-Discovery unter `plugins/`, Instanziierung via `ActivatorUtilities`, Registrierung nach `PluginType` (Z. 126/170/200) |

## `PromptZeitVersandService`
Datei: `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SchedulePromptAsync` | public | Plant In-memory-Timer (`ITimer`) für Prompt-Versand an die `PseudoConsoleSession` (Z. 44) |
| `CancelScheduledPrompt` | public | Verwirft geplanten Prompt (Z. 78) |
| `GetScheduledPromptStatus` | public | Liefert `ScheduledPromptInfo` für Kachel-Anzeige (`HasScheduledPrompt`) (Z. 86) |

Publizierte Events: `PromptSent` (`Action<Guid>`).
**Anforderungsrelevanz:** Rein in-memory (kein Persistenz-Reload nach App-Start), kein Bezug zu Pausierung.

## `MainWindowViewModel` (Seitenleisten-Refresh)
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

| Methode/Member | Sichtbarkeit | Kurzbeschreibung |
|----------------|-------------|------------------|
| `AktualisierungsIntervallSekunden` | private const | `= 5` (Z. 19) |
| `_aktualisierungsTimer` | private | `DispatcherTimer`, Intervall 5 s (Z. 198–200) |
| `AktiveAufgabenListe` | public | `ObservableCollection<AktiveAufgabePanelItem>` (Z. 120) |
| Refresh-Pfad | private | `IAktiveAufgabenService.GetAktiveAufgabenAsync` → `AktiveAufgabenListe.ReplaceAll(...)` (Z. 277–279) |
| `MapAktiveAufgabePanelItem` | private | Mapping `Aufgabe` → `AktiveAufgabePanelItem` inkl. `ResolvePluginName` für SCM-/KI-Plugin (Z. 313) |

## `TaskDetailViewModel` (Ribbon-Commands)
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

Vorhandene Commands (Auswahl): `LadenCommand`, `SpeichernCommand`, `LoeschenCommand`, `StartenCommand`, `CliStoppenCommand`, `CliNeustartenCommand`, `PluginAendernCommand`, `AufgabeAbschliessenCommand`, Prompt-Planungs-Commands, Issue-/PR-/IDE-Commands.
CanExecute-Properties u. a.: `KannSpeichern` (Status ∈ {Neu, Gestartet} && !IsCliRunning), `KannLoeschen` (Status ≠ Archiviert && !IsCliRunning).
Abonniert `KiAusfuehrungsService.CliProcessStatusChanged` (`OnCliProcessStatusChanged`).
**Kein** Pausieren-Command oder Pausiert-Anzeige vorhanden.

## `KiAusfuehrungsStatusConverter`
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (Z. 117 ff.)

Eingang: `Aufgabe`-Entity oder `AktiveAufgabePanelItem`. Ausgabe-Texte:
- `HasScheduledPrompt` → „⏳ Prompt in Wartestellung"
- `AusfuehrungsStatus != Aktiv` → „✓ Bereit"
- `AufgabeLaufAktivitaet.IstAktiv(...)` → `LaufStatus == WartetAufEingabe` ? „⏸ Wartet" : „▶ Läuft"
- `Status == Wartend` (ohne aktiven Lauf) → „⏸ Wartet"
- sonst → „✓ Bereit"

**Anforderungsrelevanz:** `Wartend` wird bereits als „⏸ Wartet" gerendert; es gibt keine Countdown- oder Pausiert-bis-Darstellung.

## `ActiveTasksListControl.xaml` (Kachel-Layout)
Datei: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`

- `AufgabenKachelInhaltTemplate`: Titel, ProjektName, `SCM: {ScmPluginName}`, `KI: {KiPluginName}` (AutomationId `AktiveAufgabeKiPlugin_{Titel}`), Statustext via `KiAusfuehrungsStatusConverter` inkl. `StatusUebergangsAnimation`, Offene-Todos-Button.
- Kachel-`Border`: `DataTrigger` auf `IsAktiv` → `AccentBrush`-Rahmen; keine Opacity-/Faded-Darstellung für pausierte/wartende Aufgaben.

## `KiSimulatorPlugin`
Datei: `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`

| Member | Wert / Beschreibung |
|--------|----------------------|
| `PluginName` | „KI Simulator" (Z. 16) |
| `ProviderDateiPraefix` | `"simulator"` (Z. 19) |
| `PluginPrefix` | `"Softwareschmiede.KiSimulator"` (Z. 22) |
| `PluginType` | `PluginType.DevelopmentAutomation` (Z. 25) |
| `GetSettingGroups` | leere Liste (Z. 34) |
| `SupportsSessionContinuation` | `false` (Z. 37) |
| `CheckHealthAsync` | immer `true` (Z. 40) |
| `FillIssueTemplateAsync` | `IIssueTemplateTextGenerator`-Implementierung (Z. 44) |

Basisklasse `CliKiPluginBase` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`): abstrakte Member `ProviderDateiPraefix`, `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups`, `BuildProcessStartInfo`, `SupportsSessionContinuation`, `CheckHealthAsync`; konkrete `StartCliAsync` (Z. 29), `GetCliHelpTextAsync` (Z. 39), `GetProcessWindowTitle` (Z. 175). Der Simulator startet `cmd.exe` mit `ping` für deterministisches Testverhalten und ist im Testmodus von `PluginManager.IsAllowedInTestMode` freigegeben.
