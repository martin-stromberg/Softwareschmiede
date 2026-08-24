# Umsetzungsplan: Plugin-Resolution für autonome Aufgaben beim Repository-Klon

## Übersicht

Der `AutonomAufgabenInitialisierungsService` wird angepasst, um das korrekte SCM-Plugin anhand der aufgabenspezifischen Konfiguration (`aufgabe.GitRepository.PluginTyp`) aufzulösen, statt blind das global konfigurierte Default-Plugin zu verwenden. Dies behebt den Fehlerfall, bei dem das Klonen des Repositories fehlschlägt, wenn das Default-Plugin nicht dem am `GitRepository.PluginTyp` konfigurierten Plugin entspricht. Die Lösung folgt dem etablierten Muster aus `EntwicklungsprozessService.ResolvePluginAsync()`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Plugin-Dependency-Injection | `PluginSelectionService` als Konstruktor-Abhängigkeit statt direkter `IGitPlugin`-Injektion | Ermöglicht dynamische Plugin-Auflösung pro Aufgabe anhand von `aufgabe.GitRepository.PluginTyp`. Der Resolving-Service ist das etablierte Pattern im Projekt (siehe `EntwicklungsprozessService`). Erlaubt später leichte Erweiterung um projekt- oder aufgabenspezifische Plugin-Defaults. |
| Plugin-Auflösungs-Zeitpunkt | Plugin-Auflösung erfolgt einmalig in `InitialisiereAsync()` vor den Klon- und Branch-Operationen | Die Aufgabe ist im Speicher verfügbar und alle erforderlichen Daten (`GitRepository.PluginTyp`) stehen zur Verfügung. Das aufgelöste Plugin ist dann deterministisch für alle nachfolgenden Operationen derselben Initialisierung. |
| Plugin-Übergabe | Das aufgelöste Plugin wird als `IGitPlugin gitPlugin`-Parameter an `KloneHauptRepositoryAsync()` und `ErstelleProjektbranchAsync()` übergeben | Macht die Abhängigkeit explizit (nicht versteckt in Private Fields), erlaubt Testability (Mock-Injection), und stellt sicher, dass beide Methoden dasselbe Plugin verwenden. |

## Programmabläufe

### Initialisierung einer autonomen Aufgabe mit Plugin-Auflösung

1. **Anwender ruft `AutonomAufgabenInitialisierungsService.InitialisiereAsync(aufgabe, anfrage, ct)` auf** mit einer `Aufgabe`, die ein verknüpftes `GitRepository` mit konfig uriertem `PluginTyp` hat
2. **Validierung der Anfrage** (ProjektBranchName, InitialPrompt, Laufzeit-Limits) via `ValidiereAnfrage(anfrage)`
3. **Plugin-Auflösung:** `_pluginSelectionService.ResolveSourceCodeManagementPluginAsync(aufgabe.GitRepository?.PluginTyp, ct)` wird aufgerufen
   - Wenn `aufgabe.GitRepository?.PluginTyp` nicht null ist, wird versucht, das Plugin mit diesem Präfix zu laden
   - Falls keine explizite Auswahl vorhanden, wird der gespeicherte Default herangezogen
   - Falls auch kein Default vorhanden, wird ein Fallback-Plugin (alphabetisch erste aktive Implementierung) verwendet
   - Rückgabe ist eine vollständig initialisierte `IGitPlugin`-Instanz
4. **Verzeichnisstruktur anlegen** via `ErstelleArbeitsverzeichnisStrukturAsync(...)` (plan.md, progress.md, governance.md, Subdirectories)
5. **Repository klonen** via `KloneHauptRepositoryAsync(gitPlugin, aufgabe, repoMainPfad, ct)`
   - Das aufgelöste `gitPlugin` wird verwendet (nicht das injizierte Default-Plugin)
   - `gitPlugin.CloneRepositoryAsync(aufgabe.GitRepository.RepositoryUrl, repoMainPfad, ct)` wird aufgerufen
   - Idempotenz-Guard: Falls Zielverzeichnis bereits existiert und nicht leer ist, wird Kloning übersprungen (ermöglicht sicheres Retry nach fehlgeschlagenem nächsten Schritt)
