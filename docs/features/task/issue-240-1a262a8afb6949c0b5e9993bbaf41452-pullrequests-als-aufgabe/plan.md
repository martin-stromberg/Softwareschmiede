# Umsetzungsplan: Pullrequests als Aufgabe

## Ziel und Leitplanken

Offene Pullrequests aus GitHub und Bitbucket werden in der Projektdetailansicht neben Issues als eindeutig gekennzeichnete Vorschlaege angezeigt. Aus einem Vorschlag entsteht atomar eine Aufgabe mit einer als `ReviewSource` gekennzeichneten `PullRequestReferenz`. Nur diese Rolle aktiviert beim Start den Review-Checkout; andere PR-Referenzen an derselben Aufgabe bleiben Ausgaben der Aufgabe und veraendern den Startmodus nicht. Der bestehende Issue- und Normalaufgabenfluss bleibt unveraendert.

Der Plan verwendet die vorhandenen Typen `PullRequest`, `PullRequestReferenz` und `ScmRequirement`, erweitert sie aber um die fuer Fork-Checkout und stabile Identitaet erforderlichen Daten. Die Desktop-E2E-Pruefung laeuft ohne Netzwerk und beobachtet den realen, separaten App-Prozess ueber dateibasierte Fixtures und Aufrufprotokolle.

## Verbindliche technische Entscheidungen

### Repository-Identitaet und Normalisierung

- `GitRepository.RepositoryName` ist die kanonische API-Repository-ID. `RepositoryUrl` wird nur fuer Clone-, Fetch- und sonstige Git-Operationen verwendet und niemals als PR-Identitaet gespeichert oder verglichen.
- Eine zentrale, providerabhaengige Normalisierung wird in der Contract-/Application-Schicht eingefuehrt und bei Provider-Mapping, Vorschlagsfilter, Create-Pruefung und Persistenz identisch verwendet:
  - GitHub: `owner/repository`, beide Segmente getrimmt, ein abschliessendes `.git` entfernt und invariant kleingeschrieben.
  - Bitbucket Cloud: `workspace/repository-slug` nach denselben Regeln invariant kleingeschrieben.
  - Bitbucket Server/Data Center: `PROJECT_KEY/repository-slug`; Projekt-Key invariant gross, Repository-Slug invariant klein. Host und Basis-URL stammen weiterhin aus der Plugin-Konfiguration, nicht aus `RepositoryId`.
- Andere Formen, insbesondere HTTPS-/SSH-URLs, werden als API-ID abgelehnt und nicht stillschweigend umgedeutet. Die Repository-Verknuepfung muss bereits eine gueltige `RepositoryName` liefern.
- Der fachliche Schluessel bleibt `Provider + normalisierte RepositoryId + PullRequestNumber`. Neue und aktualisierte `PullRequestReferenz`-Datensaetze werden ausschliesslich kanonisch gespeichert. Eine EF-Migration normalisiert bestehende Werte vor Beibehaltung bzw. Neuerstellung des eindeutigen Index; bei durch die Normalisierung sichtbar werdenden Kollisionen bricht die Migration mit einer klaren Diagnose ab, statt Datensaetze zusammenzufuehren.
- Die Doppelzuordnungsabfrage arbeitet direkt ueber alle `PullRequestReferenz`-Datensaetze und nicht ueber die aktuell geladene Projekt-/Aufgabenliste. Damit gelten archivierte Aufgaben und Aufgaben anderer Projekte ebenfalls als bereits zugeordnet. Der eindeutige Datenbankindex bleibt die Konkurrenzsicherung.

### Referenzrolle, Migration und Eindeutigkeit der Review-Quelle

- `PullRequestReferenz` erhaelt die persistierte Enum-Eigenschaft `Rolle` mit expliziten Zahlenwerten `CreatedByTask = 0` und `ReviewSource = 1`. `CreatedByTask` bezeichnet einen PR, den die Aufgabe ueber `PullRequestReferenzService.SaveCreatedAsync` selbst erzeugt hat; `ReviewSource` bezeichnet genau den externen PR, aus dessen Vorschlag die Review-Aufgabe atomar angelegt wurde.
- Die EF-Migration legt `Rolle` nicht nullable mit Default `CreatedByTask` an und setzt saemtlichen Altbestand explizit auf `CreatedByTask`. Das ist migrationsfaehig und fachlich eindeutig, weil vor diesem Feature noch kein Importpfad fuer Review-Aufgaben existiert. Bestehende normale Aufgaben werden dadurch nicht nachtraeglich zu Review-Aufgaben.
- Der atomare PR-Import schreibt ausschliesslich `ReviewSource`. `SaveCreatedAsync` schreibt bei Neuanlage ausschliesslich `CreatedByTask`; bei einem vorhandenen kanonischen PR-Schluessel ist nur ein idempotenter Treffer derselben Aufgabe mit derselben Rolle zulaessig. Ein Treffer einer anderen Aufgabe oder der Rolle `ReviewSource` fuehrt zu einem fachlichen Konflikt und darf die Rolle niemals stillschweigend umschreiben.
- Pro Aufgabe ist hoechstens eine `ReviewSource` zulaessig. Das wird durch einen gefilterten eindeutigen Index auf `AufgabeId` fuer `Rolle = ReviewSource`, eine Vorpruefung im atomaren Create-Pfad und eine gemeinsame Domain-/Servicevalidierung abgesichert. Werden beim Laden oder Start trotz dieser Sicherungen mehrere Review-Quellen gefunden, bricht der Vorgang mit einem expliziten Datenkonsistenzfehler ab; es gibt weder Auswahl nach Reihenfolge noch Fallback auf den normalen Start.
- Die Startentscheidung betrachtet ausschliesslich die Anzahl der als `ReviewSource` geladenen Referenzen: keine Review-Quelle bedeutet unveraendert `CreateTaskBranch`, genau eine bedeutet `CheckoutPullRequestSource`, mehr als eine bedeutet Datenkonsistenzfehler. Die Gesamtzahl aller PR-Referenzen ist fuer diese Entscheidung ohne Bedeutung.
- Eine importierte Aufgabe darf spaeter weitere `CreatedByTask`-Referenzen erhalten. Bei jedem erneuten Start bleibt die urspruengliche `ReviewSource` die alleinige Checkout-Quelle; erzeugte Referenzen werden weiterhin angezeigt und nach ihrer eigenen Monitoring-Policy verarbeitet, aber niemals als Startquelle interpretiert.

