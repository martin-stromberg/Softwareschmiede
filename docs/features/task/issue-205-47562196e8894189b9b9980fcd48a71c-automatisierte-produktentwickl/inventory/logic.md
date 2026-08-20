# Logik-Services — Bestandsaufnahme

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

**Status der Anforderung:** Service existiert, aber neue Methoden sind nicht implementiert.

### Existierende Methoden:
| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync` | public | Gibt alle aktiven Aufgaben eines Projekts zurück. |
| `GetArchiviertByProjektAsync` | public | Gibt alle archivierten Aufgaben eines Projekts zurück. |
| `GetAktiveUndWartendeCountAsync` | public | Gibt Anzahl aktiver und wartender Aufgaben zurück. |
| `GetByIdAsync` | public | Gibt eine Aufgabe anhand ihrer ID zurück. |
| `GetDetailAsync` | public | Gibt eine Aufgabe mit Details zurück. |
| `GetByAlertSourceKeyAsync` | public | Gibt eine Aufgabe anhand des Alert-SourceKeys zurück. |
| `GetLatestDiffResultIdAsync` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses zurück. |
| `GetLatestDiffResultIdForFileAsync` | public | Gibt die dateispezifische DiffResult-ID zurück. |
| `CreateAsync` | public | Erstellt eine neue Aufgabe mit Status `Neu`. |
| `CreateFromIssueAsync` | public | Erstellt eine neue Aufgabe aus einem Issue. |
| `CreateFromAlertAsync` | public | Erstellt eine neue Aufgabe aus einem Alert. |
| `UpdateAsync` | public | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix. |
| `UpdateIssueReferenzAsync` | public | Setzt die IssueReferenz einer Aufgabe. |
| `TryAssignIssueReferenzIfNoneAsync` | public | Weist IssueReferenz zu, falls noch keine existiert. |
| `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync` | public | Weist IssueReferenz zu und aktualisiert optional die Beschreibung. |
| `DeleteAsync` | public | Löscht eine Aufgabe. |
| `VerwerfenAsync` | public | Verwirft eine Aufgabe im Status Neu. |
| `ArchivierenAsync` | public | Archiviert eine beendete Aufgabe. |
| `StartenAsync` | public | Startet eine Aufgabe: Status → Gestartet. |
| `SavePromptVorschlagAsync` | public | Speichert einen Vorschlagsprompt. |
| `ClearPromptVorschlagAsync` | public | Entfernt den gespeicherten Vorschlagsprompt. |
| `AbschliessenAsync` | public | Schließt eine Aufgabe ab: Status → Beendet. |
| `SetStatusAsync` | public | Setzt den Status mit Validierung der Übergänge. |
| `StatusSetzenAsync` | public | Setzt den Status ohne Transitions-Validierung. |
| `StartZuruecksetzenAsync` | public | Setzt fehlgeschlagene Startvorbereitung zurück. |
| `UpdateHeartbeatAsync` | public | Aktualisiert LastHeartbeatUtc. |
| `AusfuehrungAktivSetzenAsync` | public | Setzt KI-Ausführungsstatus auf aktiv. |
| `AktivenLaufSetzenAsync` | public | Markiert eine Aufgabe als aktiv ausgeführt. |
| `AktivenLaufBeendenAsync` | public | Markiert eine Aufgabe als nicht mehr aktiv ausgeführt. |
| `AktualisiereLaufStatusAsync` | public | Aktualisiert den Laufzeit-Substatus. |
| `GetHeartbeatAgeMinutesAsync` | public | Gibt die Minuten seit dem letzten Heartbeat zurück. |
| `GetAktiveAufgabenAsync` | public | Gibt alle aktiven Aufgaben sortiert nach letztem CLI-Start zurück. |
| `CanCompleteTaskAsync` | public | Validiert, ob eine Aufgabe beendet werden kann. |

### Abhängigkeiten:
- `SoftwareschmiededDbContext` — Datenbankzugriff
- `ILogger<AufgabeService>` — Logging
- `TodoService` — To-Do-Verwaltung

### Fehlende Methoden (aus Anforderung):
- `async Task<AutonomAufgabeKonfiguration> ErzeugeAutonomAufgabeAsync(Aufgabe aufgabe, string initialprompt, CancellationToken ct = default)` — Wrapper für Initialisierung einer Autonomen Aufgabe

---

## `AutonomAufgabenInitialisierungsService`
**Status der Anforderung:** Service ist nicht implementiert.

Neue Service-Klasse erforderlich in: `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`

### Erforderliche Methoden (gemäß Anforderung):
| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InitialisiereAsync` | public | Erstellt das Arbeitsverzeichnis, erzeugt den Repository-Klon, initialisiert state.json |
| `ErstelleArbeitsverzeichnisStrukturAsync` | public | Erstellt die Verzeichnisstruktur (plan.md, progress.md, state.json, governance.md, etc.) |

---

## `ProjektleiterAgentService`
**Status der Anforderung:** Service ist nicht implementiert.

Neue Service-Klasse erforderlich in: `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`

### Erforderliche Methoden (gemäß Anforderung):
| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StarteAgenAsync` | public | Startet den Projektleiter-Agenten mit InitialPrompt |
| `SteuereUnteragentAsync` | public | Erzeugt und konfiguriert einen Unteragenten |
| `IntegriereErgebnisseAsync` | public | Integriert Unteragenten-Ergebnisse in plan.md, progress.md, state.json |

---

## `UnteragentGovernanceService`
**Status der Anforderung:** Service ist nicht implementiert.

Neue Service-Klasse erforderlich in: `src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs`

### Erforderliche Methoden (gemäß Anforderung):
| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `VerifiziereBerechtigung` | public | Validiert, dass ein Unteragent nur in seinem eigenen Bereich arbeitet |
| `ValidiereFehlerBedingungAsync` | public | Prüft auf Abbruchbedingungen (Tokenlimit, Rechtsverletzung, Laufzeitüberschreitung) |

---

## `SessionManagementService`
**Status der Anforderung:** Service ist nicht implementiert.

Neue Service-Klasse erforderlich in: `src/Softwareschmiede/Application/Services/SessionManagementService.cs`

### Erforderliche Methoden (gemäß Anforderung):
| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PauseAufgabeBeiBudgetLimitAsync` | public | Pausiert die Aufgabe und speichert SessionPauseUtc |
| `SetzeFortAsync` | public | Setzt die Aufgabe nach Pause fort und sendet "weitermachen"-Prompt |
| `PruefeAusfuehrungAsync` | public | Prüft mittels Heartbeat, ob die Ausführung unterbrochen wurde |
