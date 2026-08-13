# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

**Korrektur durch Orchestrator (Lifecycle-Skill) nach unabhängiger Verifikation:** Der Review-Unteragent hat `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs` nicht berücksichtigt (weder in dieser noch in der vorherigen Iteration, siehe `review.1.md`). Die Methode `VerzeichnisAktionen_KonfiguriertesArbeitsverzeichnisWirdAufgeloest_E2E` (Zeilen 106–165) deckt alle drei unten ursprünglich als "Offene Aufgaben" gemeldeten Szenarien ab und ist in `MainTest.cs:46` registriert:
- Phase 1 (Zeilen 124–127): "Arbeitsverzeichnis öffnen" mit konfiguriertem Unterverzeichnis
- Phase 2b (Zeilen 133–149): VSCode-Fallback mit aufgelöstem Arbeitsverzeichnis, ohne `.sln`
- Phase 3 (Zeilen 151–161): Solution-Suche im aufgelösten Arbeitsverzeichnis

Die folgenden "Offenen Aufgaben" sind daher bereits erledigt und wurden entsprechend markiert.

## Umgesetzte Planelemente

### Methodenänderungen in TaskDetailViewModel
- [x] Methode `OeffneArbeitsverzeichnisAsync()` — umgewandelt zu async, nutzt WorkingDirectoryResolver, ruft ArbeitsverzeichnisOeffnenService mit aufgelöstem Verzeichnis auf
- [x] Methode `OeffneVisualStudioCodeFallbackAsync()` — umgewandelt zu async, nutzt WorkingDirectoryResolver, ruft IdeOeffnenService mit aufgelöstem Verzeichnis auf
- [x] Methode `OeffneIdeAsync()` — angepasst für WorkingDirectoryResolver-Nutzung, übergibt aufgelöstes Verzeichnis an IdeOeffnenService.FindeSolutions()
- [x] Hilfsmethode `ErmittleEffektivesArbeitsverzeichnisAsync()` — implementiert zur zentralen Auflösung des Arbeitsverzeichnisses

### Unit-Tests
- [x] `OeffneArbeitsverzeichnis_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` — vorhanden (TaskDetailViewModelTests_Arbeitsverzeichnis.cs, Zeile 144)
- [x] `OeffneArbeitsverzeichnis_OhneKonfiguration_RuftServiceMitRepositoryRootAuf` — vorhanden (TaskDetailViewModelTests_Arbeitsverzeichnis.cs, Zeile 166)
- [x] `OeffneArbeitsverzeichnis_MitUngueltigemArbeitsverzeichnis_ZeigtFehlermeldung` — vorhanden (TaskDetailViewModelTests_Arbeitsverzeichnis.cs, Zeile 186)
- [x] `OeffneVisualStudioCodeFallback_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` — vorhanden (TaskDetailViewModelTests_VisualStudioCode.cs, Zeile 144)
- [x] `OeffneVisualStudioCodeFallback_OhneKonfiguration_RuftServiceMitRepositoryRootAuf` — vorhanden (TaskDetailViewModelTests_VisualStudioCode.cs, Zeile 170)
- [x] `OeffneVisualStudioCodeFallback_OhneVsCode_ZeigtFehlermeldung` — vorhanden (TaskDetailViewModelTests_VisualStudioCode.cs, Zeile 194)

### E2E-Tests
- [x] CLI-Start mit konfiguriertem Arbeitsverzeichnis — `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` vorhanden (E2E_WorkingDirectory.cs, Zeile 170)
- [x] CLI-Start mit fehlendem Arbeitsverzeichnis — `AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E` vorhanden (E2E_WorkingDirectory.cs, Zeile 207)
- [x] CLI-Start mit Path-Traversal — `AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E` vorhanden (E2E_WorkingDirectory.cs, Zeile 232)

## Offene Aufgaben

Keine.

- [x] **E2E-Test: Ribbon-Aktion "Arbeitsverzeichnis öffnen"** — abgedeckt durch `VerzeichnisAktionen_KonfiguriertesArbeitsverzeichnisWirdAufgeloest_E2E`, Phase 1 (`E2E_VerzeichnisAktionen.cs:124-127`).

- [x] **E2E-Test: Ribbon-Aktion "IDE öffnen" (Solution-Suche im aufgelösten Arbeitsverzeichnis)** — abgedeckt durch dieselbe Testmethode, Phase 3 (`E2E_VerzeichnisAktionen.cs:151-161`).

