using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den KiAusfuehrungsService.</summary>
public sealed class AiExecutionServiceTests : IDisposable
{
    private readonly AiExecutionService _sut;

    /// <summary>KiAusfuehrungsServiceTests.</summary>
    public AiExecutionServiceTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _sut.Dispose();

    /// <summary>IsRunning returns false for unknown task.</summary>
    [Fact]
    public void IsRunning_ShouldReturnFalse_WhenNoProcessStarted()
    {
        _sut.IsRunning(Guid.NewGuid()).Should().BeFalse();
    }

    /// <summary>GetRunningCount returns zero initially.</summary>
    [Fact]
    public void GetRunningCount_ShouldReturnZero_WhenNoProcessStarted()
    {
        _sut.GetRunningCount().Should().Be(0);
    }

    /// <summary>GetLastExitCode returns null for unknown task.</summary>
    [Fact]
    public void GetLastExitCode_ShouldReturnNull_WhenNoProcessStarted()
    {
        _sut.GetLastExitCode(Guid.NewGuid()).Should().BeNull();
    }

    /// <summary>StopCliAsync does nothing for unknown task.</summary>
    [Fact]
    public async Task StopCliAsync_ShouldNotThrow_WhenNoProcessStarted()
    {
        var act = () => _sut.StopCliAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    /// <summary>UpdateHeartbeat does nothing for unknown task.</summary>
    [Fact]
    public void UpdateHeartbeat_ShouldNotThrow_WhenNoProcessStarted()
    {
        var act = () => _sut.UpdateHeartbeat(Guid.NewGuid());
        act.Should().NotThrow();
    }

    /// <summary>TestCliStartAsync: CLI wird gestartet, ProcessHandle wird zurückgegeben.</summary>
    [OsInterfaceFact]
    public async Task TestCliStartAsync()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Act
        var handle = await _sut.StartCliAsync(workItemId, pluginMock.Object, Path.GetTempPath());

        // Assert
        handle.Should().NotBeNull();
        handle.WorkItemId.Should().Be(workItemId);
        _sut.IsRunning(workItemId).Should().BeTrue();
        _sut.GetRunningCount().Should().BeGreaterThan(0);
    }

