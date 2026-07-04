# Logikklassen

## `TerminalControl`
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

WPF-Control, das eine `PseudoConsoleSession` rendert und Tastatureingaben weiterleitet.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TerminalControl()` | public | Konstruktor; registriert Unloaded-Event-Handler |
| `OnSessionChanged(PseudoConsoleSession?)` | private | Wird aufgerufen, wenn Session-Property sich ändert; cancelt alte ReadLoop, startet neue |
| `ReadLoopAsync(PseudoConsoleSession, TerminalBuffer, CancellationToken)` | private async | Kontinuierliche Leseschleife aus session.OutputStream; parsed Bytes, wendet Events auf Buffer an, triggert Neuzeichnung |
| `OnRender(DrawingContext)` | protected | Zeichnet den TerminalBuffer-Inhalt (Zellen, Farben, Cursor) |
| `OnPreviewKeyDown(KeyEventArgs)` | protected | Kodiert Tastatureingaben als VT100-Sequenzen, schreibt in session.InputStream |
| `OnTextInput(TextCompositionEventArgs)` | protected | Kodiert Texteingaben als UTF-8-Bytes, schreibt in session.InputStream |
| `WriteToInputStream(byte[])` | private | Schreibt Bytes in session.InputStream; loggt Fehler |
| `OnMouseDown(MouseButtonEventArgs)` | protected | Setzt Fokus auf das Control |
| `OnRenderSizeChanged(SizeChangedInfo)` | protected | Bei Größenänderung: Buffer und Pseudo Console resizen |
| `MeasureCellSize()` | private | Misst Zellgröße basierend auf Consolas-Schriftart |
| `CalculateCols()` und `CalculateRows()` | private | Berechnet Spalten/Zeilen basierend auf ActualWidth/Height |

**Abonnierte Events:**
- `Unloaded` (Konstruktor, Zeilen 51–56): Bricht `_readCts` ab und disposed es. **→ Dies ist das kritische Verhalten für die Anforderung.**

**Publizierte Events:**
- Keine public Events; triggert aber `InvalidateVisual()` zur Neuzeichnung.

**Abhängigkeiten:**
- `TerminalBuffer` (geladen aus `Session.Buffer` oder neu erstellt)
- `PseudoConsoleSession` (via Session-Property)
- `AnsiSequenceParser` (Parser für Terminal-Ausgabe)
- `ILogger<TerminalControl>` (Fehlerprotokollierung)

**Kritische Felder:**
- `_readCts`: CancellationTokenSource für die ReadLoop
- `_readLoopTask`: Task-Referenz der ReadLoop
- `_buffer`: Der aktuelle Terminal-Buffer

---

## `PseudoConsoleSession`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Koordiniert eine laufende Pseudo-Console-Sitzung bestehend aus `PseudoConsole`, `Process`, Eingabe- und Ausgabe-Stream.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PseudoConsoleSession(PseudoConsole, Process, Stream, Stream)` | internal | Erstellt eine neue Session mit Streams; startet internen _runtimeStatusTimer |
| `PseudoConsoleSession(PseudoConsole, Process, Stream, Stream, TimeProvider, TimeSpan)` | internal | Überladene Variante für Tests mit injizierbarem TimeProvider und Threshold |
| `MarkOutputActivity()` | public | Meldet gelesene Ausgabe an; setzt _lastOutputUtc; triggert SetRuntimeStatus(Laeuft) |
| `MarkInputActivity()` | public | Meldet Benutzereingabe an; setzt _lastInputUtc; triggert SetRuntimeStatus(Laeuft) |
| `Resize(int cols, int rows)` | public | Ändert Größe der Pseudo Console; delegiert an _pseudoConsole.Resize() |
| `Dispose()` | public | Disposed Streams, Timer, Pseudo Console und Prozess |
| `RefreshRuntimeStatus()` | private | Wird alle 1 Sekunde aufgerufen (via _runtimeStatusTimer); berechnet neuen Status via CliRuntimeStatusEvaluator |
| `SetRuntimeStatus(CliRuntimeStatus)` | private | Setzt _runtimeStatus thread-safe; fired RuntimeStatusChanged-Event wenn Status sich ändert |

**Properties:**
- `InputStream`: Schreibbarer Stream für Tastatureingaben an den Prozess
- `OutputStream`: Lesbarer Stream für die Prozessausgabe
- `Process`: Der verwaltete Prozess
- `RuntimeStatus`: Aktueller CLI-Laufzeitstatus (thread-safe)
- `Buffer`: Der Terminal-Buffer dieser Sitzung (wird von TerminalControl gesetzt und bei erneuter Anzeige wiederverwendet)