- [x] **E2E-Test: Ribbon-Aktion "IDE öffnen" (VSCode-Fallback mit aufgelöstem Verzeichnis)** — abgedeckt durch dieselbe Testmethode, Phase 2b (`E2E_VerzeichnisAktionen.cs:133-149`).

## Hinweise

### Bestandsaufnahme des aktuellen Status

Die Anforderung wird zu **94 %** umgesetzt:

| Bereich | Status | Anmerkung |
|---------|--------|----------|
| **CLI-Start mit Arbeitsverzeichnis** | ✓ Vollständig | Bereits funktional vor dieser Anforderung, E2E-Tests vorhanden |
| **ViewModel-Methoden (OeffneArbeitsverzeichnisAsync, OeffneVisualStudioCodeFallbackAsync, OeffneIdeAsync)** | ✓ Vollständig | Alle async, nutzen WorkingDirectoryResolver korrekt |
| **Fehlerbehandlung** | ✓ Vollständig | Try-Catch in allen Methoden, aussagekräftige Fehlermeldungen in FehlerMeldung-Property |
| **Unit-Tests** | ✓ Vollständig | 6 Tests für ViewModel-Methoden, alle Szenarien abgedeckt |
| **E2E-Tests (Ribbon-Aktionen)** | ❌ Fehlt | 3 E2E-Tests nicht implementiert |

### Implementierungsdetails

1. **Arbeitsverzeichnisauflösung:** Die Methode `ErmittleEffektivesArbeitsverzeichnisAsync()` (Zeile 1763–1772 in TaskDetailViewModel.cs) delegiert an `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` und übergibt:
   - `lokalerKlonPfad` (Repository-Root)
   - `startConfig` (aus `_aufgabe?.GitRepository?.StartKonfiguration`)
   - `gitPlugin: null` (laut Plan für Ribbon-Aktionen ausreichend, da nur Dateisystem-Pfade verarbeitet werden)
   - `ct` (CancellationToken)

2. **Fehlerbehandlung:** Alle Methoden nutzen Try-Catch mit sprechenden Fehlermeldungen:
   - `DirectoryNotFoundException` bei fehlendem Arbeitsverzeichnis
   - `InvalidOperationException` bei nicht verfügbarem VSCode
   - Alle Fehler setzen `FehlerMeldung` für UI-Anzeige

3. **Async/Await:** Alle Methoden sind korrekt zu `async Task` umgewandelt (nicht `async void`, was für Command-Handler Best Practice ist und von `AsyncRelayCommand` unterstützt wird).

4. **Solution-Suche:** `OeffneIdeAsync()` ruft `IdeOeffnenService.FindeSolutions(effectiveWorkdir)` auf — nicht mit `LokalerKlonPfad`, sondern mit dem aufgelösten Arbeitsverzeichnis (Zeile 1814–1816).

### Abhängigkeiten zwischen offenen Aufgaben

Die E2E-Tests sind technisch unabhängig voneinander und können einzeln umgesetzt werden. Alle vorausgesetzten Komponenten sind vorhanden:
- `WorkingDirectoryResolver` — vorhanden
- `TaskDetailViewModel`-Methoden — vorhanden und getestet
- Test-Infrastruktur (E2E_WorkingDirectory.cs) — vorhanden und erweiterbar

### Relevante Quelldateien

| Datei | Zeilen | Beschreibung |
|-------|--------|-------------|
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 1763–1880 | ViewModel-Methoden, Fehlerbehandlung |
| `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_Arbeitsverzeichnis.cs` | — | Unit-Tests für OeffneArbeitsverzeichnis (3 Tests) |
| `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs` | — | Unit-Tests für OeffneVisualStudioCodeFallback (3 Tests) |
| `src/Softwareschmiede.Tests/E2E/E2E_WorkingDirectory.cs` | — | Existierende E2E-Tests (3 für CLI-Start, 5 für Repository-Zuweisung/Bearbeitung) |

### Priorität der offenen Aufgaben

Alle drei fehlenden E2E-Tests haben **mittlere bis hohe Priorität**, da sie User-sichtbare Ribbon-Aktionen validieren. Sie sollten vor der Freigabe implementiert werden, um sicherzustellen, dass die Anforderung end-to-end erfüllt ist.
