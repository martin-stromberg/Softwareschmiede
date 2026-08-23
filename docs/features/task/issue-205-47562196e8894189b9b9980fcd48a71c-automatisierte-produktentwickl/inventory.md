# Bestandsaufnahme: Initialisierungsworkflow autonomer Aufgaben — Branch-Anlagenlogik

## Korrektur / Kritischer Zusatzbefund (nach unabhängiger Verifikation)

Die folgenden zwei Punkte wurden nach der ursprünglichen Bestandsaufnahme durch direkte Code-Prüfung
ergänzt bzw. korrigiert und **verändern den Umfang der notwendigen Änderung erheblich**:

1. **Das eigentliche Kernproblem liegt tiefer als nur beim "Anlegen"-Button.**
   `AutonomAufgabenInitialisierungsService.KloneHauptRepositoryAsync()` (Zeilen 138–154) klont **nicht**
   direkt von der Remote-Repository-URL, sondern verwendet `aufgabe.LokalerKlonPfad` als Klon-**Quelle**:
   ```csharp
   private Task KloneHauptRepositoryAsync(Aufgabe aufgabe, string zielPfad, CancellationToken ct)
   {
       if (string.IsNullOrWhiteSpace(aufgabe.LokalerKlonPfad))
       {
           throw new InvalidOperationException(
               $"Aufgabe {aufgabe.Id} besitzt keinen lokalen Klon-Pfad; Repository-Klon für die Autonome Aufgabe kann nicht erstellt werden.");
       }
       return GitKlonHelper.KloneFallsNichtVorhandenAsync(_cliRunner, aufgabe.LokalerKlonPfad, zielPfad, branch: null, _logger, ..., ct);
   }
   ```
   `Aufgabe.LokalerKlonPfad` wird laut Kommentar in `AutonomAufgabeStartService.StarteAsync()` (Zeile 37)
   **ausschließlich beim Starten einer regulären (nicht-autonomen) Aufgabe gesetzt**
   (`EntwicklungsprozessService.PrepareCloneDirectoryAsync()`, Zeile 495: `gitPlugin.CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad, ct)`).
   Für eine frische Aufgabe, die nie regulär gestartet wurde, ist `LokalerKlonPfad` also `null` — **die
   gesamte `InitialisiereAsync()`-Submit-Kette scheitert dann**, nicht nur die Branch-Erstellung im
   Dialog. Das ist exakt der vom Anwender beschriebene Kern-Missstand: die Konzeption verlangt, dass der
   Klon für eine autonome Aufgabe **direkt von der Repository-URL** angelegt wird (analog zu
   `EntwicklungsprozessService.PrepareCloneDirectoryAsync()`, welches `gitPlugin.CloneRepositoryAsync(repositoryUrl, lokalerKlonPfad, ct)`
   nutzt), nicht durch Klonen eines bereits vorhandenen anderen lokalen Klons.
   **Konsequenz für die Planung:** `KloneHauptRepositoryAsync()` muss so umgebaut werden, dass es die
   Repository-URL der Aufgabe (`aufgabe.GitRepository.RepositoryUrl`, per `IGitPlugin.CloneRepositoryAsync`)
   als Quelle verwendet und **nicht** von `aufgabe.LokalerKlonPfad` abhängt. Erst danach ist der
   „Klon existiert noch nicht"-Fehlerfall der Branch-Erstellung im Dialog überhaupt der einzige verbleibende
   Blocker.

2. **Korrektur zu „Frage 1" (Unterscheidung Autonom vs. Regulär):** Nicht erforderlich.
   `AutonomAufgabeInitialisierungsDialogViewModel` ist laut Klassendokumentation (Zeile 14: "ViewModel für
   den Initialisierungsdialog einer Autonomen Aufgabe") **ausschließlich** für autonome Aufgaben bestimmt.
   Es gibt keinen gemeinsamen Dialog mit dem regulären Aufgaben-Workflow. Jeder Aufruf von
   `NeuenBranchAnlegenAsync()` in dieser Klasse ist implizit bereits im autonomen Kontext — es muss also
   **keine** Unterscheidung Autonom/Regulär eingeführt werden. Der Fix kann sich rein darauf beschränken,
   dass dieser Dialog niemals versucht, lokal (im noch nicht existierenden Klon) einen Branch anzulegen,
   sondern den gewünschten Branch-Namen nur validiert/merkt und die eigentliche Git-Branch-Operation dem
   `AutonomAufgabenInitialisierungsService` nach dem (korrigierten) Klon-Schritt überlässt.

3. **Offene Detailfrage für die Planung (aus der Dialog-Logik):** `LadeProjektBranchesAsync()`
   (Zeilen 237–273) lädt bestehende Remote-Branches über `gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct)`
   — das funktioniert bereits **ohne** lokalen Klon. Der Dialog erlaubt sowohl Auswahl eines bestehenden
   Branches als auch Eingabe eines neuen Branch-Namens über „Anlegen" (fügt ihn nur der lokalen
   `AvailableProjectBranches`-Liste hinzu). `AutonomAufgabeInitialisierungsAnfrage.ProjektBranchName` trägt
   am Ende nur den gewählten Namen, aber **keine Information, ob dieser neu ist oder bereits remote
   existiert**. Der Service muss beim Anlegen also selbst prüfen (z. B. gegen die bereits geladene
   Remote-Branch-Liste oder per Git-Check), ob `git branch <name>` (neu) oder
   `IGitPlugin.CheckoutRemoteBranchAsync()` (bestehend) korrekt ist. Diese Entscheidung sollte die Planung
   explizit treffen.

