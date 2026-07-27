# Umsetzungsplan: GitHub Security and quality alerts

## Zielbild

GitHub-Code-Scanning-Alerts werden als eigene SCM-Anforderungsart neben normalen Issues geladen und in der Projektdetailansicht unter "Offene Anforderungen" angezeigt. Wird ein Alert ausgewaehlt, erstellt die Anwendung zuerst automatisch ein GitHub-Issue aus den Alert-Daten und danach eine lokale Aufgabe, deren `IssueReferenz` auf das neu angelegte GitHub-Issue zeigt. Die Herkunft aus dem Alert wird separat persistiert, damit derselbe Alert nicht mehrfach angeboten oder konvertiert wird.

## Fachliche Entscheidungen

- Initial werden ausschliesslich GitHub-Code-Scanning-Alerts gelesen.
- Dependabot- und Secret-Scanning-Alerts werden in dieser Umsetzung nicht angebunden.
- Das automatisch angelegte GitHub-Issue bekommt einen deterministischen Titel nach dem Schema `Code scanning alert: <Rule/Alert-Titel>` und einen Body mit Alert-Typ, Severity, Tool, Rule, betroffenem Ort, Alert-URL und Kurzbeschreibung.
- Ein Alert gilt lokal als konvertiert, sobald eine Aufgabe mit passender persistierter Alert-Referenz existiert. Der Alert selbst wird in GitHub nicht geschlossen oder anderweitig veraendert.
- Bitbucket/Jira erhalten keine Alert-Implementierung und muessen durch Default- oder optionale Interfaces unveraendert weiter funktionieren.

## Umsetzungsschritte

### 1. Contract- und ValueObject-Erweiterung

1. In `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/` neue ValueObjects anlegen:
   - `ScmRequirement` als gemeinsamer UI-/Workflow-Typ fuer offene Anforderungen.
   - `ScmRequirementKind` mit mindestens `Issue` und `Alert`.
   - `ScmAlert` fuer Provider-Alerts mit stabiler Quellkennung.
   - `ScmAlertType` mit initial `CodeScanning`.
2. `ScmAlert` enthaelt mindestens:
   - `AlertId` oder `AlertNumber`
   - `SourceKey` als stabile, providerweit eindeutige Kennung, z. B. `github:code-scanning:<repository>:<alert-number>`
   - `Title`, `Description`, `AlertUrl`
   - `Severity`, `State`
   - `ToolName`, `RuleId`, `RuleName`
   - optional `FilePath`, `StartLine`
3. Ein optionales Interface ergaenzen, z. B. `IScmAlertProvider`:
   - `Task<IEnumerable<ScmAlert>> GetAlertsAsync(string repositoryId, CancellationToken ct = default)`
4. `GitPluginBase<TPlugin>` implementiert das Interface virtuell mit leerer Liste, damit vorhandene Plugins nicht brechen.
5. Normale Issues bleiben `Issue`; es erfolgt kein Alert-Mapping auf `Issue`.

### 2. GitHub-Code-Scanning-Alerts lesen

1. `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` erweitert `GetAlertsAsync()`.
2. Repository-IDs werden mit der vorhandenen Normalisierung verarbeitet.
3. Der GitHub-CLI-Aufruf nutzt:
   - `gh api repos/<owner>/<repo>/code-scanning/alerts`
   - optional mit Query `?state=open&per_page=100`
4. JSON-Parsing bildet mindestens folgende GitHub-Felder ab:
   - `number`, `state`, `html_url`
   - `rule.id`, `rule.name`, `rule.description`, `rule.security_severity_level`, `rule.severity`
   - `tool.name`
   - `most_recent_instance.location.path`
   - `most_recent_instance.location.start_line`
   - `most_recent_instance.message.text`
5. Fehlerverhalten:
   - leere Repository-ID ergibt leere Liste.
   - 404 oder nicht aktiviertes Code Scanning ergibt leere Liste mit Logeintrag.
   - 403/fehlende Rechte ergibt leere Liste mit sanitiztem Logeintrag und ohne UI-Absturz.
   - Cancellation wird weitergereicht.