### Repository-Kontext der Vorschlagsliste

- Die Vorschlagsliste bleibt konservativ wie bisher auf `_selectedRepository` begrenzt. Pro Ladevorgang wird genau dieses Repository einmal als Snapshot erfasst; Issues und PRs werden nur ueber dessen Plugin und kanonische API-ID geladen. Es gibt keine Aggregation ueber alle aktiven Projekt-Repositories und keine repositoryuebergreifende Aenderung der Issue-Identitaet oder des bestehenden `IssueReferenz.IssueNummer`-Filters.
- Jeder repositorygebundene `ScmRequirement`-Vorschlag, also Issue und Pullrequest, traegt einen unveraenderlichen `ScmRepositoryContext` aus lokaler `GitRepositoryId`, normalisiertem `PluginPrefix` und kanonischer Ziel-`RepositoryId`. Alerts behalten ihren nicht repositorygebundenen Pfad. Der Kontext wird beim Laden aus demselben Repository-Snapshot gebildet und nicht spaeter aus der aktuellen Auswahl neu abgeleitet.
- Providerabruf, Anzeige, Filter und Create-Pfad verwenden denselben Snapshot. Ein Auswahlwechsel annulliert bzw. versioniert den laufenden Abruf, sodass spaete Ergebnisse des zuvor ausgewaehlten Repositories nicht angezeigt werden. Der Create-Command uebergibt ausschliesslich den Kontext des angeklickten Vorschlags und niemals `_selectedRepository` zum Ausfuehrungszeitpunkt.
- Der Application-Service validiert atomar, dass die lokale `GitRepositoryId` zum Projekt gehoert und dass Plugin-Prefix, kanonische Ziel-Repository-ID sowie die Ziel-ID des PR-Datensatzes konsistent sind. Bei Abweichung wird die Anlage ohne Teilpersistenz abgelehnt. Die neue Aufgabe erhaelt exakt diese `GitRepositoryId`; die `ReviewSource.RepositoryId` erhaelt exakt die kanonische Ziel-ID. Checkout und Pluginaufloesung verwenden danach die persistierte Aufgaben-Repositoryzuordnung und die Review-Quelle.
- Der Issue-Pfad erhaelt denselben Snapshot gegen Auswahlwechsel, bleibt ansonsten aber fachlich unveraendert: nur Issues des ausgewaehlten Repositories werden geladen, die bestehende Nummernfilterung wird nicht zu einer projektweiten Multi-Repository-Identitaet erweitert und Alerts bleiben unberuehrt.

### Provider-Repraesentation und Hosting-Modus

- `PullRequestProvider` wird mit expliziten, persistenzstabilen Zahlenwerten definiert: `GitHub = 0`, `BitbucketCloud = 1` und `BitbucketServerDataCenter = 2`. Der bestehende GitHub-Wert bleibt dadurch unveraendert; Cloud und Server/Data Center koennen weder untereinander noch mit GitHub im fachlichen Schluessel kollidieren.
- Der Bitbucket-Hosting-Modus wird an einer zentralen Stelle strikt und case-insensitiv normalisiert: `Cloud` wird zu `BitbucketCloud`; `SelfHosted` sowie intern akzeptierte Bezeichnungen `Server` und `DataCenter` werden zu `BitbucketServerDataCenter`. Ein leerer oder unbekannter konfigurierter Wert wird vor dem API-Aufruf als Konfigurationsfehler abgelehnt und niemals auf GitHub oder Cloud zurueckgesetzt.
- Der Default `PullRequestProvider.GitHub` im `PullRequest`-Konstruktor entfaellt. Jeder Provider-Parser und jede Test-Fixture muss den Provider explizit setzen. Das Bitbucket-Plugin setzt den normalisierten Wert bereits beim API-Mapping; `ScmRequirement`, der Create-Request und `PullRequestReferenz` reichen ihn ohne Neuableitung durch.
- Ein zentraler Providerdeskriptor bildet `GitHub` auf `Softwareschmiede.GitHub` und beide Bitbucket-Werte auf `Softwareschmiede.Bitbucket` ab. Provideraufloesung fuer Checkout und weitere Provideroperationen verwendet diesen Deskriptor und faellt bei unbekannten Werten nicht auf das Default-SCM-Plugin zurueck.
- Projektdetail- und Aufgabendetailansicht zeigen die stabilen Bezeichnungen `GitHub`, `Bitbucket Cloud` bzw. `Bitbucket Server/Data Center`. Filter, Persistenz und Anzeige verwenden ausschliesslich den bereits gemappten Enum-Wert; der aktuelle Plugin-Hosting-Modus darf eine erneut geladene Referenz nicht nachtraeglich umdeuten.

### Vollstaendiger PR-Abruf und Pagination

- `IGitPlugin.GetOpenPullRequestsAsync(repositoryId, ct)` liefert ausschliesslich offene PRs fuer die normalisierte `GitRepository.RepositoryName`.
- GitHub verwendet den paginierten API-Pfad, beispielsweise `gh api --paginate --slurp` gegen `/repos/{owner}/{repo}/pulls?state=open&per_page=100`; alle Seiten werden zusammengefuehrt. Ein festes `gh pr list --limit` ist nicht ausreichend.
- Bitbucket Cloud folgt dem `next`-Link, bis keiner mehr vorhanden ist. Bitbucket Server/Data Center folgt `isLastPage` und `nextPageStart`. Beide Schleifen beachten `CancellationToken`, erkennen wiederholte Continuation-Werte und brechen dann mit Providerfehler ab.
- Provider-Tests erzwingen mindestens zwei Seiten und pruefen, dass nur offene PRs, aber alle offenen Seitenresultate geliefert werden.

### PR-Checkout und Forks

