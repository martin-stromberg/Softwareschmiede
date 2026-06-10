# Services & Logik

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Service für Aufgabenverwaltung (CRUD + Lebenszyklus-Operationen).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetByProjektAsync(projektId, ct)` | public | Gibt alle aktiven Aufgaben eines Projekts zurück |
| `GetArchiviertByProjektAsync(projektId, ct)` | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetByIdAsync(id, ct)` | public | Gibt Aufgabe per ID zurück (ohne Details) |
| `GetDetailAsync(id, ct)` | public | Gibt Aufgabe mit Projekt, Issue, Repo-Konfiguration und Protokolleinträgen zurück |
| `GetLatestDiffResultIdAsync(aufgabeId, ct)` | public | Gibt ID des letzten Diff-Ergebnisses zurück |
| `GetLatestDiffResultIdForFileAsync(aufgabeId, relativePath, ct)` | public | Gibt ID des letzten Diff-Ergebnisses für spezifische Datei zurück |
| `CreateAsync(projektId, titel, beschreibung, gitRepositoryId, ct)` | public | Erstellt neue Aufgabe mit Status `Offen` |
| `CreateFromIssueAsync(projektId, issue, gitRepositoryId, ct)` | public | Erstellt Aufgabe aus GitHub-Issue (inklusive `IssueReferenz`) |
| `UpdateAsync(id, titel, beschreibung, paketName, agentenName, kiPrefix, ct)` | public | Aktualisiert Titel, Beschreibung und Agent-Infos |
| `DeleteAsync(id, ct)` | public | Löscht Aufgabe |
| `VerwerfenAsync(id, aktion, ct)` | public | Verwirft offene Aufgabe (Archivieren oder Löschen) |
| `ArchivierenAsync(id, ct)` | public | Archiviert abgeschlossene oder fehlgeschlagene Aufgabe |
| `StartenAsync(id, branchName, lokalerKlonPfad, ct)` | public | Startet Aufgabe: Status → `InBearbeitung`, setzt Branch und Klonpfad |
| `KiAktiviertAsync(id, ct)` | public | Setzt Status auf `KiAktiv` |
| `SavePromptVorschlagAsync(id, prompt, ausfuehrenAbUtc, ct)` | public | Speichert Vorschlagsprompt und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync(id, ct)` | public | Löscht gespeicherten Vorschlagsprompt |
| `KiAbgeschlossenAsync(id, ct)` | public | Setzt Status zurück auf `InBearbeitung` nach KI-Abschluss |
| `AbschliessenAsync(id, ct)` | public | Schließt Aufgabe ab: Status → `Abgeschlossen`, setzt Abschlussdatum, leert Branch und Klonpfad |
| `AbbrechenAsync(id, ct)` | public | Bricht Aufgabe ab: Status → `Offen`, leert Branch und Klonpfad |
| `FehlgeschlagenAsync(id, ct)` | public | Setzt Status auf `Fehlgeschlagen` |
| `StatusSetzenAsync(id, status, ct)` | public | Setzt Status generisch |

## `ProjektService`
Datei: `src/Softwareschmiede/Application/Services/ProjektService.cs`

Service für Projektverwaltung (CRUD + Archivieren).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetAllAsync(ct)` | public | Gibt alle Projekte zurück (sortiert nach Name) |
| `GetByIdAsync(id, ct)` | public | Gibt Projekt per ID zurück |
| `GetDetailAsync(id, ct)` | public | Gibt Projekt mit Repositories und Aufgaben zurück |
| `CreateAsync(name, beschreibung, ct)` | public | Erstellt neues Projekt mit Status `Aktiv` |
| `UpdateAsync(id, name, beschreibung, ct)` | public | Aktualisiert Name und Beschreibung |
| `ArchivierenAsync(id, ct)` | public | Archiviert Projekt |

## `ProtokollService`
Datei: `src/Softwareschmiede/Application/Services/ProtokollService.cs`

