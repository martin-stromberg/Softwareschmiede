## `ProtokollService`
Datei: `src/Softwareschmiede/Application/Services/ProtokollService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByAufgabeAsync(Guid aufgabeId, CancellationToken ct = default)` | `public` | Lädt Protokolle einer Aufgabe inkl. `TestErgebnisse`, sortiert nach `Zeitstempel`. |
| `AddEintragAsync(Guid aufgabeId, ProtokollTyp typ, string inhalt, string? agentName = null, CancellationToken ct = default)` | `public` | Persistiert einen allgemeinen Protokolleintrag. |
| `AddTestErgebnisseAsync(Guid aufgabeId, TestResult testResult, CancellationToken ct = default)` | `public` | Persistiert Testzusammenfassung plus zugehörige `TestErgebnis`-Datensätze. |
| `AddStatusUebergangAsync(Guid aufgabeId, AufgabeStatus vonStatus, AufgabeStatus nachStatus, CancellationToken ct = default)` | `public` | Erstellt Statusübergangsprotokoll über `AddEintragAsync`. |
| `AddCliOutputAsync(Guid aufgabeId, string outputLine, CancellationToken ct = default)` | `public` | Persistiert eine CLI-Ausgabezeile als `ProtokollTyp.CliOutput`; erkennt zusätzlich Rate-Limit-Marker. |
| `ParseRateLimitMarker(string outputLine)` | `public static` | Wrapper zum Marker-Parsing mit Rückgabe `(Found, Prompt, ResetUtc)`. |
| `TryParseRateLimitMarker(string outputLine, out DateTimeOffset? resetUtc)` | `public static` | Parst Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:...]]` und optionalen Zeitstempel. |
| `SuchenAsync(Guid aufgabeId, string suchbegriff, CancellationToken ct = default)` | `public` | Sucht in `Inhalt` und `AgentName` der Protokolleinträge. |

Abonnierte Events: Keine.  
Publizierte Events: Keine.

Querverweise:
- Wird von `TaskDetailViewModel.LadeProtokolleAsync` für das Laden der UI-Liste `Protokolleintraege` aufgerufen.
- Wird von `CliOutputProtokollWriter.PersistLineAsync` für die Persistierung von CLI-Zeilen aufgerufen.
- Wird indirekt durch `KiAusfuehrungsService` genutzt (über `CliOutputProtokollWriter`).

---

## `CliOutputProtokollWriter`
Datei: `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CliOutputProtokollWriter(Guid aufgabeId, IServiceScopeFactory scopeFactory, ILogger<CliOutputProtokollWriter> logger)` | `public` | Initialisiert Queue/Worker für nicht-blockierende CLI-Ausgabe-Persistierung. |
| `OnOutputChunk(ReadOnlySpan<byte> bytes)` | `public` | Nimmt Output-Bytes an, segmentiert sie via `CliOutputLineAccumulator` und queued Zeilen. |
| `Complete()` | `public` | Schließt idempotent den Writer und flusht Restzeilen. |
| `CompleteAsync(TimeSpan timeout, CancellationToken ct = default)` | `public` | Schließt und wartet begrenzt auf das Abarbeiten der Queue. |
| `CompleteLocked()` | `private` | Interner Abschluss inkl. Flush und Channel-Complete. |
| `TryQueueLine(string line)` | `private` | Schreibt Zeilen in den bounded Channel inkl. Backpressure/Warnings. |
| `ProcessLinesAsync()` | `private` | Worker-Schleife zum Persistieren aller gequeue-ten Zeilen. |
| `PersistLineAsync(string line)` | `private` | Erstellt Scope, löst `ProtokollService` auf und ruft `AddCliOutputAsync` auf. |

Abonnierte Events: Keine.  
Publizierte Events: Keine.

Querverweise:
- Implementiert `ITerminalOutputSink`.
- Wird in `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erzeugt und an den ConPTY-Launcher übergeben.

---

## `CliOutputLineAccumulator`
Datei: `src/Softwareschmiede/Application/Services/CliOutputLineAccumulator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Append(ReadOnlySpan<byte> bytes)` | `public` | Dekodiert UTF-8-Chunk und liefert abgeschlossene Zeilen in Reihenfolge. |
| `Flush()` | `public` | Schließt Dekodierung ab und liefert ggf. verbleibende Restzeile. |
| `AppendText(ReadOnlySpan<char> text)` | `private` | Zerlegt Zeichenstrom in Zeilen (`LF`/`CRLF`/`CR`). |

