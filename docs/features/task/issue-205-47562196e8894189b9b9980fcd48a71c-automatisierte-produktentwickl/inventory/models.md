# Datenmodell

## `AutonomAufgabeKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration |
| `AufgabeId` | `Guid` | ID der zugehörigen Aufgabe |
| `ProjektBranchName` | `string` | Name des dedizierten Projektbranches |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter-Agent |
| `PermissionsJsonPfad` | `string` | Pfad zur permissions.json |
| `TokenBudget` | `int` | Token-Budget für die Gesamtaufgabe |
| `TokenBudgetErweitert` | `int?` | Optionales erweitertes Token-Budget |
| `LaufzeitLimitMinuten` | `int` | Nettozeit-Limit in Minuten |
| `RessourcenLimits` | `RessourcenLimits` (NotMapped) | Value-Object für Ressourcen-Limits (Convenience-Zugriff) |
| `PersistenzModus` | `PersistenzModus` | Persistenz-Modus |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren? |
| `ArbeitsverzeichnisPfad` | `string` | Pfad zum Arbeitsverzeichnis der Autonomen Aufgabe |
| `ProjektleiterAgentId` | `string?` | ID des aktuell laufenden Projektleiter-Agenten |
| `SessionPauseUtc` | `DateTimeOffset?` | Zeitstempel der letzten Session-Pause wegen Budget-Limit |
| `AktiveUnteragenten` | `int?` | Anzahl aktuell aktiver Unteragenten |
| `Aufgabe` | `Aufgabe` (Navigation) | Navigationseigenschaft zur zugehörigen Aufgabe |
| `Unteragenten` | `List<UnteragentSpezifikation>` | Unteragenten dieser Autonomen Aufgabe |
| `Skills` | `List<SkillDefinition>` | Skills dieser Autonomen Aufgabe |

**Beobachtung**: Das Feld `ExplizitGestoppt` (für explizites Stoppen durch Nutzer) ist **nicht vorhanden**. Auch `LetzterStartStatusUtc` existiert nicht. Diese sind laut Anforderung geplant, aber noch nicht implementiert.

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Status der KI-Ausführung |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `GitArbeitsbereich` | `GitArbeitsbereich?` (NotMapped) | Value-Object für Git-Arbeitsbereich (Convenience-Zugriff) |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum der Aufgabe (null wenn noch nicht abgeschlossen) |
| `AktiveRunId` | `string?` | Optionale ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats einer Ausführung |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Zeitstempel des letzten echten CLI-Prozessstarts |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus der aktiven CLI-Ausführung |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für den nächsten Prompt |
| `AutonomKonfiguration` | `AutonomAufgabeKonfiguration?` (Navigation) | Konfiguration der Autonomen Aufgabe (null für reguläre Aufgaben) |
| `Projekt` | `Projekt` (Navigation) | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` (Navigation) | Navigationseigenschaft zum verknüpften Git-Repository |
