# F021 – Live Project Browser mit Git-Status

## Einleitung

Auf der Aufgabenseite sehen Sie den aktuellen Git-Status des verknüpften lokalen Repositories direkt im Browser.
Sie können zwischen Aufgabenansicht und Projektverzeichnis wechseln, Dateien auswählen und den Inhalt oder eine Vergleichsansicht prüfen.

---

## Wer nutzt es?

- Entwickler, die vor einem Commit schnell den Repository-Zustand prüfen möchten.
- Reviewer, die Änderungen ohne externes Git-Tool nachvollziehen wollen.
- Fachanwender, die auf der Aufgabenseite bleiben und trotzdem den lokalen Stand sehen möchten.

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie eine Aufgabe.
2. Prüfen Sie die Kennzahlen für Commits und lokale Änderungen.
3. Klicken Sie auf **🗂️ Projektverzeichnis**.
4. Wechseln Sie bei Bedarf zwischen **Baum** und **Liste**.
5. Klicken Sie auf eine Datei, um Inhalt, Hinweis oder Vergleich anzuzeigen.
6. Nutzen Sie **↻ Aktualisieren**, um den aktuellen Git-Stand neu zu laden.
7. Wechseln Sie mit **← Zur Aufgabe** zurück.

---

## Was passiert im Hintergrund?

- Die Aufgabenseite lädt den lokalen Repository-Zustand über `IGitWorkspaceBrowserService`.
- Die Commit-Zahl wird aus `git rev-list --count HEAD` ermittelt.
- Änderungen werden aus `git status --porcelain=v1 --untracked-files=all` gelesen.
- Der Explorer zeigt staged und unstaged Änderungen getrennt an.
- Verzeichnisse werden rekursiv als Baum dargestellt; zusätzlich gibt es eine flache Listenansicht.
- Beim Klick auf eine Datei wird der Arbeitsstand gelesen oder die Originalversion aus `HEAD` geladen.
- Große Dateien und Binärdateien werden defensiv mit Hinweis behandelt.

---

## Grenzen

- Die Ansicht ist schreibgeschützt.
- Commit-, Push- und Pull-Aktionen bleiben auf der regulären Aufgabenansicht.
- Sehr große Repositories mit vielen untracked Dateien bleiben ein dokumentierter Performance-Trade-off.

---

## Häufige Fragen (FAQ)

**Bleibt die Ansicht nach F5 erhalten?**  
Ja, die Ansicht wird über den Query-Parameter `?view=task` bzw. `?view=tree` wiederhergestellt.

**Kann ich Dateien direkt bearbeiten?**  
Nein. Der Browser ist nur zur Inspektion gedacht.

**Was sehe ich bei gelöschten Dateien?**  
Die Originalversion aus `HEAD` wird angezeigt.

---

## Verwandte Funktionen

- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md)
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md)
- [Requirements Analysis](../../requirements/live-project-browser-git-status-requirements-analysis.md)
- [Architecture Blueprint](../../architecture/live-project-browser-git-status-architecture-blueprint.md)
- [Flow-Dokumentation](../../flows/live-project-browser-git-status-flow.md)
- [Zurück zur Übersicht](../features.md)