Abonnierte Events: Keine.  
Publizierte Events: Keine.

Querverweise:
- Wird von `CliOutputProtokollWriter` für chunkübergreifende Zeilensegmentierung verwendet.

---

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCliAsync(...)` | `public` | Startet klassischen CLI-Prozess pro Aufgabe und registriert Exited-Handling. |
| `StartWithPseudoConsoleAsync(...)` | `public` | Startet ConPTY-Session und erzeugt dafür `CliOutputProtokollWriter` als Output-Senke. |
| `GetPseudoConsoleSession(Guid aufgabeId)` | `public` | Liefert laufende Session einer Aufgabe für UI-Bindung. |
| `StopCliAsync(Guid aufgabeId, CancellationToken ct = default)` | `public` | Beendet laufenden Prozess einer Aufgabe. |
| `HandleProcessExitedAsync(...)` | `private` | Einheitlicher Exited-Pfad: Statusableitung, Persistierung, Events. |
| `CancelAndDisposeConPtyResourcesAsync(CliProcessHandle handle)` | `private` | Räumt ConPTY-Ressourcen auf und schließt `OutputSink` per `CompleteAsync`. |

Abonnierte Events:
- `Process.Exited` (registriert in `StartCliAsync` und `StartWithPseudoConsoleAsync`).

Publizierte Events:
- `CliProcessStatusChanged`
- `RunningCountChanged`

Querverweise:
- Übergibt `CliOutputProtokollWriter` im ConPTY-Pfad an den Launcher; dieser Pfad erzeugt die `CliOutput`-Protokolleinträge.
- Wird in `TaskDetailViewModel` für CLI-Laufstatus (`IsRunning`, `GetPseudoConsoleSession`, `CliProcessStatusChanged`) verwendet.

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync(CancellationToken ct)` | `private` | Lädt Aufgabendaten und stößt Protokollnachladen (`LadeProtokolleAsync`) per Fire-and-Forget an. |
| `LadeProtokolleAsync(CancellationToken ct)` | `private` | Lädt Protokolle via `ProtokollService.GetByAufgabeAsync` in `Protokolleintraege`. |
| `OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)` | `private` | Reagiert auf Laufstatuswechsel der CLI und aktualisiert UI-Zustand. |
| `AttachCliStatusSession(PseudoConsoleSession? session)` | `private` | Bindet Runtime-Status-Quelle der aktuellen Session an/unab. |
| `UpdateCliStatusText(CliRuntimeStatus status)` | `private` | Setzt textuelle CLI-Statusanzeige. |
| `Dispose()` | `public` | Deregistriert Eventhandler und beendet interne Ressourcen. |

Abonnierte Events:
- `_kiService.CliProcessStatusChanged += OnCliProcessStatusChanged`
- `_promptZeitVersandService.PromptSent += OnPromptSent`
- `_cliStatusSession.RuntimeStatusChanged += OnCliRuntimeStatusChanged` (über `AttachCliStatusSession`)

Publizierte Events:
- `PseudoConsoleSessionGestartet`
- `CliGestoppt`
- `PromptVorlageGesendet`

Querverweise:
- Protokollliste `Protokolleintraege` wird aus `ProtokollService` befüllt und in `TaskDetailView.xaml` als `TaskProtocol` angezeigt.
- Enthält aktuell keinen Export-Command und keine Dateischreiboperation für CLI-Rohdaten.

---

## `TaskDetailView`
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)` | `private` | Bindet ViewModel-Events und setzt Terminal-Session passend zum neuen DataContext. |
| `OnPseudoConsoleSessionGestartet(PseudoConsoleSession session)` | `private` | Übergibt gestartete Session an `TerminalControl`. |
| `OnCliGestoppt()` | `private` | Entfernt Session aus `TerminalControl`. |
| `SetTerminalSession(PseudoConsoleSession? session)` | `private` | Setzt `TerminalConsole.Session` und diagnostischen HelpText (PID). |
| `OnTerminalScrollViewerPreviewMouseDown(...)` / `ShouldFocusTerminalFromScrollViewerMouseSource(...)` | `private` / `internal static` | Fokussteuerung für Klickverhalten in der Terminal-Fläche. |

Abonnierte Events:
- `DataContextChanged` (UserControl)
- `TaskDetailViewModel.PseudoConsoleSessionGestartet`
- `TaskDetailViewModel.CliGestoppt`
- `TaskDetailViewModel.PromptVorlageGesendet`

Publizierte Events: Keine.
