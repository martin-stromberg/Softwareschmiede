# Code-Review: Branch `72-wpf-redesign`

Effort: high — 7 Finder-Winkel × bis zu 6 Kandidaten, 1-Stimmen-Verifikation (recall-biased).

---

## Findings

```json
[
  {
    "file": "src/Softwareschmiede.App/Views/ProjectDetailView.xaml",
    "line": 121,
    "summary": "EnumToBoolConverter ist nicht definiert — XamlParseException beim Öffnen der Filteransicht",
    "failure_scenario": "ProjectDetailView.xaml referenziert {StaticResource EnumToBoolConverter} an drei Stellen (Zeilen 121, 125, 129) für die RadioButton-IsChecked-Bindungen des Aufgabenfilters. Die Klasse existiert weder in AppConverters.cs noch ist sie in App.xaml oder einer MergedDictionary registriert. Sobald der Filter-Overlay gerendert wird, wirft WPF eine XamlParseException, die die gesamte Detailansicht zum Absturz bringt."
  },
  {
    "file": "src/Softwareschmiede.App/Views/DashboardView.xaml",
    "line": 64,
    "summary": "BoolToVisibilityConverter mit int-Binding (RecoveryKandidaten.Count) — Recovery-Banner erscheint nie",
    "failure_scenario": "BoolToVisibilityConverter.Convert prüft `value is true` (C#-Mustererkennung gegen den booleschen Literal true). Ein int (auch > 0) erfüllt `is true` nie. Das Banner bleibt dauerhaft Collapsed, unabhängig von der Anzahl der Recovery-Kandidaten. Nutzer sehen den Recovery-Hinweis nie."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 298,
    "summary": "ProjektLoeschenAsync ruft nur ZurueckAction auf — gelöschtes Projekt bleibt in der Projektliste sichtbar",
    "failure_scenario": "Nach erfolgreichem DeleteAsync wird nur ZurueckAction?.Invoke() aufgerufen, was lediglich DetailViewModel = null setzt. ProjectListViewModel.Projekte wird nicht aktualisiert. Das gelöschte Projekt bleibt in der Liste sichtbar. Ein erneuter Klick darauf löst LadenAsync für eine nicht mehr existierende ID aus und produziert eine Fehlermeldung."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs",
    "line": 16,
    "summary": "VerfuegbareRepositories wird nie befüllt — Repository-Zuweisungs-Feature ist vollständig nicht funktionsfähig",
    "failure_scenario": "RepositoryAssignViewModel hat keinen Service-Abhängigkeiten und keine Lade-Methode. VerfuegbareRepositories ist immer leer. Da BestaetigenCommand durch `() => _selectedRepository != null` bewacht wird und nichts auswählbar ist, bleibt der Bestätigen-Button dauerhaft deaktiviert. AddRepositoryAsync (ProjectDetailViewModel Zeile 325) ist unerreichbar."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs",
    "line": 90,
    "summary": "NavigateToProjectList cached den ViewModel nicht — bei jeder Navigation wird eine neue leere Instanz erzeugt",
    "failure_scenario": "NavigateToDashboard und NavigateToSettings verwenden `??=` zum Cachen ihrer ViewModels. NavigateToProjectList ruft GetRequiredService<ProjectListViewModel>() ohne Caching auf — jede Navigation zur Projektliste erzeugt eine neue Instanz mit leerer Projekteliste, die erst durch LadenCommand nachgeladen werden muss. Geladener Zustand und Auswahl gehen verloren."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 82,
    "summary": "SelectedTaskViewModel-Setter disposed den alten VM vor SetProperty — falsche Reihenfolge",
    "failure_scenario": "Der alte TaskDetailViewModel wird disposed, bevor SetProperty den neuen Wert setzt und PropertyChanged auslöst. Falls ein Subscriber auf PropertyChanged noch eine Referenz auf den alten VM hält (z.B. über eine Closure oder Animation), erhält er ein bereits disposed Objekt. ProjectListViewModel.DetailViewModel verwendet korrekt die Reihenfolge: alten Wert speichern → SetProperty → alten disposed."
  },
  {
    "file": "src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml",
    "line": 8,
    "summary": "WindowStartupLocation=CenterOwner ohne gesetzten Owner — Dialog zentriert sich auf dem Bildschirm statt über dem App-Fenster",
    "failure_scenario": "RepositoryAssignDialog wird in ProjectDetailViewModel.RepositoryZuweisenAsync mit `new RepositoryAssignDialog(vm)` instanziiert ohne Owner-Zuweisung. WPF fällt bei fehlendem Owner auf CenterScreen zurück. Auf Multi-Monitor-Setups erscheint der Dialog auf einem anderen Bildschirm als das Hauptfenster."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 79,
    "summary": "process.Exited-Handler entfernt den Eintrag nie aus _handles — Dictionary wächst unbegrenzt",
    "failure_scenario": "Der Exited-Handler loggt den Ausstieg und aktualisiert den Status, ruft aber nie _handles.TryRemove(aufgabeId, out _) auf. Jedes beendete CLI-Prozess-Handle verbleibt dauerhaft im Dictionary. Jedes CliProcessHandle hält ein Process-Objekt (mit Kernel-Handle, Environment-Block etc.) auch nach dem Prozessende. Bei langläufigen Sessions mit vielen CLI-Starts ist dies ein unbegrenzter Speicher- und Handle-Leak."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs",
    "line": 93,
    "summary": "DarkModeChanged-Event-Subscription via anonymer Lambda wird nie abgemeldet — kein IDisposable",
    "failure_scenario": "Der Konstruktor registriert `_darkModeService.DarkModeChanged += enabled => IsDarkMode = enabled` mit einer anonymen Lambda. Es gibt kein Dispose() zum Abmelden. Derzeit harmlos, da SettingsViewModel in MainWindowViewModel._settingsViewModel gecacht ist. Wird das Caching entfernt oder die Klasse auf echtes Transient umgestellt, hält DarkModeService jeden verworfenen SettingsViewModel über den Event-Delegate am Leben (Speicherleck)."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/NavigationViewModel.cs",
    "line": 1,
    "summary": "NavigationViewModel ist toter Code — MainWindowViewModel besitzt bereits denselben Navigation-State",
    "failure_scenario": "MainWindowViewModel verwaltet IsNavigationExpanded und ToggleNavigationCommand, an die MainWindow.xaml direkt bindet. NavigationViewModel dupliziert dieselbe Logik (IsExpanded, ToggleCommand, NavigationWidth) und ist in DI registriert, wird aber von keiner View, keinem ViewModel und keinem Code-Behind verwendet. Zudem stimmen die hartcodierten Breiten nicht überein: BoolToWidthConverter liefert 240 px (expanded), NavigationViewModel.NavigationWidth liefert 220 px."
  }
]
```

