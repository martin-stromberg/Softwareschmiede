# Datenmodellklassen

## `AutonomAufgabenOptions`
Datei: `src\Softwareschmiede\Application\Services\AutonomAufgabenOptions.cs`

Konfigurationsklasse für das Feature "Autonome Aufgaben" (Projektleiter-Agent), via Dependency Injection als `IOptions<AutonomAufgabenOptions>` verfügbar.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Enabled` | `bool` | Feature-Flag zum Aktivieren/Deaktivieren von Autonomen Aufgaben (default: `true`) |
| `DefaultTokenBudget` | `int` | Standardbudget für neue Autonome Aufgaben (default: 500000) |
| `DefaultRuntimeLimitMinutes` | `int` | Standard-Laufzeitlimit in Minuten (default: 480 = 8 Stunden) |
| `WorkingDirectoryBase` | `string` | Basis-Verzeichnis für Arbeitsverzeichnisse (default: `%APPDATA%\AutonomAufgaben`) |
| `HeartbeatTimeoutSeconds` | `int` | Timeout für Heartbeat-Unterbrechungserkennung (default: 300) |
| `MaxConcurrentUnteragenten` | `int` | Max. Anzahl gleichzeitiger Unteragenten pro Aufgabe (default: 5) |
| `SkillAutogenerationEnabled` | `bool` | Standard für automatische Skill-Generierung (default: `false`) |
| `MaxClones` | `int` | Max. Anzahl gleichzeitiger Repository-Klone pro Aufgabe (default: 3) |
| `MaxFeatureBranches` | `int` | Max. Anzahl gleichzeitiger Feature-Branches pro Aufgabe (default: 10) |

**Konfigurationssektion:** `AutonomAufgaben` in `appsettings.json`

---

## `AutonomAufgabeKonfiguration`
Datei: `src\Softwareschmiede\Domain\Entities\AutonomAufgabeKonfiguration.cs`

Entity für die Konfiguration einer einzelnen Autonomen Aufgabe unter Steuerung eines Projektleiter-Agenten.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration |
| `AufgabeId` | `Guid` | ID der zugehörigen `Aufgabe` (FK) |
| `ProjektBranchName` | `string` | Name des dedizierten Projektbranches |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter |
| `PermissionsJsonPfad` | `string` | Pfad zur `permissions.json`-Datei |
| `TokenBudget` | `int` | Token-Budget für die Gesamtaufgabe |
| `TokenBudgetErweitert` | `int?` | Optionales erweitertes Token-Budget |
| `LaufzeitLimitMinuten` | `int` | Nettozeit-Limit in Minuten |
| `RessourcenLimits` | `RessourcenLimits` (NotMapped) | Value-Object-Convenience für `TokenBudget`, `TokenBudgetErweitert`, `LaufzeitLimitMinuten` |
| `PersistenzModus` | `PersistenzModus` | Persistenz-Modus (Standard, SitzungZuruecksetzen) |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren? |
| `ArbeitsverzeichnisPfad` | `string` | Pfad zum Arbeitsverzeichnis der Autonomen Aufgabe |
| `ProjektleiterAgentId` | `string?` | ID des aktuell laufenden Projektleiter-Agenten |
| `SessionPauseUtc` | `DateTimeOffset?` | Zeitstempel der letzten Session-Pause wegen Budget-Limit |
| `AktiveUnteragenten` | `int?` | Anzahl aktuell aktiver Unteragenten |
| `ExplizitGestoppt` | `bool` | Flag: Wurde der Agent explizit vom Nutzer gestoppt? |
| `Aufgabe` | `Aufgabe` | Navigation Property zur zugehörigen Aufgabe |
| `Unteragenten` | `List<UnteragentSpezifikation>` | Liste der Unteragenten |
| `Skills` | `List<SkillDefinition>` | Liste der Skills |

---

## `Aufgabe`
Datei: `src\Softwareschmiede\Domain\Entities\Aufgabe.cs`

Hauptentity für eine Aufgabe im Entwicklungsprozess. Kann sowohl für "einfache Aufgaben" (nicht-autonom) als auch autonome Aufgaben verwendet werden.

**Relevante Eigenschaften für autonome Aufgaben:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `Status` | `AufgabeStatus` | Aktueller Status (Neu, Gestartet, Wartend, Beendet, Archiviert) |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Status der KI-Ausführung (NichtGestartet, Aktiv, Wartet, Beendet) |
| `AktiveRunId` | `string?` | Optional: Aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Optional: Zeitstempel des letzten Heartbeats |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus (Läuft, Wartet) |
| `AutonomKonfiguration` | `AutonomAufgabeKonfiguration?` | **Navigation Property: Ist null für nicht-autonome Aufgaben, gesetzt für autonome Aufgaben** |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `BasisBranchName` | `string?` | Ursprünglicher Start-Branch |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des KI-Plugins |

---

## `AppEinstellung`
Datei: (implizit verwendet durch `AppEinstellungService`)

Entity zum persistenten Speichern von Anwendungseinstellungen als Key-Value-Paare in der Datenbank.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID |
| `Schluessel` | `string` | Eindeutiger Schlüssel der Einstellung |
| `Wert` | `string?` | Wert als String |
| `AktualisiertAm` | `DateTimeOffset` | Letztes Aktualisierungsdatum |

**Bekannte Schlüssel in `AppEinstellungService`:**
- `window.position.x`, `window.position.y`, `window.size.width`, `window.size.height`
- `ui.designmode.name`
- `ki.plugin.default`, `scm.plugin.default`
- `logging.level`
- `plugins.ide.order`
