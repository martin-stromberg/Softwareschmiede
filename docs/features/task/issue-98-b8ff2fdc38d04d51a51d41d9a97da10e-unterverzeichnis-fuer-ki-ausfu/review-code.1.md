# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Fokus des Reviews lag auf den zuletzt nachimplementierten, noch uncommitteten Änderungen
(`LocalDirectoryPlugin.GetRepositoryStructureAsync` und die neue UI zur nachträglichen
Bearbeitung des Arbeitsverzeichnisses). Die geprüften Sicherheits-, Cancellation- und
MVVM-Aspekte sind überwiegend solide gelöst; die Persistenz-Normalisierung (`"."` → `null`),
die Reparse-Point-/`.git`-Ausschlüsse und die Konsistenz der DialogService-Registrierung sind
korrekt. Die folgenden Befunde betreffen Robustheit, Nebenläufigkeit und Wartbarkeit.

## Befunde

### LocalDirectoryPlugin.cs (LocalDirectoryPlugin)

- **Kopplung/Nebenläufigkeit (Threading)** — `GetRepositoryStructureAsync` (Zeile 289–302) ist als
  `Task`-Methode deklariert, führt die potenziell teure rekursive Verzeichnistraversierung
  (`CollectDirectoryEntries`) aber vollständig synchron aus und verpackt das Ergebnis nur via
  `Task.FromResult`. Es gibt keinen echten `await`/Yield-Punkt. Im Aufrufpfad
  `ProjectDetailViewModel.ArbeitsverzeichnisBearbeitenAsync` → `LadenAsync` →
  `DirectoryStructureBrowserService.GetDirectoriesAsync` → `GetRepositoryStructureAsync` läuft die
  Traversierung damit synchron auf dem UI-Thread (die vorgelagerten `await`s kehren mangels echtem
  Yield synchron zurück). Bei einem großen lokalen Verzeichnisbaum friert die UI ein, bis die
  Traversierung fertig ist. Das übergebene `CancellationToken` wird zwar kooperativ geprüft, ändert
  aber nichts an der Blockade des aufrufenden Threads.

  Empfehlung: Die eigentliche Traversierung in `Task.Run(() => CollectDirectoryEntries(...), ct)`
  auslagern (oder die Methode `async` machen und die IO-Schleife per `Task.Run` offloaden), damit
  der UI-Thread nicht durch die blockierende Datei-IO belegt wird.

- **Fehlerbehandlung/Robustheit** — In `CollectDirectoryEntries` (Zeile 328–345) ist nur der Aufruf
  `Directory.EnumerateDirectories(currentPath)` gegen `UnauthorizedAccessException` abgesichert. Der
  anschließende `IsReparsePoint(directory)`-Aufruf (Zeile 332) ruft `File.GetAttributes(path)`
  (Zeile 348–352) ungeschützt auf. Wird ein Verzeichnis zwischen Enumeration und Attributabfrage
  gelöscht/unzugänglich (TOCTOU), oder liefert der Zugriff `UnauthorizedAccessException` /
  `IOException`, propagiert die Exception nach oben und bricht die **gesamte** Traversierung ab. Der
  aufrufende `DirectoryStructureBrowserService` fängt das global ab und liefert eine leere Liste –
  d. h. ein einzelnes problematisches Unterverzeichnis lässt die komplette Struktur verschwinden.

  Empfehlung: `IsReparsePoint` defensiv gestalten (try/catch → im Zweifel als „überspringen"
  behandeln) oder den Schleifenkörper pro Verzeichnis in try/catch kapseln und das betroffene
  Verzeichnis überspringen statt die Traversierung abzubrechen.

- **Toter/überflüssiger Code (async ohne await, CS1998)** — `CopyDirectoryForSyncAsync` (Zeile
  642–659) ist als `private async Task` deklariert, enthält aber keinen einzigen `await`. Das erzeugt
  Compiler-Warnung CS1998 und die Methode läuft vollständig synchron. (Committed als Teil von
  `3f0bd9c`, aber im Branch-Diff enthalten.)

  Empfehlung: `async` entfernen und den Body synchron ausführen mit `return Task.CompletedTask;` bzw.
  die IO tatsächlich asynchron gestalten. Konsistenz mit den anderen `…Async`-Methoden beachten.

### ArbeitsverzeichnisBearbeitenViewModel.cs (ArbeitsverzeichnisBearbeitenViewModel)

- **Doppelter Code / fehlende Kapselung** — `LoadDirectoryStructureAsync` (Zeile 75–114) dupliziert
  im Wesentlichen die Lade-Logik aus `RepositoryAssignViewModel.LoadDirectoryStructureAsync`
  (Zeile 210–247): `AvailableWorkingDirectories` mit `"."` + geladenen Verzeichnissen befüllen,
  `IsLoadingDirectoryStructure`-Toggle, identisches `catch (OperationCanceledException) when
  (ct.IsCancellationRequested)` plus generisches `catch (Exception)` mit derselben Log-Meldung
  „Fehler beim Laden der Verzeichnisstruktur." Diese nahezu identische Logik existiert nun in zwei
  ViewModels.

  Empfehlung: Die gemeinsame Lade-/Fehlerbehandlungs-Logik in eine wiederverwendbare Hilfsmethode
  auslagern – z. B. als Methode am `DirectoryStructureBrowserService` (der ohnehin schon alle
  Exceptions kapselt) oder in eine gemeinsame Basis/Erweiterung für beide ViewModels.

- **Defensiver, praktisch unerreichbarer catch-Block** — Der `try/catch (Exception ex)` um
  `_directoryStructureService.GetDirectoriesAsync(...)` (Zeile 82–98) ist faktisch nicht erreichbar:
  `DirectoryStructureBrowserService.GetDirectoriesAsync` fängt intern bereits jede `Exception` ab und
  liefert eine leere Liste (siehe `DirectoryStructureBrowserService.cs` Zeile 56–60). Die
  tatsächliche Abbruchbehandlung erfolgt ausschließlich über das nachgelagerte
  `ct.ThrowIfCancellationRequested()` (Zeile 86). Der generische `catch (Exception)` fängt damit nie
  einen realen Fehler aus dem Service.

  Empfehlung: Bewusst entscheiden – entweder den generischen Catch entfernen (da unerreichbar) oder,
  falls der Service künftig Exceptions durchreichen soll, dies dokumentieren. Konsistent mit dem
  identischen Muster in `RepositoryAssignViewModel` behandeln.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`
- `src/Softwareschmiede.App/ViewModels/ArbeitsverzeichnisBearbeitenViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs` (Vergleichsmuster)
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml`
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Services/IDialogService.cs`
- `src/Softwareschmiede.App/Services/WpfDialogService.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede/Application/Services/DirectoryStructureBrowserService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs` (SaveRepositoryWorkingDirectoryAsync)
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/RepositoryDirectoryEntry.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests_GetRepositoryStructureAsync.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ArbeitsverzeichnisBearbeitenViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests_Arbeitsverzeichnis.cs`
