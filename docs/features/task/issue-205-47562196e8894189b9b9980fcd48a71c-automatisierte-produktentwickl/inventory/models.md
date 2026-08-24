# Datenmodelle

## `AutonomAufgabeKonfiguration`

Datei: Nicht direkt untersucht (gehört zu Application-Layer), wird in AutonomAufgabeDetailViewModel verwendet.

Bekannte Properties (aus AutonomAufgabeDetailView.xaml):
- `ProjektBranchName`: string — Branch der Aufgabe
- `TokenBudget`: Numerischer Wert — Token-Limit für Agent-Lauf
- `LaufzeitLimitMinuten`: Numerischer Wert — Maximale Laufzeit in Minuten
- `PersistenzModus`: string/enum — Persistenz-Verhalten
- `ArbeitsverzeichnisPfad`: string — Pfad zum Arbeitsverzeichnis

**Status:** Keine Breaking Changes notwendig. AutonomAufgabeKonfiguration wird von TaskDetailViewModel als Property über AutonomAufgabeDetailViewModel injiziert sein.

## `Aufgabe`

Datei: `src/Softwareschmiede.Domain/Entities/Aufgabe.cs` (nicht vollständig untersucht)

Entity aus der Domain-Layer. Relevante Properties:
- `Id`: Guid
- `Titel`: string
- `Status`: AufgabeStatus (Neu, Gestartet, Wartend, Beendet, Archiviert)
- `AusfuehrungsStatus`: AufgabeAusfuehrungsStatus — zeigt CLI-Status an
- `LokalerKlonPfad`: string? — Pfad zum geklonten Repository
- `BranchName`: string? — Git-Branch-Name
- `IstAutonom()`: Method — prüft, ob Aufgabe eine Autonome Aufgabe ist

**Status:** Keine Änderungen notwendig. Wird weiterhin verwendet, um festzustellen, ob `ShowAutomatisierungPanel` true sein soll.

## `UnteragentSpezifikation`

Datei: Nicht direkt untersucht (wird in AutonomAufgabeDetailView.xaml ListBox angezeigt)

Bekannte Properties (aus DataGrid-Columns in AutonomAufgabeDetailView.xaml):
- `ExterneAgentId`: string — Eindeutige Agent-ID
- `Scope`: string — Bereich des Agenten
- `Status`: string — Aktueller Status
- `ErzeugungsDatum`: DateTime — Erstellungszeitpunkt
- `AbschlussDatum`: DateTime? — Abschluss-Zeitpunkt

**Status:** Keine Änderungen notwendig. ObservableCollection wird in AutonomAufgabeDetailViewModel verwundet.

## `SkillDefinition`

Datei: Nicht direkt untersucht (wird in AutonomAufgabeDetailView.xaml ListBox angezeigt)

Bekannte Properties (aus ListBox-DisplayMemberPath in AutonomAufgabeDetailView.xaml):
- `Name`: string — Name des Skills

**Status:** Keine Änderungen notwendig. ObservableCollection wird in AutonomAufgabeDetailViewModel verwendet.
