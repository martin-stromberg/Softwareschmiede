# Tasks: Unterstützung eines Staging-Branch / Basis-Branch-Konfiguration

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `GitRepository.DefaultSourceBranchName`-Eigenschaft hinzufügen | Erledigt | Build erfolgreich |
| 2 | Datenbank | Migration `AddDefaultSourceBranchNameToGitRepository` erstellen und anwenden | Erledigt | Migration per `dotnet ef migrations add` erzeugt (`src/Softwareschmiede/Migrations/20260803163323_AddDefaultSourceBranchNameToGitRepository.cs`) |
| 3 | Schnittstellen | `IGitPlugin.CreateBranchAsync()` um Parameter `sourceBranchName: string?` erweitern | Erledigt | Build erfolgreich |
| 4 | Schnittstellen | `IGitPlugin.CreatePullRequestAsync()` um Parameter `baseBranch: string?` erweitern | Erledigt | Build erfolgreich |
| 5 | Plugin (GitHub) | `CreateBranchAsync()` um Logik für Basis-Branch-Nutzung erweitern (`git checkout -b <branch> <remote>/<source>`) | Erledigt | Umgesetzt in `GitPluginBase.CreateBranchAsync` (gilt für GitHub- und BitBucket-Plugin, die diese Basisklasse nutzen) |
| 6 | Plugin (GitHub) | `CreatePullRequestAsync()` um GitHub-API-Anpassung für `baseBranch` erweitern | Erledigt | `gh pr create --base <baseBranch>` in `GitHubPlugin.CreatePullRequestAsync` |
| 7 | Logik | Validierungsmethode `ValidateBaseBranchExistsAsync()` in `EntwicklungsprozessService` implementieren | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldThrow_WhenBaseBranchDoesNotExist` |
| 8 | Logik | `ProzessStartenAsync()` um Basis-Branch-Validierungsaufruf erweitern | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldSucceed_WhenBaseBranchExists` |
| 9 | Logik | `SetupBranchAsync()` um Basis-Branch-Handling anpassen (Checkout + Create) | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.SetupBranchAsync_ShouldCreateBranchFromBaseBranch_WhenConfigured` |
| 10 | Logik | `GitOrchestrationService.PullRequestErstellenAsync()` um Basis-Branch-Übergabe an Plugin erweitern | Erledigt | `GitOrchestrationServiceTests.PullRequestErstellenAsync_ShouldCallPluginWithBaseBranch_WhenConfigured` |
| 11 | Tests | Unit-Test: Aufgabenstart schlägt fehl, wenn Basis-Branch nicht existiert | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldThrow_WhenBaseBranchDoesNotExist` |
| 12 | Tests | Unit-Test: Aufgabenstart erfolgreich mit existierendem Basis-Branch | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldSucceed_WhenBaseBranchExists` |
| 13 | Tests | Unit-Test: Aufgabenstart erfolgreich ohne Basis-Branch-Konfiguration (Fallback) | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldSucceed_WhenNoBranchConfigured` |
| 14 | Tests | Unit-Test: Feature-Branch wird vom Basis-Branch abgezweigt, wenn konfiguriert | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.SetupBranchAsync_ShouldCreateBranchFromBaseBranch_WhenConfigured` |
| 15 | Tests | Unit-Test: Feature-Branch wird vom HEAD abgezweigt, wenn nicht konfiguriert | Erledigt | `EntwicklungsprozessServiceTests_BasisBranch.SetupBranchAsync_ShouldCreateBranchFromHead_WhenNotConfigured` |
| 16 | Tests | Unit-Test: PR-Erstellung übergibt konfigurierten Basis-Branch an Plugin | Erledigt | `GitOrchestrationServiceTests.PullRequestErstellenAsync_ShouldCallPluginWithBaseBranch_WhenConfigured` |
| 17 | Tests | Unit-Test: PR-Erstellung übergibt `baseBranch=null`, wenn nicht konfiguriert | Erledigt | `GitOrchestrationServiceTests.PullRequestErstellenAsync_ShouldCallPluginWithoutBaseBranch_WhenNotConfigured` |
| 18 | Tests | Integrations-Test: `DefaultSourceBranchName` wird in DB gespeichert und kann gelesen werden | Erledigt | `Softwareschmiede.IntegrationTests.Services.EntwicklungsprozessServiceTests.DefaultSourceBranchName_ShouldBePersisted` (+ `_ShouldBeNull_WhenNotConfigured`) |
| 19 | Tests | Anpassung bestehender Tests in `EntwicklungsprozessServiceTests` auf neue Logik | Erledigt | Alle betroffenen Setup/Verify-Aufrufe auf neue `CreateBranchAsync`/`CreatePullRequestAsync`-Signaturen angepasst; volle Testlane grün (1160 erfolgreich, 1 übersprungen) |
| 20 | Tests | Anpassung bestehender Tests in `GitOrchestrationServiceTests` auf neue Logik | Erledigt | Alle betroffenen Setup/Verify-Aufrufe angepasst; volle Testlane grün |
| 21 | E2E-Tests | E2E: Repository-Zuordnung mit Basis-Branch-Auswahl (Benutzer wählt Basis-Branch, wird gespeichert) | Blockiert | Plan enthält keine UI-/ViewModel-Änderungen ("Neue Klassen: Keine", keine UI-Klassen unter "Änderungen an bestehenden Klassen") — es existiert kein Eingabefeld für `DefaultSourceBranchName`, das ein E2E-Test bedienen könnte. Rückfrage nötig, bevor UI-Arbeit (Repository-Zuordnungsformular) begonnen wird. |
| 22 | E2E-Tests | E2E: Aufgabenstart mit erfolgreicher Basis-Branch-Validierung (Happy Path) | Blockiert | Setzt UI-Eingabefeld aus Punkt 21 voraus (siehe dort) |
| 23 | E2E-Tests | E2E: Aufgabenstart mit Basis-Branch-Validierungsfehler (Branch nicht existiert) | Blockiert | Setzt UI-Eingabefeld aus Punkt 21 voraus (siehe dort) |
| 24 | E2E-Tests | E2E: PR-Erstellung mit konfiguriertem Basis-Branch als Ziel | Blockiert | Setzt UI-Eingabefeld aus Punkt 21 voraus (siehe dort) |
