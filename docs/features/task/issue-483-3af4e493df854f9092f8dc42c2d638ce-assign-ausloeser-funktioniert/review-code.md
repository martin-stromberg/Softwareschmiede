# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine. Abschluss-Verifikation nach drei Review-Runden (Befunde aus `review-code.1.md`, `review-code.2.md`, `review-code.3.md` jeweils behoben); der Gesamtstand wurde mit frischem Blick erneut geprüft.

Verifikation des letzten Fix-Befunds: Der neue Doc-Kommentar von `GetRejection_AssignForeignAssignerWithMarker_StaysRejected` (`ScmEventValidationServiceTests.cs`, Z. 298–305) ist faktisch korrekt. Durchgespielt: `ObserveAssign` verwendet `ScmAccountMode.SeparateAccount` (Viewer `scm-bot`); der Body `"Auftrag\n<!-- softwareschmiede:action:abc -->"` enthält keine frei stehende `@scm-bot`-Erwähnung, sodass der Texttrigger-Fallback in `GetAssignRejection` (`ScmEventValidationService.cs`, Z. 114) bereits an `NoMentionRejection` scheitert — die Marker-Prüfung (Z. 80–81) wird nicht erreicht und das Ergebnis `AssignerNotAllowedRejection` bleibt führend. Der Verweis auf `…_WithMentionAndMarker_StaysRejected` (Z. 271–273) für den marker-entscheidenden Fall stimmt ebenfalls.

Stichproben der Kernbehauptungen gegen den Code verifiziert:

- `GitHubPlugin.Events.cs` Z. 384–389: `ScmEventReadRequest` trägt tatsächlich keinen Auslöser (nur `Reference`, `ContentKind`, `SinceUtc`, `WatermarkUtc`) — der Kommentar „den sieht das Plugin im ScmEventReadRequest nicht" stimmt; `IsCommentKind()`-Beschränkung greift korrekt nur für die drei Kommentar-Arten, `Description` wird stets als Beobachtung geliefert.
- `ScmEventStore.cs` Z. 25–32 und Z. 142–147: Die Behauptung „remote nicht lesbare Beschreibung → Permanent-Fehler, kein leerer Batch" stimmt — `AddObservation` wirft `Permanent` bei `node.ValueKind != Object` (Issue `null`) sowie über `Required*`/`RequiredDate` bei fehlenden Pflichtfeldern; post-watermark liegende Texte erzeugen dagegen den leeren Batch.
- `ScmEventValidationService.cs` Z. 22: „leere Referenzbeschreibung" ohne „nicht lesbar" ist korrekt — nicht lesbare Beschreibungen erreichen den Validator nicht (pluginseitiger Permanent-Fehler).
- `ScmEvents.cs` Z. 18–23: Die korrigierte `IsCommentKind`-Summary trifft zu (Reaktionsauswertung nur für Kommentar-Arten; Marker-Filterung nur pluginseitig für Kommentare; Beschreibungen über Assign-Auslöser legitimierbar).
- Marker-Abwehr im Texttrigger-Pfad weiterhin wirksam (Z. 80–81, Ordinal-Vergleich), im Assign-Pfad legitimiert allein der Zuweisender-Kreis — Rekursionsschutz intakt: Eine eigene Marker-Beschreibung ohne Zuweisung läuft mit `Trigger = null` in den regulären Pfad und wird abgelehnt.
- `IsCommentKind` ist für beide Konsumenten erreichbar (`Softwareschmiede.csproj` und `Softwareschmiede.Plugin.GitHub.csproj` referenzieren `Softwareschmiede.Plugin.Contracts`); die verbliebenen Zwei-Mengen-Prüfungen (`GitHubPlugin.Events.cs` Z. 191, `GitHubPlugin.EventResponses.cs` Z. 348) sind ein anderes Konzept (REST-Kommentar-Endpunkte) und korrekt unverändert.
- Kein Seiteneffekt auf `ScmIssuePreparationService.DetectAndEnqueueAsync`: Die dort neu ankommenden viewer-authierten Marker-Beschreibungen ändern nichts — die Aufbereitung qualifiziert ohnehin jede neue Issue-Beschreibung (`CreatedAt` > Baseline), und ein `ScmResponseKind` zum Issue-Anlegen existiert nicht.
- Zustellungs-Gegenprüfung (`ScmEventOrchestrator.cs` Z. 219–231): Angenommene Assign-Ereignisse mit Marker-Beschreibung revalidieren über `GetAssignRejection` erneut legitim — kein neuer Wartepfad.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`
- `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventStore.cs`
- `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Assignments.cs`
- `docs/help/plugins/github-ereignisse/ablauf-technisch.md`
- `docs/help/plugins/github-ereignisse/business-rules.md`
- `docs/help/plugins/github-ereignisse/ereignisablaeufe.md`
- `docs/help/plugins/github-ereignisse/troubleshooting.md`
