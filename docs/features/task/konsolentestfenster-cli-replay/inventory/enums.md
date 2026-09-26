# Enums — Konsolentestfenster für CLI-Ausgabe-Replay

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 578–586)

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess aktiv |
| `Laeuft` | CLI läuft und hat kürzlich Ausgabe/Eingabe verarbeitet |
| `WartetAufEingabe` | Keine Ausgabe seit `WaitingThreshold` — vermutlich Warten auf Eingabe |

Wird von `CliRuntimeStatusEvaluator.Determine` (Z. 613–633) aus Prozesslauf + letzten I/O-Zeitpunkten abgeleitet; für eine Replay-Session ohne echten Prozess muss der Status sinnvoll gemappt werden (z. B. `Laeuft` während der Wiedergabe, `Inaktiv` am Ende).

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Z. 673–683)

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft |
| `Gestoppt` | Prozess wurde gestoppt |
| `Fehler` | Prozess mit Fehler beendet |

## `ProtokollTyp`
Datei: `src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs`

| Wert | Bedeutung |
|------|-----------|
| `Prompt` | An den KI-Agenten gesendeter Prompt |
| `KiAntwort` | Antwort des KI-Agenten |
| `StatusUebergang` | Statusübergang der Aufgabe |
| `TestErgebnis` | Ergebnis eines Testlaufs |
| `GitAktion` | Git-Aktion |
| `CliOutput` | Ausgabezeile eines eingebetteten CLI-Prozesses (Basis des `.raw`-Exports) |
| `RateLimit` | Erkannter Rate-Limit-Marker |
| `SystemMeldung` | Interne Systemmeldung |

## `TerminalProviderCapabilities`
Datei: `src/Softwareschmiede/Domain/Enums/TerminalProviderCapabilities.cs` — `[Flags]`

| Wert | Bedeutung |
|------|-----------|
| `None = 0` | Keine besonderen Terminal-Fähigkeiten |
| `SupportsPty = 1` | CLI kann in einer Pseudo Console laufen |
| `RequiresPty = 2` | CLI benötigt ein echtes Pseudo-Terminal |

## `TerminalBackendEmpfehlung`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionDiagnostics.cs` (Z. 15–24)

| Wert | Bedeutung |
|------|-----------|
| `Pty` | PTY-/ConPTY-Backend empfohlen |
| `Pipe` | Pipe-Fallback-Backend empfohlen |
| `Fehler` | Start nicht möglich (harter Fehler) |

## `AnsiSequenceParser.State` (private)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (Z. 12)

`Normal`, `Escape`, `Csi`, `CsiQuestion`, `Osc`, `EscapeCharset` — interne Zustandsmaschine des Parsers; relevant für die Reproduktion chunk-übergreifender Sequenzfehler (Zustand überlebt `Parse`-Aufrufe, wird via `Reset()` zurückgesetzt).
