← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Beschreibung

## Zweck

Autonome Aufgaben automatisieren den gesamten Entwicklungsprozess durch einen intelligenten Agenten (Projektleiter), der die Aufgabe selbstständig in manageable Teilaufgaben zerlegt, spezialisierte Unteragenten erzeugt und orchestriert, und Pull Requests vorbereitet. Sie ermöglichen großflächige, selbstgesteuerte Projektentwicklung ohne manuelle Intervention des Anwenders für jeden Teilschritt.

## Funktionsweise

### Initialisierung

Eine Autonome Aufgabe wird über ein Initialisierungsformular konfiguriert:

1. **Projektbranch** — Git-Branch, in dem die gesamte Aufgabe koordiniert wird. Ist der Aufgabe ein Git-Repository mit passendem Plugin zugeordnet, schlägt der Dialog die Remote-Branches des Repositories in einer Auswahlliste vor; ohne Repository/Plugin fällt das Feld auf eine freie Texteingabe zurück. Über den **„+"-Button** neben der Auswahl kann direkt aus dem Dialog heraus ein neuer Branch angelegt werden (siehe unten).
2. **Promptvorlage** — Optionale Auswahl einer vorhandenen Prompt-Vorlage, die den Initialprompt automatisch mit aufgelöstem Vorlagentext vorbefüllt (siehe unten)
3. **Initialprompt** — Fachliche Anforderung für den Projektleiter-Agenten
4. **Permissions** — Berechtigungsprofil (automatisch generiert, aus Datei gewählt oder vordefiniert)
5. **Token-Budget** — Maximale Token für die gesamte Aufgabe (Standard: 500.000)
6. **Laufzeitbegrenzung** — Maximale Nettozeit in Minuten (Standard: 480 Min / 8 Std)
7. **Persistenz-Modus** — Verhalten bei Session-Unterbrechungen (Standard, SitzungZuruecksetzen)
8. **Skill-Autogeneration** — Skills automatisch aus Anforderungen generieren?

Über den **„Hilfe"-Button** oben im Dialog kann sich der Anwender jederzeit den Ablauf einer Autonomen Aufgabe (Initialisierung, Agent-Start, Unteragenten, Fortschritt/Integration, Session-Pause) sowie eine Kurzerklärung aller Formularfelder in einem Informationsdialog anzeigen lassen.

### Projektbranch-Auswahl und Branch-Neuanlage

Der Dialog ermittelt beim Öffnen über das der Aufgabe zugeordnete Git-Plugin die verfügbaren Remote-Branches des Repositories:

- Ist ein Repository samt passendem Plugin vorhanden und liefert es mindestens einen Branch, wird eine Auswahlliste (Dropdown) angezeigt; vorausgewählt ist der Branch der Aufgabe.
- Kann keine Branch-Liste ermittelt werden (kein Repository, kein passendes Plugin, oder Fehler beim Laden), wechselt das Feld automatisch auf eine freie Texteingabe.
- Über den „+"-Button neben dem Auswahlfeld öffnet sich eine Eingabezeile für einen neuen Branchnamen. Nach Bestätigung wird der Branch im lokalen Klon der Aufgabe angelegt (ausgehend vom aktuell gewählten Projektbranch als Basis), der Auswahlliste hinzugefügt und automatisch als Projektbranch übernommen. Ist kein lokaler Klon vorhanden oder schlägt die Anlage fehl, wird eine Fehlermeldung angezeigt, ohne den Dialog zu schließen.

### Promptvorlagen für den Initialprompt

Der Dialog bietet eine Auswahl bestehender Promptvorlagen (aus der allgemeinen Promptvorlagen-Verwaltung der Anwendung) an. Wird eine Vorlage ausgewählt, wird ihr Vorlagentext mit automatisch aufgelösten Platzhaltern (z. B. Aufgaben- oder Projektbezug) in das Initialprompt-Feld übernommen; der Anwender kann den Text danach weiter anpassen.

Nach der Initialisierung:
- Strukturiertes Arbeitsverzeichnis wird erstellt
- Repository wird geklont
- `state.json` wird mit Initialkonfiguration generiert
- Projektleiter-Agent wird vorbereitet, aber noch nicht gestartet

### Ausführung

