# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des übergeordneten Projekts |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories |
| `GitRepository` | `GitRepository?` | **Navigation-Property zum verknüpften Repository** (verwendet in `InitialisiereAsync` zur Auflösung der Plugin-Information via `GitRepository?.PluginTyp`) |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für KI-Agent |
| `Status` | `AufgabeStatus` | Aktueller Status (`Neu`, `Gestartet`, `Beendet`, `Archiviert`) |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Status der KI-Ausführung |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `KiPluginPrefix` | `string?` | Prefix des verwendeten KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungszeitpunkt |
| `AbschlussDatum` | `DateTimeOffset?` | Abschluss-Zeitpunkt (null wenn noch nicht abgeschlossen) |
| `AutonomKonfiguration` | `AutonomAufgabeKonfiguration?` | **Navigation-Property zur Konfiguration autonomer Aufgaben** (null für reguläre Aufgaben) |

---

## `GitRepository`
Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID des Repositories |
| `ProjektId` | `Guid` | ID des übergeordneten Projekts |
| **`PluginTyp`** | `string` | **Plugin-Typ des Repositories (z. B. "GitHub", "GitLab", "LocalDirectory"). ZENTRAL FÜR DIE ANFORDERUNG: `AutonomAufgabenInitialisierungsService.InitialisiereAsync` liest diese Eigenschaft (Zeile 45: `aufgabe.GitRepository?.PluginTyp`) und übergibt sie an `ResolveSourceCodeManagementPluginAsync`.** |
| `RepositoryUrl` | `string` | URL des Repositories |
| `RepositoryName` | `string` | Name des Repositories |
| `Aktiv` | `bool` | Gibt an, ob das Repository aktiv ist (default: true) |
| `DefaultSourceBranchName` | `string?` | Optionaler Basis-Branch, von dem neue Feature-Branches abgezweigt werden (null = Remote-Standard-Branch) |
| `StartKonfiguration` | `RepositoryStartKonfiguration?` | Optionale Konfiguration für Repository-Startskripte |

---

## `AutonomAufgabeKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration |
| `AufgabeId` | `Guid` | ID der zugehörigen Aufgabe |
| `ProjektBranchName` | `string` | Name des dedizierten Projektbranches (wird von `InitialisiereAsync` an `ErstelleProjektbranchAsync` übergeben) |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter-Agenten |
| `PermissionsJsonPfad` | `string` | Pfad zur `permissions.json` |
| `TokenBudget` | `int` | Token-Budget für die Gesamtaufgabe |
| `TokenBudgetErweitert` | `int?` | Optionales erweitertes Token-Budget |
| `LaufzeitLimitMinuten` | `int` | Nettozeit-Limit in Minuten |
| `RessourcenLimits` | `RessourcenLimits` | Value Object für Zugriffskonvenience (nicht von EF Core gemappt) |
| `PersistenzModus` | `PersistenzModus` | Persistenz-Modus (`Standard`, `Speichern`, `Snapshot`) |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren? |
| `ArbeitsverzeichnisPfad` | `string` | Pfad zum Arbeitsverzeichnis der Autonomen Aufgabe |
| `ProjektleiterAgentId` | `string?` | ID des aktuell laufenden Projektleiter-Agenten |
| `SessionPauseUtc` | `DateTimeOffset?` | Zeitstempel der letzten Session-Pause wegen Budget-Limit |
| `AktiveUnteragenten` | `int?` | Anzahl aktuell aktiver Unteragenten |
| `Aufgabe` | `Aufgabe` | Navigation-Property zur zugehörigen Aufgabe |

---

## `AutonomAufgabeInitialisierungsAnfrage`
Datei: `src/Softwareschmiede/Domain/ValueObjects/AutonomAufgabeInitialisierungsAnfrage.cs`

Record (Eingabedaten für `InitialisiereAsync`):

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProjektBranchName` | `string` | Name des dedizierten Projektbranches (wird an `ErstelleProjektbranchAsync` übergeben) |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter (wird validiert: min. 10 Zeichen) |
| `ArbeitsverzeichnisPfad` | `string` | Absoluter Pfad zum Arbeitsverzeichnis (wird validiert: absolut, nicht relativ) |
| `RessourcenLimits` | `RessourcenLimits` | Token-Budget und Laufzeit-Limits (wird validiert: TokenBudget 1–5Mio., Laufzeit 60–1440 Min.) |
| `PersistenzModus` | `PersistenzModus` | Persistenz-Modus der Autonomen Aufgabe |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren? |
| `PermissionsQuelle` | `PermissionsJsonOption` | Quelle der `permissions.json` (default: `Generieren`) |

---

## `RessourcenLimits`
Datei: `src/Softwareschmiede/Domain/ValueObjects/RessourcenLimits.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TokenBudget` | `int` | Token-Budget (1–5.000.000) |
| `TokenBudgetErweitert` | `int?` | Optionales erweitertes Token-Budget |
| `LaufzeitLimitMinuten` | `int` | Laufzeit-Limit in Minuten (60–1440) |

