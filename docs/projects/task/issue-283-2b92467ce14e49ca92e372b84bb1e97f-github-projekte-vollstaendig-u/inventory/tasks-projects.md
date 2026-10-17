# Projekte, Repositories und Aufgaben

Zurück: [../inventory.md](../inventory.md)

## 1. Datenmodell und Beziehungen

| Entität | Datei | Relevante Felder |
|---------|-------|------------------|
| `Projekt` | `src/Softwareschmiede/Domain/Entities/Projekt.cs` | `Id`, `Name`, `Status` (`ProjektStatus`, u. a. `Aktiv`), `Repositories`, `Aufgaben` |
| `GitRepository` | `src/Softwareschmiede/Domain/Entities/GitRepository.cs` | `ProjektId`, `PluginTyp` (z. B. `Softwareschmiede.GitHub`), `RepositoryUrl`, `RepositoryName`, **`Aktiv` (default true, Z. 22)**, `DefaultSourceBranchName`, `StartKonfiguration`, `InitialisierungKonfiguration` |
| `Aufgabe` | `src/Softwareschmiede/Domain/Entities/Aufgabe.cs` | `ProjektId`, `GitRepositoryId`, `Titel`, `AnforderungsBeschreibung` (= Prompt-Quelle), `Status` (`AufgabeStatus`), `LokalerKlonPfad`, `BranchName`, `BasisBranchName`, `KiPluginPrefix`, `PausiertBisUtc`, `IssueReferenz`, `PullRequests`, `AlertReferenz` |
| `IssueReferenz` | `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs` | `AufgabeId`, `IssueNummer`, `Titel`, `Body`, `LabelsJson`, `IssueUrl` |
| `PullRequestReferenz` | `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs` | `AufgabeId`, `Provider`, `Rolle` (`CreatedByTask`/`ReviewSource`), `RepositoryId`, `PullRequestNumber`, `SourceRef`/`SourceBranch`, `TargetBranch`, `MonitoringPhase` |

Beziehungskette: `Projekt` 1—n `GitRepository` 1—n `Aufgabe` 1—{0..1 `IssueReferenz`, n `PullRequestReferenz`}.

**„Als Projekt erfasst und aktiviert"** (requirement-Sprache) = `GitRepository`-Datensatz mit `Aktiv == true` an einem `Projekt`. Die Erfassung läuft über `ProjektService.AddRepositoryAsync` (`src/Softwareschmiede/Application/Services/ProjektService.cs:133-197`): `Aktiv = true` wird beim Anlegen gesetzt (Z. 189); `GetRepositoryLinkFields()` des SCM-Plugins (`GitHubPlugin.cs:79-95`) liefern die Dialogfelder (RepositoryUrl + RepositoryName `owner/repo`). Ein UI-/Servicepfad zum nachträglichen De-/Aktivieren wurde nicht gefunden (Feld existiert, wird u. a. vom Monitor und von `CreateFromPullRequestAsync` geprüft). `ProjektService.GetUnassignedRepositoriesAsync` (Z. 210+) listet vom Provider angebotene, noch nicht verknüpfte Repos.

`AufgabeStatus` (`src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`): `Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert` (+ weitere, siehe Enum). `PausiertBisUtc` blockiert Starts unabhängig vom Status (`Aufgabe.cs:97-103`).

## 2. Aufgabenanlage (heute ausschließlich UI-gesteuert)

`AufgabeService` (`src/Softwareschmiede/Application/Services/AufgabeService.cs`):

- `CreateAsync` (Z. 181-207): generische Anlage (ProjektId, Titel, Beschreibung, optional GitRepositoryId).
- `CreateFromIssueAsync` (Z. 210-249): Aufgabe + `IssueReferenz` in einem Save; **keine Duplikatprüfung** — die liegt im Aufrufer.
- `CreateFromPullRequestAsync` (Z. 252-355): validiert Repo-Kontext (Repository muss existieren, aktiv, PluginTyp passend, Z. 275-283) und prüft Duplikate über `PullRequestReferenzen` (Provider+RepositoryId+Number, Z. 285-292) inkl. `DbUpdateException`-Race-Absicherung (Z. 335-352); legt `PullRequestReferenz` mit `Rolle=ReviewSource` an; `BuildPullRequestAnforderungsBeschreibung`/`CleanPullRequestBody` (Z. 357-395) bereinigt HTML/Kommentarmarker aus dem PR-Body.
- `CreateFromAlertAsync` (Z. 398+): Code-Scanning-Alert-Pfad.
- `IsPullRequestLinkedAsync` (Z. 120+), `TryAssignIssueReferenzIfNoneAsync` (Z. 556+), `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync` (Z. 564+) — letztere wird beim Task-Start genutzt, um Issue-Metadaten nachträglich zu verknüpfen (`TaskDetailViewModel.cs:1475`).

Aufrufer/UI: `ProjectDetailViewModel` lädt „Offene Anforderungen" (Issues, PRs, Alerts) pro ausgewähltem Repository und dedupliziert anzeigeseitig: bereits verknüpfte Issue-Nummern per `HashSet` (`ProjectDetailViewModel.cs:882-884`), PRs per `IsPullRequestLinkedAsync`, Alerts per `AlertReferenz.SourceKey`. Anlage nur nach `BestaetigenDialog` (`ProjectDetailViewModel.cs:984`, `1025`).