6. **Projektbranch erstellen/auschecken** via `ErstelleProjektbranchAsync(gitPlugin, aufgabe, repoMainPfad, projektBranchName, ct)`
   - Das aufgelöste `gitPlugin` wird verwendet
   - Remote-Branches werden via `gitPlugin.GetRemoteBranchesAsync(repositoryUrl, ct)` geladen (mit Fallback auf leere Liste bei nicht unterstützten Plugins wie `LocalDirectoryPlugin`)
   - Falls Branch remote existiert: `gitPlugin.CheckoutRemoteBranchAsync(repoMainPfad, branchName, ct)` wird aufgerufen
   - Falls nicht: `gitPlugin.CreateBranchAsync(repoMainPfad, branchName, sourceBranchName: null, ct)` wird aufgerufen (legt lokal an und checkt direkt aus via "git checkout -b")
   - Idempotenz-Guard: Falls lokaler Branch bereits existiert, wird Neuanlage übersprungen
7. **JSON-Dateien generieren**
   - `permissions.json` wird via `BuildPermissionsJson(anfrage)` erzeugt (erlaubte Aktionen, Limits aus anfrage)
   - `state.json` wird via `BuildStateJson(aufgabe, anfrage)` erzeugt (Task-ID, Runtime-Infos, Governance, Clones, Subagents)
8. **Konfiguration persistieren**
   - Neue `AutonomAufgabeKonfiguration`-Entität wird angelegt mit `ProjektBranchName`, `InitialPrompt`, `ArbeitsverzeichnisPfad`, `RessourcenLimits`, etc.
   - `Aufgabe` wird (falls noch nicht getrackt) via `SicherstelleAufgabeGetrackt(aufgabe)` in den EF Core ChangeTracker eingefügt
   - Alle Änderungen werden via `_db.SaveChangesAsync(ct)` persistiert
9. **Rückgabe der erstellten Konfiguration**

Beteiligte Klassen/Komponenten: `AutonomAufgabenInitialisierungsService`, `PluginSelectionService`, `IGitPlugin`, `ICliRunner`, `SoftwareschmiededDbContext`, `Aufgabe`, `GitRepository`, `AutonomAufgabeKonfiguration`

## Neue Klassen

Keine. Alle erforderlichen Klassen und Interfaces existieren bereits:
- `AutonomAufgabenInitialisierungsService` — bereits vorhanden, Methoden-Signaturen angepasst
- `PluginSelectionService` — bereits vorhanden, wird neu injiziert
- `IGitPlugin` — bereits vorhanden, wird parametrisiert übergeben
- Datenmodelle (`Aufgabe`, `GitRepository`, `AutonomAufgabeKonfiguration`) — bereits vorhanden mit erforderlichen Eigenschaften

## Änderungen an bestehenden Klassen

### `AutonomAufgabenInitialisierungsService` (Service)

- **Geänderte Abhängigkeiten im Konstruktor:**
  - `IGitPlugin` wird NICHT injiziert (vorher implizit vorhanden als Private Field, jetzt entfernt)
  - `PluginSelectionService` wird stattdessen injiziert und als `_pluginSelectionService` gepuffert
  - Ermöglicht dynamische Plugin-Auflösung pro `InitialisiereAsync()`-Aufruf

- **Geänderte Methode: `InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct)`**
  - Zeile 45: Neue Zeile zur Plugin-Auflösung:
    ```
    var gitPlugin = await _pluginSelectionService.ResolveSourceCodeManagementPluginAsync(aufgabe.GitRepository?.PluginTyp, ct);
    ```
  - Zeile 50-51: Übergibt `gitPlugin` als ersten Parameter an `KloneHauptRepositoryAsync()` und `ErstelleProjektbranchAsync()`

- **Geänderte Methode: `KloneHauptRepositoryAsync(IGitPlugin gitPlugin, Aufgabe aufgabe, string zielPfad, CancellationToken ct)`**
  - Neue Signatur mit `IGitPlugin gitPlugin` als erstem Parameter (bisher implizit via injiziertes Default-Plugin)
  - Zeile 166: Nutzt das übergebene `gitPlugin` statt `_gitPlugin` (welches jetzt nicht mehr existiert)
  - Zeile 166: `await gitPlugin.CloneRepositoryAsync(repositoryUrl, zielPfad, ct);` — verwendet das aufgelöste Plugin

