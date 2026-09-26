using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für den Rohbyte-Mitschnitt (<see cref="CliOutputRecorder"/>) in
/// <see cref="Softwareschmiede.Application.Services.KiAusfuehrungsService"/>: Recorder-Verdrahtung,
/// Registry und Budget-Deaktivierung. Die Services werden bewusst nicht disposed — der
/// deterministische Test-Launcher bindet <c>Process.GetCurrentProcess()</c> und
/// <see cref="IDisposable.Dispose"/> würde den Testhost selbst beenden.</summary>
public sealed class KiAusfuehrungsServiceCliAufzeichnungTests
{
    private static Mock<IKiPlugin> CreatePlugin()
    {
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.SetupTerminalSpec();
        return pluginMock;
    }

    /// <summary>Nach dem Session-Start liefert <c>GetCliAufzeichnung</c> die aufgezeichneten Roh-Chunks —
    /// inklusive derer, die bereits beim Start an die Senke gingen (kein Race-Fenster).</summary>
    [Fact]
    public async Task GetCliAufzeichnung_NachSessionStart_EnthaeltRohChunks()
    {
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("früher-chunk"),
            Encoding.UTF8.GetBytes("\x1b[31mfarbchunk"),
        };
        var sut = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher(chunks));
        var aufgabeId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        await sut.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var aufzeichnung = sut.GetCliAufzeichnung(aufgabeId);
        aufzeichnung.Should().NotBeNull();
        aufzeichnung!.AufgabeId.Should().Be(aufgabeId);
        aufzeichnung.PluginName.Should().Be("TestPlugin");
        aufzeichnung.Cols.Should().Be(new TerminalSessionOptions().DefaultCols);
        aufzeichnung.Rows.Should().Be(new TerminalSessionOptions().DefaultRows);
        aufzeichnung.IstVollstaendig.Should().BeTrue();
        aufzeichnung.Chunks.Should().HaveCount(2);
        aufzeichnung.Chunks[0].Data.Should().Equal(chunks[0]);
        aufzeichnung.Chunks[1].Data.Should().Equal(chunks[1]);
    }

    /// <summary>Ohne Session-Start liefert <c>GetCliAufzeichnung</c> null.</summary>
    [Fact]
    public void GetCliAufzeichnung_OhneSession_GibtNull()
    {
        var sut = TestKiAusfuehrungsServiceFactory.Create();

        sut.GetCliAufzeichnung(Guid.NewGuid()).Should().BeNull();
    }

    /// <summary>Mit <c>AufzeichnungByteBudget &lt;= 0</c> wird kein Recorder erstellt — die Senke des
    /// Handles ist dann direkt der Protokoll-Writer (keine einelementige Composite).</summary>
    [Fact]
    public async Task StartTerminalSessionAsync_DeaktiviertesBudget_KeinRecorderUndKeineComposite()
    {
        var launcher = TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]);
        var sut = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: launcher,
            terminalOptions: new TerminalSessionOptions { AufzeichnungByteBudget = 0 });
        var aufgabeId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        var handle = await sut.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        sut.GetCliAufzeichnung(aufgabeId).Should().BeNull("bei deaktiviertem Budget wird kein Recorder erstellt");
        handle.OutputSink.Should().BeOfType<CliOutputProtokollWriter>(
            "ohne Recorder wird der Protokoll-Writer direkt als Senke übergeben");
    }

    /// <summary>Mit aktivem Budget ist die Senke eine <see cref="CompositeTerminalOutputSink"/> aus
    /// Protokoll-Writer und Recorder.</summary>
    [Fact]
    public async Task StartTerminalSessionAsync_AktivesBudget_SenkeIstComposite()
    {
        var sut = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]));
        var aufgabeId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        var handle = await sut.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        handle.OutputSink.Should().BeOfType<CompositeTerminalOutputSink>(
            "der an die Session übergebene Sink muss die Composite-Instanz sein (Recorder + Writer)");
    }

    /// <summary>Ein überschrittenes Aufzeichnungsbudget markiert die Aufzeichnung als unvollständig —
    /// das Präfix bleibt erhalten.</summary>
    [Fact]
    public async Task GetCliAufzeichnung_BudgetUeberschritten_UnvollstaendigMitPraefix()
    {
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("12345678"),
            Encoding.UTF8.GetBytes("9"),
        };
        var sut = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher(chunks),
            terminalOptions: new TerminalSessionOptions { AufzeichnungByteBudget = 8 });
        var aufgabeId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        await sut.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var aufzeichnung = sut.GetCliAufzeichnung(aufgabeId);
        aufzeichnung.Should().NotBeNull();
        aufzeichnung!.IstVollstaendig.Should().BeFalse();
        aufzeichnung.Chunks.Should().HaveCount(1);
        aufzeichnung.Chunks[0].Data.Should().Equal(chunks[0]);
    }

    /// <summary>Die Aufzeichnungs-Registry ist auf die letzten
    /// <see cref="Softwareschmiede.Application.Services.KiAusfuehrungsService.MaxAufzeichnungenAnzahl"/>
    /// Aufgaben begrenzt — der älteste Mitschnitt wird verworfen, die jüngsten bleiben vorgehalten.</summary>
    [Fact]
    public async Task GetCliAufzeichnung_MehrAlsMaxAufzeichnungen_VerwirftAeltesteEintraege()
    {
        var launcher = TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]);
        var sut = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: launcher);
        var pluginMock = CreatePlugin();
        var aufgabeIds = Enumerable.Range(0, KiAusfuehrungsService.MaxAufzeichnungenAnzahl + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        foreach (var id in aufgabeIds)
            await sut.StartTerminalSessionAsync(id, pluginMock.Object, Path.GetTempPath());

        sut.GetCliAufzeichnung(aufgabeIds[0]).Should().BeNull(
            "der älteste Mitschnitt wird über der Retention-Grenze verworfen");
        foreach (var id in aufgabeIds.Skip(1))
            sut.GetCliAufzeichnung(id).Should().NotBeNull(
                "die letzten {0} Mitschnitte bleiben vorgehalten", KiAusfuehrungsService.MaxAufzeichnungenAnzahl);
    }

    /// <summary>Der übergebene TimeProvider liefert die Start- und Offset-Zeitstempel der Aufzeichnung.</summary>
    [Fact]
    public async Task GetCliAufzeichnung_VerwendetTimeProviderFuerZeitstempel()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 5, 5, 10, 0, 0, TimeSpan.Zero));
        var sut = TestKiAusfuehrungsServiceFactory.Create(
            new Mock<IServiceScopeFactory>().Object,
            launcher: TestTerminalSessionFactory.CreateChunkEmittingLauncher([Encoding.UTF8.GetBytes("x")]),
            timeProvider: timeProvider);
        var aufgabeId = Guid.NewGuid();
        var pluginMock = CreatePlugin();

        await sut.StartTerminalSessionAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var aufzeichnung = sut.GetCliAufzeichnung(aufgabeId);
        aufzeichnung.Should().NotBeNull();
        aufzeichnung!.StartUtc.Should().Be(timeProvider.Start);
        aufzeichnung.Chunks[0].Offset.Should().Be(TimeSpan.Zero);
    }
}
