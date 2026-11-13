using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Bündelt die optionalen Abhängigkeiten des <see cref="MainWindowViewModel"/>, die in
/// produktiven Aufrufen aus dem DI-Container aufgelöst und in Tests gezielt injiziert
/// werden, damit die Konstruktor-Parameterliste nicht für jede optionale Nahtstelle
/// einen eigenen Parameter benötigt.
/// </summary>
/// <param name="DispatcherInvoke">Optionale Abstraktion des UI-Dispatchers (Tests: synchron).</param>
/// <param name="DialogService">Optionaler Dialogdienst (Bestätigungs- und Auswahldialoge).</param>
/// <param name="VersionProvider">Optionaler Anbieter der installierten Programmversion.</param>
/// <param name="LaufdatenChangedNotifier">Optionaler Benachrichtiger für geänderte Aufgaben-Laufdaten.</param>
/// <param name="UpdateDienste">Optionales Dienstbündel für den Update-Ablauf.</param>
/// <param name="AufgabenListenModusService">Optionaler Persistenzdienst für den Anzeigemodus der Aufgabenliste.</param>
/// <param name="AutoShutdownOrchestrator">Optionaler Orchestrator des automatischen OS-Shutdowns; <c>null</c> in Tests.</param>
/// <param name="AutoShutdownEinstellungenService">Optionaler Persistenz-Service der Auto-Shutdown-Nebenwerte; <c>null</c> in Tests.</param>
/// <param name="TimeProvider">Optionaler Zeitgeber für den Countdown-Tick (für Tests injizierbar); <c>null</c> → <see cref="TimeProvider.System"/>.</param>
public sealed record MainWindowOptionaleDienste(
    Action<Action>? DispatcherInvoke = null,
    IDialogService? DialogService = null,
    IApplicationVersionProvider? VersionProvider = null,
    AufgabeLaufdatenChangedNotifier? LaufdatenChangedNotifier = null,
    MainWindowUpdateDienste? UpdateDienste = null,
    AufgabenListenModusService? AufgabenListenModusService = null,
    IAutoShutdownOrchestrator? AutoShutdownOrchestrator = null,
    AutoShutdownEinstellungenService? AutoShutdownEinstellungenService = null,
    TimeProvider? TimeProvider = null);
