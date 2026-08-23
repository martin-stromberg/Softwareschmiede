# Anforderungsübersetzung: Anlageworkflow für autonome Aufgaben

## Fachliche Zusammenfassung

Der Initialisierungsdialog autonomer Aufgaben versucht derzeit, Projektbranches im lokalen Repository-Klon der Aufgabe anzulegen, bevor die Verzeichnisstruktur der autonomen Aufgabe überhaupt vorhanden ist. Dies führt zur Fehlermeldung „Kein lokaler Klon der Aufgabe vorhanden". Bei autonomen Aufgaben muss der gesamte Workflow verschoben werden: Die Verzeichnisstruktur einschließlich des Repository-Klons und der darin erzeugten Projektbranches wird erst beim finalen Absenden (Submit) des Initialisierungsdialogs erstellt, nicht während einzelner Interaktionen im Dialog selbst.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- `AutonomAufgabeKonfiguration` (bestehend, ggf. neue Felder für Branch-Status)
- `AutonomAufgabeInitialisierungsAnfrage` (bestehend)

### Logikklassen / Services
- `AutonomAufgabenInitialisierungsService` — orchestriert die Erstellung der Verzeichnisstruktur, des Klons und der Initialisialisierung; muss erweitert werden, um Projektbranch-Erstellung zu unterstützen
- `AutonomAufgabeInitialisierungsDialogViewModel` — Dialog-Logik; der Branch-Anleg-Befehl (`CreateBranchCommand`, `NeuenBranchAnlegenAsync()`) muss angepasst werden, damit er im autonomen Fall nicht versucht, im nicht-existenten lokalen Klon einen Branch zu erstellen
- `UnteragentGitProvisioningService` — legt Branches für Unteragenten an; arbeitet bereits mit vorhandenem `repoMainPfad`, daher nicht direkt betroffen, aber relevant für die konzeptionelle Konsistenz

### Interfaces
- Keine neuen Interfaces erforderlich

### Enums
- Keine neuen Enums erforderlich

### UI-Komponenten
- Dialog (XAML) — ggf. UI-Anpassung, um User-Feedback zu geben, dass Branch erst bei Submit angelegt wird

### Tests
- `AutonomAufgabeInitialisierungsDialogViewModelTests` — prüfen des überarbeiteten Branch-Anlag-Verhaltens
- `AutonomAufgabenInitialisierungsServiceTests` — prüfen, dass Branch-Erstellung Teil der `InitialisiereAsync()`-Orchestrierung ist
- E2E-Tests in `E2E_AutonomAufgabenAgentExecution.cs` (falls betroffen)

## Implementierungsansatz

### Kernproblem
Derzeit wird im `AutonomAufgabeInitialisierungsDialogViewModel.NeuenBranchAnlegenAsync()` (Zeilen 325–363) die Methode `gitPlugin.CreateBranchAsync(_aufgabe.LokalerKlonPfad, NewBranchName, SelectedProjectBranch, ct)` aufgerufen. Diese prüft, ob `_aufgabe.LokalerKlonPfad` existiert; bei autonomen Aufgaben ist dies jedoch falsch, da der Klon erst später erstellt wird.

### Lösungsansatz

1. **Im Dialog-ViewModel (`AutonomAufgabeInitialisierungsDialogViewModel`)**
   - Umstellung des Verhaltens: Die Branch-Erstellung während des Dialogs muss validieren, dass der Branch-Name gültig ist (ohne ihn anzulegen).
   - Wahlweise: Zur Unterscheidung zwischen regulären und autonomen Aufgaben das `Aufgabe`-Objekt prüfen (z. B. über einen neuen Modus-Indikator oder durch Feststellen, ob dies eine autonome Aufgabe ist).
   - Der eingegebene Branch-Name wird bis zum Absenden des Dialogs nur im ViewModel gespeichert.

2. **Im Initialisierungsservice (`AutonomAufgabenInitialisierungsService`)**
   - Erweitern von `InitialisiereAsync()`, um nach dem Erstellen des Klons (`KloneHauptRepositoryAsync()`) den Projektbranch anzulegen.
   - Neue Methode `ErstelleProjektbranchAsync(aufgabe, repoMainPfad, branchName, ct)` — nutzt die Git-CLI wie `UnteragentGitProvisioningService` es tut.
   - Dies stellt sicher, dass der Branch erst existiert, wenn der Klon vorhanden ist.

3. **Workflow-Reihenfolge**
   - Dialog zeigt Eingabefelder für Branch-Namen; Validierung nur auf Syntax (keine Git-Operation).
   - Benutzer klickt "Absenden" (BestaetigenAsync).
   - `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` wird aufgerufen:
     1. Verzeichnisstruktur erstellen (`ErstelleArbeitsverzeichnisStrukturAsync()`).
     2. Hauptrepository klonen (`KloneHauptRepositoryAsync()`).
     3. **Neu:** Projektbranch im geklonten Repo anlegen.
     4. state.json und permissions.json schreiben.

### Abhängigkeiten
- `AutonomAufgabenInitialisierungsService` benötigt Zugriff auf eine Git-Operation zum Branch-Anlegen (entweder über `ICliRunner` oder über `IGitPlugin`).
- Der Dialog-ViewModel muss erkennen, dass es sich um eine autonome Aufgabe handelt (über `Aufgabe`-Eigenschaften oder einen neuen Parameter).

## Konfiguration
Keine neue Konfiguration erforderlich. Das Verhalten wird durch die Aufgabe-Klassifikation (autonom vs. regulär) gesteuert.

## Offene Fragen

1. **Unterscheidung Autonom vs. Regulär im Dialog:** Wie wird festgestellt, ob `Aufgabe` autonom ist?
   - Option A: Neue `IsAutonom`-Property auf `Aufgabe`?
   - Option B: Prüfung, ob bereits eine `AutonomAufgabeKonfiguration` existiert?
   - Option C: Separate Code-Pfade basierend auf dem Aufruf-Kontext (z. B. ob der Dialog über `AutonomAufgabeStartService` oder einen anderen Service aufgerufen wird)?

2. **Git-Operation für Branch-Erstellung:** Sollte `AutonomAufgabenInitialisierungsService` die gleiche Methode wie `UnteragentGitProvisioningService` nutzen (über `ICliRunner` mit `git branch`), oder ein eigenes Interface/Plugin?

3. **Error-Handling:** Wenn die Branch-Erstellung bei Submit fehlschlägt, wird die gesamte Initialisierung rückgängig gemacht? Oder wird sie mit einem Default-Branch-Namen fortgesetzt?

4. **UI-Feedback:** Sollte der Dialog dem Benutzer mitteilen, dass die Branch-Erstellung erst beim Submit stattfindet (z. B. mit einer Info-Meldung)?
