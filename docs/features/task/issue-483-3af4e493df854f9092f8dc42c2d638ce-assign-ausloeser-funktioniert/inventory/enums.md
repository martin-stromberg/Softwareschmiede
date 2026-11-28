# Enums und Wertelisten: Assign-Auslöser

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 13)

| Wert | Bedeutung |
|------|-----------|
| `Description` | Referenzbeschreibung (Issue/PR-Body) — die assign-relevante Quellart; vom plugin-seitigen Marker-Vorfilter aktuell ebenfalls erfasst. |
| `IssueComment` | Issue-/PR-Unterhaltungskommentar. |
| `ReviewComment` | PR-Zeilenkommentar. |
| `PullRequestReview` | Veröffentlichtes PR-Review. |

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 10)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz. |
| `PullRequest` | PR-Referenz (Assign-Pfad über denselben `issues/{n}/events`-Endpunkt). |

## `ScmResponseKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 66)

| Wert | Bedeutung |
|------|-----------|
| `Comment` | Kommentar-Antwort (mit `Marker`). |
| `Reaction` | Reaktion auf Kommentar/Review. |
| `PullRequest` | PR-Anlage. |
| `IssueBodyUpdate` | Ersetzt die komplette Issue-Beschreibung remote (Titel unverändert) **mit** `Marker` — produziert genau die aufbereiteten Beschreibungen, die der Assign-Auslöser lesen soll. Neuer Enum-Wert am Ende angehängt (numerische Persistenz). |

## `ScmAccountMode`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmAccountMode.cs` (Z. 8–15)

| Wert | Bedeutung |
|------|-----------|
| `UserAccount` (= 0) | Überwachung/Antworten unter dem Benutzer-Token; Keyword-Trigger, keine Inbox — Assign-Auslöser nicht wirksam. |
| `SeparateAccount` (= 1) | Überwachung/Antworten unter dem Token des separaten Kontos; Notifications-Inbox mit `mention`-/`assign`-Auslösern. |

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Z. 15–23)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden/angelegt. |
| `Ambiguous` | Mehrere Kandidaten ⇒ `AmbiguousAssignmentDiagnostic`. |
| `Unassigned` | Keine Zuordnung möglich ⇒ `UnassignedDiagnostic`. |

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 110)

| Wert | Bedeutung |
|------|-----------|
| `Authentication` | Token ungültig/fehlt (z. B. fehlender AgentToken). |
| `Permission` | Zugriffsrecht fehlt. |
| `RateLimit` | Kontingent erschöpft (Backoff mit `RetryAfterUtc`). |
| `Temporary` | Vorübergehender Fehler. |
| `Permanent` | Dauerhafter Fehler (u. a. `repository: null`/`NOT_FOUND`, unleserliche Referenz). |

## `ScmIncomingEventStatus` (Konstantenklasse, kein Enum)
Datei: `src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Aufgenommen` | Neu registriert, noch nicht zugestellt. |
| `Verzögert` | Ruht bis `NextAttemptUtc` (Verzögerungsdirektive). |
| `Wartend` | Wartet auf Bedingung/Korrektur (`Reason` erklärt). |
| `ZustellungVorbereitet` | Versuchskanal angelegt, Prompt nicht geschrieben. |
| `Zugestellt` | Prompt geschrieben, Annahme offen. |
| `ZustellungUngewiss` | Schreibversuch unbestätigt; kein automatischer Neuversand. |
| `InBearbeitung` | Annahme korreliert, Abschluss offen. |
| `VerarbeitungUngeklärt` | Terminaler Diagnose-Status; nur manuell klärbar. |
| `Verarbeitet` | Final: Annahme + Abschluss bestätigt. |
| `Abgebrochen` | Final: Benutzerabbruch. |

## Weitere relevante Konstanten/Grundstrings (keine Enums)

| Konstante | Ort | Wert / Zweck |
|-----------|-----|--------------|
| `ScmNotification.MentionReason` | `ScmEvents.cs` Z. 96 | `"mention"` — Texttrigger-Inbox-Auslöser. |
| `ScmNotification.AssignReason` | `ScmEvents.cs` Z. 101 | `"assign"` — Zuweisungs-Inbox-Auslöser. |
| `ScmResponseRequest.Marker` | `ScmEvents.cs` Z. 84 | `<!-- softwareschmiede:action:{ActionId} -->`. |
| `ScmEventValidationService.TrustedAutomationLogins` | `ScmEventValidationService.cs` Z. 37 | `github-actions[bot]`, `app/github-actions` (case-insensitive). |
| Ablehnungs-/Diagnosekonstanten | `ScmEventValidationService.cs` Z. 15–31, `ScmEventStore.cs` Z. 22–34 | `NoMentionRejection`, `AlreadyReactedRejection`, `AssignEmptyDescriptionRejection`, `AssignerUnknownRejection`, `AssignerNotAllowedRejection`, `AmbiguousAssignmentDiagnostic`, `UnassignedDiagnostic`, `EmptyDescriptionBatchDiagnostic`, `InboxDiscardDiagnosticPrefix`. |
