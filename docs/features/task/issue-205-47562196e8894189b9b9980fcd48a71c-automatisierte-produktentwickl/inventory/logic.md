# Logik-Komponenten

## `AutonomAufgabenInitialisierungsService`
Datei: `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InitialisiereAsync(aufgabe, anfrage, ct)` | public | Orchestriert die vollständige Initialisierung: Plugin-Auflösung, Verzeichnisstruktur, Klon, Branch, JSON-Dateien. **Ruft ab Zeile 45 `ResolveSourceCodeManagementPluginAsync` auf und übergibt das aufgelöste Plugin an Klon- und Branch-Methoden.** |
| `ErstelleArbeitsverzeichnisStrukturAsync(arbeitsverzeichnisPfad, ct)` | public | Erstellt die Verzeichnisstruktur (skills/, clones/, tasks/, logs/). Wird von `InitialisiereAsync` aufgerufen. |
| `KloneHauptRepositoryAsync(gitPlugin, aufgabe, zielPfad, ct)` | private | Klont das Repository unter Verwendung des übergebenen `gitPlugin`-Parameters (Zeile 144). Testet ob Zielverzeichnis bereits existiert (Idempotenz) und wirft aussagekräftige Fehler. |
| `ErstelleProjektbranchAsync(gitPlugin, aufgabe, repoMainPfad, projektBranchName, ct)` | private | Erstellt oder checkt den Projektbranch im geklonten Repository unter Verwendung des übergebenen `gitPlugin`-Parameters (Zeile 190). Unterstützt Remote-Branch-Checkout oder lokale Neuanlage. Idempotent. |
| `LokalerBranchExistiertBereitsAsync(gitPlugin, repoPfad, branchName, ct)` | private | Hilfsmethod zur Überprüfung der lokalen Branch-Existenz (Zeile 236). Wird von `ErstelleProjektbranchAsync` aufgerufen für Idempotenz-Guard. |
| `LadeRemoteBranchesAsync(gitPlugin, repositoryUrl, ct)` | private | Lädt Remote-Branches; gibt leere Liste zurück bei `NotSupportedException` (z. B. für `LocalDirectoryPlugin`). |
| `SicherstelleAufgabeGetrackt(aufgabe)` | private | Stellt sicher, dass die `Aufgabe` im EF Core ChangeTracker getrackt ist. |
| `BuildPermissionsJson(anfrage)` | private | Erzeugt permissions.json mit Berechtigungen und Limits. |
| `BuildStateJson(aufgabe, anfrage)` | private | Erzeugt state.json mit Initialisierungsdaten. |
| `ValidiereAnfrage(anfrage)` | private | Validiert ProjektBranchName, InitialPrompt, TokenBudget, Laufzeit-Limit, Arbeitsverzeichnispath. |

### Abhängigkeiten (injiziert im Konstruktor):
- `SoftwareschmiededDbContext` — Datenbankkontext
- `ICliRunner` — Zum Ausführen von Git-Befehlen
- **`PluginSelectionService`** — Zur Auflösung des Plugins anhand von `aufgabe.GitRepository.PluginTyp`
- `AutonomAufgabenOptions` — Options für Max-Subagenten, Max-Clones, Max-Feature-Branches
- `ILogger<AutonomAufgabenInitialisierungsService>` — Logging

### Abonnierte Events:
Keine

### Publizierte Events:
Keine

---

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveSourceCodeManagementPluginAsync(selectedPluginPrefix, ct)` | public | **Löst das SCM-Plugin auf: explizite Auswahl → gespeicherter Default → Fallback.** Wird von `AutonomAufgabenInitialisierungsService.InitialisiereAsync` aufgerufen mit `aufgabe.GitRepository?.PluginTyp`. Rückgabewert ist `IGitPlugin`. |
| `ResolveDevelopmentAutomationPluginAsync(selectedPluginPrefix, ct)` | public | Löst das KI-Plugin auf (analoges Muster, nicht relevant für diese Anforderung). |
| `ResolveDevelopmentAutomationPluginWithProjectScopeAsync(aufgabenPluginPrefix, projektId, ct)` | public | Löst KI-Plugin mit Projekt-Kontext auf. |
| `ResolveIdePluginAsync(repositoryPath, ct)` | public | Löst IDE-Plugin für ein Repository auf. |
| `GetStoredDefaultPluginPrefixAsync(pluginType, ct)` | public | Liest den gespeicherten Default-PluginPrefix. |
| `SaveDefaultPluginPrefixAsync(pluginType, pluginPrefix, ct)` | public | Speichert den PluginPrefix als Standard. |
| `SaveProjectDefaultPluginPrefixAsync(projektId, pluginType, pluginPrefix, ct)` | public | Speichert Projekt-spezifischen Default-PluginPrefix. |

### Abhängigkeiten (injiziert im Konstruktor):
- `IPluginManager` — Zugriff auf verfügbare Plugins
- `PluginDefaultSettingsService` — Default-Settings
- `PluginActivationService` — Plugin-Aktivierungsstatus
- `ILogger<PluginSelectionService>` — Logging
- `AppEinstellungService?` — Optional, für IDE-Plugin-Order

### Methode `ResolvePluginAsync<TPlugin>` (private):
Implementiert die Plugin-Auflösungs-Logik: 
1. Wenn `selectedPluginPrefix` nicht null → versuche Match
2. Sonst gespeicherten Default laden und versuchen
3. Sonst Fallback via alphabetische Sortierung
4. Sonst `defaultResolver()` aufrufen

---

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

Relevante Methode (Referenzimplementierung für Plugin-Auflösung):

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolvePluginAsync(repository, selectedScmPluginPrefix, aufgabeId, ct)` | private | **Zeigt das Pattern für Plugin-Auflösung, das von `AutonomAufgabenInitialisierungsService` übernommen wurde:** Verwendet `repository.PluginTyp` wenn gesetzt, sonst `selectedScmPluginPrefix`. Ruft `_pluginSelectionService.ResolveSourceCodeManagementPluginAsync(...)` auf (Zeile 467). |

