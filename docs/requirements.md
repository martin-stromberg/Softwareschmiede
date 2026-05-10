# Anforderungen – Softwareschmiede

## 1. Überblick

**Softwareschmiede** ist eine webbasierte Anwendung (Blazor), die den gesamten Workflow der KI-gestützten Softwareentwicklung verwaltet. Sie verbindet Projektmanagement, Git-Integration, Aufgabenverwaltung und KI-Steuerung in einer einheitlichen Oberfläche.

---

## 2. Ziele

- Verwaltung mehrerer Softwareprojekte an einem zentralen Ort
- Strukturierte Erfassung von Anforderungen je Aufgabe
- Automatisierte Umsetzung von Anforderungen durch KI-Plugins
- Nachvollziehbarer Verlauf jeder KI-gesteuerten Entwicklungsaufgabe
- Erweiterbarkeit durch Plugins für Git-Provider und KI-Systeme

---

## 3. Funktionale Anforderungen

### 3.1 Projektverwaltung

- Der Anwender kann beliebig viele **Projekte** anlegen, bearbeiten und löschen.
- Jedes Projekt kann optional mit **beliebig vielen Git-Repositories** verknüpft werden.
- Ein Projekt enthält Metadaten (Name, Beschreibung, Erstellungsdatum, Status).
- Projekte können **archiviert** werden (keine neuen Aufgaben, aber lesbar).

### 3.2 Git-Plugin-System

- Die Anwendung stellt ein **Plugin-System für Git-Provider** bereit.
- Plugins werden registriert und konfiguriert (z. B. Token, URL).
- Jedes Git-Plugin muss folgende Funktionen anbieten:
  - Repository-Verknüpfung mit einem Projekt (mehrere Repositories je Projekt möglich)
  - Abrufen von **Issues** aus dem Git-Provider (wird beim Öffnen/Auswählen eines Projekts automatisch ausgelöst)
  - Klonen des Repositories in ein **aufgabenspezifisches lokales Verzeichnis**
  - Anlegen eines **aufgabenspezifischen Branches** im geklonten Repository
  - **Push** des Branches auf den Remote
  - **Pull** (Änderungen vom Remote holen)
  - Erstellen eines **Pull Requests**
- **Schreibzugriff auf Issues** (Kommentare, Status-Updates) ist für eine spätere Version vorgesehen.
- **Erstes Plugin:** GitHub – gesteuert über die **GitHub CLI (`gh`)**

#### Übernommene Issue-Felder

| Feld | Beschrieben |
|------|-------------|
| Titel | Titel des Issues |
| Body | Beschreibungstext des Issues |
| Labels | Alle Labels des Issues |
| Milestone | Verknüpfter Meilenstein |

### 3.3 Aufgabenverwaltung

- Jedes Projekt kann beliebig viele **Aufgaben** enthalten.
- Eine Aufgabe kann auf einem **Issue** des verknüpften Git-Providers basieren (Übernahme aus dem Issue-Feed: Titel, Body, Labels, Milestone).
- Alternativ erfasst der Anwender eine **freie Anforderungsbeschreibung** direkt in der Anwendung.
- Aufgaben haben mindestens folgende Attribute:
  - Titel
  - Anforderungsbeschreibung / Referenz auf Issue
  - Status (offen, in Bearbeitung, KI aktiv, abgeschlossen, fehlgeschlagen)
  - Verknüpftes KI-Protokoll
  - Ausgewähltes Agentenpaket
  - Ausgewählter Agent (aus dem Agentenpaket)
- Jede Aufgabe erhält beim Start des Entwicklungsprozesses einen **eigenen Repository-Klon** und einen **eigenen Branch**.

#### Aufgaben-Lebenszyklus
- **Abschließen:** Der Anwender veranlasst manuell einen Pull Request (über das Git-Plugin). Nach dem PR kann er die Aufgabe als abgeschlossen markieren → lokaler Branch und Klon werden automatisch gelöscht.
- **Abbrechen:** Der Anwender kann eine Aufgabe jederzeit abbrechen → lokaler Branch und Klon werden gelöscht; keine Änderungen werden gepusht.
- Der Anwender kann den Branch jederzeit **pushen** (Remote-Branch anlegen / aktualisieren) und **pullen** (Änderungen vom Remote holen).

#### Repository-Klon & Branch pro Aufgabe
- Branch-Namenskonvention: `task/<aufgaben-id>-<kurzname>`
- Das Repository wird in ein **aufgabenspezifisches Verzeichnis** geklont.
- Der Anwender kann jederzeit **Commits** auf dem Branch durchführen.
- Der Anwender kann **Commits zurücksetzen** – wahlweise:
  - Mit Beibehalten der Änderungen im Arbeitsverzeichnis (`git reset --soft` / `--mixed`)
  - Ohne Beibehalten der Änderungen (`git reset --hard`)
