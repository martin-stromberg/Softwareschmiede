# Logik — Bestandsaufnahme

## `ProjektService`
Datei: `src/Softwareschmiede/Application/Services/ProjektService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllAsync` | public | Gibt alle Projekte zurück |
| `GetByIdAsync` | public | Gibt ein Projekt anhand seiner ID zurück |
| `GetDetailAsync` | public | Gibt ein Projekt mit Repositories und Aufgaben zurück |
| `CreateAsync` | public | Erstellt ein neues Projekt |
| `UpdateAsync` | public | Aktualisiert Name und Beschreibung eines Projekts |
| `ArchivierenAsync` | public | Archiviert ein Projekt |
| `DeleteAsync` | public | Löscht ein Projekt inkl. aller verknüpften Daten |
| `AddRepositoryAsync` | public | Fügt ein Git-Repository zu einem Projekt hinzu |
| `RemoveRepositoryAsync` | public | Entfernt ein Git-Repository aus einem Projekt |
| `SaveRepositoryStartKonfigurationAsync` | public | Speichert die Startkonfiguration für ein Repository |
| `GetRepositoryStartKonfigurationAsync` | public | Liefert die Startkonfiguration eines Repositories |

Abonnierte Events: Keine
Publizierte Events: Keine

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync` | public | Gibt alle aktiven Aufgaben eines Projekts zurück |
| `GetArchiviertByProjektAsync` | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync` | public | Gibt die Anzahl aktiver und wartender Aufgaben zurück |
| `GetByIdAsync` | public | Gibt eine Aufgabe anhand ihrer ID zurück |
| `GetDetailAsync` | public | Gibt eine Aufgabe mit IssueReferenz und Protokolleinträgen zurück |
| `GetLatestDiffResultIdAsync` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Aufgabe zurück |
| `GetLatestDiffResultIdForFileAsync` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Datei innerhalb einer Aufgabe zurück |
| `CreateAsync` | public | Erstellt eine neue Aufgabe mit Status Neu |
| `CreateFromIssueAsync` | public | Erstellt eine neue Aufgabe aus einem Issue |
| `UpdateAsync` | public | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix einer Aufgabe |
| `DeleteAsync` | public | Löscht eine Aufgabe |
| `VerwerfenAsync` | public | Verwirft eine Aufgabe im Status Neu durch Archivieren oder Löschen |
| `ArchivierenAsync` | public | Archiviert eine Aufgabe (nur für Status Beendet) |
| `StartenAsync` | public | Startet eine Aufgabe: Status → ArbeitsverzeichnisEingerichtet |
| `SavePromptVorschlagAsync` | public | Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync` | public | Entfernt den gespeicherten Vorschlagsprompt |
| `AbschliessenAsync` | public | Schließt eine Aufgabe ab: Status → Beendet |
| `SetStatusAsync` | public | Setzt den Status einer Aufgabe mit Validierung der erlaubten Übergänge |
| `StatusSetzenAsync` | public | Setzt den Status einer Aufgabe generisch (ohne Transitions-Validierung) |
| `UpdateHeartbeatAsync` | public | Aktualisiert LastHeartbeatUtc der Aufgabe |
| `GetHeartbeatAgeMinutesAsync` | public | Gibt die Minuten seit dem letzten Heartbeat zurück |

Abonnierte Events: Keine
Publizierte Events: Keine
