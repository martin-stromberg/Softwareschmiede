# Test Coverage Gaps

## Scope
- .NET/C# workspace
- Fokus: fehlende Unit- und Component-Tests mit hohem Risiko

## Current coverage snapshot
- Gut abgedeckt: `ProjektService`, `AufgabeService`, `PluginManager`, `ArbeitsverzeichnisResolver`, mehrere Plugin-Abstraktionen und einige Component-Helfer.
- Teilweise abgedeckt: `ProjektDetail`, `AufgabeDetail`, `Einstellungen`.
- Kaum oder nicht direkt abgedeckt: systemnahe Infrastruktur, mehrere Razor-Page-Workflows, Credential-Store, CLI-Runner.

## Gaps

### 1. `PluginSettingsService`
**Risk:** hoch  
**Why:** Schlüsselbildung und CRUD auf Credentials sind zentral für Plugin-Konfiguration.  
**Missing tests:**
- `GetAllPlugins` kombiniert Git- und KI-Plugins korrekt
- `GetValue` verwendet `<PluginPrefix>.<FieldKey>`
- `SetValue` und `DeleteValue` rufen den Credential-Store mit dem erwarteten Schlüssel auf
- `HasValue` behandelt `null`/leer korrekt

### 2. `CliRunner`
**Risk:** hoch  
**Why:** Prozessstart, Argument-Handling, Env-Variablen und Stream-Verhalten sind fehleranfällig.  
**Missing tests:**
- `RunAsync` übergibt Argumente via `ArgumentList`
- stdout/stderr werden parallel gelesen und korrekt zusammengesetzt
- Exit-Codes ungleich 0 werden als Fehler zurückgegeben
- `StreamAsync` liefert Output zeilenweise, trennt stderr als Fehlerzeilen
- Cleanup beendet laufende Prozesse zuverlässig

### 3. `SystemShutdownService`
**Risk:** hoch  
**Why:** OS-spezifische Shutdown-Commands sind sicherheitskritisch.  
**Missing tests:**
- Windows/Linux/macOS erzeugen die richtigen Commands
- nicht unterstützte Plattformen werfen `PlatformNotSupportedException`
- nicht-null Prozessstart und ExitCode≠0 werden korrekt behandelt

### 4. `WindowsCredentialStore`
**Risk:** mittel  
**Why:** Persistenz von Secrets/Credentials ist zentral; Fehler dürfen nicht stillschweigend verschwinden.  
**Missing tests:**
- Get/Set/Delete-Roundtrip
- Verhalten bei fehlenden Einträgen
- Fehlerpfade bei Credential-Manager-Zugriff

### 5. `Home` page
**Risk:** mittel  
**Why:** Dashboard-Zahlen steuern die erste Nutzerwahrnehmung.  
**Missing tests:**
- Laden der Projekte/Aufgaben beim Initialisieren
- Zählung aktiver Projekte, offener Aufgaben und KI-aktiver Aufgaben
- Filterung der aktiven Aufgaben nach Status
- Navigation zur Aufgabe

### 6. `ProjektListe`
**Risk:** mittel  
**Why:** CRUD-Workflow der Projektübersicht ist noch ungetestet.  
**Missing tests:**
- Initiales Laden der Projektliste
- Validierung beim Erstellen
- Speichern lädt die Liste neu
- Abbrechen setzt Formularzustand zurück
- Navigation zur Detailseite

### 7. `ProjektDetail`
**Risk:** hoch  
**Why:** Viele Pfade, mehrere Services und Repository-Verknüpfung.  
**Missing tests:**
- Initiales Laden von Projekt, Aufgaben und archivierten Aufgaben
- Archivieren / Aktualisieren / Löschen inkl. Navigation
- Öffnen/Schließen des Repository-Formulars
- Plugin-Auswahl und Fallback-Logik
- Validierung und Mapping der Repository-Felder

### 8. `NeueAufgabe`
**Risk:** hoch  
**Why:** Kombiniert Projekt, Repository, Issues und Task-Erstellung.  
**Missing tests:**
- Vorbelegung aktiver Repositories
- Laden von Issues asynchron
- Issue-Auswahl füllt Titel/Beschreibung
- Erstellen per manuellem Titel oder aus Issue
- Rücknavigation

### 9. `AufgabeDetail`
**Risk:** hoch  
**Why:** Umfangreicher Bildschirm mit vielen Zustandswechseln.  
**Missing tests:**
- Laden der Detaildaten
- Statuswechsel und Aktionen
- Folgeprompt-/Kommentarpfade
- Navigation und Fehlerbehandlung

### 10. `AgentenpaketeSeite`
**Risk:** mittel bis hoch  
**Why:** Sehr viele Dateisystem- und UI-Operationen, hohe Änderungsrate.  
**Missing tests:**
- Paketbaum laden und flatten
- Expand/Select-Verhalten
- Markdown-Vorschau
- Datei/Paket/Ordner anlegen, umbenennen, löschen
- Upload- und Fehlerpfade

## Recommended priority order
1. `PluginSettingsService`
2. `CliRunner`
3. `SystemShutdownService`
4. `ProjektDetail`
5. `NeueAufgabe`
6. `ProjektListe` / `Home`
7. `WindowsCredentialStore`
8. `AgentenpaketeSeite`
9. `AufgabeDetail`
