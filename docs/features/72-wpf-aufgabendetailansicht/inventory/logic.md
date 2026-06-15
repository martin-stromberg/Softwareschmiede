# Logik-Klassen

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync(Guid, CancellationToken)` | public async | Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück |
| `GetArchiviertByProjektAsync(Guid, CancellationToken)` | public async | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync(CancellationToken)` | public async | Gibt die Anzahl aktiver (InArbeit) und wartender (Wartend) Aufgaben als Tupel zurück |
| `GetByIdAsync(Guid, CancellationToken)` | public async | Gibt eine Aufgabe anhand ihrer ID zurück |
| `GetDetailAsync(Guid, CancellationToken)` | public async | Gibt eine Aufgabe mit IssueReferenz und Protokolleinträgen zurück (für Detail-Views) |
| `GetLatestDiffResultIdAsync(Guid, CancellationToken)` | public async | Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Aufgabe zurück |
| `GetLatestDiffResultIdForFileAsync(Guid, string, CancellationToken)` | public async | Gibt die ID des zuletzt generierten Diff-Ergebnisses für eine spezifische Datei zurück |
| `CreateAsync(Guid, string, string?, Guid?, CancellationToken)` | public async | Erstellt eine neue Aufgabe mit Status `Neu` |
| `CreateFromIssueAsync(Guid, Issue, Guid?, CancellationToken)` | public async | Erstellt eine neue Aufgabe aus einem Issue mit IssueReferenz |
| `UpdateAsync(Guid, string, string?, string?, CancellationToken)` | public async | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix einer Aufgabe |
| `DeleteAsync(Guid, CancellationToken)` | public async | Löscht eine Aufgabe (mit Validierung: nicht während aktive Status Gestartet/InArbeit/Wartend) |
| `VerwerfenAsync(Guid, VerwerfenAktion, CancellationToken)` | public async | Verwirft eine Aufgabe im Status `Neu` durch Archivieren oder Löschen |
| `ArchivierenAsync(Guid, CancellationToken)` | public async | Archiviert eine Aufgabe (nur für Status `Beendet` möglich) |
| `StartenAsync(Guid, string, string, CancellationToken)` | public async | Startet eine Aufgabe: Status → `ArbeitsverzeichnisEingerichtet`, Branch und Arbeitsverzeichnis setzen |
| `SavePromptVorschlagAsync(Guid, string?, DateTimeOffset?, CancellationToken)` | public async | Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync(Guid, CancellationToken)` | public async | Entfernt den gespeicherten Vorschlagsprompt und den geplanten Ausführungszeitpunkt |
| `AbschliessenAsync(Guid, CancellationToken)` | public async | Schließt eine Aufgabe ab: Status → `Beendet`, AbschlussDatum setzen, Branch- und Klonpfad-Felder leeren |
| `SetStatusAsync(Guid, AufgabeStatus, CancellationToken)` | public async | Setzt den Status einer Aufgabe mit Validierung der erlaubten Übergänge |
| `StatusSetzenAsync(Guid, AufgabeStatus, CancellationToken)` | public async | Setzt den Status einer Aufgabe generisch (ohne Transitions-Validierung) |
| `UpdateHeartbeatAsync(Guid, CancellationToken)` | public async | Aktualisiert LastHeartbeatUtc der Aufgabe |
| `GetHeartbeatAgeMinutesAsync(Guid, CancellationToken)` | public async | Gibt die Minuten seit dem letzten Heartbeat zurück (null wenn kein Heartbeat gesetzt) |
| `ValidateStatusTransition(AufgabeStatus, AufgabeStatus)` | private static | Validiert die Übergänge zwischen Status-Werten (definiert erlaubte Transitionen) |

**Status-Übergänge (ValidateStatusTransition):**
- `Neu` → `ArbeitsverzeichnisEingerichtet`
- `ArbeitsverzeichnisEingerichtet` → `Gestartet`
- `Gestartet` → `InArbeit`
- `InArbeit` → `Beendet`, `Wartend`
- `Wartend` → `InArbeit`, `Beendet`
- `Beendet` → keine (Terminal)
- `Archiviert` → keine (Terminal)
- Alle Stati können zu `Archiviert` übergehen


## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync(Guid, string, string?, string?, CancellationToken)` | public async | Richtet das Git-Repository für eine Aufgabe ein: Klon, Branch, optionales Startskript. Setzt Status auf `ArbeitsverzeichnisEingerichtet` |
| `CommitDurchfuehrenAsync(Guid, string, CancellationToken)` | public async | Führt einen manuellen Commit durch |
| `ResetDurchfuehrenAsync(Guid, string, string?, CancellationToken)` | public async | Setzt Commits zurück |
| `PushDurchfuehrenAsync(Guid, CancellationToken)` | public async | Pusht den Branch auf den Remote |
| `PullDurchfuehrenAsync(Guid, CancellationToken)` | public async | Holt Änderungen vom Remote |
| `PullRequestErstellenAsync(Guid, string, string, string, CancellationToken)` | public async | Erstellt einen Pull Request für die Aufgabe |
| `AbschliessenAsync(Guid, CancellationToken)` | public async | Schließt die Aufgabe ab: Klon löschen, Status auf `Beendet` setzen. Nutzt `AufgabeService.AbschliessenAsync()` |
| `GetRemoteBranchesAsync(string, string?, CancellationToken)` | public async | Gibt die Remote-Branches eines Repositories zurück |
| `RepositoryStartskriptAusfuehrenAsync(Guid, CancellationToken)` | public async | Führt das Repository-Startskript für eine Aufgabe manuell aus |
| `TryParseRateLimitSuggestion(string, out SuggestionInfo?)` | public static | Parst einen Rate-Limit-Marker aus einer CLI-Ausgabezeile |


## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(Guid)` | public | Gibt an, ob ein CLI-Prozess für eine Aufgabe läuft |
| `GetRunningCount()` | public | Gibt die Anzahl aktuell laufender CLI-Prozesse zurück |
| `StartCliAsync(Guid, IKiPlugin, string, string?, CancellationToken)` | public async | Startet einen CLI-Prozess für eine Aufgabe und gibt das Handle zurück |
| `StopCliAsync(Guid, CancellationToken)` | public async | Stoppt den laufenden CLI-Prozess für eine Aufgabe (SIGTERM → 5s → Kill) |

**Abonnierte Events:** keine

**Publizierte Events:**
- `CliProcessStatusChanged` — Wird ausgelöst, wenn ein CLI-Prozess gestartet, gestoppt oder ein Fehler aufgetreten ist (Parametralisierung: `Guid aufgabeId, CliProcessStatus status`)
- `RunningCountChanged` — Wird ausgelöst, wenn sich die Anzahl laufender Prozesse ändert


## `ProtokollService`
Datei: `src/Softwareschmiede/Application/Services/ProtokollService.cs` (wird von TaskDetailViewModel verwendet)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByAufgabeAsync(Guid, CancellationToken)` | public async | Gibt alle Protokolleinträge einer Aufgabe zurück |
| `AddEintragAsync(Guid, ProtokollTyp, string, CancellationToken)` | public async | Fügt einen Protokolleintrag hinzu |
| `AddStatusUebergangAsync(Guid, AufgabeStatus, AufgabeStatus, CancellationToken)` | public async | Fügt einen Protokolleintrag für einen Status-Übergang ein |


## `PluginSelectionService`
Datei: wird von TaskDetailViewModel verwendet

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAvailableKiPluginPrefixesAsync(CancellationToken)` | public async | Gibt verfügbare KI-Plugin-Prefixe zurück |
| `ResolveDevelopmentAutomationPluginAsync(string?, CancellationToken)` | public async | Resolves ein KI-Plugin basierend auf dem Prefix |
