using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.Services;

/// <summary>Unit-Tests für <see cref="CliReplayExportService"/>.</summary>
public sealed class CliReplayExportServiceTests : IDisposable
{
    private readonly KiAusfuehrungsService _kiService;
    private readonly CliReplayAufzeichnungStore _store = new();
    private readonly CliReplayExportService _sut;

    /// <summary>CliReplayExportServiceTests.</summary>
    public CliReplayExportServiceTests()
    {
        _kiService = TestKiAusfuehrungsServiceFactory.Create();
        _sut = new CliReplayExportService(_kiService, _store, NullLogger<CliReplayExportService>.Instance);
    }

    /// <summary>Dispose — die KiAusfuehrungsService-Instanzen werden bewusst nicht disposed, weil der
    /// deterministische Test-Launcher <c>Process.GetCurrentProcess()</c> bindet und Dispose den
    /// Testhost beenden würde.</summary>
    public void Dispose()
    {
    }

    /// <summary>Ohne aufgezeichnete Session schlägt der Export mit InvalidOperationException fehl.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_OhneAufzeichnung_WirftInvalidOperationException()
    {
        var act = () => _sut.ExportCliReplayAsync(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "x.clireplay"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary><see cref="CliReplayExportService.HatAufzeichnung"/> spiegelt die Verfügbarkeit des
    /// Mitschnitts wider — false ohne Session-Start, true nach einem Start.</summary>
    [Fact]
    public async Task HatAufzeichnung_SpiegeltVerfuegbarkeitDesMitschnitts()
    {
        var aufgabeId = Guid.NewGuid();
        _sut.HatAufzeichnung(aufgabeId).Should().BeFalse("ohne Session-Start liegt kein Mitschnitt vor");

        var kiService = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]));
        var sut = new CliReplayExportService(kiService, _store, NullLogger<CliReplayExportService>.Instance);
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.SetupTerminalSpec();
        await kiService.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        sut.HatAufzeichnung(aufgabeId).Should().BeTrue();
    }

    /// <summary>Der Export schreibt den Mitschnitt der laufenden Session als .clireplay — inklusive
    /// Chunks, die vor der ersten BufferChanged-Beobachtung ankamen (Rekorder ab Session-Start).</summary>
    [Fact]
    public async Task ExportCliReplayAsync_MitAufzeichnung_SchreibtLadbareReplayDatei()
    {
        var aufgabeId = Guid.NewGuid();
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("chunk-eins"),
            Encoding.UTF8.GetBytes("\x1b[31mchunk-zwei"),
        };
        var kiService = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher(chunks));
        var sut = new CliReplayExportService(kiService, _store, NullLogger<CliReplayExportService>.Instance);
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.SetupTerminalSpec();

        await kiService.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var zielPfad = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        try
        {
            await sut.ExportCliReplayAsync(aufgabeId, zielPfad);

            File.Exists(zielPfad).Should().BeTrue();
            var geladen = await _store.LadeAsync(zielPfad);
            geladen.AufgabeId.Should().Be(aufgabeId);
            geladen.PluginName.Should().Be("TestPlugin");
            geladen.Chunks.Should().HaveCount(2);
            geladen.Chunks[0].Data.Should().Equal(chunks[0]);
            geladen.Chunks[1].Data.Should().Equal(chunks[1]);
        }
        finally
        {
            if (File.Exists(zielPfad))
                File.Delete(zielPfad);
        }
    }
}
