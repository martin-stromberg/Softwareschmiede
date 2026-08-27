# Enums

## `AufgabeStatus`
Datei: `src\Softwareschmiede\Domain\Enums\AufgabeStatus.cs`

Status einer Aufgabe im Entwicklungsprozess. Wird für reguläre und autonome Aufgaben verwendet.

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen) |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler) |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv |

---

## `AufgabeAusfuehrungsStatus`
Datei: (indiziert durch Verwendung in Aufgabe.cs)

Persistierter Status der KI-Ausführung einer Aufgabe.

**Bekannte Werte (aus Aufgabe.cs):**
- `NichtGestartet` – Aufgabe wurde noch nicht gestartet
- `Aktiv` – KI-Ausführung ist aktiv (wird gesetzt, wenn Agent startet)
- `Wartet` – KI-Ausführung wartet (z. B. auf Fortführung)
- `Beendet` – KI-Ausführung ist beendet

---

## `PersistenzModus`
Datei: `src\Softwareschmiede\Domain\Enums\PersistenzModus.cs`

Persistenz-Modus einer `AutonomAufgabeKonfiguration` für Session-Wiederaufnahmen.

| Wert | Bedeutung |
|------|-----------|
| `Standard` | Der Zustand wird beim Fortsetzen unverändert übernommen |
| `SitzungZuruecksetzen` | Die Session wird beim Fortsetzen zurückgesetzt |

---

## `AufgabeLaufStatus`
Datei: (indiziert durch Verwendung in Aufgabe.cs)

Laufzeit-Substatus der aktiven CLI-Ausführung (nur relevant, solange `AktiveRunId` gesetzt ist).

**Bekannte Werte:**
- `Läuft` – CLI-Prozess läuft aktiv
- `Wartet` – CLI-Prozess läuft, aber Agent wartet (z. B. auf User-Input)

**Verwendung:** Wird von `CliProcessManager` anhand des `PseudoConsoleSession.RuntimeStatusChanged`-Ereignisses aktualisiert, um die Seitenleisten-/Dashboard-Kachel (`KiAusfuehrungsStatusConverter`) zwischen "▶ Läuft" und "⏸ Wartet" unterscheiden zu können.

---

## `BenachrichtigungsModus`
Datei: (indiziert durch Verwendung in SettingsViewModel)

Benachrichtigungsmodus für die Anwendung. Wird in `SettingsViewModel.BenachrichtigungsModus` gespeichert.

**Bekannte Werte:**
- `Sound` – Benachrichtigungen mit Sound
- `PopUp` – PopUp-Benachrichtigungen
- `Stumm` – Keine Benachrichtigungen

**Persistierungs-Pattern:** Wird via `AppEinstellungService` gespeichert.
