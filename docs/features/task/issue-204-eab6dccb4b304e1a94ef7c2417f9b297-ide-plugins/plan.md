# Umsetzungsplan: Generalisierung des IDE-Plugin-Mehreinstiegspunkt-Vertrags

## Übersicht

Dieser Plan dokumentiert die Implementierung einer Architektur-Verbesserung am IDE-Plugin-System: Entfernung des leaky Type-Checks (`plugin is VisualStudioIdePlugin`) in `IdeOeffnenService.OpenRepositoryInIdeAsync` durch Generalisierung des `IIdePlugin`-Vertrags. Das Refactoring führt zwei neue abstrakte Methoden (`FindEntryPointsAsync`, `OpenEntryPointAsync`) und einen neuen Value-Object-Typ (`IdeEntryPoint`) ein, so dass alle IDE-Plugins generisch Mehrfach-Einstiegspunkte beschreiben können. Die Änderung betrifft primär Service-, Plugin- und Test-Klassen sowie die Callback-Signatur in `TaskDetailViewModel`.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **`IdeEntryPoint` Typ** | `public record IdeEntryPoint(string Path, string? DisplayName = null)` | Immutable Value Object; einfache Serialisierbarkeit; optionales DisplayName für UI-Anzeige (z. B. „Solution 1", „VS Code") |
| **Neue Interface-Methoden** | Beide Methoden (`FindEntryPointsAsync`, `OpenEntryPointAsync`) als abstrakte/pure virtuelle Methoden erzwungen | Sichert Bewusstsein aller Plugin-Implementierungen; keine stillen Fallbacks möglich |
| **Fehlerbehandlung bei 0 Einstiegspunkten** | `FindEntryPointsAsync` gibt leere Liste zurück (kein Exception); `IdeOeffnenService` entscheidet, ob kritisch | Optimale Separation of Concerns: Plugin findet Kandidaten, Service entscheidet über Fehlerbehandlung |
| **Callback-Signatur Migration** | Single-shot, keine parallele Rückwärtskompabilität | Bessere Wartbarkeit; Eindeutigkeit in Methodensignaturen; die neue Signatur bringt mehr Kontext (`IdeEntryPoint` statt nur Pfad-String) |
| **Speicherort für `IdeEntryPoint`** | Neue Datei in `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs` | Klare Trennung von Interfaces (ValueObjects als Schwester zu Interfaces im Contracts-Assembly) |

---

## Programmabläufe

### Öffnen eines Repositories mit generischen Mehreinstiegspunkten

1. `TaskDetailViewModel.OeffneIdeAsync()` ruft `IdeOeffnenService.OpenRepositoryInIdeAsync(repositoryPath, waehleEntryPointAsync, ct)` auf
2. `IdeOeffnenService.OpenRepositoryInIdeAsync()` ruft `pluginSelectionService.ResolveIdePluginAsync(repositoryPath, ct)` auf → liefert das beste kompatible Plugin
3. Service ruft `plugin.FindEntryPointsAsync(repositoryPath, ct)` auf (neu) → liefert `IReadOnlyList<IdeEntryPoint>`
4. Service bewertet die Anzahl der Einstiegspunkte:
   - **0 Punkte:** `FileNotFoundException` werfen (keine Kandidaten vorhanden)
   - **1 Punkt:** Direkt `plugin.OpenEntryPointAsync(entryPoints[0], ct)` aufrufen (kein User-Dialog erforderlich)
   - **>1 Punkte und Callback vorhanden:** `waehleEntryPointAsync(entryPoints, ct)` aufrufen (UI wählt einen Punkt aus)
     - Falls Callback `null` zurückgibt: Abbruch, nichts öffnen
     - Falls Callback `IdeEntryPoint` zurückgibt: `plugin.OpenEntryPointAsync(selectedEntryPoint, ct)` aufrufen
   - **>1 Punkte und kein Callback:** Fallback: `plugin.OpenEntryPointAsync(entryPoints[0], ct)` aufrufen (öffne ersten Kandidaten)

Beteiligte Klassen/Komponenten: `IdeOeffnenService`, `IIdePlugin`, `VisualStudioIdePlugin`, `VisualStudioCodeIdePlugin`, `PluginSelectionService`, `TaskDetailViewModel`

### VisualStudio-spezifische Mehrfach-Solutions-Behandlung (in Plugin delegiert)