- **Geänderte Methode: `ErstelleProjektbranchAsync(IGitPlugin gitPlugin, Aufgabe aufgabe, string repoMainPfad, string projektBranchName, CancellationToken ct)`**
  - Neue Signatur mit `IGitPlugin gitPlugin` als erstem Parameter
  - Zeile 197, 214, 257: Nutzen das übergebene `gitPlugin` statt eines injiziert gepufferten Default-Plugins
  - Zeile 194: `LadeRemoteBranchesAsync()` wird mit `gitPlugin` aufgerufen (wird nachfolgend ausgeführt)

- **Keine Änderung:** `ErstelleArbeitsverzeichnisStrukturAsync()`, `BuildPermissionsJson()`, `BuildStateJson()`, `ValidiereAnfrage()`, `SicherstelleAufgabeGetrackt()` — diese Methoden sind unabhängig von der Plugin-Auflösung und bleiben ungeändert

## Datenbankmigrationen

Keine. Die erforderliche Struktur existiert bereits:
- `Aufgabe.GitRepositoryId` (Foreign Key zu `GitRepository`) — bereits vorhanden
- `GitRepository.PluginTyp` (String-Property) — bereits vorhanden
- `AutonomAufgabeKonfiguration` mit Verweis auf `Aufgabe.Id` — bereits vorhanden

## Validierungsregeln

Keine neuen Validierungsregeln. Bestehende Validierung bleibt:
- `ProjektBranchName` muss gültiger Git-Branch-Name sein (validiert via `GitBranchNameValidator.IstGueltig()`)
- `InitialPrompt` muss min. 10 Zeichen enthalten
- `TokenBudget` muss 1–5.000.000 liegen
- `LaufzeitLimitMinuten` muss 60–1440 liegen
- `ArbeitsverzeichnisPfad` muss absolut sein

Die Plugin-Auflösung selbst wird delegiert an `PluginSelectionService.ResolveSourceCodeManagementPluginAsync()`, das interne Fehlerbehandlung implementiert (z. B. wenn kein Plugin gefunden wird).

## Konfigurationsänderungen

Keine. Die Plugin-Auflösung wird vollständig durch die Aufgabendaten gesteuert (`aufgabe.GitRepository.PluginTyp`), die vom Anwender beim Repository-Setup konfiguriert werden. Keine neuen Konfigurationsschlüssel oder Umgebungsvariablen nötig.

## Seiteneffekte und Risiken

Keine bekannten Seiteneffekte. Die Änderung ist lokal isoliert auf den `AutonomAufgabenInitialisierungsService` und bricht keine bestehenden Schnittstellen:
- Öffentliche Signatur von `InitialisiereAsync()` ändert sich nicht (Plugin-Auflösung ist intern)
- Private Hilfsmethoden `KloneHauptRepositoryAsync()` und `ErstelleProjektbranchAsync()` werden nur von `InitialisiereAsync()` aufgerufen, sind also intern
- Tests mit direkten Aufrufen dieser private Hilfsmethoden müssen angepasst werden (siehe Tests-Abschnitt)

## Umsetzungsreihenfolge

**Hinweis:** Diese Sektion dokumentiert die bereits durchgeführte Umsetzung. Die folgenden Schritte waren erforderlich:

1. **Dependency Injection in `AutonomAufgabenInitialisierungsService` anpassen**
   - Voraussetzungen: `PluginSelectionService` existiert bereits, `IGitPlugin` ist bereits injiziert
   - Beschreibung: `IGitPlugin` aus den Konstruktor-Abhängigkeiten entfernen; `PluginSelectionService` injizieren und als `_pluginSelectionService` gepuffert

2. **Plugin-Auflösung in `InitialisiereAsync()` implementieren**
   - Voraussetzungen: `PluginSelectionService` ist injiziert, `Aufgabe.GitRepository.PluginTyp` existiert als Eigenschaft
   - Beschreibung: Vor dem Klon-Schritt `_pluginSelectionService.ResolveSourceCodeManagementPluginAsync(aufgabe.GitRepository?.PluginTyp, ct)` aufrufen und Ergebnis in `var gitPlugin` speichern

3. **Methoden-Signaturen für `KloneHauptRepositoryAsync()` und `ErstelleProjektbranchAsync()` anpassen**
   - Voraussetzungen: Beide Methoden existieren, sind private, werden nur von `InitialisiereAsync()` aufgerufen
   - Beschreibung: `IGitPlugin gitPlugin`-Parameter als ersten Parameter hinzufügen; alle Aufrufe von `_gitPlugin` durch den Parameter `gitPlugin` ersetzen