    /// <summary>StartCliAsync returns handle when plugin provides valid ProcessStartInfo.</summary>
    [OsInterfaceFact]
    public async Task StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo()
    {
        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var handle = await _sut.StartCliAsync(workItemId, pluginMock.Object, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.WorkItemId.Should().Be(workItemId);
    }

    /// <summary>GetTerminalSession gibt null zurück wenn keine ConPTY-Session gestartet wurde.</summary>
    [Fact]
    public void GetTerminalSession_ReturnsNull_WithoutSession()
    {
        _sut.GetTerminalSession(Guid.NewGuid()).Should().BeNull();
    }

    /// <summary>
    /// Regressionstest für die Race Condition beim App-Shutdown: Wenn ein CLI-Prozess mit
    /// Fehler-ExitCode beendet wird, während der ScopeFactory-Zugriff (z. B. weil der
    /// IServiceProvider während des Anwendungs-Shutdowns bereits disposed wurde) eine
    /// ObjectDisposedException wirft, darf PersistFehlgeschlagenAsync diese nicht unbehandelt
    /// weiterwerfen (führte zuvor zu einer TaskScheduler.UnobservedTaskException).
    /// </summary>
    [OsInterfaceFact]
    public async Task ProcessExited_ScopeFactoryDisposed_PersistedNotAndThrowsNot()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(() => new ObjectDisposedException("IServiceProvider"));

        using var sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);

        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 1",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var statusEvents = new List<CliProcessStatus>();
        var errorSignal = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            statusEvents.Add(status);
            if (status == CliProcessStatus.Error)
            {
                errorSignal.TrySetResult();
            }
        };

        await sut.StartCliAsync(workItemId, pluginMock.Object, Path.GetTempPath());

        var completed = await Task.WhenAny(errorSignal.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().Be(errorSignal.Task, "der Exited-Handler sollte trotz disposed ScopeFactory ausgelöst werden, ohne die Anwendung zum Absturz zu bringen");

        statusEvents.Should().Contain(CliProcessStatus.Error);
    }

    /// <summary>Natürliches Prozessende persistiert den Ausführungsstatus zentral als Beendet.</summary>
    [OsInterfaceFact]
    public async Task ProcessExited_ExitCodeZero_EndPersistedExecutionCompletedWithoutOverallstatusTo()
    {
        await using var provider = CreateWorkItemServiceProvider();
        var (workItemId, _) = await CreateStartedWorkItemWithActiveRunAsync(provider);
        using var sut = TestAiExecutionServiceFactory.Create(provider.GetRequiredService<IServiceScopeFactory>());

        await StartAndWaitForExitAsync(
            sut,
            workItemId,
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            CliProcessStatus.Stopped);

        var loaded = await WaitForWorkItemStatusAsync(provider, workItemId, WorkItemExecutionStatus.Completed);
        loaded.Status.Should().Be(WorkItemStatus.Started);
        loaded.ActiveRunId.Should().BeNull();
        loaded.LastHeartbeatUtc.Should().BeNull();
        loaded.LastCliStartUtc.Should().BeNull();
    }

    /// <summary>Fehlerhaftes Prozessende beendet nur die Ausführung, nicht den Gesamtstatus.</summary>
    [OsInterfaceFact]
    public async Task ProcessExited_ExitCodeNonZero_EndPersistedExecutionCompletedWithoutOverallstatusTo()
    {
        await using var provider = CreateWorkItemServiceProvider();
        var (workItemId, _) = await CreateStartedWorkItemWithActiveRunAsync(provider);
        using var sut = TestAiExecutionServiceFactory.Create(provider.GetRequiredService<IServiceScopeFactory>());

        await StartAndWaitForExitAsync(
            sut,
            workItemId,
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 1",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            CliProcessStatus.Error);

        var loaded = await WaitForWorkItemStatusAsync(provider, workItemId, WorkItemExecutionStatus.Completed);
        loaded.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>Manueller Stop persistiert den Ausführungsstatus zentral als Beendet.</summary>
    [OsInterfaceFact]
    public async Task StopCliAsync_EndPersistedExecutionCompletedWithoutOverallstatusTo()
    {
        await using var provider = CreateWorkItemServiceProvider();
        var (workItemId, _) = await CreateStartedWorkItemWithActiveRunAsync(provider);
        using var sut = TestAiExecutionServiceFactory.Create(provider.GetRequiredService<IServiceScopeFactory>());

        var stopped = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == CliProcessStatus.Stopped)
                stopped.TrySetResult();
        };

        var pluginMock = CreatePlugin(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout /t 30 /nobreak > nul",
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        await sut.StartCliAsync(workItemId, pluginMock.Object, Path.GetTempPath());
        await sut.StopCliAsync(workItemId);

        var completed = await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().Be(stopped.Task);

        var loaded = await WaitForWorkItemStatusAsync(provider, workItemId, WorkItemExecutionStatus.Completed);
        loaded.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>
    /// Wenn ein Abonnent von CliProcessStatusChanged im Exited-Handler von StartCliAsync eine Exception
    /// wirft, muss diese geloggt werden, ohne die Multicast-Kette oder die Anwendung zum Absturz zu bringen.
    /// </summary>
    [OsInterfaceFact]
    public async Task ProcessExited_SubscriberThrows_LogsAndDoesNotCrash()
    {
        // Klassischer Pfad: der Plugin-ProcessStartInfo wird direkt als der überwachte Prozess gestartet
        // ("cmd.exe /c exit 0" terminiert diesen Prozess sofort selbst).
        await AssertSubscriberExceptionIsLoggedAsync(
            (sut, workItemId, plugin) => sut.StartCliAsync(workItemId, plugin, Path.GetTempPath()),
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Wenn ein Abonnent von CliProcessStatusChanged im ConPTY-Exited-Handler von StartTerminalSessionAsync
    /// eine Exception wirft, muss diese geloggt werden, ohne die Anwendung zum Absturz zu bringen.
    /// </summary>
    [OsInterfaceFact]
    public async Task ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash()
    {
        // Terminal-Pfad: die Session startet den Plugin-Prozess direkt aus der
        // TerminalSessionStartSpec (keine cmd.exe-Hülle mehr) — "cmd.exe /c exit 0" terminiert
        // den überwachten Prozess selbst und löst das Exited-Ereignis der Session aus.
        await AssertSubscriberExceptionIsLoggedAsync(
            (sut, workItemId, plugin) => sut.StartTerminalSessionAsync(workItemId, plugin, Path.GetTempPath()),
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            TimeSpan.FromSeconds(15),
            new SimulatedPseudoConsoleProcessLauncher(
                NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance,
                NullLoggerFactory.Instance,
                Options.Create(new TerminalSessionOptions())));
    }

    /// <summary>
    /// Gemeinsames Setup für <see cref="ProcessExited_SubscriberThrows_LogsAndDoesNotCrash"/> und
    /// <see cref="ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash"/>: Erstellt Mocks für Logger,
    /// ScopeFactory und Plugin, registriert einen Subscriber, der im Exited-Handler eine Exception wirft,
    /// ruft den übergebenen Start-Vorgang auf und verifiziert, dass die Exception geloggt wird.
    /// </summary>
    /// <param name="startAsync">Der auszuführende Start-Aufruf (StartCliAsync oder StartTerminalSessionAsync).</param>
    /// <param name="pluginProcessStartInfo">Der vom gemockten Plugin gelieferte Befehl, der den überwachten Prozess
    /// (klassisch) bzw. die getippte Konsoleneingabe (ConPTY) tatsächlich beendet.</param>
    /// <param name="timeout">Maximale Wartezeit auf das Logging der Exception.</param>
    /// <param name="pseudoConsoleProcessLauncher">Optionaler Launcher für den PseudoConsole-Pfad, damit Tests keinen
    /// echten Win32-ConPTY-Launcher verwenden müssen.</param>
    private static async Task AssertSubscriberExceptionIsLoggedAsync(
        Func<AiExecutionService, Guid, IAiPlugin, Task> startAsync,
        System.Diagnostics.ProcessStartInfo pluginProcessStartInfo,
        TimeSpan timeout,
        IPseudoConsoleProcessLauncher? pseudoConsoleProcessLauncher = null)
    {
        var loggerMock = new Mock<ILogger<AiExecutionService>>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        using var sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object, loggerMock.Object, pseudoConsoleProcessLauncher);

        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pluginProcessStartInfo);
        pluginMock.SetupTerminalSpec(pluginProcessStartInfo.FileName, pluginProcessStartInfo.Arguments);

        var errorLogged = new TaskCompletionSource();
        loggerMock
            .Setup(l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => e is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => errorLogged.TrySetResult());
        sut.CliProcessStatusChanged += (_, status) =>
        {
            // Nur beim Exited-Handler werfen (Stopped/Fehler), nicht beim initialen Gestartet-Event,
            // das außerhalb des try-catch-geschützten Exited-Handlers liegt.
            if (status != CliProcessStatus.Started)
                throw new InvalidOperationException("Simulierter Subscriber-Fehler");
        };

        await startAsync(sut, workItemId, pluginMock.Object);

        var finished = await Task.WhenAny(errorLogged.Task, Task.Delay(timeout));
        finished.Should().Be(errorLogged.Task, "der Exited-Handler muss die Exception des werfenden Subscribers loggen, statt die Anwendung abstürzen zu lassen");
    }

    /// <summary>Beendet der CLI-Prozess einer Terminal-Session (hier über <see cref="AiExecutionService.StopCliAsync"/>),
    /// muss der Session-<c>Exited</c>-Handler die zugehörige <see cref="PseudoConsoleSession"/>
    /// disposen (Leseschleife wird abgebrochen, Streams werden geschlossen), bevor das Handle aus <c>_handles</c>
    /// entfernt wird. Nutzt den Pipe-basierten <see cref="SimulatedPseudoConsoleProcessLauncher"/> mit einem
    /// echten <c>cmd.exe</c>, damit Kill/Exit auf einem echten Prozess beobachtbar sind.</summary>
    [OsInterfaceFact]
    public async Task AiExecutionService_HandleProcessExited_DisposesSession()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var launcher = new SimulatedPseudoConsoleProcessLauncher(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance, Options.Create(new TerminalSessionOptions()));
        using var sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object, launcher: launcher);

        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.SetupTerminalSpec("cmd.exe");

        await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath());
        var session = sut.GetTerminalSession(workItemId);
        session.Should().NotBeNull("StartTerminalSessionAsync muss eine PseudoConsoleSession im Handle hinterlegen");

        var stopped = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == CliProcessStatus.Stopped)
                stopped.TrySetResult();
        };

        await sut.StopCliAsync(workItemId);

        var finished = await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        finished.Should().Be(stopped.Task, "StopCliAsync muss letztlich zu HandleProcessExited und damit zum Stopped-Status führen");

        AssertSessionDisposed(session!, "HandleProcessExited muss die PseudoConsoleSession disposen");
        sut.GetTerminalSession(workItemId).Should().BeNull("nach HandleProcessExited darf das Handle nicht mehr in _handles vorhanden sein");
    }

    /// <summary>Ruft man <see cref="AiExecutionService.Dispose"/> auf, während noch eine Terminal-Session läuft,
    /// müssen deren Prozess beendet und die zugehörige <see cref="PseudoConsoleSession"/> disposed werden (Leseschleife
    /// abgebrochen, Streams geschlossen) — keine verwaisten Leseschleifen nach dem Beenden des Service. Nutzt den
    /// Pipe-basierten <see cref="SimulatedPseudoConsoleProcessLauncher"/> mit einem echten <c>cmd.exe</c>.</summary>
    [OsInterfaceFact]
    public async Task AiExecutionService_Dispose_CancelsAllSessions()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var launcher = new SimulatedPseudoConsoleProcessLauncher(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance, Options.Create(new TerminalSessionOptions()));
        var sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object, launcher: launcher);

        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.SetupTerminalSpec("cmd.exe");

        await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath());
        var session = sut.GetTerminalSession(workItemId);
        session.Should().NotBeNull("StartTerminalSessionAsync muss eine PseudoConsoleSession im Handle hinterlegen");

        sut.Dispose();

        AssertSessionDisposed(session!, "KiAusfuehrungsService.Dispose() muss alle noch laufenden PseudoConsoleSession-Instanzen disposen");
    }

    /// <summary>Prüft anhand beobachtbaren Verhaltens, dass <paramref name="session"/> disposed wurde: Nach
    /// <see cref="Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession.Dispose"/> ist der Input-Stream
    /// geschlossen, ein Schreibversuch muss daher eine <see cref="ObjectDisposedException"/> werfen.</summary>
    /// <param name="session">Die Sitzung, deren Dispose-Zustand geprüft wird.</param>
    /// <param name="because">Begründung für die Assertion, falls sie fehlschlägt.</param>
    private static void AssertSessionDisposed(Softwareschmiede.Infrastructure.Terminal.ITerminalSession session, string because)
    {
        var act = () => session.InputStream.WriteByte(0);
        act.Should().Throw<ObjectDisposedException>(because);
    }

    /// <summary>
    /// Regressionstest für frühes Prozessende direkt nach dem Session-Start: Endet der Prozess (hier
    /// deterministisch per <c>Kill</c> erzwungen) sofort nach <see cref="AiExecutionService.StartTerminalSessionAsync"/>,
    /// muss der Exited-Handler anlaufen und die Session sauber aufräumen — es darf gar keine
    /// <see cref="ObjectDisposedException"/> auftreten (unabhängig vom Log-Level, mit dem eine trotzdem
    /// auftretende ObjectDisposedException protokolliert würde). Historisch deckte dieser Test die
    /// Race Condition zum inzwischen entfernten verzögerten Befehlsversand (<c>SendCommandDelayedAsync</c>)
    /// ab; seit Issue #271 startet die Session den Prozess direkt und es gibt keinen getippten
    /// Nachsende-Befehl mehr — die ODE-Regressionsabsicherung bleibt dennoch relevant.
    /// </summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_EarlyProcessend_NoWarningBecauseofClosedStream()
    {
        var loggerMock = new Mock<ILogger<AiExecutionService>>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var launcher = new SimulatedPseudoConsoleProcessLauncher(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance, Options.Create(new TerminalSessionOptions()));
        using var sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object, loggerMock.Object, launcher: launcher);

        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.SetupTerminalSpec("cmd.exe");

        // Bewusst auf jedem Log-Level geprüft (nicht nur Warning): Ziel des Fixes ist, dass die
        // ObjectDisposedException durch die Stornierung gar nicht erst auftritt — nicht nur, dass sie auf
        // einem anderen Level geloggt würde.
        var objectDisposedLogged = new TaskCompletionSource();
        loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => e is ObjectDisposedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => objectDisposedLogged.TrySetResult());

        var handle = await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath());

        var exitStatus = new TaskCompletionSource<CliProcessStatus>();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status != CliProcessStatus.Started)
                exitStatus.TrySetResult(status);
        };

        // Prozess deterministisch und sofort beenden — reproduziert das frühe Prozessende ohne auf
        // zufälliges, umgebungsabhängiges Timing angewiesen zu sein.
        try
        {
            if (!handle.Process.HasExited)
                handle.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        var completed = await Task.WhenAny(exitStatus.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().Be(exitStatus.Task, "der Exited-Handler muss nach dem Kill anlaufen und die Session aufräumen");

        // Kurz warten: kein nachträglicher Zugriff auf geschlossene Streams darf eine
        // ObjectDisposedException in die Log-Ausgabe schreiben.
        var finished = await Task.WhenAny(objectDisposedLogged.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        finished.Should().NotBe(objectDisposedLogged.Task,
            "das Aufräumen beim Prozessende darf keine ObjectDisposedException auslösen oder protokollieren");
    }

    /// <summary>Wird ein <see cref="SimulatedPseudoConsoleProcessLauncher"/> injiziert, muss
    /// <see cref="AiExecutionService.StartTerminalSessionAsync"/> ohne echtes ConPTY bis zum Status
    /// <see cref="CliProcessStatus.Started"/> gelangen (siehe docs/features/e2e-korrektur/requirement.md).</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_WithInjectedFakeLauncher_ReachesStarted()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var launcher = new SimulatedPseudoConsoleProcessLauncher(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance, Options.Create(new TerminalSessionOptions()));
        using var sut = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object, launcher: launcher);

        var workItemId = Guid.NewGuid();
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.SetupTerminalSpec("cmd.exe", "/c echo simuliert");

        var started = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == CliProcessStatus.Started)
                started.TrySetResult();
        };

        var handle = await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath());

        var finished = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        finished.Should().Be(started.Task, "der simulierte Launcher muss ohne echtes ConPTY bis zum Status Gestartet gelangen");

        try
        {
            if (!handle.Process.HasExited)
                handle.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>StartTerminalSessionAsync persistiert Ausgabe der PseudoConsoleSession automatisch im
    /// Aufgabenprotokoll, ohne dass ein TerminalControl gebunden ist.</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_PersistedSessionOutputAsCliOutput()
    {
        await using var provider = CreateCliOutputServiceProvider();

        var workItemId = Guid.NewGuid();
        var launcher = new FixedOutputPseudoConsoleProcessLauncher("erste zeile\nzweite zeile\n");
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.SetupTerminalSpec("echo", "irrelevant");

        var handle = await sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath());

        var entries = await WaitForCliOutputAsync(provider, workItemId, expectedCount: 2);
        entries.Select(e => e.Content).Should().Equal("erste zeile", "zweite zeile");
        entries.All(e => e.Type == LogEntryType.CliOutput).Should().BeTrue();

        try
        {
            if (!handle.Process.HasExited)
                handle.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Zwei parallele ConPTY-Sitzungen schreiben ihre Ausgabe in getrennte Aufgabenprotokolle.</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_ParallelWorkItems_DisconnectsLogAfterWorkItemId()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var workItemA = Guid.NewGuid();
        var workItemB = Guid.NewGuid();
        var outputs = new Dictionary<Guid, string>
        {
            [workItemA] = "a-1\na-2\n",
            [workItemB] = "b-1\nb-2\n"
        };
        var launcher = new OutputByTaskPseudoConsoleProcessLauncher(id => outputs[id], exitImmediately: false);
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);
        var pluginMock = CreateNoOpPlugin();

        var handleA = await sut.StartTerminalSessionAsync(workItemA, pluginMock.Object, Path.GetTempPath());
        var handleB = await sut.StartTerminalSessionAsync(workItemB, pluginMock.Object, Path.GetTempPath());

        var entriesA = await WaitForCliOutputAsync(provider, workItemA, expectedCount: 2);
        var entriesB = await WaitForCliOutputAsync(provider, workItemB, expectedCount: 2);

        entriesA.Select(e => e.Content).Should().Equal("a-1", "a-2");
        entriesB.Select(e => e.Content).Should().Equal("b-1", "b-2");

        KillIfRunning(handleA.Process);
        KillIfRunning(handleB.Process);
    }

    /// <summary>Ein Rate-Limit-Marker aus Session-Output erzeugt ueber den Writer-Pfad einen RateLimit-Eintrag.</summary>
    [OsInterfaceFact]
    public async Task AddCliOutputAsync_RateLimitMarkerOutConPtyOutput_CreatesRateLimitEntry()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var workItemId = Guid.NewGuid();
        var marker = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]";
        var launcher = new FixedOutputPseudoConsoleProcessLauncher(marker + "\n");
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);

        var handle = await sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath());

        var entries = await WaitForLogEntriesAsync(provider, workItemId, expectedCount: 2);
        entries.Should().Contain(e => e.Type == LogEntryType.CliOutput && e.Content == marker);
        entries.Should().Contain(e => e.Type == LogEntryType.RateLimit);

        KillIfRunning(handle.Process);
    }

    /// <summary>Eine Restzeile ohne abschliessenden Zeilentrenner wird ueber den Session-/Service-Pfad persistiert.</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_RemainderlineWithoutLineseparator_PersistedCliOutput()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var workItemId = Guid.NewGuid();
        var launcher = new FixedOutputPseudoConsoleProcessLauncher("letzte restzeile");
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);

        var handle = await sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath());

        var entries = await WaitForCliOutputAsync(provider, workItemId, expectedCount: 1);
        entries.Single().Content.Should().Be("letzte restzeile");

        KillIfRunning(handle.Process);
    }

    /// <summary>Der kontrollierte Prozessende-Pfad wartet auf den ReadLoop-Drain, bevor der Writer abgeschlossen wird.</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_ProcessEndBeforeReadLoopDrain_LosesTailOutputNot()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var workItemId = Guid.NewGuid();
        var launcher = new DelayedOutputPseudoConsoleProcessLauncher("tail-output\n", TimeSpan.FromMilliseconds(200));
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);

        await sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath());

        var entries = await WaitForCliOutputAsync(provider, workItemId, expectedCount: 1);
        entries.Single().Content.Should().Be("tail-output");
    }

    /// <summary>Regressionstest für den Early-Exit-Pfad in <see cref="AiExecutionService.StartTerminalSessionAsync"/>:
    /// Ist der Prozess bereits vor der Event-Verdrahtung beendet (das Session-<c>Exited</c> feuerte ohne
    /// Subscriber), muss der Exit-Code denselben Status-Mapping-Pfad wie ein reguläres Exited durchlaufen —
    /// ExitCode != 0 → <see cref="CliProcessStatus.Error"/> inkl. SystemMeldung-Protokolleintrag,
    /// ExitCode == 0 → <see cref="CliProcessStatus.Stopped"/>. Zuvor wurde auf diesem Pfad immer Stopped
    /// gemeldet und weder der Fehlerstatus noch der Fehler-Protokolleintrag erzeugt.</summary>
    [OsInterfaceTheory]
    [InlineData(0, CliProcessStatus.Stopped)]
    [InlineData(1, CliProcessStatus.Error)]
    public async Task StartTerminalSessionAsync_ProcessBeforeWiringCompleted_MapsExitCodeOnStatus(
        int exitCode,
        CliProcessStatus expectedStatus)
    {
        await using var provider = CreateWorkItemServiceProvider();
        var (workItemId, _) = await CreateStartedWorkItemWithActiveRunAsync(provider);
        var launcher = new AlreadyExitedProcessLauncher(exitCode);
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);

        var statusEvents = new List<CliProcessStatus>();
        sut.CliProcessStatusChanged += (_, status) => statusEvents.Add(status);

        await sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath());

        statusEvents.Should().Equal(
            new[] { expectedStatus },
            "der Early-Exit-Pfad muss denselben Endzustand wie ein reguläres Exited melden — ohne vorheriges Gestartet");
        sut.GetTerminalSession(workItemId).Should().BeNull("das Handle muss im Early-Exit-Pfad entfernt worden sein");

        if (expectedStatus == CliProcessStatus.Error)
        {
            var entries = await WaitForLogEntriesAsync(provider, workItemId, 1, LogEntryType.SystemMessage);
            entries.Should().Contain(
                e => e.Content.Contains($"ExitCode: {exitCode}"),
                "der Fehler-Pfad muss den Exit-Code als SystemMeldung-Protokolleintrag persistieren");
        }
    }

    /// <summary>Regressionstest für ein <see cref="ITerminalSession.Failed"/>, das zwischen Session-Erzeugung
    /// und Event-Verdrahtung feuert (Leseschleifen-Fehler bei noch laufendem Prozess): Der Fehlerzustand muss
    /// über <see cref="ITerminalSession.Failure"/> nachträglich erkannt und wie ein reguläres Failed behandelt
    /// werden — Status <see cref="CliProcessStatus.Error"/>, Handle entfernt, Session disposed. Zuvor blieb
    /// das Handle mit toter Leseschleife als „Gestartet" hängen.</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_FailedBeforeWiring_WillDetectedAndAsErrorReported()
    {
        await using var provider = CreateWorkItemServiceProvider();
        var (workItemId, _) = await CreateStartedWorkItemWithActiveRunAsync(provider);
        var launcher = new FailingReadSessionLauncher();
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);

        var statusEvents = new List<CliProcessStatus>();
        sut.CliProcessStatusChanged += (_, status) => statusEvents.Add(status);

        var handle = await sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath());

        try
        {
            statusEvents.Should().Equal(
                new[] { CliProcessStatus.Error },
                "das vor der Verdrahtung ausgelöste Failed muss erkannt und als Fehler gemeldet werden — kein Gestartet");
            sut.GetTerminalSession(workItemId).Should().BeNull("das Handle muss nach dem Session-Fehler entfernt worden sein");
            AssertSessionDisposed(handle.Session!, "der Fehler-Pfad muss die Session-Ressourcen aufräumen");

            var entries = await WaitForLogEntriesAsync(provider, workItemId, 1, LogEntryType.SystemMessage);
            entries.Should().Contain(
                e => e.Content.Contains("Laufzeitfehler"),
                "ein fataler Session-Fehler ohne Exit-Code muss als SystemMeldung-Protokolleintrag persistiert werden");
        }
        finally
        {
            KillIfRunning(handle.Process);
        }
    }

    /// <summary>Fehlschlägt die Leseschleife einer Terminal-Session zur Laufzeit (<see cref="ITerminalSession.Failed"/>
    /// nach der Event-Verdrahtung, Prozess läuft noch), muss der Dienst gemäß Plan
    /// <see cref="CliProcessStatus.Error"/> melden — ein fataler Session-Fehler ist kein reguläres Prozessende
    /// (zuvor wurde hier fälschlich Stopped gemeldet).</summary>
    [OsInterfaceFact]
    public async Task StartTerminalSessionAsync_SessionFailed_ReportsErrorInsteadStopped()
    {
        await using var provider = CreateWorkItemServiceProvider();
        var (workItemId, _) = await CreateStartedWorkItemWithActiveRunAsync(provider);
        var launcher = new DelayedFailingReadSessionLauncher(TimeSpan.FromMilliseconds(300));
        using var sut = TestAiExecutionServiceFactory.Create(
            provider.GetRequiredService<IServiceScopeFactory>(),
            launcher: launcher);

        var statusEvents = new List<CliProcessStatus>();
        var errorSignal = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            statusEvents.Add(status);
            if (status == CliProcessStatus.Error)
                errorSignal.TrySetResult();
        };

        var handle = await sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath());

        try
        {
            statusEvents.Should().Equal(
                new[] { CliProcessStatus.Started },
                "bei verzögertem Lesefehler ist der Start zunächst erfolgreich");

            var completed = await Task.WhenAny(errorSignal.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().Be(errorSignal.Task,
                "ein Laufzeitfehler der Leseschleife muss als Fehler gemeldet werden, nicht als Stopped");

            statusEvents.Should().Equal(new[] { CliProcessStatus.Started, CliProcessStatus.Error });
            sut.GetTerminalSession(workItemId).Should().BeNull("das Handle muss nach dem Session-Fehler entfernt worden sein");

            var entries = await WaitForLogEntriesAsync(provider, workItemId, 1, LogEntryType.SystemMessage);
            entries.Should().Contain(
                e => e.Content.Contains("Laufzeitfehler"),
                "ein fataler Session-Fehler ohne Exit-Code muss als SystemMeldung-Protokolleintrag persistiert werden");
        }
        finally
        {
            KillIfRunning(handle.Process);
        }
    }

    /// <summary>Ein zweiter Start bei laufendem Handle feuert CliLaufAktiv (nicht ein zweites Gestartet) —
    /// sowohl auf dem StartCliAsync- als auch auf dem StartTerminalSessionAsync-Pfad (Issue 342, H1).</summary>
    [Fact]
    public async Task SecondStart_FiresCliRunActive_InsteadStarted()
    {
        var workItemId = Guid.NewGuid();
        var pluginMock = CreateNoOpPlugin();

        var startedCount = 0;
        var runActiveCount = 0;
        _sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == CliProcessStatus.Started)
                startedCount++;
        };
        _sut.CliRunActive += _ => runActiveCount++;

        var handle = await _sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        startedCount.Should().Be(1);

        var handle2 = await _sut.StartCliAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        var handle3 = await _sut.StartTerminalSessionAsync(workItemId, pluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        handle2.Should().BeSameAs(handle, "der zweite Start liefert das bereits laufende Handle zurück");
        handle3.Should().BeSameAs(handle);
        startedCount.Should().Be(1, "bei Wiederverwendung darf kein zweites Gestartet-Ereignis feuern");
        runActiveCount.Should().Be(2, "beide Early-Returns melden den laufenden Prozess über CliLaufAktiv");
    }

    /// <summary>NotifyLaufAktiv feuert CliLaufAktiv nur, wenn für die Aufgabe ein Prozess läuft.</summary>
    [Fact]
    public async Task NotifyRunActive_FiresOnly_AtRunningProcess()
    {
        var workItemId = Guid.NewGuid();
        var runActiveCount = 0;
        _sut.CliRunActive += _ => runActiveCount++;

        _sut.NotifyRunActive(workItemId);
        runActiveCount.Should().Be(0, "ohne laufendes Handle ist NotifyLaufAktiv ein No-Op");

        await _sut.StartTerminalSessionAsync(workItemId, CreateNoOpPlugin().Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        _sut.NotifyRunActive(workItemId);

        runActiveCount.Should().Be(1);
    }

    /// <summary>GetLiveLaufStatus liefert null ohne laufendes Handle und bildet den Session-Status ab:
    /// laufend ohne/mit Session (Laeuft) bzw. WartetAufEingabe → die persistierten Substatus-Werte.</summary>
    [Fact]
    public void GetLiveRunStatus_FormsSessionStatusFrom()
    {
        var withoutHandle = Guid.NewGuid();
        _sut.GetLiveRunStatus(withoutHandle).Should().BeNull("ohne laufendes Handle gibt es keinen Live-Substatus");

        var runningWithoutSession = Guid.NewGuid();
        SchedulesHandle(runningWithoutSession, new CliProcessHandle(runningWithoutSession, System.Diagnostics.Process.GetCurrentProcess()));
        _sut.GetLiveRunStatus(runningWithoutSession).Should().Be(WorkItemRunStatus.Running);

        var runningWithSession = Guid.NewGuid();
        var session = TestPseudoConsoleSessionFactory.Create(
            new MemoryStream(),
            new MemoryStream(),
            NullLogger.Instance);
        SchedulesHandle(runningWithSession, new CliProcessHandle(runningWithSession, System.Diagnostics.Process.GetCurrentProcess()) { Session = session });
        try
        {
            _sut.GetLiveRunStatus(runningWithSession).Should().Be(WorkItemRunStatus.Running);
        }
        finally
        {
            session.Dispose();
        }
    }

    /// <summary>GetLaufendeAufgabenIds listet nur Aufgaben mit nachweislich laufendem Handle.</summary>
    [Fact]
    public void GetRunningWorkItemsIds_ListsOnlyRunningHandles()
    {
        var running = Guid.NewGuid();
        var notAssigned = Guid.NewGuid();

        SchedulesHandle(running, new CliProcessHandle(running, System.Diagnostics.Process.GetCurrentProcess()));
        // Ein Process-Objekt ohne zugeordneten Prozess wirft bei HasExited — wird als nicht laufend gewertet.
        SchedulesHandle(notAssigned, new CliProcessHandle(notAssigned, new System.Diagnostics.Process()));

        _sut.GetRunningWorkItemIds().Should().ContainSingle()
            .Which.Should().Be(running);
    }

    private void SchedulesHandle(Guid workItemId, CliProcessHandle handle)
    {
        var handlesField = typeof(AiExecutionService).GetField("_handles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_sut)!;
        handles[workItemId] = handle;
    }

    private static async Task<IReadOnlyList<Softwareschmiede.Domain.Entities.LogEntry>> WaitForCliOutputAsync(
        ServiceProvider provider,
        Guid workItemId,
        int expectedCount)
        => (await WaitForLogEntriesAsync(provider, workItemId, expectedCount, LogEntryType.CliOutput)).Where(e => e.Type == LogEntryType.CliOutput).ToList();

    private static async Task<IReadOnlyList<Softwareschmiede.Domain.Entities.LogEntry>> WaitForLogEntriesAsync(
        ServiceProvider provider,
        Guid workItemId,
        int expectedCount,
        LogEntryType? type = null)
    {
        for (var i = 0; i < 50; i++)
        {
            var entries = GetLogEntries(provider, workItemId, type);
            if (entries.Count >= expectedCount)
                return entries;

            await Task.Delay(100);
        }

        return GetLogEntries(provider, workItemId, type);
    }

    private static IReadOnlyList<Softwareschmiede.Domain.Entities.LogEntry> GetLogEntries(
        ServiceProvider provider,
        Guid workItemId,
        LogEntryType? type)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        var query = db.LogEntries
            .AsNoTracking()
            .Where(e => e.WorkItemId == workItemId);
        if (type is not null)
            query = query.Where(e => e.Type == type.Value);

        return query
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    private static ServiceProvider CreateCliOutputServiceProvider()
    {
        var databaseName = Guid.NewGuid().ToString();
        return new ServiceCollection()
            .AddDbContext<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddScoped<LogService>()
            .AddSingleton<ILogger<LogService>>(NullLogger<LogService>.Instance)
            .BuildServiceProvider();
    }

    private static Mock<IAiPlugin> CreateNoOpPlugin()
    {
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "echo",
                Arguments = "irrelevant",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        pluginMock.SetupTerminalSpec();
        return pluginMock;
    }

    private static Mock<IAiPlugin> CreatePlugin(System.Diagnostics.ProcessStartInfo processStartInfo)
    {
        var pluginMock = new Mock<IAiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processStartInfo);
        pluginMock.SetupTerminalSpec(processStartInfo.FileName, processStartInfo.Arguments);
        return pluginMock;
    }

    private static ServiceProvider CreateWorkItemServiceProvider()
    {
        var databaseName = Guid.NewGuid().ToString();
        return new ServiceCollection()
            .AddDbContext<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddScoped<TodoService>()
            .AddScoped<WorkItemService>()
            .AddScoped<LogService>()
            .AddSingleton<ILogger<TodoService>>(NullLogger<TodoService>.Instance)
            .AddSingleton<ILogger<WorkItemService>>(NullLogger<WorkItemService>.Instance)
            .AddSingleton<ILogger<LogService>>(NullLogger<LogService>.Instance)
            .BuildServiceProvider();
    }

    private static async Task<(Guid WorkItemId, Guid ProjectId)> CreateStartedWorkItemWithActiveRunAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        var projectId = Guid.NewGuid();
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Prozessende-Testprojekt",
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var workItemService = scope.ServiceProvider.GetRequiredService<WorkItemService>();
        var workItem = await workItemService.CreateAsync(projectId, "Prozessende-Aufgabe", "Beschreibung");
        await workItemService.StartAsync(workItem.Id, "feature/prozessende", Path.GetTempPath());
        await workItemService.SetActiveRunAsync(workItem.Id, "lauf-prozessende");

        return (workItem.Id, projectId);
    }

    private static async Task StartAndWaitForExitAsync(
        AiExecutionService sut,
        Guid workItemId,
        System.Diagnostics.ProcessStartInfo processStartInfo,
        CliProcessStatus expectedStatus)
    {
        var exitSignal = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == expectedStatus)
                exitSignal.TrySetResult();
        };

        await sut.StartCliAsync(workItemId, CreatePlugin(processStartInfo).Object, Path.GetTempPath());

        var completed = await Task.WhenAny(exitSignal.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().Be(exitSignal.Task);
    }

    private static async Task<WorkItem> WaitForWorkItemStatusAsync(
        ServiceProvider provider,
        Guid workItemId,
        WorkItemExecutionStatus expectedStatus)
    {
        for (var i = 0; i < 50; i++)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
            var workItem = await db.WorkItems.AsNoTracking().SingleAsync(a => a.Id == workItemId);
            if (workItem.ExecutionStatus == expectedStatus)
                return workItem;

            await Task.Delay(100);
        }

        await using var finalScope = provider.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        return await finalDb.WorkItems.AsNoTracking().SingleAsync(a => a.Id == workItemId);
    }

    private static void KillIfRunning(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static System.Diagnostics.Process StartTestProcess(bool exitImmediately)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = exitImmediately ? "/c exit 0" : "/c timeout /t 30 /nobreak > nul",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Testprozess konnte nicht gestartet werden.");

    /// <summary>Startet einen garantiert ~30 s laufenden Prozess, der nicht von stdin abhängt
    /// (<c>timeout</c> bricht unter umgeleitetem stdin sofort mit einem Fehlercode ab — <c>ping</c>
    /// ist dafür immun und bleibt zuverlässig am Leben, bis er gekillt wird).</summary>
    private static System.Diagnostics.Process StartLongRunningTestProcess()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 31 127.0.0.1 > nul",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Testprozess konnte nicht gestartet werden.");

    private sealed class FixedOutputPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly string _output;

        public FixedOutputPseudoConsoleProcessLauncher(string output)
        {
            _output = output;
        }

        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(
            Guid workItemId,
            TerminalSessionStartSpec spec,
            ITerminalOutputSink? outputSink = null)
        {
            var process = StartTestProcess(exitImmediately: false);

            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_output)),
                new PseudoConsoleSessionContext
                {
                    Logger = NullLogger<PseudoConsoleSession>.Instance,
                    OutputSink = outputSink,
                });

            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }

    private sealed class OutputByTaskPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly Func<Guid, string> _outputFactory;
        private readonly bool _exitImmediately;

        public OutputByTaskPseudoConsoleProcessLauncher(Func<Guid, string> outputFactory, bool exitImmediately)
        {
            _outputFactory = outputFactory;
            _exitImmediately = exitImmediately;
        }

        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(
            Guid workItemId,
            TerminalSessionStartSpec spec,
            ITerminalOutputSink? outputSink = null)
        {
            var process = StartTestProcess(_exitImmediately);
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_outputFactory(workItemId))),
                new PseudoConsoleSessionContext
                {
                    Logger = NullLogger<PseudoConsoleSession>.Instance,
                    OutputSink = outputSink,
                });

            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }

    private sealed class DelayedOutputPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly string _output;
        private readonly TimeSpan _readDelay;

        public DelayedOutputPseudoConsoleProcessLauncher(string output, TimeSpan readDelay)
        {
            _output = output;
            _readDelay = readDelay;
        }

        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(
            Guid workItemId,
            TerminalSessionStartSpec spec,
            ITerminalOutputSink? outputSink = null)
        {
            var process = StartTestProcess(exitImmediately: true);
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new DelayedContentStream(_output, _readDelay),
                new PseudoConsoleSessionContext
                {
                    Logger = NullLogger<PseudoConsoleSession>.Instance,
                    OutputSink = outputSink,
                });

            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }

    /// <summary>Launcher-Double, das einen bereits beendeten Prozess in die Session legt (wartet in
    /// <see cref="Start"/> synchron auf das Prozessende). Reproduziert deterministisch den Early-Exit-Pfad
    /// in <see cref="AiExecutionService.StartTerminalSessionAsync"/>: Der Prozess ist bei der
    /// Event-Verdrahtung bereits beendet und das Session-Exited feuerte ohne Subscriber.</summary>
    private sealed class AlreadyExitedProcessLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly int _exitCode;

        public AlreadyExitedProcessLauncher(int exitCode)
        {
            _exitCode = exitCode;
        }

        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(
            Guid workItemId,
            TerminalSessionStartSpec spec,
            ITerminalOutputSink? outputSink = null)
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c exit {_exitCode}",
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("Testprozess konnte nicht gestartet werden.");
            process.WaitForExit();

            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new MemoryStream(),
                new PseudoConsoleSessionContext
                {
                    Logger = NullLogger<PseudoConsoleSession>.Instance,
                    OutputSink = outputSink,
                });

            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }

    /// <summary>Launcher-Double, dessen Session-Leseschleife sofort mit einem IOException aussteigt, während
    /// der Prozess weiterläuft — reproduziert ein <see cref="ITerminalSession.Failed"/>, das bereits vor der
    /// Event-Verdrahtung in <see cref="AiExecutionService.StartTerminalSessionAsync"/> ausgelöst wurde.
    /// <see cref="Start"/> wartet deterministisch auf <see cref="ITerminalSession.Failure"/>, damit das
    /// Timing garantiert ist und der Test nicht von zufälliger Scheduling-Reihenfolge abhängt.</summary>
    private sealed class FailingReadSessionLauncher : IPseudoConsoleProcessLauncher
    {
        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(
            Guid workItemId,
            TerminalSessionStartSpec spec,
            ITerminalOutputSink? outputSink = null)
        {
            var process = StartLongRunningTestProcess();
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new ThrowingReadStream(),
                new PseudoConsoleSessionContext
                {
                    Logger = NullLogger<PseudoConsoleSession>.Instance,
                    OutputSink = outputSink,
                });

            if (!System.Threading.SpinWait.SpinUntil(() => session.Failure is not null, TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("Die Leseschleife der Testsession hat den simulierten Fehler nicht innerhalb des Timeouts gemeldet.");

            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }

    /// <summary>Launcher-Double wie <see cref="FailingReadSessionLauncher"/>, bei dem der Lesefehler erst
    /// nach einer Verzögerung ausgelöst wird — damit das <see cref="ITerminalSession.Failed"/> erst
    /// <em>nach</em> der Event-Verdrahtung in <see cref="AiExecutionService.StartTerminalSessionAsync"/>
    /// feuert und der reguläre Failed-Ereignis-Pfad (statt des Failure-Rechecks) getroffen wird.</summary>
    private sealed class DelayedFailingReadSessionLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly TimeSpan _delay;

        public DelayedFailingReadSessionLauncher(TimeSpan delay)
        {
            _delay = delay;
        }

        public bool IsPseudoTerminal => false;

        public TerminalSessionStartResult Start(
            Guid workItemId,
            TerminalSessionStartSpec spec,
            ITerminalOutputSink? outputSink = null)
        {
            var process = StartLongRunningTestProcess();
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new DelayedThrowingReadStream(_delay),
                new PseudoConsoleSessionContext
                {
                    Logger = NullLogger<PseudoConsoleSession>.Instance,
                    OutputSink = outputSink,
                });

            return new TerminalSessionStartResult(process, session, IsPseudoTerminal: false);
        }
    }

    /// <summary>Stream, dessen Lesevorgang sofort eine Exception wirft (simulierter Leseschleifen-Fehler).</summary>
    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new IOException("Simulierter Lesefehler des Terminal-Output-Streams");

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream, dessen Lesevorgang erst nach einer Verzögerung eine Exception wirft — simuliert einen
    /// Leseschleifen-Fehler zur Laufzeit (nach der Event-Verdrahtung des Aufrufers).</summary>
    private sealed class DelayedThrowingReadStream : Stream
    {
        private readonly TimeSpan _delay;

        public DelayedThrowingReadStream(TimeSpan delay)
        {
            _delay = delay;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken);
            throw new IOException("Simulierter Lesefehler des Terminal-Output-Streams");
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DelayedContentStream : Stream
    {
        private readonly byte[] _content;
        private readonly TimeSpan _readDelay;
        private bool _served;

        public DelayedContentStream(string content, TimeSpan readDelay)
        {
            _content = System.Text.Encoding.UTF8.GetBytes(content);
            _readDelay = readDelay;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_served)
                return 0;

            _served = true;
            await Task.Delay(_readDelay, cancellationToken);
            _content.CopyTo(buffer);
            return _content.Length;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
