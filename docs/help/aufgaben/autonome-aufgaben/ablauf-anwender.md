← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Ablauf für Anwender

## Voraussetzungen

- Projektleiter-Modus muss aktiviert sein (Feature-Flag `AutonomAufgaben.Enabled = true`)
- Aufgabe muss existieren (Projekt und Aufgabe-Entity in der DB)
- Git-Repository-Zugang muss vorhanden sein (für Repository-Klon)
- Mindestens 60 Minuten Laufzeitbudget erforderlich

## Schritt-für-Schritt-Anleitung

### 1. Initialisierungsdialog öffnen

1. Navigiere zur bestehenden oder neu erstellten Aufgabe in der Aufgabenliste
2. Öffne die Detailansicht der Aufgabe (Doppelklick oder Auswahl im Panel)
3. Im Ribbon-Menü der Aufgabe (unter der Gruppe **„Autonome Aufgabe"**) klicke auf den Button **„Autonome Aufgabe starten"**
   - Ein Initialisierungsdialog öffnet sich mit Formularfeldern

> **Hinweis:** Der Dialog wird nur angezeigt, wenn die Aufgabe noch nicht als Autonome Aufgabe initialisiert wurde (Status != `AutonomAufgabe`).

> **Hinweis:** Im Initialisierungsdialog gibt es oben rechts einen Button **„Hilfe"**. Er öffnet einen Informationsdialog mit einer Erklärung des gesamten Ablaufs einer Autonomen Aufgabe (Initialisierung, Agent-Start, Unteragenten, Fortschritt/Integration, Session-Pause) sowie einer Kurzbeschreibung aller Formularfelder — hilfreich, wenn du zum ersten Mal eine Autonome Aufgabe anlegst.

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
- Nach Eingabe eines Namens und Klick auf **„Anlegen"** wird der Name validiert, automatisch der Branch-Liste hinzugefügt und als Projektbranch übernommen. Es findet an dieser Stelle noch keine Git-Operation statt — zu diesem Zeitpunkt existiert noch kein lokaler Klon der Aufgabe. Der Branch wird erst beim Absenden des Dialogs (Schritt 3) im frisch geklonten Repository tatsächlich angelegt.

> **Hinweis:** Ist der eingegebene Name leer, kein gültiger Git-Branch-Name oder bereits in der Auswahlliste vorhanden (Duplikat), erscheint eine Fehlermeldung unterhalb der Eingabezeile; der Dialog bleibt geöffnet und die Eingabe kann korrigiert oder abgebrochen werden.

#### Promptvorlage für den Initialprompt nutzen

- Im Feld **Promptvorlage** stehen die im System hinterlegten Promptvorlagen zur Auswahl.
- Nach Auswahl einer Vorlage wird das Feld **Initialprompt** automatisch mit dem Vorlagentext befüllt; enthaltene Platzhalter (z. B. Bezug auf die aktuelle Aufgabe) werden dabei automatisch aufgelöst.
- Der übernommene Text kann anschließend im Initialprompt-Feld frei weiterbearbeitet werden.

### 3. Initialisierung bestätigen

1. Klicke den Button **„Initialisieren"**
2. Das System erstellt:
   - Strukturiertes Arbeitsverzeichnis
   - Repository-Klon (direkt von der Repository-URL der Aufgabe, mit dem der Aufgabe zugeordneten Git-Plugin)
   - Projektbranch im geklonten Repository (neu angelegt oder — falls bereits remote vorhanden — ausgecheckt, ebenfalls mit dem aufgabenspezifischen Git-Plugin)
   - Initial-Dateien (plan.md, progress.md, state.json, etc.)
   - Datenbankeinträge für Konfiguration
3. Der Initialisierungsdialog schließt sich
4. Die Detailansicht der Aufgabe zeigt jetzt eine neue Registerkarte **„Automatisierung"**, auf der die Details der Autonomen Aufgabe sichtbar sind
5. Im Ribbon-Menü (Gruppe **„Autonome Aufgabe"**) sind jetzt die Buttons **„Start"**, **„Stop"** und **„Fortsetzen"** verfügbar

> **Hinweis:** Das Git-Plugin für Repository-Klon und Projektbranch-Operationen wird anhand der Aufgabenkonfiguration (`GitRepository.PluginTyp`) bestimmt, nicht anhand eines global konfigurierten Default-Plugins. Dies stellt sicher, dass die richtige SCM-Integration für jede Aufgabe verwendet wird.

### 4. Projektleiter-Agenten starten

1. Im Ribbon-Menü der Aufgabendetails, Gruppe **„Autonome Aufgabe"**, klicke auf den Button **„Start"**
2. Es wird ein echter KI-CLI-Prozess für die Aufgabe gestartet (dieselbe CLI-Infrastruktur wie bei regulären Aufgaben) und die Projektleiter-Skill-Datei erzeugt
3. Wenige Sekunden nach dem CLI-Start wird der Initialprompt automatisch an die laufende CLI-Session gesendet, damit der Projektleiter-Agent seine Arbeit beginnt
4. Status in der **„Automatisierung"**-Registerkarte wechselt zu **„Läuft"** (mit aktiver Agent-ID)

Solange die CLI läuft, sind ausschließlich die Buttons **„Start"**, **„Stop"** und **„Resume"** im Ribbon (Gruppe **„Autonome Aufgabe"**) für die Steuerung zuständig — es gibt keine zusätzlichen Bedienelemente innerhalb der **„Automatisierung"**-Registerkarte selbst. Zudem sind für Autonome Aufgaben die regulären Ribbon-Buttons **„Starten"**/**„Beenden"** (Gruppe **„Aufgabe"**) sowie **„CLI starten"**/**„Stoppen"** (Gruppe **„Ausführung"**) ausgeblendet, da die gesamte Steuerung über die Gruppe **„Autonome Aufgabe"** läuft.

