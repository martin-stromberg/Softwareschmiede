# Logik — Shutdown bei Abschluss

## `AutoShutdownOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/AutoShutdownOrchestrator.cs` (gesamt ~120 Zeilen)

`public sealed class AutoShutdownOrchestrator : IAutoShutdownOrchestrator, IDisposable`. Konstruktor: `AutoShutdownOrchestrator(IRunningAutomationStatusSource statusSource, ISystemShutdownService systemShutdownService, ILogger<AutoShutdownOrchestrator>)` — abonniert sofort `statusSource.RunningCountChanged`.

Aktueller Ablauf:

- `SetEnabled(bool enabled)` setzt nur ein internes Flag; kein Zustandsevent, keine Persistenz.
- `OnRunningCountChanged(previousCount, currentCount)`:
  - `currentCount > 0` → `_zeroTransitionSeen = false` (Guard zurückgesetzt).
  - `currentCount == 0 && previousCount > 0 && _enabled && !_zeroTransitionSeen` → `_zeroTransitionSeen = true`, `SafeFireAndForget(ShutdownAsync(), "Shutdown-Anfrage")`.
- `ShutdownAsync()` (intern): Final-Recheck via `GetRunningCount()` — ist die Zahl wieder >0, kein Shutdown; sonst `_systemShutdownService.RequestShutdownAsync()`. Exceptions werden geloggt und verschluckt.
- `Dispose()` meldet `RunningCountChanged` ab.

**Nicht vorhanden:** Timer/Countdown, `CancellationToken`, Abbruch bei Statuswechsel (abonniert ausschließlich `RunningCountChanged`), Force-Flag, Countdown-/Fortschritts-Events, Persistenz, Sichtbarkeitssteuerung. Shutdown ist **sofort** beim Übergang >0→0.

Callers: keine Produktiv-Aufrufer — weder DI-Registrierung noch Instanziierung außerhalb von `AutoShutdownOrchestratorTests`. Siehe DI-Abschnitt unten.

## `SystemShutdownService`
Datei: `src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs`

`public sealed class SystemShutdownService : ISystemShutdownService` mit `ILogger<SystemShutdownService>`.

- `RequestShutdownAsync(CancellationToken)` (Z. ~30–60): ruft `ResolveShutdownCommand()` (privat) → `ProcessStartInfo` (`UseShellExecute = false`, `CreateNoWindow = true`) → `Process.Start` → `WaitForExitAsync(cancellationToken)` → `ExitCode != 0` → `InvalidOperationException` mit Exit-Code und Plattform.
- `ResolveShutdownCommand()` (privat, parameterlos, Z. ~62–85): Windows → `shutdown /s /f /t 0` (**Force `/f` fest eingebaut**), Linux → `shutdown -h now`, macOS → `osascript -e 'tell app "System Events" to shut down'`, sonst `PlatformNotSupportedException`.

**Nicht vorhanden:** Force als Parameter (Windows immer forcierend, macOS/Unix ohne Force-Begriff), kein E2E-/Testmodus-Fake, keine DI-Registrierung.

## `KiAusfuehrungsService` als `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (~850 Zeilen)

Implementiert u. a. `IRunningAutomationStatusSource`; verwaltet `_handles: Dictionary<Guid, CliProcessHandle>` und `_runningCountLock`.

