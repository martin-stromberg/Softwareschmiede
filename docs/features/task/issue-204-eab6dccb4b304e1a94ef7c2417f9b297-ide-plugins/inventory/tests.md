# Bestandsaufnahme: Tests

## Testklassen

### `IdeOeffnenServiceTests`

**Datei:** `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs`

**Umfang:** 14 Testmethoden, befestigt auf dem aktuellen Verhalten (inklusive Type-Check-Sonderfall)

**Tests für `FindeSolutions()`:**
- `FindeSolutions_LiefertAlleSlnAlphabetischSortiert()` — Prüft, dass alle `.sln`/`.slnx`-Dateien gefunden und alphabetisch sortiert zurückgegeben werden
- `FindeSolutions_OhneSln_LiefertLeereListe()` — Leere Liste bei fehlenden `.sln`-Dateien
- `FindeSolutions_NichtExistierendesVerzeichnis_LiefertLeereListe()` — Leere Liste bei nicht existierendem Verzeichnis
- `FindeSolutions_LeererPfad_LiefertLeereListe(string? pfad)` — Theory-Test mit null und "" — liefert leere Liste

**Tests für `OeffneSolution()`:**
- `OeffneSolution_StartetShellExecuteFuerSln()` — Prüft, dass `IProzessStarter.Starten()` mit `ShellAusfuehren: true` aufgerufen wird
- `OeffneSolution_MitLeeremPfad_WirftArgumentException(string solutionPfad)` — Theory-Test: wirft bei leerem/whitespace-Pfad
- `OeffneSolution_WennProzessStarterWirft_ReichtAusnahmeUnveraendertWeiter()` — Exception-Weitergabe-Test

**Tests für `OpenRepositoryInIdeAsync()`:**
- `OpenRepositoryInIdeAsync_OhnePluginSelectionService_Wirft()` — Wirft `InvalidOperationException` ohne PluginSelectionService
- `OpenRepositoryInIdeAsync_MitLeeremPfad_WirftArgumentException(string repositoryPfad)` — Theory-Test: wirft bei leerem/whitespace-Pfad
- `OpenRepositoryInIdeAsync_LoestPluginAufUndOeffnet()` — Prüft, dass Plugin aufgelöst und `OpenRepositoryAsync()` aufgerufen wird
- **`OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndVisualStudioPlugin_RuftCallbackAufUndOeffnetGewaehlteSolution()` — Tests die aktuelle Leaky-Abstraktion! (Zeilen 175–201)** <br/>Dies ist der kritische Test, der zeigt, dass der Type-Check `if (plugin is VisualStudioIdePlugin)` in Aktion tritt. Wenn mehrere `.sln`-Dateien existieren, wird der Callback aufgerufen und die gewählte Solution wird via `OeffneSolution()` geöffnet (nicht via `plugin.OpenRepositoryAsync()`).
- `OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndAbgebrochenerAuswahl_OeffnetNichts()` — Callback gibt null zurück, nichts wird geöffnet
- `OpenRepositoryInIdeAsync_MitGenauEinerSolutionUndCallback_RuftCallbackNichtAufUndOeffnetDirekt()` — Mit nur einer `.sln` wird Callback nicht aufgerufen, `.sln` wird direkt geöffnet

**Hilfsmethoden:**
- `CreateTempDirectory()` — Erzeugt temporäres Test-Verzeichnis via `TestTempDirectoryFixture`
- `CreateService()` — Erzeugt `IdeOeffnenService` mit Mock-`IProzessStarter`
- `CreatePluginSelectionService(IReadOnlyList<IIdePlugin> idePlugins)` — Erzeugt kompletten `PluginSelectionService` mit Datenbank, `PluginManager`, `AppEinstellungService`, etc.

**Fixture:**
- `_tempDirectoryFixture` — `TestTempDirectoryFixture`, wird im `Dispose()` aufgeräumt

---

### `VisualStudioIdePluginTests`

**Datei:** `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioIdePluginTests.cs`

**Umfang:** 6 Testmethoden

**Tests für Eigenschaften:**
- `Eigenschaften_SindKorrektGesetzt()` — Prüft `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`

