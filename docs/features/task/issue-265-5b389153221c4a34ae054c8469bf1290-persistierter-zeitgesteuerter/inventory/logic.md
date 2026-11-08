# Logik — Bestandsaufnahme

## `PromptZeitVersandService`
Datei: `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`

Singleton-Service (DI: `App.xaml.cs` Zeile 281), der die **rein sitzungsgebundene** Laufzeit-Warteschlange zeitgesteuerter Prompts verwaltet (Klassenkommentar Zeilen 7–11: „Es gibt keine Persistierung"). Interner Zustand: `Dictionary<Guid, ScheduledPromptEntry> _scheduledPrompts` (Zeile 24) unter `_lock` (Zeile 25). Abhängigkeiten: `KiAusfuehrungsService`, `TimeProvider`, `ILogger`, optionale `IServiceScopeFactory` (Konstruktor Zeilen 32–38; ohne sie entfällt die Pausenprüfung — z. B. in Unit-Tests).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PromptZeitVersandService(kiService, timeProvider, logger, scopeFactory = null)` | public ctor | Verdrahtet Abhängigkeiten; `scopeFactory` optional für DB-Zugriff (`PausiertBisUtc`) aus dem Singleton. |
| `SchedulePromptAsync(Guid aufgabeId, string promptText, DateTimeOffset targetTime)` | public | Plant den Versand. Ablauf: aktive Pause lädt die Zielzeit auf das Pausenende vor (Zeilen 57–61); Zielzeit ≤ jetzt → Eintrag entfernen + `SendPromptAsync` sofort (Zeilen 63–68); sonst ersetzt ein neuer Eintrag einen vorhandenen (Zeile 70), Timer wird zunächst inaktiv angelegt und erst **nach** dem Einfügen in `_scheduledPrompts` via `timer.Change` scharf gemacht (Reihenfolge-Garantie, Kommentar Zeilen 73–89). |
| `CancelScheduledPrompt(Guid aufgabeId)` | public | Entfernt den Eintrag und disposed den Timer (Zeilen 94–97). Aufgerufen von `TaskDetailViewModel.Dispose` (Zeile 1559), `TaskDetailViewModel.OnCliProcessStatusChanged` bei CLI-Stopp (Zeile 1532) und `EntwicklungsprozessService.AbschliessenCoordinatedAsync` (Zeile 438). |
| `GetScheduledPromptStatus(Guid aufgabeId)` | public | Liefert die `ScheduledPromptInfo` des Eintrags oder `null`. Aufgerufen von `TaskDetailViewModel.SchedulePromptAsync` (Zeile 1835) und `MainWindowViewModel` (Zeile 406). |
| `RemoveEntry(Guid aufgabeId)` | private | Entfernt den Eintrag unter `_lock` und liefert den Timer zum Disposen (Zeilen 110–116). |
| `HandleTimerElapsedAsync(Guid aufgabeId)` | private | Timer-Callback (als `SafeFireAndForget` registriert, Zeile 79). Entfernt den Eintrag; lädt aktuellen `PausiertBisUtc` — bei aktiver Pause wird der Eintrag mit neuer `TargetTime = bis` zurückgelegt und der Timer neu scharf gemacht, sofern nicht zwischenzeitlich ein neuer Prompt geplant wurde (Zeilen 130–165); sonst Timer disposen und `SendPromptAsync` (Zeilen 167–168). |
| `GetPausiertBisUtcAsync(Guid aufgabeId)` | private | Lädt `Aufgabe.PausiertBisUtc` über einen Async-Scope mit `AufgabeService.GetByIdAsync` (Zeilen 171–191); ohne `_scopeFactory` oder bei Fehler → `null` (keine Verschiebung, Log-Warnung). |
| `SendPromptAsync(Guid aufgabeId, string promptText)` | private | Holt die Session via `_kiService.GetTerminalSession(aufgabeId)`; ohne Session → stiller Verwurf mit Log-Warnung (Zeilen 195–202). Bei Erfolg `session.WritePromptAsync(promptText, CancellationToken.None)` und `PromptSent?.Invoke(aufgabeId)`; `ObjectDisposedException` → LogDebug, andere Fehler → LogWarning (Zeilen 204–219). |

Publizierte Events: `PromptSent` (`Action<Guid>`, Zeile 41) — abonniert in `TaskDetailViewModel` (Zeile 672, Handler `OnPromptSent` Zeilen 1846–1857: Statusanzeige leeren + Wechsel in CLI-Ansicht).

Abonnierte Events: keine.

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AbschliessenAsync(Guid aufgabeId, CancellationToken ct)` | public | Einziger produktiver Einstieg des Geschäftsvorgangs „Aufgabe abschließen"; läuft unter `ScmTaskCoordinator.Shared`-Aufgabensperre (Zeilen 425–427). |
| `AbschliessenCoordinatedAsync(Guid aufgabeId, CancellationToken ct)` | private | Stoppt die CLI (`KiAusfuehrungsService.StopCliAsync`), ruft dann `_options.PromptZeitVersandService?.CancelScheduledPrompt(aufgabeId)` (Zeile 438), prüft `CanCompleteTaskAsync`, löscht den Klon, setzt Status `Beendet`, schreibt Protokolleintrag. |

## `TaskDetailViewModel` (anforderungsrelevante Member)
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `ScheduledPromptTargetHours` / `ScheduledPromptTargetMinutes` | public Property (`int?`) | Bindung der HH:mm-Eingabefelder (Zeilen 301–320); Setter feuern `PropertyChanged` für `KannPromptPlanen`. |
| `ScheduledPromptStatus` | public Property (`string?`, private setter) | Anzeigetext „Prompt in Wartestellung" (Zeilen 323–327). |
| `ScheduledPromptTimeDisplay` | public Property (`string?`, private setter) | Zielzeit-Anzeige im Format HH:mm (Zeilen 330–334). |
| `KannPromptPlanen` | public Property (`bool`) | Guard: `_isCliRunning` && Vorlage mit Text gewählt && mind. ein Zeitfeld gesetzt && `!IstPausiert` (Zeilen 337–341). |
| `SchedulePromptCommand` | public `ICommand` | `AsyncRelayCommand(SchedulePromptAsync, () => KannPromptPlanen)` (Zeilen 598, 711); gebunden an den Ribbon-Button `ZeitgesteuertSenden` (`TaskDetailView.xaml` Zeilen 149–152). |
| `SchedulePromptAsync(CancellationToken ct)` | private | Validiert Stunde (0–23) und Minute (0–59) mit `FehlerMeldung` (Zeilen 1803–1813); baut `targetTime` aus heutigem Datum + eingegebener Uhrzeit über `_timeProvider.GetLocalNow()` (Zeilen 1820–1823); löst Platzhalter via `_promptVorlagenPlatzhalterService.Resolve` (Zeile 1825); ruft `_promptZeitVersandService.SchedulePromptAsync` (Zeile 1829); setzt bei vorhandenem Warteschlangen-Eintrag `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` (Zeilen 1835–1839); leert danach Zeitfelder und Vorlagenauswahl. |
| `OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)` | private | Event-Handler für `KiAusfuehrungsService.CliProcessStatusChanged` (Abonnement Zeile 671). Bei Status ≠ `Gestartet` und nicht laufender CLI: `AttachCliStatusSession(null)`, `CancelScheduledPrompt`, Statusanzeige leeren (Zeilen 1529–1535). |
| `OnPromptSent(Guid aufgabeId)` | private | Event-Handler für `PromptZeitVersandService.PromptSent` (Abonnement Zeile 672): leert die Statusanzeige und wechselt in die CLI-Ansicht (Zeilen 1846–1857). |
| `Dispose()` | public | Meldet beide Events ab, ruft `CancelScheduledPrompt(_aufgabeId)` (Zeile 1559), räumt CTS-Objekte (Zeilen 1551–1567). |
| `GetTerminalSession()` | public | Delegiert an `_kiService.GetTerminalSession(_aufgabeId)` (Zeile 625); Kommentar: Session überdauert den Lebenszyklus der View. |

Abonnierte Events: `KiAusfuehrungsService.CliProcessStatusChanged`, `PromptZeitVersandService.PromptSent`.
Publizierte Events: `TerminalSessionGestartet`, `CliGestoppt`, `PromptVorlageGesendet` (Zeilen 612–618).

## `MainWindowViewModel` (anforderungsrelevante Member)
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `_promptZeitVersandService` | private readonly Feld | Injiziert (Zeilen 28, 152, 161). |
| Panel-Item-Erzeugung (`HasScheduledPrompt`) | private | Beim Aufbau eines `AktiveAufgabePanelItem`: `HasScheduledPrompt = _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id) is not null` (Zeile 406). |

## `KiAusfuehrungsService` (anforderungsrelevante Member)
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `_handles` | private readonly `ConcurrentDictionary<Guid, CliProcessHandle>` | Prozessweite Session-Map (Zeile 26); CLI-Prozesse sind Kindprozesse der App — nach einem App-Neustart ist die Map leer. |
| `CliProcessStatusChanged` | public event `Action<Guid, CliProcessStatus>?` | Wird beim Start (Zeilen 184, 349) und bei Prozessende (Zeile 660) gefeuert. |
| `IsRunning(Guid aufgabeId)` | public | Liefert, ob ein Handle existiert und dessen Prozess läuft (Zeilen 68–…). |
| `GetTerminalSession(Guid aufgabeId)` | public | Liefert `handle.Session` oder `null` (Zeilen 404–409) — Zustellpunkt des Versands. |
| `StopCliAsync` | public | Wird u. a. von `EntwicklungsprozessService` beim Abschluss aufgerufen (Zeile 435). |

## `CliProcessManager`
Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

Singleton (DI: `App.xaml.cs` Zeile 286), abonniert `CliProcessStatusChanged` und pflegt den Heartbeat-Timer pro Status (`Gestartet`/`Fehler`, Zeilen 131–151). Für die Anforderung indirekt relevant (Session-Lebenszyklus).

## `AufgabeService` (anforderungsrelevante Methoden)
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByIdAsync` | public | Lädt eine Aufgabe; vom `PromptZeitVersandService` über den Scope für `PausiertBisUtc` genutzt (`GetPausiertBisUtcAsync`, Zeile 180). |
| `SavePromptVorschlagAsync(Guid id, string? prompt, DateTimeOffset? ausfuehrenAbUtc, ct)` | public | Persistiert `VorschlagPrompt`/`VorschlagAusfuehrenAbUtc` auf der `Aufgabe` (Zeilen 788–805) — fachlich „Prompt-Vorschlag", nicht der zeitgesteuerte Versand. |
| `ClearPromptVorschlagAsync(Guid id, ct)` | public | Leert beide Vorschlags-Felder (Zeilen 808–819). |
| `SetPauseAsync(Guid aufgabeId, DateTimeOffset? pausiertBisUtc, ct)` | public | Setzt/leert `PausiertBisUtc`; schreibt `ProtokollTyp.SystemMeldung` (Zeilen 831 ff.). |

