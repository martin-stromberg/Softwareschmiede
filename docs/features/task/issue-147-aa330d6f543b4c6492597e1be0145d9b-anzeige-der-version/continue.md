# Offene Aufgaben

Erstellt am: 2026-07-18
Abbruchgrund: Manuell durch Anwender gemeldete CI-Befunde auf PR #148 (Security-Scan und Testlauf), nach Abschluss der ursprünglichen Anforderung "Anzeige der Version".

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] **Verwundbares NuGet-Paket:** `AngleSharp` 1.4.0 (transitive Abhängigkeit von `bunit` 2.7.2 im Projekt `Softwareschmiede.Tests`) hat eine Moderate-Severity-Schwachstelle (GHSA-pgww-w46g-26qg, CVE-2026-54570), behoben in AngleSharp 1.5.0. `bunit` 2.7.2 referenziert `AngleSharp` mit `"1.4.0"` (Mindestversion, kein Exact-Pin) — Update-Optionen: (a) `bunit` auf eine Version aktualisieren, die bereits AngleSharp >=1.5.0 referenziert, oder (b) in `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` eine direkte `PackageReference` auf `AngleSharp` >=1.5.0 ergänzen, die die transitive Version überschreibt. Betrifft ausschließlich das Testprojekt, nicht `Softwareschmiede.App` oder `Softwareschmiede`.
- [ ] **Fehlgeschlagener Test:** `LocalDirectoryPluginTests_GetRepositoryStructureAsync.GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` (`src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests_GetRepositoryStructureAsync.cs:117`) schlug im "test"-Job von PR #148 fehl: "Expected a System.OperationCanceledException to be thrown, but no exception was thrown." Ursache: Der Test setzt `cts.CancelAfter(TimeSpan.FromMilliseconds(5))` und erzeugt/traversiert dabei 3000 Verzeichnisse, damit der Abbruch zuverlässig *während* der Traversierung (nicht davor) greift — ein hartes 5-ms-Zeitfenster, das unter Last (z. B. auf dem GitHub-Actions-Windows-Runner) reißen kann, wenn die Traversierung schneller fertig ist als erwartet. Empfehlung: Zeitfenster vergrößern und/oder Verzeichnisanzahl erhöhen, um die Race robuster zu machen, oder auf eine deterministische Abbruch-Simulation (z. B. Callback/Hook statt reinem Timing) umstellen.

## Einordnung (Antwort auf Anwenderfrage)

Beide Befunde sind **nicht** durch die Änderungen dieser Anforderung (`MainWindowViewModel.cs`, `MainWindow.xaml`, `E2E_VersionAnzeige.cs`) verursacht — der Branch-Diff enthält keine Änderungen an `.csproj`/`.slnx`-Dateien, und `LocalDirectoryPluginTests_GetRepositoryStructureAsync.cs` wurde laut `git log` zuletzt im unabhängigen Feature zu Issue #98 geändert.

Sie sind auch **kein Fehler bei der Test-Kategorisierung aus der vorherigen Session** (Credential-Store-Isolation der E2E-Tests / ConPTY-Sortierung `Category=OsInterface`): Der fehlgeschlagene Test ist ein einfacher `[Fact]` ohne `[OsInterface]`-Attribut und lief im regulären "Test regular tests"-Job, genauso wie vorgesehen — er gehörte nie zu den ConPTY-/Clipboard-/E2E-Tests, die in jener Session sortiert wurden. Es handelt sich um einen eigenständigen, bisher unbemerkten Flaky-Test mit einer zu knapp bemessenen Zeitrace (5 ms), unabhängig vom OsInterface-Splitting.

Die NuGet-Schwachstelle ist ebenfalls unabhängig: `AngleSharp` ist eine bereits vor dieser Anforderung vorhandene transitive Abhängigkeit von `bunit`; die Advisory wurde vom Security-Scan-Job jetzt (erneut) aufgedeckt, unabhängig vom Umfang dieser Anforderung.

## Code-Review-Befunde

Keine (betrifft CI-Befunde nach Merge-Vorbereitung, nicht den Code-Review-Zyklus der Anforderung selbst).

## Fehlgeschlagene Tests

- `LocalDirectoryPluginTests_GetRepositoryStructureAsync.GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` — siehe oben.

## Quellen

- PR: https://github.com/martin-stromberg/Softwareschmiede/pull/148
- Security-Scan-Lauf: https://github.com/martin-stromberg/Softwareschmiede/actions/runs/29634544356
- Test-Lauf: https://github.com/martin-stromberg/Softwareschmiede/actions/runs/29634544406
