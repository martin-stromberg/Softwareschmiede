# Testbasis und erwartete Abdeckung

## Vorhandene Tests

- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` testet den bestehenden Issue-JSON-Abruf und Checkout-Verhalten.
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/BitbucketPluginTests.cs:410-440` testet den Jira-Issue-Abruf.
- `src/Softwareschmiede.Tests/Application/Services/PullRequestReferenzServiceTests.cs` und `PullRequestMonitoringServiceTests.cs` decken die bestehende PR-Persistenz und das Monitoring ab.
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_BasisBranch.cs` sowie `EntwicklungsprozessServiceTests.cs` prüfen Checkout eines vorhandenen Branches gegenüber dem Erzeugen eines neuen Branches.
- `src/Softwareschmiede.Tests/App/ViewModels/IssueSelectionDialogViewModelTests.cs` und `TaskDetailViewModelTests.cs` bilden die vorhandene Issue-Zuordnung ab.
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs` und `src/Softwareschmiede.Tests/E2E/Views/ProjectDetailView.cs` sind die naheliegende E2E-Basis für die Projektdetailansicht.

## Für die Umsetzung notwendige Tests

1. GitHub-Plugin liefert nur offene PRs und mappt Nummer, Titel, URL, Source-/Target-Branch, Provider und Repository-ID.
2. Bitbucket-Plugin liefert offene PRs für Cloud und, soweit unterstützt, Self-Hosted und behält die Repository-Metadaten.
3. `ProjectDetailViewModel` führt Issues und PRs gemeinsam zusammen, kennzeichnet PRs, filtert bereits zugeordnete PRs und entfernt den Vorschlag nach erfolgreicher Anlage.
4. Der Create-Service persistiert eine `PullRequestReferenz` mit allen Checkout-relevanten Feldern und verhindert Doppelzuordnung.
5. Der Aufgabenstart ruft für PR-Aufgaben `CheckoutRemoteBranchAsync` mit `SourceBranch` auf und ruft `CreateBranchAsync` nicht auf; für normale Aufgaben bleibt der bisherige Pfad erhalten.
6. Ein E2E-Szenario deckt den Benutzerfluss Vorschlag anzeigen, PR-Aufgabe anlegen und Start der Review-Aufgabe ab. Providerzugriffe sollten im Test über die vorhandenen Plugin-Mocks/Fixtures deterministisch sein.

## Abdeckungslücke

Die aktuelle E2E-Suite weist keinen PR-Vorschlags- und Review-Start-Fluss aus. Dieser muss im Plan ausdrücklich als primärer Nachweis für die UI-Anforderung verankert werden.