---

## Zusammenfassung

| # | Schwere | Datei | Problem |
|---|---------|-------|---------|
| 1 | Kritisch | ProjectDetailView.xaml:121 | `EnumToBoolConverter` fehlt → XamlParseException beim Filter-Overlay |
| 2 | Hoch | DashboardView.xaml:64 | `int is true` nie wahr → Recovery-Banner erscheint nie |
| 3 | Hoch | ProjectDetailViewModel.cs:298 | Gelöschtes Projekt bleibt in der Liste |
| 4 | Hoch | RepositoryAssignViewModel.cs:16 | `VerfuegbareRepositories` nie befüllt → Feature nicht funktionsfähig |
| 5 | Mittel | MainWindowViewModel.cs:90 | `ProjectListViewModel` nicht gecacht → Zustand geht bei Navigation verloren |
| 6 | Mittel | ProjectDetailViewModel.cs:82 | Dispose-vor-SetProperty → potentielles Use-after-Dispose |
| 7 | Niedrig | RepositoryAssignDialog.xaml:8 | Fehlender Owner → Dialog zentriert sich auf Bildschirm |
| 8 | Niedrig | KiAusfuehrungsService.cs:79 | Handles werden nie aus `_handles` entfernt → unbegrenztes Wachstum |
| 9 | Niedrig | SettingsViewModel.cs:93 | Event-Subscription ohne Abmeldung → latentes Speicherleck |
| 10 | Cleanup | NavigationViewModel.cs:1 | Toter Code + inkonsistente Breite (240 vs 220 px) |