- Die KI kann selbstständig Commits durchführen, sofern sie dazu in der Lage ist (abhängig vom KI-Plugin).

### 3.4 Entwicklungsprozess (KI-gestützt)

- Für jede Aufgabe kann der Anwender den **Entwicklungsprozess starten**.
- Die Anwendung klont das verknüpfte Repository in ein **aufgabenspezifisches Arbeitsverzeichnis** und legt einen eigenen Branch an.
- Das Basis-Arbeitsverzeichnis für Klone ist benutzerseitig in den Einstellungen konfigurierbar (`repositories.workdir`).
- Ist kein gültiger Pfad verfügbar, verwendet die Anwendung automatisch einen Fallback auf Basis von `Path.GetTempPath()`.
- Das ausgewählte **KI-Plugin** wird mit den Anforderungen als Prompt gesteuert.
- Das KI-Plugin erhält das ausgewählte **Agentenpaket** und legt dessen Dateien strukturiert im Branch ab (z. B. `.github/`-Ordner beim GitHub-Copilot-Plugin).
- Der Fortschritt und die Ausgabe der KI wird in Echtzeit im Aufgabenprotokoll angezeigt.
- **Iterationen** sind möglich: Der Anwender kann nach einem KI-Lauf direkt einen Folge-Prompt aus dem Protokoll heraus starten.
- **Rollbacks** erfolgen entweder durch KI-Entscheidung (die KI nimmt Korrekturen selbstständig vor) oder manuell durch den Anwender (Commit-Reset, siehe 3.3).
- Die KI kann **Tests ausführen** und deren Ergebnisse auswerten.

#### Agenten-Auswahl pro Prompt
- Das KI-Plugin liest das gewählte Agentenpaket und liefert eine **Liste verfügbarer Agenten**.
- Vor jedem Prompt wählt der Anwender den **zu verwendenden Agenten** aus dieser Liste aus.
- Der gewählte Agent wird für den nächsten KI-Lauf verwendet.

### 3.5 KI-Plugin-System

- Die Anwendung stellt ein **Plugin-System für KI-Systeme** bereit.
- Jedes KI-Plugin muss folgende Funktionen anbieten:
  - Entgegennahme eines Anforderungsprompts und eines Agenten
  - Strukturiertes Ablegen des Agentenpakets im Branch (plugin-spezifisches Layout)
  - Steuerung der KI auf einem lokalen Repository-Klon
  - Ausführung von Tests und Auswertung der Ergebnisse
  - Liefern von Rückmeldungen / Ergebnissen an die Anwendung (Echtzeit-Streaming)
  - Liefern einer Liste verfügbarer Agenten aus dem gewählten Agentenpaket
- **Erstes Plugin:** GitHub Copilot – gesteuert über die **GitHub CLI (`gh copilot`)**
  - Legt das Agentenpaket im `.github/`-Verzeichnis des Branches ab

### 3.6 Agentenpakete

- In einem **festen Verzeichnis** `<Programmverzeichnis>/agent-packages/` können Ordner mit Agentenpaketen abgelegt werden. Das Verzeichnis ist nicht konfigurierbar und wird beim App-Start automatisch angelegt.
- Jeder Unterordner repräsentiert ein **Agentenpaket**.
- Ein Agentenpaket kann aus **beliebig vielen Dateien und Unterverzeichnissen** bestehen, z. B.:
  - Agentendateien (`.agent.md`, `.agents.yml`, …)
  - Skilldateien (`.skills.md`, …)
  - Kommandodateien
  - Skriptdateien
  - Sonstige Konfigurationsdateien
- Die Anwendung liest die vorhandenen Agentenpakete ein und stellt sie zur Auswahl bereit.
- Der Anwender wählt das Agentenpaket **pro Aufgabe** aus.
- Agentenpakete sind in der Oberfläche **vorschaubar** (Dateiliste, Beschreibung, enthaltene Agenten).
- Das KI-Plugin liest das Agentenpaket und liefert eine **Liste verfügbarer Agenten** zurück (plugin-spezifische Auswertung der Paketstruktur).

### 3.7 Aufgabenprotokoll

- Jede Aufgabe besitzt ein **Protokoll**, das den gesamten KI-gesteuerten Entwicklungsverlauf dokumentiert.
- Das Protokoll enthält:
  - Alle gesendeten Anforderungsprompts (inkl. gewähltem Agent)
  - Alle Rückmeldungen / Antworten der KI (Echtzeit-Streaming)
  - Zeitstempel je Eintrag
  - Status-Übergänge der Aufgabe
- Das Protokoll ist in der Anwendung **lesbar und durchsuchbar**.
- Ein Export des Protokolls ist **nicht** vorgesehen.
- Aus dem Protokoll heraus kann der Anwender direkt einen **Folge-Prompt starten** (iterativer Dialog ohne Seitenwechsel).

