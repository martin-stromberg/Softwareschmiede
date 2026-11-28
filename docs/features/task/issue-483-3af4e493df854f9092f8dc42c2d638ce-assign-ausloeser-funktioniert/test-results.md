# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Ausgeführte Lanes (jeweils nach vollständigem Solution-Build, `dotnet build Softwareschmiede.slnx` → 0 Fehler, 0 Warnungen; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline gesetzt):

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --collect:"XPlat Code Coverage"` → 2883 Tests: **2880 bestanden, 0 fehlgeschlagen, 3 übersprungen** (plattformbedingte Skips: 2× Shutdown-Kommando Linux/macOS, 1× `Open_OnNotWindows_ThrowsPlatformNotSupportedException`)
- `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --collect:"XPlat Code Coverage"` → 456 Tests: **456 bestanden, 0 fehlgeschlagen**
- OsInterface-Lane (`--filter "Category=OsInterface"`, FlaUI/ConPTY-E2E, Clipboard, echte Prozessstarts): **nicht ausgeführt** — umgebungssensitive Lane ohne Bezug zu dieser reinen Backend-Änderung (kein UI-/CLI-/Vertragsanteil). In dieser Sandbox ist die ConPTY-Isolation nachweislich defekt (Dauerzustand, siehe Projektregeln) und die Lane erfordert eine interaktive Desktop-Sitzung; laut Plan (Schritt 7) ist die Verifikation auf die beiden regulären Lanes festgelegt.

## E2E-Abdeckung

Keine FlaUI-E2E-Szenarien geplant (im Plan begründet: kein UI-/Nutzerfluss — externer Auslöser, Wirkung in Hintergrunddiensten). Primärer Funktionsnachweis ist der Integrationstest auf `ScmDispatchFixture`-Ebene.

| Szenario (aus plan.md, Akzeptanzszenarien) | Test / Testklasse | Ergebnis |
|-------------------------------------------|-------------------|----------|
| 1 – Happy Path: berechtigte Zuweisung auf aufbereitetes Issue (Notification → Description-Read → Validierung → Aufgabe → Dispatch mit `AssigneePromptPrefix`) | `Monitor_SeparatesAccount_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches` (`ScmEventMonitoringServiceTests_Assignments`, IntegrationTests) + `OwnMarkedDescription_IsDelivered` (`GitHubEventSourceTests`) | Bestanden |
| 2 – Zuweisung entzogen vor Verarbeitung → `AssignerUnknownRejection` | `Monitor_SeparatesAccount_AssignUndeterminable_Rejected` | Bestanden |
| 3 – Unberechtigter Zuweisender mit Marker-Beschreibung → `AssignerNotAllowedRejection` | `GetRejection_AssignForeignAssignerWithMarker_StaysRejected` (Unit) + `Monitor_SeparatesAccount_AssignByForeignLogin_Rejected` | Bestanden |
| 4 – Marker-Beschreibung ohne Assign-Kontext → „Eigene Antwort", kein Loop | `SeparatesAccount_OwnResponseMarkerStillRejected` (Unit) + `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` (Integration) | Bestanden |
| 5 – Leere Beschreibung / leerer Batch | `Monitor_SeparatesAccount_AssignEmptyDescription_CreatesNoTask` + `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` | Bestanden |

Zusätzliche geplante Nachweise (alle vorhanden und bestanden): `GetRejection_AssignOwnResponseMarker_Accepted` (umgewandelter Akzeptanzfall), `GetRejection_AssignForeignAssignerWithMarker_StaysRejected`, `OwnMarkedDescription_IsDelivered`, `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted` (Kommentar-Filter unverändert), `Monitor_SeparatesAccount_AssignOnPullRequest_CreatesTask` (PR-Geltung).

## Reale End-to-End-Abnahme

Die Anforderung berührt externe Dienste (GitHub-Notifications-API) und Persistenz (SQLite-Eventstore) — eine reale Abnahme ist grundsätzlich erforderlich, aber in dieser Umgebung **nicht durchführbar**:

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| Happy Path: reale GitHub-Zuweisung eines aufbereiteten, marker-tragenden Issues an das Überwachungskonto → lokale Aufgabe + zugestelltes `ScmIncomingEvent` mit `AssigneePromptPrefix` | Würde erfordern: gestartete App-Instanz mit `SeparateAccount`-Konfiguration, echtes GitHub-Repository mit PAT, Issue-Aufbereitung in Betrieb, reale Zuweisung | **Nicht durchführbar** — kein Test-PAT/Test-Repository in dieser Umgebung verfügbar; die einzige laufende `Softwareschmiede.exe`-Instanz (PID 31000, Desktop-Installation) hostet produktiv diese Session und darf weder beendet noch für Testzwecke gestört/rekonfiguriert werden; eine zweite Instanz gegen produktive SCM-Konfiguration würde reale Repositories/Notifications beeinflussen. Nächstbeste durchgeführte Ebene: `ScmDispatchFixture`-Integrationstest mit echten Services, echter SQLite und realem `ScmEventOrchestrator`-Dispatch inkl. `AssigneePromptPrefix` (bestanden). **Eskaliert an den Anwender** (2026-11-27, Issue-Kommentar [#issuecomment-7229535547](https://github.com/martin-stromberg/Softwareschmiede/issues/483#issuecomment-7229535547)) mit den beiden Auflösungswegen manuelle Abnahme / dokumentierter Verzicht; Entscheidung wird im PR-Review gefällt. |

## Sichtprüfung der Abnahme-Screenshots

**Status:** Nicht erforderlich

Begründung: Keine UI-Änderung und keine UI-Szenarien — die Anforderung betrifft ausschließlich Filter-/Validierungslogik in Backend-Diensten (`GitHubPlugin.AddObservation`, `ScmEventValidationService.GetAssignRejection`) plus Kommentar-/Doku-Anpassungen. Es existieren keine Abnahme-Screenshots.

## Zusammenfassung

- Gesamt: 3339 (Softwareschmiede.Tests: 2883 + Softwareschmiede.IntegrationTests: 456)
- Bestanden: 3336
- Fehlgeschlagen: 0
- Übersprungen: 3 (plattformbedingt, ohne Bezug zur Änderung)
- Nicht ausgeführt: OsInterface-Lane (s. Ergebnis-Block; keine geplanten E2E-Szenarien für diese Anforderung)

## Testabdeckung

**Abdeckung:** 89,0 % (kombinierte Zeilenabdeckung über beide Lanes, unionierte Cobertura-Daten; 74825/84057 Zeilen)

Von dieser Änderung betroffene Dateien (alle deutlich über 80 %):

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs` | 100 % |
| `src/Softwareschmiede/Application/Services/ScmEventStore.cs` | 98,4 % |
| `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` | 97,9 % |

