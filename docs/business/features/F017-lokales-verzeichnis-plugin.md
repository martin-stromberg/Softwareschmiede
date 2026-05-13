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
3. Wählen Sie den **Workspace-Modus**:
   - `SeparateWorkingDirectory` (Standard, empfohlen)
   - `InSourceDirectory` (direkt im Quellordner)
4. Starten Sie den KI-Prozess für Ihre Aufgabe.
5. Die Softwareschmiede bereitet den Workspace entsprechend des Modus vor.

---

## Workspace-Modi im Alltag

### SeparateWorkingDirectory (Standard)

- Der Quellordner bleibt unverändert.
- Es wird eine getrennte Arbeitskopie erstellt.
- Schutzregeln verhindern riskante Kopien (z. B. zu viele Dateien, zu große Datenmenge, Timeout).

### InSourceDirectory

- Die Arbeit erfolgt direkt im Quellordner.
- Falls dort noch kein Git-Repository existiert, ist eine **explizite Bestätigung** für `git init` nötig.
- Bei ungesicherten lokalen Änderungen wird der Start aus Sicherheitsgründen abgebrochen.

---

## Was passiert im Hintergrund?

- Das Plugin arbeitet lokal und unterstützt die Kernschritte **Branch erstellen**, **Commit** und **Reset**.
- Nicht passende Remote-Funktionen (z. B. Pull Request erstellen, Push/Pull, Issues laden) sind bewusst nicht verfügbar.
- Beim Modus `InSourceDirectory` wird eine Zuordnung zum echten Arbeitsort gespeichert, damit Folgeschritte stabil im richtigen Ordner laufen.

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
