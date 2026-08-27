# Logikklassen / Services

## `AutonomAufgabenInitialisierungsService`
Datei: `src\Softwareschmiede\Application\Services\AutonomAufgabenInitialisierungsService.cs`

Orchestriert die Erstellung des Arbeitsverzeichnisses, des Repository-Klons und der Initialisierung von `state.json` und `permissions.json` für eine Autonome Aufgabe.

**Abhängigkeiten:**
- `SoftwareschmiededDbContext` (_db)
- `ICliRunner` (_cliRunner)
- `PluginSelectionService` (_pluginSelectionService)
- `IOptions<AutonomAufgabenOptions>` → `_options` (aktuell nicht für Feature-Flag-Gate abgefragt)
- `ILogger<AutonomAufgabenInitialisierungsService>` (_logger)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InitialisiereAsync(Aufgabe, AutonomAufgabeInitialisierungsAnfrage, CancellationToken)` | public | Erstellt Arbeitsverzeichnisstruktur, klont Repository, legt Projektbranch an, schreibt `state.json` und `permissions.json`, speichert `AutonomAufgabeKonfiguration` in DB |
| `ErstelleArbeitsverzeichnisStrukturAsync(string, CancellationToken)` | public | Erstellt Verzeichnisstruktur mit `plan.md`, `progress.md`, `governance.md` und Subdirectories |
| `KloneHauptRepositoryAsync(IGitPlugin, Aufgabe, string, CancellationToken)` | private | Klont Hauptrepository (idempotent: überspringt, wenn bereits geklont) |
| `ErstelleProjektbranchAsync(IGitPlugin, Aufgabe, string, string, CancellationToken)` | private | Legt Projektbranch lokal an oder checkt Remote-Branch aus (idempotent) |
| `LokalerBranchExistiertBereitsAsync(IGitPlugin, string, string, CancellationToken)` | private | Prüft, ob Branch lokal bereits existiert |
| `LadeRemoteBranchesAsync(IGitPlugin, string?, CancellationToken)` | private | Lädt Remote-Branches; gibt leere Liste zurück wenn nicht unterstützt |
| `BuildPermissionsJson(AutonomAufgabeInitialisierungsAnfrage)` | private | Erzeugt `permissions.json` mit Limits aus `_options` |
| `BuildStateJson(Aufgabe, AutonomAufgabeInitialisierungsAnfrage)` | private | Erzeugt `state.json` mit Initial-State |
| `BuildGovernanceMarkdown()` | private static | Erzeugt `governance.md` mit Governance-Dokumentation |
| `ValidiereAnfrage(AutonomAufgabeInitialisierungsAnfrage)` | private static | Validiert die Initialisierungsanfrage |
| `SicherstelleAufgabeGetrackt(Aufgabe)` | private | Gewährleistet EF-Change-Tracking für Relationship-Fixup |

**Wichtig:** Die Methode `InitialisiereAsync` nutzt `_options` zum Befüllen von `permissions.json` (z. B. `MaxConcurrentUnteragenten`, `MaxClones`), aber prüft **nicht** das `Enabled`-Flag. Dies ist ein Ansatzpunkt für das Feature-Flag-Gating.

---

## `ProjektleiterAgentService`
Datei: `src\Softwareschmiede\Application\Services\ProjektleiterAgentService.cs`

Verwaltet den Projektleiter-Agent-Lifecycle: Start, optionaler Resume nach App-Neustart, Stopp.

**Abhängigkeiten:**
- `SoftwareschmiededDbContext` (_db)
- `UnteragentGovernanceService` (_governanceService)
- `UnteragentGitProvisioningService` (_gitProvisioningService)
- `KiAusfuehrungsService` (_kiAusfuehrungsService)
- `PluginSelectionService` (_pluginSelectionService)
- `ILogger<ProjektleiterAgentService>` (_logger)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StarteAgentAsync(AutonomAufgabeKonfiguration, string?, CancellationToken)` | public | Startet den Projektleiter-Agenten: erzeugt Skill, startet CLI mit PseudoConsole, sendet Initial/Resume-Prompt verzögert |
| `StarteAgenNachAppNeustartAsync(Guid, string, CancellationToken)` | public | Startet Agent nach App-Neustart automatisch neu (falls nicht explizit gestoppt) |
| `StoppeAgenExplizitAsync(Guid, CancellationToken)` | public | Stoppt Agent explizit auf Benutzerwunsch: setzt `ExplizitGestoppt`, beendet CLI-Prozess |
| `SendeInitialPromptVerzoegertAsync(Guid, string, CancellationToken)` | private | Sendet Initial-Prompt nach Verzögerung (Fire-and-Forget) |
| `BuildDefaultProjektleiterSkill(AutonomAufgabeKonfiguration)` | private static | Erzeugt Default-Projektleiter-Skill |

