# Tasks: Nacharbeiten zu Issue #147 (AngleSharp-Vulnerability + Flaky-Test)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `Softwareschmiede.Tests.csproj`: `<PackageReference Include="AngleSharp" Version="1.5.0" />` ergänzen | Offen | — |
| 2 | Konfiguration | `Softwareschmiede.Plugin.LocalDirectory.csproj`: `<InternalsVisibleTo Include="Softwareschmiede.Tests" />` ergänzen | Offen | — |
| 3 | Logik | `LocalDirectoryPlugin`: Feld `_enumerateDirectories` (`Func<string, IEnumerable<string>>`) anlegen | Offen | — |
| 4 | Logik | `LocalDirectoryPlugin`: `internal`-Konstruktor-Overload mit Parameter `enumerateDirectories` anlegen; öffentlicher Konstruktor setzt Default `Directory.EnumerateDirectories` | Offen | — |
| 5 | Logik | `LocalDirectoryPlugin.CollectDirectoryEntries`: Enumeration auf `_enumerateDirectories(currentPath)` umstellen | Offen | — |
| 6 | Tests | `LocalDirectoryPluginTests_GetRepositoryStructureAsync`: Seam-basierte SUT-Hilfe (nutzt `internal`-Konstruktor mit injiziertem Enumerator) bereitstellen | Offen | — |
| 7 | Tests | `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` deterministisch umschreiben (kleiner Baum, Enumerator löst `cts.Cancel()` aus; 3000er-Schleife und `CancelAfter(5ms)` entfernen) | Offen | — |
| 8 | Verifikation | Vollständiger Build + `dotnet test --filter "Category!=OsInterface"` mehrfach ausführen; AngleSharp-Warnung verschwunden bestätigen | Offen | — |