## Kurzzusammenfassung

Der Initialisierungsdialog für autonome Aufgaben versucht derzeit, Projektbranches sofort im lokalen Repository-Klon der Aufgabe anzulegen (beim Klick auf "Anlegen" im Dialog), obwohl bei autonomen Aufgaben dieser Klon zu diesem Zeitpunkt noch gar nicht existiert. Der Klon wird erst beim finalen Absenden des Dialogs durch `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` angelegt. Dies führt zur Fehlermeldung „Kein lokaler Klon der Aufgabe vorhanden; Branch kann nicht angelegt werden." (exakte Meldung: Zeile 331 in `AutonomAufgabeInitialisierungsDialogViewModel.cs`).

Der Workflow muss so verschoben werden, dass die Branch-Erstellung erst **nach** der Klon-Erstellung erfolgt, nicht während des Dialog-Interaktionen.

---

## Zusammenfassung der Befunde

### Problem-Zentren

1. **Fehler-Ort:** `AutonomAufgabeInitialisierungsDialogViewModel.NeuenBranchAnlegenAsync()`, Zeilen 325–363
   - Zeilen 329–332: Prüfung auf `LokalerKlonPfad` == null → Fehlermeldung

2. **Fehlende Funktionalität:** Branch-Erstellung im `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`
   - Aktuell: Verzeichnisstruktur → Klon → state.json/permissions.json
   - Erwartet: Verzeichnisstruktur → Klon → **Branch anlegen** → state.json/permissions.json

3. **Unterscheidungs-Problem:** Der Dialog-ViewModel kann während der Initialisierung nicht erkennen, ob die Aufgabe autonom oder regulär ist
   - `Aufgabe.AutonomKonfiguration` existiert noch nicht (wird erst nach erfolgreichem Dialog erstellt)
   - `IstAutonom()`-Erweiterungsmethode funktioniert nur nach Dialog-Completion

4. **Fehlende Tests:** Keine Unit-Tests, die das Branch-Anlag-Verhalten bei autonomen Aufgaben prüfen

### Bestandsaufnahme der Komponenten

| Komponente | Status | Relevanz |
|------------|--------|----------|
| Datenmodelle (`Aufgabe`, `AutonomAufgabeKonfiguration`, `AutonomAufgabeInitialisierungsAnfrage`) | Vorhanden | `Aufgabe.LokalerKlonPfad` ist `null` bei autonomen Aufgaben; `AutonomAufgabeKonfiguration` existiert erst nach Dialog-Completion |
| Dialog-ViewModel (`AutonomAufgabeInitialisierungsDialogViewModel`) | Vorhanden, fehlerhaft | `NeuenBranchAnlegenAsync()` versucht sofort Branch-Anlage ohne Unterscheidung Autonom/Regulär; keine Möglichkeit, Autonom-Status zu prüfen |
| Initialisierungs-Service (`AutonomAufgabenInitialisierungsService`) | Vorhanden, unvollständig | Orchestriert Verzeichnis + Klon, aber **keine Branch-Erstellung** |
| Unteragenten-Git-Service (`UnteragentGitProvisioningService`) | Vorhanden, Muster-Service | Zeigt korrektes Muster: Branch-Anlage nach Klon-Existierung (Zeile 34: `git branch` im `repoMainPfad`) |
| Erweiterungs-Methode (`AufgabeExtensions.IstAutonom()`) | Vorhanden, limitiert | Funktioniert nur mit `AutonomKonfiguration` geladen; während Dialog nicht verfügbar |
| Git-Plugin-Interface (`IGitPlugin.CreateBranchAsync`) | Vorhanden | Erwartet lokalen Klon; nicht einsetzbar während Dialog ohne Klon |
| Tests | Vorhanden, lückenhaft | `AutonomAufgabeInitialisierungsDialogViewModelTests`, `AutonomAufgabenInitialisierungsServiceTests` — keine Tests für Branch-Anlag-Fehler oder Service-Branch-Erstellung |