---

## `AutonomAufgabeStartService`
Datei: `src\Softwareschmiede.App\Services\AutonomAufgabeStartService.cs`

Orchestriert den Ablauf "Autonome Aufgabe initialisieren": öffnet den Initialisierungsdialog, lädt die aktualisierte Aufgabe, zeigt Detail-Ansicht an.

**Abhängigkeiten:**
- `IServiceProvider` (_serviceProvider)
- `IDialogService` (_dialogService)
- `AufgabeService` (_aufgabeService)
- `ILogger<AutonomAufgabeStartService>` (_logger)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StarteAsync(Aufgabe, CancellationToken)` | public | Zeigt Initialisierungsdialog an; bei Erfolg lädt aktualisierte Aufgabe und erzeugt `AutonomAufgabeDetailViewModel` |

**Gibt zurück:** `AutonomAufgabeStartResult?` (null bei Abbruch, Fehlertext oder Detail-ViewModel bei Erfolg)

---

## `EntwicklungsprozessService`
Datei: `src\Softwareschmiede\Application\Services\EntwicklungsprozessService.cs`

Koordiniert Git-Repository-Setup für Aufgaben (sowohl für einfache als auch autonome Aufgaben). **Dieser Service ist zentral für den nicht-autonomen Weg ("einfaches Starten").**

**Abhängigkeiten:**
- `AufgabeService` (_aufgabeService)
- `ProtokollService` (_protokollService)
- `IGitPlugin` (_gitPlugin)
- `PluginSelectionService` (_pluginSelectionService)
- `IArbeitsverzeichnisResolver` (_arbeitsverzeichnisResolver)
- `EntwicklungsprozessServiceOptions` (_options) — optionale Dependencies für KI-Ausführung, Git-Orchestrierung
- `ILogger<EntwicklungsprozessService>` (_logger)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync(Guid, string, string?, string?, CancellationToken)` | public | Repository-Setup: Klon, Branch-Anlage, Status auf `Gestartet` setzen. **Dies ist der "einfache Weg" für nicht-autonome Aufgaben.** |
| `ProzessStartenUndCliStartenAsync(Guid, string, string?, string?, CancellationToken)` | public | Kombiniert Repository-Setup + CLI-Start in einem Schritt; setzt Status direkt auf `Gestartet` und startet CLI |
| `ResolveRepositoryAsync(Aufgabe, string, CancellationToken)` | private | Löst Repository auf |
| `ResolvePluginAsync(GitRepository, string?, Guid, CancellationToken)` | private | Wählt Git-Plugin aus |
| `ValidateBaseBranchExistsAsync(GitRepository, IGitPlugin, CancellationToken)` | private | Validiert, dass Basis-Branch existiert |
| `PrepareCloneDirectoryAsync(IGitPlugin, string, Guid, CancellationToken)` | private | Bereitet Klonverzeichnis vor |
| `SetupBranchAsync(IGitPlugin, string, string, string?, string, Aufgabe, CancellationToken)` | private | Legt Branch an oder checkout existierenden Branch |
| `FinalizeStartAsync(Guid, Aufgabe, GitRepository, string, string, bool, string?, CancellationToken)` | private | Finalisiert: setzt Status, persistiert Branch-Info, optional Startskript-Ausführung |

**Wichtig:** `ProzessStartenAsync` ist der zentrale Einstiegspunkt für den nicht-autonomen Weg und sollte bei deaktiviertem Feature-Flag weiterhin verfügbar bleiben.

