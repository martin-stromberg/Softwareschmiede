# F011 – GUID-präfixierte Copilot-Task-Datei

## Einleitung
Beim Start eines KI-Laufs speichert Softwareschmiede Ihre Anweisung als Datei im Format `{executionId}.copilot-task.md` im Arbeitsrepository.  
Zusätzlich wird sichergestellt, dass alle solchen Dateien per `*.copilot-task.md` in `.gitignore` geschützt sind.

---

## Wer nutzt es?
- **Entwickler**, die KI-Läufe eindeutig korrelieren und nachvollziehen möchten.
- **Repository-Verantwortliche**, die temporäre Prompt-Dateien zuverlässig aus Commits ausschließen wollen.

---

## Was passiert für Sie sichtbar?
1. Sie starten einen KI-Lauf wie gewohnt.
2. Die Anwendung verwendet eine eindeutige Ausführungs-ID (`executionId`) pro Lauf.
3. Der Prompt wird lokal als `{executionId}.copilot-task.md` gespeichert.
4. Die `.gitignore`-Regel `*.copilot-task.md` wird einmalig korrekt sichergestellt.
5. Der KI-Lauf startet und streamt die Ausgabe in die Oberfläche.

---

## Fachlicher Nutzen
- **Eindeutige Nachverfolgung:** Jeder Lauf hat eine eigene, korrelierbare Prompt-Datei.
- **Sicherer Workflow:** Keine versehentlichen Commits von Prompt-Dateien.
- **Robustheit:** Alte Ignore-Regeln werden konsolidiert, Duplikate vermieden.

---

## Häufige Fragen (FAQ)
**Muss ich eine `executionId` selbst vergeben?**  
Nein. Wenn keine angegeben ist, erzeugt das System automatisch eine GUID.

**Werden alte `.gitignore`-Einträge unterstützt?**  
Ja. Legacy-Regeln wie `/.copilot-task.md` werden auf `*.copilot-task.md` konsolidiert.

**Wird die Task-Datei nach dem Lauf entfernt?**  
Ja, der Cleanup läuft immer am Ende; Fehler dabei blockieren den Hauptablauf nicht.

---

## Verwandte Funktionen
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F010 – Plugin-Prinzip für Integrationen](./F010-plugin-prinzip-integrationen.md)
- [Technische API-Doku](../../api/copilot-task-binding.md)
- [Ablaufdiagramm](../../flows/copilot-task-binding-flow.md)
- [Zurück zur Übersicht](../features.md)
