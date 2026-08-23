# Umsetzungsplan: Anlageworkflow für autonome Aufgaben

## Übersicht

Der Initialisierungsdialog autonomer Aufgaben versucht derzeit, Projektbranches im lokalen Repository-Klon der Aufgabe anzulegen, bevor die Verzeichnisstruktur überhaupt vorhanden ist. Das eigentliche Kernproblem liegt jedoch tiefer: `AutonomAufgabenInitialisierungsService.KloneHauptRepositoryAsync()` klont von `aufgabe.LokalerKlonPfad` statt direkt von der Repository-URL — dieser Pfad ist aber für frische, nie regulär gestartete autonome Aufgaben `null`, wodurch die gesamte Submit-Kette scheitert.

Die Umsetzung erfordert drei zusammenhängende Änderungen:
1. **Service:** Umstellung von Klon-Quell-Pfad zur direkten Repository-URL; Branch-Erstellung nach dem Klon als neue Service-Methode.
2. **Dialog:** Verzicht auf sofortige Branch-Anlage; stattdessen nur Validierung und Speicherung des Branch-Namens.
3. **Tests:** Unit- und E2E-Tests für die neue Branch-Erstellungslogik im Service und das angepasste Dialog-Verhalten.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Klon-Quelle in `KloneHauptRepositoryAsync()` | Direkte Repository-URL (`aufgabe.GitRepository.RepositoryUrl` via `IGitPlugin.CloneRepositoryAsync()`) statt `aufgabe.LokalerKlonPfad` | Autonome Aufgaben sollen unabhängig von regulären Aufgabe-Starts funktionieren; Analog zu `EntwicklungsprozessService.PrepareCloneDirectoryAsync()`, das bereits das Pattern vorgibt. |
| Branch-Erstellungs-Mechanismus | Direkte `ICliRunner`-Nutzung mit `git branch` (analog `UnteragentGitProvisioningService`) | Low-Level, zuverlässig und getestet; keine neuen Plugin-Dependencies erforderlich. |
| Entscheidungslogik neu/existierend (Remote-Branch) | Service prüft Remote-Branches gegen Liste aus Dialog oder per Git-Abfrage; neu: `git branch`, existierend: `IGitPlugin.CheckoutRemoteBranchAsync()` | Dialogs `LadeProjektBranchesAsync()` lädt bereits Remote-Branches; Service kann diese Information nutzen oder bei Bedarf nochmals abfragen. |
| Dialog-Verhaltensänderung | Kein neuer Parameter/Flag. `NeuenBranchAnlegenAsync()` führt keine Git-Operation mehr aus, sondern validiert nur den eingegebenen Branch-Namen (nicht leer, keine Duplikate in `AvailableProjectBranches`) und übernimmt ihn wie bisher in `AvailableProjectBranches`/`SelectedProjectBranch`. | `AutonomAufgabeInitialisierungsDialogViewModel` ist bereits ausschließlich für autonome Aufgaben bestimmt (Klassendoku) — ein Flag, das stets denselben Wert hätte, wäre eine unnötige Abstraktion (vgl. CLAUDE.md: keine Parameter für hypothetische Fälle). Die einfachste Änderung, die den Fehler behebt, ist, den verfrühten Git-Aufruf ersatzlos zu entfernen. |
| Branch-Anlag-Button im Dialog | Bleibt aktiv und unverändert nutzbar (kein Deaktivieren/Ausblenden). | Der Button funktioniert konzeptionell weiterhin identisch aus Anwendersicht (Name eingeben → erscheint in der Liste); er löst nur keine verfrühte Git-Operation mehr aus. Kein UX-Funktionsverlust, keine zusätzliche UI-Änderung nötig. |

## Programmabläufe

### Korrigierter Workflow: Autonome Aufgabeninitialisierung mit Branch-Erstellung

