# Datenmodell — Bestandsaufnahme

## `ScmEventRepository`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 15–17)

Vom Provider geladene Repository- und Authentifizierungsdaten, ohne Credentials (`record`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProviderPrefix` | `string` | Plugin-Präfix des Quell-Providers (GitHub: `Softwareschmiede.GitHub`); geeignet für eine Provider-Bindung der `github-actions`-Akzeptanz |
| `Host` | `string` | SCM-Host (z. B. `github.com`, Enterprise-Hosts); geeignet für Host-Bindung |
| `RepositoryId` | `string` | Remote-Repository-Kennung (GitHub: `node_id`) |
| `Owner` | `string` | Kanonischer Owner-Login; Vergleichsziel der Akteursprüfung |
| `Name` | `string` | Repository-Name |
| `OwnerId` | `string?` | Node-ID des Owners; primäres Vergleichsziel von `SameIdentity` |
| `ViewerId` | `string` | Node-ID des authentifizierten Ereignis-Kontos |
| `ViewerLogin` | `string` | Login des authentifizierten Ereignis-Kontos (im Modus `SeparatesKonto` = Bot-Identität, Trigger ist `@`-Erwähnung dieses Logins) |
| `CanWrite` | `bool` | Schreibberechtigung des Tokens |
| `Konto` | `ScmKontoModus` | Kontomodus der Beobachtung (`Benutzerkonto` Standard, `SeparatesKonto`) |

## `ScmEventObservation`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 30–52)

Text und Identität einer gemeinsamen Remoteversion (`record`); Eingangswert von `GetRejection`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Reference` | `ScmEventReference` | Ursprung (Repository + Referenzart + Nummer) |
| `ContentKind` | `ScmContentKind` | Art des Texts (`Description`, `IssueComment`, `ReviewComment`, `PullRequestReview`) |
| `RemoteNodeId` | `string` | Remote-Knoten-ID (Bestandteil von `ContentKey`) |
| `RemoteCommentId` | `string?` | Numerische Remote-Kommentar-ID |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | Remote-Zeitpunkte |
| `LastEditedAt` | `DateTimeOffset?` | Bearbeitungszeitpunkt; steuert, ob `Editor*` oder `Author*` der geprüfte Akteur ist (`GetRejection` Z. 51–53) |
| `AuthorId` / `AuthorLogin` | `string?` | Identität des Autors — für `github-actions`-Texte trägt GitHub hier die Bot-Identität (`github-actions[bot]`) |
| `EditorId` / `EditorLogin` | `string?` | Identität des letzten Bearbeiters |
| `Body` | `string` | Textinhalt (Trigger- und Marker-Prüfung) |
| `Url` | `string` | Remote-URL |
| `ObservedAt` | `DateTimeOffset` | Lokaler Beobachtungszeitpunkt |
| `ViewerReactions` | `IReadOnlyList<string>?` | Reaktionen des lesenden Kontos (Duplikatschutz `BereitsReagiertRejection`) |
| `BodyHash` | `string` (berechnet) | SHA-256 über den UTF-8-Text |
| `ViewerHasReacted` | `bool` (berechnet) | `ViewerReactions` nicht leer |
| `Revision` | `DateTimeOffset` (berechnet) | `LastEditedAt ?? CreatedAt` |
| `ContentKey` | `string` (berechnet) | Stabiler Textschlüssel inkl. `ProviderPrefix`/`Host`/`RepositoryId` |
| `EventKey` | `string` (berechnet) | Deduplizierungsschlüssel `ContentKey/Revision/BodyHash` |

## `ScmEventReference`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 20)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Repository` | `ScmEventRepository` | Ereignisursprung samt Identitäten und Kontomodus |
| `Kind` | `ScmReferenceKind` | `Issue` oder `PullRequest` |
| `Number` | `int` | Remote-Referenznummer |

## `ScmContentSnapshot` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/ScmContentSnapshot.cs`

Persistierter Erkennungsstand eines Texts; nimmt die Ablehnungsdiagnose auf.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ContentKey` | `string` | Primärschlüssel, identisch zu `ScmEventObservation.ContentKey` |
| `BodyHash` | `string` | Zuletzt gesehener Inhaltshash |
| `Revision` | `string` | Zuletzt gesehene Revision (ISO-Format) |
| `IdentityResolved` | `bool` | `false` nur bei Diagnose „Bearbeiter nicht bestimmbar" (`ScmEventStore.AcceptBatchAsync` Z. 63); steuert erneutes Einlesen ohne `since`-Grenze (`ScmEventMonitoringService` Z. 179–182) |
| `Diagnostic` | `string?` | Gespeicherte Ablehnungs-/Zuordnungsdiagnose (z. B. „Textversion stammt nicht vom Repository-Owner") |

**Feststellung:** Kein Datenmodellfeld fehlt für die Anforderung — `github-actions`-Autorenschaft ist bereits als `AuthorLogin`/`EditorLogin` repräsentierbar; die Akteursentscheidung ist reine Validierungslogik.