Service zum Speichern und Abrufen von Protokolleinträgen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetByAufgabeAsync(aufgabeId, ct)` | public | Gibt alle Protokolleinträge einer Aufgabe chronologisch sortiert zurück (inkl. TestErgebnisse) |
| `AddEintragAsync(aufgabeId, typ, inhalt, agentName, ct)` | public | Fügt neuen Protokolleintrag hinzu |
| `AddTestErgebnisseAsync(aufgabeId, testResult, ct)` | public | Erstellt Protokolleintrag vom Typ `TestErgebnis` mit Detail-Einträgen |
| `AddStatusUebergangAsync(aufgabeId, vonStatus, nachStatus, ct)` | public | Erstellt Protokolleintrag für Statusübergang |
| `SuchenAsync(aufgabeId, suchbegriff, ct)` | public | Sucht in Inhalt und AgentName der Protokolleinträge |

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

Singleton-Service, der laufende KI-Ausführungen verwaltet. Ermöglicht Hintergrundausführung unabhängig von Blazor-Komponenten-Lebensdauer.

| Methode / Eigenschaft | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `RunningCountChanged` { event } | public | Event: Wird ausgelöst, wenn sich Anzahl laufender Automatisierungen ändert |
| `IsRunning(aufgabeId)` | public | Gibt an, ob für Aufgabe aktuell KI-Ausführung läuft |
| `GetRunningCount()` | public | Gibt Anzahl aktuell laufender KI-Ausführungen zurück |
| `GetBufferedLines(aufgabeId)` | public | Gibt alle gepufferten Ausgabezeilen einer Session zurück |
| `Subscribe(aufgabeId, onLine)` | public | Abonniert neue Ausgabezeilen einer Session; kehrt `IDisposable` zum Beenden zurück |
| `StartKiLauf(aufgabeId, prompt, agent, kiPluginPrefix, model, kontextmodus, onStarted, onStatus, onCompleted)` | public | Startet KI-Lauf im Hintergrund (kehrt sofort zurück) |

**Verwendeter Kontext:**
- Hält alle aktiven `KiSession`-Objekte in `ConcurrentDictionary`
- Implementiert `IRunningAutomationStatusSource`
- Wird von `AufgabeRecoveryService` zur Abfrage von Lauf-Status genutzt

## `GitOrchestrationService`
Datei: `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`

Orchestriert Git-Operationen für Aufgaben.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `IssuesAbrufenAsync(repositoryId, ct)` | public | Ruft Issues aus einem Repository ab |
| `CommitAsync(aufgabeId, message, ct)` | public | Führt Commit durch und protokolliert die Aktion |
| `ResetAsync(aufgabeId, resetType, targetRef, ct)` | public | Setzt Commits zurück und protokolliert |

**Abhängigkeiten:**
- Nutzt `AufgabeService` zum Abrufen von Aufgabendetails
- Nutzt `ProjektService` für Projektinfos
- Nutzt `ProtokollService` zur Protokollierung
- Nutzt `IGitPlugin` für Git-Operationen
- Nutzt `PluginSelectionService` zur Plugin-Auswahl

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

Orchestriert den KI-gestützten Entwicklungsprozess: Klon → Branch → Paket Deploy → KI Start → Protokoll.

**Konstanten:**
- `DefaultContextCompressionSoftLimit = 12_000` Zeichen
- `DefaultContextCompressionHardLimit = 20_000` Zeichen
- `RateLimitSuggestionMarker = "[[SOFTWARESCHMIEDE_RATE_LIMIT]]"`

**Abhängigkeiten:**
- `AufgabeService`, `ProtokollService`, `ProjektService`
- `IGitPlugin` für Git-Operationen
- `PluginSelectionService` für Plugin-Auswahl
- `IAgentPackageService` für Agentenpaket-Info
- `IArbeitsverzeichnisResolver` für Arbeitsverzeichnis-Auflösung
- `RepositoryStartskriptService?` für Startup-Skripte (optional)
- `KiAufgabenBenachrichtigungsHub?` für Benachrichtigungen (optional)
- `IConfiguration` für Config

## `AufgabeRecoveryService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`

Service für manuelle Wiederherstellung festhängender Aufgaben.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `RecoverManuellAsync(aufgabeId, ct)` | public | Führt manuelle Recovery auf `InBearbeitung` aus (nur aus `KiAktiv` oder `TestsLaufen`) |

**Recovery-Logik:**
- Prüft ob Aufgabe existiert
- Prüft ob Status in `KiAktiv` oder `TestsLaufen` ist
- Prüft ob keine KI-Ausführung läuft (via `IRunningAutomationStatusSource`)
- Setzt Status zurück auf `InBearbeitung`

## `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

Lädt Plugins dynamisch aus dem Unterordner `plugins`. Implementiert `IPluginManager`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetSourceCodeManagementPlugins()` | public | Gibt alle geladenen Git-Plugins zurück |
| `GetDevelopmentAutomationPlugins()` | public | Gibt alle geladenen KI-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | public | Gibt First-Git-Plugin oder Exception |
| `GetDefaultDevelopmentAutomationPlugin()` | public | Gibt KI-Plugin mit Priorität (Copilot bevorzugt) oder Exception |

**Plugin-Discovery:**
- Scannt `/plugins` Verzeichnis (konfigurierbar)
- Lädt `.dll`-Dateien dynamisch via Reflection (`AssemblyLoadContext`)
- Instanziiert Klassen, die `IGitPlugin` oder `IKiPlugin` implementieren

## `PluginSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`

Service zum Lesen und Schreiben von Plugin-Einstellungen über `ICredentialStore`. Schlüssel werden als `<PluginPrefix>.<FieldKey>` gespeichert.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `GetAllPlugins(gitPlugins, kiPlugins)` | public | Gibt alle konfigurierten Plugins zurück (kombiniert) |
| `GetValue(plugin, field)` | public | Gibt gespeicherten Wert für Einstellungsfeld zurück |
| `SetValue(plugin, field, value)` | public | Speichert Wert für Einstellungsfeld |
| `DeleteValue(plugin, field)` | public | Löscht gespeicherten Wert |
| `HasValue(plugin, field)` | public | Gibt an, ob Wert für Feld gespeichert ist |

## `ArbeitsverzeichnisSettingsService`
Datei: `src/Softwareschmiede/Application/Services/ArbeitsverzeichnisSettingsService.cs`

Verwaltet Anwendungseinstellungen für Arbeitsverzeichnis (WorkDir).

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

Service zur Auswahl und Auflösung von Plugins für Aufgaben und Repositorys.

## `ArbeitsverzeichnisResolver`
Datei: `src/Softwareschmiede/Infrastructure/Services/ArbeitsverzeichnisResolver.cs`

Auflösung von Arbeitsverzeichnis-Pfaden.

## `WindowsCredentialStore`
Datei: `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs`

Implementierung von `ICredentialStore` via Windows Credential Manager (DPAPI).

## `CliRunner`
Datei: `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs`

Implementierung von `ICliRunner` für Ausführung von CLI-Befehlen mit Streaming.

## `SystemShutdownService`
Datei: `src/Softwareschmiede/Infrastructure/Services/SystemShutdownService.cs`

Implementierung von `ISystemShutdownService`.
