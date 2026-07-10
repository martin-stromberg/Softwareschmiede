# Services und Logik

## `AufgabeService`
Datei: `src\Softwareschmiede\Application\Services\AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync(Guid, CancellationToken)` | public | Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück |
| `GetArchiviertByProjektAsync(Guid, CancellationToken)` | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync(CancellationToken)` | public | Gibt die Anzahl aktiver und wartender Aufgaben als Tupel zurück |
| `GetByIdAsync(Guid, CancellationToken)` | public | Gibt eine Aufgabe anhand ihrer ID zurück |
| `GetDetailAsync(Guid, CancellationToken)` | public | Gibt eine Aufgabe mit vollständigen Details zurück |
| `GetLatestDiffResultIdAsync(Guid, CancellationToken)` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses zurück |
| `GetLatestDiffResultIdForFileAsync(Guid, string, CancellationToken)` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses für eine Datei zurück |
| `CreateAsync(...)` | public | Erstellt eine neue Aufgabe mit Status `Neu` |
| `CreateFromIssueAsync(...)` | public | Erstellt eine neue Aufgabe aus einem Issue |
| `UpdateAsync(...)` | public | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix |
| `UpdateIssueReferenzAsync(Guid, Issue?, CancellationToken)` | public | Setzt/aktualisiert die IssueReferenz einer Aufgabe |
| `DeleteAsync(Guid, CancellationToken)` | public | Löscht eine Aufgabe |
| `VerwerfenAsync(Guid, VerwerfenAktion, CancellationToken)` | public | Verwirft eine neue Aufgabe durch Archivieren oder Löschen |
| `ArchivierenAsync(Guid, CancellationToken)` | public | Archiviert eine beendete Aufgabe |
| `StartenAsync(Guid, string, string, CancellationToken)` | public | Startet eine Aufgabe: Status → Gestartet, Branch und Klonpfad setzen |
| `SavePromptVorschlagAsync(Guid, string?, DateTimeOffset?, CancellationToken)` | public | Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync(Guid, CancellationToken)` | public | Entfernt den gespeicherten Vorschlagsprompt |
| `AbschliessenAsync(Guid, CancellationToken)` | public | Schließt eine Aufgabe ab: Status → Beendet |
| `SetStatusAsync(Guid, AufgabeStatus, CancellationToken)` | public | Setzt den Status mit Validierung erlaubter Übergänge |
| `StatusSetzenAsync(Guid, AufgabeStatus, CancellationToken)` | public | Setzt den Status generisch ohne Transitions-Validierung |
| `UpdateHeartbeatAsync(Guid, CancellationToken)` | public | Aktualisiert `LastHeartbeatUtc` der Aufgabe |
| `GetHeartbeatAgeMinutesAsync(Guid, CancellationToken)` | public | Gibt die Minuten seit dem letzten Heartbeat zurück |
| `GetAktiveAufgabenAsync(CancellationToken)` | public | **ZENTRAL:** Gibt alle aktiven Aufgaben (Status Gestartet oder Wartend) zurück, sortiert nach letzter Aktivität, maximal 20 |

### Interne Mechaniken:
- **Status-Übergänge:** Validierung über `ValidateStatusTransition()` (nur für `SetStatusAsync()`, nicht für `StatusSetzenAsync()`)
- **Abfrage-Predicate:** `IstAktivOderWartendPredicate` definiert die Regel für aktive/wartende Aufgaben
- **Datenbankzugriff:** Verwendet Entity Framework Core mit Async/Await

---

## `KiAusfuehrungsService`
Datei: `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(Guid)` | public | Gibt an, ob für eine Aufgabe ein CLI-Prozess läuft |
| `GetRunningProcess(Guid)` | public | Gibt den laufenden Prozess für eine Aufgabe zurück, oder null |
| `GetRunningCount()` | public | Gibt die Anzahl aktuell laufender CLI-Prozesse zurück |
| `StartCliAsync(...)` | public | Startet einen CLI-Prozess für eine Aufgabe |
| `StartWithPseudoConsoleAsync(...)` | public | Startet einen CLI-Prozess über die Windows Pseudo Console API |
| `GetPseudoConsoleSession(Guid)` | public | Gibt die `PseudoConsoleSession` für eine Aufgabe zurück, oder null |
| `StopCliAsync(Guid, CancellationToken)` | public | Stoppt den laufenden CLI-Prozess einer Aufgabe (SIGTERM → 5s → Kill) |
| `GetLastExitCode(Guid)` | public | Gibt den Exit-Code des letzten Prozesses zurück |
| `UpdateHeartbeat(Guid)` | public | Aktualisiert `LastHeartbeatUtc` im internen Handle |
| `Dispose()` | public | Bereinigt alle laufenden Prozesse |

**Abonnierte Events:**
- `Process.Exited` – Wird registriert für jeden gestarteten Prozess

**Publizierte Events:**
- `CliProcessStatusChanged` – Action<Guid, CliProcessStatus>: Wird ausgelöst, wenn ein CLI-Prozess gestartet, gestoppt oder mit Fehler beendet wird
- `RunningCountChanged` – Action<int, int>: Wird ausgelöst, wenn sich die Anzahl laufender Prozesse ändert

