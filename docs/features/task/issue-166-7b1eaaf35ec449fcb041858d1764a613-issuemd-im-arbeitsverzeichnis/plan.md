# Umsetzungsplan: issue.md im Arbeitsverzeichnis erstellen

## Übersicht

Bisher schreibt der `EntwicklungsprozessService` die `issue.md` (Aufgabenbeschreibung) und den zugehörigen `.gitignore`-Eintrag immer in den Root des geklonten Repositories. Diese Änderung leitet beide Dateien in das konfigurierte Arbeitsverzeichnis (`RepositoryStartKonfiguration.WorkingDirectoryRelativePath`) um, sofern eines gesetzt ist; ist keines konfiguriert (`null`/`"."`), bleibt das bisherige Root-Verhalten unverändert. Betroffen ist ausschließlich die Server-/Service-Schicht (`EntwicklungsprozessService`), keine UI und keine Datenbank.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Pfad-Auflösung für `issue.md`/`.gitignore` | Wiederverwendung des bestehenden `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(repositoryRoot, relativePath)` (synchrone statische Überladung) | Die Klasse kapselt bereits die Kombination von Root + relativem Pfad inklusive Path-Traversal-Prüfung und ist bereits von `KiAusfuehrungsService` und `GitOrchestrationService` gemeinsam genutzt. Kein zweiter Pfad-Berechnungsweg, keine Duplizierung der Sicherheitslogik. |
| Kein Plugin-basiertes Resolving (`IGitPlugin.ResolveEffectiveRepositoryPathAsync`) | `issue.md`/`.gitignore` werden relativ zu `lokalerKlonPfad` aufgelöst, ohne das Git-Plugin einzubeziehen | Antwort auf offene Frage #2: Die Dateien werden im lokalen Klon erzeugt, nicht im Quellverzeichnis; der Klonpfad ist immer `lokalerKlonPfad`. Deshalb wird die synchrone Überladung `ResolveEffectiveWorkingDirectory` (ohne `gitPlugin`) verwendet, nicht `DetermineEffectiveWorkingDirectoryAsync`. |
| Fehlerbehandlung bei fehlendem Arbeitsverzeichnis | Bestehendes Verhalten beibehalten: Warnung loggen, nicht abbrechen; kein Root-Fallback | Antwort auf offene Frage #1 (Fall b): Die Existenz des Arbeitsverzeichnisses wird bereits früh in `ProzessStartenAsync` über `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` geprüft. Ein zusätzlicher Root-Fallback würde nur Komplexität bringen. `Directory.CreateDirectory` bleibt als Sicherheitsnetz erhalten. |
| Parameter-Übergabe | Übergabe von `RepositoryStartKonfiguration?` an die Hilfsmethoden (statt der ganzen `GitRepository`) | Die Hilfsmethoden benötigen nur die Startkonfiguration; ein schmalerer Parameter hält die Signatur fokussiert und folgt dem bestehenden Muster von `ValidateWorkingDirectoryAfterCloneAsync(clonePath, startConfig, …)`. |

## Programmabläufe

### issue.md und .gitignore im Arbeitsverzeichnis ablegen

1. `ProzessStartenAsync` klont das Repository, validiert das Arbeitsverzeichnis über `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` und ruft anschließend `FinalizeStartAsync` mit dem `repository` auf (unverändert).
2. `FinalizeStartAsync` ruft `CreateIssueFileAsync` nun zusätzlich mit `repository.StartKonfiguration` auf.
3. `CreateIssueFileAsync` ermittelt das effektive Zielverzeichnis über `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(lokalerKlonPfad, startKonfiguration?.WorkingDirectoryRelativePath)`. Bei `null`/leer ergibt sich der Repository-Root.
4. `CreateIssueFileAsync` stellt das Zielverzeichnis via `Directory.CreateDirectory` sicher und schreibt `issue.md` in `Path.Combine(effektivesVerzeichnis, "issue.md")`.
5. `FinalizeStartAsync` ruft `UpdateGitignoreAsync` ebenfalls mit `repository.StartKonfiguration` auf.
6. `UpdateGitignoreAsync` ermittelt dasselbe effektive Zielverzeichnis, stellt es sicher und aktualisiert die `.gitignore` in diesem Verzeichnis mit dem Eintrag `issue.md` (Idempotenz-Prüfung wie bisher).

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `WorkingDirectoryResolver`, `RepositoryStartKonfiguration`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `EntwicklungsprozessService` (Service Layer)

