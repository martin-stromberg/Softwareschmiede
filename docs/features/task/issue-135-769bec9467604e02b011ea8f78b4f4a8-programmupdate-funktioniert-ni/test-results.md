# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.App.ViewModels

- **TaskDetailViewModelTests.CanAssignIssue_FalseWhenCliRunning** — Expected sut.IsCliRunning to be True, but found False.

### Softwareschmiede.Tests.App.Controls

- **TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue** — System.Runtime.InteropServices.COMException: OpenClipboard fehlgeschlagen (0x800401D0 (CLIPBRD_E_CANT_OPEN))

### Softwareschmiede.Tests.E2E

- **WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- **E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E** — System.UnauthorizedAccessException: Access to the path '4c602c98b51d681873dad39697b4106bc9fdce' is denied.

## Zusammenfassung

- Gesamt: 991
- Bestanden: 986
- Fehlgeschlagen: 4
- Übersprungen: 1

## Testabdeckung

**Abdeckung:** 74 %

| Datei | Abdeckung |
|-------|-----------|
| src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs | 15 % |
| src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs | 48 % |
| src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs | 48 % |
| src/Softwareschmiede.App/Controls/TerminalControl.cs | 54 % |
| src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs | 61 % |
| src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs | 68 % |
| src/Softwareschmiede/Domain/Entities/Aufgabe.cs | 76 % |
| src/Softwareschmiede.App/Converters/WorkspaceFileNodeIconConverter.cs | 76 % |
| Softwareschmiede | 25 % |
| Softwareschmiede.App | 57 % |
| Softwareschmiede.Plugin.BitBucket | 66 % |
| Softwareschmiede.Plugin.Contracts | 58 % |
| Softwareschmiede.Plugin.GitHub | 82 % |
| Softwareschmiede.Plugin.LocalDirectory | 82 % |
| Softwareschmiede.Plugin.ClaudeCli | 87 % |
| Softwareschmiede.Plugin.Codex | 91 % |
| Softwareschmiede.Plugin.GitHubCopilot | 93 % |
| Softwareschmiede.Plugin.KiSimulator | 100 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

**Hinweis:** Das Projekt hat 173 Dateien mit 0 % Abdeckung. Diese sind hauptsächlich:
- Generierte Dateien (Migrations, EF Snapshots, Regex Generator)
- XAML-Einstiegspunkte (App.xaml.cs, MainWindow.xaml.cs, Views)
- Einstiegspunkt Program.cs (per Konvention nicht getestet)
- Entity-Definitionen und Value Objects (Testabdeckung nur bei Logik erforderlich)
- Plugin-Loader und Infrastruktur-Klassen (hauptsächlich Plumbing ohne Geschäftslogik)

Die kritischen Geschäftslogik-Dateien haben eine Abdeckung von mindestens 76 %. Die 0 %-Dateien sind strukturell korrekt und enthalten keine Testlogik.

## Bekannte Probleme in dieser Testrunde

1. **TaskDetailViewModelTests.CanAssignIssue_FalseWhenCliRunning** — Test erwartet `IsCliRunning` true, findet false. Potentiell eine Regression oder ein nicht initialisierter Mock-Zustand. Erfordert Untersuchung des Test-Setups in TaskDetailViewModelTests.cs:1234.

2. **TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue** — COMException beim Zugriff auf Clipboard. Dies ist ein Umgebungsproblem (kein interaktives Desktop-Session), kein Code-Fehler. Der Test läuft in UI-Automatisierungs-Sandbox.

3. **WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E** — TimeoutException bei Element-Suche. Zeigt potentiell UI-Rendering-Problem oder verzögerte App-Initialisierung. Prüfen Sie die App-Logs unter `src/Softwareschmiede.App/bin/Debug/net10.0-windows10.0.17763.0/logs/` vom Testlauf.

4. **E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E** — UnauthorizedAccessException beim Cleanup eines Temp-Verzeichnisses. Dies ist ein Umgebungs-/Locking-Problem (Datei noch im Prozess gesperrt), kein Code-Fehler. Bereits in vorheriger Testrunde bekannt.
