← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Business Rules

## Verfügbarkeit

### Regel: Feature-Flag steuert Verfügbarkeit Autonomer Aufgaben

**Beschreibung:** Autonome Aufgaben können über `AutonomAufgabenOptions.Enabled` global deaktiviert werden. Ist das Flag deaktiviert, wird an jedem der drei Einstiegspunkte per Guard-Klausel verhindert, dass eine neue Autonome Aufgabe initialisiert oder ein Projektleiter-Agent gestartet wird.

**Bedingungen:**
- `IOptions<AutonomAufgabenOptions>.Value.Enabled == false` (gebunden aus `appsettings.json`, Sektion `AutonomAufgaben`, bzw. der Umgebungsvariable `AutonomAufgaben__Enabled`, beim App-Start)

**Verhalten (Defense-in-Depth, drei unabhängige Guard-Klauseln):**
- `AutonomAufgabeStartService.StarteAsync()` (UI-Einstiegspunkt, vor dem Öffnen des Initialisierungsdialogs): gibt statt einer Dialog-Anzeige ein `AutonomAufgabeStartResult` mit `FehlerMeldung = "Autonome Aufgaben sind in den Einstellungen deaktiviert."` zurück (kein Exception-Wurf, da UI-nah)
- `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`: wirft `InvalidOperationException(AutonomAufgabenOptions.DisabledErrorMessage)` ("Autonome Aufgaben sind nicht aktiviert."), bevor Arbeitsverzeichnis oder Repository-Klon angelegt werden
- `ProjektleiterAgentService.StarteAgentAsync()`: wirft ebenfalls `InvalidOperationException(AutonomAufgabenOptions.DisabledErrorMessage)`, bevor der CLI-Prozess gestartet wird
- `TaskDetailViewModel.IsAutonomAufgabenEnabled` (`_autonomAufgabenOptions?.Value.Enabled ?? false`) steuert zusätzlich `ShowAutomatisierungPanel` (`IsAutonomAufgabe && IsAutonomAufgabenEnabled`), sodass die Registerkarte „Automatisierung" bei deaktiviertem Flag nicht angezeigt wird

**Umsetzung:** `AutonomAufgabeStartService.StarteAsync()`, `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`, `ProjektleiterAgentService.StarteAgentAsync()`, `TaskDetailViewModel.IsAutonomAufgabenEnabled`/`ShowAutomatisierungPanel`

> **Hinweis:** Die Registerkarte „Automatisierung" (Einstellungen → Allgemein) besitzt zusätzlich eine Checkbox „Autonome Aufgaben aktivieren", die über `AppEinstellungService` (Schlüssel `autonomeaufgaben.enabled`) in der Datenbank persistiert wird. Dieser Wert wird ausschließlich von `SettingsViewModel` gelesen/geschrieben (Round-Trip für die Anzeige) und fließt derzeit **nicht** in die oben genannten Guard-Klauseln oder in `TaskDetailViewModel.IsAutonomAufgabenEnabled` ein — diese prüfen ausschließlich `IOptions<AutonomAufgabenOptions>`. Maßgeblich für die tatsächliche Verfügbarkeit ist also der beim App-Start aus `appsettings.json`/Umgebungsvariable gebundene Wert.

---

### Regel: Nicht-autonomer Weg ist vom Feature-Flag unabhängig