| Member | Verhalten |
|--------|-----------|
| `event Action<int,int>? RunningCountChanged` | Wird aus `RaiseRunningCountChanged()` ausgelöst — bei CLI-Prozessstart und nach Prozessende/Session-Cleanup. Vorher/Nachher-Anzahl unter `_runningCountLock` ermittelt. |
| `int GetRunningCount()` | Zählt ` _handles.Values.Count(h => !h.Process.HasExited)` — **alle** laufenden gehandelten Prozesse, inkl. solcher mit Substatus `WartetAufEingabe`. Keine Unterscheidung Läuft/Wartet. |
| `bool IsRunning(Guid aufgabeId)` | `true`, wenn Handle existiert und Prozess nicht beendet. |
| `AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId)` | Liest `Handle.Session.RuntimeStatus`: `WartetAufEingabe` → `AufgabeLaufStatus.WartetAufEingabe`, sonst aktiver Prozess → `Laeuft`; `null` ohne aktiven Handle. |
| `IReadOnlyCollection<Guid> GetLaufendeAufgabenIds()` | IDs aller Handles mit laufendem Prozess. |
| `event Action<Guid, CliProcessStatus>? CliProcessStatusChanged` | Lebenszyklus-Event: `Gestartet`/`Gestoppt`/`Fehler` (`RaiseCliStatusChanged`). |
| `event Action<Guid>? CliLaufAktiv` | „Lauf aktiv"-Signal (Heartbeat-/Startbezogen). |

Prozessende: `Process.Exited`-Hook räumt Handle/Session auf (`CleanupSession`/`DisposeSession`), setzt `AbsichtlichGestoppt`-Mapping (`Gestoppt` vs `Fehler`), feuert `CliProcessStatusChanged` und `RaiseRunningCountChanged` — Reihenfolge: Status-Event vor Count-Event oder umgekehrt je nach Codepfad (siehe `TryGetExitCode`-/`Exited`-Pfade ab ~Z. 500–650).

## `CliProcessManager` — Laufstatus-Persistenz
Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

Abonniert `ITerminalSession.RuntimeStatusChanged` (bei interaktiven Sessions): persistiert den Substatus via `AufgabeService` in `Aufgabe.LaufStatus` (`Laeuft`/`WartetAufEingabe`); `CliRuntimeStatus.Inaktiv` wird ignoriert (Prozessende läuft über den separaten `Process.Exited`-Pfad). Zusätzlich setzt er `AktiveRunId`/Heartbeats und feuert `AufgabeLaufdatenChangedNotifier` — damit existiert ein fachlicher Statuswechsel-Lauf: `RuntimeStatusChanged` → `Aufgabe.LaufStatus` → `LaufdatenChanged` → ViewModel-Refresh.

## `AufgabeLaufdatenChangedNotifier`
Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufdatenChangedNotifier.cs`

Singleton (`AddSingleton`, `App.xaml.cs` Z. 260). API: `event Action<Guid>? LaufdatenChanged` + `void NotifyLaufdatenChanged(Guid aufgabeId)`; thread-sicherer Snapshot-Invoke. Aufrufer u. a. `CliProcessManager` (Laufstatusänderungen), `KiPluginLimitService`, `MainWindowViewModel`-nahe Services. Relevant als zusätzliche Statuswechsel-Quelle neben `RunningCountChanged`.

## `KiAusfuehrungsStatusKlassifikation` + Konverter
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (Z. 60–260)

- `internal static class KiAusfuehrungsStatusKlassifikation` (Z. 138–218), `internal enum KiAusfuehrungsZustand` (Z. ~130–137): `Pausiert`, `PromptInWartestellung`, `Laeuft`, `Wartet`, `Bereit`.
- `Klassifiziere(...)` — Überladungen für `AktiveAufgabePanelItem` und Einzelparameter; Priorität:
  1. `PausiertBisUtc` in Zukunft → `Pausiert` (überlagert alles).
  2. Geplanter Prompt in Wartestellung → `PromptInWartestellung`.
  3. Aktiver Lauf (Heartbeat-/`AktiveRunId`-Beleg oder Live-Prozess) **und** Live-/persistierter Substatus != `WartetAufEingabe` → `Laeuft`.
  4. Substatus `WartetAufEingabe` **oder** `AufgabeStatus.Wartend` → `Wartet`.
  5. sonst → `Bereit`.
- `KiAusfuehrungsStatusConverter` (Z. 60–110): mappt Zustand → Glyph-Text „▶ Läuft" / „⏸ Wartet" (plus weitere), verwendet in `ActiveTasksListControl.xaml` und der Seitenleisten-Liste.

Die Klassifikation implementiert bereits die fachliche „Läuft/Wartet"-Semantik der Anforderung (`Wartet` zählt nicht als laufend), ist aber `internal` im **App-Projekt** — `AutoShutdownOrchestrator`/`KiAusfuehrungsService` liegen im Application-/Domain-Layer und können sie nicht referenzieren (Abhängigkeitsrichtung App → Softwareschmiede).

## `MainWindowViewModel` — Seitenleisten-Pipeline
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` (Teile davon ~1400+ Zeilen)