- Der Startvertrag erhaelt einen expliziten Modus, etwa `BranchSetupMode.CreateTaskBranch` und `BranchSetupMode.CheckoutPullRequestSource`, statt den Modus indirekt aus `basisBranchName` und dem Default-Branch abzuleiten.
- `EntwicklungsprozessService` bestimmt den Modus aus der vollstaendig geladenen Aufgabe und ihrer Referenzrollen. Genau eine `ReviewSource` fuehrt immer zu `CheckoutPullRequestSource`, auch wenn weitere `CreatedByTask`-Referenzen vorhanden sind oder `SourceBranch` dem Remote-Default-Branch entspricht. Keine `ReviewSource` fuehrt unabhaengig von der Anzahl erzeugter PRs in den normalen Startpfad; mehrere Review-Quellen liefern den festgelegten Datenkonsistenzfehler. Im Review-Modus ist kein Codepfad zu `CreateBranchAsync` zulaessig.
- `PullRequest` und `PullRequestReferenz` werden um `SourceRepositoryId`, `SourceRepositoryUrl` und `SourceRef` erweitert. `SourceBranch` bleibt der lokale Arbeitsbranch; `SourceRef` bezeichnet den vom Provider gelieferten fetchbaren Ref. `HeadSha` wird nach dem Checkout zur Verifikation verwendet, sofern vorhanden.
- `IGitPlugin` erhaelt `CheckoutPullRequestSourceAsync(localPath, checkoutSpec, ct)`. Das Checkout-Spec enthaelt Ziel-Repository-ID/-URL, Source-Repository-ID/-URL, Source-Branch, SourceRef und optional Head-SHA.
- Bei einem PR aus dem konfigurierten Repository fetcht die Implementierung den Provider-Ref bzw. `refs/heads/{SourceBranch}` von `origin` und checkt ihn als lokalen `SourceBranch` aus. Das gilt auch fuer einen Source-Branch mit dem Namen des Default-Branches.
- Bei einem Fork-PR fetcht die Implementierung `SourceRef` aus `SourceRepositoryUrl` mit den vorhandenen Provider-Zugangsdaten und checkt den erhaltenen Commit lokal als `SourceBranch` aus. Es wird kein dauerhaftes, kollisionsanfaelliges Remote benoetigt. GitHub mappt `head.repo`, `head.ref` und den Head-SHA; Bitbucket mappt das Source-Repository samt Clone-Link und Branch/Ref fuer Cloud sowie Server/Data Center.
- Ist der Source-Branch nicht unter `origin` vorhanden, wird nicht auf `CreateBranchAsync` zurueckgefallen. Sind Fork-Metadaten vorhanden, wird der Fork-Pfad verwendet. Fehlen sie oder schlaegt Fetch/Head-SHA-Pruefung fehl, bleibt die Aufgabe im vorherigen Status und die UI zeigt den konkreten Startfehler.
- Normale Aufgaben sowie Aufgaben mit beliebig vielen ausschliesslich als `CreatedByTask` markierten Referenzen verwenden weiterhin unveraendert den bisherigen Default-/Basisbranch- und `CreateBranchAsync`-Pfad.

### Validierungszeitpunkt fuer Source-Daten

- Die strukturelle Validierung erfolgt verbindlich beim Anlegen der Aufgabe: `SourceBranch`, `SourceRepositoryId` und ein fetchbarer Locator (`SourceRef` oder die Kombination aus Source-Repository-URL und Branch) muessen vorhanden und gueltig sein. Andernfalls wird keine Aufgabe und keine `PullRequestReferenz` gespeichert; die Projektdetailansicht zeigt den Create-Fehler.
- Die Erreichbarkeit des Refs wird erst beim Start nach dem Klonen/fetchen validiert. Ein inzwischen geloeschter Branch oder nicht mehr zugreifbarer Fork fuehrt zu einem sichtbaren Startfehler, ohne Statusabschluss und ohne neu erzeugten Branch.
- Unit-, ViewModel- und E2E-Tests verwenden dieselbe Trennung. Es gibt kein Szenario mehr, in dem eine PR-Aufgabe ohne strukturelle Source-Daten erfolgreich angelegt und erst beim Start wegen genau dieser fehlenden Daten abgewiesen wird.

### Rollenabhaengige Monitoring- und Auto-Complete-Policy

- Eine zentrale Policy entscheidet anhand von `Rolle` und `Provider` getrennt ueber `CanMonitor` und `CanAutoComplete`; die beiden Entscheidungen duerfen nicht aus der Monitoring-Phase oder aus einem globalen Plugin-Schalter allein abgeleitet werden.
- `CreatedByTask + GitHub` behaelt das bestehende Verhalten: periodisches und manuelles Monitoring sind aktiv, und nur der periodische Lauf darf bei erfolgreicher Pre-Merge-Phase sowie aktivierter Plugin-Einstellung `CompletePullRequestAsync` ausloesen. Damit regressiert der bisherige PR-Erstellungsfluss nicht.
- `ReviewSource + GitHub` wird periodisch und manuell nur lesend ueber Status und Workflows beobachtet, damit der Review-Zustand sichtbar bleibt. `CanAutoComplete` ist fuer diese Rolle immer `false`; weder periodischer Lauf noch manueller Refresh duerfen `CompletePullRequestAsync`, Approval oder Auto-Merge ausloesen. Ein erfolgreicher Check oder externer Merge aktualisiert nur Referenzstatus und Monitoring-Phase und schliesst weder den zu pruefenden PR aktiv ab noch aendert er automatisch den Aufgabenstatus.
- Bitbucket-Status- und Workflow-Abfragen sind nicht Bestandteil dieser Anforderung. `ReviewSource + BitbucketCloud` und `ReviewSource + BitbucketServerDataCenter` sind daher weder monitorbar noch auto-completable. Auch `CreatedByTask`-Referenzen dieser beiden Provider werden bis zu einer spaeteren expliziten Monitoring-Unterstuetzung nach derselben Providergrenze als nicht monitorbar behandelt; ihre Rolle bleibt dennoch fuer Start und Herkunft erhalten.
- `PullRequestMonitoringPhase` erhaelt den expliziten, terminalen Wert `NotMonitored = 12`; alle vorhandenen Enum-Werte werden mit ihren bisherigen Zahlenwerten `0` bis `11` fixiert. Bitbucket-Importe werden mit `NotMonitored`, `NextCheckUtc = null` und `LastError = null` initialisiert. GitHub-Review-Quellen starten wie GitHub-Ausgaben mit `Created`, unterscheiden sich aber bei der Auto-Complete-Freigabe ueber ihre Rolle.
- `GetDueForMonitoringAsync` und `GetRefreshableByAufgabeAsync` wenden `CanMonitor` an und schliessen `NotMonitored` als terminale Phase aus. `PullRequestMonitoringService.RunOnceAsync` berechnet `allowAutoComplete` pro Referenz aus `CanAutoComplete`; es gibt kein pauschales `true` mehr fuer alle faelligen GitHub-Referenzen. `MonitorAsync` prueft die Policy defensiv erneut, bevor Provider- oder Completion-Aufrufe erfolgen.
- In der Aufgabendetailansicht bleiben Rolle, Provider, Link, PR-Nummer und importierter Status nachvollziehbar. `NotMonitored` wird neutral als `Nicht automatisch ueberwacht` dargestellt. Der Refresh ist aktiv, wenn mindestens eine monitorbare Referenz vorhanden ist, verarbeitet bei gemischten Rollen/Providern aber nur freigegebene Referenzen; Bitbucket erzeugt weder Provideraufruf noch `LastError`/`Failed`.

