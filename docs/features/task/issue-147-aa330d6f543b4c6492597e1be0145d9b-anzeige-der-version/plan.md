# Umsetzungsplan: Nacharbeiten zu Issue #147 (AngleSharp-Vulnerability + Flaky-Test)

## Übersicht

Zwei unabhängige Wartungs-Fixes am Testprojekt bzw. am LocalDirectory-Plugin: (1) Behebung der Security-Vulnerability GHSA-pgww-w46g-26qg (CVE-2026-54570) in der transitiven Abhängigkeit `AngleSharp 1.4.0` und (2) Robustifizierung des zeitabhängigen Flaky-Tests `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal`. Betroffen sind ausschließlich `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`, die Testklasse `LocalDirectoryPluginTests_GetRepositoryStructureAsync` sowie ein kleiner Test-Seam in `LocalDirectoryPlugin`. Keine Auswirkung auf produktives Laufzeitverhalten.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| AngleSharp-Fix | Variant A2 – direkte `<PackageReference Include="AngleSharp" Version="1.5.0" />` im Testprojekt (Gateway auf die transitive Version) | Löst die Vulnerability garantiert und versionsunabhängig, ohne ein `bunit`-Upgrade zu riskieren (vermeidet potenzielle Breaking Changes gegenüber `FlaUI`/`Moq`/`xunit`). Beantwortet die offenen Fragen 1 und 2 der Anforderung: eine unverifizierte neuere `bunit`-Version ist nicht erforderlich. Variant A1 (bunit-Bump) wäre nur ein alternativer Weg mit zusätzlichem Kompatibilitätsrisiko. |
| Flaky-Test-Robustifizierung | Ansatz 3 (deterministisch) – Test-Seam über einen injizierbaren Verzeichnis-Enumerator (`Func<string, IEnumerable<string>>`) statt Wall-Clock-Timing | Beseitigt die Timing-Abhängigkeit vollständig statt sie nur zu verkleinern (Ansätze 1/2 bleiben probabilistisch und verlängern die Testlaufzeit durch Tausende Verzeichnisse auf der Platte). Die Anforderung nennt den deterministischen Ansatz ausdrücklich als „bevorzugt" und sanktioniert die Refaktorierung des Traversierungs-Codes für eine injizierbare Abbruch-Auslösung. |
| Sichtbarkeit des Seams | `internal` Konstruktor-Overload + `[InternalsVisibleTo("Softwareschmiede.Tests")]`; öffentlicher 3-Argument-Konstruktor bleibt unverändert | Immutable Constructor Injection (kein mutierbarer öffentlicher Zustand); der produktive öffentliche Vertrag und alle anderen Aufrufer bleiben unberührt. |

## Programmabläufe

### Deterministischer Abbruch während der Traversierung (Test)

1. Testaufbau erstellt ein kleines Wurzelverzeichnis mit wenigen Unterverzeichnissen (kein 3000er-Baum mehr nötig).
2. Der Test erzeugt das SUT über den `internal`-Konstruktor und injiziert einen Verzeichnis-Enumerator, der beim ersten Aufruf zunächst `cts.Cancel()` ausführt und anschließend das reale `Directory.EnumerateDirectories(path)`-Ergebnis liefert.
3. Der Test ruft `GetRepositoryStructureAsync(root, maxDepth, cts.Token)` auf. Der Upfront-Check `ct.ThrowIfCancellationRequested()` in `GetRepositoryStructureLoadResultAsync` läuft durch, weil der Token zu diesem Zeitpunkt noch nicht abgebrochen ist (der Enumerator wird erst innerhalb von `Task.Run` aufgerufen).
4. Innerhalb von `Task.Run` ruft `CollectDirectoryEntries` den injizierten Enumerator auf → dieser bricht den Token ab und liefert die Verzeichnisliste.
5. Die erste Iteration der `foreach`-Schleife erreicht `ct.ThrowIfCancellationRequested()` (bisher Zeile 345) und wirft deterministisch eine `OperationCanceledException`.
6. Der Test verifiziert die `OperationCanceledException`. Kein Wall-Clock-Delay, keine Last-Abhängigkeit.

Beteiligte Klassen/Komponenten: `LocalDirectoryPlugin`, `LocalDirectoryPluginTests_GetRepositoryStructureAsync`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `LocalDirectoryPlugin` (sealed class, `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`)

