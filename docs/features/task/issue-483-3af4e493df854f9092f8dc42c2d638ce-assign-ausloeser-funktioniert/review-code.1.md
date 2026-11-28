# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmEventStore.cs (ScmEventStore)

- **Fehlermeldungen/Kommentar-Genauigkeit (niedrig)** — Der XML-Kommentar von `EmptyDescriptionBatchDiagnostic` (Z. 25–27) und der erläuterte Kommentar vor `checkpoint.Diagnostic` (Z. 143–145) nennen „remote nicht lesbar" als Beispiel-Ursache eines leeren Assign-Batches. Nach dem gelesenen Code-Stand kann eine remote nicht lesbare Beschreibung aber gar keinen leeren Batch erzeugen: In `GitHubPlugin.AddObservation` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`, Z. 372) führt ein fehlender/`null`-Beschreibungsknoten zu `Permanent("Die verknüpfte GitHub-Referenz ist nicht verfügbar.")` bzw. fehlende Pflichtfelder zu `Permanent("Unvollständige GitHub-Antwort.")` — der Batch erreicht `AcceptBatchAsync` in diesem Fall nie, sondern landet als `ErrorCode` am Quellen-Checkpoint via `RecordFailureAsync`. Die einzig verbleibende Leer-Batch-Ursache für `ScmContentKind.Description` ist der Watermark-Vorfilter (`AddObservation`, Z. 377: `created/updated/edited > WatermarkUtc → return`). Dasselbe unzutreffende Beispiel steht auch in `docs/help/plugins/github-ereignisse/ablauf-technisch.md` („remote nicht lesbare oder post-watermark liegende Beschreibung") und im Doc-Kommentar von `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` (`ScmEventMonitoringServiceTests_Assignments.cs`, Z. 401–404). Die Formulierung war bereits im Altbestand ungenau („nicht lesbar") und wurde beim Rewrite beibehalten.

  Empfehlung: Das Beispiel „remote nicht lesbar" aus den drei Stellen streichen oder präzisieren (z. B. „ihr Textstand liegt nach dem Watermark"); wer eine Leer-Batch-Diagnose sieht, sollte nicht in Richtung Checkpoint-Fehlerpfad suchen.

### GitHubPlugin.Events.cs (GitHubPlugin) / ScmEventValidationService.cs (ScmEventValidationService)

- **Doppelter Code (niedrig)** — Die Kommentar-Arten-Liste `ScmContentKind.IssueComment or ScmContentKind.ReviewComment or ScmContentKind.PullRequestReview` existiert jetzt zweimal mit demselben Konzeptnamen „Kommentar-Arten": im Plugin-Vorfilter (`GitHubPlugin.Events.cs`, Z. 388) und in der Reaktionsprüfung (`ScmEventValidationService.cs`, Z. 68). Beide Listen müssen bei einem künftigen `ScmContentKind`-Wert synchron angepasst werden, liegen aber in getrennten Assemblies (Plugin vs. Application) — ein vergessener Listenpunkt fiele still auf. (Milder Umstand: Die jeweilige Tiefenverteidigung — Marker-Ablehnung im Validator ist kind-agnostisch, der Plugin-Vorfilter greift nicht auf Description — verhindert ein sicherheitsrelevantes Durchrutschen, es gäbe nur ein Verhaltensabweichen.)

  Empfehlung: Entweder eine gemeinsame Definition der „Kommentar-Arten" in `Softwareschmiede.Plugin.Contracts` anlegen (wo `ScmContentKind` selbst liegt, z. B. Erweiterungsmethode `IsCommentContentKind` oder statische Liste) und an beiden Stellen verwenden, oder zumindest gegenseitig referenzierende Kommentare ergänzen, damit eine Erweiterung beide Stellen findet.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`
- `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventStore.cs`
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`
- `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Assignments.cs`
- `docs/help/plugins/github-ereignisse/ablauf-technisch.md`
- `docs/help/plugins/github-ereignisse/business-rules.md`
- `docs/help/plugins/github-ereignisse/ereignisablaeufe.md`
- `docs/help/plugins/github-ereignisse/troubleshooting.md`

Kontextuell mitgeprüft (Referenz, nicht im Diff): `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`, `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_IssuePreparation.cs`.

## Verifizierte Kernpunkte (keine Befunde)

- **Nebenwirkungen der Vorfilter-Aufweichung:** Marker-tragende Beschreibungen ohne Assign-Kontext werden hostseitig verworfen — im Texttrigger-Pfad von `GetRejection` über „Kein Trigger"/`NoMentionRejection`, „Textversion stammt nicht vom Repository-Owner" oder „Eigene Antwort" (Z. 81–82, unverändert); bei unzugeordneten Quellen über `UnassignedDiagnostic`; in Assign-Batches über das `unassignedRejection`-Lambda. Die Rekursionsschutz-Absicht ist gewahrt.
- **Marker kann keinen fremden Zuweisender legitimieren:** Scheitert die Zuweisungsprüfung, fällt `GetAssignRejection` auf `GetRejection(observation with { Trigger = null })` zurück, dessen Marker-Prüfung unverändert greift — belegt durch `GetRejection_AssignForeignAssignerWithMarker_StaysRejected` und `GetRejection_AssignForeignAssignerWithMentionAndMarker_StaysRejected`.
- **Kommentar-Arten-Liste vollständig:** `ScmContentKind` hat genau die Werte `Description, IssueComment, ReviewComment, PullRequestReview`; beide expliziten Listen decken alle Nicht-Description-Arten ab und sind zueinander konsistent.
- **Erste-Beobachtung-/Race-Semantik:** Die Assign-Dekoration trägt die Ereignisidentität in den `ContentKey` (`…/assign-{eventNodeId}`), eine `unassigned`-Nachlauf macht den Zuweisender unbestimmbar (`AssignerUnknownRejection`), Fehler der Zuweisungsabfrage gehen in den Source-Key-Backoff — jeweils testbelegt. Kein neuer Race entstanden.
- **Tests belegen die Absicht korrekt:** `OwnMarkedDescription_IsDelivered` (Plugin-Ebene, Viewer-Autor via `Node`-Default = Viewer `U_1`), `GetRejection_AssignOwnResponseMarker_Accepted` (Validator-Ebene) und `Monitor_SeparatesAccount_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches` (End-to-End mit `BOT`/`scm-bot`-Autor) decken die drei Ebenen sauber ab; `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` sichert den Nicht-Assign-Loop-Schutz weiterhin ab.