- Abhängigkeiten u. a.: `IRunningAutomationStatusSource`, `IAktiveAufgabenService`, `AufgabeLaufdatenChangedNotifier` (über `MainWindowOptionaleDienste`), `PromptZeitVersandService`, `DispatcherInvoke` (aus `DispatcherInvokeFactory`/`Action<Action>`).
- `ObservableCollection<AktiveAufgabePanelItem> AktiveAufgabenListe` — ItemsSource der Seitenleisten-`ListBox` (`MainWindow.xaml`, `ActiveTasksListControl`).
- Refresh-Trigger (alle → `AktiveAufgabenAktualisierenAsync`, via `DispatcherInvoke` auf UI-Thread gemarshallt):
  - `RunningCountChanged` (Abonnement im Konstruktor, Abmeldung in `Dispose`),
  - `AufgabeLaufdatenChangedNotifier.LaufdatenChanged`,
  - interner `DispatcherTimer` mit 5 s Intervall (z. B. Pausenende, geplante Prompts).
- `MapAktiveAufgabePanelItem` (Z. 366–411): lädt `Aufgabe` + `GetLiveLaufStatus(aufgabeId)` + `GetScheduledPromptStatus(aufgabeId)` → befüllt `AktiveAufgabePanelItem` inkl. `LiveLaufStatus`, `HasScheduledPrompt`.
- `AktiveAufgabenAktualisierenAsync`: liest Kandidaten via `IAktiveAufgabenService` (siehe interfaces.md), mapped, sortiert (`SortierZeitpunkt` desc), dedupliziert, merged in die ObservableCollection (Update-in-Place statt Clear/Add — erhält `IsAktiv`-Selektion).
- Feuer-und-vergiss-Aufrufe laufen durch `AsyncTaskExtensions.SafeFireAndForget`.

**Nicht vorhanden:** Properties/Commands für Auto-Shutdown-Checkbox, Toleranz-Eingabe, Force-Checkbox, Countdown-Text, `RunningCount`-Anzeige-Property; kein Abonnement eines Orchestrators.

## `MainWindow.xaml` — Fußzeilen-Layout
Datei: `src/Softwareschmiede.App/Views/MainWindow.xaml`

- Seitenleisten-Grid (Z. ~85–230): `RowDefinitions` 0=Header/Navigation, 1=`ActiveTasksListControl`+Suchfeld (`*`), 2=Fußzeile (`Auto`).
- Fußzeile (Z. 170–228): `Border`/`StackPanel` mit Trennlinie, `AppVersionText`, Update-Buttons (`UpdatePruefenButton`, `UpdateInstallierenButton` mit `UpdateProgressBar`/`UpdateFortschrittText`), `UpdateHinweis`-Text; Sichtbarkeiten via `BoolToVisibilityConverter`, `GreaterThanZeroToVisibilityConverter`, `NullOrEmptyToVisibilityConverter` (in `App.xaml`-Ressourcen registriert).
- Eingeklappte Navigation (Breite 48 px): Fußzeilen-Texte/Buttons werden per `DataTrigger` auf `IsNavigationCollapsed` ausgeblendet.
- **Nicht vorhanden:** Auto-Shutdown-Checkbox, Running-Count-Anzeige, Toleranz-Eingabe, Force-Checkbox, Countdown-Text/Abbruch-Button.

