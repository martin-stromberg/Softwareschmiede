```json
[
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 88,
    "summary": "PluginManager ist als konkreter Typ registriert, nicht als IPluginManager – PluginSelectionService schlägt bei der DI-Auflösung fehl.",
    "failure_scenario": "PluginSelectionService fordert IPluginManager im Konstruktor an. App.xaml.cs registriert AddSingleton<PluginManager>() ohne Interface-Binding. Beim ersten Zugriff auf TaskDetailViewModel → PluginSelectionService wirft der DI-Container eine InvalidOperationException. Außerdem fehlen IGitPlugin (für EntwicklungsprozessService), IBenutzerkontextService (für BenachrichtigungsService), IArbeitsverzeichnisResolver (für EntwicklungsprozessService) und PluginDefaultSettingsService vollständig im Container."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/CliProcessManager.cs",
    "line": 38,
    "summary": "Timer-Callback ist async void – unkontrollierte Exceptions reißen den Prozess ab.",
    "failure_scenario": "Der Timer erstellt eine async void Lambda (TimerCallback = void(object?)). Wenn UpdateHeartbeatAsync eine unerwartete Exception wirft, die nicht durch den inneren catch-Block abgefangen wird (z.B. eine ObjectDisposedException nach Dispose des DbContext), propagiert die Exception auf den ThreadPool und beendet die Anwendung unkontrolliert."
  },
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 25,
    "summary": "OnStartup ist async void – Exceptions aus _host.StartAsync() führen zu unbehandelten Ausnahmen ohne Fehlerdialog.",
    "failure_scenario": "Wenn Host.StartAsync() fehlschlägt (z.B. wegen fehlender DI-Registrierung oder Datenbankfehler), wird die Exception nicht abgefangen. Sie landet als unhandledException auf dem UI-Thread und beendet die Anwendung ohne eine für den Benutzer verständliche Fehlermeldung."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 45,
    "summary": "TOCTOU-Race zwischen IsRunning-Prüfung und _handles[aufgabeId]-Zugriff beim doppelten CLI-Start.",
    "failure_scenario": "Thread A: IsRunning gibt true zurück. Gleichzeitig wird Dispose() aufgerufen (_handles.Clear()). Thread A greift danach auf _handles[aufgabeId] zu → KeyNotFoundException, da ConcurrentDictionary.Clear() dazwischengefeuert hat. Alternativ: zwei gleichzeitige StartCliAsync-Aufrufe für dieselbe aufgabeId, der erste belegt den Slot, der zweite überschreibt ihn mit einem neuen Prozess ohne den alten zu beenden."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs",
    "line": 207,
    "summary": "Temporäre Audiodateien werden niemals gelöscht – dauerhaftes Leck im Temp-Verzeichnis.",
    "failure_scenario": "Bei jeder Ton-Benachrichtigung wird eine neue Datei in Path.GetTempPath() angelegt (softwareschmiede-audio-<guid>.mp3/wav/ogg). Es gibt keinen Aufräummechanismus. Über lange Betriebszeit (viele Aufgaben-Statuswechsel) akkumulieren Hunderte temporärer Audiodateien auf der Entwicklungsmaschine."
  },
  {
    "file": "src/Softwareschmiede.App/Controls/ProcessWindowHost.cs",
    "line": 97,
    "summary": "ResizeEmbeddedWindow setzt SWP_SHOWWINDOW ohne SWP_NOACTIVATE – jede Größenänderung stiehlt den Tastaturfokus.",
    "failure_scenario": "Wenn der Benutzer Text in einem anderen Feld eintippt und das Hauptfenster in der Größe verändert wird, ruft WPF OnRenderSizeChanged auf. SetWindowPos mit 0x0040 (SWP_SHOWWINDOW) aktiviert das eingebettete CLI-Fenster und übernimmt den Fokus. Das unterbricht die Texteingabe im umgebenden WPF-Bereich."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 91,
    "summary": "KannCliStarten erlaubt Start aus Status Gestartet, aber der Aufgabenstatus wird nach dem CLI-Start nicht auf InArbeit aktualisiert – Status läuft auseinander.",
    "failure_scenario": "Benutzer klickt 'CLI starten' wenn Status=Gestartet. KiAusfuehrungsService.StartCliAsync startet den Prozess, aber kein Code im ViewModel setzt den Aufgabenstatus auf InArbeit. Der AufgabeRecoveryService sucht nach InArbeit/Wartend-Aufgaben ohne laufenden Prozess – diese Aufgabe würde nicht wiederhergestellt, da ihr Status noch Gestartet ist. Gleichzeitig zeigt die UI Status=Gestartet, obwohl die CLI bereits läuft."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 29,
    "summary": "Dispatcher.InvokeAsync-Rückgabewert wird verworfen – MediaPlayer-Fehler werden nicht an den Aufrufer zurückgegeben.",
    "failure_scenario": "Dispatcher.InvokeAsync gibt eine DispatcherOperation zurück, die nicht mit await verknüpft wird. Das zurückgegebene tcs.Task wird zwar korrekt durch die Events abgeschlossen, aber falls die Dispatcher-Queue gesperrt ist (Shutdown, kein UI-Thread), wird der TCS nie gesetzt. Der Aufrufer wartet unendlich auf tcs.Task."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs",
    "line": 104,
    "summary": "N+1-Datenbankabfragen in LadenAsync – eine Query pro Projekt für aktive Aufgaben.",
    "failure_scenario": "Für jedes Projekt in projekte wird GetByProjektAsync aufgerufen (je eine SQL-Query). Bei 50 Projekten = 51 Datenbankrundtrips. Da AppEinstellungService und ProjektService als Scoped registriert sind und DbContext auf SQLite läuft, führt dies bei vollen Projektlisten zu spürbaren Ladezeiten (>1s) beim Dashboard-Start."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs",
    "line": 68,
    "summary": "DarkModeChanged-Event wird im Konstruktor abonniert, aber nie abgemeldet – potentielles Speicherleck.",
    "failure_scenario": "MainWindowViewModel wird als Transient registriert (AddTransient). Falls zukünftig mehrere Instanzen erzeugt werden (Navigation oder Tests), hält DarkModeService (Singleton) eine Referenz auf jede ViewModel-Instanz durch das Event. Die alten ViewModels werden nicht vom GC freigegeben, weil der Singleton sie indirekt hält."
  }
]
```
