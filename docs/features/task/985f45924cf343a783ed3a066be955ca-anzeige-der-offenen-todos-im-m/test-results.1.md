# Testergebnisse - Anzeige offener Todos im Menue

Ausgefuehrt am: 2026-08-07

Status: Fehler vorhanden

Hinweis zur Ausfuehrung: Der Lifecycle-Schritt 8b wurde in dieser Umgebung direkt ausgefuehrt, weil kein separater Unteragent gestartet wurde.

## Zusammenfassung

| Kommando | Ergebnis |
|----------|----------|
| `dotnet build Softwareschmiede.slnx --no-restore` | Bestanden: 0 Warnungen, 0 Fehler |
| `dotnet test Softwareschmiede.slnx --no-restore` | Fehlgeschlagen: Exitcode 1 nach ca. 173 s, keine Konsolenausgabe |
| `dotnet test src\Softwareschmiede.IntegrationTests\Softwareschmiede.IntegrationTests.csproj --no-restore` | Bestanden: 75/75 |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-restore` | Fehlgeschlagen: Exitcode 1 nach ca. 174 s, keine Konsolenausgabe |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-restore --logger trx --results-directory TestResults --diag TestResults\Softwareschmiede.Tests.diag.log` | Fehlgeschlagen: 1245 ausgefuehrt, 1242 bestanden, 1 uebersprungen, 2 fehlgeschlagen |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-restore --filter "FullyQualifiedName~TodoServiceTests\|FullyQualifiedName~MainWindowViewModelTests\|FullyQualifiedName~OpenTodosDialogViewModel"` | Bestanden: 44/44 |

## Fehlgeschlagene Tests

1. `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests`
   - Fehlermeldung: `System.TimeoutException : Element wurde nicht innerhalb von 15s gefunden.`
   - Stack-Auszug:
     - `Softwareschmiede.Tests.E2E.WpfTestBase.WaitForElement(...)` in `src\Softwareschmiede.Tests\E2E\WpfTestBase.cs:310`
     - `Softwareschmiede.Tests.E2E.End2EndTest.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E(...)` in `src\Softwareschmiede.Tests\E2E\E2E_AutoStartCli.cs:43`
     - `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests()` in `src\Softwareschmiede.Tests\E2E\MainTest.cs:54`

2. `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests`
   - Fehlermeldung: `System.Runtime.InteropServices.COMException : Ausnahmefehler des Servers. (0x80010105 (RPC_E_SERVERFAULT))`
   - Stack-Auszug:
     - `Interop.UIAutomationClient.IUIAutomationElement.FindFirst(...)`
     - `FlaUI.UIA3.UIA3FrameworkAutomationElement.FindFirst(...)`
     - `Softwareschmiede.Tests.E2E.WpfTestBase.WaitUntilGone(...)` in `src\Softwareschmiede.Tests\E2E\WpfTestBase.cs:411`
     - `Softwareschmiede.Tests.E2E.End2EndTest.CommandLineParameters_TextBoxSpeichertWertUndHilfeDialogFunktioniert_E2E(...)` in `src\Softwareschmiede.Tests\E2E\E2E_SettingsCommandLineParameters.cs:51`
     - `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests()` in `src\Softwareschmiede.Tests\E2E\MainTest.cs:27`

## Bewertung

- Build ist erfolgreich.
- Die feature-relevanten Tests fuer Todo-Service, MainWindow-Mapping/Command und OpenTodosDialogViewModel sind erfolgreich.
- Die Integrationstests sind erfolgreich.
- Der komplette Unit-/E2E-Testlauf ist nicht gruen, weil zwei bestehende WPF-E2E-Sammler in UIAutomation scheitern.

## Artefakte

- TRX: `TestResults\Martin_DESKTOP-CM8OBSG_2026-08-07_08_54_21_net10.0.trx`
- Diagnose: `TestResults\Softwareschmiede.Tests.diag.log`
- Diagnose Host: `TestResults\Softwareschmiede.Tests.diag.host.26-08-07_08-54-20_59913_5.log`
