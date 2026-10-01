# Enums — Replay-Geometrie

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 578–586)

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess aktiv |
| `Laeuft` | CLI läuft, hat kürzlich Ausgabe/Eingabe verarbeitet |
| `WartetAufEingabe` | CLI läuft, erzeugt länger keine Ausgabe (vermutlich Warte auf Eingabe) |

Für die Anforderung nur peripher relevant (`TerminalReplaySession` nutzt nur `Inaktiv`/`Laeuft`); keine Geometrie-bezogenen Werte.

## `AnsiSequenceParser.State` (privat)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (Z. 12)

| Wert | Bedeutung |
|------|-----------|
| `Normal` | Textmodus |
| `Escape` | Nach ESC |
| `Csi` | In CSI-Sequenz |
| `CsiQuestion` | In `CSI ?`-Sequenz (DEC-private) |
| `Osc` | In OSC-Sequenz |
| `EscapeCharset` | Charset-Selektion |

Interner Zustand, wird von `Reset()` zurückgesetzt — für die Geometrie-Thematik selbst nicht relevant (der Parser ist geometrie-agnostisch).

## `ScrollBarVisibility` (WPF, Framework-Enum)
Namespace: `System.Windows.Controls` — kein Projekt-Enum, aber der direkte Eingriffspunkt in `KonsolenTestDialog.xaml`/`TaskDetailView.xaml`.

| Wert | Verwendung im Bestand |
|------|------------------------|
| `Disabled` | `KonsolenTestDialog.xaml` Z. 138 (Horizontal, `ReplayTerminal`); `TaskDetailView.xaml` Z. 490 (Horizontal, `TerminalConsole`); diverse andere ScrollViewer |
| `Auto` | `KonsolenTestDialog.xaml` Z. 137 + `TaskDetailView.xaml` Z. 489 (vertikal) — Zielwert für die horizontale Scrollbar der Replay-Ansicht laut Anforderung |
| `Visible`/`Hidden` | derzeit nicht an den Terminal-ScrollViewern im Einsatz |
