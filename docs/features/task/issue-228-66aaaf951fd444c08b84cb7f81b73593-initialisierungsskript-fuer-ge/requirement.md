# Technische Anforderungsbeschreibung: Initialisierungsskript für geklonte Repositories

## Fachliche Zusammenfassung

Das System wird um ein optionales Initialisierungsskript pro Projekt erweitert, das unmittelbar nach dem Klonen eines zugeordneten Repositorys automatisch ausgeführt wird. Das Skript dient der projektspezifischen Initialisierung (z. B. Setzen von `git config`-Werten, Installation von Hooks, Ausführung von Build-Schritten). Fehler bei der Ausführung werden protokolliert, blockieren aber nicht die weitere Bearbeitung der Aufgabe.

## Betroffene Klassen und Komponenten

### Domain-Entitäten
- **`GitRepository`** (Erweiterung)
  - Neue Eigenschaft: `InitialisierungsskriptRelativePfad?: string` — optionaler relativer Pfad zum Initialisierungsskript im Repository (analog zu `RepositoryStartKonfiguration.StartScriptRelativePath`)

### Neue Klassen
- **`RepositoryInitialisierungKonfiguration`** (neue Entity)
  - `Id: Guid` — eindeutige ID der Konfiguration
  - `GitRepositoryId: Guid` — Referenz zum zugehörigen Repository
  - `InitialisierungsskriptRelativePfad: string` — relativer Pfad zum Initialisierungsskript
  - `Aktiv: bool` — Schalter zur Aktivierung/Deaktivierung (Default: `true`)
  - `GitRepository: GitRepository` — Navigationseigenschaft (Backnavigation)

### Services
- **`RepositoryInitialisierungService`** (neuer Service, analog zu `RepositoryStartskriptService`)
  - `RunAsync(repositoryRootPath: string, configuration: RepositoryInitialisierungKonfiguration, ct: CancellationToken): Task`
  - Logik:
    - Prüfung, ob Konfiguration aktiv ist
    - Auflösen des Skriptpfads (Security: Überprüfung, dass das Skript innerhalb des Repository-Roots liegt)
    - Ausführung des Skripts mittels `ICliRunner` (PowerShell-Wrapper, analog zu `RepositoryStartskriptService.BuildArguments`)
    - Fehlerbehandlung: Warnung loggen, aber nicht werfen (keine Exception)

### Modifikationen an bestehenden Services
- **`EntwicklungsprozessService`** — nach dem Klonen und vor der KI-Ausführung:
  - Wenn `repository.InitialisierungKonfiguration` konfiguriert: `RepositoryInitialisierungService.RunAsync()` aufrufen
  - Fehler fangen und als `Warning` loggen (nicht propagieren)
  - Protokolleintrag erstellen (optional): Status-Log mit Erfolg/Fehler

### UI-Komponenten (ViewModels, Views)
- **`ProjectDetailViewModel`** — Erweiterung:
  - Neue Sammlung/Property: `InitialisierungsskriptSuggestionen: IEnumerable<string>` — Liste ausführbarer Dateien aus dem Remote-Repository
  - `SelectedInitialisierungsskript: string?` — vom Benutzer ausgewähltes oder überschriebenes Skript
  - `SaveInitialisierungsskriptAsync(): Task` — Persistierung der Auswahl

- **`ProjectDetailView.xaml`** — neue Steuerelemente:
  - TextBox oder SearchField für `SelectedInitialisierungsskript`
  - AutoComplete/Dropdown-Bindung an `InitialisierungsskriptSuggestionen`
  - Label "Initialisierungsskript:"

### Testing
- `RepositoryInitialisierungServiceTests` — Unit-Tests:
  - Erfolgreiche Ausführung
  - Fehlerbehandlung (Fehler werden geloggt, nicht geworfen)
  - Path-Traversal-Sicherheit (Skript muss in Repository liegen)
  - Deaktivierte Konfiguration (kein Aufruf)
  - Datei nicht gefunden