### Interne Mechaniken:
- **Prozess-Handles:** `ConcurrentDictionary<Guid, CliProcessHandle>` verwaltet aktive Prozesse
- **Heartbeat-Tracking:** Jedes `CliProcessHandle` hat ein `LastHeartbeat`-Property
- **Start-Synchronisierung:** `SemaphoreSlim` verhindert Race-Conditions beim Starten mehrerer Prozesse
- **Event-Wiring:** Der `Exited`-Handler wird VOR `process.Start()` registriert, um Race-Conditions zu vermeiden

---

## `AufgabeRecoveryService`
Datei: `src\Softwareschmiede\Application\Services\AufgabeRecoveryService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanForRecoveryCandidatesAsync(CancellationToken)` | public | Scannt alle Aufgaben nach Recovery-Kandidaten (Status aktiv/wartend, Heartbeat > 5 Min. alt, kein laufender Prozess) |
| `RecoverManuellAsync(Guid, CancellationToken)` | public | Führt manuelle Recovery durch mit Concurrency-Conflict-Erkennung |

**Konstanten:**
- `HeartbeatTimeoutMinutes = 5` – Schwelle ab der ein Heartbeat als abgelaufen gilt

### Interne Mechaniken:
- **Heartbeat-Timeout:** Abgelaufener Heartbeat (älter als 5 Minuten) ist ein Indiz für verwaiste Aufgaben
- **Concurrency-Schutz:** Verwendet `RecoveryVersion` Versionsverwaltung und Transaktionen
- **Audit-Logging:** Erstellt `Protokolleintrag` für jede Recovery-Operation
- **Dependency:** Nutzt `IRunningAutomationStatusSource` zur Prüfung laufender Prozesse

---

## `MainWindowViewModel`
Datei: `src\Softwareschmiede.App\ViewModels\MainWindowViewModel.cs`

| Property/Methode | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `AktiveAufgabenListe` | public | **Zentral:** ObservableCollection<Aufgabe> für die Seitenleisten-Anzeige |
| `CurrentView` | public | Das aktuell angezeigte ViewModel |
| `AktiveAufgabenAktualisierenAsync(CancellationToken)` | public | Ruft `AufgabeService.GetAktiveAufgabenAsync()` auf und aktualisiert die Collection |

### Mechaniken:
- Beim Wechsel von `CurrentView` (z.B. zum Dashboard) wird automatisch `AktiveAufgabenAktualisierenAsync()` aufgerufen
- Die `AktiveAufgabenListe` wird mit `ReplaceAll()` aktualisiert (Erweiterungsmethode)
- **Fehlende Aktualisierungsmechanik:** Keine sichtbare Timer- oder Event-basierte periodische/ereignisgesteuerte Aktualisierung

---

## `DashboardViewModel`
Datei: `src\Softwareschmiede.App\ViewModels\DashboardViewModel.cs`

| Property/Methode | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `AktiveAufgabenListe` | public | ObservableCollection<Aufgabe>, die vom MainWindowViewModel über `Initialize()` gesetzt wird (gemeinsame Datenquelle) |
| `AktiveAufgaben` | public | Anzahl aktiver Aufgaben (Status Gestartet) |
| `WartendAufgaben` | public | Anzahl wartender Aufgaben (Status Wartend) |
| `Initialize(ObservableCollection, Action)` | public | Verdrahtet die gemeinsame Aufgabenliste und Navigationsaktion |

### Mechaniken:
- Teilt die `AktiveAufgabenListe` mit `MainWindowViewModel` (gleiche Instanz)
- `LadenAsync()` ruft `AufgabeService.GetAktiveUndWartendeCountAsync()` auf und aktualisiert nur die Zähler, nicht die Liste selbst
- **Fehlende Aktualisierungsmechanik:** Keine automatische Aktualisierung der Liste nach Status-Wechseln

---

## `KiAusfuehrungsStatusConverter`
Datei: `src\Softwareschmiede.App\Converters\AppConverters.cs`

| Methode | Rückgabe | Logik |
|---------|----------|-------|
| `Convert(object, Type, object, CultureInfo)` | `string` | Konvertiert `Aufgabe`-Objekt zu Status-String: "▶ Läuft" (wenn `AktiveRunId` + aktueller Heartbeat), "⏸ Wartet" (wenn Status == Wartend), "✓ Bereit" (Fallback) |
| `ConvertBack(...)` | - | Wirft `NotSupportedException` (Converter ist uni-direktional) |

### Status-Berechnung:
1. Wenn `AktiveRunId != null` UND `LastHeartbeatUtc != null` UND `jetzt - LastHeartbeatUtc < 5 Minuten`:
   - → `"▶ Läuft"`
2. Wenn `Status == AufgabeStatus.Wartend`:
   - → `"⏸ Wartet"`
3. Fallback:
   - → `"✓ Bereit"`

**Heartbeat-Timeout-Schwelle:** Nutzt `AufgabeRecoveryService.HeartbeatTimeoutMinutes` (5 Minuten)
