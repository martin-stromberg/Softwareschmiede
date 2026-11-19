# Enums

## `ScmKontoModus`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs`

Kontomodus der SCM-Ereignisüberwachung; wählt zugleich das zugehörige Credential. Gilt
ausschließlich für Ereignispfade (Beobachtung, Zustellung, Ereignis-Antworten) —
Nicht-Ereignis-APIs laufen weiter unter dem Benutzerkonto. Persistiert als Zahl unter
`scm.monitoring.accountmode` (`0`/`1`).

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto = 0` | Überwachung und Antworten laufen unter dem Benutzer-Token (bisheriges Verhalten). |
| `SeparatesKonto = 1` | Überwachung und Antworten laufen unter dem Token eines separaten Kontos (Notifications-Inbox ersetzt die Enumeration). |

## `ScmReferenceKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:9`

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz; der neue Zyklus betrachtet nur diesen Wert. |
| `PullRequest` | Pull-Request-Referenz (Backmerge-Pfad). |

## `ScmContentKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:12`

Getrennte ID-Namensräume der beobachteten Texte.

| Wert | Bedeutung |
|------|-----------|
| `Description` | Beschreibungstext; die „Issue erstellt“-Erkennung wertet ausschließlich diese Art aus. |
| `IssueComment` | Issue-/PR-Kommentare. |
| `ReviewComment` | Review-Kommentare (nur PR). |
| `PullRequestReview` | PR-Reviews (nur PR). |

## `ScmProviderFailureKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:92`

Fehlerklasse für gezielte Sperren und zeitgesteuerte Wiederholungen
(`ScmCheckpointSupport.ApplyFailure`).

| Wert | Bedeutung |
|------|-----------|
| `Authentication` | Dauerhafte Sperre (`NextAttemptUtc = long.MaxValue`). |
| `Permission` | Dauerhafte Sperre. |
| `RateLimit` | Backoff, ggf. mit `RetryAfterUtc`. |
| `Temporary` | Backoff. |
| `Permanent` | Ohne `retryPermanent` dauerhaft; mit `retryPermanent` (Alias-Auflösungen) nur Backoff. |

## `ScmResponseKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:58`

Vom Antwortkanal erlaubte Remoteaktionen; neue Werte werden nur am Ende angehängt
(JSON-Kompatibilität der Outbox).

| Wert | Bedeutung |
|------|-----------|
| `Comment` | Kommentar schreiben. |
| `Reaction` | Reaktion setzen. |
| `PullRequest` | Pull Request anlegen. |
| `IssueBodyUpdate` | Ersetzt ausschließlich die Issue-Beschreibung (Titel bleibt unberührt); Ergebnis des Aufbereitungspfads. |

## `ScmBackmergeModus`

Datei: `src/Softwareschmiede/Domain/Enums/ScmBackmergeModus.cs`

Aktivierungsmodus des Backmerge-Prüfzyklus — Vorbild für die Schalter-Prüfung des neuen
Dienstes (`Deaktiviert` ⇒ Durchlauf übersprungen).

| Wert | Bedeutung |
|------|-----------|
| `Deaktiviert = 0` | Keine automatischen Backmerges (Standard). |
| `Global = 1` | Backmerges in allen Projekt-Repositories. |
| `Projektbezogen = 2` | Nur Projekte mit aktivierter Projektoption. |
