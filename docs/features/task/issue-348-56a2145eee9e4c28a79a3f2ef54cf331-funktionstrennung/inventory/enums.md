# Enums — Bestandsaufnahme

## `ScmBackmergeModus`
Datei: `src/Softwareschmiede/Domain/Enums/ScmBackmergeModus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Deaktiviert = 0` | Backmerge-PRs werden nicht automatisch gemergt. |
| `Global = 1` | In allen Projekt-Repositories automatisch mergen. |
| `Projektbezogen = 2` | Nur in Projekten mit aktivierter Projektoption (`plugins.backmerge.project.{projektId}`). |

Persistiert unter `scm.backmerge.mode` (int). Fallback bei fehlendem/ungültigem Wert: `Deaktiviert`.

## `ScmKontoModus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto = 0` | Überwachung/Antworten unter dem Benutzer-Token; repository-weite Enumeration aktiv. |
| `SeparatesKonto = 1` | Überwachung/Antworten unter dem separaten Token; Notifications-Inbox ersetzt die Enumeration (→ heute keine Backmerge-Entdeckung). |

Persistiert unter `scm.monitoring.accountmode` (int). Fallback: `Benutzerkonto`. Hinweis im Enum-`remarks`: der Modus gilt bewusst nur für Ereignispfade; Nicht-Ereignis-APIs laufen immer unter dem Benutzerkonto („Token-Grenze") — deckt sich mit der Anforderung, die Backmerge-Prüfung immer unter `Benutzerkonto` laufen zu lassen.

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu`, `Gestartet`, `Wartend` | Kandidaten der Abschlussüberwachung (heute: nur mit `ScmIncomingEvent`). |
| `Beendet`, `Archiviert` | Von der Kandidatenauswahl ausgeschlossen. |

## `PullRequestStatus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unknown` | Unbekannt. |
| `Open` | Offen — blockiert Abschluss; Backmerge-Kandidat. |
| `Closed` | Geschlossen — zählt als Abschlusskriterium (kann wieder öffnen, wird weiter geprüft). |
| `Merged` | Gemergt — final; löst Abschluss aus; bei Backmerge-Bewertung finaler Zustand. |

## `PullRequestMergeStatus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestMergeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unknown`, `Blocked`, `Conflicting` | Nicht mergebar → `WaitingForMergeable`-Retry. |
| `Mergeable` | Backmerge-PR wird per Admin-Merge bestätigt. |
| `Merged` | Bereits gemergt. |

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 9)

| Wert | Bedeutung |
|------|-----------|
| `Issue`, `PullRequest` | Referenzart; Backmerge wertet ausschließlich `PullRequest` aus. |

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 12)

| Wert | Bedeutung |
|------|-----------|
| `Description`, `IssueComment`, `ReviewComment`, `PullRequestReview` | Getrennte ID-Namensräume der beobachteten Texte. |

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 77)

| Wert | Bedeutung |
|------|-----------|
| `Authentication`, `Permission`, `Permanent` | Blockieren dauerhaft (`NextAttemptUtc = long.MaxValue`; `Permanent` bei Repository-Auflösung wird dennoch per Backoff wiederholt). `Permission`/`Permanent` gelten im `SeparatesKonto` als „Repository nicht erreichbar" (`IsInaccessible`). |
| `RateLimit`, `Temporary` | Backoff-Wiederholung (`ScmEventStore.RetryAt`). |

## `PullRequestReferenzRolle` / `PullRequestMonitoringPhase` / `PullRequestProvider`
Dateien: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/` (`PullRequestMonitoringPhase.cs` etc.)

| Enum | Relevante Werte | Zweck |
|------|-----------------|-------|
| `PullRequestReferenzRolle` | `CreatedByTask`, `ReviewSource` | Rolle der PR-Verknüpfung; Eingang in `PullRequestMonitoringPolicy.CanMonitor`. |
| `PullRequestMonitoringPhase` | `Created`, `NotMonitored` u. a. | Monitoring-Phase der PR-Referenz. |
| `PullRequestProvider` | `GitHub` u. a. | `CanMonitor`/`CanAutoComplete` akzeptieren nur `GitHub`. |

## UI-Optionsklassen (Enum ↔ Anzeige-Label)

### `ScmBackmergeModusTexte` / `ScmBackmergeModusOption`
Datei: `src/Softwareschmiede.App/ViewModels/ScmBackmergeModusOptionen.cs`

- Textkonstanten: `Deaktiviert = "Deaktiviert"`, `Global = "Global aktiviert"`, `Projektbezogen = "Projektbezogen aktivierbar"`.
- `record ScmBackmergeModusOption(ScmBackmergeModus Modus, string Label)` mit statischer Liste `Alle` (3 Einträge) und `FuerModus(modus)`; `ToString()` → `Label`. **Muster für die geforderte neue Optionsklasse des Abschluss-Prüfmodus.**

### `ScmKontoModusTexte` / `ScmKontoModusOption`
Datei: `src/Softwareschmiede.App/ViewModels/ScmKontoModusOptionen.cs`

- Textkonstanten: `Benutzerkonto = "Benutzerkonto"`, `SeparatesKonto = "Separates Konto"`.
- `record ScmKontoModusOption(ScmKontoModus Modus, string Label)` mit `Alle` (2 Einträge) und `FuerModus(modus)`.

**Es existiert noch kein Enum/Optionspaar für den Abschluss-Prüfmodus** („Nur ereignisgesteuerte Aufgaben" / „Alle Aufgaben") — die Anforderung sieht einen neuen Enum z. B. `ScmTaskCompletionPruefmodus` in `src/Softwareschmiede/Domain/Enums/` plus Optionsklasse nach diesem Muster vor.