4. **Unit-Tests anpassen**
   - Voraussetzungen: Test-Factory existiert, Tests für die beiden Hilfsmethoden existieren
   - Beschreibung: Tests, die `KloneHauptRepositoryAsync()` oder `ErstelleProjektbranchAsync()` direkt aufrufen (nicht über `InitialisiereAsync()`), müssen das aufgelöste Plugin als Parameter übergeben

5. **Regressionstest implementieren**
   - Voraussetzungen: Test-Factory, Mock-Infrastruktur für `IGitPlugin` und `PluginSelectionService`
   - Beschreibung: Test `InitialisiereAsync_VerwendetPluginAusGitRepositoryPluginTyp_NichtDasDefaultPlugin` implementieren, der validiert, dass das richtige Plugin (nicht das Global-Default) verwendet wird

6. **E2E-Tests durchführen**
   - Voraussetzungen: Alle Komponenten zusammengebaut, Datenbank verfügbar
   - Beschreibung: E2E-Tests ausführen (siehe E2E-Tests-Abschnitt); sicherstellen, dass Initialisierung mit aufgabenspezifischem Plugin korrekt funktioniert

## Tests

### Neue Tests

Keine völlig neuen Testklassen, aber ein neuer kritischer Regressionstest:

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `InitialisiereAsync_VerwendetPluginAusGitRepositoryPluginTyp_NichtDasDefaultPlugin` | `AutonomAufgabenInitialisierungsServiceTests` | **Zentral für die Anforderung:** Validiert, dass das Plugin korrekt anhand von `aufgabe.GitRepository.PluginTyp` aufgelöst wird und verwendet wird — nicht das global konfigurierte Default-Plugin. Test erstellt zwei unterschiedliche Mock-Plugins, setzt das eine als Default, das andere als Aufgaben-Plugin, und verbietet Aufrufe auf dem Default via `.Verify(..., Times.Never)`. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AutonomAufgabenInitialisierungsServiceTests` — alle Tests mit direktem Aufruf von `KloneHauptRepositoryAsync()` oder `ErstelleProjektbranchAsync()` | Diese private Hilfsmethoden haben neue Signaturen (`IGitPlugin gitPlugin`-Parameter). Tests, die sie direkt aufrufen, müssen ein aufgelöstes Mock-Plugin übergeben. |
| `AutonomAufgabenInitialisierungsServiceTestFactory` — `CreateService()` und verwandte Test-Setup-Methoden | Factory muss `PluginSelectionService` mit korrekten Mock-Plugins aufbauen und injizieren, anstelle eines einzelnen injiziert en Default-Plugins. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Initialisierung autonomer Aufgabe mit nicht-Default-Plugin | `E2E_AutonomAufgabenInitialisierung` | Aufgabe mit `GitRepository.PluginTyp ≠ Global-Default` wird korrekt initialisiert; Repository wird mit dem aufgabenspezifischen Plugin geklont; `state.json` und `permissions.json` werden mit korrektem Arbeitsverzeichnis angelegt; Projektbranch existiert nach Abschluss. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_AutonomAufgabenInitialisierung` — alle Szenarien | Falls Tests explizit mit Default-Plugin arbeiten und kein explizites `GitRepository.PluginTyp` setzen: optional neu schreiben, um auch nicht-Default-Plugins zu testen. Keine Änderung erforderlich, falls Tests bereits mit Aufgaben arbeiten, die ein Plugin-Setup haben. |

## Offene Punkte

Keine. Die Implementierung wurde vollständig durchgeführt und durch Unit-Tests und E2E-Tests validiert. Alle in der Anforderung formulierten Punkte sind gelöst:

1. ✅ Plugin-Auflösung anhand von `aufgabe.GitRepository.PluginTyp` — implementiert
2. ✅ Fehlerbehandlung bei fehlender Repository-Konfiguration — implementiert (wirft `InvalidOperationException`)
3. ✅ Rollout für bestehende autonome Aufgaben — obsolet (neue Aufgaben werden mit dem neuen Muster initialisiert; bestehende Aufgaben sind bereits mit ihrem ursprünglichen Plugin initialisiert)
4. ✅ Testabdeckung mit nicht-Default-Plugins — implementiert (Regressionstest validiert Plugin-Auflösung)
