# F006 – Aufgabe abschließen

## Einleitung

Diese Funktion beendet eine Aufgabe sauber und nachvollziehbar.  
Sie sichern erst Ihre Änderungen und schließen danach den Arbeitsvorgang ab.  
So bleibt klar dokumentiert, was erledigt wurde und was als Nächstes geprüft wird.

![Aufgabe abschließen](../images/F006-abschliessen.png)

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender nach einem erfolgreichen KI-Lauf.  
Sie ist wichtig für alle, die Ergebnisse geordnet an das Team übergeben möchten.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen die Aufgabe mit dem Status **In Bearbeitung**.
2. Sie klicken auf **Commit** und geben eine verständliche Nachricht ein.
3. Sie öffnen **Push/Pull** und klicken auf **Push**.
4. Falls Ihr SCM-Plugin Pull Requests unterstützt (z. B. GitHub), klicken Sie auf **Pull Request** und prüfen Titel/Beschreibung.
5. Optional klicken Sie auf **PR erstellen**. Bei Aufgaben mit verknüpfter Issue ergänzt die Anwendung automatisch `Closes #<Issue>`.
6. Zum Schluss klicken Sie auf **Abschließen**.

---

## Beispiel

Die KI hat ein neues Suchfeld umgesetzt.  
Sie speichern den Stand über **Commit**.  
Danach übertragen Sie ihn mit **Push**:
- bei Remote-Plugins als klassischer Push zum Remote-Repository,
- beim **LocalDirectoryPlugin** als Dateisynchronisation vom Arbeitsverzeichnis in den Quellordner (kein `git push`).
Anschließend erstellen Sie bei Bedarf einen Prüfvorschlag via **PR erstellen** und schließen die Aufgabe ab.
Bei einer zuvor ausgewählten Issue wird diese beim Merge automatisch geschlossen.

---

## Was passiert im Hintergrund?

Beim Commit wird ein fester Zwischenstand Ihrer Änderungen gespeichert.  
Mit Push wird dieser Stand pluginabhängig übertragen:
- **Remote-SCM-Plugins:** Push zum Remote-Repository
- **LocalDirectoryPlugin:** lokaler Datei-Sync `WorkingDirectory -> SourceDirectory` inkl. Delete-Sync über Git-Status (`git status --porcelain`)  
Beim PR-Erstellen ergänzt die Anwendung bei verknüpfter Issue automatisch die Closing-Direktive (`Closes #<IssueNummer>`), falls sie noch nicht im Body enthalten ist.
Beim Abschließen wird die Aufgabe als erledigt markiert.

---

## Häufige Fragen (FAQ)

**Kann ich eine Aufgabe ohne PR abschließen?**  
Ja. Das ist möglich, aber für Teamprüfung meist nicht sinnvoll.

**Ist Push immer ein `git push` zum Remote?**  
Nein. Beim **LocalDirectoryPlugin** ist Push bewusst eine lokale Dateisynchronisation (kein Remote-`git push`).

**Muss ich immer erst committen?**  
Für einen sauberen Ablauf ja, sonst fehlen klare Zwischenstände.

**Was mache ich bei einem Push-Fehler?**  
Prüfen Sie die Meldung im Protokoll und wiederholen Sie den Schritt.

**Kann ich nach dem Abschluss weiterarbeiten?**  
Für weitere Änderungen legen Sie eine neue Aufgabe an.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [F019 – Issue-, Branch- und PR-Verknüpfung](./F019-issue-branch-pr-verknuepfung.md)
- [Zurück zur Übersicht](../features.md)
