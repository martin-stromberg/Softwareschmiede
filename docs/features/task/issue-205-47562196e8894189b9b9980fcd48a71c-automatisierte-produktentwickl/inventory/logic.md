# Logikklassen und Services

## `AutonomAufgabeInitialisierungsDialogViewModel`
**Datei:** `src/Softwareschmiede.App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Initialize(Aufgabe aufgabe)` | `public` | Initialisiert den Dialog mit der Zielaufgabe; setzt `_aufgabe`, `SelectedProjectBranch` auf `aufgabe.BranchName` |
| `LadeAsync(CancellationToken ct)` | `public async Task` | Lädt verfügbare Remote-Branches des Repositories und verfügbare Promptvorlagen |
| `LadeProjektBranchesAsync(CancellationToken ct)` | `private async Task` | Ruft Remote-Branches vom Git-Plugin ab; setzt `AvailableProjectBranches` oder `IsProjectBranchManualInput = true` |
| `LadePromptVorlagenAsync(CancellationToken ct)` | `private async Task` | Lädt Promptvorlagen aus dem Service |
| `ResolveGitPlugin()` | `private IGitPlugin?` | Sucht das Git-Plugin anhand des Repository-Typs |
| `ZeigeBranchAnlegen()` | `private void` | Setzt `IsCreatingBranch = true`; zeigt Eingabefelder für neuen Branch an |
| `AbbrechenBranchAnlegen()` | `private void` | Setzt `IsCreatingBranch = false`; versteckt Eingabefelder |
| **`NeuenBranchAnlegenAsync(CancellationToken ct)`** | `private async Task` | **KRITISCH FÜR DIESEN ISSUE:** Zeilen 325-363. Versuch, sofort im Dialog einen Branch anzulegen. **Problem-Stelle: Zeile 329-332** — prüft, ob `_aufgabe.LokalerKlonPfad` gesetzt ist; wenn nicht, wird die Fehlermeldung "Kein lokaler Klon der Aufgabe vorhanden; Branch kann nicht angelegt werden." gesetzt. Bei autonomen Aufgaben ist dieser Pfad zu diesem Zeitpunkt noch `null`! |
| `BestaetigenAsync(CancellationToken ct)` | `public async Task` | Validiert Eingaben und ruft `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` auf (Zeile 397). Erzeugt `AutonomAufgabeInitialisierungsAnfrage` mit Dialog-Werten (Zeilen 385-395) |
| `Abbrechen()` | `public void` | Schließt den Dialog ohne Erstellung der Autonomen Aufgabe; löst `CloseRequested(false)` aus |
| `ValidiereEingaben()` | `private string?` | Validiert `InitialPrompt`, `TokenBudget`, `RuntimeLimitMinutes` auf Syntax-Ebene |

**Abonnierte Events:** Keine

**Publizierte Events:**
- `CloseRequested` (EventHandler<bool>) — Wird ausgelöst, wenn Dialog geschlossen werden soll. Parameter: `true` = erfolgreich, `false` = abgebrochen.

**Relevanz für Issue:**
- `NeuenBranchAnlegenAsync()` ist die Kernproblematik: Sie versucht, sofort einen Branch im nicht-existenten lokalen Klon anzulegen.
- `BestaetigenAsync()` ist der richtige Aufruf-Punkt, um den Initialisierungsservice aufzurufen, der dann die Branch-Erstellung durchführen sollte.
- Die Unterscheidung zwischen autonomen und regulären Aufgaben ist im ViewModel nicht vorhanden; es kann nicht erkennen, dass es sich um eine autonome Aufgabe handelt (da `Aufgabe.AutonomKonfiguration` noch nicht existiert).

---

