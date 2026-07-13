# Bestandsaufnahme - Arbeitsverzeichnis fuer Bitbucket

## Ergebnis

Die Codebasis enthaelt bereits eine produktive Implementierung fuer den Remote-Abruf der Bitbucket-Verzeichnisstruktur. `BitbucketPlugin.GetRepositoryStructureAsync` unterstuetzt Bitbucket Cloud und Self-Hosted/Data-Center, ermittelt zuerst den Default-Branch und ruft danach die Verzeichnisse ueber die jeweilige Bitbucket-API ab.

Die wesentliche offene Luecke aus der Anforderung liegt nicht mehr im reinen Bitbucket-Abruf, sondern in der UI-/Service-Semantik fuer Fehlerfaelle: technische Abruffehler, nicht unterstuetzte Plugins und leere Repository-Strukturen werden aktuell alle zu derselben Arbeitsverzeichnisliste mit nur `"."` verdichtet. Dadurch kann die UI nicht zwischen Auswahlbox und manuellem Texteingabefeld wechseln.

## Detaildokumente

- [SCM-Plugins und Remote-Abruf](inventory/scm-plugins.md)
- [UI und Fallback-Verhalten](inventory/ui-fallback.md)
- [Persistenz und Validierung](inventory/persistence-validation.md)
- [Tests und Testluecken](inventory/tests.md)

## Betroffene Hauptkomponenten

- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/RepositoryDirectoryEntry.cs`
- `src/Softwareschmiede/Application/Services/DirectoryStructureBrowserService.cs`
- `src/Softwareschmiede.App/ViewModels/DirectoryStructureLoadHelper.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml`
- `src/Softwareschmiede.App/ViewModels/ArbeitsverzeichnisBearbeitenViewModel.cs`
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`

## Aktueller Ablauf

1. Die Projektansicht oeffnet beim Zuweisen eines Repositories den `RepositoryAssignViewModel`.
2. Nach Auswahl eines Repositories laedt das ViewModel ueber `DirectoryStructureBrowserService` die Verzeichnisstruktur des SCM-Plugins.
3. `DirectoryStructureLoadHelper` stellt immer `"."` voran.
4. Die WPF-Views zeigen immer eine `ComboBox` fuer `AvailableWorkingDirectories`.
5. Beim Speichern wird `SelectedWorkingDirectory` ueber `ProjektService.SaveRepositoryWorkingDirectoryAsync` in `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` abgelegt.

## Wichtigste Beobachtungen

- Bitbucket Cloud wird ueber `/2.0/repositories/{repositoryId}/src/{branch}/?max_depth=...&pagelen=100` geladen.
- Bitbucket Self-Hosted wird levelweise ueber `/rest/api/1.0/projects/{projectKey}/repos/{repoSlug}/browse...` geladen.
- GitHub dient als Referenz und nutzt `gh api repos/{owner}/{repo}/git/trees/{branch}?recursive=1`.
- `DirectoryStructureBrowserService.GetDirectoriesAsync` gibt bei Fehlern eine leere Liste zurueck.
- `DirectoryStructureLoadHelper.LoadWorkingDirectoriesAsync` gibt dadurch in Fehlerfaellen effektiv nur `"."` zurueck.
- Die UI hat aktuell keine Property wie `CanSelectWorkingDirectory`, `RequiresManualWorkingDirectoryInput` oder `DirectoryStructureLoadFailed`.
- Die Dialoge enthalten aktuell nur eine `ComboBox`, kein alternatives `TextBox`-Binding fuer manuelle Eingabe.

## Abgrenzung fuer die Umsetzung

Die geplante Umsetzung sollte den vorhandenen Bitbucket-Abruf nicht neu bauen, sondern gezielt die Ergebnissemantik zwischen Service und UI erweitern. Erforderlich ist ein Ergebnisobjekt oder aequivalenter Zustand, der mindestens folgende Faelle unterscheidet:

- erfolgreicher Abruf mit Verzeichnissen
- erfolgreicher Abruf ohne Unterverzeichnisse
- technischer Fehler beziehungsweise nicht verfuegbare Struktur
- erwarteter Abbruch durch Repository-/Plugin-Wechsel

Darauf aufbauend kann die UI bei technischen Fehlern ein Texteingabefeld anzeigen, waehrend sie bei erfolgreichem Abruf weiterhin die Auswahlbox nutzt.

