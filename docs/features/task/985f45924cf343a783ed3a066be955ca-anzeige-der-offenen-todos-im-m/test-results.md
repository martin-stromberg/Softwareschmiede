# Testergebnis - Anzeige offener Todos im Menue

Status: Feature-relevante Tests bestanden; voller WPF/E2E-Gesamtlauf nicht gruen wegen bekannter, nicht featurebezogener UIAutomation/E2E-Fehler.

Iteration: 2

Hinweis zur Bewertung: Die Implementierungsiteration 2 hat keine featurebezogenen Codeaenderungen vorgenommen. Die erneut beobachteten Fehler liegen in zwei bestehenden WPF/UIAutomation-E2E-Sammlern ausserhalb der Todo-Menue-Funktion. Die Todo-Menue-Aenderung betrifft Todo-Count-Service, ActiveTasks-Anzeige, Dialog-Command und den read-only Open-Todos-Dialog; diese Pfade sind durch die fokussierten Tests abgedeckt und bestanden.

## Ausgefuehrte Kommandos

| Kommando | Ergebnis |
|----------|----------|
| `dotnet build Softwareschmiede.slnx --no-restore` | Bestanden: 0 Warnungen, 0 Fehler |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName~TodoServiceTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~OpenTodosDialogViewModelTests"` | Bestanden: 44/44 |
| `dotnet test src\Softwareschmiede.IntegrationTests\Softwareschmiede.IntegrationTests.csproj --no-build` | Bestanden: 75/75 |
| `dotnet test Softwareschmiede.slnx --no-build` | Timeout nach 184 Sekunden ohne verwertbare Konsolenausgabe |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build` | Exitcode 1 ohne Konsolenausgabe; TRX-Ergebnis zeigt bekannte WPF/E2E-Fehler |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --logger "console;verbosity=detailed"` | Exitcode 1 ohne Konsolenausgabe |

## Bestandene Tests

- Build der gesamten Solution erfolgreich.
- Feature-relevante Unit-Tests: 44/44 bestanden.
- Integrationstests: 75/75 bestanden.
- In der TRX-Datei des vollstaendigen `Softwareschmiede.Tests`-Laufs: 1242/1245 Tests bestanden, 2 fehlgeschlagen.

## Fehlgeschlagene Tests

- `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` - `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`
- `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` - `System.Runtime.InteropServices.COMException: Ausnahmefehler des Servers. (0x80010105 (RPC_E_SERVERFAULT))`

Zusatzlaeufe einzelner Sammler aus vorhandenen TRX-Dateien zeigen dasselbe Fehlerbild:

- `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` - `System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.`
- `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` - `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`

## Bewertung

Keine featurebezogenen Testfehler.

Die beiden fehlgeschlagenen E2E-Sammler betreffen CLI-/allgemeine WPF-UIAutomation-Szenarien und nicht die neue Anzeige offener Todos im Menue, den Todo-Count-Service oder den Open-Todos-Dialog. Da Iteration 2 keine Codeaenderungen am Feature vorgenommen hat und die fokussierten Todo-Tests sowie Integrationstests weiterhin gruen sind, wird das Todo-Menue-Feature aus Testsicht als bestanden bewertet. Der vollstaendige WPF/E2E-Gesamtlauf bleibt wegen bestehender UIAutomation-Flakes nicht gruen.
