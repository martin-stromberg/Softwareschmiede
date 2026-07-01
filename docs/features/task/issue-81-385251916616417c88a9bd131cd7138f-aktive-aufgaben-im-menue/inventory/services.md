# Bestandsaufnahme: Services

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync(Guid projektId, CancellationToken ct)` | public | Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück, sortiert nach ErstellungsDatum (absteigend) |
| `GetArchiviertByProjektAsync(Guid projektId, CancellationToken ct)` | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync(CancellationToken ct)` | public | **[RELEVANT]** Gibt die Anzahl aktiver (Gestartet) und wartender (Wartend) Aufgaben als Tupel zurück — projektübergreifend |
| `GetByIdAsync(Guid id, CancellationToken ct)` | public | Gibt eine Aufgabe anhand ihrer ID zurück |
| `GetDetailAsync(Guid id, CancellationToken ct)` | public | Gibt eine Aufgabe mit IssueReferenz, Projekt, GitRepository und Protokolleinträgen zurück |
| `GetLatestDiffResultIdAsync(Guid aufgabeId, CancellationToken ct)` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Aufgabe zurück |
| `GetLatestDiffResultIdForFileAsync(Guid aufgabeId, string relativePath, CancellationToken ct)` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses einer bestimmten Datei zurück |
| `CreateAsync(Guid projektId, string titel, string? anforderungsBeschreibung, Guid? gitRepositoryId, CancellationToken ct)` | public | Erstellt eine neue Aufgabe mit Status `Neu` |
| `CreateFromIssueAsync(Guid projektId, Issue issue, Guid? gitRepositoryId, CancellationToken ct)` | public | Erstellt eine neue Aufgabe aus einem Issue mit IssueReferenz |
| `UpdateAsync(Guid id, string titel, string? anforderungsBeschreibung, string? kiPluginPrefix, CancellationToken ct)` | public | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix einer Aufgabe |
| `UpdateIssueReferenzAsync(Guid id, Issue? issue, CancellationToken ct)` | public | Setzt oder aktualisiert die IssueReferenz einer Aufgabe |
| `DeleteAsync(Guid id, CancellationToken ct)` | public | Löscht eine Aufgabe (nur wenn Status nicht Gestartet/Wartend) |
| `VerwerfenAsync(Guid id, VerwerfenAktion aktion, CancellationToken ct)` | public | Verwirft eine neue Aufgabe durch Archivieren oder Löschen |
| `ArchivierenAsync(Guid id, CancellationToken ct)` | public | Archiviert eine beendete Aufgabe |
| `StartenAsync(Guid id, string branchName, string lokalerKlonPfad, CancellationToken ct)` | public | Startet eine Aufgabe: Status → Gestartet, setzt Branch und Arbeitsverzeichnis |
| `SavePromptVorschlagAsync(Guid id, string? prompt, DateTimeOffset? ausfuehrenAbUtc, CancellationToken ct)` | public | Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync(Guid id, CancellationToken ct)` | public | Entfernt den gespeicherten Vorschlagsprompt und Ausführungszeitpunkt |
| `AbschliessenAsync(Guid id, CancellationToken ct)` | public | Schließt eine Aufgabe ab: Status → Beendet, setzt AbschlussDatum, leert Branch und KlonPfad |
| `SetStatusAsync(Guid id, AufgabeStatus newStatus, CancellationToken ct)` | public | Setzt den Status mit Validierung der erlaubten Übergänge |
| `StatusSetzenAsync(Guid id, AufgabeStatus status, CancellationToken ct)` | public | Setzt den Status generisch ohne Transitions-Validierung |
| `UpdateHeartbeatAsync(Guid id, CancellationToken ct)` | public | Aktualisiert `LastHeartbeatUtc` der Aufgabe auf aktuellen UTC-Zeitpunkt |
| `GetHeartbeatAgeMinutesAsync(Guid id, CancellationToken ct)` | public | Gibt die Minuten seit dem letzten Heartbeat zurück (null wenn kein Heartbeat gesetzt) |
| `NormalizeRelativePathForLookup(string relativePath)` | private static | Normalisiert einen Dateipfad für Lookup-Operationen |
| `ValidateStatusTransition(AufgabeStatus current, AufgabeStatus next)` | private static | Validiert erlaubte Status-Übergänge |

**Hinweise:**
- **FEHLEND:** Methode `GetAktiveAufgabenAsync()` aus der Anforderung ist nicht implementiert
  - Diese Methode soll alle Aufgaben mit Status `Gestartet` oder `Wartend` zurückgeben
  - Sortierung: nach `LastHeartbeatUtc` oder `ErstellungsDatum` absteigend
  - Optional mit Limit (z.B. 20 Aufgaben)
- `GetAktiveUndWartendeCountAsync()` liefert bereits die Anzahl der aktiven und wartenden Aufgaben
- Abhängigkeiten: `SoftwareschmiededDbContext`, `ILogger<AufgabeService>`
