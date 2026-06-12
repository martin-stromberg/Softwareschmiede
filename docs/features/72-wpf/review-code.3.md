```json
[
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 118,
    "summary": "DarkModeService (Singleton) hält AppEinstellungService (Scoped) als captive dependency",
    "failure_scenario": "DarkModeService wird als AddSingleton registriert, nimmt aber AppEinstellungService (AddScoped, hält einen EF DbContext) per Constructor Injection. Host.CreateDefaultBuilder() aktiviert ValidateScopes in Development → InvalidOperationException beim Start. In Production wird ein einziger DbContext für die gesamte App-Laufzeit eingefroren, was Thread-Safety-Verletzungen und Stale-Read-Probleme verursacht."
  },
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 94,
    "summary": "Kein Aufruf von Database.Migrate() oder EnsureCreated() beim Start — Datenbankschema wird nie angelegt",
    "failure_scenario": "Bei einer Neuinstallation bleibt die SQLite-Datei schemalos; jeder erste DB-Zugriff wirft SqliteException ('no such table'). Bei einem Upgrade von der Blazor-App bleiben ausstehende EF-Migrationen unangewendet, was dieselben Fehler produziert."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 91,
    "summary": "Altes TaskDetailViewModel wird beim Navigieren nicht disposed — Event-Handler-Leak auf dem Singleton KiAusfuehrungsService",
    "failure_scenario": "Jedes Mal, wenn der Nutzer eine Aufgabe öffnet, wird ein neues TaskDetailViewModel erzeugt. Das vorherige wird ohne Dispose() verworfen, sein OnCliProcessStatusChanged-Handler bleibt am Singleton hängen. Nach N Navigationen feuern N Ghost-Handler bei jeder Statusänderung; die abgelegten VMs halten außerdem Scoped-Services (AufgabeService, ProtokollService) am Leben und verhindern deren Garbage Collection."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 42,
    "summary": "MediaPlayer nur als lokale Variable in der Dispatcher-Lambda gespeichert — GC kann ihn vor MediaEnded einsammeln",
    "failure_scenario": "Sobald der GC zwischen player.Play() und dem MediaEnded-Event läuft, wird der MediaPlayer-Wrapper finalisiert (der unmanaged DirectShow-Stack hält keine starke managed Referenz). Die Wiedergabe bricht still ab, tcs.TrySetResult wird nie aufgerufen, der Aufrufer wartet ewig (oder bis Cancellation)."
  },
  {
    "file": "src/Softwareschmiede/terminal-backend/server.js",
    "line": 43,
    "summary": "ptyProcess-Nullzugriff wenn START_CLI vor SET_CWD gesendet wird",
    "failure_scenario": "ptyProcess ist null bis SET_CWD empfangen wird. Trifft START_CLI:claude vorher ein, wirft ptyProcess.write(...) TypeError: Cannot read properties of null. Es gibt kein try/catch um den message-Handler — die Exception bricht die WebSocket-Session ab."
  },
  {
    "file": "src/Softwareschmiede.App/Controls/ProcessWindowHost.cs",
    "line": 44,
    "summary": "Rückgabewert von CreateWindowEx nicht geprüft — HandleRef mit Zero-Handle wird zurückgegeben",
    "failure_scenario": "Schlägt CreateWindowEx fehl (z. B. GDI-Objektlimit überschritten), wird IntPtr.Zero in _hostHandle gespeichert und als HandleRef zurückgegeben. HwndHost erhält ein ungültiges Handle; alle nachfolgenden Win32-Aufrufe (SetParent, SetWindowPos) operieren auf HWND 0, was undefiniertes Verhalten oder stille Fehler ohne Fehlermeldung zur Folge hat."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 40,
    "summary": "AufgabeId-Setter startet LadenAsync fire-and-forget ohne Cancellation — concurrent loads können falsche Daten anzeigen",
    "failure_scenario": "Wird AufgabeId schnell hintereinander gesetzt (Nutzer klickt durch Aufgaben), laufen mehrere LadenAsync-Calls parallel auf CancellationToken.None. Der erste Call kann nach dem zweiten abschließen und die UI mit veralteten Daten überschreiben. Außerdem kann _aufgabe während eines laufenden CliStartenAsync auf null gesetzt werden, was die Statusübergangsprüfung nach dem await verfälscht."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ViewModelBase.cs",
    "line": 126,
    "summary": "AsyncRelayCommand.Execute ist async void — nicht-cancellierte Ausnahmen propagieren zum Dispatcher und crashen die App",
    "failure_scenario": "Execute ist async void; nur OperationCanceledException wird abgefangen. Jede andere Exception aus _execute(), die nicht intern gefangen wird, landet als unhandled exception auf dem WPF-Dispatcher. Ohne Application.DispatcherUnhandledException-Handler beendet dies die Anwendung."
  },
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 72,
    "summary": "StartupAsync per fire-and-forget (.ContinueWith) gestartet — Ausnahmen beim Startup gehen verloren oder werden zu spät behandelt",
    "failure_scenario": "StartupAsync (DI-Build, _host.StartAsync, MainWindow-Anzeige) wird nicht awaited. Schlägt eine dieser Operationen fehl, wird die Exception erst im ContinueWith-Callback sichtbar, der auf TaskScheduler.Default läuft. In der Zwischenzeit ist der OnStartup-Call bereits zurückgekehrt; das Fenster erscheint nie, und der Nutzer sieht keine aussagekräftige Fehlermeldung."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/ProcessWindowEmbedder.cs",
    "line": 11,
    "summary": "ProcessWindowEmbedder als Singleton registriert aber nie injiziert — dupliziert P/Invoke-Code aus ProcessWindowHost ohne genutzt zu werden",
    "failure_scenario": "Die WPF-App verwendet ProcessWindowHost (HwndHost) für die Fenstereinbettung; ProcessWindowEmbedder ist toter Code. Vier P/Invoke-Signaturen sind doppelt vorhanden. Korrekturen in ProcessWindowHost werden nicht nach ProcessWindowEmbedder übertragen, was Divergenz und Verwirrung über den kanonischen Embedding-Pfad schafft."
  }
]
```
