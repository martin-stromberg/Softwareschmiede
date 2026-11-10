# Enums

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Zeilen 13–21)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe wurde gefunden oder angelegt. |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz → `ScmEventStore.AmbiguousAssignmentDiagnostic`. |
| `Unassigned` | Keine Aufgabe zuordenbar und keine anlegbar → `ScmEventStore.UnassignedDiagnostic`. |

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Erstellt, wartet auf Bearbeitung (Dispatch aktiviert automatisch zu `Gestartet`). |
| `Gestartet` | Branch erstellt, CLI läuft/sollte laufen. |
| `Wartend` | Rate-Limit; wartet auf Wiederaufnahme. |
| `Beendet` | Endstatus (Start gesperrt via `BlockReason`). |
| `Archiviert` | Nicht mehr aktiv (Start gesperrt via `BlockReason`). |

## `AufgabeStatusExtensions` (Hilfs-Enum-Logik)
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`

| Member | Bedeutung |
|--------|-----------|
| `AktivOderWartendStatus` | `[Gestartet, Wartend]` — **eine** bestehende „aktiv"-Definition (ohne `Neu`); EF-Variante `IstAktivOderWartendPredicate` in `AufgabeService`. |
| `IstAktivOderWartend()` | Extension; `true` für `Gestartet`/`Wartend`. |

Gegenstück: `ScmEventOrchestrator.BlockReason` wertet `Neu`/`Gestartet`/`Wartend` (+ pausiert) als nicht gesperrt — `Status ∉ {Beendet, Archiviert}` ist die **andere** bestehende Aktivdefinition.

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | KI-Ausführung noch nicht gestartet. |
| `Aktiv` | Läuft oder nach App-Neustart wiederherzustellen. |
| `Beendet` | Beendet; Neustart nur explizit. |

## `AufgabeLaufStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | CLI aktiv (I/O jüngst). |
| `WartetAufEingabe` | CLI lebt, wartet vermutlich auf Eingabe — der PR-Check-Wartefall der Anforderung. |

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 9)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz (Dispatch `ResolveIssueAsync`). |
| `PullRequest` | Pull-Request-Referenz (Dispatch `ResolvePullRequestAsync`). |

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 12)

| Wert | Bedeutung |
|------|-----------|
| `Description` | Titel-/Beschreibungstext. |
| `IssueComment` | Kommentar (Issue und PR; PR nutzt denselben Kind). |
| `ReviewComment` | PR-Review-Kommentar (nur `PullRequest`). |
| `PullRequestReview` | PR-Review (nur `PullRequest`). |

## `PullRequestReferenzRolle`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestReferenzRolle.cs`

| Wert | Bedeutung |
|------|-----------|
| `CreatedByTask = 0` | PR wurde als Ergebnis der Aufgabe erstellt (hostseitig via `SaveCreatedAsync` oder gemeldet via `SaveReportedAsync`) — Default. |
| `ReviewSource = 1` | Aufgabe wurde zur Bearbeitung dieses PRs importiert (von `CreateFromPullRequestAsync` gesetzt — die Doppelaufgaben-Rolle). |

## `PullRequestProvider`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestProvider.cs`

| Wert | Bedeutung |
|------|-----------|
| `GitHub = 0` | Einziger Provider mit `IScmEventSource`-Ereignisquelle im Befund (`SaveReportedAsync` fällt auf GitHub zurück). |
| `BitbucketCloud = 1` | Vorhanden. |
| `BitbucketServerDataCenter = 2` | Vorhanden (abweichende `Normalize`-Kasusregel). |

## `PullRequestStatus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unknown`, `Open`, `Closed`, `Merged` | Remote-Status; `Closed`/`Merged` steuern `DetermineClosureAsync`. |

## `ScmPromptReadyStatus`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Zeilen 9–21)

| Wert | Bedeutung |
|------|-----------|
| `Unknown`, `Ready`, `Busy`, `TrustPrompt`, `Exited` | Eingabebereitschaft der CLI; `Busy` → „CLI beschäftigt; wartet auf Eingabebereitschaft." |

## `ScmKontoModus` / `ScmProviderFailureKind` / `ScmResponseKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` bzw. `Domain/Enums/`

Randrelevant: `ScmKontoModus` (`Benutzerkonto`/`SeparatesKonto` — ändert den Enumerationspfad), `ScmProviderFailureKind` (Checkpoint-Backoff/Sperren), `ScmResponseKind` (Comment/Reaction/PullRequest-Aktionen).
