# Offene Aufgaben

Erstellt am: 2026-07-09


## Offene Planelemente

- [x] Keines der SCM-Plugins implementiert die Funktion für den Abruf der Verzeichnisstruktur des Repositories. Dies ist notwendig, um die Verzeichnisstruktur im UI anzuzeigen und die Auswahl von Dateien für die Aufgabenbearbeitung zu ermöglichen.
  Erledigt: `LocalDirectoryPlugin.GetRepositoryStructureAsync(...)` implementiert (rekursiv bis `MaxDepth`, `.git`-Ausschluss, Reparse-Point-Schutz). GitHub-/BitBucket-Plugin behalten bewusst `NotSupportedException` (Plan entsprechend angepasst, siehe `plan.md` „Offener Punkt 1").

## Rückmeldung des Kunden

- [x] Erweiterung der Anforderung: Der relative Pfad im Repository soll auch nachträglich geändert werden können.
  Erledigt: Neuer Dialog `ArbeitsverzeichnisBearbeitenDialog` + `ArbeitsverzeichnisBearbeitenViewModel`, verdrahtet in `ProjectDetailViewModel`.