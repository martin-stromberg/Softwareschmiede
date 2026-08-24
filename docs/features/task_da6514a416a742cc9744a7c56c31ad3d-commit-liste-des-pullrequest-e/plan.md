# Umsetzungsplan

## Ziel
Beim Erstellen eines Pull Requests aus der Aufgabenansicht sollen im Beschreibungstext nur die Commits gelistet werden, die tatsächlich auf dem aktuellen Feature-Branch hinzugekommen sind. Commits aus einem Zwischen-Branch (z. B. `staging`), von dem der Feature-Branch abgezweigt wurde, dürfen nicht in der Liste erscheinen.

## Ursache
Der `GitWorkspaceBrowserService` ermittelt die Commit-Liste mit `git log origin/{targetBranch}..HEAD`. Der Zielbranch (target branch, z. B. `main`) dient dem PR-Ziel, aber nicht als Startpunkt des Feature-Branches. Wenn der Feature-Branch von `staging` oder einem ähnlichen Zwischen-Branch erstellt wurde, sind alle Staging-Commits, die noch nicht in `main` enthalten sind, Teil der Range und erscheinen im PR-Body.

Git kann diese Commits nicht von echten Feature-Commits unterscheiden, weil es den tatsächlichen Start-Branch des lokalen Feature-Branches nicht mehr kennt.

## Lösung
1. Den tatsächlichen Start-Branch (`BasisBranchName`) im `GitArbeitsbereich` der Aufgabe persistieren.
2. `EntwicklungsprozessService.SetupBranchAsync` erweitern, sodass der verwendete Start-Branch beim Checkout ermittelt und an den Aufrufer zurückgegeben wird.
3. `AufgabeService.StartenAsync` erweitern, damit `BasisBranchName` gespeichert wird.
4. `GitOrchestrationService.BuildPullRequestBodyAsync` so anpassen, dass es für die Commit-Liste den `BasisBranchName` (sofern vorhanden) anstelle des Zielbranches an `IGitWorkspaceBrowserService.LoadSnapshotAsync` übergibt.
5. `GitWorkspaceBrowserService.LoadSnapshotAsync` und `ReadBranchCommitsAsync` so erweitern, dass sie einen optionalen Source-Branch akzeptieren, der als Basisreferenz für `git log` dient.
6. Datenbank-Migration erstellen, um die neue Spalte `BasisBranchName` in der Tabelle `Aufgaben` aufzunehmen.
7. Bestehende Tests anpassen bzw. ergänzen.
8. Vollständigen Build und relevante Unit-Tests ausführen.

## Offene Punkte

Keine.
