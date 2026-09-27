# Enums — Konsolentestfenster Schrittmodus

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 578–586)

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess aktiv — bei `TerminalReplaySession`: Initialzustand und Zustand nach Wiedergabe-Ende/`Dispose` |
| `Laeuft` | CLI läuft mit kürzlicher Aktivität — bei Replay: gesetzt ab `WiedergabeStarten` bis `RaiseExited`/`Dispose` (auch im Pausiert-Zustand) |
| `WartetAufEingabe` | CLI läuft, wartet vermutlich auf Eingabe — wird von `TerminalReplaySession` nie gesetzt |

Befund für den Schrittmodus: Es gibt keinen eigenen Statuswert für schrittweise Wiedergabe — die Frage „`Inaktiv` vs. `Laeuft` beim Schreiten ohne gestartete Wiedergabe" ist eine offene Anforderungsfrage (requirement.md, Offene Frage 7). `CliRuntimeStatus` wird von `KonsolenTestViewModel` nicht direkt gelesen; die UI-Zustände (`IstWiedergabeAktiv`/`IstPausiert`/`_wiedergabeBeendet`) sind separat geführt.

## `AnsiSequenceParser.State` (private)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (Z. 12)

| Wert | Bedeutung |
|------|-----------|
| `Normal` | Text/Bytes sammeln |
| `Escape` | Nach `0x1B` — Verteiler auf CSI/OSC/DCS-überspringende Sequenzen, `ESC 7`/`ESC 8` (Cursor Save/Restore), `ESC c` (`TerminalResetEvent`), Charset-Selektoren |
| `Csi` / `CsiQuestion` | Parameterpuffer sammeln bis Final-Byte `0x40–0x7E` (bzw. `?`-Präfix-Variante) |
| `Osc` | String-Sequenz (OSC/DCS/SOS/PM/APC) bis BEL oder ST überspringen |
| `EscapeCharset` | Ein Folgezeichen einer Charset-Sequenz verwerfen |

Relevanz: Der Parser-Zustand ist chunk-übergreifend wirksam — genau der Zustand, den `AnsiSequenceParser.Reset()` (Z. 182–188) zurücksetzt und der bei einem Rückwärtsschritt der Replay-Session inkonsistent zum verkürzten Präfix stehen würde.
