using FluentAssertions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="TerminalSessionDiagnostics"/>: Preflight-Einzelchecks und die
/// abgeleitete Backend-Empfehlung.</summary>
public sealed class TerminalSessionDiagnosticsTests
{
    private readonly TerminalSessionDiagnostics _sut = new(new TerminalSessionOptions());

    /// <summary>Bei auflösbarer Executable und verfügbarer PTY wird Pty empfohlen und die CLI-Health-Probe aufgerufen.</summary>
    [Fact]
    public async Task RunPreflightAsync_AllesOk_EmpfiehltPtyUndRuftHealthCheck()
    {
        var healthCheckCalled = false;
        var resolution = ResolvedResolution(TerminalExecutableStatus.Direct);

        var result = await _sut.RunPreflightAsync(
            CreateSpec(),
            resolution,
            _ =>
            {
                healthCheckCalled = true;
                return Task.FromResult(true);
            },
            forcePtyUnavailable: false,
            CancellationToken.None);

        healthCheckCalled.Should().BeTrue();
        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Pty);
        result.PtyVerfuegbar.Should().BeTrue();
        result.Checks.Should().Contain(c => c.Name == "Executable" && c.Ok);
    }

    /// <summary>Der Test-Override zwingt den PTY-Check auf false und verschiebt die Empfehlung auf Pipe.</summary>
    [Fact]
    public async Task RunPreflightAsync_ForcePtyUnavailable_EmpfiehltPipe()
    {
        var result = await _sut.RunPreflightAsync(
            CreateSpec(),
            ResolvedResolution(TerminalExecutableStatus.Direct),
            null,
            forcePtyUnavailable: true,
            CancellationToken.None);

        result.PtyVerfuegbar.Should().BeFalse();
        result.Checks.Should().Contain(c => c.Name == "ConPTY-Verfügbarkeit" && !c.Ok);
        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Pipe);
    }

    /// <summary>Bei RequiresPty ohne PTY-Verfügbarkeit ist die Empfehlung Fehler.</summary>
    [Fact]
    public async Task RunPreflightAsync_RequiresPtyOhnePty_EmpfiehltFehler()
    {
        var spec = CreateSpec() with { Capabilities = TerminalProviderCapabilities.RequiresPty | TerminalProviderCapabilities.SupportsPty };

        var result = await _sut.RunPreflightAsync(
            spec,
            ResolvedResolution(TerminalExecutableStatus.Direct),
            null,
            forcePtyUnavailable: true,
            CancellationToken.None);

        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Fehler);
    }

    /// <summary>Bei nicht auflösbarer Executable wird der Health-Check nicht aufgerufen und die Empfehlung ist Fehler.</summary>
    [Theory]
    [InlineData(TerminalExecutableStatus.NotFound)]
    [InlineData(TerminalExecutableStatus.NotExecutable)]
    public async Task RunPreflightAsync_ExecutableNichtStartbar_FehlerUndKeinHealthCheck(TerminalExecutableStatus status)
    {
        var healthCheckCalled = false;
        var resolution = new TerminalExecutableResolution(CreateSpec(), status, null, "nicht gefunden");

        var result = await _sut.RunPreflightAsync(
            CreateSpec(),
            resolution,
            _ =>
            {
                healthCheckCalled = true;
                return Task.FromResult(true);
            },
            forcePtyUnavailable: false,
            CancellationToken.None);

        healthCheckCalled.Should().BeFalse();
        result.Checks.Should().Contain(c => c.Name == "Executable" && !c.Ok);
        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Fehler);
    }

    /// <summary>Ein Plugin ohne SupportsPty-Flag bekommt auch bei verfügbarer PTY die Empfehlung Pipe —
    /// dieselbe Entscheidung, die TerminalSessionService.SelectBackend daraus ableitet.</summary>
    [Fact]
    public async Task RunPreflightAsync_OhneSupportsPty_EmpfiehltPipe()
    {
        var spec = CreateSpec() with { Capabilities = TerminalProviderCapabilities.None };

        var result = await _sut.RunPreflightAsync(
            spec,
            ResolvedResolution(TerminalExecutableStatus.Direct),
            null,
            forcePtyUnavailable: false,
            CancellationToken.None);

        result.PtyVerfuegbar.Should().BeTrue();
        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Pipe);
    }

    /// <summary>Der Terminalgrößen-Check muss dieselbe Grenze wie die Hartvalidierung in
    /// <c>TerminalSessionService.StartAsync</c> verwenden (1..short.MaxValue): Ein überlaufender
    /// Wert würde beim Start hart scheitern und darf im Preflight nicht als OK gemeldet werden.</summary>
    [Theory]
    [InlineData(220, 50, true)]
    [InlineData(short.MaxValue, short.MaxValue, true)]
    [InlineData(40000, 50, false)]
    [InlineData(220, 40000, false)]
    [InlineData(0, 50, false)]
    [InlineData(220, 0, false)]
    public async Task RunPreflightAsync_Terminalgroesse_CheckSpiegeltHartvalidierung(int cols, int rows, bool erwartetOk)
    {
        var sut = new TerminalSessionDiagnostics(new TerminalSessionOptions { DefaultCols = cols, DefaultRows = rows });

        var result = await sut.RunPreflightAsync(
            CreateSpec(),
            ResolvedResolution(TerminalExecutableStatus.Direct),
            null,
            forcePtyUnavailable: false,
            CancellationToken.None);

        result.Checks.Should().Contain(c => c.Name == "Terminalgröße" && c.Ok == erwartetOk,
            $"die Terminalgröße {cols}x{rows} muss synchron zur Hartvalidierung bewertet werden");
    }

    /// <summary>Ein false meldender Health-Check erzeugt einen nicht-fatalen fehlgeschlagenen Check-Eintrag.</summary>
    [Fact]
    public async Task RunPreflightAsync_HealthCheckFalse_NichtFatalerFehlerEintrag()
    {
        var result = await _sut.RunPreflightAsync(
            CreateSpec(),
            ResolvedResolution(TerminalExecutableStatus.Direct),
            _ => Task.FromResult(false),
            forcePtyUnavailable: false,
            CancellationToken.None);

        result.Checks.Should().Contain(c => c.Name == "CLI-Health" && !c.Ok);
        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Pty);
    }

    /// <summary>Eine im Health-Check geworfene Exception wird als nicht-fataler Check-Eintrag abgefangen.</summary>
    [Fact]
    public async Task RunPreflightAsync_HealthCheckWirftException_NichtFatalerFehlerEintrag()
    {
        var result = await _sut.RunPreflightAsync(
            CreateSpec(),
            ResolvedResolution(TerminalExecutableStatus.Direct),
            _ => throw new InvalidOperationException("kaputt"),
            forcePtyUnavailable: false,
            CancellationToken.None);

        result.Checks.Should().Contain(c => c.Name == "CLI-Health" && !c.Ok);
        result.BackendEmpfehlung.Should().Be(TerminalBackendEmpfehlung.Pty);
    }

    private static TerminalSessionStartSpec CreateSpec()
        => new() { FileName = "cli.exe", PluginName = "TestPlugin" };

    private static TerminalExecutableResolution ResolvedResolution(TerminalExecutableStatus status)
        => new(CreateSpec(), status, "C:\\tools\\cli.exe", null);
}
