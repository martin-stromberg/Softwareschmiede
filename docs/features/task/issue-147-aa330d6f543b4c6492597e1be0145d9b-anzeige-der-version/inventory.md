# Bestandsaufnahme: Nacharbeiten zu Issue #147

Diese Bestandsaufnahme analysiert die Anforderung zur Behebung einer Security-Vulnerability in `AngleSharp` und zur Robustifizierung eines anfälligen Tests auf dem Testprojekt `Softwareschmiede.Tests`.

## Zusammenfassung

### NuGet-Dependencies
- **Projektdatei:** `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- **Aktueller Stand:** `bunit` Version 2.7.2 ist als direkte Abhängigkeit konfiguriert (Zeile 14)
- **Vulnerability:** Die transitive Abhängigkeit `AngleSharp` 1.4.0 (gezogen von `bunit` 2.7.2) ist anfällig (GHSA-pgww-w46g-26qg, CVE-2026-54570, Moderate Severity)
- **Erforderlicher Fix:** `AngleSharp >= 1.5.0`
- **Optionen:** Entweder `bunit` auf eine neuere Version erhöhen, die bereits `AngleSharp 1.5.0+` abhängig macht, oder eine direkte `AngleSharp`-Referenz hinzufügen

### Flaky Test
- **Testdatei:** `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests_GetRepositoryStructureAsync.cs`
- **Testmethode:** `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()` (Zeilen 117–141)
- **Problem:** Der Test verwendet ein hartkodiertes 5-Millisekunden-Cancellation-Fenster mit nur 3000 Verzeichnissen; dies ist unter CI-Last zu eng und führt sporadisch zum Fehlschlag
- **Kritischer Code im Test:**
  - Zeile 124: `for (var i = 0; i < 3000; i++)` — Verzeichnisanzahl
  - Zeile 131: `cts.CancelAfter(TimeSpan.FromMilliseconds(5));` — Timing-Fenster
- **Getestete Klasse:** `Softwareschmiede.Infrastructure.Plugins.LocalDirectoryPlugin`
  - Methode: `GetRepositoryStructureLoadResultAsync()` (Zeilen 296–317)
  - Private Hilfsmethode: `CollectDirectoryEntries()` (Zeilen 319–368)
  - Cancellation-Check: Zeile 345 `ct.ThrowIfCancellationRequested()`

## Details

- [Logik - LocalDirectoryPlugin](inventory/logic.md)
- [Tests - LocalDirectoryPluginTests](inventory/tests.md)
- [Projekte und Dependencies](inventory/dependencies.md)