## Umsetzungsschritte

### 1. Contracts, Value Objects und Normalisierung

Betroffene Dateien:

- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestProvider.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestMonitoringPhase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRequirement.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRequirementKind.cs`
- neue Value Objects fuer `ScmRepositoryContext`, Repository-Normalisierung, Providerdeskriptor und PR-Checkout-Spec

Massnahmen:

1. `GetOpenPullRequestsAsync` und `CheckoutPullRequestSourceAsync` in Vertrag und Basisklasse aufnehmen; nicht unterstuetzende Plugins liefern fuer den Listenabruf leer und fuer den Checkout einen klaren `NotSupportedException`-Fehler.
2. `PullRequestProvider` und `PullRequestMonitoringPhase` mit den festgelegten Zahlenwerten erweitern, den impliziten GitHub-Default aus `PullRequest` entfernen und den Providerdeskriptor samt Hosting-Modus-Normalisierung implementieren.
3. `PullRequest` um die Source-Repository-/Ref-Daten erweitern und `ScmRequirement` um einen eigenen Pullrequest-Kind samt stabiler Provider-, Anzeige- und Vergleichsdaten ergaenzen. Repositorygebundene Issue- und PR-Vorschlaege muessen zusaetzlich den unveraenderlichen `ScmRepositoryContext` aus lokaler `GitRepositoryId`, Plugin-Prefix und kanonischer Ziel-Repository-ID tragen; Konstruktoren verhindern unvollstaendige Kontexte.
4. Einen einzigen providerabhaengigen Normalisierer implementieren. Ungueltige IDs und unbekannte Hosting-Modi liefern einen expliziten Validierungsfehler; Aufrufer duplizieren keine String-Normalisierung.
5. Contract-Tests pruefen Enum-Zahlenstabilitaet, Hosting-Modus-Mapping, Plugin-Prefix-Aufloesung, gueltige/ungueltige GitHub-, Bitbucket-Cloud- und Bitbucket-Server-IDs, Gross-/Kleinschreibung und `.git`-Suffix sowie die Unveraenderlichkeit und Pflichtfelder des Repository-Snapshots.

### 2. GitHub- und Bitbucket-Plugin

Betroffene Dateien:

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/BitbucketPluginTests.cs`

Massnahmen:

1. GitHub-PRs ueber den vollstaendig paginierten API-Listenpfad laden und Nummer, Titel, URL, Provider-ID, Source-/Target-Branch, Source-Repository-ID/-URL, SourceRef und Head-SHA mappen.
2. Bitbucket-PRs aus der Bitbucket-API laden, nicht aus Jira. Cloud- und Server/Data-Center-Antworten getrennt parsen, ihre jeweilige Pagination vollstaendig durchlaufen und jeden Treffer explizit als `BitbucketCloud` bzw. `BitbucketServerDataCenter` mappen.
3. Beide Provider uebergeben ausschliesslich normalisierte Ziel- und Source-Repository-IDs. `RepositoryUrl` bleibt die vorhandene Clone-URL des Projekt-Repositories; der Providerwert wird vom API-Ergebnis bis zum Create-Request unveraendert mitgefuehrt.
4. Den dedizierten PR-Checkout implementieren: gleicher Ursprung ueber `origin`, Fork ueber authentifizierten Source-URL-/Provider-Ref-Fetch, danach lokaler Checkout und optionale Head-SHA-Pruefung. Keine Implementierung ruft in diesem Pfad `CreateBranchAsync` auf.
5. Tests fuer zwei Seiten, leere/fehlerhafte Antworten, Cloud/Self-Hosted, Same-Repo, Default-Branch, Fork, fehlenden Origin-Branch, Fetch-Fehler und Head-SHA-Abweichung ergaenzen.

### 3. Domaene, Persistenz und atomare Anlage

Betroffene Dateien:

