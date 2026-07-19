# Tests und Testluecken

## Vorhandene relevante Tests

### `AufgabeServiceTests`

**Datei:** `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

| Test | Relevanz |
|------|----------|
| `CreateFromIssueAsync_ShouldPersistAufgabeWithIssueReferenz_WhenIssueGiven` | Belegt, dass Aufgaben aus Issues inklusive `IssueReferenz.IssueNummer`, Titel und Milestone persistiert werden. |
| `StartenAsync_ShouldSetStatusInBearbeitungAndPersistBranchInfo_WhenAufgabeExists` | Belegt, dass `BranchName` beim Start persistiert wird. |

### `EntwicklungsprozessServiceTests`

**Datei:** `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`

| Test | Relevanz |
|------|----------|
| `ProzessStartenAsync_ShouldPersistBranchAndKlonPfad_WhenAufgabeExists` | Belegt, dass Startprozess Branch und Klonpfad an der Aufgabe speichert. |
| `ProzessStartenAsync_ShouldPersistIssueBasedBranch_WhenAufgabeWasCreatedFromIssue` | Belegt, dass Issue-Aufgaben Branch-Namen mit `task/issue-<nr>-...` erhalten. |

### `TaskStartupServiceIntegrationTests`

**Datei:** `src/Softwareschmiede.Tests/ServiceIntegration/TaskStartupServiceIntegrationTests.cs`

| Test | Relevanz |
|------|----------|
| `StartenAsync_Setzt_Status_AufGestartet` | Belegt Status- und Branch-Persistenz in einem Service-Integrationstest. |

### `GitOrchestrationServiceTests`

**Datei:** `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`

| Test | Zeile | Relevanz |
|------|-------|----------|
| `PullRequestErstellenAsync_ShouldCreatePrAndLogEntry_WhenBranchExists` | 383-424 | Basisverhalten fuer PR-Erstellung ohne Issue. |
| `PullRequestErstellenAsync_ShouldAppendClosingDirective_WhenAufgabeHasIssueReference` | 457-500 | Ergaenzt `Closes #123` bei Aufgabe mit Issue-Referenz. |
| `PullRequestErstellenAsync_ShouldNotDuplicateClosingDirective_WhenBodyAlreadyContainsDirective` | 502-544 | Verhindert Duplikate fuer dieselbe Issue. |
| `PullRequestErstellenAsync_ShouldUseOnlyClosingDirective_WhenBodyIsWhitespaceAndIssueExists` | 546-589 | Whitespace-Body wird durch Closing-Direktive ersetzt. |
| `PullRequestErstellenAsync_ShouldAppendClosingDirectiveForCurrentIssue_WhenBodyContainsDirectiveForAnotherIssue` | 591-636 | Andere Direktiven bleiben erhalten; aktuelle Issue wird ergaenzt. |
| `PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenMultipleActiveRepositoriesExist` | 638-667 | Mehrdeutige Repository-Aufloesung bricht kontrolliert ab. |
| `PullRequestErstellenAsync_ShouldResolveRepositoryId_FromSshRepositoryUrl` | 689-725 | Repository-ID-Aufloesung fuer SSH-URLs. |

## Fehlende oder sinnvolle Zusatztests

| Testfall | Status | Empfehlung |
|----------|--------|------------|
| Aufgabe ohne Issue-Referenz behaelt Body unveraendert | Teilweise ueber Basis-PR-Test abgedeckt | Assertion auf unveraenderten Body beibehalten/erweitern. |
| Aufgabe mit `IssueReferenz`, aber `IssueNummer == null` behaelt Body unveraendert | Nicht sichtbar abgedeckt | Neuen Unit-Test in `GitOrchestrationServiceTests` ergaenzen. |
| Aufgabe mit `IssueReferenz.IssueNummer <= 0` behaelt Body unveraendert | Nicht sichtbar abgedeckt | Optional ergaenzen, weil Implementierung `is > 0` nutzt. |
| Bereits vorhandene Direktive mit `owner/repo#<nr>` wird erkannt | Nicht sichtbar abgedeckt | Optional fuer Regex-Absicherung. |
| Bereits vorhandene Direktive in anderer Schreibweise (`resolved #<nr>`, `Closed #<nr>`) wird erkannt | Nicht sichtbar abgedeckt | Optional fuer Regex-Absicherung. |
| Alternativer PR-Pfad in `EntwicklungsprozessService.PullRequestErstellenAsync(...)` | Nicht abgedeckt fuer Issue-Auto-Close | Nutzung pruefen; bei produktiver Nutzung entweder umleiten oder testen. |

## Teststrategie fuer die Umsetzung

Primaerer Fokus sollte auf `GitOrchestrationServiceTests` liegen, weil dort die PR-Body-Normalisierung ohne echte GitHub-CLI getestet werden kann. Integrationstests fuer `AufgabeService` und `EntwicklungsprozessService` sichern bereits den Weg von Issue-Erstellung bis Branch-Persistenz ab.
