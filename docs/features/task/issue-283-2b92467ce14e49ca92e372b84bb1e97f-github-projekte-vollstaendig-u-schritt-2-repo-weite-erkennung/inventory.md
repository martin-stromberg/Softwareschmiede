# Bestandsaufnahme: Repository-weite GitHub-Ereigniserkennung (Schritt 2)

Analyse des SCM-Ereignis-Monitorings bezogen auf die Anforderung, die Beobachtungsliste repository-weit aus aktivierten, projekt-erfassten `GitRepository`-Datensätzen aufzubauen. Baut auf der Projekt-Bestandsaufnahme `docs/projects/task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u/inventory.md` auf; die hier genannten Befunde wurden im aktuellen Code (Schritt-Branch inkl. Schritt-1-Merge) verifiziert.

## Zusammenfassung

- `ScmEventMonitoringService.RunOnceAsync` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs:25-36`) lädt ausschließlich `Aufgaben` mit `GitRepository.Aktiv == true` und sammelt daraus `IssueReferenz.IssueNummer` und `PullRequests`-Einträge. Es gibt keinen Enumerationspfad für ein ganzes Repository.
- `IScmEventSource` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`) kennt nur `GetEventRepositoryAsync` und `ReadEventsAsync` — letzteres arbeitet auf einer `ScmEventReference` mit fester `Number`.
- `GitHubPlugin` ist die einzige `IScmEventSource`-Implementierung; Bitbucket/LocalDirectory werden über `plugin is not IScmEventSource` übersprungen (`ScmEventMonitoringService.cs:39-40`).
- Die Auflösung der lokalen Aliase (`RepositoryUrl`) in kanonische `ScmEventRepository`-Identitäten inkl. Checkpoint-Sperre (`repository/{prefix}/{alias}`) existiert bereits pro Repo-Gruppe und ist für repo-weite Quellen wiederverwendbar (`ScmEventMonitoringService.cs:45-78`).
- `ScmEventStore.AcceptBatchAsync` behandelt `aufgabeId == null` bereits kontrolliert: kein Inbox-Eintrag, Snapshot mit Diagnostic „Mehrdeutige Zuordnung zu Aufgaben", `IdentityResolved = false` → Folgepolls lesen ohne `SinceUtc` (Vollscan) und wiederholen die Auflösung (`ScmEventStore.cs:46-49,76-77`, `ScmEventMonitoringService.cs:102-104`). Das ist der bestehende Pfad für „nicht zuordenbar", den die Zwischenstufe nutzt.
- `GitRepository` trägt `ProjektId`, `PluginTyp`, `RepositoryUrl`, `Aktiv` (`src/Softwareschmiede/Domain/Entities/GitRepository.cs`); DbSet `db.GitRepositories` (`SoftwareschmiededDbContext.cs:28`). Ein Repository ist „als Projekt erfasst" über `ProjektId`-FK.
- In `GitHubPlugin.Events.cs` existieren GraphQL-Pagination (`NextCursor`, `pageInfo`), das `TextFields`-Feldset (id/body/createdAt/updatedAt/lastEditedAt/url/author/editor), `AddObservation` mit Watermark- und Eigen-Marker-Filterung sowie `RestPagesAsync` mit `ValidateNextPage`-Hostvalidierung — Bausteine für eine repo-weite Abfrage.
- Fehlt: Vertrag und Implementierung zum Enumerieren aller Referenzen eines Repos; Aufruf im Monitor; Tests.

## Details

- [Überwachung, Checkpoints und Inbox-Aufnahme](inventory/monitoring.md) — `ScmEventMonitoringService`, `ScmEventStore`, `ScmEventValidationService`, `ScmObservationCheckpoint`/`ScmContentSnapshot`-Mechanik.
- [Plugin-Verträge und GitHub-Implementierung](inventory/plugin-contracts.md) — `IScmEventSource`, `ScmEvents`-Value-Objects, `GitHubPlugin.Events.cs`-Lesepfade, Fehlertypisierung.
- [Repository-/Aufgaben-Datenmodell](inventory/repositories-tasks.md) — `GitRepository`, `Aufgabe`-Referenzen, Zuordnungslogik.
- [Tests und Testinfrastruktur](inventory/tests.md) — `GitHubEventSourceTests`/`FakeApi`, `ScmDispatchFixture`, relevante Testkonventionen.