1. Benutzer klickt "Absenden" im Initialisierungsdialog.
2. Dialog validiert Eingaben (Branch-Name, Prompts, Ressourcenlimits).
3. `AutonomAufgabenInitialisierungsDialogViewModel.BestaetigenAsync()` ruft `AutonomAufgabenInitialisierungsService.InitialisiereAsync(aufgabe, anfrage, ct)` auf.
4. **Service-Orchestrierung:**
   - `ErstelleArbeitsverzeichnisStrukturAsync()` erstellt Verzeichnisstruktur (`skills/`, `clones/`, etc.).
   - **Geändert:** `KloneHauptRepositoryAsync(aufgabe, zielPfad, ct)` klont direkt von `aufgabe.GitRepository.RepositoryUrl` (via `IGitPlugin.CloneRepositoryAsync()`) in `zielPfad`, nicht von `aufgabe.LokalerKlonPfad`.
   - **Neu:** `ErstelleProjektbranchAsync(aufgabe, repoMainPfad, anfrage.ProjektBranchName, ct)` wird aufgerufen:
     - Prüft, ob `anfrage.ProjektBranchName` bereits als Remote-Branch existiert.
     - Wenn nein: `git branch` im `repoMainPfad` (lokale Branch-Erstellung).
     - Wenn ja: `IGitPlugin.CheckoutRemoteBranchAsync()` zum Auschecken des Remote-Branches.
   - `BuildStateJson()` und `BuildPermissionsJson()` erzeugen JSONs mit garantiert existierendem Branch.
   - `AutonomAufgabeKonfiguration` wird erzeugt und in DB gespeichert.

Beteiligte Klassen/Komponenten: `AutonomAufgabenInitialisierungsService`, `AutonomAufgabeInitialisierungsDialogViewModel`, `IGitPlugin`, `ICliRunner`, `AutonomAufgabeKonfiguration`.

### Dialog-Interaktion: Branch-Namenverwaltung

1. `AutonomAufgabeInitialisierungsDialogViewModel.LadeProjektBranchesAsync()` lädt verfügbare Remote-Branches via `IGitPlugin.GetRemoteBranchesAsync()` (funktioniert bereits ohne lokalen Klon).
2. Benutzer kann aus verfügbaren Branches auswählen oder neuen Namen eingeben.
3. **Geändert:** Wenn „+" (Branch anlegen) geklickt wird und der Name bestätigt wird (`NeuenBranchAnlegenAsync()`):
   - Keine Git-Operation mehr (kein `gitPlugin.CreateBranchAsync(_aufgabe.LokalerKlonPfad, ...)`, da zu diesem Zeitpunkt nie ein lokaler Klon existiert).
   - Stattdessen nur Syntax-/Duplikat-Validierung des Namens.
   - Der Name wird wie bisher zu `AvailableProjectBranches` hinzugefügt und als `SelectedProjectBranch` übernommen.
4. `BestaetigenAsync()` erzeugt `AutonomAufgabeInitialisierungsAnfrage` mit `ProjektBranchName` und leitet an Service weiter.

Beteiligte Klassen/Komponenten: `AutonomAufgabeInitialisierungsDialogViewModel`, `AutonomAufgabeInitialisierungsAnfrage`, `IGitPlugin`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `AutonomAufgabenInitialisierungsService` (Service-Klasse)

- **Neue Methode:** `ErstelleProjektbranchAsync(Aufgabe aufgabe, string repoMainPfad, string projektBranchName, CancellationToken ct)`
  - **Zweck:** Erstellt den Projektbranch nach erfolgreicher Klon-Anlage.
  - **Parameter:**
    - `aufgabe` — Die `Aufgabe` mit Zugriff auf `GitRepository.RepositoryUrl`.
    - `repoMainPfad` — Pfad zum geklonten Repository (`clones/repo_main`).
    - `projektBranchName` — Name des anzulegenden Branches.
    - `ct` — Cancellation Token.
  - **Implementierung:**
    1. Prüfe, ob `projektBranchName` bereits als Remote-Branch existiert: `IGitPlugin.GetRemoteBranchesAsync(aufgabe.GitRepository.RepositoryUrl, ct)`.
    2. Falls nein: Rufe `_cliRunner.RunAsync("git", ["branch", projektBranchName], repoMainPfad, null, ct)` auf (wie in `UnteragentGitProvisioningService`).
    3. Falls ja: Rufe `IGitPlugin.CheckoutRemoteBranchAsync(repoMainPfad, projektBranchName, ct)` auf (auschecken bestehender Remote-Branch).
    4. Error-Handling: Werfe `InvalidOperationException` mit aussagekräftiger Fehlermeldung bei Fehler.

