# Test Results: Anzeige der CLI

## Unit-Tests

- `TaskDetailViewModelTests.PluginAendernCommand_ZeigtNeuenAktivenCliName_WennVorherProjektDefaultLief` – PASS
- `TaskDetailViewModelTests.CliNeustartenCommand_NachPluginWechsel_VerwendetGeaendertesPlugin_NichtProjektDefault` – PASS
- Gesamter `TaskDetailViewModelTests`-Testbestand (175 Tests) – PASS
- Vollständige Regular Lane (`Category!=OsInterface`) – 1528 bestanden, 1 übersprungen, 0 fehlgeschlagen

## E2E-Tests

Ausgeführt (Sandbox, interaktive Windows-Sitzung verfügbar):

```powershell
$env:SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS='0'; dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~RunConPtyTests"
```

Ergebnis: 1 bestanden, 0 fehlgeschlagen, 0 übersprungen (Dauer: 5 m 30 s).

Der fokussierte E2E-Test `PluginAuswahlAbbrechenOkUndWechsel_E2E` deckt sowohl die Fußzeile (`AktiverCliName`) als auch die Seitenleiste/den Programmmenü-Button (`GetActiveTaskKiPluginName`) ab:

- Vor dem Wechsel wird "KI Simulator" in Fußzeile und Seitenleiste angezeigt.
- Nach dem Wechsel auf `Softwareschmiede.ClaudeCli` wird "Claude CLI" in Fußzeile und Seitenleiste angezeigt.

## Zusammenfassung

Die Anzeige des aktiven CLI-Namens in Fußzeile und Seitenleiste wird nach einem Plugin-Wechsel korrekt aktualisiert; Regressionsschutz durch Unit- und E2E-Abdeckung gegeben.