---

## Details

### 1. Datenmodell
Siehe: [inventory/models.md](inventory/models.md)

**Kernerkenntnisse:**
- `Aufgabe.LokalerKlonPfad` ist bei autonomen Aufgaben beim Dialog-Aufruf noch `null`
- `AutonomAufgabeKonfiguration.ProjektBranchName` speichert den Branch-Namen final nach Dialog-Completion
- Keine neuen Properties erforderlich; das Modell ist strukturell korrekt

### 2. Logikklassen und Services
Siehe: [inventory/logic.md](inventory/logic.md)

**Kernerkenntnisse:**

**Dialog-ViewModel (`NeuenBranchAnlegenAsync`, Zeilen 325–363):**
- Versuch, sofort via `gitPlugin.CreateBranchAsync()` einen Branch anzulegen
- Fehlmeldung bei fehlendem `LokalerKlonPfad` (Zeile 331)
- **Problem:** Keine Unterscheidung zwischen Autonom/Regulär möglich
- **Lösung:** Entweder (a) nicht im Dialog für autonome Aufgaben versuchen, oder (b) Dialog-Aufruf kennt Autonom-Status und deaktiviert Branch-Anlag-Button

**Initialisierungs-Service (`InitialisiereAsync`, Zeilen 36–82):**
- Aktueller Ablauf: `ErstelleArbeitsverzeichnisStrukturAsync()` → `KloneHauptRepositoryAsync()` → state.json/permissions.json
- **Fehlende Step:** Branch-Erstellung nach Zeile 45 (nach Klon)
- **Vorbild:** `UnteragentGitProvisioningService.ProvisioniereAsync()`, Zeile 34: `git branch` im `repoMainPfad`
- `state.json` referenziert bereits `"project_branch": anfrage.ProjektBranchName` (BuildStateJson, Zeile 207), aber keine Garantie, dass dieser Branch existiert

**Unteragenten-Service (`UnteragentGitProvisioningService.ProvisioniereAsync`, Zeile 34):**
- Bestätigt das Muster: Nach Klon existiert, wird Branch via `_cliRunner.RunAsync("git", ["branch", ...], repoMainPfad, ...)` angelegt
- Dies ist direkt wiederverwendbar in `AutonomAufgabenInitialisierungsService`

### 3. Interfaces und Contracts
Siehe: [inventory/interfaces.md](inventory/interfaces.md)

**Kernerkenntnisse:**
- `IGitPlugin.CreateBranchAsync()` setzt lokalen Klon voraus
- `ICliRunner` ist Low-Level-Mechanismus; direkt nutzbar nach Klon-Anlage
- Keine neuen Interfaces erforderlich

### 4. Tests
Siehe: [inventory/tests.md](inventory/tests.md)

**Kernerkenntnisse:**
- Keine Unit-Tests für `NeuenBranchAnlegenAsync()` im Fehlerfall (ohne lokalen Klon)
- Keine Tests, dass `InitialisiereAsync()` einen Branch anlegt
- Test-Hilfsmethode `ErstelleAufgabeMitLokalemKlon()` setzt bereits Klon-Pfad, verschleiert das Problem
- Mock-Infrastruktur für `ICliRunner` vorhanden, kann für Branch-Tests erweitert werden

---

## Offene Fragen aus der Anforderung

### Frage 1: Unterscheidung Autonom vs. Regulär im Dialog
> Wie wird festgestellt, ob `Aufgabe` autonom ist?
> - Option A: Neue `IsAutonom`-Property auf `Aufgabe`?
> - Option B: Prüfung, ob bereits eine `AutonomAufgabeKonfiguration` existiert?
> - Option C: Separate Code-Pfade basierend auf dem Aufruf-Kontext?

**Befund:** 
- Bereits vorhanden: `IstAutonom()`-Erweiterungsmethode (AufgabeExtensions.cs, Zeile 20) prüft `AutonomKonfiguration is not null`
- **Problem:** Diese Prüfung funktioniert während des Dialogs nicht, weil die Konfiguration noch nicht existiert
- **Lösung erforderlich:** Dialog-ViewModel muss einen Parameter/Flag haben, der es als "Autonome-Aufgaben-Dialog" kennzeichnet (z.B. über eine zweite `Initialize(Aufgabe, bool isAutonomInitialization)`-Überladung oder ein Konstruktor-Parameter)

