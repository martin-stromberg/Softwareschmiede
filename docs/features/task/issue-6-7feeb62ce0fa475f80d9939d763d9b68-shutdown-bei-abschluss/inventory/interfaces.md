# Interfaces — Shutdown bei Abschluss

## `IRunningAutomationStatusSource`
Datei: `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs`

Zentrale Statusquelle für laufende KI-Automatisierungen; implementiert von `KiAusfuehrungsService` (Singleton, `App.xaml.cs` Z. 304–305). Konsumenten: `AutoShutdownOrchestrator` (Konstruktor-Abo), `MainWindowViewModel`.

```csharp
event Action<int, int>? RunningCountChanged;              // (previousCount, currentCount)
int GetRunningCount();                                    // alle laufenden gehandelten Prozesse
bool IsRunning(Guid aufgabeId);
AufgabeLaufStatus? GetLiveLaufStatus(Guid aufgabeId);
IReadOnlyCollection<Guid> GetLaufendeAufgabenIds();
```

Semantik-Fallstrick: `GetRunningCount()` zählt Prozess-Handles (`!HasExited`) — wartende Sessions (`WartetAufEingabe`) sind eingeschlossen und entsprechen nicht der fachlichen „läuft"-Definition der Anforderung (`KiAusfuehrungsZustand.Laeuft`). Eine fachlich gefilterte Zählmethode existiert nicht.

## `ISystemShutdownService`
Datei: `src/Softwareschmiede/Domain/Interfaces/ISystemShutdownService.cs`

OS-Shutdown-Abstraktion; einzige Implementierung `SystemShutdownService` (nicht DI-registriert).

```csharp
Task RequestShutdownAsync(CancellationToken cancellationToken = default);
```

**Nicht vorhanden:** Force-Parameter (Anforderung sieht `autoshutdown.force` vor — Windows-Kommando hat `/f` fest eingebaut, Unix/macOS ohne Force-Begriff), Countdown-/Abbruch-Begriffe, Fake für Testmodus.

## `IAutoShutdownOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/IAutoShutdownOrchestrator.cs` (liegt neben der Implementierung im Application-Ordner)

```csharp
void SetEnabled(bool enabled);
```

Minimalistisch: kein Status-/Countdown-Event, kein `IsEnabled`-Getter, keine Toleranz-/Force-Übergabe. Implementierung `AutoShutdownOrchestrator` ist `IDisposable` (Event-Abo). Nicht DI-registriert.

## `IAktiveAufgabenService`
Datei: `src/Softwareschmiede/Application/Services/IAktiveAufgabenService.cs` (bzw. Domain/Interfaces)

Liefert die Aufgaben-Menge für die Seitenleiste (Kandidatenfilter: `AufgabeStatusExtensions.AktivOderWartendStatus` = `Gestartet`/`Wartend` plus Lauf-Belege). Konsument: `MainWindowViewModel.AktiveAufgabenAktualisierenAsync`. Relevanz: Datenbasis der Liste, aus der die fachliche „läuft"-Zählung (`Klassifiziere == Laeuft`) ableitbar ist.

## `IApplicationShutdownService`
Datei: `src/Softwareschmiede/Domain/Interfaces/IApplicationShutdownService.cs`

```csharp
void Shutdown();
```

Beendet die **WPF-App** (`WpfApplicationShutdownService`, `App.xaml.cs` Z. 313); im Update-E2E-Modus ersetzt durch `RecordingApplicationShutdownService` (Z. ~407), der `UpdateE2EEreignisse.ShutdownRequested` protokolliert statt zu beenden. Fachlich vom OS-`ISystemShutdownService` getrennt — für „geordneter App-Shutdown vor OS-Shutdown" (offener Punkt der Anforderung) die vorhandene App-seitige Schnittstelle.

## `ICliUpdateSafetyService`
Datei: `src/Softwareschmiede/Application/Services/ICliUpdateSafetyService.cs`

Prüft, ob CLI-Läufe aktiv sind (Update-Sicherheit), über `AufgabeLaufAktivitaet.IstAktiv` (Heartbeat/`AktiveRunId`, 5-min-Timeout). Dritte „aktiv"-Definition neben `GetRunningCount` und UI-Klassifikation.

## `ITerminalSession` (statusrelevante Member)
Datei: `src/Softwareschmiede/Domain/Interfaces/ITerminalSession.cs` (bzw. Terminal-Ordner)

```csharp
CliRuntimeStatus RuntimeStatus { get; }
event EventHandler<CliRuntimeStatus>? RuntimeStatusChanged;   // Signatur siehe Quelldatei
```

Quelle der Live-Substatus-Wechsel `Laeuft` ↔ `WartetAufEingabe`; von `CliProcessManager` abonniert (Persistenz in `Aufgabe.LaufStatus`). Implementierungen: `PseudoConsoleSession` (ConPTY), `ScmCoordinatedTerminalSession`-Wrapper, Test-Fakes (`TestTerminalSessionFactory`-basierte Sessions mit `Process.GetCurrentProcess()` + `MemoryStream`).

## `AufgabeLaufdatenChangedNotifier` (Klasse statt Interface)
Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufdatenChangedNotifier.cs`

```csharp
event Action<Guid>? LaufdatenChanged;
void NotifyLaufdatenChanged(Guid aufgabeId);
```

Singleton-Event-Bus für Laufzeitdaten-Änderungen (Substatus, Herzschlag, geplante Prompts). Relevante Statuswechsel-Quelle für Countdown-Abbruch/-Neubewertung neben `RunningCountChanged`.