- `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs`
- neue persistenzstabile Enum `PullRequestReferenzRolle`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- neue EF-Migration samt ModelSnapshot
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs`
- neue zentrale Provider-/Monitoring-Policy in der Contract- oder Application-Schicht
- zugehoerige Service-/Persistenztests

Massnahmen:

1. Source-Repository-ID/-URL und SourceRef nullable fuer Altbestand persistieren; neue Vorschlagsanlagen verlangen sie gemaess struktureller Validierung. `Rolle` wird nicht nullable mit `CreatedByTask = 0` und `ReviewSource = 1` persistiert.
2. Die Migration setzt den gesamten Altbestand explizit auf `CreatedByTask`, normalisiert die vorhandenen Repository-IDs und legt danach sowohl den globalen kanonischen PR-Schluessel als auch den gefilterten eindeutigen Index fuer hoechstens eine `ReviewSource` pro `AufgabeId` an. Vor Indexanlage prueft sie auf Kollisionen und bricht mit klarer Diagnose ab, statt Rollen oder Referenzen zu raten bzw. zusammenzufuehren.
3. Aufgabenabfragen, insbesondere `GetDetailAsync`, laden `PullRequests` inklusive Rolle. Eine gemeinsame `GetSingleReviewSourceOrThrow`-artige Validierung liefert null, genau eine Review-Quelle oder einen expliziten Datenkonsistenzfehler und wird von Start- und weiteren rollenabhaengigen Pfaden verwendet.
4. Eine projektunabhaengige Existenzabfrage ueber den kanonischen PR-Schluessel bereitstellen. Vorschlagsladen und Create-Pfad verwenden dieselbe Abfrage; die globale Eindeutigkeit gilt unabhaengig von Rolle, Projekt- oder Archivstatus.
5. Aufgabe und genau eine als `ReviewSource` markierte `PullRequestReferenz` in einer Transaktion anlegen. Vorher wird der `ScmRepositoryContext` gegen Projekt, lokale Repositoryzeile, Plugin-Prefix und kanonische Ziel-ID validiert; die Aufgabe erhaelt exakt dessen `GitRepositoryId`. Provider und Source-Felder werden unveraendert persistiert. Die Monitoring-Policy initialisiert GitHub mit `Created` und beide Bitbucket-Werte mit `NotMonitored`, leerem `NextCheckUtc` und leerem `LastError`. Validierungsfehler erzeugen keine Teilobjekte; Indexverletzungen werden in konkrete Fehler fuer Doppelzuordnung bzw. widerspruechliche Review-Quellen uebersetzt.
6. `PullRequestReferenzService.SaveCreatedAsync` setzt bei Neuanlage immer `CreatedByTask`. Idempotenz ist nur fuer denselben kanonischen PR, dieselbe Aufgabe und dieselbe Rolle erlaubt; eine vorhandene `ReviewSource`, eine andere Aufgabe oder ein Versuch zur Rollenumdeutung liefert einen expliziten Konflikt.
7. Persistenz-/Service-Tests decken Altbestandsmigration, Rollenzahlen, genau eine Review-Quelle, absichtlich widerspruechliche Daten, aktive und archivierte Aufgaben, andere Projekte, gemischt geschriebene IDs, getrennte Cloud-/Server-Schluessel, zwei konkurrierende Create-Versuche und eine importierte Aufgabe mit zusaetzlicher `CreatedByTask`-Referenz ab.
8. Repository-Service-Tests verwenden ein Projekt mit mindestens zwei aktiven Repositories A und B. Sie beweisen, dass ein Vorschlag mit Snapshot A die Aufgabe, `GitRepositoryId`, Review-Quelle und kanonische Repository-ID atomar A zuordnet, selbst wenn B inzwischen ausgewaehlt ist; inkonsistente Kombinationen aus A/B werden ohne Teilpersistenz abgelehnt. Der Issue-Create-Pfad bleibt auf den uebergebenen Snapshot des ausgewaehlten Repositories begrenzt und fuehrt keine repositoryuebergreifende Issue-Identitaet ein.
9. Ein parametrisierter Integrationsnachweis fuer `BitbucketCloud` und `BitbucketServerDataCenter` fuehrt je ein realistisches paginiertes API-Ergebnis durch den Plugin-Parser, `ScmRequirement` samt Repository-Snapshot, die atomare Aufgabenanlage und Persistenz. Nach Dispose und Neuaufbau des DbContext werden Rolle `ReviewSource`, lokale `GitRepositoryId`, Provider, normalisierte Repository-ID, Nummer und `NotMonitored` erneut geladen; ein anschliessendes Laden der Projektdetailvorschlaege filtert genau diesen PR aus, waehrend ein PR mit gleichem Repository und gleicher Nummer des jeweils anderen Bitbucket-Providers getrennt bleibt.

### 4. Expliziter Aufgabenstart

Betroffene Dateien:

- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- ggf. `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_BasisBranch.cs`

Massnahmen:

1. Branch-Setup als expliziten Modus/Request modellieren. Der Service laedt die Rollen und leitet ausschliesslich bei genau einer `ReviewSource` zwingend `CheckoutPullRequestSource` ab; ein vom UI uebergebener `basisBranchName`, die Zahl der `CreatedByTask`-Referenzen oder deren Reihenfolge duerfen dies nicht ueberschreiben. Mehrere Review-Quellen brechen mit Datenkonsistenzfehler ab.
2. `SetupBranchAsync` in getrennte Pfade fuer normalen Task-Branch und PR-Checkout aufteilen. Der PR-Pfad kennt keine `CreateBranchAsync`-Alternative.
3. Plugin und Clone-Repository werden aus der persistierten `GitRepositoryId` der Aufgabe aufgeloest; Ziel-Repository-ID und Provider der `ReviewSource` muessen dazu passen. Start erst nach erfolgreichem Checkout und SHA-Pruefung finalisieren. Inkonsistenz oder Checkoutfehler lassen Status und Startdaten unveraendert und werden ueber die bestehende UI-Fehleranzeige sichtbar.
4. Service-Tests pruefen explizit: keine Referenz, genau eine `CreatedByTask`-Referenz und mehrere `CreatedByTask`-Referenzen verwenden den normalen CreateBranch-Pfad; eine `ReviewSource` mit und ohne weitere erzeugte Referenzen verwendet immer dieselbe Review-Quelle; mehrere Review-Quellen liefern den expliziten Fehler. Hinzu kommen Source = Default-Branch, Same-Repo-Branch, Fork, fehlender `origin`-Branch mit Fork-Fallback und nicht fetchbarer Source ohne Fallback.
5. Ein Zwei-Repository-Service-Test gibt Repository A und B unterschiedliche Plugin-Prefixe, kanonische IDs, Clone-URLs und Branches. Eine auf A persistierte Review-Aufgabe muss beim Start ausschliesslich Plugin, Clone und Source von A verwenden; Auswahlzustand oder Defaultwerte von B duerfen keinen Einfluss haben.

### 5. Provideraufloesung, Monitoring und Aufgabendetailansicht

Betroffene Dateien:

- `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs`
- `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs`
- zentraler Providerdeskriptor bzw. Monitoring-Policy
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/Application/Services/PullRequestMonitoringServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/PullRequestReferenzServiceTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Massnahmen:

1. Alle Provideraufloesungen auf den zentralen Deskriptor umstellen: GitHub wird ausschliesslich zum GitHub-Plugin, beide Bitbucket-Werte werden ausschliesslich zum Bitbucket-Plugin aufgeloest; unbekannte Werte liefern einen klaren Fehler statt Default-Plugin-Fallback.
2. Due- und Refresh-Abfragen sowie beide Einstiegspunkte des Monitoring-Service wenden die zentrale Rollen-/Provider-Policy an. Bitbucket-Referenzen werden nicht abgefragt, nicht auf `Failed` gesetzt und nicht mit einem Fehlertext versehen. GitHub-Referenzen beider Rollen werden beobachtet, aber nur `CreatedByTask` darf im periodischen Lauf Auto-Complete ausloesen.
3. Das bisher pauschale `allowAutoComplete: true` des periodischen Laufs wird durch `policy.CanAutoComplete(reference)` ersetzt. Direkt vor `TryCompleteAsync` erfolgt dieselbe rollenbasierte Schutzpruefung erneut. Ein manueller Refresh bleibt fuer alle Rollen rein lesend.
4. `TaskDetailViewModel` berechnet aus den geladenen Referenzen, ob mindestens eine monitorbare Referenz vorhanden ist, und aktualisiert den CanExecute-Zustand des Refresh-Commands nach jedem Laden. Der bestehende automatische Refresh beim Wechsel in die PR-Ansicht verarbeitet ebenfalls nur die vom Service freigegebenen Referenzen.
5. `TaskDetailView.xaml` zeigt den normalisierten Providernamen, die Referenzrolle in nachvollziehbarer Form und fuer `NotMonitored` den neutralen Phasentext `Nicht automatisch ueberwacht`; `LastError` bleibt leer. Der Aktualisieren-Befehl ist bei ausschliesslich nicht monitorbaren Referenzen deaktiviert, bei gemischten Referenzen aktiv.
6. Service- und ViewModel-Tests pruefen die vollstaendige Policy-Matrix: `CreatedByTask + GitHub` darf periodisch den bisherigen Completion-Pfad erreichen; `ReviewSource + GitHub` wird periodisch/manuell aktualisiert, ruft aber selbst bei `PreMergeSucceeded` niemals `CompletePullRequestAsync` auf und aendert den Aufgabenstatus nicht; beide Bitbucket-Review-Quellen sind weder faellig noch refreshbar und erzeugen keinen Provideraufruf/`LastError`/`Failed`. Gemischte Aufgaben verarbeiten jede Referenz nach ihrer eigenen Rolle.

### 6. Projektdetailansicht und Vorschlagsfluss

Betroffene Dateien:

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- ggf. vorhandene Icon-/Converter-Ressourcen
- zugehoerige ViewModel-Tests

Massnahmen:

1. Beim Start eines Ladevorgangs `_selectedRepository` genau einmal in `ScmRepositoryContext` erfassen. Ausschliesslich fuer diesen Snapshot Issues und alle Seiten offener PRs laden und gemeinsam mit Alerts in `OffeneAnforderungen` zusammenfuehren; andere aktive Projekt-Repositories werden nicht abgefragt oder aggregiert.
2. Laufende Ladevorgaenge mit Cancellation/Generationskennung an die Auswahl binden. Nach einem Repositorywechsel werden spaete Ergebnisse des vorherigen Snapshots verworfen. Jeder erzeugte Issue-/PR-Vorschlag traegt den beim Abruf verwendeten Snapshot unveraendert.
3. PRs ueber den kanonischen Provider-/Repository-/Nummer-Schluessel gegen die globale Referenzabfrage filtern. Filterung und Provideraufruf verwenden die kanonische ID des Snapshots. Nach erfolgreicher Anlage wird der Vorschlag entfernt; bei Konkurrenzfehler wird nur der aktuell ausgewaehlte Repository-Snapshot neu geladen.
4. PR-Listeneintraege erhalten einen sichtbaren Typtext, den normalisierten Providernamen, ein vorhandenes passendes Icon und stabile AutomationProperties fuer Typ, Provider, Nummer und lokale Repository-ID. Issue-Darstellung und -Bedienung bleiben unveraendert.
5. Der bestehende Auswahl-/Doppelklickpfad dispatcht den PR-Create-Pfad mit `SelectedRequirement.RepositoryContext` und navigiert nach Erfolg zur neuen Aufgabe. Er liest zu keinem Zeitpunkt eine moeglicherweise inzwischen geaenderte `_selectedRepository.Id` oder Standard-Pluginauswahl nach.
6. Fehlende strukturelle Source-Daten oder ein nicht mehr zum Projekt passender Repository-Snapshot werden beim Create sichtbar gemeldet; es wird keine unstartbare bzw. falsch zugeordnete Aufgabe angelegt.
7. ViewModel-Tests mit zwei aktiven Projekt-Repositories A und B und absichtlich gleichen Issue-/PR-Nummern pruefen: Bei Auswahl A werden nur A-Ergebnisse angezeigt und mit A-Snapshot versehen; ein spaetes B- bzw. vorheriges A-Ergebnis kann die aktuelle Liste nicht verunreinigen; der Create-Command fuer einen A-Vorschlag uebergibt auch nach einem simulierten Auswahlwechsel unveraendert A. Beim Wechsel nach B wird separat B geladen. Der bestehende Issue-Pfad bleibt pro Auswahl begrenzt und fuehrt weder Aggregation noch einen neuen repositoryuebergreifenden Issue-Schluessel ein.

### 7. Prozessuebergreifende Desktop-E2E-Testarchitektur

Neue bzw. betroffene Dateien:

- neues Testprojekt `src/Softwareschmiede.E2E.TestScmPlugin/`
- Solution-Datei und `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`

Architektur und Build:

1. Das neue `Softwareschmiede.E2E.TestScmPlugin` implementiert `IGitPlugin`, ist aber kein App-/Publish-Plugin. Das Testprojekt erhaelt eine `ProjectReference` mit `ReferenceOutputAssembly="false"`, damit die DLL vor E2E-Laeufen gebaut wird. `Softwareschmiede.App.csproj` wird nicht um dieses Plugin erweitert und `CopyPluginsToOutput`/`CopyPluginsToPublishOutput` kopieren es nicht.
2. `WpfTestBase` ermittelt das Testplugin-Buildverzeichnis und setzt vor `LaunchApp` drei pro Test eindeutige, an den Kindprozess vererbte Variablen: `SOFTWARESCHMIEDE_TEST_SCM_PLUGIN_DIR`, `SOFTWARESCHMIEDE_TEST_SCM_FIXTURE_PATH` und `SOFTWARESCHMIEDE_TEST_SCM_CALL_LOG`.
3. `PluginManager` durchsucht dieses zusaetzliche Verzeichnis nur, wenn `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist. Aus diesem Verzeichnis ist nur die exakt benannte Testplugin-Assembly erlaubt; ohne Fixture- und Logpfad wird sie nicht geladen. Der normale Testmodus-Filter fuer `LocalDirectoryPlugin` und KI-Testplugins bleibt bestehen. Unit-Tests sichern ab, dass das Plugin ausserhalb des Testmodus und aus dem normalen Produktionsordner nie geladen wird.
4. Dadurch ist kein DLL-Kopieren in den App-Ausgabe- oder Publish-Ordner erforderlich. Der Test-Build und die externe Testmodus-Discovery sind der ausdrueckliche Bereitstellungsweg.

