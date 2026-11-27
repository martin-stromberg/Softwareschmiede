# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Zweite Review-Runde. Die beiden Befunde aus `review-code.1.md` sind korrekt behoben:

1. **Kommentar-Genauigkeit:** Die Leer-Batch-Ursache ist jetzt faktisch richtig beschrieben — in `AddObservation` (`GitHubPlugin.Events.cs` Z. 372, 377, 391) erzeugt eine remote nicht lesbare Beschreibung einen `Permanent`-Fehler; die einzig verbleibende Leer-Batch-Ursache ist der Watermark-Vorfilter. `ScmEventStore.cs` Z. 25–31 und 144–147, `ablauf-technisch.md`, der Doc-Kommentar von `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` (`ScmEventMonitoringServiceTests_Assignments.cs` Z. 401–406) und `AssignEmptyDescriptionRejection` (`ScmEventValidationService.cs` Z. 22) sind konsistent korrigiert.
2. **Doppelter Code:** `ScmContentKindExtensions.IsCommentKind` in `ScmEvents.cs` (Plugin.Contracts) ist korrekt als `public static` exportiert und von beiden Konsumenten (`ScmEventValidationService.cs` Z. 68, `GitHubPlugin.Events.cs` Z. 388) per bestehendem `using Softwareschmiede.Domain.ValueObjects` erreichbar — Build beider Assemblys grün. Vollständigkeitsprüfung: Kein weiterer Vollmengen-Membership-Test der drei Kommentar-Arten existiert mehr; die verbliebene Zwei-Mengen-Prüfung `is not (IssueComment or ReviewComment)` (`GitHubPlugin.Events.cs` Z. 191) ist ein anderes Konzept (REST-Kommentar-Endpunkte, `PullRequestReview` hat einen eigenen Zweig) und durfte nicht ersetzt werden.

Nebenwirkungen der Vorfilter-Aufweichung erneut geprüft — keine neuen Befunde: Der Rekursionsschutz im Texttrigger-Pfad (`GetRejection` Z. 80–81: `"Eigene Antwort"`) und im Assign-Fallback (`GetAssignRejection` Z. 114) bleibt wirksam; eine Marker-Beschreibung legitimiert keinen fremden Zuweisender (`GetRejection_AssignForeignAssignerWithMarker_StaysRejected`). Die Zustellungs-Gegenprüfung (`ScmEventOrchestrator.DispatchCoreAsync` Z. 219–232) läuft über denselben `GetAssignRejection`-Pfad am persistierten Trigger. Die Empfangsreaktion bleibt korrekt auf Nicht-Descriptions beschränkt (`DispatchCoreAsync` Z. 162). `ScmIssuePreparationService.DetectAndEnqueueAsync` erhält Marker-Beschreibungen jetzt als Beobachtung, der Schleifenschutz ist gewahrt (`SkipEvaluationAsync`/Baseline-`CreatedAt`/`PreparationEventKey`-Dedup). Tests der drei Ebenen sind produktiv aufgebaut (178 Tests in `ScmEventValidationServiceTests` + `GitHubEventSourceTests` lokal grün).

## Befunde

### ScmEvents.cs (ScmContentKindExtensions)

- **Kommentar-Genauigkeit (niedrig)** — Der `<summary>`-Kommentar von `IsCommentKind` (Z. 18–21) begründet die Menge mit „Nur diese können remote eine Empfangsreaktion tragen und eigene Antwortmarker transportieren" — beides ist faktisch unzutreffend: (a) Beschreibungen transportieren eigene Antwortmarker — `ScmResponseKind.IssueBodyUpdate` versieht die Beschreibung mit demselben `<!-- softwareschmiede:action:{id} -->`-Marker (`ScmResponseRequest`-Doku Z. 83–88 in derselben Datei: „versieht den Remote-Text wie bei Kommentaren mit dem Marker als Reconcile-Anker"); genau diese marker-tragende Beschreibung ist der Anlass dieses Branches. (b) Beschreibungen können remote ebenfalls Reaktionen tragen — `TextFields` fragt `reactionGroups` auch im Description-Read ab (`GitHubPlugin.Events.cs` Z. 167), `ViewerHasReacted` kann auf einer Description-Beobachtung wahr sein (vgl. `ViewerReaction_DescriptionNotRejected`); die Reaktionsprüfung wertet sie dort nur nicht aus. Die eigentliche Begründung ist die umgekehrte Richtung: Empfangsreaktionen werden nur für Kommentar-Arten *versendet/ausgewertet*, und marker-tragende Kommentare sind in keinem Pfad legitimierbar — Beschreibungen dagegen über den Assign-Auslöser.

  Empfehlung: Begründungssatz umformulieren, z. B. „Nur für diese wertet der Host eine Kontoreaktion als Quittierung aus und filtert das Plugin eigene Antwortmarker — marker-tragende Kommentare sind in keinem Pfad legitimierbar, während Beschreibungen über den Assign-Auslöser legitimiert werden können."

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventStore.cs`
- `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`
- `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Assignments.cs`
- `docs/help/plugins/github-ereignisse/ablauf-technisch.md`
- `docs/help/plugins/github-ereignisse/business-rules.md`
- `docs/help/plugins/github-ereignisse/ereignisablaeufe.md`
- `docs/help/plugins/github-ereignisse/troubleshooting.md`
