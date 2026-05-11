# Testlücken – Pull-Request-Repository-ID entfernen

## Kontext der Analyse

- Berücksichtigte Planungs-/Architekturdokumente:
  - `docs/requirements/pull-request-repository-id-removal-requirements-analysis.md`
  - `docs/architecture/pull-request-repository-id-removal-architecture-blueprint.md`
  - `docs/improvements/pull-request-repository-id-removal-architecture-review.md`
- Berücksichtigter Implementierungsstand:
  - `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor(.cs)`
  - `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- Coverage-Ermittlung: `dotnet test src/Softwareschmiede.Tests --collect:"XPlat Code Coverage"` (1 fehlgeschlagener Test, übrige Tests erfolgreich)
- Validierungsbericht: Implementierungsagent (Repository-ID-Entfernung durchgeführt und geprüft)

---

## Nicht getestete oder unzureichend getestete Funktionalitäten (priorisiert)

| Priorität | Komponente | Funktionalität | Ist-Zustand Coverage | Anforderungs-/Architekturbezug | Begründung |
|---|---|---|---|---|---|
| **P1** | `GitOrchestrationService.PullAsync` | Vollständiger Aufruf-Flow: PullAsync ruft `_gitPlugin.PullAsync` auf und schreibt Protokolleintrag | **0 % (line-rate: 0)** | FR-1, NFR-4 | Die Methode ist vollständig ungetestet. Fehler (fehlende Aufgabe, fehlender Klonpfad) und der Erfolgspfad inkl. Protokollierung sind nicht abgesichert. |
| **P1** | `GitOrchestrationService.ResolveRepositoryIdAsync` | Kein aktives Repository im Projekt (0 Treffer → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.75** | FR-3, AC-3, Architektur-Review F-03 | Die Auswahlregel „0 aktive Repositories" ist im Blueprint explizit als Abbruchpfad definiert, aber durch keinen Test abgesichert. |
| **P1** | `GitOrchestrationService.ResolveRepositoryIdAsync` | Projekt nicht gefunden (`GetDetailAsync` gibt `null` zurück → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.75** | FR-3, AC-3 | Defensive Guard beim Projekt-Lookup ohne Testschutz; Regressionsrisiko bei Service-Refactoring. |
| **P1** | `GitOrchestrationService.ExtractRepositoryIdFromUrl` | SSH-URL-Format (`git@github.com:owner/repo.git`) | Branch fehlt; **branch-rate: 0.5** | FR-1, NFR-2 | Die Methode enthält explizite Kommentare und Logik für SSH-URLs, die jedoch nicht getestet sind. Da SSH-Klone typisch sind, ist das Risiko hoch. |
| **P1** | `GitOrchestrationService.ExtractRepositoryIdFromUrl` | Ungültige URL ohne Slash (`lastSlash == -1` → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | NFR-2 | Fehlerabbruch bei invalider Repository-URL ist ungetestet; kann bei fehlerhaften Datenbankeinträgen zur unerwarteten Exception führen. |
| **P1** | `GitOrchestrationService.ExtractRepositoryIdFromUrl` | URL mit zu wenig Segmenten (`secondLastSlash == -1` → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | NFR-2 | Zweiter Fehlerabbruch-Pfad für fragmentarische URLs ist ebenfalls ungetestet. |
| **P1** | `GitOrchestrationService.PullRequestErstellenAsync` | Aufgabe nicht gefunden (null → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | FR-3, AC-3 | Defensiver Guard in der PR-Methode ohne Testschutz; identisches Muster fehlt auch bei CommitAsync/ResetAsync. |
| **P1** | `GitOrchestrationService.PullRequestErstellenAsync` | Fehlender Branch-Name (`BranchName` ist null/leer → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | FR-3, AC-3 | Kritischer Fehlerfall, der verhindert, dass ein PR ohne Branch erstellt wird; nicht getestet. |
| **P1** | `GitOrchestrationService.PullRequestErstellenAsync` | Standard-Titel (kein `title`-Parameter → `aufgabe.Titel` wird verwendet) | Zweig nicht ausgeführt | FR-1, AC-2 | Der Default-Fallback für den PR-Titel ist nicht abgedeckt; UC-1-Standardpfad (kein manueller Titel) ist ungesichert. |
| **P1** | `GitOrchestrationService.PullRequestErstellenAsync` | Standard-Body (kein `body`-Parameter → generierter Standardtext) | Zweig nicht ausgeführt | FR-1, AC-2 | Der automatisch generierte PR-Body ist nicht getestet; Inhalt und Format des Standardtexts können unbemerkt brechen. |
| **P1** | `AufgabeDetail` (UI) | Abwesenheit des Repository-ID-Felds in der PR-Maske (AC-1) | **0 % (kein UI-Test)** | FR-2, AC-1 | Kein UI-Test bestätigt, dass das Eingabefeld für die manuelle Repository-ID tatsächlich entfernt wurde. Es gibt keinen Regressionsschutz gegen ein unbeabsichtigtes Wiedereinfügen. |
| **P1** | `AufgabeDetail.PullRequestErstellenAsync` (UI-Handler) | Leerer Titel → Fehlermeldung wird gesetzt (kein Plugin-Aufruf) | **0 % (kein UI-Test)** | FR-2, AC-4 | Die einzige UI-seitige Validierung der PR-Maske ist nicht getestet; Pflichtfelddurchsetzung ohne Absicherung. |
| **P2** | `AufgabeDetail.PullRequestErstellenAsync` (UI-Handler) | Erfolgreicher PR-Flow: Form wird geschlossen, `_erfolg` gesetzt, `LadeAsync` aufgerufen | **0 % (kein UI-Test)** | FR-1, AC-2, AC-4 | Kompletter Happy-Path des UI-Handlers ist ungetestet. |
| **P2** | `AufgabeDetail.PullRequestErstellenAsync` (UI-Handler) | Fehlerfall: Service wirft Exception → `_fehler` wird angezeigt | **0 % (kein UI-Test)** | FR-3, AC-3 | Fehleranzeige in der UI bei fehlender Repository-Zuordnung ist nicht getestet. |
| **P2** | `AufgabeDetail` (UI) | PR-Formular nur bei Status `InBearbeitung` oder `KiAktiv` sichtbar | **0 % (kein UI-Test)** | FR-2, AC-1 | Die Sichtbarkeits-Logik des PR-Buttons und -Formulars in der Aktionsleiste ist komplett ungetestet. |
| **P2** | `AufgabeDetail` (UI) | Titel-Vorausfüllung des `_prTitel`-Felds aus `aufgabe.Titel` beim Laden | **0 % (kein UI-Test)** | AC-4 | `_prTitel = _aufgabe.Titel` wird in `LadeAsync` gesetzt; kein Test prüft, ob der Titel korrekt vorausgefüllt wird. |
| **P2** | `GitOrchestrationService.CommitAsync` | Fehlende Aufgabe (null → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | NFR-4 | Gleicher Fehlerguard-Muster wie in PullRequestErstellenAsync; nicht abgedeckt. |
| **P2** | `GitOrchestrationService.CommitAsync` | Fehlender Klonpfad (`LokalerKlonPfad` ist null/leer → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | NFR-4 | Validierungsschritt vor dem Commit nicht getestet; wichtiger Integritätscheck. |
| **P2** | `GitOrchestrationService.PushAsync` | Erfolgreicher Push inkl. Protokolleintrag | **line-rate: 0.4375** | NFR-4 | Nur der Fehlerfall (fehlender Branch-Name) ist getestet. Happy Path mit Protokolleintrag fehlt. |
| **P2** | `GitOrchestrationService.PushAsync` | Fehlende Aufgabe (null → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.5** | NFR-4 | Fehlerabbruch-Pfad beim Aufgaben-Lookup nicht abgedeckt. |
| **P2** | `GitOrchestrationService.PushAsync` | Fehlender Klonpfad → `InvalidOperationException` | Branch fehlt | NFR-4 | Validierungsschritt vor dem Push-Aufruf nicht getestet. |
| **P2** | `GitOrchestrationService.ResetAsync` | Fehlende Aufgabe (null → `InvalidOperationException`) | Branch fehlt; **branch-rate: 0.6666** | NFR-4 | Gleicher Guard-Muster wie bei den anderen Methoden; nicht getestet. |
| **P2** | `GitOrchestrationService.ResetAsync` | Fehlender Klonpfad → `InvalidOperationException` | Branch fehlt | NFR-4 | Validierungsschritt vor dem Reset-Aufruf fehlt im Test. |
| **P2** | `GitOrchestrationService.ResetAsync` | Angegebener `targetRef` wird im Protokolleintrag verwendet | Branch teilweise; **branch-rate: 0.6666** | NFR-4 | Nur der `targetRef == null`-Pfad (Protokoll zeigt „HEAD") ist getestet; der Pfad mit konkreter Ref-Angabe fehlt. |
| **P3** | `AufgabeDetail` (UI) | Seitenladeverhalten: `OnInitializedAsync` inkl. KI-Session-Wiederherstellen | **0 % (kein UI-Test)** | NFR-1 | Initialisierungslogik ohne Test; Fehler im Ladepfad werden erst zur Laufzeit sichtbar. |
| **P3** | `AufgabeDetail` (UI) | Aufgabe nicht gefunden → UI zeigt Alert-Meldung statt Detailansicht | **0 % (kein UI-Test)** | NFR-1 | Not-Found-Rendering-Pfad ist nicht abgesichert. |

---

## Aktuelle Testergebnisse und Coverage (Snapshot)

| Klasse/Methode | Line Rate | Branch Rate | Hinweis |
|---|---|---|---|
| `GitOrchestrationService` (gesamt) | 0.926 | 0.50 | Mehrere Fehlerpfade und Standard-Defaults nicht abgedeckt |
| `GitOrchestrationService.PullAsync` | **0.000** | **0.00** | Vollständig ungetestet |
| `GitOrchestrationService.PullRequestErstellenAsync` | 0.944 | 0.50 | Null-Guard, Default-Titel/Body fehlen |
| `GitOrchestrationService.ResolveRepositoryIdAsync` | 0.833 | 0.75 | 0-Repos- und Null-Projekt-Pfad fehlen |
| `GitOrchestrationService.ExtractRepositoryIdFromUrl` | 0.857 | 0.50 | SSH-URL und Fehlerformat-Pfade fehlen |
| `GitOrchestrationService.PushAsync` | 0.438 | 0.50 | Nur Fehlerfall (fehlender Branch); Happy Path fehlt |
| `GitOrchestrationService.CommitAsync` | 0.929 | 0.50 | Null-Guard und Klonpfad-Fehler ungetestet |
| `GitOrchestrationService.ResetAsync` | 0.933 | 0.67 | Null-Guards und targetRef-Protokollpfad fehlen |
| `AufgabeDetail` (UI-Komponente gesamt) | **0.000** | **0.00** | Kein einziger UI-Test vorhanden |