## `PromptVorlagenPlatzhalterService`
Datei: `src/Softwareschmiede/Application/Services/PromptVorlagenPlatzhalterService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Resolve(string prompttext, Aufgabe? aufgabe)` | public | Löst Platzhalter im Vorlagentext auf; wird in `TaskDetailViewModel.SchedulePromptAsync` **vor** der Übergabe an den Versand-Service aufgerufen (Zeile 1825). |

## `PeriodicBackgroundService`
Datei: `src/Softwareschmiede/Application/Services/PeriodicBackgroundService.cs`

Abstrakte `BackgroundService`-Basisklasse für Polling-Dienste: `ExecuteAsync` ruft `RunOnceAsync` in einer Schleife, fängt Fehler pro Durchlauf ab und wartet `_pollingInterval` via `Task.Delay(interval, _timeProvider, ct)` (Zeilen 28–54). `RunOnceAsync(CancellationToken)` ist abstrakt (Zeile 58). Vorhandene Ableitungen/Hosted-Service-Registrierungen: `ScmEventMonitoringService`, `ScmEventExchangeService`, `PullRequestMonitoringService`, `ScmTaskCompletionMonitoringService` (`App.xaml.cs` Zeilen 216–219, 282–285) — Muster für einen etwaigen Start-/Re-Arm-Dienst.

