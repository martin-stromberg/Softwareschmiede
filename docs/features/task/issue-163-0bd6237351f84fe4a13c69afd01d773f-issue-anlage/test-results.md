# Testresultate

Status: Keine Fehler in den relevanten Nacharbeits-Tests

## Fokussierte Tests

Kommando:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~GitPluginBaseTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests"
```

Ergebnis: erfolgreich.

- Gesamt: 212
- Bestanden: 212
- Fehlgeschlagen: 0
- Uebersprungen: 0

## Nicht-E2E-Tests

Kommando:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName!~Softwareschmiede.Tests.E2E" --logger "trx;LogFileName=non-e2e.trx" --results-directory TestResults
```

Ergebnis: erfolgreich.

- Gesamt: 1060
- Bestanden: 1059
- Fehlgeschlagen: 0
- Uebersprungen: 1 (`ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException`, erwarteter OS-Skip auf Windows)

## Vollstaendiger Testlauf inklusive E2E

Kommando:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --logger "trx;LogFileName=issue-create-full.trx" --results-directory TestResults
```

Ergebnis: nicht erfolgreich wegen eines E2E-Timeouts ausserhalb der geaenderten Issue-Anlage-Nachweise.

- Gesamt: 1090
- Ausgefuehrt: 1089
- Bestanden: 1088
- Fehlgeschlagen: 1
- Nicht ausgefuehrt: 1

Fehlgeschlagen:

- `Softwareschmiede.Tests.E2E.E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E`
- Fehler: `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`
- Reproduktionslauf nur fuer diesen Test: ebenfalls fehlgeschlagen mit derselben Meldung.

Bewertung: Die relevanten Unit-/Provider-/Dialog-/TaskDetail-Nachweise fuer die offenen Punkte aus `continue.md` sind gruen. Der verbleibende E2E-Timeout betrifft den allgemeinen Aufgabe-Start-E2E und wurde nicht durch die Test-/Nachweisergaenzungen in dieser Nacharbeit veraendert.
