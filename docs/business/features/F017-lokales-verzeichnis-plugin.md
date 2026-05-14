# F017 – Lokales Verzeichnis Plugin

## Einleitung

Mit dem **Lokales Verzeichnis Plugin** können Sie statt eines Remote-Repositorys direkt mit einem lokalen Quellordner arbeiten.  
Das ist sinnvoll für interne Codebasen, Offline-Szenarien oder vorbereitete Arbeitsstände ohne GitHub-Anbindung.

---

## Wer nutzt es?

- Teams, die lokal vorliegende Codeordner mit der Softwareschmiede bearbeiten wollen.
- Fachanwender, die zwischen sicherer Arbeitskopie und direkter Bearbeitung im Quellordner wählen müssen.

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie **Einstellungen**.
2. Stellen Sie als SCM-Plugin **Local Directory** ein (optional als Standard).
3. Wählen Sie den **Workspace-Modus** über die klaren UI-Texte:
   - **Mit separatem Arbeitsverzeichnis arbeiten** (`SeparateWorkingDirectory`, Standard, empfohlen)
   - **Direkt im Quellverzeichnis arbeiten** (`InSourceDirectory`)
4. Starten Sie den KI-Prozess für Ihre Aufgabe.
5. Die Softwareschmiede bereitet den Workspace entsprechend des Modus vor.

---

## Workspace-Modi im Alltag

### SeparateWorkingDirectory (Standard)

- Der Quellordner bleibt unverändert.
- Es wird eine getrennte Arbeitskopie erstellt.
- Das Ziel der Arbeitskopie wird aus dem globalen Arbeitsverzeichnis (`repositories.workdir`) und der Aufgaben-ID gebildet.
- Schutzregeln verhindern riskante Kopien (z. B. zu viele Dateien, zu große Datenmenge, Timeout).

#### Git-Workflow-Fallback im separaten Arbeitsverzeichnis

Beim Vorbereiten der Arbeitskopie nutzt das System eine feste Reihenfolge:

1. **Quelle ist bereits ein Git-Repository**  
   → Die Arbeitskopie wird per Clone erstellt.
2. **Quelle ist kein Git-Repository + „git init im Quellverzeichnis bestätigen“ = Ja**  
   → Die Quelle wird initialisiert, danach wird geklont.
3. **Quelle ist kein Git-Repository + Bestätigung = Nein**  
   → Die Dateien werden als sichere Kopie übernommen, im Arbeitsverzeichnis wird `git init` ausgeführt und ein initialer Snapshot-Commit angelegt.

Damit bleibt der Modus `SeparateWorkingDirectory` auch bei gemischten Ausgangslagen nutzbar.

### InSourceDirectory

- Die Arbeit erfolgt direkt im Quellordner.
- Falls dort noch kein Git-Repository existiert, ist eine **explizite Bestätigung** für `git init` nötig.
- Bei ungesicherten lokalen Änderungen wird der Start aus Sicherheitsgründen abgebrochen.

---

## Was passiert im Hintergrund?

- Das Plugin arbeitet lokal und unterstützt die Kernschritte **Branch erstellen**, **Commit** und **Reset**.
- Für die Projektverknüpfung wird plugin-gesteuert das Feld **SourceDirectory** abgefragt.
- Nicht passende Remote-Funktionen (z. B. Pull Request erstellen, Remote-Push/Pull, Issues laden) sind bewusst nicht verfügbar.
- Beim Modus `InSourceDirectory` wird eine Zuordnung zum echten Arbeitsort gespeichert, damit Folgeschritte stabil im richtigen Ordner laufen.
- Im Modus `SeparateWorkingDirectory` gilt:
  - **Pull** aktualisiert die Arbeitskopie aus der Quelle als Dateisynchronisation (**ohne Merge**).
  - **Push** überträgt Änderungen aus der Arbeitskopie zurück in die Quelle als Dateisynchronisation (**kein Remote-`git push`**).
  - **Delete-Sync** spiegelt Löschungen/Umbenennungen aus dem Workspace über Git-Status (`git status --porcelain`) in den Quellordner.
  - Die Aktionsleiste nutzt Plugin-Capabilities: **Push/Pull/Pull Request werden ausgeblendet, stattdessen wird „Merge“ eingeblendet**.
  - Der Merge-Button übernimmt Änderungen aus der Arbeitskopie zurück in den Quellordner.
  - Für alle Aktionen gilt: Das projektspezifisch verknüpfte Repository-Plugin hat Vorrang vor dem globalen Standardplugin.

---

## Grenzen, Limitierungen und offene Punkte

### Aktuelle Grenzen

- Dieses Plugin ist für **lokale Verzeichnisse** gedacht, nicht für Remote-Workflows (kein PR, kein Remote-Push).
- Bei Synchronisationen werden technische Metadaten wie `.git` nicht zwischen Quelle und Arbeitskopie gespiegelt.
- Symlinks/Reparse-Points sind aus Sicherheitsgründen ausgeschlossen.
- Bei sehr großen Datenmengen greifen Schutzgrenzen (Dateianzahl, MB, Timeout), um riskante Kopiervorgänge abzubrechen.

### Offene Punkte (fachlich bekannt)

- Für parallele gleichzeitige Push/Pull-Aktionen auf denselben Pfaden sind noch weitergehende Leitplanken vorgesehen.
- Der genaue Konfliktpfad für „Pull ohne Merge“ wird weiter präzisiert (insbesondere bei stark divergierenden lokalen Ständen).
- Audit-Konventionen für automatische Initialisierungen (z. B. einheitliche Metadaten) werden weiter geschärft.
- Ein UI-Bestätigungsdialog vor Pull in der Aufgabenansicht ist als Restpunkt bekannt und bisher nicht automatisiert getestet.

---

## Häufige Fragen (FAQ)

**Kann ich mit dem Plugin ohne GitHub arbeiten?**  
Ja. Das Plugin ist für lokale Verzeichnisse ohne Remote-Provider ausgelegt.

**Welchen Modus soll ich standardmäßig nutzen?**  
In der Regel `SeparateWorkingDirectory`, weil der Quellordner dabei geschützt bleibt.

**Warum wird der Start manchmal abgebrochen?**  
Typische Gründe sind ungesicherte Änderungen, ein nicht leeres Zielverzeichnis oder verletzte Kopier-Grenzwerte.

**Kann ich mit diesem Plugin Pull Requests erstellen?**  
Nein. Dafür benötigen Sie ein Remote-basiertes SCM-Plugin (z. B. GitHub).

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Aufgabe mit dem gewählten SCM-Plugin starten
- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – Basisverzeichnis für Arbeitskopien festlegen
- [F010 – Plugin-Prinzip für Integrationen](./F010-plugin-prinzip-integrationen.md) – Rolle von SCM- und KI-Plugins verstehen
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Local Directory als SCM-Standard setzen
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md) – gespeicherte Plugin-Einstellungen im Alltag
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md) – sicheres Vorgehen bei Abbrüchen
- [Zurück zur Übersicht](../features.md)
