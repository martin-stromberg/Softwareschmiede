← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Fehlerbehebung

## Initialisierungs-Fehler

### Problem: Dialog „Autonome Aufgabe initialisieren" wird nicht angezeigt

**Symptom:** Der Button zum Initialisieren einer Autonomen Aufgabe ist nicht sichtbar oder deaktiviert.

**Ursache:**
- Feature-Flag `AutonomAufgaben.Enabled` ist auf `false` gesetzt
- Aufgabe ist bereits eine Autonome Aufgabe (AusfuehrungsStatus = AutonomAufgabe)
- Anwendung wurde nicht neu gestartet nach Konfigurationsänderung

**Lösung:**
1. Öffne `appsettings.json`
2. Suche `"AutonomAufgaben"` Sektion
3. Prüfe: `"Enabled": true`
4. Falls nicht: Setze auf `true` und speichere
5. Starte die Anwendung neu
6. Versuche erneut

---

### Problem: Validierungsfehler beim Ausfüllen des Formulars

**Symptom:** Dialog zeigt rote Fehlermeldung, z.B. „Token-Budget muss zwischen 1 und 5.000.000 liegen"

**Ursache:**
- Eingegebener Wert liegt außerhalb erlaubter Grenzen
- Initialprompt ist zu kurz (< 10 Zeichen)

**Lösung:**
1. Überprüfe jeden Feldwert:
   - **Initialprompt**: ≥ 10 Zeichen
   - **Token-Budget**: 1–5.000.000
   - **Laufzeitbegrenzung**: 60–1440 Minuten
   - **Projektbranch**: Gültiger Git-Branch-Name
2. Korrigiere Werte
3. Versuche erneut zu bestätigen

---

### Problem: „Arbeitsverzeichnis konnte nicht erstellt werden" Fehler

**Symptom:** Initialisierung bricht mit Fehler ab: „Directory access denied" oder „Path not found"

**Ursache:**
- Pfad existiert nicht und kann nicht erstellt werden
- Keine Schreibberechtigungen im Parent-Verzeichnis
- Festplatte ist voll

**Lösung:**
1. Prüfe Pfad-Konfiguration in `appsettings.json`:
   ```json
   "AutonomAufgaben": {
     "WorkingDirectoryBase": "C:/path/to/working/dir"
   }
   ```
2. Stelle sicher, dass das Parent-Verzeichnis existiert
3. Überprüfe Schreibberechtigungen für Parent-Verzeichnis
4. Überprüfe freien Speicherplatz (`df -h` oder Windows Speicherverwaltung)
5. Falls nötig: Ändere `WorkingDirectoryBase` zu erreichbarem Pfad
6. Versuche erneut

---

### Problem: „Git-Fehler: Repository konnte nicht geklont werden"

**Symptom:** Initialisierung schlägt fehl mit: „fatal: could not read Username"

**Ursache:**
- Git ist nicht installiert oder nicht im `PATH`
- Repository-URL ist nicht erreichbar
- Git-Authentifizierung schlägt fehl
- Netzwerkverbindung unterbrochen