**Lücke für FR-3/FR-4:** Kein servicenaher Pfad „finde oder lege Aufgabe für SCM-Referenz X in Repository Y". Für die Event-getriebene Anlage muss (a) die passende Aufgabe über `IssueReferenz.IssueNummer`/`PullRequestReferenz`-Schlüssel gesucht und (b) bei Fehlschlag angelegt werden — inkl. Titel/Beschreibung aus den Observationsdaten (offene Frage Q2 in requirement.md) und `ProjektId` aus `GitRepository.ProjektId`.

## 3. Aufgabenstart und CLI-Start

`EntwicklungsprozessService` (`src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`) — alle öffentlichen Methoden laufen über `ScmTaskCoordinator.Shared.RunAsync(aufgabeId, …)` (Task-Semaphore, `ScmTaskCoordinator.cs`):

- `ProzessStartenAsync` (Z. 89-110): Klon + Branch, Status→Gestartet. Guard `WirfWennPausiert`; frühe Rückkehr wenn `LokalerKlonPfad` gesetzt.
- `ProzessStartenUndCliStartenAsync` (Z. 124-203): Klon + Branch + CLI-Start in einem Schritt; bei Race („zwischenzeitlich geklont") Fallback auf `CliNeustartenAsync` (Z. 153-157); Rollback (`RollbackStartAsync`) bei Fehler.
- `CliNeustartenAsync` (Z. 233-294): startet CLI im vorhandenen `LokalerKlonPfad`; kehrt stillschweigend zurück, wenn bereits eine lebende Session existiert (Z. 261-266); aktualisiert `aufgabe.KiPluginPrefix` auf das gewählte Plugin (Z. 276-281).
- `ProzessStartenCoreAsync` (Z. 205-230): `ResolveRepositoryAsync` → `ResolvePluginAsync` (SCM-Plugin via `PluginSelectionService`) → Basis-Branch validieren → `PrepareCloneDirectoryAsync` (Klonpfad = `<Arbeitsverzeichnis>/softwareschmiede/<aufgabeId>`, Z. 524-540, `KlonBasisVerzeichnis` Z. 40) → `SetupBranchAsync` → `FinalizeStartAsync`.
- Beide CLI-Startpfade enden in `KiAusfuehrungsService.StartTerminalSessionAsync` (Z. 188-189, 286-293).
- `strictPluginSelection` + `optionalParameters` sind SCM-spezifische Erweiterungen (verhindern Ersatzplugins; Simulator-Parameter `--scm-exchange`).

Wichtig für den Auto-Start (FR-5): Der Orchestrator ruft bereits `tasks.SetStatusAsync(id, AufgabeStatus.Gestartet)` (`ScmEventOrchestrator.cs:53,132,160`) und danach die Entwicklungsprozess-Methoden auf — derselbe Pfad ist für neu angelegte Aufgaben wiederverwendbar.

## 4. Standard-CLI-Auflösung

`PluginSelectionService` (`src/Softwareschmiede/Application/Services/PluginSelectionService.cs`):

- `ResolveDevelopmentAutomationPluginWithProjectScopeAsync(aufgabenPluginPrefix, projektId)` (Z. 101-124): Aufgaben-Prefix → Projekt-Default (`PluginDefaultSettingsService.GetProjectDefaultPluginPrefixAsync`) → globaler Default → `null` (dann „Keine Standard-CLI konfiguriert.").
- `ResolveDevelopmentAutomationPluginStrictAsync` (Z. 88-94): nur aktivierte Plugins, sonst `InvalidOperationException` „…nicht mehr installiert oder aktiviert."
- `GetAvailableKiPluginPrefixesAsync` (Z. 62-70) für den Auswahldialog; `SaveProjectDefaultPluginPrefixAsync` (Z. 43-44) — der Queue-Dialog kann die Auswahl als Projekt-Default speichern (`ScmEventQueueViewModel.cs:242-243`).
- `ResolveDevelopmentAutomationPluginAsync` (Z. 73-85): mit Fallback-Kette; `strictPluginSelection` umgeht sie.

Standard-CLI-Speicherorte: `PluginDefaultSettingsService` (globaler Key `AppEinstellungService.DefaultKiPluginKey`, siehe `ScmE2EFixture.cs:37`; projektbezogene Defaults in derselben Tabelle).

## 5. Referenzen an Aufgaben — wie sie entstehen

- `IssueReferenz`: bei `CreateFromIssueAsync`, bei Issue-Dialogen oder beim Start über `TryAssignIssueReferenz*`.
- `PullRequestReferenz` `Rolle=CreatedByTask`: bei `PullRequestErstellenAsync` (`EntwicklungsprozessService.cs:384-414`); `Rolle=ReviewSource`: bei `CreateFromPullRequestAsync`.
- Für „passende Aufgabe wiederverwenden" relevant: ein Issue-Kommentar zu Issue #N gehört zur Aufgabe mit `IssueReferenz.IssueNummer == N` **und** passendem `GitRepository`; PR-Kommentar/Reviews zu PR #N gehören zur Aufgabe mit passender `PullRequestReferenz` — Mehrdeutigkeit wird heute als Fehler-Diagnose behandelt (`ScmEventStore.cs:46,76-77`), nicht aufgelöst.