**Beschreibung:** Das einfache Starten einer Aufgabe mit direkter CLI-Ausführung (`EntwicklungsprozessService`, Ribbon-Button „Starten") prüft `AutonomAufgabenOptions.Enabled` an keiner Stelle und bleibt unabhängig vom Feature-Flag-Status uneingeschränkt nutzbar.

**Bedingungen:** —

**Verhalten:** `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` und `KiAusfuehrungsService.StartCliAsync()` laufen unverändert, unabhängig davon, ob Autonome Aufgaben aktiviert sind.

**Umsetzung:** `EntwicklungsprozessService` (kein Guard, bewusst neutral gehalten)

---

## Initialisierung

### Regel: Einmalige Initialisierung pro Aufgabe

**Beschreibung:** Eine reguläre Aufgabe kann nur einmal als Autonome Aufgabe initialisiert werden.

**Bedingungen:**
- Aufgabe existiert (Status != gelöscht)
- Aufgabe.AusfuehrungsStatus != AutonomAufgabe

**Verhalten:**
- Dialog „Autonome Aufgabe initialisieren" wird angezeigt
- Nach erfolgreicher Initialisierung: Aufgabe.AusfuehrungsStatus → AutonomAufgabe
- Beim erneuten Öffnen wird stattdessen das Detail-Panel angezeigt (nicht der Dialog)

**Umsetzung:** `AutonomAufgabeInitialisierungsDialogViewModel.InitialisiereAsync()` validiert vorher

---

### Regel: Initialprompt muss aussagekräftig sein

**Beschreibung:** Der Initialprompt muss eine Mindestlänge haben und fachliche Anforderungen beschreiben.

**Bedingungen:**
- `InitialPrompt.Length >= 10`
- Nicht nur Whitespace

**Verhalten:**
- Falls nicht erfüllt: Dialog zeigt Validierungsfehler
- Bestätigen-Button ist deaktiviert bis Bedingung erfüllt

**Umsetzung:** `AutonomAufgabeInitialisierungsDialogViewModel.BestaetigenAsync()` wirft `ArgumentException`

---

### Regel: Token-Budget ist hart

**Beschreibung:** Das Token-Budget ist die absolute Obergrenze und kann nicht überschritten werden.

**Bedingungen:**
- TokenBudget > 0 und ≤ 5.000.000
- TokenBudget ist INTEGER (keine Dezimalzahlen)

**Verhalten:**
- Bei Erreichen des Limits wird die Aufgabe pausiert
- Projektleiter-Agent wird unterbrochen
- Benutzer kann mit erhöhtem Budget fortsetzen (falls `AllowTokenExtension == true`)

**Umsetzung:** `SessionManagementService.PauseAufgabeBeiBudgetLimitAsync()` setzt SessionPauseUtc

---

### Regel: Laufzeitlimit ist zwischen 60 und 1440 Minuten

**Beschreibung:** Die Nettozeit-Limit muss zwischen 1 Stunde (60 min) und 24 Stunden (1440 min) liegen.

**Bedingungen:**
- LaufzeitLimitMinuten ∈ [60..1440]

**Verhalten:**
- Falls nicht erfüllt: Validierungsfehler im Dialog
- Spinnbox mit Min/Max-Limits

**Umsetzung:** UI-Validierung + Backend-Validierung in `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`

---

## Ausführung des Projektleiter-Agenten

### Regel: Projektleiter arbeitet nur in seinem Arbeitsverzeichnis

**Beschreibung:** Der Projektleiter-Agent hat Zugriff auf sein Arbeitsverzeichnis und kann plan.md, progress.md, state.json und Unterverzeichnisse bearbeiten, aber nicht darüber hinaus.

**Bedingungen:**
- Alle Datei-Operationen finden innerhalb von `{ArbeitsverzeichnisPfad}/*` statt
- Zugriffe auf Git-Befehle sind auf seinen Branch beschränkt

**Verhalten:**
- Projektleiter erstellt plan.md und progress.md
- Projektleiter erzeugt Unteragenten (via ProjektleiterAgentService)
- Projektleiter kann nicht außerhalb schreiben
- Verstöße werden logged und blockiert (falls implementiert)

**Umsetzung:** Implizit durch Arbeitsverzeichnis-Struktur; explizit durch `UnteragentGovernanceService` für Unteragenten

---

### Regel: Unteragenten sind räumlich isoliert

**Beschreibung:** Jeder Unteragent arbeitet nur in seinem eigenen Scope (`tasks/task_XXX/`).

**Bedingungen:**
- Unteragent.VerzeichnisPfad = `tasks/task_{N}`
- Unteragent.Scope eindeutig pro AutonomAufgabeId

**Verhalten:**
- Unteragent kann nur in `tasks/task_{N}/` Dateien erstellen/ändern
- Unteragent kann nicht in `clones/`, `skills/`, `tasks/task_{M}/` (M != N) schreiben
- Verstöße werden durch `UnteragentGovernanceService` blockiert

**Umsetzung:** `UnteragentGovernanceService.VerifiziereBerechtigung()` prüft vor Schreibzugriff

---

### Regel: Unteragenten können keine Pull Requests erstellen

**Beschreibung:** Nur der Projektleiter kann PRs vorbereiten; Unteragenten sind auf Commits begrenzt.

**Bedingungen:**
- Unteragent.Status != erlaubt_PR_Erstellung

**Verhalten:**
- Unteragent committet zu seinem Feature-Branch
- Nur Projektleiter fasst alles zusammen und bereitet PR vor
- `CreatePullRequest`-Aktion wird für Unteragenten blockiert

**Umsetzung:** `UnteragentGovernanceService.VerifiziereBerechtigung()` wirft Fehler für PR-Aktion

---

### Regel: Skills sind nach Freigabe unveränderlich

**Beschreibung:** Einmal freigegebene Skills (`Status == Freigegeben`) können nicht modifiziert werden.

**Bedingungen:**
- Status == Freigegeben

**Verhalten:**
- Änderungen auf freigegebene Skills sind nicht erlaubt
- Neue Versionen müssen neue SkillDefinition-Einträge sein (versioniert)
- Alte Versionen können archiviert werden

**Umsetzung:** `SkillDefinition.Status` wird geprüft; schreibend ist nur Status != Freigegeben erlaubt

---

## Session-Management

### Regel: Session-Pause bei Budget-Limit

**Beschreibung:** Wenn das Token-Budget erreicht wird, wird die Aufgabe automatisch pausiert.

**Bedingungen:**
- TokenVerbunden / TokenBudget >= 0,95 (95%)

**Verhalten:**
- Agent wird unterbrochen
- AutonomAufgabeKonfiguration.SessionPauseUtc = now gesetzt
- Aufgabe.AusfuehrungsStatus → Wartend/Beendet
- state.json wird aktualisiert

**Umsetzung:** `SessionManagementService.PauseAufgabeBeiBudgetLimitAsync()`

---

### Regel: Token-Erweiterung erfordert Benutzer-Bestätigung

**Beschreibung:** Wenn das Budget pausiert hat und `AllowTokenExtension == true`, kann der Benutzer das Budget erhöhen.

**Bedingungen:**
- SessionPauseUtc != null
- AllowTokenExtension == true

**Verhalten:**
- UI zeigt „Fortsetzen mit erhöhtem Budget"-Button
- Benutzer kann Betrag eingeben
- Nach Bestätigung wird TokenBudgetErweitert aktualisiert und Agent neu gestartet

**Umsetzung:** `SessionManagementService.SetzeFortAsync()` mit optionaler Budget-Erhöhung

---

### Regel: Heartbeat-Timeout erkennt Agenten-Unterbrechung

**Beschreibung:** Wenn der Agent für länger als `HeartbeatTimeoutSeconds` nicht antwortet, wird vermutet, dass er unterbrochen wurde.

**Bedingungen:**
- Zeit seit `LastHeartbeatUtc` > HeartbeatTimeoutSeconds
- SessionPauseUtc == null (nicht bereits in Pause)

**Verhalten:**
- "Wurdest du unterbrochen?"-Prompt wird an Agent gesendet
- Falls keine Antwort innerhalb weiterer Timeoutdauer: Aufgabe auf Beendet setzen
- Fehlerlog wird ausgegeben

**Umsetzung:** `SessionManagementService.PruefeAusfuehrungAsync()`

---

### Regel: Fortsetzen nach Pause mit Kontextualisierung

**Beschreibung:** Nach Session-Pause wird der Agent mit einem „Weitermachen"-Prompt neugestartet, der Kontext aus plan.md, progress.md und state.json enthält.

**Bedingungen:**
- SessionPauseUtc != null
- Benutzer klickt „Fortsetzen"

**Verhalten:**
- Prompt wird generiert: "In plan.md steht der Plan, in progress.md der bisherige Fortschritt. Fahre von dort aus fort."
- Agent wird mit Prompt neu gestartet
- SessionPauseUtc → null gesetzt

**Umsetzung:** `SessionManagementService.SetzeFortAsync()` konstruiert Weiterführungs-Prompt

---

## Unteragenten-Management

### Regel: Max. gleichzeitige Unteragenten

**Beschreibung:** Maximal N Unteragenten können gleichzeitig laufen (Standard: 5).

**Bedingungen:**
- AktiveUnteragenten <= MaxConcurrentSubagents

**Verhalten:**
- Falls Limit erreicht, wird neuer Unteragent in Warteschlange eingereiht
- Oder: Projektleiter stoppt einen Unteragent vor dem Start eines neuen
- Error wird gelogged: "Max. gleichzeitige Unteragenten erreicht"

**Umsetzung:** `ProjektleiterAgentService.SteuereUnteragentAsync()` prüft Limit vor Start

---

### Regel: Unteragenten-Branchname ist eindeutig

**Beschreibung:** Jeder Unteragent erhält einen eindeutigen Git-Branch.

**Bedingungen:**
- Branch = `feature-unteragent-{counter}` eindeutig pro AutonomAufgabeId

**Verhalten:**
- Counter wird inkrementiert für jeden neuen Unteragent
- Keine Merge-Konflikte zwischen parallelen Unteragenten

**Umsetzung:** Counter in `state.json` wird inkrementiert; Branch wird aus Counter generiert

---

### Regel: Unteragenten-Verzeichnis ist eindeutig

**Beschreibung:** Jeder Unteragent hat sein eigenes Arbeitsverzeichnis.

**Bedingungen:**
- VerzeichnisPfad = `tasks/task_{counter}` eindeutig pro AutonomAufgabeId

**Verhalten:**
- Unteragent-Ergebnisse sind räumlich isoliert
- task_report.md, task_changes.json, task_log.md landen im eigenen Directory

**Umsetzung:** Directory wird basierend auf Counter erstellt

---

## Abschluss

### Regel: Pull Request wird vorbereitet, nicht automatisch gemergt

**Beschreibung:** Der Projektleiter bereitet einen PR vor, aber mergt ihn nicht automatisch.

**Bedingungen:**
- Projektleiter-Agent hat alle Unteragenten integriert
- state.json.pull_request.status = "planned"

**Verhalten:**
- PR wird mit Commits aller Unteragenten und plan.md/progress.md vorbereitet
- PR-URL wird in state.json und DB gespeichert
- Benutzer kann PR manuell reviewen und mergen

**Umsetzung:** `ProjektleiterAgentService.IntegriereErgebnisseAsync()` führt Commits zusammen, erstellt aber keinen Merge

---

### Regel: Aufgabe kann nur abgeschlossen werden, wenn Projektleiter Abschluss signalisiert

**Beschreibung:** Die Aufgabe wird nur als Abgeschlossen markiert, wenn der Agent dies bestätigt.

**Bedingungen:**
- Agent sendet "Aufgabe abgeschlossen"-Signal
- Alle Unteragenten sind Abgeschlossen/Fehler

**Verhalten:**
- Aufgabe.AusfuehrungsStatus bleibt AutonomAufgabe (oder wird neu auf Abgeschlossen gesetzt)
- Aufgabe.AbschlussDatum wird gesetzt
- UI zeigt Abschluss-Status

**Umsetzung:** Logik in `ProjektleiterAgentService` oder `AufgabeService`

---

## Governance

### Regel: Permissions-Datei ist nach Initialisierung unveränderlich

**Beschreibung:** Die `permissions.json` wird bei Initialisierung erzeugt und darf danach nicht verändert werden.

**Bedingungen:**
- permissions.json existiert

**Verhalten:**
- Leseonly für Agenten
- Nur Admin/Anwender können Datei manuell löschen und neu initialisieren
- Keine Modifikation durch Agenten

**Umsetzung:** Governance-Service blockiert Schreibzugriff auf permissions.json

---

### Regel: Arbeitsverzeichnis-Struktur ist normalisiert

**Beschreibung:** Pfade werden normalisiert, um Pfad-Traversal-Angriffe zu verhindern.

**Bedingungen:**
- Alle Pfad-Zugriffe werden canonicalisiert
- Symlinks werden aufgelöst
- `..` wird nicht erlaubt

**Verhalten:**
- Versuch, außerhalb des Arbeitsverzeichnisses zuzugreifen, wird blockiert
- Fehlerlog wird ausgegeben

**Umsetzung:** `Path.GetFullPath()` + Validierung gegen Arbeitsverzeichnis-Pfad