- **Geänderte Methode:** `KloneHauptRepositoryAsync(Aufgabe aufgabe, string zielPfad, CancellationToken ct)`
  - **Was ändert sich:** Klont direkt von `aufgabe.GitRepository.RepositoryUrl` statt von `aufgabe.LokalerKlonPfad`.
  - **Neue Implementierung:**
    1. Validiere, dass `aufgabe.GitRepository?.RepositoryUrl` nicht null/leer ist.
    2. Rufe `_gitPlugin.CloneRepositoryAsync(aufgabe.GitRepository.RepositoryUrl, zielPfad, ct)` auf (anstatt `GitKlonHelper.KloneFallsNichtVorhandenAsync()` mit `aufgabe.LokalerKlonPfad`).
    3. Error-Handling: Werfe `InvalidOperationException` bei Fehler (bestehende Logik beibehalten).
  - **Voraussetzung:** Das `IGitPlugin`-Interface muss in der Klasse verfügbar sein (bereits der Fall: Zeile 18, `_gitPlugin`).

- **Geänderte Methode:** `InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct)`
  - **Was ändert sich:** Neue Step nach Klon-Anlage.
  - **Neue Orchestrierungs-Reihenfolge:**
    1. `ErstelleArbeitsverzeichnisStrukturAsync()` (bestehend, Zeile 42).
    2. `KloneHauptRepositoryAsync()` (geändert, Zeile 45).
    3. **Neu:** `ErstelleProjektbranchAsync(aufgabe, repoMainPfad, anfrage.ProjektBranchName, ct)` (neue Zeile nach 45).
    4. `BuildStateJson()` und `BuildPermissionsJson()` und DB-Speicherung (bestehend, Zeilen 49–74).

### `AutonomAufgabeInitialisierungsDialogViewModel` (XAML-ViewModel)

Kein neuer Parameter, keine neue Property, kein XAML-Binding-Änderung — der Button bleibt wie bisher immer aktiv.

