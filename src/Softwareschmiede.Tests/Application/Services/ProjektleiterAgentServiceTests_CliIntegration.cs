using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die echte CLI-Integration von <see cref="ProjektleiterAgentService"/>: CLI-Start über
/// <see cref="KiAusfuehrungsService"/>, Prompt-Zustellung über <see cref="PseudoConsoleSession.WritePromptAsync"/>,
/// Session-Continuation-Flag und App-Neustart-Recovery.</summary>
public sealed class ProjektleiterAgentServiceTests_CliIntegration : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ICliRunner> _cliRunnerMock = new();
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;
    private readonly Mock<IKiPlugin> _kiPluginMock;
    private readonly ProjektleiterAgentService _sut;
    private readonly string _testRoot;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>ProjektleiterAgentServiceTests_CliIntegration.</summary>
    public ProjektleiterAgentServiceTests_CliIntegration()
    {
        _db = TestDbContextFactory.Create();
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupPassthroughResolveEffectiveRepositoryPath();

        var governanceService = new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance);
        var gitProvisioningService = new UnteragentGitProvisioningService(_cliRunnerMock.Object, gitPluginMock.Object, NullLogger<UnteragentGitProvisioningService>.Instance);
        _kiAusfuehrungsService = TestKiAusfuehrungsServiceFactory.Create();
        (_kiPluginMock, var pluginSelectionService) = ProjektleiterAgentServiceTestDatenFactory.ErstellePluginSelectionServiceMitKiPlugin(_db);
        _sut = new ProjektleiterAgentService(_db, governanceService, gitProvisioningService, _kiAusfuehrungsService, pluginSelectionService, new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance), Options.Create(new AutonomAufgabenOptions()), NullLogger<ProjektleiterAgentService>.Instance);

        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "ProjektleiterAgentCli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(Path.Combine(_testRoot, "clones", "repo_main"));

        _db.Projekte.Add(new Projekt { Id = _projektId, Name = "Testprojekt", ErstellungsDatum = DateTimeOffset.UtcNow, Status = ProjektStatus.Aktiv });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _kiAusfuehrungsService.Dispose();
        _db.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    /// <summary>Beim Erststart (kein Resume-Prompt) wird die CLI mit optionalParameters == null gestartet — nicht mit dem Initialprompt-Text als Kommandozeilenargument.</summary>
    [Fact]
    public async Task StarteAgentAsync_CallsKiAusfuehrungsService_WithNullOptionalParameters()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);

        await _sut.StarteAgentAsync(konfiguration);

        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Nach dem CLI-Start wird konfiguration.InitialPrompt per PseudoConsoleSession.WritePromptAsync() gesendet (nicht als optionalParameters).</summary>
    [Fact]
    public async Task StarteAgentAsync_SendetInitialPromptUeberPseudoConsoleSession()
    {
        var (aufgabe, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);

        await _sut.StarteAgentAsync(konfiguration);

        var session = _kiAusfuehrungsService.GetPseudoConsoleSession(aufgabe.Id);
        session.Should().NotBeNull();

        var gesendet = await WarteAufGesendetenPromptAsync(session!, konfiguration.InitialPrompt, TimeSpan.FromSeconds(10));
        gesendet.Should().Contain(konfiguration.InitialPrompt);
    }

    /// <summary>Beim Resume (optionalResumePrompt gesetzt) wird der Resume-Prompt statt des Initialprompts per WritePromptAsync() gesendet.</summary>
    [Fact]
    public async Task StarteAgentAsync_MitResumePrompt_SendetWeitermachenPromptUeberPseudoConsoleSession()
    {
        var (aufgabe, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        const string resumePrompt = "Weitermachen: Setze die Arbeit fort.";

        await _sut.StarteAgentAsync(konfiguration, optionalResumePrompt: resumePrompt);

        var session = _kiAusfuehrungsService.GetPseudoConsoleSession(aufgabe.Id);
        session.Should().NotBeNull();

        var gesendet = await WarteAufGesendetenPromptAsync(session!, resumePrompt, TimeSpan.FromSeconds(10));
        gesendet.Should().Contain(resumePrompt);
        gesendet.Should().NotContain(konfiguration.InitialPrompt);
    }

    /// <summary>Bei Resume und einem Plugin mit SupportsSessionContinuation() == true wird optionalParameters == "--continue" an StartWithPseudoConsoleAsync übergeben.</summary>
    [Fact]
    public async Task StarteAgentAsync_MitResumePromptUndSessionContinuationPlugin_UebergibtContinueFlag()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        _kiPluginMock.Setup(p => p.SupportsSessionContinuation()).Returns(true);

        await _sut.StarteAgentAsync(konfiguration, optionalResumePrompt: "Weitermachen: ...");

        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), "--continue", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Bei Resume ohne Plugin-Support für Session-Continuation wird kein --continue-Flag übergeben (optionalParameters == null).</summary>
    [Fact]
    public async Task StarteAgentAsync_MitResumePromptOhneSessionContinuationPlugin_UebergibtKeinContinueFlag()
    {
        var (_, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        _kiPluginMock.Setup(p => p.SupportsSessionContinuation()).Returns(false);

        await _sut.StarteAgentAsync(konfiguration, optionalResumePrompt: "Weitermachen: ...");

        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>StarteAgenNachAppNeustartAsync startet den Agenten mit dem Resume-Prompt neu, wenn die Aufgabe nicht explizit gestoppt und aktiv ist.</summary>
    [Fact]
    public async Task StarteAgenNachAppNeustartAsync_WennNichtExplizitGestoppt_StartetNeuMitResumePrompt()
    {
        var (aufgabe, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        const string resumePrompt = "Weitermachen nach App-Neustart: ...";

        await _sut.StarteAgenNachAppNeustartAsync(aufgabe.Id, resumePrompt);

        var konfigurationAktualisiert = await _db.AutonomAufgabeKonfigurationen.FindAsync(konfiguration.Id);
        konfigurationAktualisiert!.ProjektleiterAgentId.Should().NotBeNullOrWhiteSpace();

        var session = _kiAusfuehrungsService.GetPseudoConsoleSession(aufgabe.Id);
        session.Should().NotBeNull();
        var gesendet = await WarteAufGesendetenPromptAsync(session!, resumePrompt, TimeSpan.FromSeconds(10));
        gesendet.Should().Contain(resumePrompt);
    }

    /// <summary>StarteAgenNachAppNeustartAsync startet nicht, wenn die Aufgabe explizit gestoppt wurde.</summary>
    [Fact]
    public async Task StarteAgenNachAppNeustartAsync_WennExplizitGestoppt_StartetNicht()
    {
        var (aufgabe, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);
        konfiguration.ExplizitGestoppt = true;
        await _db.SaveChangesAsync();

        await _sut.StarteAgenNachAppNeustartAsync(aufgabe.Id, "Weitermachen: ...");

        var konfigurationAktualisiert = await _db.AutonomAufgabeKonfigurationen.FindAsync(konfiguration.Id);
        konfigurationAktualisiert!.ProjektleiterAgentId.Should().BeNullOrWhiteSpace();
        _kiAusfuehrungsService.IsRunning(aufgabe.Id).Should().BeFalse();
    }

    /// <summary>StoppeAgenExplizitAsync setzt ExplizitGestoppt und ruft KiAusfuehrungsService.StopCliAsync auf (Best-Effort, auch ohne laufenden Prozess).</summary>
    [Fact]
    public async Task StoppeAgenExplizitAsync_SetzExplizitGestoppt()
    {
        var (aufgabe, konfiguration) = await ProjektleiterAgentServiceTestDatenFactory.ErstelleAutonomeAufgabeAsync(_db, _projektId, _testRoot);

        await _sut.StoppeAgenExplizitAsync(aufgabe.Id);

        var konfigurationAktualisiert = await _db.AutonomAufgabeKonfigurationen.FindAsync(konfiguration.Id);
        konfigurationAktualisiert!.ExplizitGestoppt.Should().BeTrue();
    }

    /// <summary>
    /// Wartet, bis der Input-Stream der Session <paramref name="erwarteterInhalt"/> enthält, oder bis
    /// <paramref name="timeout"/> abgelaufen ist. Da der Input-Stream vor dem verzögerten Initial-/Weitermachen-Prompt
    /// (siehe ProjektleiterAgentService.PromptSendeVerzoegerungMs, 3000ms) bereits den nach 300ms gesendeten
    /// Plugin-Befehl enthält (KiAusfuehrungsService.SendCommandDelayedAsync), genügt ein einfacher
    /// "nicht-leer"-Check nicht — es muss auf den tatsächlich erwarteten Inhalt gewartet werden.
    /// </summary>
    /// <param name="session">Die zu prüfende PseudoConsoleSession.</param>
    /// <param name="erwarteterInhalt">Der Text, auf dessen Erscheinen im Input-Stream gewartet wird.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Den zuletzt gelesenen Inhalt des Input-Streams (enthält <paramref name="erwarteterInhalt"/>, sofern rechtzeitig gesendet).</returns>
    private static async Task<string> WarteAufGesendetenPromptAsync(PseudoConsoleSession session, string erwarteterInhalt, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var letzterInhalt = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            if (session.InputStream is MemoryStream ms)
            {
                letzterInhalt = Encoding.UTF8.GetString(ms.ToArray());
                if (letzterInhalt.Contains(erwarteterInhalt, StringComparison.Ordinal))
                {
                    return letzterInhalt;
                }
            }

            await Task.Delay(100);
        }

        return letzterInhalt;
    }
}