**Tests für `CheckCompatibilityAsync()`:**
- `CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnExists()` — Gibt `Explicit` bei vorhandener `.sln`
- `CheckCompatibilityAsync_ShouldReturnExplicit_WhenSlnxExists()` — Gibt `Explicit` bei vorhandener `.slnx`
- `CheckCompatibilityAsync_ShouldReturnIncompatible_WhenNoSlnFound()` — Gibt `Incompatible` ohne `.sln`/`.slnx`
- `CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull()` — Wirft bei null-Pfad
- `CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty()` — Wirft bei leerer Pfad
- `CheckCompatibilityAsync_ShouldReturnIncompatible_WhenPathDoesNotExist()` — Gibt `Incompatible` bei nicht existierendem Pfad

**Tests für `OpenRepositoryAsync()`:**
- `OpenRepositoryAsync_ShouldOpenFirstSolution_WhenMultipleExist()` — Prüft, dass nur die erste (alphabetisch) `.sln` geöffnet wird; mehrere Solutions werden ignoriert

**Fixture:**
- `_tempDirectoryFixture` — `TestTempDirectoryFixture`, wird im `Dispose()` aufgeräumt

---

### `VisualStudioCodeIdePluginTests`

**Datei:** `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioCodeIdePluginTests.cs`

**Umfang:** 6 Testmethoden

**Tests für Eigenschaften:**
- `Eigenschaften_SindKorrektGesetzt()` — Prüft `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`

**Tests für `CheckCompatibilityAsync()`:**
- `CheckCompatibilityAsync_ShouldReturnFallback_Always()` — Gibt immer `Fallback` zurück
- `CheckCompatibilityAsync_ShouldThrowArgumentNullException_WhenPathIsNull()` — Wirft bei null-Pfad
- `CheckCompatibilityAsync_ShouldThrowArgumentException_WhenPathIsEmpty()` — Wirft bei leerer Pfad

**Tests für `OpenRepositoryAsync()`:**
- `OpenRepositoryAsync_ShouldCallProzessStarter_WithCodeCommand()` — Prüft, dass `IProzessStarter.Starten()` mit aufgelöstem `code.cmd` und gequottem Pfad aufgerufen wird
- `OpenRepositoryAsync_ShouldThrow_WhenVsCodeNotAvailable()` — Wirft `InvalidOperationException`, wenn VS Code nicht auflösbar

**Hilfsmethoden:**
- `CreateSut()` — Erzeugt `VisualStudioCodeIdePlugin` mit Mock-`IProzessStarter` und `TestVisualStudioCodeLocator`
- `CreateLocator()` — Erzeugt `TestVisualStudioCodeLocator`

---

### `PluginSelectionServiceTests_IdePlugin`

**Datei:** `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests_IdePlugin.cs`

**Umfang:** 7 Testmethoden

**Tests für `ResolveIdePluginAsync()`:**
- `ResolveIdePluginAsync_ShouldReturnExplicitPlugin_WhenAvailable()` — Gibt Plugin mit `Explicit` zurück
- `ResolveIdePluginAsync_ShouldReturnFirstExplicitPlugin_WhenMultipleAvailable()` — Gibt erstes `Explicit` Plugin zurück
- `ResolveIdePluginAsync_ShouldReturnFallbackPlugin_WhenNoExplicitAvailable()` — Gibt `Fallback` Plugin zurück, wenn kein `Explicit` verfügbar
- `ResolveIdePluginAsync_ShouldReturnFallback_WhenFirstIncompatible()` — Gibt `Fallback` zurück, wenn erstes Plugin `Incompatible` ist
- `ResolveIdePluginAsync_ShouldRespectPluginOrder_FromSetting()` — Respektiert `plugins.ide.order` Setting
- `ResolveIdePluginAsync_ShouldReturnDefaultPlugin_WhenNoPluginActive()` — Gibt Default-Plugin zurück, wenn keine Plugins aktiv
- `ResolveIdePluginAsync_ShouldReturnDefaultPlugin_WhenNoPluginCompatible()` — Gibt Default-Plugin zurück, wenn kein Plugin kompatibel

