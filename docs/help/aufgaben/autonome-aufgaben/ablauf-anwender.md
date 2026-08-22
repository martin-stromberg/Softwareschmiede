← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Ablauf für Anwender

## Voraussetzungen

- Projektleiter-Modus muss aktiviert sein (Feature-Flag `AutonomAufgaben.Enabled = true`)
- Aufgabe muss existieren (Projekt und Aufgabe-Entity in der DB)
- Git-Repository-Zugang muss vorhanden sein (für Repository-Klon)
- Mindestens 60 Minuten Laufzeitbudget erforderlich

## Schritt-für-Schritt-Anleitung

### 1. Initialisierungsdialog öffnen

1. Navigiere zur bestehenden oder neu erstellten Aufgabe
2. Öffne das Aufgaben-Detailpanel
3. Klicke auf den Button **„Autonome Aufgabe initialisieren"** (oder ähnlich)
   - Der Initialisierungsdialog öffnet sich mit Formularfeldern

> **Hinweis:** Der Dialog wird nur angezeigt, wenn die Aufgabe noch nicht als Autonome Aufgabe initialisiert wurde (Status != `AutonomAufgabe`).

> **Hinweis:** Oben rechts im Dialog befindet sich der Button **„Hilfe"**. Er öffnet einen Informationsdialog mit einer Erklärung des gesamten Ablaufs einer Autonomen Aufgabe (Initialisierung, Agent-Start, Unteragenten, Fortschritt/Integration, Session-Pause) sowie einer Kurzbeschreibung aller Formularfelder — hilfreich, wenn du zum ersten Mal eine Autonome Aufgabe anlegst.

### 2. Konfiguration ausfüllen

Im Formular werden folgende Felder abgefragt:

| Feld | Beschreibung | Standard | Erforderlich |
|------|--------------|----------|-------------|
| **Projektbranch** | Git-Branch für die Gesamtaufgabe. Wenn möglich als Auswahlliste der Remote-Branches des Repositories, sonst als Texteingabe | `autonom-{AufgabeId}` | Ja |
| **Promptvorlage** | Optionale Auswahl einer vorhandenen Promptvorlage zur Vorbefüllung des Initialprompts | — | Nein |
| **Initialprompt** | Fachliche Anforderung für den Projektleiter | — | Ja |
| **Permissions** | Berechtigungsprofil-Quelle | Generieren | Nein |
| **Token-Budget** | Max. Token für die Aufgabe | 500.000 | Ja |
| **Token-Erweiterung erlauben** | Darf Budget erhöht werden? | Ja | Nein |
| **Laufzeitbegrenzung (Minuten)** | Nettozeit-Limit | 480 (8h) | Ja |
| **Persistenz-Modus** | Verhalten bei Session-Pause | Standard | Nein |
| **Skill-Autogeneration** | Skills auto aus Anforderungen? | Nein | Nein |

Beispiel-Ausfüllung:
```
Projektbranch: feature/autonome-auth-system
Promptvorlage: (keine — freier Text)
Initialprompt: Implementiere JWT-basiertes Authentifizierungssystem mit Middleware, DB-Migrationen und Tests.
Permissions: Automatisch generieren
Token-Budget: 500000
Laufzeitbegrenzung: 480 Minuten
Persistenz-Modus: Standard
Skill-Autogeneration: Deaktiviert
```

> **Hinweis:** Der Initialprompt sollte mindestens 10 Zeichen lang sein und fachliche Anforderungen klar formulieren.

#### Projektbranch aus Liste wählen oder neu anlegen

- Ist der Aufgabe ein Git-Repository mit unterstütztem Plugin zugeordnet, zeigt das Feld **Projektbranch** eine Auswahlliste der vorhandenen Remote-Branches; solange diese geladen wird, erscheint der Hinweis „Wird geladen…". Ist kein Repository/Plugin vorhanden oder liefert es keine Branches, wird stattdessen ein freies Textfeld angezeigt.
- Um einen neuen Branch anzulegen, klicke auf den **„+"-Button** rechts neben dem Projektbranch-Feld. Es erscheint eine Eingabezeile für den neuen Branchnamen mit den Buttons **„Anlegen"** und **„Abbrechen"**.
- Nach Eingabe eines Namens und Klick auf **„Anlegen"** wird der Branch im lokalen Klon der Aufgabe erzeugt (ausgehend vom aktuell gewählten Projektbranch), automatisch der Branch-Liste hinzugefügt und als Projektbranch übernommen.

> **Hinweis:** Die Branch-Neuanlage benötigt einen lokalen Klon der Aufgabe. Ist keiner vorhanden oder schlägt die Git-Operation fehl, erscheint eine Fehlermeldung unterhalb der Eingabezeile; der Dialog bleibt geöffnet und die Eingabe kann korrigiert oder abgebrochen werden.

#### Promptvorlage für den Initialprompt nutzen

