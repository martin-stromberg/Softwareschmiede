# Services und Logik: Aufgabenworkflow Optimierung

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync(projektId, ct)` | public | Gibt alle aktiven Aufgaben eines Projekts zurück (nicht archiviert) |
| `GetArchiviertByProjektAsync(projektId, ct)` | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync(ct)` | public | Zählt Aufgaben in Status `InArbeit` und `Wartend` |
| `GetByIdAsync(id, ct)` | public | Gibt eine Aufgabe nach ID zurück |
| `GetDetailAsync(id, ct)` | public | Gibt eine Aufgabe mit Projekt, IssueReferenz, GitRepository, Protokolleinträgen zurück |
| `GetLatestDiffResultIdAsync(aufgabeId, ct)` | public | Gibt die neueste DiffResult-ID einer Aufgabe zurück |
| `GetLatestDiffResultIdForFileAsync(aufgabeId, relativePath, ct)` | public | Gibt die neueste DiffResult-ID für eine spezifische Datei zurück |
| `CreateAsync(projektId, titel, anforderungsBeschreibung, gitRepositoryId, ct)` | public | Erstellt neue Aufgabe mit Status `Neu` |
| `CreateFromIssueAsync(projektId, issue, gitRepositoryId, ct)` | public | Erstellt neue Aufgabe aus Issue mit Status `Neu` |
| `UpdateAsync(id, titel, anforderungsBeschreibung, kiPluginPrefix, ct)` | public | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix |
| `DeleteAsync(id, ct)` | public | Löscht eine Aufgabe (nicht möglich in Status `Gestartet`, `InArbeit`, `Wartend`) |
| `VerwerfenAsync(id, aktion, ct)` | public | Verwirft neue Aufgaben durch Archivieren oder Löschen |
| `ArchivierenAsync(id, ct)` | public | Archiviert eine beendete Aufgabe |
| `StartenAsync(id, branchName, lokalerKlonPfad, ct)` | public | **Setzt Status auf `ArbeitsverzeichnisEingerichtet`** (wird angepasst) |
| `SavePromptVorschlagAsync(id, prompt, ausfuehrenAbUtc, ct)` | public | Speichert einen Prompt-Vorschlag und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync(id, ct)` | public | Löscht den gespeicherten Prompt-Vorschlag |
| `AbschliessenAsync(id, ct)` | public | Schließt eine Aufgabe ab: Status → `Beendet`, setzt Branch/Klonpfad null |
| `SetStatusAsync(id, newStatus, ct)` | public | Setzt Status mit Validierung erlaubter Übergänge |
| `StatusSetzenAsync(id, status, ct)` | public | Setzt Status ohne Transitions-Validierung |
| `UpdateHeartbeatAsync(id, ct)` | public | Aktualisiert LastHeartbeatUtc |
| `GetHeartbeatAgeMinutesAsync(id, ct)` | public | Gibt Alter des letzten Heartbeats in Minuten zurück |
| `ValidateStatusTransition(current, next)` | private | Validiert erlaubte Status-Übergänge |

**Bemerkungen zu Anforderung:**
- `StartenAsync` setzt Status auf `ArbeitsverzeichnisEingerichtet` — muss in Kombination mit `EntwicklungsprozessService.ProzessStartenAsync` zu direkter Übergabe auf `Gestartet` angepasst werden.
- Keine Methode für kombiniertes Klone + CLI-Start vorhanden.

---

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync(aufgabeId, repositoryUrl, basisBranchName, selectedScmPluginPrefix, ct)` | public | **Richtet Git-Repository ein: Klon, Branch, Startskript; setzt Status auf `ArbeitsverzeichnisEingerichtet`** (wird erweitert) |
| `CommitDurchfuehrenAsync(aufgabeId, message, ct)` | public | Führt manuellen Commit durch |
| `ResetDurchfuehrenAsync(aufgabeId, resetType, targetRef, ct)` | public | Setzt Commits zurück |
| `PushDurchfuehrenAsync(aufgabeId, ct)` | public | Pusht Branch auf Remote |
| `PullDurchfuehrenAsync(aufgabeId, ct)` | public | Holt Änderungen vom Remote |
| `PullRequestErstellenAsync(aufgabeId, repositoryId, title, body, ct)` | public | Erstellt Pull Request |
| `AbschliessenAsync(aufgabeId, ct)` | public | Schließt Aufgabe ab: löscht Klon, setzt Status auf `Beendet` |

