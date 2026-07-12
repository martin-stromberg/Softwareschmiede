using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den KiAusfuehrungsService.</summary>
public sealed class KiAusfuehrungsServiceTests : IDisposable
{
    private readonly KiAusfuehrungsService _sut;

    /// <summary>KiAusfuehrungsServiceTests.</summary>
    public KiAusfuehrungsServiceTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
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
    [Fact]
    public async Task TestCliStartAsync()
    {
        // Arrange
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Act
        var handle = await _sut.StartCliAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        // Assert
        handle.Should().NotBeNull();
        handle.AufgabeId.Should().Be(aufgabeId);
        _sut.IsRunning(aufgabeId).Should().BeTrue();
        _sut.GetRunningCount().Should().BeGreaterThan(0);
    }

    /// <summary>StartCliAsync returns handle when plugin provides valid ProcessStartInfo.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo()
    {
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var handle = await _sut.StartCliAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        handle.Should().NotBeNull();
        handle.AufgabeId.Should().Be(aufgabeId);
    }

    /// <summary>GetPseudoConsoleSession gibt null zurück wenn keine ConPTY-Session gestartet wurde.</summary>
    [Fact]
    public void GetPseudoConsoleSession_GibtNull_OhneSession()
    {
        _sut.GetPseudoConsoleSession(Guid.NewGuid()).Should().BeNull();
    }

    /// <summary>
    /// Regressionstest für die Race Condition beim App-Shutdown: Wenn ein CLI-Prozess mit
    /// Fehler-ExitCode beendet wird, während der ScopeFactory-Zugriff (z. B. weil der
    /// IServiceProvider während des Anwendungs-Shutdowns bereits disposed wurde) eine
    /// ObjectDisposedException wirft, darf PersistFehlgeschlagenAsync diese nicht unbehandelt
    /// weiterwerfen (führte zuvor zu einer TaskScheduler.UnobservedTaskException).
    /// </summary>
    [Fact]
    public async Task ProcessExited_ScopeFactoryDisposed_PersistiertNichtUndWirftNicht()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(() => new ObjectDisposedException("IServiceProvider"));