1. `VisualStudioIdePlugin.FindEntryPointsAsync(repositoryPath, ct)` ruft interne statische Methode `FindSolutionFiles(repositoryPath)` auf
2. `FindSolutionFiles` enumert alle `*.sln` und `*.slnx`-Dateien im Repo-Root (top-level, alphabetisch sortiert), gibt `List<string>` zurück
3. `FindEntryPointsAsync` konvertiert jeden Pfad zu `IdeEntryPoint(path, displayName: null)` oder mit aussagekräftigem DisplayName
4. Returns `IReadOnlyList<IdeEntryPoint>` (kann leer sein, wenn keine `.sln`/`.slnx` vorhanden)

Beteiligte Klassen/Komponenten: `VisualStudioIdePlugin`

### VS-Code-spezifische Einstiegspunkt-Behandlung (in Plugin delegiert)

1. `VisualStudioCodeIdePlugin.FindEntryPointsAsync(repositoryPath, ct)` gibt immer exakt `[new IdeEntryPoint(repositoryPath, "Visual Studio Code")]` zurück
2. Keine Mehrfach-Szenarien — der Einstiegspunkt ist immer das Repo-Root

Beteiligte Klassen/Komponenten: `VisualStudioCodeIdePlugin`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IdeEntryPoint` | Record (Value Object) | Immutable Datenträger für einen IDE-Einstiegspunkt: Pfad (erforderlich) + optionale Beschreibung für UI-Anzeige |

---

## Änderungen an bestehenden Klassen

### `IIdePlugin` (Interface)

- **Neue Methoden:**
  - `FindEntryPointsAsync(string repositoryPath, CancellationToken ct = default)` → `Task<IReadOnlyList<IdeEntryPoint>>` — Ermittelt alle verfügbaren Einstiegspunkte für das gegebene Repository. Kann leere Liste zurückgeben. Abstrakt/erzwungen auf allen Implementierungen.
  - `OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct = default)` → `Task` — Öffnet den übergebenen Einstiegspunkt in der IDE. Abstrakt/erzwungen auf allen Implementierungen.

### `VisualStudioIdePlugin` (Implementierung von `IIdePlugin`)

- **Neue Methoden:**
  - `FindEntryPointsAsync(string repositoryPath, CancellationToken ct)` — Ruft interne statische Methode `FindSolutionFiles(repositoryPath)` auf, konvertiert jeden String-Pfad zu `IdeEntryPoint(path, null)`, gibt `IReadOnlyList<IdeEntryPoint>` zurück. Kann leer sein.
  - `OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct)` — Ruft interne statische Methode `OpenSolutionFile(prozessStarter, entryPoint.Path)` auf.
- **Unverändert:** `CheckCompatibilityAsync`, `OpenRepositoryAsync` (diese bleiben bestehen, werden aber in `OpenRepositoryInIdeAsync` nicht mehr direkt aufgerufen für Mehrfach-Szenarien)

### `VisualStudioCodeIdePlugin` (Implementierung von `IIdePlugin`)

- **Neue Methoden:**
  - `FindEntryPointsAsync(string repositoryPath, CancellationToken ct)` — Gibt immer `[new IdeEntryPoint(repositoryPath, "Visual Studio Code")]` zurück (oder optional DisplayName basierend auf Locator-Status).
  - `OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct)` — Ruft interne statische Methode `OpenDirectory(prozessStarter, visualStudioCodeLocator, entryPoint.Path)` auf.
- **Unverändert:** `CheckCompatibilityAsync`, `OpenRepositoryAsync`

### `IdeOeffnenService` (Service)

- **Geänderte Methoden:**
  - `OpenRepositoryInIdeAsync(string repositoryPath, Func<IReadOnlyList<IdeEntryPoint>, CancellationToken, Task<IdeEntryPoint?>>? waehleEntryPointAsync = null, CancellationToken ct = default)` — **Callback-Signatur ändert sich:** statt `Func<IReadOnlyList<string>, ...>` jetzt `Func<IReadOnlyList<IdeEntryPoint>, ...>` (gibt auch `IdeEntryPoint?` statt `string?` zurück). Die Logik wird generalisiert:
    - Entfernung des Type-Checks (`plugin is VisualStudioIdePlugin`)
    - Immer `plugin.FindEntryPointsAsync(repositoryPath, ct)` aufrufen
    - Verzweigung nach Anzahl der Einstiegspunkte (0, 1, >1)
    - Der alte fallback-Aufruf `plugin.OpenRepositoryAsync()` für den Allgemeinfall entfällt; stattdessen wird immer über die neuen Methoden gearbeitet
  - **Hilfsmethoden `FindeSolutions` und `OeffneSolution`:** Werden ggf. nicht mehr als öffentlich benötigt, da die Logik ins Plugin wandert. Können weiterhin als internal helper Methods vorhanden sein, werden aber nicht mehr direkt vom Service aufgerufen. (Später ggf. auf private zurückgestuft, wenn keine anderen Aufrufer vorhanden.)

### `TaskDetailViewModel` (ViewModel)

- **Geänderte Methoden:**
  - `OeffneIdeAsync()` — Parameter für den Callback in `IdeOeffnenService.OpenRepositoryInIdeAsync()` ändert sich. Der lokale Callback bzw. dessen Signatur muss angepasst werden: statt `Func<IReadOnlyList<string>, ...>` jetzt `Func<IReadOnlyList<IdeEntryPoint>, ...>`. Die Logik zur UI-Auswahl einer Solution wird auf die neuen `IdeEntryPoint`-Objekte angepasst.

---

## Datenbankmigrationen

**Keine.** — Das Feature ist rein Logik- und Service-seitig, keine Datenmodell-Änderungen.

---

## Validierungsregeln

**Keine neuen Validierungsregeln erforderlich.** — `IdeEntryPoint.Path` wird als nicht-null record-Property erzwungen. Die bestehenden Validierungen in `IdeOeffnenService.OpenRepositoryInIdeAsync` (null/leer-Checks für `repositoryPath`) bleiben bestehen.

---

## Konfigurationsänderungen

**Keine.** — Das Feature benötigt keine zusätzlichen Konfigurationseinträge.

---

## Seiteneffekte und Risiken

- **Callback-Signatur-Änderung:** Der Callback in `TaskDetailViewModel.OeffneIdeAsync()` muss angepasst werden. Der Callback erhält nun `IReadOnlyList<IdeEntryPoint>` statt `IReadOnlyList<string>` und gibt `IdeEntryPoint?` statt `string?` zurück. Dies ist eine breaking change für alle Aufrufer von `OpenRepositoryInIdeAsync`.
- **Type-Check-Test wird obsolet:** Der Test `OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndVisualStudioPlugin_RuftCallbackAufUndOeffnetGewaehlteSolution()` in `IdeOeffnenServiceTests` prüft die aktuelle Leaky-Abstraction-Logik mit Type-Check. Nach dem Refactoring wird dieser Test nicht länger den Type-Check testen, sondern die generische Mehrfach-Einstiegspunkt-Logik.
- **Alle IDE-Plugins müssen neue Methoden implementieren:** Jede neue `IIdePlugin`-Implementierung (falls es künftig weitere gibt) ist verpflichtet, `FindEntryPointsAsync` und `OpenEntryPointAsync` zu implementieren. Das ist beabsichtigt (explizites Design), birgt aber das Risiko, dass neue Plugins vergessen und Compiler-Fehler auslösen (positiv).
- **`OpenRepositoryAsync()` wird redundant:** Nach dem Refactoring wird `IdeOeffnenService.OpenRepositoryInIdeAsync()` die alte Methode `plugin.OpenRepositoryAsync()` nicht mehr aufrufen. Die Methode selbst bleibt zur Rückwärtskompabilität in den Plugins bestehen, wird aber nicht mehr genutzt. (Optional: später als `[Obsolete]` markieren.)

---

## Umsetzungsreihenfolge

1. **`IdeEntryPoint` Value Object definieren**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Record-Klasse in `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs` mit Eigenschaften `Path` (string, erforderlich) und `DisplayName` (string?, optional). Liegt im Contracts-Assembly, damit sie über die Plugin-Schnittstelle verfügbar ist.

2. **`IIdePlugin`-Interface um zwei neue abstrakte Methoden erweitern**
   - Voraussetzungen: `IdeEntryPoint` Klasse existiert
   - Beschreibung: In `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs` Methoden `FindEntryPointsAsync` und `OpenEntryPointAsync` als abstrakte Methoden hinzufügen. Beide sind erzwungen auf allen Implementierungen.

3. **`VisualStudioIdePlugin` – neue Methoden implementieren**
   - Voraussetzungen: `IIdePlugin`-Interface enthält die neuen Methoden
   - Beschreibung: In `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs` die beiden neuen Methoden implementieren. `FindEntryPointsAsync` wrapped `FindSolutionFiles` (bereits vorhanden), `OpenEntryPointAsync` wrapped `OpenSolutionFile` (bereits vorhanden).

4. **`VisualStudioCodeIdePlugin` – neue Methoden implementieren**
   - Voraussetzungen: `IIdePlugin`-Interface enthält die neuen Methoden
   - Beschreibung: In `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs` die beiden neuen Methoden implementieren. `FindEntryPointsAsync` gibt immer eine Liste mit genau einem Einstiegspunkt zurück, `OpenEntryPointAsync` wrapped `OpenDirectory` (bereits vorhanden).

5. **`IdeOeffnenService.OpenRepositoryInIdeAsync()` refaktorieren**
   - Voraussetzungen: Beide Plugin-Implementierungen implementieren die neuen Methoden
   - Beschreibung: In `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs` die Methode `OpenRepositoryInIdeAsync` generalisieren: Type-Check entfernen, immer `FindEntryPointsAsync` aufrufen, nach Anzahl der Einstiegspunkte verzweigen. Callback-Parameter-Typ von `IReadOnlyList<string>` zu `IReadOnlyList<IdeEntryPoint>` ändern und Return-Typ entsprechend anpassen.

6. **`TaskDetailViewModel.OeffneIdeAsync()` – Callback-Signatur anpassen**
   - Voraussetzungen: `IdeOeffnenService.OpenRepositoryInIdeAsync()` wurde refaktoriert
   - Beschreibung: In `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` den Callback, der an `OpenRepositoryInIdeAsync` übergeben wird, anpassen: Parameter von `IReadOnlyList<string>` zu `IReadOnlyList<IdeEntryPoint>` ändern, Return-Typ von `string?` zu `IdeEntryPoint?` ändern.

7. **`IdeOeffnenServiceTests` – Tests anpassen und neu schreiben**
   - Voraussetzungen: Service-Refactoring abgeschlossen
   - Beschreibung: Existierende Tests auf die neue Logik migrieren. Der Type-Check-Test wird angepasst, um die generische Mehrfach-Einstiegspunkt-Logik zu prüfen (nicht mehr Type-Check spezifisch). Neue Tests für 0/1/>1 Einstiegspunkt-Szenarien. Callback-Mock-Signaturen anpassen.

8. **`VisualStudioIdePluginTests` – Tests für neue Methoden schreiben**
   - Voraussetzungen: Plugin-Implementierungen abgeschlossen, `IdeEntryPoint` Klasse existiert
   - Beschreibung: In `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioIdePluginTests.cs` neue Tests für `FindEntryPointsAsync` (mehrere `.sln`, genau eine `.sln`, keine `.sln`, alphabetische Sortierung) und `OpenEntryPointAsync` (mit verschiedenen Pfaden) hinzufügen.

9. **`VisualStudioCodeIdePluginTests` – Tests für neue Methoden schreiben**
   - Voraussetzungen: Plugin-Implementierungen abgeschlossen, `IdeEntryPoint` Klasse existiert
   - Beschreibung: In `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioCodeIdePluginTests.cs` neue Tests für `FindEntryPointsAsync` (prüft, dass immer genau ein Einstiegspunkt zurückgegeben wird) und `OpenEntryPointAsync` (prüft Aufruf mit Repo-Path) hinzufügen.

10. **`TaskDetailViewModelTests_VisualStudioCode` – Tests anpassen**
    - Voraussetzungen: ViewModel-Änderungen und Service-Refactoring abgeschlossen
    - Beschreibung: In `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs` Tests anpassen, die den Callback verwenden. Mock-Callbacks müssen die neue Signatur (`Func<IReadOnlyList<IdeEntryPoint>, ...>`) implementieren.

11. **Dokumentation aktualisieren**
    - Voraussetzungen: Alle Code-Änderungen abgeschlossen
    - Beschreibung: 
      - `docs/help/entwicklungsumgebungen/architektur.md` — Abschnitt über IDE-Plugins aktualisieren: `IdeEntryPoint` als neuer Value-Object-Typ dokumentieren, das Datenfluss-Diagramm aktualisieren (nicht länger Typ-Check-basiert, sondern generische Mehrfach-Einstiegspunkte).
      - `docs/help/entwicklungsumgebungen/ablauf-technisch.md` — Sequenzdiagramm oder Pseudocode-Beschreibung aktualisieren: Der neue Workflow durch `FindEntryPointsAsync`, Verzweigung nach Entry-Point-Anzahl, Callback-Aufruf mit `IdeEntryPoint`-Objekten.
      - Hinweise auf den alten Type-Check-Sonderfall entfernen.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `FindEntryPointsAsync_MitMehrerneSln_LiefertAlleAlphabetischSortiert()` | `VisualStudioIdePluginTests` | Mehrere `.sln`-Dateien werden gefunden und als `IdeEntryPoint`-Liste zurückgegeben, alphabetisch sortiert |
| `FindEntryPointsAsync_MitGenauEinerSln_LiefertEinen()` | `VisualStudioIdePluginTests` | Genau eine `.sln` wird als `IdeEntryPoint` in Liste zurückgegeben |
| `FindEntryPointsAsync_OhneSln_LiefertLeereListe()` | `VisualStudioIdePluginTests` | Keine `.sln`/`.slnx` → leere Liste |
| `OpenEntryPointAsync_RuftOpenSolutionFileAuf()` | `VisualStudioIdePluginTests` | `OpenEntryPointAsync` mit `IdeEntryPoint` ruft interne `OpenSolutionFile` auf |
| `FindEntryPointsAsync_LiefertImmerGenauEinen()` | `VisualStudioCodeIdePluginTests` | VS Code Plugin gibt immer exakt einen `IdeEntryPoint` (Repository-Root) zurück |
| `OpenEntryPointAsync_RuftOpenDirectoryAuf()` | `VisualStudioCodeIdePluginTests` | `OpenEntryPointAsync` mit `IdeEntryPoint` ruft interne `OpenDirectory` auf |
| `OpenRepositoryInIdeAsync_MitEinemEinstiegspunkt_OeffnetDirekt()` | `IdeOeffnenServiceTests` | Service ruft `OpenEntryPointAsync` direkt auf, wenn `FindEntryPointsAsync` genau einen Punkt liefert (kein Callback) |
| `OpenRepositoryInIdeAsync_MitMeherenEinstiegspunktenUndCallback_RuftCallbackAufUndOeffnet()` | `IdeOeffnenServiceTests` | Service ruft generisch `FindEntryPointsAsync` auf; bei >1 Punkt wird Callback aufgerufen mit `IReadOnlyList<IdeEntryPoint>`; Service öffnet ausgewählten Punkt |
| `OpenRepositoryInIdeAsync_MitMeherenEinstiegspunktenUndAbgebrochenerAuswahl_OeffnetNichts()` | `IdeOeffnenServiceTests` | Wenn Callback `null` zurückgibt, wird nichts geöffnet |
| `OpenRepositoryInIdeAsync_OhneEinstiegspunkte_WirftFileNotFoundException()` | `IdeOeffnenServiceTests` | Service wirft `FileNotFoundException`, wenn `FindEntryPointsAsync` leere Liste liefert |
| `OeffneIdeAsync_RueftCallbackMitIdeEntryPoints_AufUndOeffnet()` | `TaskDetailViewModelTests_VisualStudioCode` | ViewModel übergibt Callback mit neuer Signatur (`IReadOnlyList<IdeEntryPoint>`) an Service |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndVisualStudioPlugin_RuftCallbackAufUndOeffnetGewaehlteSolution()` | Type-Check-basiert → Anpassung auf generische Logik; Callback-Signatur ändert sich von `IReadOnlyList<string>` zu `IReadOnlyList<IdeEntryPoint>` |
| `OpenRepositoryInIdeAsync_LoestPluginAufUndOeffnet()` | Service-Logik ändert sich; Test muss auf neue Mehrfach-Einstiegspunkt-Behandlung angepasst werden (nicht mehr direkter `OpenRepositoryAsync`-Aufruf, sondern über neue Methoden) |
| `OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndAbgebrochenerAuswahl_OeffnetNichts()` | Callback-Signatur ändert sich; Mock-Callback muss angepasst werden |
| `OpenRepositoryInIdeAsync_MitGenauEinerSolutionUndCallback_RuftCallbackNichtAufUndOeffnetDirekt()` | Callback-Signatur ändert sich; Logik wird generalisiert (nicht länger Visual-Studio-spezifisch) |
| `OeffneIdeAsync_OhneSolutionMitKonfiguriertemArbeitsverzeichnis_RuftVsCodeMitAufgeloestemPfadAuf()` | Callback-Signatur in `OeffneIdeAsync` ändert sich → Mock-Callback anpassen |
| `OeffneIdeAsync_OhneSolutionOhneKonfiguration_RuftVsCodeMitRepositoryRootAuf()` | Callback-Signatur anpassen |
| `OeffneIdeAsync_OhneSolutionOhneVsCode_ZeigtFehlermeldung()` | Callback-Signatur anpassen |
| `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck()` | Callback-Signatur anpassen |
| `OpenRepositoryInIdeAsync_OhnePluginSelectionService_Wirft()` | Ggf. minimale Anpassung (Service-Fehlerbehandlung kann sich ändern); prüfen, ob noch relevant |

