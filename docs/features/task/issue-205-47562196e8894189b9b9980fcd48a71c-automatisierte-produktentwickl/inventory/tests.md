# Tests und Hilfsmethoden

## Testklassen

### `AutonomAufgabeInitialisierungsDialogViewModelTests`
**Datei:** `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`

#### Testmethoden

- **`BestaetigenAsync_ValidatesInputsAndCallsService()`** — Testet, dass `BestaetigenAsync()` Eingaben validiert und den Service aufruft, wenn alle Werte gültig sind. **RELEVANZ:** Dies testet den erfolgreichen Pfad durch den Dialog; keine Tests für `NeuenBranchAnlegenAsync()` im autonomen Fall vorhanden.

- **`BestaetigenAsync_FailsOnInvalidTokenBudget()`** — Testet Input-Validierung für Token-Budget.

- **`BestaetigenAsync_FailsOnInvalidInitialPrompt()`** — Testet Input-Validierung für InitialPrompt.

- **`BestaetigenAsync_FailsOnInvalidRuntimeLimit()`** — Testet Input-Validierung für Laufzeitlimit.

- **`BestaetigenAsync_SetsErrorMessage_WhenServiceThrows()`** — Testet Error-Handling, wenn der Service fehlschlägt. **RELEVANZ:** Erstellt eine Aufgabe ohne `LokalerKlonPfad` und erwartet, dass der Service fehlschlägt. Dies ist der umgekehrte Fall des Dialog-Problems: Hier ist bewusst kein Klon vorhanden und das wird erwartet. Im autonomen Dialog-Fall sollte dies nicht zu einem fehler im Branch-Anlag-Dialog führen, sondern die Branch-Erstellung in den Service verschieben.

- **`Abbrechen_ClosesDialog()`** — Testet, dass `Abbrechen()` den Dialog schließt, ohne den Service aufzurufen.

#### Testaufbau

- Verwendet `TestDbContextFactory.Create()` für eine Test-Datenbank
- Erstellt eine `Aufgabe` mit lokalem Klon via `ErstelleAufgabeMitLokalemKlon()`
- Mockt das `IPluginManager` und `ICliRunner`

#### FEHLENDE Tests

- **Tests für `NeuenBranchAnlegenAsync()` im autonomen Fall** — Es gibt keine Tests, die `NeuenBranchAnlegenAsync()` aufrufen, wenn `LokalerKlonPfad` null ist (oder der Dialog als autonom erkannt wird). Dies ist die genaue Szenario, die den Fehler triggert.
- **Tests für Dialog-Verhalten bei autonomen Aufgaben** — Keine Tests, die spezifisch prüfen, dass bei autonomen Aufgaben die Branch-Erstellung verschoben oder nicht versucht wird.

---

### `AutonomAufgabenInitialisierungsServiceTests`
**Datei:** `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`

#### Testmethoden

- **`InitialisiereAsync_ErzeugtArbeitsverzeichnis()`** — Testet Verzeichniserstellung.

- **`InitialisiereAsync_ErzeugtRepositoryKlon()`** — Testet Klon-Erstellung. **RELEVANZ:** Prüft, dass ein Git-Klon-Befehl aufgerufen wird; es gibt aber keine Assertion darüber, dass danach ein Branch angelegt wird.

- **`InitialisiereAsync_ErzeugtStateJson()`** — Testet state.json-Erstellung. **RELEVANZ:** Prüft, dass `project_branch` im JSON gespeichert wird, aber nicht, dass dieser Branch im Klon existiert.

- **`InitialisiereAsync_ErzeugtPermissionsJson()`** — Testet permissions.json-Erstellung.

- **`InitialisiereAsync_WirftArgumentException_BeiUngueltigemTokenBudget()`** — Input-Validierung.

- **`ErstelleArbeitsverzeichnisStrukturAsync_WirftArgumentException_BeiRelativemPfad()`** — Input-Validierung.

- **`InitialisiereAsync_WirftArgumentException_BeiUngueltigemProjektBranchName()`** — Input-Validierung.

- **`InitialisiereAsync_WirftArgumentException_BeiZuKurzemInitialPrompt()`** — Input-Validierung.

- **`InitialisiereAsync_WirftArgumentException_BeiUngueltigemLaufzeitLimit()`** — Input-Validierung.

