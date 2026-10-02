using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="TerminalSessionService"/>: Backend-Wahl (PTY/Pipe/E2E),
/// Fehlerfälle der Executable-Auflösung und die <c>[Terminal-Diagnose]</c>-Markerzeile.</summary>
public sealed class TerminalSessionServiceTests : IDisposable
{
    private readonly RecordingLauncher _ptyLauncher = new() { IsPseudoTerminal = true };
    private readonly RecordingLauncher _pipeLauncher = new() { IsPseudoTerminal = false };
    private readonly CollectingSink _sink = new();
    private readonly ServiceProvider _provider;
    private readonly TerminalSessionService _sut;

    /// <summary>TerminalSessionServiceTests.</summary>
    public TerminalSessionServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _provider = new ServiceCollection()
            .AddDbContext<SoftwareschmiededDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddScoped<AppEinstellungService>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
        _sut = CreateService(Options.Create(new TerminalSessionOptions()));
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _ptyLauncher.DisposeSessions();
        _pipeLauncher.DisposeSessions();
        _provider.Dispose();
    }

    /// <summary>Eine Spec ohne FileName wird mit ArgumentException abgelehnt.</summary>
    [Fact]
    public async Task StartAsync_SpecOhneFileName_WirftArgumentException()
    {
        var act = () => _sut.StartAsync(Guid.NewGuid(), new TerminalSessionStartSpec { FileName = "" }, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Eine nicht auffindbare Executable führt zu InvalidOperationException inkl. Diagnose-Marker — ohne Pipe-Fallback.</summary>
    [Fact]
    public async Task StartAsync_ExecutableNichtGefunden_WirftMitMarkerUndDiagnose()
    {
        var spec = CreateSpec(fileName: "gibtes-garantiert-nicht-0815.exe");

        var act = () => _sut.StartAsync(Guid.NewGuid(), spec, _sink, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _sink.Text.Should().Contain("[Terminal-Diagnose]");
        _ptyLauncher.Aufrufe.Should().Be(0);
        _pipeLauncher.Aufrufe.Should().Be(0);
    }

    /// <summary>Eine gefundene, aber nicht ausführbare Datei (.ps1) schlägt ebenfalls ohne Fallback fehl.</summary>
    [Fact]
    public async Task StartAsync_ExecutableNichtAusfuehrbar_WirftMitMarkerUndDiagnose()
    {
        var script = Path.Combine(Path.GetTempPath(), $"nicht-ausfuehrbar-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(script, "Write-Host x");
        try
        {
            var act = () => _sut.StartAsync(Guid.NewGuid(), CreateSpec(fileName: script), _sink, null, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            _sink.Text.Should().Contain("[Terminal-Diagnose]");
            _ptyLauncher.Aufrufe.Should().Be(0);
            _pipeLauncher.Aufrufe.Should().Be(0);
        }
        finally
        {
            File.Delete(script);
        }
    }

    /// <summary>Ein .cmd-Shim wird vor dem Launcher-Aufruf zu cmd.exe /d /s /c normalisiert.</summary>
    [Fact]
    public async Task StartAsync_CmdShimSpec_LauncherErhaeltNormalisierteSpec()
    {
        var shim = Path.Combine(Path.GetTempPath(), $"shim-{Guid.NewGuid():N}.cmd");
        await File.WriteAllTextAsync(shim, "@echo off");
        try
        {
            var result = await _sut.StartAsync(Guid.NewGuid(), CreateSpec(fileName: shim), _sink, null, CancellationToken.None);

            _ptyLauncher.Aufrufe.Should().Be(1, "bei verfügbarer PTY muss der PTY-Launcher verwendet werden");
            _ptyLauncher.LetzteSpec.Should().NotBeNull();
            _ptyLauncher.LetzteSpec!.FileName.Should().Be("cmd.exe");
            _ptyLauncher.LetzteSpec.Arguments.Should().Be($"/d /s /c \"{shim}\"");
            result.IsPseudoTerminal.Should().BeTrue();
        }
        finally
        {
            File.Delete(shim);
        }
    }

    /// <summary>Der Health-Check-Delegate wird an den Preflight durchgereicht (bei auflösbarer Executable).</summary>
    [Fact]
    public async Task StartAsync_HealthCheckWirdAnPreflightDurchgereicht()
    {
        var healthCheckCalled = false;

        await _sut.StartAsync(
            Guid.NewGuid(),
            CreateSpec(),
            null,
            _ =>
            {
                healthCheckCalled = true;
                return Task.FromResult(true);
            },
            CancellationToken.None);

        healthCheckCalled.Should().BeTrue();
        _ptyLauncher.Aufrufe.Should().Be(1);
    }

    /// <summary>Ein Plugin ohne SupportsPty-Flag läuft auf dem Pipe-Backend inkl. Diagnose-Marker.</summary>
    [Fact]
    public async Task StartAsync_OhneSupportsPty_PipeMitDiagnose()
    {
        var spec = CreateSpec() with { Capabilities = TerminalProviderCapabilities.None };

        var result = await _sut.StartAsync(Guid.NewGuid(), spec, _sink, null, CancellationToken.None);

        _pipeLauncher.Aufrufe.Should().Be(1);
        _ptyLauncher.Aufrufe.Should().Be(0);
        result.IsPseudoTerminal.Should().BeFalse();
        _sink.Text.Should().Contain("[Terminal-Diagnose]");
    }

    /// <summary>Der Test-Override Terminal.ForcePtyUnavailable erzwingt den Pipe-Fallback mit Diagnose-Marker.</summary>
    [Fact]
    public async Task StartAsync_ForcePtyUnavailable_PipeMitDiagnoseMarker()
    {
        await using (var scope = _provider.CreateAsyncScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            await settings.SetSettingAsync(TerminalSessionService.ForcePtyUnavailableKey, "true");
        }

        var result = await _sut.StartAsync(Guid.NewGuid(), CreateSpec(), _sink, null, CancellationToken.None);

        _pipeLauncher.Aufrufe.Should().Be(1);
        _ptyLauncher.Aufrufe.Should().Be(0);
        result.IsPseudoTerminal.Should().BeFalse();
        _sink.Text.Should().Contain("[Terminal-Diagnose]");
        _sink.Text.Should().Contain("PtyVerfuegbar=False");
    }

    /// <summary>RequiresPty ohne PTY-Verfügbarkeit schlägt hart fehl — mit Diagnose-Marker, ohne Launcher-Aufruf.</summary>
    [Fact]
    public async Task StartAsync_RequiresPtyOhnePty_WirftMitMarkerUndDiagnose()
    {
        await using (var scope = _provider.CreateAsyncScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            await settings.SetSettingAsync(TerminalSessionService.ForcePtyUnavailableKey, "true");
        }
        var spec = CreateSpec() with { Capabilities = TerminalProviderCapabilities.RequiresPty | TerminalProviderCapabilities.SupportsPty };

        var act = () => _sut.StartAsync(Guid.NewGuid(), spec, _sink, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _sink.Text.Should().Contain("[Terminal-Diagnose]");
        _ptyLauncher.Aufrufe.Should().Be(0);
        _pipeLauncher.Aufrufe.Should().Be(0);
    }

    /// <summary>Diagnose-Marker gehen an <see cref="ITerminalDiagnoseSink"/>-Senken über
    /// <c>OnDiagnoseChunk</c> — nicht über <c>OnOutputChunk</c> — damit byte-exakte
    /// Mitschnitt-Pfade frei von Artefakt-Zeilen bleiben.</summary>
    [Fact]
    public async Task StartAsync_DiagnoseFaehigeSenke_ErhaeltMarkerUeberDiagnoseKanal()
    {
        var sink = new CollectingDiagnoseSink();

        var act = () => _sut.StartAsync(
            Guid.NewGuid(),
            CreateSpec(fileName: "gibtes-garantiert-nicht-0815.exe"),
            sink,
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        sink.DiagnoseText.Should().Contain("[Terminal-Diagnose]");
        sink.Text.Should().BeEmpty(
            "der Marker darf bei diagnose-fähigen Senken nicht als normaler Output-Chunk ankommen");
    }

    /// <summary>Ein negatives Replay-Budget ist eine ungültige Konfiguration und schlägt mit ArgumentOutOfRangeException fehl.</summary>
    [Fact]
    public async Task StartAsync_ReplayBudgetUngueltig_WirftArgumentOutOfRange()
    {
        var sut = CreateService(Options.Create(new TerminalSessionOptions { ReplayBufferByteBudget = 0 }));

        var act = () => sut.StartAsync(Guid.NewGuid(), CreateSpec(), null, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>DefaultCols/DefaultRows außerhalb von 1..short.MaxValue würden per short-Cast ein
    /// ungültiges ConPTY erzeugen — der Service lehnt die Konfiguration hart ab.</summary>
    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(220, 0)]
    [InlineData(220, -1)]
    [InlineData(40000, 50)]
    [InlineData(220, 40000)]
    public async Task StartAsync_TerminalgroesseUngueltig_WirftArgumentOutOfRange(int cols, int rows)
    {
        var sut = CreateService(Options.Create(new TerminalSessionOptions { DefaultCols = cols, DefaultRows = rows }));

        var act = () => sut.StartAsync(Guid.NewGuid(), CreateSpec(), null, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _ptyLauncher.Aufrufe.Should().Be(0);
        _pipeLauncher.Aufrufe.Should().Be(0);
    }

    private TerminalSessionService CreateService(IOptions<TerminalSessionOptions> options)
        => new(
            options,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _ptyLauncher,
            _pipeLauncher,
            NullLogger<TerminalSessionService>.Instance);

    private static TerminalSessionStartSpec CreateSpec(string fileName = "cmd.exe")
        => new()
        {
            FileName = fileName,
            PluginName = "TestPlugin",
            WorkingDirectory = Path.GetTempPath(),
        };

    /// <summary>Launcher-Double, das Aufrufe zählt und die übergebene Spec aufzeichnet.</summary>
    private sealed class RecordingLauncher : IPseudoConsoleProcessLauncher
    {
        private readonly List<ITerminalSession> _sessions = [];

        public bool IsPseudoTerminal { get; init; }
        public int Aufrufe { get; private set; }
        public TerminalSessionStartSpec? LetzteSpec { get; private set; }

        public TerminalSessionStartResult Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null)
        {
            Aufrufe++;
            LetzteSpec = spec;
            var process = Process.GetCurrentProcess();
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new MemoryStream(),
                new PseudoConsoleSessionContext
                {
                    OutputSink = outputSink,
                    IsPseudoTerminal = IsPseudoTerminal,
                });
            _sessions.Add(session);
            return new TerminalSessionStartResult(process, session, IsPseudoTerminal);
        }

        public void DisposeSessions()
        {
            foreach (var session in _sessions)
                session.Dispose();
            _sessions.Clear();
        }
    }

    /// <summary>Output-Senke, die geschriebene Bytes als UTF-8-Text sammelt.</summary>
    private sealed class CollectingSink : ITerminalOutputSink
    {
        private readonly StringBuilder _text = new();
        private readonly object _lock = new();

        public string Text
        {
            get { lock (_lock) return _text.ToString(); }
        }

        public void OnOutputChunk(ReadOnlySpan<byte> data)
        {
            lock (_lock)
                _text.Append(Encoding.UTF8.GetString(data));
        }

        public void Complete()
        {
        }

        public Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            Complete();
            return Task.CompletedTask;
        }
    }

    /// <summary>Output-Senke mit <see cref="ITerminalDiagnoseSink"/>: sammelt normale Chunks und
    /// Diagnose-Marker getrennt voneinander.</summary>
    private sealed class CollectingDiagnoseSink : ITerminalOutputSink, ITerminalDiagnoseSink
    {
        private readonly StringBuilder _text = new();
        private readonly StringBuilder _diagnoseText = new();
        private readonly object _lock = new();

        public string Text
        {
            get { lock (_lock) return _text.ToString(); }
        }

        public string DiagnoseText
        {
            get { lock (_lock) return _diagnoseText.ToString(); }
        }

        public void OnOutputChunk(ReadOnlySpan<byte> data)
        {
            lock (_lock)
                _text.Append(Encoding.UTF8.GetString(data));
        }

        public void OnDiagnoseChunk(ReadOnlySpan<byte> bytes)
        {
            lock (_lock)
                _diagnoseText.Append(Encoding.UTF8.GetString(bytes));
        }

        public void Complete()
        {
        }

        public Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
