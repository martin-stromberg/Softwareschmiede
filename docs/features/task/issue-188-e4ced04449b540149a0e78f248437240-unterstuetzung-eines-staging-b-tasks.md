# Tasks: Unterstützung eines Staging-Branch / Basis-Branch-Konfiguration

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `GitRepository.DefaultSourceBranchName`-Eigenschaft hinzufügen | Offen | — |
| 2 | Datenbank | Migration `AddDefaultSourceBranchNameToGitRepository` erstellen und anwenden | Offen | — |
| 3 | Schnittstellen | `IGitPlugin.CreateBranchAsync()` um Parameter `sourceBranchName: string?` erweitern | Offen | — |
| 4 | Schnittstellen | `IGitPlugin.CreatePullRequestAsync()` um Parameter `baseBranch: string?` erweitern | Offen | — |
| 5 | Plugin (GitHub) | `CreateBranchAsync()` um Logik für Basis-Branch-Nutzung erweitern (`git checkout -b <branch> <remote>/<source>`) | Offen | — |
| 6 | Plugin (GitHub) | `CreatePullRequestAsync()` um GitHub-API-Anpassung für `baseBranch` erweitern | Offen | — |
| 7 | Logik | Validierungsmethode `ValidateBaseBranchExistsAsync()` in `EntwicklungsprozessService` implementieren | Offen | — |
| 8 | Logik | `ProzessStartenAsync()` um Basis-Branch-Validierungsaufruf erweitern | Offen | — |
| 9 | Logik | `SetupBranchAsync()` um Basis-Branch-Handling anpassen (Checkout + Create) | Offen | — |
| 10 | Logik | `GitOrchestrationService.PullRequestErstellenAsync()` um Basis-Branch-Übergabe an Plugin erweitern | Offen | — |
| 11 | Tests | Unit-Test: Aufgabenstart schlägt fehl, wenn Basis-Branch nicht existiert | Offen | — |
| 12 | Tests | Unit-Test: Aufgabenstart erfolgreich mit existierendem Basis-Branch | Offen | — |
| 13 | Tests | Unit-Test: Aufgabenstart erfolgreich ohne Basis-Branch-Konfiguration (Fallback) | Offen | — |
| 14 | Tests | Unit-Test: Feature-Branch wird vom Basis-Branch abgezweigt, wenn konfiguriert | Offen | — |
| 15 | Tests | Unit-Test: Feature-Branch wird vom HEAD abgezweigt, wenn nicht konfiguriert | Offen | — |
| 16 | Tests | Unit-Test: PR-Erstellung übergibt konfigurierten Basis-Branch an Plugin | Offen | — |
| 17 | Tests | Unit-Test: PR-Erstellung übergibt `baseBranch=null`, wenn nicht konfiguriert | Offen | — |
| 18 | Tests | Integrations-Test: `DefaultSourceBranchName` wird in DB gespeichert und kann gelesen werden | Offen | — |
| 19 | Tests | Anpassung bestehender Tests in `EntwicklungsprozessServiceTests` auf neue Logik | Offen | — |
| 20 | Tests | Anpassung bestehender Tests in `GitOrchestrationServiceTests` auf neue Logik | Offen | — |
| 21 | E2E-Tests | E2E: Repository-Zuordnung mit Basis-Branch-Auswahl (Benutzer wählt Basis-Branch, wird gespeichert) | Offen | — |
| 22 | E2E-Tests | E2E: Aufgabenstart mit erfolgreicher Basis-Branch-Validierung (Happy Path) | Offen | — |
| 23 | E2E-Tests | E2E: Aufgabenstart mit Basis-Branch-Validierungsfehler (Branch nicht existiert) | Offen | — |
| 24 | E2E-Tests | E2E: PR-Erstellung mit konfiguriertem Basis-Branch als Ziel | Offen | — |