### E2E-Tests (Pflicht)

**Hinweis:** Die bestehenden E2E-Tests in `TaskDetailViewModelTests_VisualStudioCode` (4 Tests) sind Integration-Tests (ViewModel + Service), nicht vollständige UI-End-to-End-Tests. Sie müssen angepasst werden, aber neue dedizierte E2E-Tests sind nicht erforderlich, da die Benutzer-Interaktion (Solution-Auswahl) sich nicht ändert — nur die interne Signatur.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| **Mehrere Solutions vorhanden → User wählt eine aus → IDE öffnet sie** | `IdeOeffnenServiceTests` (neu: `OpenRepositoryInIdeAsync_MitMeherenEinstiegspunktenUndCallback_RuftCallbackAufUndOeffnet`) | Service ruft Callback mit Einstiegspunkten auf, User-Auswahl wird verarbeitet, gewählte Solution wird via `OpenEntryPointAsync` geöffnet |
| **Genau eine Solution → direkt öffnen ohne Dialog** | `IdeOeffnenServiceTests` (neu: `OpenRepositoryInIdeAsync_MitEinemEinstiegspunkt_OeffnetDirekt`) | Service öffnet direkten Einstiegspunkt ohne Callback-Aufruf |
| **Keine Solutions → Fehler** | `IdeOeffnenServiceTests` (neu: `OpenRepositoryInIdeAsync_OhneEinstiegspunkte_WirftFileNotFoundException`) | Service wirft Fehler bei 0 Einstiegspunkten |
| **VS Code (1 Einstiegspunkt) → direkt öffnen** | `VisualStudioCodeIdePluginTests` (neu: `FindEntryPointsAsync_LiefertImmerGenauEinen`) | Plugin gibt genau einen Einstiegspunkt zurück |