- Im Feld **Promptvorlage** stehen die im System hinterlegten Promptvorlagen zur Auswahl.
- Nach Auswahl einer Vorlage wird das Feld **Initialprompt** automatisch mit dem Vorlagentext befüllt; enthaltene Platzhalter (z. B. Bezug auf die aktuelle Aufgabe) werden dabei automatisch aufgelöst.
- Der übernommene Text kann anschließend im Initialprompt-Feld frei weiterbearbeitet werden.

### 3. Initialisierung bestätigen

1. Klicke den Button **„Initialisieren"**
2. Das System erstellt:
   - Strukturiertes Arbeitsverzeichnis
   - Repository-Klon
   - Initial-Dateien (plan.md, progress.md, state.json, etc.)
   - Datenbankeinträge für Konfiguration
3. Der Dialog schließt sich
4. Die Detail-View zeigt jetzt die neue Autonome Aufgabe

### 4. Projektleiter-Agenten starten

1. Der Aufgaben-Detailpanel zeigt jetzt den **„Start"**-Button
2. Klicke auf **„Start"**, um den Projektleiter-Agenten zu starten
3. Der Agent wird mit dem Initialprompt und Skill-Registry initialisiert
4. Status in der UI wechselt zu **„Läuft"** (mit aktiver Agent-ID)

> **Hinweis:** Der Agent läuft im Hintergrund. Sie können währenddessen zu anderen Aufgaben wechseln.

### 5. Fortschritt überwachen (optional)

Im Detail-Panel können Sie folgende Tabs einsehen:

| Tab | Inhalt | Aktualisierung |
|-----|--------|----------------|
| **Plan** | `plan.md` — Gesamtplan mit Teilaufgaben | Live |
| **Fortschritt** | `progress.md` — Meilensteine, Status | Live |
| **Governance** | `governance.md` — Limits und Regeln | Statisch |
| **Skills** | Liste aktiver und archivierter Skills | Live |
| **Unteragenten** | Tabelle aktiver/beendeter Unteragenten | Live |

### 6. Bei Session-Pause (Budget-Limit)

Wenn das Token-Budget erreicht wird:

1. Agent wird pausiert
2. Status ändert sich zu **„Pausiert"**
3. Der Button wechselt zu **„Fortsetzen"**
4. Falls `TokenErweiterung == true`:
   - Sie können das Budget erhöhen
   - Klicken Sie **„Fortsetzen mit erhöhtem Budget"**
   - Agent startet mit „Weitermachen"-Prompt neu

> **Hinweis:** Bei `TokenErweiterung == false` kann kein Resume durchgeführt werden; die Aufgabe muss neu initialisiert werden.

### 7. Abschluss und Pull Request

Nach erfolgreicher Ausführung:

1. Agent aktualisiert `progress.md` mit „Abgeschlossen"
2. Status zeigt **„Abgeschlossen"**
3. Ein Pull Request wird vorbereitet (nicht automatisch gemergt)
4. Sie können den PR öffnen und reviewen
5. Bei Bedarf manuell mergen oder Änderungen anfordern

## Ergebnis

Nach erfolgreichem Abschluss einer Autonomen Aufgabe:

- **Arbeitsverzeichnis** enthält alle Artefakte (plan.md, progress.md, state.json, Task-Reports, Logs)
- **Repository** hat neue Commits im Feature-Branch und einen vorbereiteten Pull Request
- **Datenbank** erfasst alle Unteragenten-Spezifikationen und Skill-Definitionen
- **UI** zeigt vollständige Historie in den Detail-Tabs

## Fehlerfälle und Behebung

| Problem | Anzeichen | Behebung |
|---------|-----------|----------|
| **Arbeitsverzeichnis nicht erstellt** | Fehlermeldung während Initialisierung | Überprüfe Pfadberechtigungen und Festplattenspeicher |
| **Repository-Klon fehlgeschlagen** | Fehlermeldung „Git-Fehler" | Überprüfe Git-Zugang und Netzwerkverbindung |
| **Agent bricht unerwartet ab** | Status springt zu „Fehler" | Siehe Logs in `logs/agent.log` im Arbeitsverzeichnis |
| **Heartbeat-Timeout** | Agent wird pausiert, obwohl aktiv | Prüfe Netzwerkverbindung; erhöhe ggf. Heartbeat-Timeout in Einstellungen |
| **Permission-Fehler bei Unteragent** | Agent kann Dateien nicht schreiben | Unteragent versucht außerhalb seines Scope zu arbeiten — manuell debuggen |

## Tipps

- **Detaillierter Initialprompt spart Zeit** — Je klarer die Anforderung, desto bessere Taskverteilung
- **Passende Budget-Grenzen wählen** — Nicht zu niedrig (häufige Pausen), nicht zu hoch (Fehler nicht erkannt)
- **Logs regelmäßig prüfen** — `logs/agent.log` gibt Aufschluss über Agent-Entscheidungen
- **Underagenten-Fortschritt tracken** — Im „Unteragenten"-Tab die Anzahl fertiger Tasks verfolgen