Der **Projektleiter-Agent** läuft innerhalb seines Arbeitsverzeichnisses und:

1. Liest den Initialprompt und versteht die Gesamtaufgabe
2. Entwirft einen Gesamtplan und aktualisiert `plan.md`
3. Identifiziert Teilaufgaben und erzeugt Unteragenten
4. Verwaltet jeden Unteragenten innerhalb von Governance-Grenzen
5. Integriert Ergebnisse in Fortschrittsberichte
6. Bereitet einen finalen Pull Request vor (kein automatisches Merging)

Jeder **Unteragent** wird mit:
- Eigenem Arbeitsverzeichnis (`tasks/task_XXX/`)
- Eigenem Branch (`feature-unteragent-XXX`)
- Eigenem Repository-Klon (`clones/repo_feature_XXX/`)
- Governance-Schutzmechanismen (darf nur im eigenen Bereich arbeiten)

## Arbeitsverzeichnis-Struktur

```
/autonomous-task-{aufgabe-id}/
    plan.md                      (Gesamtplan, vom Projektleiter verwaltet)
    progress.md                  (Fortschrittsprotokoll, live aktualisiert)
    state.json                   (Maschinenzustand: Budgets, Branches, Agenten, Status)
    governance.md                (Governance-Regeln und Limits)
    permissions.json             (Berechtigungsprofil, unveränderbar)
    skills/
        skill_projektleiter_v1.md (Hauptskill für Projektleiter)
        skill_xyz_v2.md          (Weitere Skills)
        archive/
            skill_xyz_v1.md       (Archivierte ältere Versionen)
    clones/
        repo_main/               (Hauptklon des Quellrepositories)
        repo_feature_1/          (Feature-Branch-Klone)
        repo_feature_2/
    tasks/
        task_001/                (Arbeitsbereich des Unteragenten 1)
            task_report.md
            task_changes.json
            task_log.md
        task_002/
    logs/
        cli.log                  (Befehlsprotokoll)
        agent.log                (Agenten-Aktivitätslog)
```

## Beispiele

### Szenario 1: Backend-Feature mit mehreren Komponenten

**Initialprompt:** "Implementiere ein Authentifizierungssystem mit JWT-Tokens, bestehend aus: (1) Token-Generierung und -Validierung, (2) API-Middleware für Token-Prüfung, (3) Datenbankmigrationen für Benutzer und Token-Tabellen, (4) Unit-Tests."

**Projektleiter-Verhalten:**
1. Erkennt 4 Teilaufgaben
2. Erzeugt 4 Unteragenten (auth-tokens, auth-middleware, db-migrations, unit-tests)
3. Verwaltet deren parallele Ausführung (oder sequenzielle mit Abhängigkeiten)
4. Integriert jeden Teilabschluss in `progress.md`
5. Bereitet PR vor, die alle Commits zusammenfasst

### Szenario 2: Session-Pause und Wiederaufnahme

1. Projektleiter läuft mit 500.000-Token-Budget
2. Nach 350.000 Tokens wird Session pausiert
3. Anwender kann später mit erweitertem Budget (750.000 Tokens) fortsetzen
4. Projektleiter wird mit "Weitermachen"-Prompt neugestartet
5. Läuft weiter bis zum Abschluss oder nächster Pause

## Einschränkungen

- **Keine automatischen Pull Request Merges** — Der Projektleiter bereitet PRs vor, der Merge erfolgt manuell durch Anwender oder CI/CD-Pipeline
- **Unteragenten können nur in ihrem Scope arbeiten** — Zugriff auf andere Verzeichnisse ist durch `UnteragentGovernanceService` blockiert
- **Projektleiter kann Skills nicht verändern** — Skills sind nach Freigabe unveränderlich; neue Versionen müssen explizit erzeugt werden
- **Session-Limits** — Netto-Laufzeit ist begrenzt; zu lange Pausen führen zu Heartbeat-Timeouts
- **Token-Budget ist hart** — Nach Erreichen des Limits muss eine Pause eingelegt werden; Erweiterung erfordert Benutzer-Bestätigung
- **Arbeitsverzeichnis-Persistence** — Wenn das Arbeitsverzeichnis gelöscht wird, gehen state.json und Logs verloren; nur die DB-Entities bleiben
