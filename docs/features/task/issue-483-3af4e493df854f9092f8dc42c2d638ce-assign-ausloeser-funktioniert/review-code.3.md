# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ScmEventValidationServiceTests.cs (ScmEventValidationServiceTests)

- **Testqualität / Kommentar-Genauigkeit (niedrig)** — Der Test `GetRejection_AssignForeignAssignerWithMarker_StaysRejected` (Z. 298–304) trägt im Body `"Auftrag\n<!-- softwareschmiede:action:abc -->"` ohne frei stehende Erwähnung. Der Texttrigger-Fallback in `GetAssignRejection` (Z. 114) scheitert für diesen Body bereits an `HasFreeStandingTrigger` (`NoMentionRejection`) — die Marker-Prüfung in `GetRejection` (Z. 80–81) wird für diese Eingabe gar nicht erreicht, der Marker ist für das Ergebnis nicht maßgeblich. Dieselbe Assertion gilt mit markerfreiem Body (`GetRejection_AssignForeignAssigner_Rejected`, Z. 249–252). Die marker-entscheidende Ablehnung im Fallback deckt bereits `GetRejection_AssignForeignAssignerWithMentionAndMarker_StaysRejected` (Z. 268–273) ab. Der Doc-Kommentar „der Texttrigger-Fallback lehnt den eigenen Antwortmarker ebenfalls ab" attribuiert die Ablehnung damit einem Mechanismus, den die Eingabe nicht auslöst — was tatsächlich geprüft wird, ist nur die führende Zuweisungsbegründung.

  Empfehlung: Doc-Kommentar präzisieren, z. B. „…der Texttrigger-Fallback scheitert für diesen Body bereits an der fehlenden Erwähnung (dass er den Marker selbst abwehrt, zeigt `GetRejection_AssignForeignAssignerWithMentionAndMarker_StaysRejected`), sodass die spezifischere Zuweisungsbegründung führend bleibt." Alternativ den Test als redundant entfernen — die Ablehnungs-Priorität (AssignerNotAllowedRejection vor jeder Marker-bezogenen Ablehnung) bleibt durch den bestehenden Mention+Marker-Test abgesichert.

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