## `AppEinstellungService`
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

`AddScoped`-Service (`App.xaml.cs` Z. 251) auf `SoftwareschmiededDbContext.AppEinstellungen`. Muster je Einstellung: `private const string XyzKey` + typsichere `Get…Async`/`Set…Async` mit Default + `Math.Clamp`-Begrenzung. Beispiele:

- `ScmTaskCompletionWaitMinutesKey` (`"scm.taskcompletion.wait.minutes"`, Z. 67): `GetScmTaskCompletionWaitMinutesAsync` (Z. 310–314) default/clamped Minutenwert, `SetScmTaskCompletionWaitMinutesAsync` persistiert als String.
- Fensterposition/-größe (`window.position.x` u. a.), Sprache, Update-Kanal, Listenmodi (Schlüssel siehe `AufgabenListenModusService`).

Primitive: `GetSettingAsync`, `SetSettingAsync`, `TryGet…`/`SetInt/Bool/…`-Helfer; `AktualisiertAm` wird je Write gesetzt. **Nicht vorhanden:** `autoshutdown.*`-Schlüssel.

## `PromptZeitVersandService` — Timer-/Persistenz-Muster
Datei: `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`

Singleton (`AddSingleton`, `App.xaml.cs` Z. 296). Nutzt `TimeProvider` (registriert als `TimeProvider.System`, Z. 295) und `_timeProvider.CreateTimer(callback, state, dueTime, period)` → `ITimer`; Timer wird mit `dueTime: Infinite` erstellt und per `timer.Change(...)` scharf geschaltet. Eigenschaften, die für den Shutdown-Countdown als Muster dienen:

- Persistenter Fachzustand in EF-Tabelle (`ZeitgesteuerterPromptStore`), Laufzeit-Timer in `ConcurrentDictionary<Guid, ITimer>`.
- Abonniert `CliProcessStatusChanged`; versendet Prompts bei Session-Bereitschaft; `WartetAufSession`-Flag im Status.
- Dispose entsorgt Timer und Event-Abos; Feuer-und-vergiss via `SafeFireAndForget`.
- `GetScheduledPromptStatus(Guid)` → `ScheduledPromptInfo?` für die UI-Klassifikation.

Tests nutzen `Microsoft.Extensions.TimeProvider.Testing`/`FakeTimeProvider` → Countdown deterministisch testbar.

## `AufgabenListenModusService` + `AufgabenListenModusBindung` — persistierte UI-Einstellung
Dateien: `src/Softwareschmiede/Application/Services/AufgabenListenModusService.cs`, `src/Softwareschmiede.App/…` (`AufgabenListenModusBindung`)

Singleton-Service mit `IServiceScopeFactory`-Zugriff auf den scoped `AppEinstellungService` (Muster für Singleton→Scoped); `LadeMenueModusAsync`/`SpeichereMenueModusAsync`, `LadeDashboardModusAsync`/`SpeichereDashboardModusAsync`. `AufgabenListenModusBindung` koppelt den Modus an `ComboBox`-Auswahl (Menü vs. Dashboard) und speichert bei Änderung — Referenzmuster „UI-Toggle ↔ persistierte Einstellung".

## `ScmTaskCompletionMonitoringService` — Countdown-Vorbild
Datei: `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs`

Hosted Service (Singleton, `AddHostedService`-nahe Registrierung, `ScmEventOptions.PollInterval`). Erkennt SCM-Abschluss, persistiert `ScmAbschlussErkanntUtc` und startet einen Toleranz-Countdown (`GetScmTaskCompletionWaitMinutesAsync` aus `AppEinstellungService`); bei Ablauf folgegetriggerte Aktion. Belegt: Toleranzzeit-Einstellung + `IServiceScopeFactory`-Pattern für scoped Settings aus Singleton-Monitor.

