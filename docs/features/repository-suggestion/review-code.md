# Code-Review: Branch `repository-suggestion`

Geprüfte Dateien: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`,
`src/Softwareschmiede/Migrations/20260626204930_202606260001_MigrateGitRepositoryPluginTyp.cs`,
`src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

---

```json
[
  {
    "file": "src/Softwareschmiede/Migrations/20260626204930_202606260001_MigrateGitRepositoryPluginTyp.cs",
    "line": 15,
    "summary": "Die Migration lässt zwei Klassen von Altdaten unangetastet: Zeilen mit PluginTyp='SourceCodeManagement' und On-Premises-URLs (kein github.com / bitbucket.org) sowie Zeilen mit PluginTyp='GitHub' (historisch vom Blazor-UI gespeicherter Wert), die alle für keinen Plugin-Prefix je einen Treffer liefern.",
    "failure_scenario": "Repositories, die mit einer Corporate-Bitbucket-Server-URL oder einem selbst gehosteten GitHub-Enterprise-Host angelegt wurden, behalten PluginTyp='SourceCodeManagement'. Repositories, die das Blazor-UI mit pluginTyp='GitHub' (LegacyGitHubPluginType) gespeichert hat, behalten 'GitHub'. Der neue Vergleich string.Equals(p.PluginPrefix, repository.PluginTyp, OrdinalIgnoreCase) in KannIssuesLaden (Z. 151) und LadenIssuesAsync (Z. 474) findet für beide Werte keinen Treffer — Issues werden dauerhaft leer angezeigt, ohne Fehlermeldung. Benötigt werden zwei zusätzliche UPDATE-Statements: eins für PluginTyp='GitHub' → 'Softwareschmiede.GitHub' und eins für verbleibende 'SourceCodeManagement'-Zeilen (nach URL-Heuristik für bekannte Hosts)."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 114,
    "summary": "Der SelectedRepository-Setter benachrichtigt nur über 'SelectedRepository', nie über 'KannIssuesLaden' — das WPF-Binding für das Issue-Panel erhält keine Änderungsmeldung und bleibt dauerhaft in seinem Startzustand stecken.",
    "failure_scenario": "KannIssuesLaden ist eine reine Ausdruckseigenschaft ohne Backing-Feld. ViewModelBase verwendet keinen Source Generator (kein [NotifyPropertyChangedFor]). SetProperty auf Z. 114 feuert PropertyChanged nur für 'SelectedRepository'. Das XAML-Binding auf Z. 236 (BoolToVisibilityConverter) wertet KannIssuesLaden einmalig beim ersten Rendering aus und aktualisiert sich danach nie — das Issue-Panel ist entweder dauerhaft sichtbar oder dauerhaft unsichtbar, egal welches Repository ausgewählt wird."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 511,
    "summary": "IssueZuweisenAsync wählt blind das erste registrierte SCM-Plugin, ignoriert aber welches Plugin das Repository der Aufgabe besitzt — in einer Multi-Plugin-Umgebung werden Issues vom falschen Host geladen.",
    "failure_scenario": "Z. 511: GetSourceCodeManagementPlugins().OfType<IGitPlugin>().FirstOrDefault() nimmt immer Plugin[0]. Wenn das System sowohl 'Softwareschmiede.GitHub' als auch 'Softwareschmiede.Bitbucket' geladen hat und die Aufgabe einem Bitbucket-Repository (PluginTyp='Softwareschmiede.Bitbucket') zugeordnet ist, aber GitHub als erstes Plugin registriert ist, sendet der Code die GetIssuesAsync-Anfrage an GitHub mit der Bitbucket-RepositoryUrl — GitHub antwortet leer oder mit Fehler. _aufgabe.GitRepository?.PluginTyp ist auf Z. 517 ohnehin verfügbar und würde die korrekte Filterung ermöglichen, analog zum Fix in ProjectDetailViewModel Z. 474."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 149,
    "summary": "KannIssuesLaden ruft GetSourceCodeManagementPlugins() bei jedem Property-Read auf, ohne Cache — und löst dabei keinerlei Änderungsbenachrichtigung aus.",
    "failure_scenario": "Die Eigenschaft ist XAML-gebunden. Jedes Mal, wenn WPF das Binding neu auswertet (z.B. durch DataContext-Refresh), wird GetSourceCodeManagementPlugins() erneut aufgerufen. Da gleichzeitig keine PropertyChanged-Benachrichtigung für KannIssuesLaden existiert (siehe Befund #2), wird das Ergebnis weder korrekt aktuell gehalten noch effizient gecacht — die Logik ist in beiden Richtungen defekt."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 376,
    "summary": "scmPlugin.PluginPrefix wird als pluginTyp an AddRepositoryAsync übergeben — für Bitbucket-Repositories fehlt aber ein entsprechender Zweig in ValidateRequiredFields, sodass keine Pflichtfeldprüfung stattfindet.",
    "failure_scenario": "ProjektService.ValidateRequiredFields (Z. 317) prüft nur LocalDirectoryPlugin und IsGitHubPlugin ('GitHub' oder 'Softwareschmiede.GitHub'). 'Softwareschmiede.Bitbucket' trifft keinen Zweig, ValidateRequiredFields endet kommentarlos. Erst in ResolveRepositoryUrl (Z. 344) folgt eine generische Exception, wenn RepositoryUrl fehlt — ohne den spezifischen Kontext 'Für Bitbucket ist RepositoryUrl ein Pflichtfeld'. Außerdem wird RepositoryName nicht validiert, sodass ein leerer Name für Bitbucket-Repositories lautlos in die DB geschrieben wird."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs",
    "line": 472,
    "summary": "GetSourceCodeManagementPlugins() wird in LadenIssuesAsync aufgerufen, obwohl .OfType<IGitPlugin>() überflüssig ist — der Rückgabetyp IReadOnlyList<IGitPlugin> garantiert bereits, dass alle Elemente IGitPlugin sind.",
    "failure_scenario": "scmPlugins.OfType<IGitPlugin>().FirstOrDefault(p => ...) erzeugt einen zusätzlichen Iterator-Wrapper, der jeden Eintrag auf den Typ IGitPlugin prüft, obwohl GetSourceCodeManagementPlugins() bereits IReadOnlyList<IGitPlugin> zurückgibt. Der Filter ist immer true und hat keinen Effekt, signalisiert Lesern aber fälschlicherweise, dass Nicht-IGitPlugin-Elemente möglich wären. Die direkte Form .FirstOrDefault(p => ...) auf dem IReadOnlyList-Ergebnis genügt. (Hinweis: Die tatsächlich eingecheckte Datei hat .OfType<IGitPlugin>() bereits entfernt; im Diff ist es noch vorhanden.)"
  }
]
```