Dateien unter 80 % im Umfeld der Änderung (SCM-/GitHub-Domäne, unverändert durch dieses Feature):

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmRequirement.cs` | 24 % |
| `src/Softwareschmiede/Application/Services/ScmCliWorkspaceTrust.cs` | 47 % |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Options/ScmResolutionCacheOptions.cs` | 50 % |
| `src/Softwareschmiede/Application/Services/ScmRepositoryAliasKey.cs` | 50 % |
| `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs` | 71 % |
| `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs` | 75 % |
| `src/Softwareschmiede/Application/Services/ScmBackmergeMonitoringService.cs` | 77 % |
| `src/Softwareschmiede/Infrastructure/Terminal/ScmCoordinatedTerminalSession.cs` | 78 % |

Hinweis: Insgesamt liegen 182 von 538 Dateien unter 80 %; die übrigen betreffen überwiegend `Softwareschmiede.App`-Views/ViewModels, den E2E-Hilfs-Stack (`ScmE2E*`, nur von der nicht gelaufenen OsInterface-Lane genutzt) und sonstige Domänen ohne Bezug zu diesem Feature.

## Fehlende Tests

Quelle: `Coverage-Daten`

Für diese Änderung keine fehlenden Tests — alle geänderten Dateien sind zu ≥97,9 % abgedeckt und alle geplanten Testfälle (neu, umgewandelt, Regression) existieren und liefen erfolgreich. Im SCM-Umfeld liegen folgende unveränderte Dateien bei 0 % (Vorlauf-Bestand, kein Feature-Bezug):

- `src/Softwareschmiede.App/Services/Testing/ScmE2E*.cs` (7 Dateien) — 0 % Abdeckung; ausschließlich Hilfs-Stack für die OsInterface-E2E-Lane, werden nur bei deren Ausführung getroffen
- `src/Softwareschmiede.App/Views/ScmEventQueueView(.xaml/.cs)` — 0 % Abdeckung (UI)
- `src/Softwareschmiede/Domain/Entities/ScmTaskLease.cs` — 0 % Abdeckung
- `server/Softwareschmiede.StatusServer/Options/GitHubRepositoryOptions.cs` — 0 % Abdeckung
- `server/Softwareschmiede.StatusServer/GitHub/GitHubStatusService.cs` — 12 % Abdeckung
