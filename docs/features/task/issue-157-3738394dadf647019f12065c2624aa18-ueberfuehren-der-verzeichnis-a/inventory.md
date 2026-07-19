# Bestandsaufnahme: Überführen der Verzeichnis-Aktionsbuttons in das Ribbon-Menü

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezüglich der Anforderung zur Integration der Dateiexplorer-Aktionsbuttons in die Ribbon-Menü-Struktur und zur Hinzufügung von zwei neuen Aktionsbuttons für das Arbeitsverzeichnis und die IDE.

---

## Zusammenfassung

**Was bereits existiert:**

- ✓ **TaskDetailViewModel:** Zentrale ViewModel-Klasse mit etabliertem Property-Change-Pattern und bestehenden Sichtbarkeitseigenschaften (`ShowFileExplorerPanel`, `ShowCliPanel`, etc.)
- ✓ **FileExplorerViewModel:** Presentation Model des Dateiexplorers mit allen erforderlichen Commands (`StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand`, `DateiMitStandardanwendungOeffnenCommand`)
- ✓ **TaskDetailView.xaml:** Ribbon-Struktur mit etabliertem Muster für Gruppen, Buttons und Sichtbarkeitsbindungen. Vier Ribbon-Gruppen vorhanden (Navigation, Aufgabe, CLI, Issue)
- ✓ **IGitWorkspaceBrowserService / GitWorkspaceBrowserService:** Service zum Laden von Repository-Status, Arbeitsbaum und Dateivorschauen
- ✓ **Test-Infrastruktur:** Unit-Tests für ViewModels vorhanden (TaskDetailViewModelTests, FileExplorerViewModelTests), etablierte Test-Muster

**Was fehlt:**

- ✗ **WorkspaceExplorerService / WorkdirService:** NEU erforderlich zum Öffnen des Arbeitsverzeichnisses im OS-Dateiexplorer
- ✗ **IdeService / VisualStudioService:** NEU erforderlich zum Öffnen von Visual Studio mit Solution-Datei
- ✗ **Neue Ribbon-Gruppe „Dateien":** Mit Buttons für Ansichtswechsel, Refresh, Dateiöffnung (aus FileExplorer) und zwei neuen Buttons
- ✗ **Neue Commands in TaskDetailViewModel:** `OeffneArbeitsverzeichnisCommand`, `OeffneIdeCommand`
- ✗ **Neue Properties in TaskDetailViewModel:** `SolutionFileExists`, optional `ShowFileSystemGroup`
- ✗ **IDE-Typ-Enum:** Optional, falls mehrere IDEs unterstützt werden sollen
- ✗ **Umfangreiche Test-Abdeckung:** Unit-Tests für neue Commands/Properties, E2E-Tests für neue Buttons

---

## Details

Ausführliche Analysen für jeden relevanten Bereich:

- [ViewModels (TaskDetailViewModel, FileExplorerViewModel)](inventory/viewmodels.md) — Bestehende Struktur, Properties, Commands, Abhängigkeiten
- [Services (IGitWorkspaceBrowserService, fehlende WorkspaceExplorerService, IdeService)](inventory/services.md) — Existierende und erforderliche Services, Schnittstellendefinitionen
- [UI-Komponenten und Enums (TaskDetailView.xaml, DateibrowserAnsichtsmodus, Ribbon-Controls)](inventory/ui-and-enums.md) — Ribbon-Struktur, bestehende/fehlende Gruppen und Buttons, UI-Control-Muster
- [Tests (TaskDetailViewModelTests, FileExplorerViewModelTests, fehlende E2E-Tests)](inventory/tests.md) — Bestehende Unit-Test-Infrastruktur, erforderliche neue Tests

---

## Kritische Abhängigkeiten

1. **FileExplorerViewModel → IGitWorkspaceBrowserService:** Bereits gelöst via Dependency Injection
2. **TaskDetailViewModel → FileExplorerViewModel:** Bereits gelöst, Property `FileExplorer` exportiert
3. **Neue Services → Process-Starter:** Erforderlich für OS-Dateiexplorer und IDE-Start
4. **IdeService → Repository-Scanning:** Erforderlich zum Finden von `*.sln`-Dateien

---

## Offene Fragen (aus Anforderung)

Diese müssen vor der Implementierung geklärt werden:

1. **IDE-Unterstützung:** Nur Visual Studio, oder auch VS Code, JetBrains Rider, etc.?
2. **Fehlerbehandlung:** Was soll passieren, wenn die IDE nicht installiert ist?
3. **Solution-Auswahl:** Alle `*.sln`-Dateien auflisten, oder nur eine spezifische (z. B. mit Branch-Name)?
4. **Sichtbarkeit bei fehlendem Repository:** Sollten die neuen Buttons deaktiviert (disabled) sein, wenn kein Repository vorhanden ist?

---

## Implementierungs-Roadmap (Hinweis)

Diese Bestandsaufnahme bildet die Grundlage für die Implementierungsplanung. Die typische Reihenfolge wäre:

1. Neue Services erstellen: `WorkspaceExplorerService`, `IdeService`
2. Commands und Properties in `TaskDetailViewModel` hinzufügen
3. Ribbon-Markup in `TaskDetailView.xaml` erweitern (neue Gruppe „Dateien")
4. Unit-Tests für neue Components schreiben
5. E2E-Tests für neue Buttons schreiben
6. Integration und Verifikation

---

*Diese Bestandsaufnahme wurde erstellt durch Analyse des Quellcodes unter `src/` und konzentriert sich auf aktuelle Implementierungen, nicht auf zukünftige Entwürfe.*