**Abonnierte Events:**
- Keine; verwendet aber internen Timer für Status-Refresh.

**Publizierte Events:**
- `RuntimeStatusChanged`: Wird gefeuert, wenn sich der CLI-Laufzeitstatus ändert (Laeuft → WartetAufEingabe, etc.). Abonniert von TaskDetailViewModel.

**Abhängigkeiten:**
- `PseudoConsole` (Verwaltung des HPCON-Handles)
- `Process` (der gestartete CLI-Prozess)
- `TimeProvider` (für Zeitmessungen und Status-Evaluierung)
- `CliRuntimeStatusEvaluator` (statische Hilfsmethode zur Status-Bestimmung)

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

ViewModel für die Aufgabendetailansicht. Verwaltet Status, Protokoll, CLI-Prozessstart und Fenstereinbettung.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TaskDetailViewModel(...)` | public | Konstruktor mit Dependency Injection; registriert CliProcessStatusChanged-Handler |
| `LadenAsync(CancellationToken)` | private async | Lädt Aufgabendaten; prüft ob CLI bereits läuft; startet AutoRestart falls nötig |
| `CliStoppenAsync(CancellationToken)` | private async | Stoppt den laufenden CLI-Prozess via _kiService.StopCliAsync() |
| `CliNeustartenAsync(CancellationToken)` | private async | Startet CLI manuell neu via CliAutomatischNeustartenAsync() |
| `StartenAsync(CancellationToken)` | private async | Startet Aufgabe (Plugin-Auflösung, Repository-Klonen, CLI-Start) |
| `PluginWechselAsync(CancellationToken)` | private async | Stoppt aktuelle CLI, öffnet Dialog für Plugin-Wahl, startet CLI mit neuem Plugin |
| `AufgabeAbschliessenAsync(CancellationToken)` | private async | Schließt Aufgabe ab (Status → Beendet) |
| `SpeichernAsync(CancellationToken)` | private async | Speichert Titel und Anforderungsbeschreibung |
| `LoeschenAsync(CancellationToken)` | private async | Löscht die Aufgabe nach Bestätigung |
| `IssueZuweisenAsync(CancellationToken)` | private async | Öffnet Dialog für Issue-Auswahl und zuweist das Issue |
| `IssueBrowserOeffnen()` | private | Öffnet Issue-URL im Standard-Browser |
| `OnCliProcessStatusChanged(Guid, CliProcessStatus)` | private | Wird von _kiService.CliProcessStatusChanged gefeuert; aktualisiert IsCliRunning |
| `GetPseudoConsoleSession()` | public | Gibt aktive PseudoConsoleSession für die aktuelle Aufgabe zurück (via _kiService.GetPseudoConsoleSession()) |
| `AttachCliStatusSession(PseudoConsoleSession?)` | private | Registriert/entregistriert Event-Handler für RuntimeStatusChanged; aktualisiert Statuszeile |
| `OnCliRuntimeStatusChanged(object?, CliRuntimeStatusChangedEventArgs)` | private | Wird von _cliStatusSession.RuntimeStatusChanged gefeuert; triggert UpdateCliStatusText() |
| `UpdateCliStatusText(CliRuntimeStatus)` | private | Aktualisiert die CLI-Statuszeile in der UI (Dispatcher-safe) |
| `StartCliAndUpdateStateAsync(string, string, string?, CancellationToken)` | private async | Innere Hilfsmethode zum CLI-Start mit State-Update |
| `CliAutomatischNeustartenAsync(CancellationToken)` | private async | Startet CLI automatisch neu, wenn Status=Gestartet und kein Prozess läuft |
| `Dispose()` | public | Deregistriert Events, cancelt pending Tokens |

**Properties:**
- `AufgabeId`: Liest/schreibt die Aufgaben-ID; triggert LadenAsync bei Änderung
- `Aufgabe`: Die geladene Aufgabe
- `IsCliRunning`: Ob ein CLI-Prozess läuft
- `CliStatusText`: Anzeigetext für CLI-Status (Fußzeile)
- `IsLoading`: Ob Daten gerade geladen werden
- `FehlerMeldung`: Error-Banner-Text
- `KannCliStoppen`, `KannCliNeuStarten`, `KannSpeichern`, `KannLoeschen`: CanExecute-Properties für Commands
- `Protokolleintraege`: ObservableCollection von Protokolleinträgen
- `VerfuegbareKiPlugins`: ObservableCollection verfügbarer Plugin-Prefixe
- `EditTitel`, `EditAnforderungsBeschreibung`: Two-Way-Bindings für Edit-Modus
- `ShowEditPanel`, `ShowCliPanel`, `ShowDiffPanel`: Visibility-Logik basierend auf Aufgaben-Status

**Abonnierte Events:**
- `_kiService.CliProcessStatusChanged`: Registriert im Konstruktor; wird von KiAusfuehrungsService gefeuert
- `_cliStatusSession.RuntimeStatusChanged`: Registriert in AttachCliStatusSession(); wird von PseudoConsoleSession gefeuert

**Publizierte Events:**
- `PseudoConsoleSessionGestartet`: Wird gefeuert, wenn neue Session gestartet wird; abonniert von TaskDetailView zum Setzen von TerminalControl.Session
- `CliGestoppt`: Wird gefeuert, wenn CLI gestoppt wird; abonniert von TaskDetailView zum Setzen von TerminalControl.Session = null

**Abhängigkeiten:**
- `KiAusfuehrungsService` (_kiService): Zentrale Verwaltung laufender CLI-Prozesse
- `AufgabeService`: Aufgabendaten laden/speichern/löschen
- `ProtokollService`: Protokoll-Einträge laden
- `EntwicklungsprozessService`: Prozess-Start und Aufgaben-Abschluss
- `PluginSelectionService`: Plugin-Auflösung und -Auswahl
- `IDialogService`: Dialoge für Bestätigung, Plugin-Wahl, Issue-Auswahl
- `IPluginManager`: Plugin-Registry und -Auflösung

---

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

Singleton-Service, der laufende CLI-Prozesse für KI-Ausführungen verwaltet. Startet, stoppt und überwacht CLI-Prozesse pro Aufgabe.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `KiAusfuehrungsService(ILogger, IServiceScopeFactory)` | public | Konstruktor |
| `IsRunning(Guid aufgabeId)` | public | Gibt an, ob ein Prozess für die Aufgabe läuft |
| `GetRunningProcess(Guid aufgabeId)` | public | Gibt den laufenden Prozess zurück, oder null |
| `GetRunningCount()` | public | Gibt die Anzahl laufender Prozesse zurück |
| `StartCliAsync(Guid aufgabeId, IKiPlugin kiPlugin, string localRepoPath, string? optionalParameters, CancellationToken)` | public async | Startet einen CLI-Prozess; liest PseudoConsoleSession aus kiPlugin.StartCliAsync(); speichert Handle in _handles |
| `StopCliAsync(Guid aufgabeId, CancellationToken)` | public async | Stoppt den laufenden Prozess |
| `StartWithPseudoConsoleAsync(Guid aufgabeId, IKiPlugin kiPlugin, string localRepoPath, string? optionalParameters, CancellationToken)` | public async | Startet CLI via Pseudo Console; speichert PseudoConsoleSession im Handle |
| `GetPseudoConsoleSession(Guid aufgabeId)` | public | Gibt die PseudoConsoleSession für eine Aufgabe zurück, oder null |
| `HandleProcessExited(Guid aufgabeId, Process process, CliProcessHandle handle, string reason)` | private | Wird aufgerufen, wenn ein Prozess beendet wird; fired CliProcessStatusChanged-Event |
| `Dispose()` | public | Stoppt alle laufenden Prozesse und disposed Ressourcen |

**Properties:**
- `_handles`: ConcurrentDictionary<Guid, CliProcessHandle> — Mapping Aufgaben-ID → laufender Prozess/Session

**Abonnierte Events:**
- Keine; verfeuert aber CliProcessStatusChanged beim Start/Stop/Fehler

**Publizierte Events:**
- `CliProcessStatusChanged`: Wird gefeuert mit (aufgabeId, status) bei Start, Stop, Fehler. Abonniert von TaskDetailViewModel.
- `RunningCountChanged`: Wird gefeuert, wenn sich die Anzahl laufender Prozesse ändert.

**Abhängigkeiten:**
- `IKiPlugin`: Abstraktion für Plugin-spezifische CLI-Starts
- `ProcessInformation`: oder ähnliche Klasse zum Speichern von Prozess-Handles

**Kritische Details:**
- Der Service verwaltet `_handles` als Globaler Registry laufender Prozesse.
- `StartWithPseudoConsoleAsync()` erstellt die `PseudoConsoleSession` und speichert sie im Handle.
- `GetPseudoConsoleSession()` wird von TaskDetailViewModel aufgerufen, um die Session an die View zu übergeben.