## `UpdateService` / `IApplicationShutdownService` — App-Shutdown ≠ OS-Shutdown
Dateien: `src/Softwareschmiede/Application/Services/UpdateService.cs`, `src/Softwareschmiede/Domain/Interfaces/IApplicationShutdownService.cs`, `src/Softwareschmiede.App/Services/WpfApplicationShutdownService.cs`

`UpdateService` löst nach Update-Installation den **App**-Shutdown aus (`IApplicationShutdownService.Shutdown()` → WPF `Application.Shutdown`), DI: `App.xaml.cs` Z. 313 → `WpfApplicationShutdownService`. Fachlich getrennt vom `ISystemShutdownService` (OS-Shutdown) — Namensnähe beachten. Für den Anforderungs-Punkt „geordneter App-Shutdown vor OS-Shutdown" ist `IApplicationShutdownService` die vorhandene Schnittstelle.

## `CliUpdateSafetyService`
Datei: `src/Softwareschmiede/Application/Services/CliUpdateSafetyService.cs`

`ICliUpdateSafetyService` (Z. ~20): blockiert Update-Installation, solange CLI-Läufe aktiv sind; nutzt `AufgabeLaufAktivitaet.IstAktiv` (Heartbeat-/`AktiveRunId`-Timeout 5 min, `AufgabeRecoveryService.HeartbeatTimeoutMinutes`). Alternative „aktiv"-Definition neben `GetRunningCount` (Handle-basiert) und der UI-Klassifikation — drei unterschiedliche Lauf-Definitionen existieren im Ist-Stand.

## `App.xaml.cs` — DI-Wiring und E2E-Umgebungen
Datei: `src/Softwareschmiede.App/App.xaml.cs` (Startup ~Z. 200–470)

Relevante Registrierungen (Auszug):

| Zeile | Registrierung |
|-------|---------------|
| ~251 | `AppEinstellungService` (`AddScoped`) |
| ~260 | `AufgabeLaufdatenChangedNotifier` (`AddSingleton`) |
| ~294 | `KiAusfuehrungsService` (`AddSingleton`) |
| ~295 | `TimeProvider.System` |
| ~296 | `PromptZeitVersandService` (`AddSingleton`) |
| ~301 | `CliProcessManager` (`AddSingleton`) |
| ~304–305 | `IRunningAutomationStatusSource` → `KiAusfuehrungsService` (gleiche Singleton-Instanz) |
| ~313 | `IApplicationShutdownService` → `WpfApplicationShutdownService` |
| ~321–331 | `MainWindowUpdateDienste`/`MainWindowOptionaleDienste` (`AddTransient` Factory-Aggregate) |
| ~348 | `MainWindowViewModel` (`AddTransient`) |
| ~365–377 | SCM-E2E-Modus (`SOFTWARESCHMIEDE_SCM_TEST_CONFIG`): Fakes u. a. `ScmE2ETerminalSessionFactory` |
| ~385–412 | Update-E2E-Modus (`SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` + `SOFTWARESCHMIEDE_TEST_DB_PATH`): `UpdateE2ETestKontext`, `UpdateE2EProtokoll`; `IApplicationShutdownService` → `RecordingApplicationShutdownService` |

**Fehlende Registrierungen:** `ISystemShutdownService`/`SystemShutdownService`, `IAutoShutdownOrchestrator`/`AutoShutdownOrchestrator` — der Orchestrator wird nirgends instanziiert; produktiv inaktiv.

`RecordingApplicationShutdownService` (`src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs`): schreibt `UpdateE2EEreignisse.ShutdownRequested` ins JSONL-Protokoll, beendet die Test-App **nicht** — Referenzmuster für einen Shutdown-Fake (allerdings App-Shutdown, nicht OS-Shutdown).

## Dokumentations-Ist-Stand
`docs/help/einstellungen/beschreibung.md` (Z. ~148–150) beschreibt den `AutoShutdownOrchestrator` als aktiven Lauscher — Abweichung zum DI-Ist-Stand (keine Registrierung); Hinweis auf Blazor-Vorgängerversion.
