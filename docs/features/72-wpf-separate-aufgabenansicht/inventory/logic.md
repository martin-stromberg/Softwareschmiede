# Logik (Services)

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync(projektId, ct)` | Public | Gibt alle nicht-archivierten Aufgaben eines Projekts zurück |
| `GetArchiviertByProjektAsync(projektId, ct)` | Public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync(ct)` | Public | Gibt Anzahl aktiver und wartender Aufgaben zurück |
| `GetByIdAsync(id, ct)` | Public | Ruft eine Aufgabe anhand ihrer ID ab (AsNoTracking) |
| `GetDetailAsync(id, ct)` | Public | Ruft eine Aufgabe mit Projekt, IssueReferenz, Repository und Protokoll ab |
| `GetLatestDiffResultIdAsync(aufgabeId, ct)` | Public | Ruft die ID des neuesten Diff-Ergebnisses ab |
| `GetLatestDiffResultIdForFileAsync(aufgabeId, relativePath, ct)` | Public | Ruft die neueste datei-spezifische Diff-Result-ID ab |
| `CreateAsync(projektId, titel, anforderungsBeschreibung, gitRepositoryId, ct)` | Public | **Erstellt eine neue Aufgabe mit Status `Neu`** |
| `CreateFromIssueAsync(projektId, issue, gitRepositoryId, ct)` | Public | Erstellt Aufgabe aus einem Git-Issue |
| `UpdateAsync(id, titel, anforderungsBeschreibung, kiPluginPrefix, ct)` | Public | **Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix** |
| `DeleteAsync(id, ct)` | Public | Löscht eine Aufgabe (nur wenn nicht aktiv) |
| `VerwerfenAsync(id, aktion, ct)` | Public | Verwirft neue Aufgabe (Archivieren oder Löschen) |
| `ArchivierenAsync(id, ct)` | Public | Archiviert eine beendete Aufgabe |
| `StartenAsync(id, branchName, lokalerKlonPfad, ct)` | Public | Setzt Status auf `ArbeitsverzeichnisEingerichtet`, speichert Branch und Pfad |
| `SavePromptVorschlagAsync(id, prompt, ausfuehrenAbUtc, ct)` | Public | Speichert Prompt-Vorschlag und Ausführungszeit |
| `ClearPromptVorschlagAsync(id, ct)` | Public | Löscht Prompt-Vorschlag und Ausführungszeit |
| `AbschliessenAsync(id, ct)` | Public | Setzt Status auf `Beendet`, speichert Abschlussdatum, leert Branch und Pfad |
| `SetStatusAsync(id, newStatus, ct)` | Public | Setzt Status mit Validierung der erlaubten Übergänge |
| `StatusSetzenAsync(id, status, ct)` | Public | Setzt Status generisch (ohne Transitions-Validierung) |
| `UpdateHeartbeatAsync(id, ct)` | Public | Aktualisiert `LastHeartbeatUtc` |
| `GetHeartbeatAgeMinutesAsync(id, ct)` | Public | Gibt Alter des letzten Heartbeats in Minuten zurück |
| `ValidateStatusTransition(current, next)` | Private | Validiert erlaubte Status-Übergänge |

**Besonderheiten:**
- `CreateAsync()` wird bereits von `ProjectDetailViewModel.AufgabeErstellenAsync()` aufgerufen (Zeile 233-238) für Neuanlage
- `UpdateAsync()` wird von `TaskDetailViewModel.SpeichernAsync()` aufgerufen (Zeile 456)
- Status-Übergänge sind strikt validiert: Neu → ArbeitsverzeichnisEingerichtet → Gestartet → InArbeit → {Beendet, Wartend}
- Archiviert ist von jedem Status erreichbar

## `ProjektService`
Datei: `src/Softwareschmiede/Application/Services/ProjektService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllAsync(ct)` | Public | Gibt alle Projekte sortiert nach Name zurück |
| `GetByIdAsync(id, ct)` | Public | Ruft ein Projekt ab |
| `GetDetailAsync(id, ct)` | Public | Ruft Projekt mit Repositories und Aufgaben ab |
| `CreateAsync(name, beschreibung, ct)` | Public | Erstellt neues Projekt mit Status `Aktiv` |
| `UpdateAsync(id, name, beschreibung, ct)` | Public | Aktualisiert Projekt-Metadaten |
| `ArchivierenAsync(id, ct)` | Public | Archiviert ein Projekt |
| `DeleteAsync(id, ct)` | Public | Löscht Projekt und alle Abhängigkeiten |
| `AddRepositoryAsync(projektId, pluginTyp, repositoryUrl, repositoryName, ct)` | Public | Fügt Git-Repository hinzu |
| `AddRepositoryAsync(projektId, pluginTyp, fieldValues, ct)` | Public | Fügt Repository mit Plugin-spezifischen Feldern hinzu |
| `GetAllRepositoriesAsync(ct)` | Public | Gibt alle bekannten Repositories zurück |
| `RemoveRepositoryAsync(repositoryId, ct)` | Public | Entfernt Repository aus Projekt |
| `SaveRepositoryStartKonfigurationAsync(repositoryId, startScriptRelativePath, aktiv, ct)` | Public | Speichert Start-Konfiguration |
| `GetRepositoryStartKonfigurationAsync(repositoryId, ct)` | Public | Ruft Start-Konfiguration ab |

**Besonderheiten:**
- `GetDetailAsync()` wird von `ProjectDetailViewModel.LadenAsync()` aufgerufen (Zeile 198)
- Wird hauptsächlich für Projekt-CRUD und Repository-Verwaltung genutzt (nicht direkt relevant für Aufgaben-Navigation)