**Abhängigkeiten:**
- `AufgabeService` — für Status-Updates
- `ProtokollService` — für Protokolleinträge
- `IGitPlugin` — für Git-Operationen
- `PluginSelectionService` — für Plugin-Auflösung
- `IArbeitsverzeichnisResolver` — für Arbeitsverzeichnis-Auflösung
- `RepositoryStartskriptService` — optionales Startskript-Ausführen

**Bemerkungen zu Anforderung:**
- `ProzessStartenAsync` muss erweitert werden, um direkt nach dem Klone auch die CLI zu starten (kombinierter Ablauf).
- Fehlerbehandlung mit Rollback erforderlich.

---

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStoredDefaultPluginPrefixAsync(pluginType, ct)` | public | Liest gespeicherten Default-Plugin |
| `SaveDefaultPluginPrefixAsync(pluginType, pluginPrefix, ct)` | public | Speichert Default-Plugin |
| `ResolveSourceCodeManagementPluginAsync(selectedPluginPrefix, ct)` | public | Löst SCM-Plugin auf (explizit → gespeichert → Fallback) |
| `GetAvailableKiPluginPrefixesAsync(ct)` | public | Gibt Liste verfügbarer KI-Plugin-Prefixe zurück |
| `ResolveDevelopmentAutomationPluginAsync(selectedPluginPrefix, ct)` | public | Löst KI-Plugin auf |
| `ResolvePluginAsync<TPlugin>(...)` | private | Generische Plugin-Auflösungslogik |
| `TryResolveByPrefix<TPlugin>(plugins, pluginPrefix)` | private | Sucht Plugin nach Prefix |
| `GetKiFallbackSortKey(plugin)` | private | Definiert Fallback-Sortierung für KI-Plugins |

**Abhängigkeiten:**
- `IPluginManager` — Verwaltet verfügbare Plugins
- `PluginDefaultSettingsService` — Speichert/lädt Defaults

**Bemerkungen zu Anforderung:**
- `ResolveDevelopmentAutomationPluginAsync` wird verwendet, aber hat keine Dialog-Integration.
- Dialog-Kontext muss hinzugefügt werden (Projekt-ID für Projekt-Level-Speicherung).

---

## `PluginDefaultSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginDefaultSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDefaultPluginPrefixAsync(pluginType, ct)` | public | Liest Standard-Plugin-Prefix für Typ aus AppEinstellung |
| `SaveDefaultPluginPrefixAsync(pluginType, pluginPrefix, ct)` | public | Speichert Standard-Plugin-Prefix in AppEinstellung |
| `BuildKey(pluginType)` | private | Konstruiert Schlüssel aus PluginType |

**Speicherung:** Nutzt `AppEinstellung`-Entity mit Key `plugins.default.{PluginType}`.

**Bemerkungen zu Anforderung:**
- Aktuell nur **globale** Standard-Speicherung pro `PluginType`.
- **Projekt-Level-Speicherung** muss hinzugefügt werden (Ansatz: Scoped-Key wie `plugins.default.project.{ProjektId}.{PluginType}` oder eigene Tabelle).

---

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(aufgabeId)` | public | Prüft, ob CLI für Aufgabe läuft |
| `GetRunningCount()` | public | Gibt Anzahl laufender CLI-Prozesse zurück |
| `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct)` | public | Startet CLI-Prozess, gibt CliProcessHandle zurück |
| `StopCliAsync(aufgabeId, ct)` | public | Stoppt CLI-Prozess (SIGTERM → 5s → Kill) |
| `GetLastExitCode(aufgabeId)` | public | Gibt Exit-Code des Prozesses zurück |
| `UpdateHeartbeat(aufgabeId)` | public | Aktualisiert LastHeartbeat des Prozesses |
| `Dispose()` | public | Gibt Ressourcen frei (stoppt alle Prozesse) |
| `PersistFehlgeschlagenAsync(aufgabeId)` | private | Setzt Status auf `Beendet` wenn CLI mit Fehler endet |

**Events:**
- `CliProcessStatusChanged` — Wird ausgelöst wenn Prozess startet/stoppt/fehler
- `RunningCountChanged` — Wird ausgelöst wenn Anzahl laufender Prozesse sich ändert