6. Token-Beschreibung im GitHub-Plugin aktualisieren: Neben `repo` und `read:org` Hinweis auf Berechtigungen zum Lesen von Code-Scanning-Alerts aufnehmen.

### 3. Alert-Herkunft persistieren

1. Neue Entity `AlertReferenz` unter `src/Softwareschmiede/Domain/Entities/` anlegen.
2. Felder:
   - `Id`
   - `AufgabeId`
   - `Provider`
   - `RepositoryId`
   - `AlertType`
   - `SourceKey`
   - `AlertUrl`
   - `Titel`
   - `Severity`
   - `State`
   - `RuleId`
   - `ToolName`
3. `Aufgabe` erhaelt eine optionale Navigation `AlertReferenz`.
4. `SoftwareschmiededDbContext` erhaelt `DbSet<AlertReferenz>` und eine 1:1-Beziehung `Aufgabe` -> `AlertReferenz` mit Cascade Delete.
5. Auf `SourceKey` wird ein eindeutiger Index gelegt, damit Duplikate auch bei paralleler Konvertierung verhindert werden.
6. EF-Core-Migration anlegen und Snapshot aktualisieren.

### 4. AufgabeService fuer Alert-Konvertierung erweitern

1. Neue Methode in `AufgabeService` ergaenzen, z. B.:
   - `CreateFromAlertAsync(Guid projektId, ScmAlert alert, Issue createdIssue, Guid? gitRepositoryId, CancellationToken ct = default)`
2. Die Methode erstellt:
   - lokale Aufgabe mit Alert-Titel und Alert-Beschreibung
   - `IssueReferenz` aus dem zuvor erstellten GitHub-Issue
   - `AlertReferenz` aus dem Quell-Alert
3. Vor dem Speichern prueft die Methode, ob `AlertReferenz.SourceKey` bereits vorhanden ist. Bei vorhandenem Eintrag wird keine zweite Aufgabe erzeugt.
4. Die Methode darf kein externes GitHub-Issue erstellen. Diese Verantwortung bleibt im ViewModel/Workflow, damit die Reihenfolge klar bleibt: externes Issue zuerst, lokale Aufgabe danach.

### 5. Gemeinsame Anzeige offener Anforderungen

1. `ProjectDetailViewModel` von issue-spezifischer Collection auf gemeinsame Anforderungsliste umstellen:
   - neue Collection, z. B. `ObservableCollection<ScmRequirement> OffeneAnforderungen`
   - bestehende `IssueVorschlaege` nur entfernen, wenn keine Tests oder Bindings mehr darauf angewiesen sind; andernfalls als kompatible Weiterleitung waehrend der Umstellung belassen.
2. `LadenIssuesAsync()` in eine allgemeiner benannte Lademethode ueberfuehren, z. B. `LadenOffeneAnforderungenAsync()`.
3. Ladeablauf:
   - normales `gitPlugin.GetIssuesAsync()` laden und in `ScmRequirementKind.Issue` mappen.
   - falls Plugin `IScmAlertProvider` unterstuetzt, `GetAlertsAsync()` laden und in `ScmRequirementKind.Alert` mappen.
4. Filter:
   - Issues weiter ueber vorhandene `IssueReferenz.IssueNummer` ausblenden.
   - Alerts ueber persistierte `AlertReferenz.SourceKey` ausblenden.
5. `KannIssuesLaden` fachlich in `KannAnforderungenLaden` umbenennen oder semantisch erweitern. XAML-Bindings entsprechend anpassen.

### 6. Alert-Auswahl und GitHub-Issue-Anlage

1. Command auf gemeinsamen Typ umstellen:
   - z. B. `AsyncRelayCommand<ScmRequirement> AufgabeAusAnforderungErstellenCommand`
2. Issue-Pfad:
   - bestehendes Verhalten fuer normale Issues beibehalten.
