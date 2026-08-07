# Testergebnis - Anzeige offener Todos im Menue

Status: Feature-relevante Tests bestanden; voller WPF/E2E-Gesamtlauf nicht gruen wegen bestehender, nicht featurebezogener UIAutomation/E2E-Fehler.

Iteration: Fortsetzung aus `continue.md` am 2026-08-07

Hinweis zur Bewertung: In dieser Fortsetzung wurden ausschliesslich die beiden offenen Punkte aus `continue.md` erneut geprueft. Es wurden keine Codeaenderungen vorgenommen, weil die reproduzierten Fehler in bestehenden E2E-Sammlern ausserhalb der Todo-Menue-Funktion liegen und kein sinnvoller, eng begrenzter Todo-Menue-Fix erkennbar war. Die Todo-Menue-Aenderung betrifft Todo-Count-Service, ActiveTasks-Anzeige, Dialog-Command und den read-only Open-Todos-Dialog; diese Pfade waren bereits durch fokussierte Tests abgedeckt und bestanden.

## Ausgefuehrte Kommandos

| Kommando | Ergebnis |
|----------|----------|
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName=Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests" --logger "trx;LogFileName=run-general-continue.trx"` | Fehlgeschlagen nach ca. 25s: `DefaultKiPlugin`/Combobox-Element wurde nicht innerhalb von 20s gefunden |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName=Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests" --logger "trx;LogFileName=run-conpty-continue.trx"` | Fehlgeschlagen nach ca. 104s: `CliStoppen` im AutoStart-CLI-Szenario wurde nicht innerhalb von 15s gefunden |

## Bestandene Tests

- Vorherige Validierung bleibt unveraendert: Build der gesamten Solution erfolgreich, feature-relevante Unit-Tests 44/44 bestanden, Integrationstests 75/75 bestanden.
- In dieser Fortsetzung wurden die beiden offenen E2E-Sammler gezielt erneut ausgefuehrt; beide blieben nicht gruen.

## Fehlgeschlagene Tests

- `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` - aktueller Repro: `System.TimeoutException: Element wurde nicht innerhalb von 20s gefunden.` bei `SelectComboBoxItemByClick` fuer `DefaultKiPlugin` in `E2E_SettingsKiPluginPersistence.cs`.
- `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` - aktueller Repro: `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.` beim Warten auf `CliStoppen` in `AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E`.

Vorherige TRX-Dateien zeigten zusaetzlich fuer `RunGeneralTests` ein wechselndes Fehlerbild beim Schliessen des Hilfe-Dialogs: `RPC_E_SERVERFAULT` bzw. Timeout in `WaitUntilGone`.

## Bewertung

Keine featurebezogenen Testfehler.

Die beiden fehlgeschlagenen E2E-Sammler betreffen bestehende Einstellungen-/CLI-/ConPTY-UIAutomation-Szenarien und nicht die neue Anzeige offener Todos im Menue, den Todo-Count-Service oder den Open-Todos-Dialog. Der erneute Lauf hat wechselnde Fehlerpunkte innerhalb der bestehenden WPF-E2E-Infrastruktur gezeigt. Eine Behebung waere nur ueber breitere E2E-Testinfrastruktur- oder CLI-AutoStart-Arbeiten sinnvoll und damit ausserhalb des Todo-Menue-Feature-Scopes. Die offenen Punkte bleiben daher in `continue.md` dokumentiert.
