# Repository-Provider und fehlende Create-/Template-Schnittstellen

## Gemeinsamer Vertrag

`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs` bietet derzeit `GetIssuesAsync`, Git-Operationen, Pull-Request-Erstellung und optionale Repository-Strukturfunktionen. Eine Methode zum Erstellen eines Issues oder Laden von Issue-Templates existiert nicht.

`GitPluginBase<TPlugin>` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`) macht `GetIssuesAsync` und `CreatePullRequestAsync` abstrakt. Neue Create-/Template-Funktionen müssen daher bewusst als verpflichtend, optional per Default-Implementierung oder über ein separates Capability-Interface gestaltet werden. Die Anforderung verlangt, dass fehlende Template-Unterstützung die Anlage ohne Template nicht blockiert; eine optionale Fähigkeit mit klarer `NotSupported`-/Fehlersemantik passt dazu besser als eine unbedingte Template-Pflicht.

Das vorhandene `GitActionCapabilities` beschreibt Git/PR-Aktionen, nicht Issue-Fähigkeiten. Es enthält keine Flags für Issue-Erstellung oder Templates.

## Issue-Wertobjekt und fehlendes Request-Modell

`Domain/ValueObjects/Issue.cs` ist ein read-orientiertes Record mit `Nummer`, `Titel`, `Body`, `Labels`, `Milestone` und `IssueUrl`. Es gibt kein Create-Request-Modell und keine stabile externe ID neben der Nummer. Für die Neuanlage werden mindestens ein separates Eingabemodell für Titel/Body und ein Ergebnis-/Referenzmodell benötigt; providerabhängige Felder sollten optional bleiben oder in Provider-spezifischen Erweiterungen liegen.

## GitHub

`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:325-366` ruft `gh issue list --repo ... --json number,title,body,labels,milestone` auf und parst die Antwort. Der Code enthält keine `gh issue create`-Operation und keine Template-Abfrage. Der Provider verwendet ein GitHub-Token aus dem Credential Store und setzt `GH_TOKEN` für CLI-Aufrufe.

Für die Umsetzung muss daher entschieden werden, ob `gh issue create` oder die GitHub REST/GraphQL-API genutzt wird. Templates sind im GitHub-Ökosystem typischerweise Repository-Dateien bzw. Formulare; der aktuelle Provider hat dafür keinen Zugriffspfad und das bestehende `Issue`-Modell bildet Formular-Metadaten nicht ab.

## Bitbucket und Jira

`plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs:326-397` liest Issues nicht aus einer Bitbucket-Issue-API, sondern über Jira REST `/rest/api/3/search/jql`. Jira-Zugangsdaten, URL und Projekt-Key werden aus Plugin-Credentials gelesen. Jira-Issues werden in `Issue` mit `Nummer: 0` und einem zusammengesetzten Titel aus Jira-Key und Summary abgebildet.

Der Provider enthält aktuell keine Jira-Create-Operation und keinen Endpoint für Template-Verfügbarkeit. Für eine Neuanlage müssen Jira-Pflichtfelder wie Projekt, Summary, Issue-Typ und die Beschreibung in Atlassian Document Format berücksichtigt werden; die vorhandene `RenderAdf`-Logik ist nur lesend.

Bitbucket- und Jira-Templates sind damit im Bestand nicht verifiziert oder nutzbar. Der Plan sollte die beiden Provider-Funktionen getrennt bewerten und für nicht unterstützte Templates den No-Template-Pfad sicherstellen.

## LocalDirectory und weitere Provider

`Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs:135-136` wirft für `GetIssuesAsync` bereits `NotSupportedException`. Für diesen Provider darf die neue Ribbon-Aktion nicht als verfügbar erscheinen. Das bestehende Plugin- und Capability-Muster kann dafür als Grundlage dienen.

## Provider-Fehler

GitHub gibt beim Lesen fehlgeschlagener CLI-Aufrufe eine leere Liste zurück; Bitbucket/Jira gibt bei Fehlern ebenfalls eine leere Liste zurück. Diese Lesesemantik ist für die Erstellung nicht ausreichend: Create-Aufrufe müssen Fehler von einem erfolgreichen Ergebnis unterscheiden und verständliche Fehlermeldungen bis zum Dialog liefern.