## `App` (DI und Startpfad)
Datei: `src/Softwareschmiede.App/App.xaml.cs`

| Bereich | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `db.Database.MigrateAsync()` + `EnsureInitialPromptVorlagenAsync()` | 93–100 | Migrationen und Seed-Daten beim App-Start, in einem Scope. |
| `AddDbContext<SoftwareschmiededDbContext>` (SQLite) | 177–182 | Scoped DbContext; Pfad via `DatenbankPfadResolver`. |
| `AddSingleton(TimeProvider.System)` | 280 | Zentrale Zeitquelle (auch für `PromptZeitVersandService`-Timer). |
| `AddSingleton<PromptZeitVersandService>()` | 281 | Singleton-Registrierung (bezieht `IServiceScopeFactory` automatisch aus dem Root-Provider). |
| `AddSingleton` + `AddHostedService`-Muster | 216–219, 282–285 | Registrierung derselben Instanz als Singleton und `IHostedService`. |
| `EntwicklungsprozessService`-Factory | 220–233 | Verdrahtet `PromptZeitVersandService` in `EntwicklungsprozessServiceOptions`. |
| `CliProcessManager`-Initialisierung | 86–91, 286 | Wird beim Start explizit aufgelöst. |

## `KiAusfuehrungsStatusKlassifikation`
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Klassifiziere(object value, out TimeSpan pausiertRestzeit)` | internal static | Mappt `Aufgabe`/`AktiveAufgabePanelItem` auf `KiAusfuehrungsZustand`: `Pausiert` (Vorrang, Zeilen 164–168) → `PromptInWartestellung` bei `HasScheduledPrompt` (Zeilen 170–173) → `Laeuft`/`Wartet`/`Bereit`. `HasScheduledPrompt` kommt bei `Aufgabe`-Eingaben fest als `false` herein (Zeile 152). |

## `AsyncTaskExtensions.SafeFireAndForget`
Datei: `src/Softwareschmiede/Application/Services/AsyncTaskExtensions.cs` (Zeile 14)

Fire-and-forget-Helfer mit Logging; verwendet für den Timer-Callback `HandleTimerElapsedAsync` (`PromptZeitVersandService` Zeile 79).