## `AutonomAufgabenInitialisierungsService`
**Datei:** `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| **`InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct)`** | `public async Task<AutonomAufgabeKonfiguration>` | **ORCHESTRIERUNGS-KERNPUNKT:** Zeilen 36-82. Aktueller Ablauf: 1. `ErstelleArbeitsverzeichnisStrukturAsync()` (Zeile 42) → 2. `KloneHauptRepositoryAsync()` (Zeile 45) → 3. Schreibe state.json und permissions.json (Zeilen 49-57) → 4. Erstelle `AutonomAufgabeKonfiguration` und speichere in DB (Zeilen 59-74). **FEHLENDE STEP:** Branch-Erstellung nach dem Klon und vor/nach state.json! |
| `ErstelleArbeitsverzeichnisStrukturAsync(string arbeitsverzeichnisPfad, CancellationToken ct)` | `public async Task` | Zeilen 85-120. Erstellt die Verzeichnisstruktur mit `skills/`, `skills/archive/`, `clones/`, `tasks/`, `logs/` und die Dateien `plan.md`, `progress.md`, `governance.md`. |
| `KloneHauptRepositoryAsync(Aufgabe aufgabe, string zielPfad, CancellationToken ct)` | `private Task` | Zeilen 138-154. Klont das Hauptrepository (aus `aufgabe.LokalerKlonPfad`) in den `zielPfad` (typischerweise `clones/repo_main`). **Problem:** Erwartet, dass `aufgabe.LokalerKlonPfad` bereits gesetzt ist! Bei autonomen Aufgaben muss der Klon vor dem Service-Aufruf angelegt werden (typischerweise durch einen anderen Service für reguläre Aufgaben). |
| `SicherstelleAufgabeGetrackt(Aufgabe aufgabe)` | `private void` | Zeilen 129-136. Stellt sicher, dass die `Aufgabe` im EF-ChangeTracker getrackt ist, damit Relationship-Fixup zwischen `AutonomAufgabeKonfiguration.AufgabeId` und `Aufgabe.AutonomKonfiguration` greift. |
| `BuildPermissionsJson(AutonomAufgabeInitialisierungsAnfrage anfrage)` | `private string` | Zeilen 156-181. Erzeugt JSON für permissions.json mit allowed_actions und limits. |
| `BuildStateJson(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage)` | `private string` | Zeilen 183-230. Erzeugt JSON für state.json mit task_id, project_branch, runtime, governance, clones, subagents, skills, progress, pull_request, flags. **KRITISCH:** `project_branch` wird gesetzt auf `anfrage.ProjektBranchName`, aber es gibt keine Garantie, dass dieser Branch im `clones/repo_main`-Klon bereits existiert! |
| `BuildGovernanceMarkdown()` | `private static string` | Zeilen 232-242. Erzeugt Markdown für governance.md. |
| `ValidiereAnfrage(AutonomAufgabeInitialisierungsAnfrage anfrage)` | `private static void` | Zeilen 244-270. Validiert `ProjektBranchName`, `InitialPrompt`, `TokenBudget`, `LaufzeitLimitMinuten`, `ArbeitsverzeichnisPfad` auf Syntax-Ebene. |
| `IstGueltigerBranchName(string branchName)` | `private static bool` | Zeilen 272-281. Validiert Branch-Namen nach Git-Regeln (keine Leerzeichen, ~, ^, :, ?, *, [, \, keine / am Anfang/Ende, kein . am Ende, kein ..). |

**Abonnierte Events:** Keine

**Publizierte Events:** Keine

**Relevanz für Issue:**
- Dies ist der Service, der die Branch-Erstellung orchestrieren sollte.
- Aktuell wird kein Branch angelegt; `state.json` verweist auf einen `project_branch`, der möglicherweise nicht existiert.
- Nach `KloneHauptRepositoryAsync()` (Zeile 45) sollte eine neue Methode (z.B. `ErstelleProjektbranchAsync()`) aufgerufen werden, um den Branch im geklonten Repo anzulegen.
- Der Service hat bereits alle notwendigen Informationen: `repoMainPfad` (Zeile 44), `anfrage.ProjektBranchName`, und Zugriff auf `_cliRunner`.

---

## `UnteragentGitProvisioningService`
**Datei:** `src/Softwareschmiede/Application/Services/UnteragentGitProvisioningService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProvisioniereAsync(UnteragentSpezifikation unteragent, string repoMainPfad, CancellationToken ct)` | `public async Task` | Zeilen 24-48. Orchestriert die Provisioning eines Unteragenten: 1. Erstelle Arbeitsverzeichnis (Zeile 28-31) → 2. **Lege Branch an** (Zeile 34, GitKommando: `git branch <branchName>`) → 3. Klone Branch (Zeilen 40-47). **MUSTER FÜR AUTONOME AUFGABEN:** Die Branch-Erstellung (Zeile 34) ist das direkte Vorbild für die fehlende Step in `AutonomAufgabenInitialisierungsService`. |

**Verwendeter CLI-Befehl für Branch-Erstellung (Zeile 34):**
```csharp
await _cliRunner.RunAsync("git", ["branch", unteragent.GitArbeitsbereich.BranchName], repoMainPfad, null, ct);
```

**Abonnierte Events:** Keine

**Publizierte Events:** Keine

**Relevanz für Issue:**
- Dieses Service zeigt das bewährte Muster für Branch-Erstellung.
- Wird nach `KloneHauptRepositoryAsync()` ausgeführt, wenn der Klon bereits vorhanden ist.
- Die gleiche Logik sollte in `AutonomAufgabenInitialisierungsService` wiederverwendet werden.

---

## `AufgabeExtensions`
**Datei:** `src/Softwareschmiede/Domain/Entities/AufgabeExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IstAutonom(this Aufgabe aufgabe)` | `public static bool` | Zeile 20. Gibt an, ob die Aufgabe eine Autonome Aufgabe ist, indem geprüft wird, ob `aufgabe.AutonomKonfiguration is not null`. **WARNUNG (Zeile 11-16):** Diese Methode funktioniert nur, wenn `AutonomKonfiguration` geladen ist (via `.Include()`) oder die `Aufgabe` im gleichen `DbContext` bereits getrackt wurde. Bei `AsNoTracking()`-Queries ohne `Include` liefert sie fälschlich `false`. |

**Relevanz für Issue:**
- Diese Methode könnte als Unterscheidungskriterium verwendet werden, funktioniert aber nur nach dem erfolgreichen Absenden des Dialogs.
- **Während des Dialogs** existiert `AutonomAufgabeKonfiguration` noch nicht, daher kann `IstAutonom()` nicht verwendet werden, um zu erkennen, dass es sich um eine autonome Aufgabe handelt.
- Der Dialog-ViewModel hat aktuell keine Möglichkeit, zu erkennen, ob die Aufgabe autonom oder regulär ist.