---

## `KiAusfuehrungsService`
Datei: `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs`

Singleton-Service, der laufende CLI-Prozesse für KI-Ausführungen verwaltet: startet, stoppt, überwacht pro Aufgabe.

**Abhängigkeiten:**
- `ILogger<KiAusfuehrungsService>` (_logger)
- `ILoggerFactory` (_loggerFactory)
- `IServiceScopeFactory` (_scopeFactory)
- `IPseudoConsoleProcessLauncher` (_launcher) — optional, default `Win32PseudoConsoleProcessLauncher`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCliAsync(Guid, IKiPlugin, string, string?, CancellationToken, RepositoryStartKonfiguration?, IGitPlugin?)` | public | Startet einen CLI-Prozess für eine Aufgabe; gibt `CliProcessHandle` zurück |
| `StartWithPseudoConsoleAsync(Guid, IKiPlugin, string, string?, CancellationToken)` | public | Startet CLI mit PseudoConsole für autonome Aufgaben (später in Flow) |
| `IsRunning(Guid)` | public | Gibt an, ob CLI-Prozess läuft |
| `GetRunningProcess(Guid)` | public | Gibt laufenden `Process` zurück oder null |
| `GetRunningCount()` | public | Gibt Anzahl laufender Prozesse zurück |
| `StopCliAsync(Guid, CancellationToken)` | public | Stoppt CLI-Prozess; wartet auf Graceful Shutdown |

**Event:**
- `CliProcessStatusChanged`: Wird ausgelöst wenn CLI-Prozess startet, stoppt oder Fehler auftritt

---

## `AppEinstellungService`
Datei: `src\Softwareschmiede\Application\Services\AppEinstellungService.cs`

Generischer Service zum Lesen und Schreiben von Anwendungseinstellungen (Key-Value-Paare) in der Datenbank.

**Abhängigkeiten:**
- `SoftwareschmiededDbContext` (_db)
- `ILogger<AppEinstellungService>` (_logger)

**Bekannte Schlüssel-Konstanten:**
- `WindowPositionXKey`, `WindowPositionYKey`, `WindowWidthKey`, `WindowHeightKey`
- `DesignModeKey`
- `DefaultKiPluginKey`, `DefaultScmPluginKey`
- `LogLevelKey`
- `IdePluginOrderKey`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSettingAsync(string, CancellationToken)` | public | Liest String-Wert einer Einstellung; gibt null zurück wenn nicht vorhanden |
| `GetIntSettingAsync(string, CancellationToken)` | public | Liest Integer-Wert; gibt null bei ungültigem Format |
| `GetBoolSettingAsync(string, CancellationToken)` | public | Liest Boolean-Wert; gibt null bei ungültigem Format |
| `SetSettingAsync(string, string?, CancellationToken)` | public | Speichert oder überschreibt String-Wert |
| `SetIntSettingAsync(string, int, CancellationToken)` | public | Speichert Integer-Wert |
| `SetBoolSettingAsync(string, bool, CancellationToken)` | public | Speichert Boolean-Wert |
| `GetSettingsAsync(IReadOnlyCollection<string>, CancellationToken)` | public | Liest mehrere Werte in einer DB-Abfrage |
| `GetWindowGeometryAsync(CancellationToken)` | public | Liest alle Fenstergeometrie-Einstellungen |

**Persistierung:** Alle Einstellungen werden als `AppEinstellung`-Entities in der DB gespeichert (Key-Value mit `AktualisiertAm`-Timestamp).

---

## `AutonomAufgabeDetailViewModel` (erwähnt in TaskDetailViewModel)
Datei: (Verzeichnis indiziert durch Glob)

Verwaltet die Detail-Ansicht einer autonomen Aufgabe; wird von `AutonomAufgabeStartService` erzeugt.

**Abhängigkeiten:**
- `Aufgabe` (konstruktor)
- `AutonomAufgabeKonfiguration` (konstruktor)
- `ProjektleiterAgentService`
- `SessionManagementService`
- `KiAusfuehrungsService`
- `ILogger<AutonomAufgabeDetailViewModel>`