Fixtures und Aufzeichnung:

1. `WpfTestBase` schreibt vor dem App-Start atomar eine JSON-Fixture mit mindestens zwei voneinander unterscheidbaren Projekt-Repositories, ihren Issues, PRs, Providern, Plugin-Prefixen, kanonischen IDs, Source-/Target-Daten, Default-Branches und lokalen Remote-Pfaden. Jeder Test besitzt ein eigenes Temp-Verzeichnis; die E2E-Collection bleibt nicht parallel.
2. Das Testplugin liest die Fixture im App-Prozess fuer `GetAvailableRepositoriesAsync`, `GetIssuesAsync`, `GetOpenPullRequestsAsync` und Default-Branch-Abfragen. Es fuehrt keinen Provider-/Netzwerkzugriff aus.
3. Das Fixture erzeugt ueber Git-CLI ein lokales Source-Repository und einen lokalen Bare-Remote mit `main` und `feature/review-240`. Fuer Fork-Szenarien wird ein zweiter Bare-Remote angelegt, dessen Source-Branch nicht unter `origin` existiert.
4. `CloneRepositoryAsync`, `CheckoutRemoteBranchAsync`, `CheckoutPullRequestSourceAsync` und `CreateBranchAsync` schreiben je Aufruf einen JSONL-Eintrag mit Zeit, Operation, Repository/Remote, lokalem Pfad, Branch/Ref und Ergebnis in `SOFTWARESCHMIEDE_TEST_SCM_CALL_LOG`. Anschliessend fuehrt das Testplugin die reale lokale Git-Operation aus. Schreibvorgaenge sind pro Zeile atomar; der Test liest mit Retry/FileShare und wertet nur Eintraege seines eindeutigen Logpfads aus.
5. `WpfTestBase.Dispose` beendet zuerst den App-Prozess, loescht danach Fixture, Log und lokale Repositories und entfernt alle drei Umgebungsvariablen. Start und Dispose loeschen Altdateien defensiv.
6. Page Objects erhalten nur die fuer den realen Bedienpfad notwendigen Methoden und stabile Automation-IDs; Assertions auf Persistenz erfolgen gegen `TestDbPath`, Assertions auf Git-Verhalten gegen JSONL und den tatsaechlichen HEAD des lokalen Arbeitsverzeichnisses.

