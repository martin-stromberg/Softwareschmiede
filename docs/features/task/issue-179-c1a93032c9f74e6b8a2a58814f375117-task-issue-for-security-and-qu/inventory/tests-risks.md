# Tests und Risiken

## Vorhandene Tests

### GitHub-Plugin

**Datei:** `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`

Relevante vorhandene Abdeckung:

| Testbereich | Beispiele | Aussage |
|-------------|-----------|---------|
| Issue-Lesen | `GetIssuesAsync_ShouldReturnParsedIssues_WhenCliSucceeds` | JSON wird in `Issue` gemappt. |
| Issue-Lesen Fehler | `GetIssuesAsync_ShouldReturnEmptyList_WhenCliFails` | Providerfehler fuehren zu leerer Liste. |
| Issue-Anlage | `CreateIssueAsync_ShouldReturnIssue_WhenCliSucceeds` | `gh issue create` wird aufgerufen und Rueckgabe gemappt. |
| Validierung | fehlende Repository-ID, leerer Titel | Kein CLI-Aufruf bei ungueltiger Eingabe. |
| Fehlerbehandlung | CLI-Fehler, Cancellation | Fehler- und Abbruchsemantik ist abgedeckt. |

Fehlende Alert-Abdeckung:

- GitHub-Code-Scanning-Alert-API-Aufruf
- Parsing von Alert-JSON
- leere Liste bei fehlender Berechtigung oder nicht aktiviertem Code Scanning
- Sanitizing von GitHub-API-Fehlern
- Repository-ID-Normalisierung fuer Alert-Aufrufe

### ProjectDetailViewModel

**Datei:** `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`

Relevante vorhandene Abdeckung:

| Test | Aussage |
|------|---------|
| `LadenIssuesAsync_LoadsIssuesWhenRepositorySupportsIssues` | Offene Issues werden geladen. |
| `LadenIssuesAsync_ReturnsEmptyListWhenPluginDoesNotSupport` | Keine Vorschlaege ohne SCM-Plugin. |
| `LadenIssuesAsync_HandlesExceptionGracefully` | Ladefehler bleiben UI-vertraeglich. |
| `LadenIssuesAsync_FiltersOutAlreadyConvertedIssues` | Bereits konvertierte Issue-Nummern werden ausgeblendet. |
| `AufgabeAusIssueErstellenAsync_CreatesAufgabeAndRemovesFromVorschlaege` | Issue-Klick erzeugt Aufgabe und entfernt Vorschlag. |
| `AufgabeAusIssueErstellenAsync_UserCancellation_DoesNothing` | Abbruch erzeugt keine Aufgabe. |

Fehlende Alert-Abdeckung:

- gemeinsame Anzeige von Issues und Alerts
- Anzeige eines Alert-Typs ohne Issue-Nummer
- Alert-Auswahl erstellt zuerst externes GitHub-Issue
- lokale Aufgabe referenziert das erzeugte GitHub-Issue
- Fehler bei externer Issue-Anlage verhindert lokale Aufgabe
- bereits konvertierte Alerts werden ueber stabile Alert-Referenz ausgefiltert

### AufgabeService

**Dateien:**

- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

Es gibt umfangreiche Aufgabe-Service-Tests. Fuer diese Anforderung fehlen Tests fuer eine moegliche neue Methode wie `CreateFromAlertAsync()` oder fuer persistierte Alert-Referenzen.

## Risiken

| Risiko | Auswirkung | Empfehlung |
|--------|------------|------------|
| Alerts als `Issue` modelliert | Fachliche Regel wird verletzt; spaetere Alert-Typen werden schwer integrierbar. | Eigenen Alert-/Anforderungstyp einfuehren. |
| Kein Duplikatschutz | Derselbe Alert kann mehrfach Aufgaben und GitHub-Issues erzeugen. | Persistente Alert-Quellkennung speichern und beim Laden filtern. |
| GitHub-Berechtigungen unklar | Alert-Laden kann in realen Repos leer oder fehlerhaft sein. | Fehler unterscheidbar loggen und Token-Hinweise aktualisieren. |
| Lokale Aufgabe vor externer Issue-Anlage | Aufgabe existiert ohne gefordertes GitHub-Issue. | Externes Issue zuerst anlegen; lokale Persistenz danach. |
| GitHub-only Logik im allgemeinen UI-Code | Bitbucket/Jira koennen unbeabsichtigt beeinflusst werden. | Alert-Provider optional und nur GitHub implementieren. |
| Alert-Status ungeklart | Erstellte Alerts bleiben ggf. weiterhin in GitHub offen und in der Liste sichtbar. | In Planung als offene fachliche Frage behandeln oder Duplikatfilter als Minimalverhalten definieren. |

## Empfohlene Testschwerpunkte fuer die Umsetzung

- Unit-Tests fuer neue Contract-ValueObjects und Alert-Parser.
- GitHubPlugin-Tests fuer Code-Scanning-Alerts via `gh api`.
- ProjectDetailViewModel-Tests fuer gemischte Anforderungen und Alert-Konvertierung.
- AufgabeService-Tests fuer Speicherung von GitHub-Issue-Referenz plus Alert-Quellreferenz.
- Fehlerpfad: `CreateIssueAsync()` schlaegt fehl, es wird keine lokale Aufgabe angelegt.