> **Hinweis:** Der Agent läuft im Hintergrund. Sie können währenddessen zu anderen Aufgaben wechseln. Wenn Sie zu einer anderen Aufgabe wechseln und später zu dieser Aufgabe zurückkehren, wird für die Detailansicht eine neue Instanz erzeugt, in der die **„Automatisierung"**-Registerkarte zunächst nicht sichtbar ist — es wird beim Laden nicht automatisch geprüft, ob bereits eine Autonome Aufgabe initialisiert wurde.

### 4a. Autonome Aufgabe beenden

Ein Klick auf den Ribbon-Button **„Stop"** (Gruppe **„Autonome Aufgabe"**) stoppt den laufenden CLI-Prozess und merkt sich dauerhaft, dass die Aufgabe **explizit gestoppt** wurde. Das ist wichtig für das Verhalten bei einem App-Neustart (siehe Abschnitt 8): Nur explizit gestoppte Aufgaben werden beim nächsten Programmstart **nicht** automatisch fortgesetzt. Der Button ist auch dann klickbar, wenn der CLI-Prozess bereits von selbst beendet wurde (z. B. zwischen zwei Arbeitsschritten oder nach einem Absturz) — das Stoppen-Flag wird in jedem Fall gesetzt.

### 5. Fortschritt überwachen (optional)

Die **„Automatisierung"**-Registerkarte in der Aufgabendetailansicht enthält ein eigenes Register mit folgenden Tabs:

| Tab | Inhalt | Aktualisierung |
|-----|--------|----------------|
| **Konfiguration** | Übersicht der Initialisierungs-Konfiguration (Projektbranch, Ressourcenlimits, Persistenzmodus) | Statisch |
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

### 8. Verhalten bei Beenden und Neustart des Programms

Wird das Programm beendet und später wieder gestartet, prüft es beim Start automatisch alle Autonomen Aufgaben:

- **Nicht explizit gestoppte Aufgaben** (Status weiterhin aktiv, siehe Abschnitt 4a) werden **automatisch fortgesetzt**: Die CLI wird neu gestartet, dabei wird — sofern das verwendete KI-Plugin Session-Fortsetzung unterstützt — versucht, an die zuletzt aktive Session anzuknüpfen, und ein „Weitermachen"-Prompt wird automatisch an die neue CLI-Session gesendet, der den Agenten auffordert, anhand von `state.json`, `plan.md` und `progress.md` den aktuellen Stand zu prüfen und fortzufahren.
- **Explizit gestoppte Aufgaben** (Button „Stop" wurde geklickt) werden **nicht** automatisch neu gestartet. Um sie fortzusetzen, ist ein manueller Klick auf **„Start"** im Ribbon erforderlich.

Fehler bei dieser automatischen Wiederaufnahme (z. B. wenn das Arbeitsverzeichnis nicht mehr existiert) werden protokolliert, verhindern aber nicht den normalen Programmstart — betroffen ist immer nur die jeweilige Aufgabe.

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
| **Repository-Klon fehlgeschlagen** | Fehlermeldung „Git-Fehler" | Überprüfe Git-Zugang und Netzwerkverbindung; löse die Initialisierung erneut aus (partieller Klon wird überschrieben) |
| **Projektbranch-Erstellung fehlgeschlagen** | Fehlermeldung „Branch konnte nicht angelegt werden" | Überprüfe, ob der Branch-Name gültig ist; versuche mit anderem Namen neu zu initialisieren |
| **Agent bricht unerwartet ab** | Status springt zu „Fehler" | Siehe Logs in `logs/agent.log` im Arbeitsverzeichnis |
| **Heartbeat-Timeout** | Agent wird pausiert, obwohl aktiv | Prüfe Netzwerkverbindung; erhöhe ggf. Heartbeat-Timeout in Einstellungen |
| **Permission-Fehler bei Unteragent** | Agent kann Dateien nicht schreiben | Unteragent versucht außerhalb seines Scope zu arbeiten — manuell debuggen |

## Tipps

- **Detaillierter Initialprompt spart Zeit** — Je klarer die Anforderung, desto bessere Taskverteilung
- **Passende Budget-Grenzen wählen** — Nicht zu niedrig (häufige Pausen), nicht zu hoch (Fehler nicht erkannt)
- **Logs regelmäßig prüfen** — `logs/agent.log` gibt Aufschluss über Agent-Entscheidungen
- **Underagenten-Fortschritt tracken** — Im „Unteragenten"-Tab die Anzahl fertiger Tasks verfolgen
