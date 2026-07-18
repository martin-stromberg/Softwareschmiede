# Anforderung: Nacharbeiten zu Issue #147

## Fachliche Zusammenfassung

Nach erfolgreicher Fertigstellung der Anzeige der Programmversion (Issue #147) wurden im Pull Request #148 zwei unabhängige Nacharbeiten identifiziert, die zur Stabilitäts- und Sicherheitspflege des Testprojekts notwendig sind:

1. **Security-Vulnerability behoben:** Das Testprojekt zieht eine anfällige Version von `AngleSharp` 1.4.0 als transitive Abhängigkeit von `bunit` 2.7.2. Die Schwachstelle GHSA-pgww-w46g-26qg (CVE-2026-54570, Moderate Severity) ist in AngleSharp 1.5.0 behoben. Ein Update ist erforderlich.

2. **Flaky Test robust gemacht:** Der Test `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` schlägt sporadisch in CI-Umgebungen fehl, weil er mit einem hart kodierten 5-Millisekunden-Cancellation-Fenster einen Timing-abhängigen Zustand testet, der unter Last reißen kann.

---

## Betroffene Klassen und Komponenten

### NuGet-Dependency-Management

- **Projektdatei:** `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
  - Derzeit: `<PackageReference Include="bunit" Version="2.7.2" />`
  - Betroffen: transitive Abhängigkeit `AngleSharp` 1.4.0

- **Betroffene Assemblies:** Nur das Test-Projekt (`Softwareschmiede.Tests`); keine Auswirkung auf `Softwareschmiede.App` oder `Softwareschmiede`.

### Test-Klasse und -Methode

- **Testklasse:** `Softwareschmiede.Tests.Infrastructure.Plugins.LocalDirectoryPluginTests_GetRepositoryStructureAsync`
  - **Testmethode:** `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()` (Zeilen 117–141)
  - **Getestete Klasse:** `Softwareschmiede.Infrastructure.Plugins.LocalDirectoryPlugin`
    - Methode: `GetRepositoryStructureLoadResultAsync()` (Zeile 296)
    - Private Methode: `CollectDirectoryEntries()` (Zeile 319, enthält das `ct.ThrowIfCancellationRequested()` auf Zeile 345)

---

## Implementierungsansatz

### Option A: AngleSharp-Vulnerability beheben

**Zwei Möglichkeiten (nicht beide erforderlich):**

1. **Variant A1: `bunit` aktualisieren** (bevorzugt, wenn stabil)
   - Prüfe, ob eine neuere Version von `bunit` (z. B. 2.7.3+, 2.8.x, 3.x) bereits `AngleSharp >= 1.5.0` abhängig macht.
   - Wenn ja: `bunit` in `Softwareschmiede.Tests.csproj` auf diese Version erhöhen.
   - Vorteil: Transitive Abhängigkeits-Verwaltung durch `bunit` selbst.

2. **Variant A2: Direkte `AngleSharp`-Referenz einführen** (Fallback / zusätzliche Sicherheit)
   - In `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` eine neue `<PackageReference />` hinzufügen:
     ```xml
     <PackageReference Include="AngleSharp" Version="1.5.0" />
     ```
   - Diese überschreibt die transitive Version aus `bunit` durch NuGet-Dependency-Resolution.
   - Vorteil: Explizite Kontrolle, unabhängig von `bunit`-Version; zusätzliche Sicherheit gegen künftige `bunit`-Versionen, die noch ältere `AngleSharp` ziehen könnten.

**Empfehlung:** Erst Variant A1 versuchen; wenn kein passender `bunit`-Release existiert oder Kompatibilitätsprobleme entstehen, auf Variant A2 fallen.

### Option B: Test robust gegenüber Timing-Schwankungen machen

**Aktuelle Schwäche:**
- Der Test erstellt 3.000 flache Verzeichnisse und setzt dann `cts.CancelAfter(TimeSpan.FromMilliseconds(5))`.
- Ziel: Abbruch zuverlässig *während* der Verzeichnis-Traversierung auslösen (nicht vor Start oder nach Fertigstellung).
- Problem: Das 5-ms-Fenster ist zu eng, besonders auf Last-Tests (GitHub-Actions-Runner) — die Traversierung kann schneller fertig sein als erwartet, ohne dass der Abbruch überhaupt geprüft wird.

**Lösungsansätze (mind. einer ist erforderlich):**

1. **Zeitfenster vergrößern:**
   - `TimeSpan.FromMilliseconds(5)` → `TimeSpan.FromMilliseconds(50)` oder höher.
   - Gibt der Traversierung mehr Zeit, bietet aber immer noch ein Fenster, in dem der Abbruch eintritt.

2. **Verzeichnisanzahl erhöhen:**
   - `3000` → `10000` oder mehr, damit die Traversierung garantiert länger dauert als das Zeitfenster.
   - Kombiniert mit oder ohne Zeitfenster-Anpassung.

3. **Deterministische Abbruch-Simulation (bevorzugt):**
   - Statt auf reales Timing zu verlassen, nutze eine `ManualResetEvent` oder ähnlich explizites Synchronisations-Primitive:
     - Erstelle einen Mock oder Wrapper um `IEnumerator<string>` von `Directory.EnumerateDirectories()`, der nach N Iterationen ein Flag setzt.
     - Der Traversierungs-Code prüft vor dem `ct.ThrowIfCancellationRequested()` dieses Flag und setzt das `CancellationToken` dann explizit ab.
     - Vorteil: Vollständig deterministisch, keine Hardware-Last-Abhängigkeit.
   - Alternative: `LocalDirectoryPlugin.CollectDirectoryEntries` so refaktorieren, dass sie einen optionalen Callback akzeptiert, der sich nach jedem Verzeichnis triggert und Abbruch injizieren kann.

**Empfehlung:** Kombinieren Sie Ansätze 1 und 2 (vergrößert beide Parameter) für schnelle Verbesserung; evaluieren Sie Ansatz 3 für langfristige Robustheit.

---

## Konfiguration

Keine Konfigurationsebene erforderlich. Beide Nacharbeiten sind Wartungs-Fixes ohne Benutzer-sichtbare Einstellungen.

---

## Offene Fragen

1. **NuGet-Update:** Gibt es eine getestete neuere Version von `bunit`, die bereits `AngleSharp >= 1.5.0` zieht? Muss vor A1-Implementierung validiert werden.

2. **Breaking Changes:** Wäre das Update auf eine neuere `bunit`-Version ein Breaking Change für andere Test-Abhängigkeiten (z. B. `FlaUI`, `Moq`, `xunit`)?

3. **Test-Robustheit:** Werden andere timinglastige Tests im Projekt beobachtet, die ähnliche Probleme aufweisen? Diese Anforderung könnte ein Muster für ähnliche Fälle etablieren.

4. **CI-Verifikation:** Nach Implementierung: Wurde der Test mindestens 10 Mal in CI-Umgebung ausgeführt, ohne Fehlschlag?

---

## Anhang: Betroffene Code-Stellen

### Schwachstelle
- **GHSA-pgww-w46g-26qg** (CVE-2026-54570)
- **Betroffenes Paket:** AngleSharp 1.4.0
- **Fix:** AngleSharp >= 1.5.0

### Test-Implementierung (zu robustifizieren)
- **Datei:** `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests_GetRepositoryStructureAsync.cs`
- **Methode:** `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` (Zeilen 117–141)
- **Kritischer Code im Test:** 
  - Zeile 124: `for (var i = 0; i < 3000; i++)` — Verzeichnisanzahl
  - Zeile 131: `cts.CancelAfter(TimeSpan.FromMilliseconds(5));` — Timing-Fenster
- **Getestete Stelle in Production-Code:**
  - `LocalDirectoryPlugin.CollectDirectoryEntries()` (Zeile 319–368)
  - Zeile 345: `ct.ThrowIfCancellationRequested();` — wo Abbruch prüft wird