### Frage 2: Git-Operation für Branch-Erstellung
> Sollte `AutonomAufgabenInitialisierungsService` die gleiche Methode wie `UnteragentGitProvisioningService` nutzen (über `ICliRunner` mit `git branch`), oder ein eigenes Interface/Plugin?

**Befund:**
- `UnteragentGitProvisioningService` nutzt bereits `ICliRunner.RunAsync("git", ["branch", ...], repoMainPfad, null, ct)`
- Dies ist direkt wiederverwendbar
- **Empfehlung:** Gleiche Methode (`ICliRunner`) verwenden; kein neues Interface erforderlich

### Frage 3: Error-Handling
> Wenn die Branch-Erstellung bei Submit fehlschlägt, wird die gesamte Initialisierung rückgängig gemacht? Oder wird sie mit einem Default-Branch-Namen fortgesetzt?

**Befund:**
- Aktuell: `InitialisiereAsync()` hat keine Rollback-Logik für partiell erfolgreiche Operationen
- `KloneHauptRepositoryAsync()` wirft `InvalidOperationException` bei Fehler (Zeile 142)
- `BuildStateJson()` und `BuildPermissionsJson()` sind pure Funktionen, keine Fehler zu erwarten
- **Frage bleibt offen:** Muss definiert werden, wie mit Branch-Anlage-Fehler umgegangen wird (z.B. Rollback von Verzeichnissen?)

### Frage 4: UI-Feedback
> Sollte der Dialog dem Benutzer mitteilen, dass die Branch-Erstellung erst beim Submit stattfindet?

**Befund:**
- Dialog-XAML zeigt derzeit kein Feedback, dass Branch-Anlage verschoben ist
- Button "+" für "Branch anlegen" wird immer gezeigt, auch für autonome Aufgaben
- **Empfehlung:** (a) Button deaktivieren/verstecken für autonome Aufgaben, oder (b) Tooltip/Label ändern auf "Branch wird beim Absenden angelegt"

---

## Code-Ablauf-Diagramm: Aktueller Fehlerfall

```
Benutzer klickt "+" (ShowCreateBranchCommand)
    ↓
AutonomAufgabeInitialisierungsDialogViewModel.ZeigeBranchAnlegen()
    ↓ IsCreatingBranch = true (zeigt Branch-Name-Eingabe)
Benutzer tippt Branch-Namen und klickt "Anlegen" (CreateBranchCommand)
    ↓
AutonomAufgabeInitialisierungsDialogViewModel.NeuenBranchAnlegenAsync()
    ↓
if (_aufgabe is null || string.IsNullOrWhiteSpace(_aufgabe.LokalerKlonPfad))
    ↓ JA (bei autonomen Aufgaben!)
NewBranchError = "Kein lokaler Klon der Aufgabe vorhanden; Branch kann nicht angelegt werden."
    ↓ FEHLER
```

---

## Notwendige Änderungen (Übersicht)

### Im Dialog-ViewModel
1. Erkennen, dass Dialog für autonome Aufgabe aufgerufen wird (neuer Parameter/Flag)
2. Wenn autonom: `NeuenBranchAnlegenAsync()` nicht versuchen, oder Button deaktivieren
3. Alternativ: Branch-Name nur validieren (nicht via Git anlegen), bis Submit

### Im Initialisierungs-Service
1. Nach `KloneHauptRepositoryAsync()` (Zeile 45): Neue Methode `ErstelleProjektbranchAsync()` aufrufen
2. Diese Methode nutzt `ICliRunner.RunAsync()` mit `git branch` (wie `UnteragentGitProvisioningService`)
3. Error-Handling definieren (vollständiger Rollback? Oder mit Default-Namen fortfahren?)

### In Tests
1. Unit-Tests für `NeuenBranchAnlegenAsync()` im Fehlerfall (ohne lokalen Klon)
2. Unit-Tests für `InitialisiereAsync()` prüfen, dass Branch angelegt wird
3. Tests für Autonom/Regulär-Unterscheidung im Dialog

### Optional
1. UI-Feedback aktualisieren: Button deaktivieren oder Label ändern für autonome Aufgaben
2. `IstAutonom()`-Methode dokumentieren mit Warnung über EF-Tracking-Voraussetzung