**Betroffene bestehende E2E/Integrations-Tests:**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `OeffneIdeAsync_OhneSolutionMitKonfiguriertemArbeitsverzeichnis_RuftVsCodeMitAufgeloestemPfadAuf()` | Callback-Signatur-Änderung: Mock-Callback muss `IReadOnlyList<IdeEntryPoint>` akzeptieren |
| `OeffneIdeAsync_OhneSolutionOhneKonfiguration_RuftVsCodeMitRepositoryRootAuf()` | Callback-Signatur-Änderung: Mock-Callback anpassen |
| `OeffneIdeAsync_OhneSolutionOhneVsCode_ZeigtFehlermeldung()` | Callback-Signatur-Änderung: Mock-Callback anpassen |
| `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck()` | Callback-Signatur-Änderung: Mock-Callback anpassen |

---

## Offene Punkte

**Keine.** — Alle Designentscheidungen und Implementierungsdetails sind in requirement.md und den Recommendations bereits geklärt:
- ✓ `IdeEntryPoint` wird als record definiert
- ✓ Beide neue Interface-Methoden sind abstrakt/mandatory
- ✓ Fehlerbehandlung: leere Liste statt Exception bei 0 Punkten
- ✓ Callback-Signatur: Single-shot Migration, keine Rückwärtskompatibilität
- ✓ Speicherort `IdeEntryPoint`: Contracts Assembly, ValueObjects-Ordner