- **`InitialisiereAsync_WirftInvalidOperationException_OhneLokalenKlonPfad()`** — **SEHR RELEVANT:** Testet, dass der Service eine `InvalidOperationException` wirft, wenn `aufgabe.LokalerKlonPfad` null ist. Dies ist der Punkt, an dem der Service scheitert, wenn der Klon nicht vorhanden ist.

- **`InitialisiereAsync_WirftInvalidOperationException_BeiFehlgeschlagenemGitKlon()`** — Testet Error-Handling bei Klon-Fehler.

#### Testaufbau

- Verwendet `TestDbContextFactory.Create()` für eine Test-Datenbank
- Erstellt eine `Aufgabe` mit lokalem Klon via `ErstelleAufgabeMitLokalemKlon()`
- Mockt den `ICliRunner` mit erfolgreicher Klon-Simulation

#### FEHLENDE Tests

- **Tests für Branch-Erstellung im Service** — Es gibt keine Tests, die prüfen, dass `InitialisiereAsync()` einen Branch anlegt. Dies ist die fehlende Funktionalität, die hinzugefügt werden muss.
- **Tests für state.json mit existierendem Branch** — Es gibt keine Assertion, dass der Branch im Klon nach `InitialisiereAsync()` existiert.

---

## Hilfsmethoden

### `AutonomAufgabenInitialisierungsServiceTestFactory`
**Datei:** Vermutlich in `src/Softwareschmiede.Tests/Helpers/` oder direkt in der Test-Datei.

- **`CreateCliRunnerMockMitErfolgreichemGitKlon()`** — Erzeugt einen Mock für `ICliRunner`, der erfolgreiche Git-Klon-Operationen simuliert. **Verwendung:** In beiden Test-Klassen verwendet, um Git-Operationen zu mocken.

- **`CreateService(SoftwareschmiededDbContext db, ICliRunner cliRunner)`** — Erstellt eine Instanz von `AutonomAufgabenInitialisierungsService` mit Mocks. **Verwendung:** In beiden Test-Klassen verwendet.

- **`ErstelleProjekt(SoftwareschmiededDbContext db)`** — Erstellt ein Test-Projekt und speichert es in die Datenbank. **Verwendung:** In beiden Test-Klassen verwendet.

- **`ErstelleAufgabeMitLokalemKlon(SoftwareschmiededDbContext db, Guid projektId, string pfad, string titel, string branchName = "main")`** — Erstellt eine Aufgabe mit einem lokalen Klon-Pfad. **Verwendung:** In beiden Test-Klassen verwendet. **RELEVANZ:** Diese Hilfsmethode setzt `aufgabe.LokalerKlonPfad` bereits, um die Tests zu ermöglichen. Dies ist für reguläre Aufgaben korrekt, für autonome Aufgaben sollte der Klon erst durch den Service angelegt werden.

---

## E2E-Tests

### `E2E_AutonomAufgabenInitialisierung.cs`
**Datei:** `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`

**Status:** Nicht vollständig gelesen, aber Dateiname deutet auf E2E-Tests für Dialog hin.

**RELEVANZ:** Diese E2E-Tests könnten die Fehlermeldung reproduzieren, wenn sie versuchen, einen Branch anzulegen.

---

### `E2E_AutonomAufgabenAgentExecution.cs`
**Datei:** `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`

**Status:** Nicht vollständig gelesen.

**RELEVANZ:** Diese E2E-Tests testen die Agent-Ausführung nach erfolgreicher Initialisierung.

---

## Test-Erkenntnisse

1. **Tests für Dialog-Branch-Erstellung sind lückenhaft:** Es gibt keine Unit-Tests, die prüfen, dass `NeuenBranchAnlegenAsync()` bei autonomen Aufgaben (ohne lokalen Klon) korrekt behandelt wird.

2. **Tests für Service-Branch-Erstellung sind nicht vorhanden:** Es gibt keine Assertions, die prüfen, dass `InitialisiereAsync()` einen Branch anlegt.

3. **Die Test-Hilfsmethode `ErstelleAufgabeMitLokalemKlon()` setzt bereits einen Klon-Pfad:** Dies ist für reguläre Aufgaben korrekt, verschleiert aber das eigentliche Problem bei autonomen Aufgaben (wo der Klon noch nicht existiert).

4. **Mock für `ICliRunner` ist bereits vorhanden:** Die Test-Infrastruktur kann erweitert werden, um Branch-Operationen zu mocken.