- `EntwicklungsprozessServiceTests` — Integrationstests:
  - Klon + Initialisierungsskript-Ausführung im Kontext
  - Fehlertoleranz (Aufgabe wird trotz Skript-Fehler nicht blockiert)

- `ProjectDetailViewModelTests` — Erweiterung:
  - `InitialisierungsskriptSuggestionen` werden korrekt ermittelt
  - Manuelle Überschreibung funktioniert

### Datenbank-Migrationen
- Neue Migration: Spalte `InitialisierungsskriptRelativePfad` zu `GitRepositories` hinzufügen oder `RepositoryInitialisierungKonfiguration`-Tabelle anlegen (abhängig von Entity-Modeling-Entscheidung)

## Implementierungsansatz

1. **Domain-Modellierung:**
   - Entscheidung: `InitialisierungsskriptRelativePfad` direkt auf `GitRepository` oder über separate `RepositoryInitialisierungKonfiguration`-Entität (analog zu `RepositoryStartKonfiguration`)?
   - Annahme: Separate Entität für Konsistenz mit `RepositoryStartKonfiguration`

2. **Service-Architektur:**
   - `RepositoryInitialisierungService` folgt dem gleichen Pattern wie `RepositoryStartskriptService` (PowerShell-Ausführung, Path-Validierung)
   - Unterschied: Fehler werden als `Warning` geloggt, nicht als Exception propagiert

3. **Integration in Aufgaben-Lifecycle:**
   - Hook im `EntwicklungsprozessService` nach erfolgreichem Klonen und vor KI-Agenten-Start
   - Event-Punkt: `OnRepositoryClonedAsync()` (if such exists) oder direkt in der `Clone()`-Methode

4. **UI-Integration:**
   - Projekt-Dialog wird um Suchfeld erweitert
   - Vorschlagslogik: Remote-Repository abrufen, ausführbare Dateien identifizieren (Erweiterungen: `.cmd`, `.bat`, `.sh`, `.exe`, `.ps1`)
   - Anforderung: Plugin-Integration für Remote-Repository-Zugriff (z. B. GitHub-API, Bitbucket-API)

## Konfiguration

**Ebene:** Pro Projekt / Pro Git-Repository  
**Speicherung:** EF Core in `RepositoryInitialisierungKonfiguration` oder direkt auf `GitRepository`  
**Gültigkeitsbereich:** Lokal und persistent (Datenbank)

## Abhängigkeiten und Schnittstellen

- `ICliRunner` — für Skript-Ausführung (bereits vorhanden)
- `IPluginManager` — für Remote-Repository-Zugriff (bereits vorhanden)
- `SoftwareschmiededDbContext` — für Persistierung (bereits vorhanden)
- `ILogger<T>` — für Protokollierung (bereits vorhanden)

## Offene Fragen

1. **Entity-Modellierung:** Sollte `InitialisierungsskriptRelativePfad` direkt auf `GitRepository` oder über eine separate `RepositoryInitialisierungKonfiguration`-Entität gespeichert werden (analog zu `RepositoryStartKonfiguration`)?

2. **Vorschlag-Implementierung:** Welche Plugin-APIs stehen zur Verfügung, um Remote-Repository-Dateien aufzulisten? (GitHub REST API, Bitbucket API, etc.)

3. **Fehlerbehandlung-Granularität:** Soll jeder Initialisierungsskript-Fehler als separater Protokolleintrag protokolliert werden, oder reicht eine generische Warnung?

4. **Ausführungskontext:** Sollte das Initialisierungsskript mit den gleichen Environment-Variablen/Arbeitsverzeichnis wie das Startup-Skript ausgeführt werden, oder können/müssen diese abweichen?

5. **Abhängigkeiten zwischen Skripten:** Falls sowohl Initialisierungs- als auch Startskript konfiguriert sind — in welcher Reihenfolge sollten diese ausgeführt werden? (Initialisierung first, dann Startup?)

6. **Datenbankschema:** Soll die neue Konfiguration eine 1:1-Beziehung zu `GitRepository` sein (immer ein Initialisierungsskript pro Repository) oder eine optionale 0..1-Beziehung?
