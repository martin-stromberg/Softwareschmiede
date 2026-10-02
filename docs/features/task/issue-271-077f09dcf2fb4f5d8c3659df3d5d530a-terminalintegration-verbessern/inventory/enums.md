# Bestandsaufnahme: Enums (Terminalintegration)

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Zeilen 400–408)

Laufzeitstatus einer aktiven CLI-Sitzung; wird von `CliRuntimeStatusEvaluator.Determine` abgeleitet und via `PseudoConsoleSession.RuntimeStatusChanged` publiziert. Abgebildet auf `AufgabeLaufStatus` durch `CliProcessManager.OnRuntimeStatusChanged`.

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess aktiv |
| `Laeuft` | CLI läuft, kürzlich Ausgabe oder Eingabe |
| `WartetAufEingabe` | CLI läuft, aber seit `waitingThreshold` (4 s) keine I/O-Aktivität |

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Zeilen 731–739)

Grober Lebenszyklus-Status eines CLI-Prozesses; Payload von `KiAusfuehrungsService.CliProcessStatusChanged`.

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft |
| `Gestoppt` | Prozess wurde gestoppt oder regulär beendet (auch ExitCode 0) |
| `Fehler` | Prozess mit `ExitCode != 0` beendet (nur wenn nicht `AbsichtlichGestoppt`) |

## `AufgabeLaufStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

Persistierter Laufzeit-Substatus einer Aufgabe (`Aufgabe.LaufStatus`) — bewusst eigenständiges Domain-Enum ohne Infrastructure-Abhängigkeit, kein `Inaktiv`-Wert.

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | CLI läuft und hat kürzlich Ausgabe/Eingabe verarbeitet |
| `WartetAufEingabe` | CLI läuft, wartet vermutlich auf Benutzereingabe |

## `AnsiSequenceParser.State` (private)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (Zeile 10)

Interne Zustandsmaschine des Parsers — relevant, weil sie den Umfang der VT-Emulation definiert.

| Wert | Bedeutung |
|------|-----------|
| `Normal` | Text-Akkumulation in `_textBuffer` |
| `Escape` | Nach `ESC` empfangen (nur `[` und `]` werden weiterverfolgt) |
| `Csi` | Innerhalb `ESC [ …` |
| `CsiQuestion` | Innerhalb `ESC [ ? …` (nur `?25` ausgewertet) |
| `Osc` | Innerhalb `ESC ] …` (bis `BEL` oder `ESC \` überlesen) |

## `PluginType` (relevanter Wert)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

Alle fünf `CliKiPluginBase`-Ableitungen melden `PluginType.DevelopmentAutomation` — das ist der Plugin-Typ, den `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync` auflöst und der den interaktiven CLI-Startpfad betrifft.

## Nicht vorhanden
Kein Enum/Flags-Typ für Anbieterfähigkeiten (`TerminalProviderCapabilities` o. ä.) — kein Plugin deklariert, ob seine CLI PTY/ConPTY benötigt oder unterstützt.
