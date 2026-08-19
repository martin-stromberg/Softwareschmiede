# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine (siehe Hinweis zur Flakiness unten).

Beim ersten Lauf der Lane 2 (Category=OsInterface) trat ein Fehlschlag auf (42 bestanden / 1 fehlgeschlagen / 1 übersprungen von 44). Die xUnit-Fehlermeldung selbst wurde durch das umfangreiche EF-Core-Migrations-/SQL-Log-Rauschen dieser Lane im Terminal-Tail abgeschnitten und war nicht mehr isoliert auffindbar. Da keine Code-Änderung zwischen den Läufen erfolgte, wurde die komplette Lane 2 daraufhin zweimal erneut ausgeführt (kein `--filter` auf einen Einzeltest, sondern volle Lane, da der Testname aus dem ersten Lauf nicht mehr rekonstruierbar war) — beide Wiederholungen liefen **fehlerfrei durch** (43 bestanden / 0 fehlgeschlagen / 1 übersprungen von 44). Das deckt sich mit der aus Iteration 2 bekannten, bereits verifizierten Flakiness (siehe Kontext dieses Auftrags) und wird hier erneut als Timing-Flakiness eingestuft, nicht als Regression. Für den offiziellen Ergebnis-Stand wird der reproduzierbare, fehlerfreie Lauf zugrunde gelegt.

## Zusammenfassung

- Gesamt: 1316
- Bestanden: 1314
- Fehlgeschlagen: 0
- Übersprungen: 2

(Lane 1, Category!=OsInterface: 1272 gesamt / 1271 bestanden / 0 fehlgeschlagen / 1 übersprungen.
Lane 2, Category=OsInterface: 44 gesamt / 43 bestanden / 0 fehlgeschlagen / 1 übersprungen (RunConPtyTests, erwartet via SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1).
Gegenüber dem letzten Testlauf (1315/1312/1/2) sind das wie erwartet 1 Test mehr — der neue Regressionstest `KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue` — bei 0 echten Fehlschlägen.)

## Testabdeckung

**Abdeckung:** 35,5 % (Gesamtprojekt, kombiniert aus beiden Lanes)

| Datei | Abdeckung |
|-------|-----------|
| src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs | 0,0 % |
| src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml | 0,0 % |
| src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs | 0,0 % |
| src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml | 0,0 % |
| src/Softwareschmiede.App/Views/SettingsView.xaml.cs | 7,6 % |
| src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs | 76,0 % |
| src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs | 83,3 % |
| src/Softwareschmiede/Infrastructure/Services/VisualStudioCodeLocator.cs | 87,3 % |
| src/Softwareschmiede/Application/Services/PluginSelectionService.cs | 96,9 % |

Feature-Dateien mit 100 % (nicht in der Tabelle): `VisualStudioCodeIdePlugin.cs`, `VisualStudioIdePlugin.cs`, `VisualStudioCodeAvailability.cs`.
`IIdePlugin.cs` enthält als reines Interface keine ausführbaren/coverable Zeilen und taucht daher nicht im Cobertura-Report auf (nicht mit 0 % Abdeckung zu verwechseln).

Gesamtprojekt: 314 Quelldateien mit messbaren Zeilen, davon 174 unter 80 % Abdeckung und 137 bei 0 % Abdeckung — bei dieser Menge (über dem im Auftrag genannten Schwellenwert für Einzellisten) erfolgt hier nur die kategorische Zusammenfassung statt einer vollständigen Auflistung. Die 0 %-Dateien liegen fast ausschließlich außerhalb des IDE-Plugin/Split-Button-Feature-Bereichs:

- **WPF Views/Controls (Code-Behind, `*.xaml.cs`) und generierte `*.g.cs`/`*.Designer.cs`**: überwiegend 0 %, da über FlaUI-E2E nur teilweise oder gar nicht instrumentiert erfasst wird (u. a. `MainWindow.xaml.cs`, `FileExplorerView.xaml.cs`, `ProjectDetailView.xaml.cs`, diverse Dialoge).
- **EF-Core-Migrationen (`src/Softwareschmiede/Migrations/*.cs`, `*.Designer.cs`, `SoftwareschmiededDbContextModelSnapshot.cs`)**: durchgehend 0 %, generierter/deklarativer Code, üblicherweise nicht separat getestet.
- **WPF-Dialog-/UI-Infrastruktur-Services** (`WpfAudioService.cs`, `WpfDialogService.cs`, `WpfBannerService.cs`, `WpfUpdateProgressDialogService.cs`, `WpfApplicationShutdownService.cs`, `PluginSelectionDialogService.cs`): 0 %, erfordern echte WPF-Dialoge/OS-Interaktion.
- **Prozess-/OS-Infrastruktur** (`CliRunner.cs`, `CliSessionService.cs`, `SystemProzessStarter.cs`, `SystemShutdownService.cs`): 0 %, OS-nahe Legacy-/Infrastrukturklassen außerhalb dieses Features.
- **Kleine Value-Objects/Interfaces/Events** (`WorkspaceNodeRow.cs`, `PluginKonfiguration.cs`, `AgentInfo.cs`, `IGitPlugin.cs`, `KiAufgabenAbschlussEreignis.cs`, `KiAufgabenBenachrichtigungsHub.cs`, `BenutzerkontextService.cs`): 0 %, meist reine Datenhalter/Schnittstellen ohne dedizierte Tests.

## Fehlende Tests

Quelle: `Coverage-Daten`

- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs` — 0 % Abdeckung (kein dediziertes Testfile gefunden; wird nur indirekt über WPF/E2E-UI-Interaktion erreicht)
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml` — 0 % Abdeckung (XAML-Markup, gleiche Ursache wie zugehöriges Code-Behind)
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml.cs` — 0 % Abdeckung (in Iteration 3 aus `SettingsView.xaml`-Duplikat extrahiertes UserControl; kein dediziertes Code-Behind-Testfile, nur indirekt über SettingsView/E2E erreichbar)
- `src/Softwareschmiede.App/Controls/PluginDetailPanel.xaml` — 0 % Abdeckung (XAML-Markup, gleiche Ursache wie zugehöriges Code-Behind)
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs` — 7,6 % Abdeckung (5/66 Zeilen; kein dediziertes Code-Behind-Testfile, nur indirekt über SettingsViewModelTests/E2E abgedeckt)