- **Geänderte Methode:** `NeuenBranchAnlegenAsync(CancellationToken ct)`
  - **Was ändert sich:** Der Aufruf `await gitPlugin.CreateBranchAsync(_aufgabe.LokalerKlonPfad, NewBranchName, SelectedProjectBranch, ct)` (Zeile 344) sowie die vorausgehende `LokalerKlonPfad`-Prüfung (Zeilen 329–333, inkl. der Fehlermeldung „Kein lokaler Klon der Aufgabe vorhanden…") und die `ResolveGitPlugin()`-Prüfung (Zeilen 335–340) entfallen ersatzlos, da zu diesem Zeitpunkt nie ein lokaler Klon existiert und keine Git-Operation nötig ist.
  - **Neue Implementierung:**
    1. Validiere `NewBranchName` (nicht leer/whitespace; ggf. gültige Branch-Namens-Zeichen).
    2. Prüfe auf Duplikat in `AvailableProjectBranches` (case-insensitive, wie bisher beim Hinzufügen).
    3. Bei Validierungsfehler: `NewBranchError` setzen, `return`.
    4. Bei Erfolg: `AvailableProjectBranches.Add(NewBranchName)` (falls noch nicht enthalten), `SelectedProjectBranch = NewBranchName`, `IsProjectBranchManualInput = false`, `IsCreatingBranch = false`, `NewBranchName = string.Empty` — identisch zum bisherigen Erfolgspfad, nur ohne den vorausgehenden Git-Aufruf.
  - **Kein Try/Catch mehr nötig**, da keine Git-Operation (also keine Exception-Quelle) mehr in der Methode verbleibt.

- **Geänderte Methode:** `LadeProjektBranchesAsync(CancellationToken ct)`
  - **Was ändert sich:** Keine Änderung des Kernverhaltens; bestehender Code ist bereits korrekt (lädt via `IGitPlugin.GetRemoteBranchesAsync()` ohne lokalen Klon).

- **Geänderte Methode:** `BestaetigenAsync(CancellationToken ct)`
  - **Was ändert sich:** Keine Änderung; bestehender Code ist korrekt.
  - **Hinweis:** Dialog-Bestätigung liefert `anfrage.ProjektBranchName`, der dann vom Service angelegt wird.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `aufgabe.GitRepository.RepositoryUrl` (neu geprüft) | Darf nicht null/leer sein | `InvalidOperationException` in `KloneHauptRepositoryAsync()` |
| `projektBranchName` (in `ErstelleProjektbranchAsync()`) | Bereits validiert durch `IstGueltigerBranchName()` in der Anfrage-Validierung; zusätzliche Prüfung: muss nach Klon-Anlage eindeutig sein | `InvalidOperationException` bei Branch-Anlage-Fehler |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Abhängigkeit auf `IGitPlugin` in `KloneHauptRepositoryAsync()`:** Die Methode nutzt nun das Plugin statt `GitKlonHelper`. Dies ist risikoärmer (Plugin ist getestet), ändert aber das Fehlerverhalten: Plugins können andere Fehler werfen als `GitKlonHelper`. → Mitigieren durch umfassende Error-Tests.

- **Branch-Checkout vs. Branch-Erstellung:** Die neue Logik in `ErstelleProjektbranchAsync()` entscheidet, ob `git branch` (neu) oder `CheckoutRemoteBranchAsync()` (existierend) verwendet wird. Fehler-Handling muss beide Fälle abdecken. → Mitigieren durch explizite Fehlerbehandlung und Tests.

- **Abhängigkeit auf Remote-Branch-Liste:** Die Logik in `ErstelleProjektbranchAsync()` lädt Remote-Branches nochmals ab (redundant zu Dialog). Dies ist ineffizient, aber sicher. → Optimierung später möglich; aktuell Trade-off für Sicherheit.

- **E2E-Tests:** Bestehende E2E-Tests in `E2E_AutonomAufgabenInitialisierung.cs`, die einen Branch im Dialog anlegen versuchen, müssen angepasst werden (Branch-Anlage wird jetzt erst bei Submit durchgeführt). → Siehe Tests-Sektion.

- **Reguläre Aufgaben:** `AutonomAufgabenInitialisierungsDialogViewModel` ist nur für autonome Aufgaben bestimmt; reguläre Aufgaben verwenden einen anderen Workflow (`EntwicklungsprozessService`). Keine Seiteneffekte erwartet.

## Umsetzungsreihenfolge

1. **`IGitPlugin.CloneRepositoryAsync()` Verifikation**
   - Voraussetzungen: Keine
   - Beschreibung: Stelle sicher, dass `IGitPlugin.CloneRepositoryAsync(repositoryUrl, zielPfad, ct)` in allen Git-Plugin-Implementierungen (Bitbucket, GitHub, etc.) vorhanden ist und funktioniert. Dies ist bereits der Fall (verwendet in `EntwicklungsprozessService`), aber wird jetzt auch von `AutonomAufgabenInitialisierungsService` genutzt. Keine Codeänderung nötig; nur Verifikation.

2. **`AutonomAufgabenInitialisierungsService.KloneHauptRepositoryAsync()` überarbeiten**
   - Voraussetzungen: `IGitPlugin` Interface bereits vorhanden (in der Klasse `_gitPlugin` injiziert).
   - Beschreibung:
     1. Ändere die Methode, um von `aufgabe.GitRepository.RepositoryUrl` statt `aufgabe.LokalerKlonPfad` zu klonen.
     2. Nutze `_gitPlugin.CloneRepositoryAsync(repositoryUrl, zielPfad, ct)`.
     3. Entferne oder kommentiere alte Logik aus, die auf `aufgabe.LokalerKlonPfad` prüft.
     4. Aktualisiere Fehlermeldungen und Logging.

3. **`AutonomAufgabenInitialisierungsService.ErstelleProjektbranchAsync()` neue Methode**
   - Voraussetzungen: `IGitPlugin` und `ICliRunner` bereits verfügbar; `KloneHauptRepositoryAsync()` überarbeitet.
   - Beschreibung:
     1. Implementiere neue Methode mit Signatur: `ErstelleProjektbranchAsync(Aufgabe aufgabe, string repoMainPfad, string projektBranchName, CancellationToken ct)`.
     2. Logik:
        - Lade Remote-Branches: `_gitPlugin.GetRemoteBranchesAsync(aufgabe.GitRepository.RepositoryUrl, ct)`.
        - Falls `projektBranchName` in Remote-Branches: `_gitPlugin.CheckoutRemoteBranchAsync(repoMainPfad, projektBranchName, ct)`.
        - Sonst: `_cliRunner.RunAsync("git", ["branch", projektBranchName], repoMainPfad, null, ct)`.
        - Error-Handling mit aussagekräftigen Fehlermeldungen.

4. **`AutonomAufgabenInitialisierungsService.InitialisiereAsync()` Orchestrierung anpassen**
   - Voraussetzungen: `ErstelleProjektbranchAsync()` implementiert.
   - Beschreibung:
     1. Nach `KloneHauptRepositoryAsync()` (Zeile 45) neue Zeile: `await ErstelleProjektbranchAsync(aufgabe, repoMainPfad, anfrage.ProjektBranchName, ct);`.
     2. Testen, dass die Reihenfolge korrekt ist und keine Exception-Handling-Issues entstehen.

5. **`AutonomAufgabeInitialisierungsDialogViewModel.NeuenBranchAnlegenAsync()` vereinfachen**
   - Voraussetzungen: Keine (unabhängig von den Service-Änderungen editierbar).
   - Beschreibung:
     1. Git-Aufruf (`gitPlugin.CreateBranchAsync(...)`), die vorausgehende `LokalerKlonPfad`-Prüfung und die `ResolveGitPlugin()`-Prüfung entfernen.
     2. Durch Namens-/Duplikat-Validierung ersetzen (siehe „Änderungen an bestehenden Klassen" oben).
     3. Kein XAML ändern — Button bleibt unverändert immer aktiv.

6. **Unit-Tests schreiben: `AutonomAufgabenInitialisierungsServiceTests`**
   - Voraussetzungen: Service-Änderungen abgeschlossen; Testinfrastruktur (`ICliRunner`-Mock, `IGitPlugin`-Mock) vorhanden.
   - Beschreibung:
     1. **Neuer Test:** `InitialisiereAsync_KlontDirectVonRepositoryUrl()` — Prüft, dass `KloneHauptRepositoryAsync()` `_gitPlugin.CloneRepositoryAsync()` mit korrekter URL aufruft.
     2. **Neuer Test:** `InitialisiereAsync_ErstelltProjektBranchNachKlon()` — Prüft, dass nach Klon `ErstelleProjektbranchAsync()` aufgerufen wird.
     3. **Neuer Test:** `ErstelleProjektbranchAsync_AnlegtNeuenBranchMitGit()` — Prüft, dass `git branch` aufgerufen wird, wenn Branch nicht remote existiert.
     4. **Neuer Test:** `ErstelleProjektbranchAsync_CheckoutRemoteBranch_WennExistent()` — Prüft, dass `CheckoutRemoteBranchAsync()` aufgerufen wird, wenn Branch remote existiert.
     5. **Neuer Test:** `ErstelleProjektbranchAsync_WirftException_BeiGitFehler()` — Error-Handling prüfen.
     6. **Anpassung:** `InitialisiereAsync_WirftInvalidOperationException_OhneLokalenKlonPfad()` kann entfernt/angepasst werden (ist nicht mehr relevant, da Klon nicht von `LokalerKlonPfad` abhängt).

7. **Unit-Tests schreiben: `AutonomAufgabeInitialisierungsDialogViewModelTests`**
   - Voraussetzungen: Dialog-Anpassungen abgeschlossen; Testinfrastruktur vorhanden.
   - Beschreibung:
     1. **Neuer Test:** `NeuenBranchAnlegenAsync_UebernimmtBranchName_OhneGitAufruf()` — Prüft, dass der Branch-Name ohne Klon/Git-Plugin übernommen wird.
     2. **Neuer Test:** `NeuenBranchAnlegenAsync_SetztFehler_BeiDuplikatOderLeeremNamen()` — Prüft Validierungsfehler.
     3. **Anpassung:** Bestehende Tests, die den alten Git-Aufruf/die alte Fehlermeldung „Kein lokaler Klon der Aufgabe vorhanden…" für `NeuenBranchAnlegenAsync()` erwarten, müssen entsprechend angepasst oder entfernt werden.

8. **E2E-Tests anpassen/neu schreiben**
   - Voraussetzungen: Unit-Tests grün; Dialog und Service-Logik funktional.
   - Beschreibung:
     1. **Bestehende Anpassung:** E2E-Tests in `E2E_AutonomAufgabenInitialisierung.cs`, die „Branch im Dialog anlegen" prüfen, müssen angepasst werden: Branch-Anlage erfolgt jetzt beim Submit, nicht sofort im Dialog.
     2. **Neuer E2E-Test:** Happy-Path-Szenario: Dialog öffnen → Branch-Name eingeben → Submit → Prüfen, dass Branch nach erfolgreicher Initialisierung im Klon existiert.
     3. **Neuer E2E-Test:** Bestehender Remote-Branch: Dialog öffnen → Bestehenden Branch aus Liste auswählen → Submit → Prüfen, dass Checkout erfolgreich war.
     4. **Neuer E2E-Test:** Fehlschlag bei Branch-Erstellung: Service-Mock so konfigurieren, dass Klon erfolgreich, aber Branch-Anlage fehlschlägt → Prüfen, dass Initialisierung rückgängig gemacht oder mit Fehler gemeldet wird.

9. **Dokumentationsanpassungen unter `docs/help/`**
   - Voraussetzungen: Alle Tests grün; Implementierung stabil.
   - Beschreibung: Prüfe, ob Help-Dokumentationen existieren, die das autonome Aufgaben-Workflow beschreiben, und aktualisiere diese, wenn nötig:
     1. Falls Dokumentation das Dialog-Verhalten beschreibt: Anpassen, dass Branch erst beim Submit angelegt wird.
     2. Falls Dokumentation Fehlermeldungen beschreibt: Alte Fehlermeldung "Kein lokaler Klon vorhanden" ist jetzt nicht mehr relevant (sollte nicht auftreten).
     3. Falls Dokumentation Troubleshooting enthält: Aktualisieren oder neue Sektion für Branch-Anlage-Fehler hinzufügen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `InitialisiereAsync_KlontDirectVonRepositoryUrl()` | `AutonomAufgabenInitialisierungsServiceTests` | Prüft, dass `KloneHauptRepositoryAsync()` `IGitPlugin.CloneRepositoryAsync()` mit `aufgabe.GitRepository.RepositoryUrl` aufruft. |
| `InitialisiereAsync_ErstelltProjektBranchNachKlon()` | `AutonomAufgabenInitialisierungsServiceTests` | Prüft, dass nach `KloneHauptRepositoryAsync()` `ErstelleProjektbranchAsync()` aufgerufen wird. |
| `ErstelleProjektbranchAsync_AnlegtNeuenBranchMitGit()` | `AutonomAufgabenInitialisierungsServiceTests` | Prüft, dass `ICliRunner.RunAsync("git", ["branch", ...], repoMainPfad, ...)` aufgerufen wird, wenn Branch nicht remote existiert. |
| `ErstelleProjektbranchAsync_CheckoutRemoteBranch_WennExistent()` | `AutonomAufgabenInitialisierungsServiceTests` | Prüft, dass `IGitPlugin.CheckoutRemoteBranchAsync()` aufgerufen wird, wenn Branch remote existiert. |
| `ErstelleProjektbranchAsync_WirftException_BeiGitFehler()` | `AutonomAufgabenInitialisierungsServiceTests` | Prüft Error-Handling bei `git branch`-Fehler. |
| `NeuenBranchAnlegenAsync_UebernimmtBranchName_OhneGitAufruf()` | `AutonomAufgabeInitialisierungsDialogViewModelTests` | Prüft, dass der Branch-Name ohne Klon/Git-Plugin übernommen wird (kein `LokalerKlonPfad`, kein `IGitPlugin`-Mock-Aufruf) und in `AvailableProjectBranches`/`SelectedProjectBranch` landet. |
| `NeuenBranchAnlegenAsync_SetztFehler_BeiDuplikatOderLeeremNamen()` | `AutonomAufgabeInitialisierungsDialogViewModelTests` | Prüft Validierungsfehler bei leerem Namen bzw. Duplikat in `AvailableProjectBranches`. |
| `E2E_AutonomAufgabenInitialisierung_HappyPath_NewBranch()` | `E2E_AutonomAufgabenInitialisierung.cs` | End-to-End: Dialog → Branch-Name eingeben → Submit → Prüfen, dass Branch existiert. |
| `E2E_AutonomAufgabenInitialisierung_ExistingRemoteBranch()` | `E2E_AutonomAufgabenInitialisierung.cs` | End-to-End: Dialog → Bestehenden Branch auswählen → Submit → Prüfen, dass Checkout erfolgt. |
| `E2E_AutonomAufgabenInitialisierung_BranchCreationFailure()` | `E2E_AutonomAufgabenInitialisierung.cs` | End-to-End: Fehler bei Branch-Erstellung → Prüfen, dass Fehlerbehandlung korrekt. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `InitialisiereAsync_WirftInvalidOperationException_OhneLokalenKlonPfad()` | Test ist nicht mehr relevant: Service hängt nicht von `aufgabe.LokalerKlonPfad` ab. Kann entfernt oder in "TestAltesVerhalten" umbenannt werden. |
| `InitialisiereAsync_ErzeugtRepositoryKlon()` | Anpassung: Mock-Setup muss `IGitPlugin.CloneRepositoryAsync()` statt `GitKlonHelper`-Logik mocken. |
| `InitialisiereAsync_ErzeugtStateJson()` | Keine Änderung nötig: Test prüft JSON-Struktur, nicht Branch-Existierung. |
| E2E-Tests in `E2E_AutonomAufgabenInitialisierung.cs` (falls vorhanden) | Anpassung: Szenarien, die „Branch im Dialog anlegen" testen, müssen angepasst werden: Branch wird jetzt erst beim Submit angelegt. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy-Path: Dialog → Neuer Branch eingeben → Submit | `E2E_AutonomAufgabenInitialisierung.cs` | Branch wird beim Submit angelegt; `state.json` verweist auf existierenden Branch. |
| Bestehender Remote-Branch: Dialog → Auswählen → Submit | `E2E_AutonomAufgabenInitialisierung.cs` | Checkout von Remote-Branch funktioniert; Initialisierung erfolgreich. |
| Branch-Anlage-Fehler: Submit mit fehlerhafte Branch-Erstellung | `E2E_AutonomAufgabenInitialisierung.cs` | Fehlerbehandlung: Fehler wird korrekt gemeldet; Rollback oder Fehler-State wird korrekt gesetzt. |

Bestehende E2E-Tests, die angepasst werden müssen:
- Alle Tests in `E2E_AutonomAufgabenInitialisierung.cs`, die versuchen, einen Branch im Dialog anzulegen (wenn vorhanden).
- Anpassung: Statt im Dialog einen Branch anzulegen, Submit mit Branch-Namen durchführen und dann nach Submit prüfen, dass Branch existiert.

## Offene Punkte

Keine. Alle kritischen Punkte wurden durch die Bestandsaufnahme und ihre Korrektur-/Zusatzbefunde geklärt:
- Klon-Quelle: Direkt von Repository-URL via `IGitPlugin.CloneRepositoryAsync()`.
- Dialog-Verhalten: Kein sofortiger Git-Aufruf mehr im Dialog; Button bleibt aktiv, `NeuenBranchAnlegenAsync()` validiert nur noch den Namen.
- Branch-Erstellung: Neue Methode `ErstelleProjektbranchAsync()` nach Klon mit Logik für neu/existierend.
- Unterscheidung Autonom/Regulär: Nicht erforderlich; `AutonomAufgabeInitialisierungsDialogViewModel` ist per Definition nur für autonome Aufgaben.
- Entscheidung neu/existierend: Service prüft Remote-Branches und wählt entsprechende Operation.
