# Logik-Services

## `WorkingDirectoryResolver`

Datei: `src/Softwareschmiede/Application/Services/WorkingDirectoryResolver.cs`

**Hinweis:** Alle Methoden sind statisch; keine Instanziierung erforderlich.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DetermineEffectiveWorkingDirectoryAsync(repositoryRoot, startConfig, gitPlugin, ct)` | public static async | **Zentrale Methode:** Ermittelt das effektive Arbeitsverzeichnis aus Repository-Root und optionaler `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`. Unterstützt auch `IGitPlugin` zur Auflösung des tatsächlichen Repository-Pfads (z. B. bei `LocalDirectoryPlugin.InSourceDirectory`-Modus). Führt Path-Traversal-Prüfung und Existenz-Validierung durch. |
| `ResolveEffectiveWorkingDirectory(repositoryRoot, relativePath)` | public static | Kombiniert Repository-Root-Verzeichnis mit relativem Arbeitsverzeichnis-Pfad und normalisiert das Ergebnis. Verhindert Path-Traversal-Angriffe und Escaping in Sibling-Verzeichnisse. |
| `ValidateWorkingDirectory(effectiveWorkingDirectory)` | public static | Prüft, ob das effektive Arbeitsverzeichnis im Dateisystem existiert; wirft `DirectoryNotFoundException` wenn nicht vorhanden. |

---

## `KiAusfuehrungsService`

Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCliAsync(aufgabeId, kiPlugin, localRepositoryPath, startConfig, gitPlugin, ct)` | public async | **Kritisch für Anforderung:** Startet CLI-Prozess für Aufgabe. Ruft intern `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` zur Auflösung des effektiven Arbeitsverzeichnisses auf. Das resultierende Arbeitsverzeichnis wird dem Plugin (`kiPlugin.StartCliAsync(effectiveWorkdir, ...)`) übergeben. |
| `StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, localRepositoryPath, startConfig, gitPlugin, ct)` | public async | Startet CLI-Prozess über Windows Pseudo Console API (für Echtzeit-Ausgabe-Streaming). Nutzt ebenfalls `WorkingDirectoryResolver` zur Auflösung des effektiven Arbeitsverzeichnisses. |
| `GetPseudoConsoleSession(aufgabeId)` | public | Gibt die `PseudoConsoleSession` für eine Aufgabe zurück, oder `null` wenn keine aktive Session vorhanden ist. |
| `StopCliAsync(aufgabeId)` | public async | Stoppt den laufenden CLI-Prozess für eine Aufgabe. |
| `GetLastExitCode(aufgabeId)` | public | Gibt den Exit-Code des letzten abgeschlossenen Prozesses zurück. |
| `UpdateHeartbeat(aufgabeId)` | public | Aktualisiert `LastHeartbeatUtc` der Aufgabe (für Timeout-Tracking). |

**Publizierte Events:**
- `CliProcessStatusChanged` (Action<Guid, CliProcessStatus>): Wird ausgelöst, wenn CLI-Prozess startet, stoppt oder ein Fehler auftritt (Status: Running, Stopped, Failed). Der Event-Receiver kann daran UI-Zustandsänderungen (z. B. Play-Button deaktivieren) koppeln.
- `RunningCountChanged` (Action<int, int>): Wird ausgelöst, wenn sich die Anzahl gleichzeitig laufender CLI-Prozesse ändert. Parameter: (altCount, neuerCount).

**Technische Besonderheiten:**
- Akzeptiert optionale `RepositoryStartKonfiguration` mit optionalem `WorkingDirectoryRelativePath`
- Unterstützt `IGitPlugin` (z. B. `GitPlugin`, `LocalDirectoryPlugin`) zur Auflösung des tatsächlichen Repository-Pfads bei speziellen Modi
- Der Service selbst speichert keine Arbeitsverzeichnis-Auflösung; diese wird bei jedem `StartCliAsync`-Aufruf neu durchgeführt

---

## `ArbeitsverzeichnisOeffnenService`

Datei: `src/Softwareschmiede/Application/Services/ArbeitsverzeichnisOeffnenService.cs`