Verbindliches E2E-Hauptszenario `Projektdetail_PRVorschlag_AufgabeAnlegen_undStartetMitSourceBranch_E2E`:

1. Fixture mit Projekt-Repositories A und B vorbereiten. Beide enthalten ein Issue und einen offenen GitHub-PR mit absichtlich gleicher Nummer, aber unterschiedlichen Plugin-Prefixen, kanonischen IDs, Bare-Remotes und Source-Branches; A verwendet `feature/review-a`, B `feature/review-b`.
2. Projekt und beide Repositories ueber den realen UI-Zuweisungspfad einrichten, A auswaehlen und Projektdetail oeffnen. Nur Issue und PR aus A duerfen sichtbar sein; der PR ist als Pullrequest gekennzeichnet und seine Automation-Eigenschaften weisen A aus. Ergebnisse aus B duerfen nicht in der Liste stehen.
3. Den A-PR ueber den realen Auswahl-/Doppelklickpfad als Aufgabe anlegen. Datenbankseitig `Aufgabe.GitRepositoryId = A`, `PullRequestReferenz.Rolle = ReviewSource`, kanonische Ziel-ID A, Provider, Nummer, URL, SourceBranch, SourceRef und Source-Repository pruefen; es darf keine Zuordnung zu B existieren.
4. Aufgabe ueber den normalen Start-Button starten. Auf `CheckoutPullRequestSourceAsync` fuer Plugin/Remote A im JSONL warten, `feature/review-a` und HEAD im Arbeitsverzeichnis A pruefen und sicherstellen, dass weder Plugin/Remote B noch `CreateBranchAsync` aufgerufen wurden.
5. Zur Projektdetailansicht zurueckkehren und sicherstellen, dass der A-PR nicht erneut angeboten wird. Danach B auswaehlen und nachweisen, dass die Vorschlagsliste separat aus B geladen wird. Der Issue-Fluss zeigt jeweils nur das Issue des ausgewaehlten Repositories und wird nicht repositoryuebergreifend aggregiert oder umgefiltert.

Weitere verbindliche Desktop-E2E-Szenarien:

- PR-Source ist `main`: Checkout wird protokolliert, `CreateBranchAsync` bleibt aus.
- Fork-PR: Source-Ref wird aus dem zweiten lokalen Bare-Remote ausgecheckt, obwohl der Branch unter `origin` fehlt; kein stiller Fallback.
- Nicht fetchbarer Source-Ref: sichtbarer Startfehler, Aufgabe nicht finalisiert, kein `CreateBranchAsync`.
- PR ohne strukturelle Source-Daten: sichtbarer Create-Fehler und keine Aufgabe/Referenz in der Datenbank.
- Bereits verknuepfter PR aus einem anderen Projekt bzw. einer archivierten Aufgabe: kein Vorschlag.
- Gleichzeitige Anzeige von Jira-Issue und Bitbucket-PR fuer dasselbe ausgewaehlte Bitbucket/Jira-Repository: beide Vorschlaege sind nebeneinander sichtbar, der Issue bleibt als Issue bedienbar, der PR ist eindeutig als Pullrequest mit Bitbucket-Provider gekennzeichnet, und beide Vorschlaege verwenden denselben unveraenderlichen Repository-Snapshot.
- Je eine Bitbucket-Cloud- und Bitbucket-Server/Data-Center-Fixture: PR wird mit dem richtigen Providernamen sichtbar und anlegbar; nach Navigation weg und erneutem Laden bleiben Provider und `NotMonitored` erhalten, der PR wird nicht erneut vorgeschlagen, der Refresh ist deaktiviert und das Aufrufprotokoll enthaelt weder Status-/Workflow-Abfrage noch einen Monitoringfehler.
- Importierte GitHub-Review-Quelle: periodischer und manueller Statusabruf koennen protokolliert werden, aber auch bei erfolgreicher Pre-Merge-Fixture existiert kein `CompletePullRequestAsync`-/Approval-/Auto-Merge-Aufruf und der Aufgabenstatus wird nicht automatisch abgeschlossen.
- Aufgabe ohne jegliche `PullRequestReferenz`: Start ueber den realen UI-Startbutton verwendet unveraendert den normalen Default-/Basisbranch- und `CreateBranchAsync`-Pfad, erzeugt keinen `CheckoutPullRequestSourceAsync`-Eintrag und bleibt damit der explizite Regressionsnachweis fuer FR-7.
- Normale Aufgabe mit genau einer ueber `SaveCreatedAsync` erzeugten `CreatedByTask`-Referenz: bestehender `CreateBranchAsync`-Startpfad bleibt nachweisbar; die Referenz wird nicht als Review-Quelle interpretiert.
- Importierte Aufgabe mit zusaetzlicher `CreatedByTask`-Referenz: erneuter Start checkt weiterhin exakt die urspruengliche `ReviewSource` aus und ignoriert die erzeugte Referenz fuer die Startauswahl.

