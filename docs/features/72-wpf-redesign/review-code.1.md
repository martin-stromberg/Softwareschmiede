# Code-Review: 72-wpf-redesign

```json
[
  {
    "file": "src/Softwareschmiede.App/Views/ProjectDetailView.xaml",
    "line": 140,
    "summary": "Die Projekt-Kachel (Name- und Beschreibungs-Textboxen) ist im Anlage-Modus unsichtbar, weil ihre Visibility an 'Projekt' gebunden ist, das fuer ein neues Projekt stets null bleibt.",
    "failure_scenario": "Nutzer klickt 'Neu': ZeigeDetailErstellungsFormularAsync setzt ProjektId = Guid.Empty, LadenAsync bricht sofort ab (Guard-Bedingung), Projekt bleibt null, NullOrEmptyToVisibilityConverter gibt Collapsed zurueck — die Textboxen fuer Name und Beschreibung sind ausgeblendet. Der Nutzer kann keinen Namen eingeben und das Projekt nicht erstellen."
  },
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 156,
    "summary": "RepositoryAssignViewModel ist nicht im DI-Container registriert; GetRequiredService wirft InvalidOperationException bei jedem Klick auf 'Repository zuweisen'.",
    "failure_scenario": "Nutzer klickt 'Zuweisen' in der Projektdetailansicht: ProjectDetailViewModel.cs Zeile 315 ruft _serviceProvider.GetRequiredService<RepositoryAssignViewModel>() auf. Da kein AddTransient/AddScoped-Eintrag existiert, wirft der Container InvalidOperationException. FehlerMeldung wird gesetzt, die Funktion ist vollstaendig nicht nutzbar."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 181,
    "summary": "ZurueckCommand ist fuer alle Navigationspfade dauerhaft wirkungslos, weil kein Aufrufer die zurueckAction uebergibt.",
    "failure_scenario": "Beide Aufrufer (ZeigeDetailAsync und ZeigeDetailErstellungsFormularAsync in ProjectListViewModel) loesen das ViewModel per GetRequiredService ohne zurueckAction-Parameter. Da der DI-Container den optionalen Parameter nicht befuellen kann, ist _zurueckAction immer null. Klick auf 'Zurueck' im Ribbon fuehrt zu keiner Aktion."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 264,
    "summary": "Nach dem Anlegen eines neuen Projekts wird ProjectListViewModel.Projekte nicht aktualisiert; das neue Projekt erscheint nicht in der Kachelansicht.",
    "failure_scenario": "Nutzer legt neues Projekt an und speichert: ProjektSpeichernAsync legt das Projekt an, setzt ProjektId und laedt das Detail neu. Es gibt keinen Rueckruf zur ProjectListViewModel.Projekte-Collection. Das neue Projekt fehlt im Dashboard, bis der Nutzer manuell 'Laden' ausfuehrt."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 264,
    "summary": "ProjektName und ProjektBeschreibung werden ohne Trim() an CreateAsync uebergeben; fuehrende/nachfolgende Leerzeichen werden gespeichert.",
    "failure_scenario": "Nutzer gibt '  Mein Projekt  ' ein. ProjektSpeichernAsync uebergibt diesen String unveraendert an _projektService.CreateAsync. Das Projekt wird mit unbereinigtem Namen angelegt. Das alte ProjektErstellenAsync hatte .Trim() an beiden Feldern."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 358,
    "summary": "RepositoryOeffnenAsync faengt OperationCanceledException in einem generischen catch-Block ab und protokolliert sie als Fehler, anstatt sie neu zu werfen.",
    "failure_scenario": "Alle anderen async-Methoden der Klasse haben ein gesondertes 'catch (OperationCanceledException) { throw; }' vor dem allgemeinen Exception-Catch. RepositoryOeffnenAsync fehlt dieses Muster. Ein stornierter Vorgang erscheint als Fehlermeldung 'Fehler: A task was canceled.' im UI und im Log."
  },
  {
    "file": "src/Softwareschmiede.App/Views/ProjectListView.xaml",
    "line": 52,
    "summary": "Die Projektkacheln sind nur per Mausklick waehlbar (ItemsControl + MouseBinding); Tastaturnavigation (Pfeiltasten, Enter, Tab) ist nicht mehr vorhanden.",
    "failure_scenario": "Nutzer navigiert per Tastatur: Die alte ListBox mit SelectedItem-Binding bot vollstaendige Tastaturnavigation. Das neue ItemsControl mit MouseAction='LeftClick' hat keine Focusable-Elemente, keine KeyBindings und keine Selektor-Logik. Keyboard-only-Nutzer koennen kein Projekt auswahlen."
  },
  {
    "file": "src/Softwareschmiede.App/Views/ProjectListView.xaml",
    "line": 172,
    "summary": "Die Fehlermeldungs-Border hat kein Panel.ZIndex und wird hinter dem modalen Overlay (ZIndex 12/13) gerendert; Fehler sind unsichtbar, waehrend das Detail-Panel geoeffnet ist.",
    "failure_scenario": "LadenAsync oder ProjektArchivierenAsync schlaegt fehl, waehrend DetailViewModel != null ist (Modal sichtbar): FehlerMeldung wird gesetzt, aber die Border mit ZIndex 0 liegt unter den Overlay-Borders mit ZIndex 12 und 13. Der Nutzer sieht keine Fehlermeldung."
  },
  {
    "file": "src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml.cs",
    "line": 19,
    "summary": "CloseRequested-Event-Handler wird abonniert, aber nie abgemeldet; bei nicht-transient registriertem ViewModel entsteht ein Speicherleck und potenziell ein Folgefehler.",
    "failure_scenario": "Sobald RepositoryAssignViewModel im DI registriert ist (notwendig fuer Finding 2): Wird es als Scoped oder Singleton registriert, haelt das ViewModel einen Delegate auf jede geschlossene RepositoryAssignDialog-Instanz. Zweiter Aufruf von RepositoryZuweisenAsync loest CloseRequested auf dem bereits geschlossenen Dialog aus und wirft InvalidOperationException ('DialogResult kann nur gesetzt werden, nachdem das Fenster als Dialog angezeigt wurde')."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs",
    "line": 44,
    "summary": "DetailViewModel-Setter ruft Dispose() auf dem alten ViewModel auf, bevor SetProperty PropertyChanged ausloest; WPF-Bindings sehen einen verworfenen DataContext.",
    "failure_scenario": "Nutzer klickt schnell auf zwei verschiedene Projektkacheln: DetailViewModel-Setter verwirft VM_A (Dispose -> _ladenCts.Cancel), bevor SetProperty den View benachrichtigt. Der noch laufende LadenAsync-Abschluss (finally: IsLoading = false) schreibt auf das verworfene VM_A und loest PropertyChanged aus, waehrend der View noch VM_A als DataContext haelt. Kein Absturz, aber ein Flackern und potenziell inkonsistenter Bindungszustand."
  }
]
```