- **Neues Feld:** `_enumerateDirectories` (`Func<string, IEnumerable<string>>`) — Verzeichnis-Enumerations-Seam; Standard `Directory.EnumerateDirectories`.
- **Neuer Konstruktor (`internal`):** Overload mit zusätzlichem Parameter `Func<string, IEnumerable<string>> enumerateDirectories`, delegiert an den bestehenden öffentlichen Konstruktor und setzt das Feld. Der öffentliche 3-Argument-Konstruktor bleibt bestehen und initialisiert das Feld mit `Directory.EnumerateDirectories`.
- **Geänderte Methode:** `CollectDirectoryEntries` — verwendet `_enumerateDirectories(currentPath).ToList()` statt des direkten Aufrufs `Directory.EnumerateDirectories(currentPath).ToList()` (bisher Zeile 335). Verhalten im Produktivbetrieb unverändert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine benutzersichtbare Konfiguration. Projekt-/Build-Konfigurationsänderungen:

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `<PackageReference Include="AngleSharp" Version="1.5.0" />` in `Softwareschmiede.Tests.csproj` | NuGet-PackageReference | 1.5.0 | Überschreibt die anfällige transitive Version 1.4.0 aus `bunit`. |
| `[InternalsVisibleTo("Softwareschmiede.Tests")]` für `Softwareschmiede.Plugin.LocalDirectory` (via `<InternalsVisibleTo>`-Item in der `.csproj`) | MSBuild-/Assembly-Attribut | — | Macht den `internal`-Konstruktor-Seam für das Testprojekt sichtbar. |

## Seiteneffekte und Risiken

- **AngleSharp 1.5.0 vs. bunit 2.7.2:** Eine explizite direkte Referenz auf eine höhere Minor-Version einer transitiven Abhängigkeit ist ein etabliertes Muster. Rest-Risiko einer Inkompatibilität zwischen `bunit 2.7.2` und `AngleSharp 1.5.0` ist gering; wird durch grünen Build + bestehende bUnit-Tests verifiziert.
- **`LocalDirectoryPlugin`-Seam:** Der neue `internal`-Konstruktor und das Feld ändern das produktive Verhalten nicht (Default-Enumerator identisch zum bisherigen Aufruf). Kein anderer Aufrufer ist betroffen, da der öffentliche Konstruktor unverändert bleibt.
- **Wegfall des 3000er-Verzeichnisbaums:** Reduziert Platten-I/O und Testlaufzeit; keine Auswirkung auf andere Tests.

## Umsetzungsreihenfolge

1. **AngleSharp-Referenz hinzufügen**
   - Voraussetzungen: Keine.
   - Beschreibung: In `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` `<PackageReference Include="AngleSharp" Version="1.5.0" />` im vorhandenen PackageReference-`ItemGroup` ergänzen. Restore/Build ausführen und prüfen, dass die AngleSharp-Vulnerability-Warnung (NU1903/NU1904) verschwindet.

2. **Test-Seam im Plugin einführen**
   - Voraussetzungen: Keine.
   - Beschreibung: In `LocalDirectoryPlugin` das Feld `_enumerateDirectories` und den `internal`-Konstruktor-Overload anlegen, `CollectDirectoryEntries` auf das Feld umstellen. In `Softwareschmiede.Plugin.LocalDirectory.csproj` `<InternalsVisibleTo Include="Softwareschmiede.Tests" />` ergänzen.

3. **Flaky-Test deterministisch umschreiben**
   - Voraussetzungen: Schritt 2 (internal-Konstruktor + InternalsVisibleTo müssen existieren).
   - Beschreibung: `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` auf den deterministischen Ablauf umstellen (kleiner Baum, injizierter Enumerator, der `cts.Cancel()` auslöst). 3000er-Schleife und `CancelAfter(5ms)` entfernen. Optional eine lokale Seam-Konstruktor-Hilfsmethode für dieses Szenario ergänzen (das gemeinsame `CreateSut` bleibt für die übrigen Tests unverändert).

4. **Verifikation**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: Vollständiger Build, danach `dotnet test ... --filter "Category!=OsInterface"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Test mehrfach ausführen, um Determinismus zu bestätigen (offene Frage 4: mehrfache Ausführung).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| Seam-basierte SUT-Erstellung (lokale Hilfe im Testszenario, nutzt `internal`-Konstruktor) | `LocalDirectoryPluginTests_GetRepositoryStructureAsync` | Stellt ein `LocalDirectoryPlugin` mit injiziertem Verzeichnis-Enumerator bereit. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal` (in `LocalDirectoryPluginTests_GetRepositoryStructureAsync`) | Wird von timing-basiert (3000 Verzeichnisse, `CancelAfter(5ms)`) auf deterministischen Seam umgestellt. |

### E2E-Tests (Pflicht)

Keine. Beide Nacharbeiten sind reine Wartungs-Fixes ohne neue oder geänderte Benutzerinteraktion; ein E2E-Test ist fachlich nicht anwendbar. Der betroffene Abbruch-Pfad wird durch den deterministischen Unit-Test vollständig abgedeckt.

Betroffene bestehende E2E-Tests: Keine.

## Offene Punkte

Keine.