        using var sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);

        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 1",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var statusEvents = new List<CliProcessStatus>();
        var fehlerSignal = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            statusEvents.Add(status);
            if (status == CliProcessStatus.Fehler)
            {
                fehlerSignal.TrySetResult();
            }
        };

        await sut.StartCliAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var completed = await Task.WhenAny(fehlerSignal.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().Be(fehlerSignal.Task, "der Exited-Handler sollte trotz disposed ScopeFactory ausgelöst werden, ohne die Anwendung zum Absturz zu bringen");

        statusEvents.Should().Contain(CliProcessStatus.Fehler);
    }

    /// <summary>
    /// Wenn ein Abonnent von CliProcessStatusChanged im Exited-Handler von StartCliAsync eine Exception
    /// wirft, muss diese geloggt werden, ohne die Multicast-Kette oder die Anwendung zum Absturz zu bringen.
    /// </summary>
    [Fact]
    public async Task ProcessExited_SubscriberThrows_LogsAndDoesNotCrash()
    {
        // Klassischer Pfad: der Plugin-ProcessStartInfo wird direkt als der überwachte Prozess gestartet
        // ("cmd.exe /c exit 0" terminiert diesen Prozess sofort selbst).
        await AssertSubscriberExceptionIsLoggedAsync(
            (sut, aufgabeId, plugin) => sut.StartCliAsync(aufgabeId, plugin, Path.GetTempPath()),
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
    /// Wenn ein Abonnent von CliProcessStatusChanged im ConPTY-Exited-Handler von StartWithPseudoConsoleAsync
    /// eine Exception wirft, muss diese geloggt werden, ohne die Anwendung zum Absturz zu bringen.
    /// </summary>
    [Fact]
    public async Task ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash()
    {
        // ConPTY-Pfad: der überwachte Prozess ist die dauerhafte, interaktive äußere cmd.exe-Shell; der
        // Plugin-Befehl wird lediglich als Text in deren Eingabe getippt (SendCommandDelayedAsync). Ein
        // getipptes "cmd.exe /c exit 0" würde nur einen verschachtelten Unterprozess starten und beenden,
        // ohne dass die äußere Shell selbst terminiert. Nur ein direktes "exit"-Kommando beendet die Shell.
        await AssertSubscriberExceptionIsLoggedAsync(
            (sut, aufgabeId, plugin) => sut.StartWithPseudoConsoleAsync(aufgabeId, plugin, Path.GetTempPath()),
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "exit",
                Arguments = "0",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Gemeinsames Setup für <see cref="ProcessExited_SubscriberThrows_LogsAndDoesNotCrash"/> und
    /// <see cref="ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash"/>: Erstellt Mocks für Logger,
    /// ScopeFactory und Plugin, registriert einen Subscriber, der im Exited-Handler eine Exception wirft,
    /// ruft den übergebenen Start-Vorgang auf und verifiziert, dass die Exception geloggt wird.
    /// </summary>
    /// <param name="startAsync">Der auszuführende Start-Aufruf (StartCliAsync oder StartWithPseudoConsoleAsync).</param>
    /// <param name="pluginProcessStartInfo">Der vom gemockten Plugin gelieferte Befehl, der den überwachten Prozess
    /// (klassisch) bzw. die getippte Konsoleneingabe (ConPTY) tatsächlich beendet.</param>
    /// <param name="timeout">Maximale Wartezeit auf das Logging der Exception.</param>
    private static async Task AssertSubscriberExceptionIsLoggedAsync(
        Func<KiAusfuehrungsService, Guid, IKiPlugin, Task> startAsync,
        System.Diagnostics.ProcessStartInfo pluginProcessStartInfo,
        TimeSpan timeout)
    {
        var loggerMock = new Mock<ILogger<KiAusfuehrungsService>>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        using var sut = new KiAusfuehrungsService(loggerMock.Object, NullLoggerFactory.Instance, scopeFactoryMock.Object);

        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pluginProcessStartInfo);

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
            // Nur beim Exited-Handler werfen (Gestoppt/Fehler), nicht beim initialen Gestartet-Event,
            // das außerhalb des try-catch-geschützten Exited-Handlers liegt.
            if (status != CliProcessStatus.Gestartet)
                throw new InvalidOperationException("Simulierter Subscriber-Fehler");
        };

        await startAsync(sut, aufgabeId, pluginMock.Object);

        var finished = await Task.WhenAny(errorLogged.Task, Task.Delay(timeout));
        finished.Should().Be(errorLogged.Task, "der Exited-Handler muss die Exception des werfenden Subscribers loggen, statt die Anwendung abstürzen zu lassen");
    }

    /// <summary>Beendet der CLI-Prozess einer ConPTY-Sitzung (hier über <see cref="KiAusfuehrungsService.StopCliAsync"/>),
    /// muss <see cref="KiAusfuehrungsService.HandleProcessExited"/> die zugehörige <see cref="PseudoConsoleSession"/>
    /// disposen (Leseschleife wird abgebrochen, Streams werden geschlossen), bevor das Handle aus <c>_handles</c>
    /// entfernt wird.</summary>
    [Fact]
    public async Task KiAusfuehrungsService_HandleProcessExited_DisposesSession()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        using var sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);

        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        await sut.StartWithPseudoConsoleAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());
        var session = sut.GetPseudoConsoleSession(aufgabeId);
        session.Should().NotBeNull("StartWithPseudoConsoleAsync muss eine PseudoConsoleSession im Handle hinterlegen");

        var gestoppt = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == CliProcessStatus.Gestoppt)
                gestoppt.TrySetResult();
        };

        await sut.StopCliAsync(aufgabeId);

        var finished = await Task.WhenAny(gestoppt.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        finished.Should().Be(gestoppt.Task, "StopCliAsync muss letztlich zu HandleProcessExited und damit zum Gestoppt-Status führen");

        AssertSessionDisposed(session!, "HandleProcessExited muss die PseudoConsoleSession disposen");
        sut.GetPseudoConsoleSession(aufgabeId).Should().BeNull("nach HandleProcessExited darf das Handle nicht mehr in _handles vorhanden sein");
    }

    /// <summary>Ruft man <see cref="KiAusfuehrungsService.Dispose"/> auf, während noch eine ConPTY-Sitzung läuft,
    /// müssen deren Prozess beendet und die zugehörige <see cref="PseudoConsoleSession"/> disposed werden (Leseschleife
    /// abgebrochen, Streams geschlossen) — keine verwaisten Leseschleifen nach dem Beenden des Service.</summary>
    [Fact]
    public async Task KiAusfuehrungsService_Dispose_CancelsAllSessions()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);

        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        await sut.StartWithPseudoConsoleAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());
        var session = sut.GetPseudoConsoleSession(aufgabeId);
        session.Should().NotBeNull("StartWithPseudoConsoleAsync muss eine PseudoConsoleSession im Handle hinterlegen");

        sut.Dispose();

        AssertSessionDisposed(session!, "KiAusfuehrungsService.Dispose() muss alle noch laufenden PseudoConsoleSession-Instanzen disposen");
    }

    /// <summary>Prüft anhand beobachtbaren Verhaltens, dass <paramref name="session"/> disposed wurde: Nach
    /// <see cref="Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession.Dispose"/> ist der Input-Stream
    /// geschlossen, ein Schreibversuch muss daher eine <see cref="ObjectDisposedException"/> werfen.</summary>
    /// <param name="session">Die Sitzung, deren Dispose-Zustand geprüft wird.</param>
    /// <param name="because">Begründung für die Assertion, falls sie fehlschlägt.</param>
    private static void AssertSessionDisposed(Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession session, string because)
    {
        var act = () => session.InputStream.WriteByte(0);
        act.Should().Throw<ObjectDisposedException>(because);
    }

    /// <summary>
    /// Regressionstest für die Race Condition zwischen dem verzögerten Plugin-Befehlsversand
    /// (<c>SendCommandDelayedAsync</c>, 300 ms Verzögerung vor dem Schreiben in den ConPTY-Input-Stream) und
    /// dem Dispose der <see cref="Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession"/> beim Beenden
    /// des ConPTY-Kindprozesses: Endet der Prozess (hier deterministisch per <c>Kill</c> erzwungen, statt auf
    /// das timing-abhängige, nur in bestimmten Umgebungen reproduzierbare frühe ConPTY-Prozessende zu warten),
    /// bevor die Verzögerung abgelaufen ist, darf kein Zugriff auf den bereits geschlossenen Input-Stream
    /// erfolgen. Der ausstehende Sendevorgang muss über den mit dem Prozess-Exit verknüpften <c>SendCts</c>
    /// storniert werden — es darf gar keine <see cref="ObjectDisposedException"/> auftreten (unabhängig vom
    /// Log-Level, mit dem eine trotzdem auftretende ObjectDisposedException protokolliert würde).
    /// </summary>
    [Fact]
    public async Task StartWithPseudoConsoleAsync_ProzessEndetVorVerzoegertemSenden_KeineWarnungWegenGeschlossenemStream()
    {
        var loggerMock = new Mock<ILogger<KiAusfuehrungsService>>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        using var sut = new KiAusfuehrungsService(loggerMock.Object, NullLoggerFactory.Instance, scopeFactoryMock.Object);

        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo Test",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        // Bewusst auf jedem Log-Level geprüft (nicht nur Warning): Ziel des Fixes ist, dass die
        // ObjectDisposedException durch die Stornierung gar nicht erst auftritt — nicht nur, dass sie auf
        // einem anderen Level geloggt würde.
        var objectDisposedGeloggt = new TaskCompletionSource();
        loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => e is ObjectDisposedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => objectDisposedGeloggt.TrySetResult());

        var handle = await sut.StartWithPseudoConsoleAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var exitStatus = new TaskCompletionSource<CliProcessStatus>();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status != CliProcessStatus.Gestartet)
                exitStatus.TrySetResult(status);
        };

        // Prozess deterministisch und sofort beenden, deutlich vor Ablauf der 300ms-Verzögerung in
        // SendCommandDelayedAsync — reproduziert die Race Condition ohne auf zufälliges, umgebungsabhängiges
        // frühes ConPTY-Prozessende angewiesen zu sein. In Umgebungen, in denen der ConPTY-Kindprozess
        // (wie in e2e-timeout-analyse.md dokumentiert) bereits von selbst beendet ist, bevor Kill aufgerufen
        // wird, ist das Ziel (Prozessende vor Ablauf der Verzögerung) ohnehin schon erreicht.
        try
        {
            if (!handle.Process.HasExited)
                handle.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        var exited = await Task.WhenAny(exitStatus.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        exited.Should().Be(exitStatus.Task, "der Exited-Handler muss nach dem Kill anlaufen und SendCts stornieren");

        // Über die 300ms-Verzögerung hinaus warten: Ohne den Fix hätte SendCommandDelayedAsync jetzt längst
        // versucht, in den bereits geschlossenen Input-Stream zu schreiben und eine ObjectDisposedException geloggt.
        var finished = await Task.WhenAny(objectDisposedGeloggt.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        finished.Should().NotBe(objectDisposedGeloggt.Task,
            "der verzögerte Sendevorgang muss beim Prozess-Exit über SendCts storniert werden, sodass gar keine ObjectDisposedException auftritt");
    }

    /// <summary>Wird ein <see cref="SimulatedPseudoConsoleProcessLauncher"/> injiziert, muss
    /// <see cref="KiAusfuehrungsService.StartWithPseudoConsoleAsync"/> ohne echtes ConPTY bis zum Status
    /// <see cref="CliProcessStatus.Gestartet"/> gelangen (siehe docs/features/e2e-korrektur/requirement.md).</summary>
    [Fact]
    public async Task StartWithPseudoConsoleAsync_MitInjiziertemFakeLauncher_ErreichtGestartet()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var launcher = new SimulatedPseudoConsoleProcessLauncher(NullLogger<SimulatedPseudoConsoleProcessLauncher>.Instance, NullLoggerFactory.Instance);
        using var sut = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object, launcher);

        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c echo simuliert",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var gestartet = new TaskCompletionSource();
        sut.CliProcessStatusChanged += (_, status) =>
        {
            if (status == CliProcessStatus.Gestartet)
                gestartet.TrySetResult();
        };

        var handle = await sut.StartWithPseudoConsoleAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var finished = await Task.WhenAny(gestartet.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        finished.Should().Be(gestartet.Task, "der simulierte Launcher muss ohne echtes ConPTY bis zum Status Gestartet gelangen");

        try
        {
            if (!handle.Process.HasExited)
                handle.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
