# Plugins und Verträge

## Gemeinsamer Vertrag

- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs:17-20` bietet derzeit `GetIssuesAsync`; ein Vertrag zum Abruf offener Pullrequests existiert dort nicht.
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs:7-26` erzwingt/implementiert denselben Issue-Abruf für alle Git-Plugins.
- `PullRequest` ist als gemeinsamer Value Object bereits vorhanden und wird für Erstellung, Status und Monitoring genutzt.
- `IGitPlugin.CheckoutRemoteBranchAsync` ist in `IGitPlugin.cs:117-121` vorhanden und damit der vorgesehene technische Mechanismus für den Review-Quell-Branch.

## GitHub

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs:354-377` ruft offene Issues über `gh issue list` ab und parst sie in `Issue`.
- `GitHubPlugin.cs:908-933` ruft einzelne PR-Statusdaten über `gh pr view` ab; `GitHubPlugin.cs:1060` enthält den Parser. Der Abruf einer Liste offener PRs ist noch nicht vorhanden.
- Die vorhandene CLI- und JSON-Parser-Testbasis liegt in `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`.

## Bitbucket/Jira

- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs:337-370` lädt aktuell Jira-Issues; `:457` parst diese.
- `BitBucketPlugin.cs:607-678` kann bereits Pullrequests erstellen, aber ein Abruf offener Bitbucket-PRs fehlt.
- Die Plugin-Tests liegen in `src/Softwareschmiede.Tests/Infrastructure/Plugins/BitbucketPluginTests.cs`; für die neue API sind Cloud- und Self-Hosted-Pfade sowie die Provider-Metadaten zu prüfen.

## Technische Risiken

- Für GitHub muss der Listenaufruf nur offene PRs liefern und Quell-Branch, Ziel-Branch, URL sowie stabile ID zurückgeben.
- Bitbucket/Jira vermischt im bestehenden Plugin zwei externe Systeme. Pullrequests müssen aus Bitbucket und nicht aus der Jira-Issue-Suche stammen; die Repository-ID und Hosting-Konfiguration müssen korrekt in die Provider-Anfrage eingehen.
