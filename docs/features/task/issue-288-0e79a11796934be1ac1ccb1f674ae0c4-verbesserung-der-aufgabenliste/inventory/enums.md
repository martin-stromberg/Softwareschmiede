# Enums

## `AufgabeStatus`

Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Erstellt, wartet auf Bearbeitung |
| `Gestartet` | Gestartet (Branch erstellt, CLI läuft oder sollte laufen) — in der Aufgabenliste sichtbar |
| `Wartend` | Rate-Limit erreicht; wartet auf Wiederaufnahme — in der Aufgabenliste sichtbar |
| `Beendet` | Beendet (erfolgreich oder mit Fehler) |
| `Archiviert` | Archiviert, nicht mehr aktiv |

Erweiterung: `AufgabeStatusExtensions.AktivOderWartendStatus` (`src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`) ist die einzige Quelle der Wahrheit für "aktiv oder wartend" (genutzt vom `IstAktivOderWartendPredicate` in `AufgabeService`).

## `AufgabeAusfuehrungsStatus`

Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | KI-Ausführung noch nicht gestartet |
| `Aktiv` | KI-Ausführung aktiv bzw. nach App-Neustart wiederherzustellen |
| `Beendet` | KI-Ausführung beendet |

## `AufgabeLaufStatus`

Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

Feingranularer Laufzeit-Substatus während `Aufgabe.AktiveRunId` gesetzt ist (beobachtend, ohne Transitions-Validierung; unterscheidet "▶ Läuft" von "⏸ Wartet" im `KiAusfuehrungsStatusConverter`).

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | CLI läuft und hat kürzlich I/O verarbeitet |
| `WartetAufEingabe` | CLI läuft, erzeugt aber seit längerem keine Ausgabe (vermutlich wartet sie auf Eingabe) |

## `PluginType`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | SCM-/Git-Plugin (`IGitPlugin`) |
| `DevelopmentAutomation` | KI-Plugin (`IKiPlugin`) |
| `Ide` | IDE-Plugin (`IIdePlugin`) |

Wird in `MainWindowViewModel.ResolvePluginName` zur Auswahl der Plugin-Liste verwendet.

## `DateibrowserAnsichtsmodus`

Datei: `src/Softwareschmiede.App/ViewModels/DateibrowserAnsichtsmodus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Standard` | Vollständiger Arbeitsbaum des geklonten Repos |
| `Vergleich` | Nur geänderte Dateien, gruppiert nach Commits |

Für die Anforderung nur als **Namenskonvention** relevant (UI-Enum im `ViewModels`-Ordner); ein `AufgabenListenModus`-Enum existiert noch nicht.