**Lösung:**
1. Überprüfe Git-Installation:
   ```bash
   git --version
   ```
   Falls nicht vorhanden: Installiere Git (https://git-scm.com)
2. Prüfe Netzwerkverbindung:
   ```bash
   ping github.com
   ```
3. Prüfe Repository-Zugang manuell:
   ```bash
   git clone {repository-url} test-clone
   ```
   Falls Fehler: Git-Konfiguration / SSH-Keys prüfen
4. Falls SSH-basiert: Überprüfe SSH-Key und Passphrase
5. Versuche erneut

---

## Ausführungs-Fehler

### Problem: Agent wird nicht gestartet

**Symptom:** Nach Klick auf „Start" passiert nichts; Status zeigt nicht „Läuft"

**Ursache:**
- Agent-Runtime ist nicht korrekt konfiguriert
- Zu wenig Systemressourcen
- Fehler beim Agent-Startup (check logs)

**Lösung:**
1. Überprüfe Anwendungs-Logs (`logs/` Verzeichnis)
2. Suche nach Fehlereinträgen mit Prefix `ProjektleiterAgentService`
3. Falls Ressourcenfehler: Beende andere Anwendungen
4. Falls Agent-Runtime-Fehler: Kontaktiere Support mit Log-Ausschnitten

---

### Problem: Agent wird plötzlich unterbrochen

**Symptom:** Status wechselt von „Läuft" zu „Fehler" oder „Pausiert" ohne Aktion des Benutzers

**Ursache (nach Priorität):**
1. Token-Budget erreicht
2. Heartbeat-Timeout (Agent antwortet nicht)
3. Laufzeitlimit überschritten
4. Interner Agent-Fehler
5. System-Ressourcen erschöpft

**Lösung:**
1. Überprüfe Session-Status:
   - Aufgabe.SessionPauseUtc != null? → Budget-Limit erreicht → Fortsetzen mit erweitertem Budget
   - Aufgabe.AusfuehrungsStatus = Fehler? → Interner Fehler
2. Überprüfe `logs/agent.log` im Arbeitsverzeichnis für Details
3. Falls Budget-Limit: Erhöhe `DefaultTokenBudget` in appsettings.json (für nächste Aufgabe)
4. Falls Heartbeat-Timeout: Erhöhe `HeartbeatTimeoutSeconds`
5. Falls Laufzeitlimit: Erhöhe `DefaultRuntimeLimitMinutes`

---

### Problem: Unteragent-Fehler: Governance-Verletzung

**Symptom:** Logs zeigen: „Governance violation: [unteragent] tried to write outside scope"

**Ursache:**
- Unteragent versucht, Dateien außerhalb seines `tasks/task_XXX/` Bereichs zu schreiben
- Unteragent versucht, Skill zu modifizieren oder PR zu erstellen

**Lösung:**
1. Überprüfe `UnteragentSpezifikation.AgentScope` im Arbeitsverzeichnis (`state.json`)
2. Falls Scope zu eng: Initialisiere Aufgabe neu mit besserer Taskverteilung
3. Falls Unteragent-Prompt falsch: Benutzer/Entwickler muss Prompt präzisieren
4. Für Debug: Setze Logging-Level auf `Debug` in `appsettings.json`:
   ```json
   "Logging": {
     "LogLevel": {
       "Softwareschmiede.Application.Services.UnteragentGovernanceService": "Debug"
     }
   }
   ```

---

### Problem: Heartbeat-Timeout — Agent antwortet nicht

**Symptom:** UI zeigt: „Wurdest du unterbrochen? (Heartbeat-Timeout)" Meldung

**Ursache:**
- Agent-Prozess hängt oder ist gecrasht
- Netzwerkverbindung unterbrochen
- Heartbeat-Timeout zu kurz für langsame Systeme

**Lösung:**
1. Warte: Agent versucht automatisch zu antworten
2. Wenn keine Antwort: Aufgabe wird auf „Beendet" gesetzt
3. Überprüfe Agent-Logs und System-Logs auf Fehler
4. Falls häufig: Erhöhe `HeartbeatTimeoutSeconds` in appsettings.json:
   ```json
   "AutonomAufgaben": {
     "HeartbeatTimeoutSeconds": 600  // Von 300 auf 600 Sekunden
   }
   ```
5. Starte Anwendung neu und versuche erneut

---

## Datenbank-Fehler

### Problem: Fehler bei Datenbank-Migrations

**Symptom:** `dotnet ef database update` schlägt fehl mit SQL-Fehler

**Ursache:**
- Datenbank-Version zu alt
- Migration wurde bereits teilweise angewendet
- SQL-Syntax-Fehler in Migration

**Lösung:**
1. Überprüfe aktuelle Migrations:
   ```bash
   dotnet ef migrations list
   ```
2. Falls letzte Migration fehlgeschlagen: Entferne die Migration:
   ```bash
   dotnet ef migrations remove
   ```
3. Versuche Update erneut:
   ```bash
   dotnet ef database update
   ```
4. Falls Datenbankverbindung fehlschlägt: Überprüfe Connection String in `appsettings.json`
5. Falls Fehler persisiert: Kontaktiere Datenbankadmin

---

### Problem: Doppelte Einträge oder Inkonsistenz zwischen DB und Arbeitsverzeichnis

**Symptom:** `state.json` zeigt andere Daten als DB-Entities; Unteragenten sind doppelt gelistet

**Ursache:**
- Arbeitsverzeichnis wurde manuell gelöscht/modifiziert
- DB-Transaktion wurde nicht ordnungsgemäß abgeschlossen
- Mehrere Instanzen der Anwendung schreiben gleichzeitig

**Lösung:**
1. **Nie mehrere Instanzen starten** — Feature ist nicht für Parallelierung vorbereitet
2. Falls Daten inkonsistent: Wähle eine Quelle der Wahrheit:
   - **DB-Entities**: Lösche Arbeitsverzeichnis und Initialisiere neu
   - **state.json**: Manuell DB-Einträge korrigieren (SQL-Update)
3. Starte Anwendung neu

---

## Arbeitsverzeichnis-Fehler

### Problem: Arbeitsverzeichnis ist voll oder zu groß

**Symptom:** Fehler: „No space left on device" oder UI zeigt extrem langsame Updates

**Ursache:**
- Logs wurden nicht gelöscht
- Repository-Klone sind sehr groß
- Zu viele Unteragenten mit großen Artifacts

**Lösung:**
1. Überprüfe Größe des Arbeitsverzeichnisses:
   ```bash
   du -sh {arbeitsverzeichnis}
   ```
2. Überprüfe größte Verzeichnisse:
   ```bash
   du -sh {arbeitsverzeichnis}/*
   ```
3. Alte Logs löschen:
   ```bash
   rm -f {arbeitsverzeichnis}/logs/agent.log.old
   rm -f {arbeitsverzeichnis}/logs/cli.log.old
   ```
4. Falls Klone zu groß: Überprüfe `.git` Verzeichnis-Größe (Git-Garbage-Collection):
   ```bash
   cd {arbeitsverzeichnis}/clones/repo_main
   git gc --aggressive
   ```
5. Falls Speicherproblem persisiert: Vergrößere verfügbaren Speicherplatz

---

### Problem: Dateien in Arbeitsverzeichnis sind beschädigt

**Symptom:** Fehler beim Laden von `state.json`: „Invalid JSON" oder plan.md ist korrupt

**Ursache:**
- Datei wurde während Schreiboperation unterbrochen
- Dateisystem-Fehler
- Manuelle Manipulation

**Lösung:**
1. Überprüfe Datei:
   ```bash
   cat {arbeitsverzeichnis}/state.json
   ```
2. Falls JSON-Fehler: Validiere mit JSON-Parser:
   ```bash
   jq . {arbeitsverzeichnis}/state.json
   ```
3. Falls beschädigt: Versuche Backup wiederherzustellen (falls vorhanden)
4. Falls kein Backup: Löschen und neu initialisieren:
   ```bash
   rm -rf {arbeitsverzeichnis}
   # Initialisiere Aufgabe erneut im UI
   ```
5. Überprüfe Festplatte auf Fehler:
   ```bash
   chkdsk  # Windows
   fsck    # Linux
   ```

---

## Performance-Probleme

### Problem: Anwendung wird langsam / UI-Freeze bei laufender Autonomer Aufgabe

**Symptom:** UI reagiert nicht, oder Tastaturdruck hat Verzögerung

**Ursache:**
- Zu viele Unteragenten gleichzeitig
- Zu häufige Logging-Operationen
- Fehlerhafte Plan/Progress-Dateien werden ständig neu geschrieben
- Netzwerk-Bottleneck

**Lösung:**
1. Reduziere Unteragenten-Parallelität:
   ```json
   "AutonomAufgaben": {
     "MaxConcurrentSubagents": 2  // Von 5 auf 2
   }
   ```
2. Erhöhe Logging-Level auf `Warning`:
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Warning"
     }
   }
   ```
3. Überprüfe, ob plan.md/progress.md sehr groß werden:
   ```bash
   wc -l {arbeitsverzeichnis}/plan.md
   ```
   Falls > 10.000 Zeilen: Archiviere alte Abschnitte manuell
4. Überprüfe Netzwerk-Latenz zu Repository:
   ```bash
   ping {repository-host}
   ```

---

## Häufig gestellte Fragen

**F: Kann ich eine laufende Autonome Aufgabe pausieren und später fortsetzen?**  
A: Ja, aber nur wenn das Token-Budget erreicht wird. Manuelle Pause wird noch nicht unterstützt.

**F: Was passiert, wenn ich die Anwendung während einer Aufgabe beende?**  
A: Der Agent wird unterbrochen. Nach dem Neustart wird die Aufgabe automatisch fortgesetzt (falls `auto_resume = true` in `permissions.json`).

**F: Kann ich einen Unteragent manuell debuggen?**  
A: Ja, öffne sein Arbeitsverzeichnis (`tasks/task_XXX/`) und überprüfe `task_log.md` und `task_report.md`.

**F: Wie kann ich eine fehlgeschlagene Aufgabe löschen?**  
A: Lösche die Aufgabe vom Projekt (oder setze ihren Status auf gelöscht). Das Arbeitsverzeichnis wird nicht automatisch gelöscht; lösche es manuell falls gewünscht.

**F: Was ist der Unterschied zwischen Token-Limit und Laufzeitlimit?**  
A: Token-Limit = API-Tokens für Agent-Calls; Laufzeitlimit = echte verstrichene Zeit in Minuten. Ein Agent kann token-budget-limit brechen bevor Laufzeit-Limit erreicht wird.

---

## Support & Weitere Hilfe

Falls das Problem nicht gelöst werden konnte:

1. Sammle folgende Informationen:
   - Fehlermeldung (aus UI oder Logs)
   - Arbeitsverzeichnis-Logs (`logs/agent.log`, `logs/cli.log`)
   - Anwendungs-Logs (EventLog oder Konsole)
   - Aufgaben-ID und zugehörige DB-Einträge
   - Schritte zum Reproduzieren

2. Kontaktiere den Support oder öffne ein Issue im Repository

3. Stelle sicher, dass keine sensiblen Daten in Logs exposed werden (token credentials, etc.)
