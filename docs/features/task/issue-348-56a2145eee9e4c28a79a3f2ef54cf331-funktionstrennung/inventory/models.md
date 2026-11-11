# Datenmodell — Bestandsaufnahme

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Nur die für die Automatisierungsfunktionen relevanten Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel; wird von `ScmIncomingEvent.AufgabeId` referenziert (Kriterium „ereignisgesteuert"). |
| `Status` | `AufgabeStatus` | `Beendet`/`Archiviert` werden von der Abschlussüberwachung ausgeschlossen (`ScmTaskCompletionMonitoringService`, Zeile 47). |
| `GitRepositoryId` / `GitRepository` | `Guid?` / `GitRepository?` | Repository-Verknüpfung; wird für Plugin-Auflösung und Issue-Prüfung benötigt. |
| `IssueReferenz` | `IssueReferenz?` | Verknüpftes Issue; Abschlusskriterium, wenn keine PR-Referenzen existieren. Anforderung: Aufgaben ohne Issue werden künftig nicht automatisch beendet. |
| `PullRequests` | `List<PullRequestReferenz>` | Verknüpfte Pull Requests; haben in `DetermineClosureAsync` Vorrang vor der Issue-Prüfung. |
| `ScmAbschlussErkanntUtc` | `DateTimeOffset?` | Persistierter Startpunkt der Abschluss-Wartezeit; überlebt Neustarts, Countdown bleibt über die Umstellung erhalten (Anforderung). |
| `ScmAbschlussReferenz` | `string?` | Anzeigetext der beobachteten Referenz (z. B. „Pull Request #7", „Issue #3"), Diagnose-/Protokollwert. |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter KI-Ausführungsstatus (Default `NichtGestartet`). |

## `IssueReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel. |
| `AufgabeId` | `Guid` | FK zur Aufgabe. |
| `IssueNummer` | `int?` | Nummer im Provider; `DetermineClosureAsync` prüft nur `> 0`. |
| `Titel` / `Body` / `LabelsJson` / `Milestone` / `IssueUrl` | `string`/`string?` | Anzeige-/Metadaten. |
| `SourceKey` | `string?` | Kanonischer Deduplizierungsschlüssel, nur bei ereignisgesteuerter Anlage gesetzt. |

## `PullRequestReferenz`
Datei: `src/Softwareschmiede/Domain/Entities/PullRequestReferenz.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AufgabeId` | `Guid` | FK zur Aufgabe. |
| `Provider` | `PullRequestProvider` | Provider-Kennung; `PullRequestMonitoringPolicy.CanMonitor` akzeptiert nur `GitHub`. |
| `Rolle` | `PullRequestReferenzRolle` | `CreatedByTask` (Default) / `ReviewSource`. |
| `RepositoryId` | `string` | `owner/repo` beim Provider. |
| `PullRequestNumber` | `int` | PR-Nummer. |
| `SourceBranch` / `TargetBranch` | `string` | Für Backmerge-Muster relevant (head = Hauptbranch, base = `DefaultSourceBranchName`). |
| `Status` / `MergeStatus` | `PullRequestStatus` / `PullRequestMergeStatus` | Von der Abschlussüberwachung aktualisiert (`Merged` = final). |
| `MonitoringPhase` | `PullRequestMonitoringPhase` | Monitoring-Zustand (`Created`/`NotMonitored` u. a.). |
| `LastCheckedUtc`, `NextCheckUtc`, `LastError`, `Url`, `Titel`, `HeadSha`, `MergeCommitSha`, `ProviderPullRequestId`, `SourceRepositoryId`, `SourceRepositoryUrl`, `SourceRef`, `CreatedUtc` | div. | Status-/Diagnosefelder. |

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Lokale Repository-ID (`ScmRepositoryContext.GitRepositoryId`). |
| `ProjektId` | `Guid` | Träger der projektbezogenen Backmerge-Freigabe (`plugins.backmerge.project.{projektId}`). |
| `PluginTyp` | `string` | Plugin-Prefix oder -Name; `MatchesPlugin`-Vergleich in `ScmEventMonitoringService`/`ScmTaskCompletionMonitoringService`. |
| `RepositoryUrl` | `string` | Alias-/Gruppierungsschlüssel der Enumeration. |
| `Aktiv` | `bool` | Nur aktive Repositories werden enumeriert/geprüft. |
| `DefaultSourceBranchName` | `string?` | Konfigurierter Projekt-Basisbranch; Backmerge-Kandidaten brauchen einen vom Hauptbranch abweichenden Wert. |

## `ScmIncomingEvent`
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sequence` | `long` | Fortlaufende Ordnung der Inbox. |
| `AufgabeId` | `Guid` | Kriterium „ereignisgesteuerte Aufgabe" (`ScmIncomingEvents.Any(e => e.AufgabeId == a.Id)`). |
| `Status` | `string` | `"Aufgenommen"`, `"Zugestellt"`, `"Verarbeitet"`, `"Abgebrochen"` u. a.; nicht abgeschlossene Ereignisse blockieren den Abschluss. |
| `ObservationJson`, `EventKey`, `Reason`, `Attempts`, `NextAttemptUtc`, `ErrorCode`, `LeaseOwner`, `LeaseUntilUtc`, `DeliveryAttemptId`, `CliRunId`, `ExchangePath`, `DeliveredUtc`, `AcceptedUtc`, `ExchangeRestrictedWriteSidsJson`, `ReceivedUtc` | div. | Inbox-/Dispatch-Zustand (nicht Bestandteil dieser Anforderung). |

## `ScmObservationCheckpoint`
Datei: `src/Softwareschmiede/Domain/Entities/ScmObservationCheckpoint.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceKey` | `string` | Schlüssel der Quelle; Backmerge: `backmerge/{provider}/{host}/{repoId}/{nummer}`, Enumeration: `enumeration/{provider}/{host}/{repoId}`, Inbox: `notifications/{plugin}/{host}`, Auflösung: `repository/{prefix}/{alias}`. |
| `WatermarkUtc` | `long?` | Letzter erfolgreicher Stand (Unix-ms). |
| `Failures` | `int` | Fehlerzähler für den Backoff (`ScmEventStore.RetryAt`). |
| `NextAttemptUtc` | `long?` | Frühester nächster Versuch; `long.MaxValue` = endgültig entschieden/gesperrt. |
| `ErrorCode` / `Diagnostic` | `string?` | Fehlerkategorie (`ScmProviderFailureKind`-Name, `Merged`, `WaitingForMergeable`, `Blocked`, `Decided` u. a.) und Anzeigediagnose. |

## `AppEinstellung`
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel. |
| `Schluessel` | `string` | Einstellungsschlüssel (alle SCM-Automatisierungs-Einstellungen, siehe `AppEinstellungService`-Konstanten in [logic.md](logic.md)). |
| `Wert` | `string?` | Wert; `null` = Default. |
| `AktualisiertAm` | `DateTimeOffset` | Letzte Änderung. |

## `ScmRepositoryContext`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRepositoryContext.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GitRepositoryId` | `Guid` | ID des lokal persistierten Projekt-Repositories. |
| `PluginPrefix` | `string` | Plugin-Prefix des Providers. |
| `RepositoryId` | `string` | Kanonische API-Repository-ID. |
| `Konto` | `ScmKontoModus` | Kontomodus der Provider-Aufrufe (Default `Benutzerkonto`); heute überall der konfigurierte Modus — für die Backmerge-Entkopplung relevant. |

## `ScmEventRepository` / `ScmEventReference` / `ScmNotification`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`

| Record | Relevante Member | Zweck |
|--------|------------------|-------|
| `ScmEventRepository` | `ProviderPrefix`, `Host`, `RepositoryId`, `Owner`, `Name`, `OwnerId`, `ViewerId`, `ViewerLogin`, `CanWrite`, `Konto` | Kanonisch aufgelöstes Repository; `CanWrite` ist Voraussetzung für den Admin-Merge, `Konto` zeichnet den Auflösungsmodus nach. |
| `ScmEventReference` | `Repository`, `Kind` (`Issue`/`PullRequest`), `Number` | Validierter Ursprung einer Beobachtung; `EnumerateReferencesAsync` liefert die `discovered`-Liste des Backmerge-Dienstes. |
| `ScmNotification` | `ThreadId`, `RepositoryNodeId`, `Host`, `Owner`, `Name`, `Kind`, `Number`, `Reason` (`MentionReason = "mention"`), `UpdatedAt` | Roher Inbox-Eintrag des separaten Kontos; treibt den `SeparatesKonto`-Pfad, der die Enumeration ersetzt. |