**Bemerkungen zu Anforderung:**
- Keine Dialog-Integration für Plugin-Wechsel.
- `StopCliAsync` + `StartCliAsync` können kombiniert werden für Plugin-Wechsel.

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `AufgabeId` | Guid | ID der angezeigten Aufgabe |
| `Aufgabe` | Aufgabe? | Die geladene Aufgabe |
| `AufgabeTitel` | string | Titel der Aufgabe (oder "wird geladen…") |
| `AufgabeStatus` | AufgabeStatus | Status der Aufgabe |
| `IsLoading` | bool | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | string? | Fehlermeldung bei Fehlern |
| `IsCliRunning` | bool | Gibt an, ob CLI läuft |
| `KannCliStarten` | bool | `!IsCliRunning && Status ∈ {Gestartet, Wartend} && PluginPrefixGesetzt` |
| `KannCliStoppen` | bool | `IsCliRunning` |
| `SelectedKiPluginPrefix` | string? | Aktuell gewähltes KI-Plugin |
| `OptionalCliParameters` | string? | Optionale CLI-Parameter |
| `EmbeddedWindowHandle` | IntPtr | Handle für eingebettetes CLI-Fenster |
| `Protokolleintraege` | ObservableCollection<Protokolleintrag> | Log-Einträge |
| `VerfuegbareKiPlugins` | ObservableCollection<string> | Verfügbare Plugin-Prefixe |
| `IsInfoViewVisible` | bool | Sichtbarkeit Info-Panel vs. CLI-Fenster |
| `EditTitel` | string? | Editable Kopie für Edit-Modus |
| `EditAnforderungsBeschreibung` | string? | Editable Kopie für Edit-Modus |
| `ShowEditPanel` | bool | `Status == Neu` |
| `ShowCliPanel` | bool | `Status ∈ {Gestartet, InArbeit, Wartend}` |
| `ShowDiffPanel` | bool | `Status == Beendet` |
| `KannSpeichern` | bool | `Status ∈ {Neu, Gestartet} && !IsCliRunning && TitelGesetzt` |
| `KannLoeschen` | bool | `Status ∉ {Beendet, Archiviert} && !IsCliRunning` |

| Command | CanExecute | Beschreibung |
|---------|-----------|--------------|
| `LadenCommand` | immer | Lädt die Aufgabe |
| `CliStartenCommand` | `KannCliStarten` | **Startet CLI-Prozess** |
| `CliStoppenCommand` | `KannCliStoppen` | Stoppt CLI-Prozess |
| `StatusGestartetSetzenCommand` | `Status == Neu && !IsCliRunning` | **Setzt Status auf Gestartet** |
| `AufgabeAbschliessenCommand` | `ShowCliPanel && !IsCliRunning` | Setzt Status auf Beendet, löscht Klon |
| `SpeichernCommand` | `KannSpeichern` | Speichert Titel und Beschreibung |
| `LoeschenCommand` | `KannLoeschen` | Löscht Aufgabe (mit Bestätigungsdialog) |
| `InfoCliToggleCommand` | immer | Togglet Sichtbarkeit Info-Panel ↔ CLI |
| `ZurueckCommand` | immer | Navigiert zurück |

**Events:**
- `CliProzessGestartet` — Wird ausgelöst wenn CLI startet (übergibt Process-Handle)

**Abhängigkeiten:**
- `AufgabeService` — CRUD-Operationen
- `ProtokollService` — Lädt Protokoll
- `KiAusfuehrungsService` — CLI-Verwaltung
- `EntwicklungsprozessService` — Repository-Setup und Abschluss
- `PluginSelectionService` — Plugin-Auflösung
- `IDialogService` — Dialog-Interaktion

**Bemerkungen zu Anforderung:**
- `StatusGestartetSetzenCommand` ist vorhanden, aber kombiniert nicht Repository-Klone mit CLI-Start.
- Kein `StartenCommand` für kombinierte Aktion (Klone + CLI-Start).
- Kein `PluginAendernCommand` für Plugin-Wechsel mit Dialog.
- `LadenAsync` hat keine Logik für automatischen CLI-Neustart bei Status `Gestartet` ohne laufenden Prozess.