### 3.8 Startseite / Dashboard

- Die **Startseite** zeigt eine Liste aller aktuell gestarteten (aktiven) Aufgaben über alle Projekte hinweg.
- Je Aufgabe wird der **aktuelle Status** angezeigt, z. B.:
  - KI aktiv
  - Warten auf Eingabe
  - Tests werden ausgeführt
  - Abgeschlossen
  - Fehlgeschlagen
- Ein Klick auf eine Aufgabe führt direkt zum Aufgabenprotokoll.

---

## 4. Nicht-funktionale Anforderungen

| Kategorie | Anforderung |
|-----------|-------------|
| Technologie | Blazor (Web, interaktiver Server-Modus) |
| Erweiterbarkeit | Alle Plugins (Git, KI) über definierte Interfaces austauschbar |
| Persistenz | Lokale Datenbank (SQLite via EF Core) |
| Sicherheit | API-Tokens werden im **Windows Credential Store** gespeichert (kein Klartext in DB oder Code) |
| Benutzer | Einzelnutzer-Anwendung (kein Login / Benutzerverwaltung) |
| Benutzeroberfläche | Einsprachig Deutsch, responsive, einheitliches Design |
| Lokalisierung | Vorbereitung für mehrsprachige Erweiterung (i18n via resx) |

---

## 5. Systemgrenzen & Abgrenzungen

- Die Anwendung läuft **lokal** auf dem Rechner des Anwenders (kein Cloud-Deployment im ersten Schritt).
- Die KI führt die Codeänderungen auf dem **geklonten Branch** durch.
- Commits können durch die KI (automatisch) oder den Anwender (manuell) erfolgen.
- **Push und Pull** des aufgabenspezifischen Branches sind möglich.
- **Pull Requests** werden über das Git-Plugin veranlasst.

---

## 6. Domänenmodell (grob)

```
Projekt (archivierbar)
 ├── GitRepositories[] (0..n, via Git-Plugin)
 ├── Aufgaben[]
 │    ├── IssueReferenz (optional: Titel, Body, Labels, Milestone)
 │    ├── Anforderungsbeschreibung (frei)
 │    ├── Branch (aufgabenspezifisch)
 │    ├── LokalerKlon (aufgabenspezifisches Verzeichnis)
 │    ├── AgentenpaketAuswahl
 │    ├── AgentAuswahl (aus dem Agentenpaket, je Prompt)
 │    ├── Status (offen | in Bearbeitung | KI aktiv | abgeschlossen | fehlgeschlagen)
 │    └── Protokolleinträge[]
 │         ├── Prompt + Agent
 │         ├── KI-Antwort
 │         ├── Zeitstempel
 │         └── Typ (Prompt | Antwort | Status | Test-Ergebnis)
 └── Status

Plugin-System
 ├── IGitPlugin (z.B. GitHubPlugin via `gh` CLI)
 └── IKiPlugin  (z.B. GitHubCopilotPlugin via `gh copilot` CLI)

Agentenpaket
 └── Verzeichnis
      ├── Agentendateien (.agent.md, …)
      ├── Skilldateien
      ├── Kommandodateien
      ├── Skriptdateien
      └── Sonstige Dateien / Unterverzeichnisse
```

---

## 7. Entschiedene Konfigurationspunkte

| Punkt | Entscheidung |
|-------|-------------|
| Branch-Namenskonvention | `task/<aufgaben-id>-<kurzname>` |
| Agentenpaket-Verzeichnis | Fest: `<Programmverzeichnis>/agent-packages/`; nicht konfigurierbar; wird beim App-Start automatisch angelegt falls nicht vorhanden |
| Agentenpaket-Verwaltung | Rein manuell: Pakete werden als Ordner im konfigurierten Verzeichnis abgelegt; kein Download aus Onlinequellen |
| Lokaler Klon / Branch nach Aufgabenende | Anwender entscheidet explizit: **Aufgabe abschließen** (nach PR) → Branch & Klon werden gelöscht; **Aufgabe abbrechen** → Branch & Klon werden ebenfalls gelöscht |
| Pull Request | Wird durch den Anwender manuell veranlasst, wenn er die Aufgabe als erledigt betrachtet; Voraussetzung zum Abschluss der Aufgabe |
| Test-Ergebnisse im Protokoll | Strukturiert: Testname, Status (bestanden/fehlgeschlagen/übersprungen), Fehlermeldung, Dauer; tabellarisch oder als Baumstruktur je nach Testframework-Ausgabe |
| Push / Pull / Pull Request | Alle drei sind Teil des ersten Umfangs; werden über das Git-Plugin bereitgestellt |

---

## 8. Offene Punkte / Zu klärende Details

*Keine offenen Punkte.*

---

*Erstellt: 2026-05-06 | Status: Entwurf – wird fortlaufend ergänzt*