## Testmatrix und Abnahme

- Contract-/Normalizer-Tests: explizite Provider-, Monitoring- und Referenzrollen-Zahlenwerte, Hosting-Modus- und Plugin-Prefix-Mapping, alle kanonischen ID-Formen, ungueltige URLs, Case- und `.git`-Varianten sowie vollstaendige unveraenderliche Repository-Snapshots.
- Plugin-Tests: GitHub und Bitbucket Cloud/Server mit mindestens zwei Seiten, offenen/geschlossenen Ergebnissen, korrektem Providerwert, Source-Metadaten und Pagination-Abbruchschutz.
- Persistenz-/Service-Tests: Altbestand wird `CreatedByTask`, atomarer Import wird `ReviewSource`, `SaveCreatedAsync` bleibt `CreatedByTask`, genau eine Review-Quelle, expliziter Widerspruchsfehler, Provider-Durchreichung, globale Doppelzuordnung inklusive Archiv/anderem Projekt, Konkurrenz, Source-Felder, `NotMonitored`-Initialisierung und Migration.
- Repository-Service-Tests mit mindestens zwei Projekt-Repositories: Snapshotvalidierung, atomare Zuordnung zu A, Ablehnung gemischter A/B-Daten, persistierte `GitRepositoryId`/kanonische Ziel-ID und Checkout ueber dasselbe Repository; Issue-Create bleibt auf den ausgewaehlten Snapshot begrenzt.
- Durchgaengiger Bitbucket-Integrationstest: API-Ergebnis -> Plugin-Mapping -> `ScmRequirement` mit Repository-Snapshot -> Create -> Persistenz -> neuer DbContext -> erneutes Laden -> Vorschlagsfilter, parametrisiert fuer Cloud und Server/Data Center.
- Monitoring-Tests: `CreatedByTask + GitHub` behaelt Auto-Complete, `ReviewSource + GitHub` ist nur lesend und kann niemals Completion ausloesen, Bitbucket-Review-Quellen sind weder automatisch faellig noch manuell refreshbar und fuehren zu keinem Plugin-Aufruf/`LastError`/`Failed`; gemischte Aufgaben wenden die Policy pro Referenz an.
- Starttests: Auswahl ausschliesslich nach Rolle, null/eine/mehrere Review-Quellen, zusaetzliche erzeugte Referenzen, expliziter Modus, Default-Branch, Same-Repo, Fork, nicht fetchbarer Ref, Head-SHA und normale Aufgabe mit vorhandener erzeugter PR-Referenz.
- ViewModel-/UI-Tests mit mindestens zwei Projekt-Repositories: nur ausgewaehltes Repository laden/anzeigen, Snapshot trotz Auswahlwechsel beibehalten, veraltete Ladeergebnisse verwerfen, Providerkennzeichnung, Filter, erfolgreicher Create-Pfad, strukturelle Create-Validierung, neutraler `NotMonitored`-Text und Refresh-CanExecute; Issue-Workflow bleibt repositoryweise statt aggregiert.
- Desktop-E2E: alle oben genannten Benutzerflaechen-Szenarien im separaten App-Prozess mit mindestens zwei lokalen Repository-Remotes, Testplugin und JSONL-Aufrufnachweis. Der Haupttest muss Anzeige, Anlage, Persistenz und Checkout derselben Repository-Identitaet durchgaengig korrelieren.
- Ausfuehrung: komplette Unit-/Integrationstests, `Category!=OsInterface` sowie der gezielte Windows-/FlaUI-Lauf der neuen `OsInterface`-Tests. Fehlende oder uebersprungene E2E-Szenarien gelten nicht als Abnahme.

## Reihenfolge und Risiken

1. Contracts, Repository-Snapshot, Normalisierung und Source-Metadaten.
2. Provider-Abruf samt Pagination und Fork-Mapping.
3. Referenzrollen-Migration, globale Zuordnung, Monitoring-Policy und atomarer Create-Pfad.
4. Expliziter rollenbasierter PR-Startmodus und Checkout-Implementierungen.
5. Provideraufloesung, rollenabhaengiges Monitoring und Aufgabendetailansicht.
6. Projektdetail-UI und ViewModel.
7. Testplugin-Infrastruktur, Page Objects und verbindliche Desktop-E2E-Szenarien.

Wesentliche Risiken sind unterschiedliche Bitbucket-Schemata, versehentliche Provider-Defaults, eine falsche Rollenmigration, Auto-Complete einer Review-Quelle, Repositorywechsel waehrend asynchroner Abrufe, unzugreifbare Forks und Desktop-Testisolation. Sie werden durch getrennte Provider-Parser, explizite Enum-Werte und Rollen, Altbestand=`CreatedByTask`, doppelt abgesicherte Rollen-/Monitoring-Policy, unveraenderliche Repository-Snapshots mit Generationskennung, einen expliziten Fehler ohne Branch-Fallback, lokale Bare-Remotes, pro Test eindeutige Dateien und ausschliesslich testmodusgebundene Plugin-Discovery begrenzt.

## Offene Punkte

Keine fachlichen oder technischen offenen Punkte. Die beschriebenen Entscheidungen sind verbindliche Umsetzungsvorgaben.
