# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### EntwicklungsprozessService.cs (EntwicklungsprozessService)

- **Fehlermeldung ohne aussagekräftigen Kontext** — In `UpdateGitignoreAsync` wird die `.gitignore` seit dieser Änderung in `effektivesVerzeichnis` (dem konfigurierten Arbeitsunterverzeichnis) geschrieben, die Log-Meldungen referenzieren jedoch weiterhin `lokalerKlonPfad` (den Repository-Root). Zeile 654: `_logger.LogInformation(".gitignore für '{KlonPfad}' aktualisiert: 'issue.md' eingetragen.", lokalerKlonPfad);` und der Catch-Block Zeile 662 (`"Fehler beim Aktualisieren von .gitignore in '{KlonPfad}'.", lokalerKlonPfad`). Bei konfiguriertem `WorkingDirectoryRelativePath` (z. B. `backend`) meldet das Log den Root-Pfad, obwohl die Datei tatsächlich in `<root>/backend/.gitignore` liegt — die Diagnose zeigt auf das falsche Verzeichnis.

  Empfehlung: In beiden Log-Aufrufen (Zeile 654 und 662) `lokalerKlonPfad` durch `effektivesVerzeichnis` ersetzen, damit die geloggte Pfadangabe mit dem tatsächlich beschriebenen Verzeichnis übereinstimmt.

- **Kopplung/Konsistenz** — Das neue Helper `EnsureEffectiveWorkingDirectory` (Zeile 666–672) löst das Arbeitsverzeichnis über die synchrone `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(...)` auf, **ohne** das Git-Plugin. Die beiden anderen Aufrufer desselben Konzepts — `KiAusfuehrungsService` (CLI-Start, `KiAusfuehrungsService.cs:115` und `:204`) und `GitOrchestrationService` (`GitOrchestrationService.cs:270`) — verwenden dagegen `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(..., gitPlugin, ...)`, die den Root zuvor via `IGitPlugin.ResolveEffectiveRepositoryPathAsync` auflöst (relevant z. B. für `LocalDirectoryPlugin` im `InSourceDirectory`-Modus, wo `lokalerKlonPfad` nur eine Pointer-Datei enthält). Dadurch landen `issue.md`/`.gitignore` bei solchen Plugins in `<pointer>/<workdir>`, während die CLI anschließend in `<resolvedRepo>/<workdir>` läuft — die geschriebene `issue.md` ist im Ausführungsverzeichnis der KI nicht sichtbar. Die Divergenz besteht für `issue.md` bereits vor dieser Änderung (auch zuvor wurde ohne Plugin-Auflösung geschrieben); der neue Code führt das Muster fort, statt den bereits vorhandenen, plugin-fähigen Resolver zu nutzen.

  Empfehlung: `FinalizeStartAsync` das aufgelöste `gitPlugin` durchreichen (steht in `ProzessStartenAsync` bereits zur Verfügung) und in `EnsureEffectiveWorkingDirectory` die plugin-basierte Auflösung verwenden — z. B. den Repository-Root vorab über `IGitPlugin.ResolveEffectiveRepositoryPathAsync` auflösen und erst dann mit dem relativen Pfad kombinieren —, damit `issue.md`/`.gitignore` in genau dem Verzeichnis landen, in dem später CLI-Start und Validierung arbeiten. Alternativ transparent dokumentieren, dass dies für Pointer-Datei-Plugins bewusst nicht unterstützt wird.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