3. Alert-Pfad:
   - bestaetigenden Dialog mit Alert-Titel anzeigen.
   - sicherstellen, dass das Plugin `IIssueCreateProvider` unterstuetzt und `CanCreateIssueAsync()` erfolgreich ist.
   - aus Alert-Daten `IssueCreateRequest` bauen.
   - `CreateIssueAsync()` aufrufen.
   - bei Fehler keine lokale Aufgabe erstellen und `FehlerMeldung` setzen.
   - bei Erfolg `AufgabeService.CreateFromAlertAsync()` aufrufen.
   - erstellte Aufgabe der Aufgabenliste hinzufuegen, Anforderung aus der Vorschlagsliste entfernen.
4. Keine Dialogbearbeitung fuer den Issue-Text beim Alert-Pfad; die Anforderung verlangt automatische Anlage.

### 7. XAML-Anpassung

1. Abschnittstitel "Offene Anforderungen" beibehalten.
2. `ItemsSource` auf die gemeinsame Anforderungsliste umstellen.
3. Anzeige fuer normale Issues:
   - Nummer `#<Nummer>`
   - Titel
   - Typtext "GitHub Issue" oder bisher "Offene Anforderung"
4. Anzeige fuer Alerts:
   - kein Issue-Nummernfeld
   - Badge/Text wie "GitHub Code Scanning Alert"
   - Titel
   - Severity/Rule/Dateipfad als zweite Zeile, sofern vorhanden
5. Double-Click-Handler auf den neuen Command umstellen.

### 8. Tests

1. Contract-/Domain-Tests:
   - `ScmAlert.SourceKey` oder Factory/Mapping erzeugt stabile Kennungen.
   - `AlertReferenz` wird mit Aufgabe kaskadierend gespeichert/geloescht.
2. GitHub-Plugin-Tests in `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`:
   - erfolgreicher `gh api repos/<repo>/code-scanning/alerts` Aufruf wird korrekt gemappt.
   - Repository-URL wird normalisiert.
   - 404/Code-Scanning nicht aktiv liefert leere Liste.
   - 403 wird sanitizt geloggt und liefert leere Liste.
   - Cancellation wird propagiert.
3. AufgabeService-Tests:
   - `CreateFromAlertAsync()` speichert Aufgabe, `IssueReferenz` und `AlertReferenz`.
   - doppelte `SourceKey`-Konvertierung erzeugt keine zweite Aufgabe bzw. bricht kontrolliert ab.
4. ProjectDetailViewModel-Tests:
   - Issues und Alerts werden gemeinsam geladen.
   - bereits konvertierte Issues und Alerts werden gefiltert.
   - Alert-Auswahl erstellt zuerst externes GitHub-Issue und danach lokale Aufgabe.
   - Fehlschlag bei `CreateIssueAsync()` verhindert lokale Aufgabe.
   - Benutzerabbruch erzeugt weder GitHub-Issue noch lokale Aufgabe.
5. XAML-/Binding-nahe Tests nur erweitern, falls vorhandene View-Tests den Abschnitt "Offene Anforderungen" direkt pruefen.

## Akzeptanzkriterien

- GitHub-Code-Scanning-Alerts erscheinen neben normalen Issues in "Offene Anforderungen".
- Alerts sind im UI als Alerts erkennbar und werden nicht als normale Issues dargestellt.
- Beim Auswaehlen eines Alerts wird automatisch ein GitHub-Issue angelegt.
- Die lokale Aufgabe referenziert das neu angelegte GitHub-Issue.
- Die lokale Aufgabe speichert zusaetzlich die Alert-Herkunft.
- Derselbe Alert wird nach erfolgreicher Konvertierung nicht erneut angeboten.
- Bitbucket/Jira funktionieren unveraendert und muessen keine Alerts liefern.
- Fehler beim Alert-Laden oder bei fehlenden GitHub-Rechten lassen die Projektdetailansicht stabil.

## Offene Punkte

Keine.
