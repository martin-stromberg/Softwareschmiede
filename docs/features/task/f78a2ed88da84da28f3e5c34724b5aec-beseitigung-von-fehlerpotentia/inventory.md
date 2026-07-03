# Bestandsaufnahme: Beseitigung von Fehlerpotentialen

Analysiert wurden die WPF-Desktopanwendung Softwareschmiede (`src/Softwareschmiede.App`) sowie die zugrunde liegenden Application-Services (`src/Softwareschmiede/Application/Services`) bezogen auf die 19 identifizierten Fehlerquellen (F1–F19) aus der Fehleranalyse: globale Exception-Handler, Fire-and-Forget-Aufrufe, Event-Handler-Absicherung und native Ressourcen-Freigabe.

## Zusammenfassung

- **Globale Exception-Handler (F1–F3):** Keiner der drei Handler (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) ist in `App.xaml.cs` registriert. Serilog ist bereits vollständig konfiguriert und einsatzbereit für die Anbindung.
- **Startup-Pfade (F4–F5):** `App.xaml.cs → StartupAsync()` hat nur ein pauschales try-catch um den gesamten Startvorgang (führt bei jedem Fehler zu `Shutdown(1)`); die einzelnen kritischen Operationen (`GetRequiredService<CliProcessManager>()`, `mainWindow.Show()`) sind nicht individuell abgesichert.
- **Fire-and-Forget-Aufrufe (F6, F8, F15–F18):** Fünf konkrete `_ = ...`-Aufrufe ohne zentrale Absicherung identifiziert — in `CliProcessManager` (Timer-Callback), `KiAusfuehrungsService` (`SendCommandDelayedAsync`), `MainWindowViewModel`, `ProjectDetailViewModel`, `TaskDetailViewModel` (jeweils in Property-Settern) sowie `ProjectDetailView.xaml.cs` (`IssueDoubleClick`). Es existiert **keine** `SafeFireAndForgetTaskHelper`- oder `AsyncTaskExtensions`-Klasse im Code.
- **Heartbeat-Concurrency (F7):** `CliProcessManager.AktualisierungAsync()` hat kein `SemaphoreSlim` oder anderen Schutz gegen überlappende Timer-Ticks.
- **Process.Exited-Handler (F9–F10):** Beide `process.Exited`-Handler in `KiAusfuehrungsService` (`StartCliAsync` und `StartWithPseudoConsoleAsync`) sind nicht in try-catch eingehüllt; der Multicast-Event `CliProcessStatusChanged?.Invoke(...)` propagiert Exceptions eines Abonnenten ungefiltert.
- **Native Handle-Freigabe (F11):** `StartWithPseudoConsoleAsync()` erstellt zwei `FileStream`-Objekte ohne try-finally-Absicherung; bei Exception zwischen Erstellung und Session-Zuweisung droht Ressourcen-Leck.
- **Terminal-Lesevorgang (F12–F14):** `TerminalControl.OnSessionChanged()` speichert die vom `Task.Run(...)` erzeugte Task-Referenz nicht (kein `_readLoopTask`-Feld, kein `ContinueWith`-Logging); `ReadLoopAsync()` fängt nur `OperationCanceledException` im äußeren try-catch, kein generisches `catch (Exception)`.
- **ConfigureAwait (F19):** Keine `ConfigureAwait(false)`-Aufrufe in `CliProcessManager` oder `KiAusfuehrungsService` gefunden.
- **Tests:** Ein Regressionstest existiert bereits für einen Teilaspekt von F9 (`ProcessExited_ScopeFactoryDisposed_PersistiertNichtUndWirftNicht`). Für alle übrigen Fehlerquellen (globale Handler, restliche Exited-Handler-Pfade, Fire-and-Forget-Setter, TerminalControl, native Handle-Freigabe) existieren keine Tests.

## Details

- [App.xaml.cs — Globale Exception-Handler und Startup-Pfade](inventory/app.md)
- [CliProcessManager](inventory/cliprocessmanager.md)
- [KiAusfuehrungsService](inventory/kiausfuehrungsservice.md)
- [TerminalControl](inventory/terminalcontrol.md)
- [ViewModels (MainWindowViewModel, ProjectDetailViewModel, TaskDetailViewModel)](inventory/viewmodels.md)
- [ProjectDetailView.xaml.cs](inventory/projectdetailview.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