**Hilfsmethoden:**
- `CreateSut()` — Erzeugt kompletten `PluginSelectionService`
- `CreateAppEinstellungService()` — Erzeugt `AppEinstellungService`
- `CreateDb()` — Erzeugt Test-Datenbank via `TestDbContextFactory.Create()`
- `CreatePluginManager()` — Erzeugt Mock `IPluginManager`
- `CreateIdePlugin()` — Erzeugt Mock `IIdePlugin`

**Hinweis:** Nicht direkt relevant für die vorliegende Anforderung, da die Plugin-Auflösungslogik sich nicht ändert.

---

### `TaskDetailViewModelTests_VisualStudioCode`

**Datei:** `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs`

**Umfang:** 4 Testmethoden

**Tests für `OeffneIdeAsync()`:**
- `OeffneIdeAsync_OhneSolutionMitKonfiguriertemArbeitsverzeichnis_RuftVsCodeMitAufgeloestemPfadAuf()` — Prüft, dass VS Code mit aufgelöstem Arbeitsverzeichnis-Pfad (via `WorkingDirectoryResolver`) gestartet wird
- `OeffneIdeAsync_OhneSolutionOhneKonfiguration_RuftVsCodeMitRepositoryRootAuf()` — Prüft, dass VS Code mit Repository-Root gestartet wird, wenn keine `RepositoryStartKonfiguration`
- `OeffneIdeAsync_OhneSolutionOhneVsCode_ZeigtFehlermeldung()` — Zeigt Fehlermeldung, wenn VS Code nicht verfügbar
- `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck()` — Fällt zu VS Code zurück, wenn keine `.sln` im aufgelösten Arbeitsverzeichnis

**Basis-Klasse:**
- Erbt von `TaskDetailViewModelTestsBase`

**Hinweis:** Diese Tests sind ebenfalls von der Type-Check-Eliminierung betroffen, da der Callback `waehleEntryPointAsync` eine andere Signatur bekommen soll.

---

## Hilfsmethoden / Test-Utilities

### `TestTempDirectoryFixture`

**Datei:** (nicht vollständig gelesen, aber in Tests verwendet)

**Zweck:** Verwaltet temporäre Testverzeichnisse und räumt sie im `Dispose()` auf

**Methoden:**
- `CreateTempDirectory(string prefix)` — Erzeugt temporäres Verzeichnis mit Präfix

### `TestDbContextFactory`

**Datei:** (nicht vollständig gelesen)

**Zweck:** Erzeugt Test-Datenbank-Kontexte

**Methoden:**
- `Create()` → `SoftwareschmiededDbContext`

### `TestVisualStudioCodeLocator`

**Datei:** (nicht vollständig gelesen)

**Zweck:** Test-Double für `IVisualStudioCodeLocator`

**Methoden:**
- Constructor nimmt `VisualStudioCodeAvailability` auf
- `Locate()` → gibt die im Constructor übergebene Availability zurück

---

## Zusammenfassung: Testabdeckung für neue Anforderung

**Betroffen durch die Refactoring:**

1. **`IdeOeffnenServiceTests`** — Großteil der Tests muss angepasst werden:
   - Test für Type-Check wird überflüssig (Zeilen 175–201)
   - Tests müssen auf neue `FindEntryPointsAsync` / `OpenEntryPointAsync` Calls angepasst werden
   - Callback-Signatur ändert sich von `IReadOnlyList<string>` zu `IReadOnlyList<IdeEntryPoint>`

2. **`VisualStudioIdePluginTests`** — Neue Tests erforderlich:
   - Tests für `FindEntryPointsAsync()` (mehrere `.sln`, eine `.sln`, keine `.sln`)
   - Tests für `OpenEntryPointAsync(IdeEntryPoint entryPoint)`

3. **`VisualStudioCodeIdePluginTests`** — Neue Tests erforderlich:
   - Tests für `FindEntryPointsAsync()` (sollte immer genau 1 zurückgeben)
   - Tests für `OpenEntryPointAsync(IdeEntryPoint entryPoint)`

4. **`TaskDetailViewModelTests_VisualStudioCode`** — Anpassung erforderlich:
   - Callback-Signatur ändert sich, Tests müssen angepasst werden