- **Geänderte Methoden:**
  - `CreateIssueFileAsync` — Neuer Parameter `RepositoryStartKonfiguration? startKonfiguration`. Statt fest `Path.Combine(lokalerKlonPfad, "issue.md")` wird das effektive Zielverzeichnis über `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory` bestimmt, mit `Directory.CreateDirectory` sichergestellt und `issue.md` dorthin geschrieben. Fehlerbehandlung (Warnung, kein Abbruch) bleibt unverändert.
  - `UpdateGitignoreAsync` — Neuer Parameter `RepositoryStartKonfiguration? startKonfiguration`. `.gitignore` wird analog im effektiven Zielverzeichnis (statt Root) gelesen/geschrieben; Verzeichnis wird via `Directory.CreateDirectory` sichergestellt. Idempotenz-Prüfung und Fehlerbehandlung bleiben unverändert.
  - `FinalizeStartAsync` — Übergibt `repository.StartKonfiguration` an `CreateIssueFileAsync` und `UpdateGitignoreAsync`. Keine weitere Verhaltensänderung.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Regeln. Die Path-Traversal-Sicherheit wird durch die bereits in `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory` enthaltene Prüfung („Pfad verlässt Repository-Verzeichnis") abgedeckt; `WorkingDirectoryRelativePath` wird zusätzlich bereits beim Speichern in der `RepositoryStartKonfiguration` validiert.

## Konfigurationsänderungen

Keine. Das Feature nutzt die bestehende Eigenschaft `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`.

## Seiteneffekte und Risiken

- **Repository-Startskript-Ausführung:** `FinalizeStartAsync` führt vor der Datei-Erstellung optional das Startskript aus. Dessen Verhalten bleibt unberührt; die Reihenfolge (Startskript → issue.md → .gitignore) ändert sich nicht.
- **Rückwärtskompatibilität:** Repositories ohne Arbeitsverzeichnis (`StartKonfiguration == null` oder `WorkingDirectoryRelativePath == null`/`"."`) verhalten sich exakt wie bisher (Datei im Root). Bestandsdaten sind nicht betroffen.
- **`.gitignore`-Wirkungsbereich:** Ein `.gitignore`-Eintrag `issue.md` in einem Unterverzeichnis ignoriert nur die `issue.md` in genau diesem Verzeichnis — das ist das gewünschte, kohärente Verhalten (Datei und ihr Ignore-Eintrag liegen zusammen).
- **Doppelte `.gitignore`:** Falls im Repo-Root bereits eine `.gitignore` existiert und nun zusätzlich eine im Arbeitsverzeichnis angelegt wird, entstehen zwei Dateien. Das ist beabsichtigt und in Git ein normales Muster (verschachtelte `.gitignore`).

## Umsetzungsreihenfolge

1. **`CreateIssueFileAsync` um Arbeitsverzeichnis-Auflösung erweitern**
   - Voraussetzungen: `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory` (bereits vorhanden); `RepositoryStartKonfiguration` (bereits vorhanden).
   - Beschreibung: Parameter `RepositoryStartKonfiguration? startKonfiguration` ergänzen, effektives Verzeichnis auflösen, `Directory.CreateDirectory` aufrufen, `issue.md` in das effektive Verzeichnis schreiben.

2. **`UpdateGitignoreAsync` um Arbeitsverzeichnis-Auflösung erweitern**
   - Voraussetzungen: wie Schritt 1.
   - Beschreibung: Parameter `RepositoryStartKonfiguration? startKonfiguration` ergänzen, effektives Verzeichnis auflösen und sicherstellen, `.gitignore` in diesem Verzeichnis lesen/schreiben.

3. **`FinalizeStartAsync` anpassen**
   - Voraussetzungen: Schritte 1 und 2 (neue Signaturen).
   - Beschreibung: Aufrufe von `CreateIssueFileAsync` und `UpdateGitignoreAsync` um `repository.StartKonfiguration` ergänzen.

4. **Tests ergänzen**
   - Voraussetzungen: Schritte 1–3 (geändertes Verhalten); vorhandene Test-Helper `SetupCloneWithDirectoryCreation`, `DeleteDirectoryIfExists`.
   - Beschreibung: Neue Testmethoden für Datei-Platzierung mit und ohne Arbeitsverzeichnis schreiben (siehe Abschnitt Tests).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ProzessStartenAsync_ShouldWriteIssueFileIntoWorkingDirectory_WhenWorkingDirectoryConfigured` | `EntwicklungsprozessServiceTests` | Bei gesetztem `WorkingDirectoryRelativePath` (z. B. `"backend"`) liegt die `issue.md` in `<Klon>/backend/issue.md` und nicht im Root. |
| `ProzessStartenAsync_ShouldWriteIssueFileIntoRepositoryRoot_WhenNoWorkingDirectoryConfigured` | `EntwicklungsprozessServiceTests` | Ohne Arbeitsverzeichnis liegt die `issue.md` im Repository-Root (`<Klon>/issue.md`) — Rückwärtskompatibilität. |
| `ProzessStartenAsync_ShouldWriteGitignoreEntryIntoWorkingDirectory_WhenWorkingDirectoryConfigured` | `EntwicklungsprozessServiceTests` | Bei gesetztem Arbeitsverzeichnis wird die `.gitignore` mit dem Eintrag `issue.md` in `<Klon>/backend/.gitignore` erstellt/aktualisiert. |
| `ProzessStartenAsync_ShouldWriteGitignoreEntryIntoRepositoryRoot_WhenNoWorkingDirectoryConfigured` | `EntwicklungsprozessServiceTests` | Ohne Arbeitsverzeichnis wird der `.gitignore`-Eintrag im Root gepflegt — Rückwärtskompatibilität. |

Hinweis: Die genannten Tests lassen sich zu weniger Methoden zusammenfassen (z. B. je ein Test „mit Arbeitsverzeichnis" prüft sowohl `issue.md`- als auch `.gitignore`-Platzierung), sofern die Coverage (mit/ohne Arbeitsverzeichnis, beide Dateien) vollständig erhalten bleibt. Vorhandene Helper (`SetupCloneWithDirectoryCreation`, das das Arbeitsunterverzeichnis mit anlegt, sowie `DeleteDirectoryIfExists`) werden wiederverwendet; ein neuer Helper ist nicht zwingend erforderlich.

### Betroffene bestehende Tests

Keine. `CreateIssueFileAsync` und `UpdateGitignoreAsync` sind `private`, werden von keinem Test direkt aufgerufen; die Signaturänderung bricht keine Kompilierung. Die bestehenden Integrations-Tests (`ProzessStartenAsync_*`, `ProzessStartenUndCliStartenAsync_*`) laufen ohne Arbeitsverzeichnis bzw. legen das Unterverzeichnis bereits an und bleiben grün.

### E2E-Tests (Pflicht)

Keine. Die Platzierung von `issue.md`/`.gitignore` erzeugt keine neue oder geänderte UI-Benutzerinteraktion, die über FlaUI beobachtbar wäre. Der Happy Path (Datei landet im Arbeitsverzeichnis) wird durch die oben genannten dateisystem-basierten Service-/Integrationstests abgedeckt, die reale Dateien in temporären Verzeichnissen schreiben und deren Ablageort verifizieren.

Betroffene bestehende E2E-Tests: Keine.

## Offene Punkte

Keine.
