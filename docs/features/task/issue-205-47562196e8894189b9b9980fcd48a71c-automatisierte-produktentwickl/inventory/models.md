# Datenmodelle

## `Aufgabe`
**Datei:** `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

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
| `LokalerKlonPfad` | `string?` | **Kritisch für diesen Issue:** Lokaler Pfad des geklonten Repositories. Bei autonomen Aufgaben ist dieser Pfad beim Dialog-Aufruf noch `null` (wird erst beim Absenden gesetzt). |
| `GitArbeitsbereich` | `GitArbeitsbereich?` | NotMapped Property — Convenience-Zugriff auf `BranchName` + `LokalerKlonPfad` als ValueObject; `null`, solange kein Branch/Klon-Pfad gesetzt ist. |
| `AutonomKonfiguration` | `AutonomAufgabeKonfiguration?` | **Kritisch für Unterscheidung Autonom/Regulär:** Navigationseigenschaft zur Konfiguration der Autonomen Aufgabe. `null` für reguläre Aufgaben, nicht-`null` für autonome Aufgaben. |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |

**Relevanz für Issue:**
- `LokalerKlonPfad` ist bei Dialog-Initialisierung noch `null` → triggert die Fehlermeldung in Zeile 331 des ViewModels
- `AutonomKonfiguration` existiert noch nicht während des Dialogs → kann nicht als Unterscheidungskriterium für Autonom/Regulär verwendet werden

---

## `AutonomAufgabeKonfiguration`
**Datei:** `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Konfiguration |
| `AufgabeId` | `Guid` | ID der zugehörigen Aufgabe |
| `ProjektBranchName` | `string` | **Kritisch:** Name des dedizierten Projektbranches (z.B. "feature/autonom-test"). Dies ist der Branch-Name, der im Dialog eingegeben/angezeigt wird. |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter |
| `PermissionsJsonPfad` | `string` | Pfad zur permissions.json |
| `TokenBudget` | `int` | Token-Budget für die Gesamtaufgabe |
| `TokenBudgetErweitert` | `int?` | Optionales erweitertes Token-Budget |
| `LaufzeitLimitMinuten` | `int` | Nettozeit-Limit in Minuten |
| `RessourcenLimits` | `RessourcenLimits` | NotMapped Property — Convenience-Zugriff auf die drei Limit-Felder als ValueObject |
| `PersistenzModus` | `PersistenzModus` | Persistenz-Modus (Standard, SitzungZuruecksetzen) |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren? |
| `ArbeitsverzeichnisPfad` | `string` | Pfad zum Arbeitsverzeichnis der Autonomen Aufgabe |
| `ProjektleiterAgentId` | `string?` | ID des aktuell laufenden Projektleiter-Agenten |
| `SessionPauseUtc` | `DateTimeOffset?` | Zeitstempel der letzten Session-Pause wegen Budget-Limit |
| `AktiveUnteragenten` | `int?` | Anzahl aktuell aktiver Unteragenten |
| `Aufgabe` | `Aufgabe` | Navigationseigenschaft zur zugehörigen Aufgabe |
| `Unteragenten` | `List<UnteragentSpezifikation>` | Unteragenten dieser Autonomen Aufgabe |
| `Skills` | `List<SkillDefinition>` | Skills dieser Autonomen Aufgabe |

**Relevanz für Issue:**
- Wird erst am Ende von `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` (Zeile 59-70) erzeugt
- Enthält den finalen `ProjektBranchName`, der im Dialog eingegeben wurde
- Während des Dialogs existiert diese Konfiguration noch nicht

---

## `AutonomAufgabeInitialisierungsAnfrage`
**Datei:** `src/Softwareschmiede/Domain/ValueObjects/AutonomAufgabeInitialisierungsAnfrage.cs`

| Parameter | Typ | Beschreibung / Zweck |
|-----------|-----|----------------------|
| `ProjektBranchName` | `string` | **Kritisch:** Der Branch-Name, der im Dialog eingegeben und beim Submit übergeben wird. |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter (aus Dialog) |
| `ArbeitsverzeichnisPfad` | `string` | Absoluter Pfad zum Arbeitsverzeichnis (aus Dialog, kombiniert aus Basis-Verzeichnis und Aufgaben-ID) |
| `RessourcenLimits` | `RessourcenLimits` | Token-Budget und Laufzeitbegrenzung (aus Dialog) |
| `PersistenzModus` | `PersistenzModus` | Persistenz-Modus (aus Dialog) |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren? (aus Dialog) |
| `PermissionsQuelle` | `PermissionsJsonOption` | Quelle der permissions.json (aus Dialog, Default: Generieren) |

**Relevanz für Issue:**
- Wird von `AutonomAufgabeInitialisierungsDialogViewModel.BestaetigenAsync()` (Zeile 385-395) erzeugt
- `ProjektBranchName` wird aus `SelectedProjectBranch` befüllt, der entweder im Dialog ausgewählt oder neu eingegeben wurde
- Wird an `AutonomAufgabenInitialisierungsService.InitialisiereAsync()` übergeben (Zeile 397)

---

## `RessourcenLimits`
**Datei:** `src/Softwareschmiede/Domain/ValueObjects/RessourcenLimits.cs`

| Parameter | Typ | Beschreibung / Zweck |
|-----------|-----|----------------------|
| `TokenBudget` | `int` | Token-Budget für die Gesamtaufgabe |
| `TokenBudgetErweitert` | `int?` | Optionales erweitertes Token-Budget |
| `LaufzeitLimitMinuten` | `int` | Nettozeit-Limit in Minuten |

**Relevanz für Issue:**
- Value Object für Ressourcenlimits
- Im Dialog durch Token-Budget und Laufzeitbegrenzungs-Eingabefelder repräsentiert
- Wird in der `AutonomAufgabeInitialisierungsAnfrage` verwendet