**Konstruktor-Abhängigkeiten:**
- `IProzessStarter prozessStarter`: Startet die plattformabhängigen Öffnen-Befehle (z. B. `explorer.exe` auf Windows)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Oeffne(string arbeitsverzeichnis)` | public | **Relevant für Anforderung:** Öffnet das übergebene Arbeitsverzeichnis im Standard-Dateiexplorer des Betriebssystems. **Windows:** `explorer.exe` mit gequottem Pfad. **Andere Plattformen:** wirft `PlatformNotSupportedException`. Validiert: leere/Whitespace-Verzeichnisse werfen `ArgumentException`. |

**Hinweis:** Dieser Service führt KEINE Arbeitsverzeichnisauflösung durch; der Caller (z. B. `TaskDetailViewModel`) muss `WorkingDirectoryResolver` nutzen und das aufgelöste Verzeichnis übergeben.

---

## `IdeOeffnenService`

Datei: `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs`

**Konstruktor-Abhängigkeiten:**
- `IProzessStarter prozessStarter`: Startet Öffnen-Befehle (Shell-Execute für `.sln`, Prozessstart für VSCode)
- `IVisualStudioCodeLocator visualStudioCodeLocator`: Ermittelt den VSCode-Befehl (z. B. `code` oder `C:\Users\...\AppData\Local\Programs\Microsoft VS Code\Code.exe`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `FindeSolutions(string arbeitsverzeichnis)` | public | Sucht nach `*.sln` und `*.slnx` Dateien auf der obersten Ebene des Arbeitsverzeichnisses, alphabetisch sortiert. Gibt leere Liste bei `null`/leerem Pfad oder nicht existierendem Verzeichnis zurück. |
| `OeffneSolution(string solutionPath)` | public | Öffnet die übergebene Solution-Datei mit dem registrierten Standardhandler (Shell-Execute). Validiert: leerer Pfad wirft `ArgumentException`. |
| `IstVisualStudioCodeVerfuegbar` | public | Gibt `true` zurück, wenn Visual Studio Code über `IVisualStudioCodeLocator` aufgelöst werden kann. |
| `OeffneVisualStudioCode(string arbeitsverzeichnis)` | public | **Relevant für Anforderung:** Öffnet das Arbeitsverzeichnis in Visual Studio Code. Der VSCode-Prozess erhält das Verzeichnis als erstes Argument. Validiert: fehlendes Verzeichnis wirft `DirectoryNotFoundException`, VSCode nicht verfügbar wirft `InvalidOperationException`. |

**Hinweis:** Dieser Service führt ebenfalls KEINE Arbeitsverzeichnisauflösung durch; der Caller muss `WorkingDirectoryResolver` nutzen und das aufgelöste Verzeichnis übergeben.

---

## Zusammenfassung der Arbeitsverzeichnis-Flüsse

### 1. CLI-Start mit Arbeitsverzeichnis (bereits funktional)
```
TaskDetailViewModel.StartenCommand
  → KiAusfuehrungsService.StartCliAsync(aufgabeId, kiPlugin, localRepoPath, startConfig, gitPlugin)
    → WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(localRepoPath, startConfig, gitPlugin)
      → (optional) GitPlugin.ResolveEffectiveRepositoryPathAsync() [zur Auflösung des echten Repo-Pfads]
      → ResolveEffectiveWorkingDirectory() [Kombination Root + relativ]
      → ValidateWorkingDirectory() [Existenz-Prüfung]
    → kiPlugin.StartCliAsync(effectiveWorkingDirectory, ...)
```

### 2. Arbeitsverzeichnis öffnen (teilweise funktional, aber ohne Auflösung)
```
TaskDetailViewModel.OeffneArbeitsverzeichnisCommand
  → ArbeitsverzeichnisOeffnenService.Oeffne(Aufgabe.LokalerKlonPfad)  ← PROBLEM: keine Auflösung!
    → explorer.exe LokalerKlonPfad
```

**ISSUE:** Der Caller (`TaskDetailViewModel`) übergibt nur `LokalerKlonPfad` (Repository-Root), nicht das aufgelöste Arbeitsverzeichnis basierend auf `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`.

### 3. IDE öffnen (teilweise funktional, aber ohne Auflösung)
```
TaskDetailViewModel.OeffneIdeCommand
  → IdeOeffnenService.FindeSolutions(Aufgabe.LokalerKlonPfad)  ← PROBLEM: keine Auflösung!
    → sucht .sln/.slnx im Repository-Root
  → OeffneSolution() oder OeffneVisualStudioCode(Aufgabe.LokalerKlonPfad)  ← PROBLEM: keine Auflösung!
```

**ISSUE:** Analog zu Punkt 2 — der Caller übergibt nur `LokalerKlonPfad` ohne Auflösung des konfigurierten Arbeitsverzeichnisses. Solutions werden im Repository-Root gesucht, nicht im konfigurierten Arbeitsverzeichnis.
