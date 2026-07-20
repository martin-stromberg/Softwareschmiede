# Plan-Review: Issue-Anlage aus der Aufgabendetailansicht

Status: **Vollstaendig umgesetzt**

## Ergebnis

Die Nacharbeit aus `continue.md` ist umgesetzt. Die zuvor offenen Test- und Nachweislaecken zu Cancellation, Providerfehlern, Dialog-/TaskDetail-Fehlerfaellen und den elf Abnahmekriterien sind geschlossen.

## Abgedeckte Nacharbeiten

- Contract-/Base- und Provider-Tests wurden um Cancellation-Weitergabe, Pflichtfeld-/Konfigurationsvalidierung, Providerfehler und vollstaendige Erfolgsantworten erweitert.
- `IssueCreateDialogViewModelTests` decken Provider-Create-Fehler, Exceptions, Cancellation beim Template-Laden, bei KI und Submit sowie doppelte Submit-Ausfuehrungen ab.
- `TaskDetailViewModelTests` decken Provider ohne Create-Capability, Capability-Providerfehler, lokale Persistenzfehler mit externer Issue-URL und doppelte laufende Create-Aktionen ab.
- `acceptance-criteria.md` fuehrt alle elf Abnahmekriterien aus `requirement.md` einzeln auf Tests oder nachvollziehbare UI-/Provider-Verifikation zurueck.

## Offene Aufgaben

Keine.

## Verifikation

Ausgefuehrt:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~GitPluginBaseTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests"
```

Ergebnis: **212 erfolgreich, 0 fehlgeschlagen, 0 uebersprungen**.

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName!~Softwareschmiede.Tests.E2E" --logger "trx;LogFileName=non-e2e.trx" --results-directory TestResults
```

Ergebnis: **1059 erfolgreich, 0 fehlgeschlagen, 1 uebersprungen**.

Der vollstaendige Lauf inklusive E2E wurde ebenfalls ausgefuehrt. Er scheitert ausschliesslich an `Softwareschmiede.Tests.E2E.E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E` mit einem reproduzierbaren UI-Timeout beim Warten auf ein Element; dieser Test liegt ausserhalb der angefassten Issue-Anlage-Nachweise.
