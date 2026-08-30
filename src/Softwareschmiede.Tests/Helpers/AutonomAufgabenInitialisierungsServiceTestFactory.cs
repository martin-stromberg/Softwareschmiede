using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt die für AutonomAufgabenInitialisierungsService-/AutonomAufgabeInitialisierungsDialogViewModel-Tests benötigten Testdaten und Mocks (ICliRunner für "git branch", IGitPlugin mit Klon-Callback, Service, Projekt/Aufgabe).</summary>
internal static class AutonomAufgabenInitialisierungsServiceTestFactory
{
    /// <summary>Erstellt einen ICliRunner-Mock, der jeden "git"-Aufruf (insbesondere "git branch" in ErstelleProjektbranchAsync) erfolgreich simuliert.</summary>
    /// <returns>Ein Mock, der bei jedem "git"-Aufruf einen erfolgreichen CliResult zurückgibt.</returns>
    public static Mock<ICliRunner> CreateCliRunnerMockMitErfolgreicherGitAusfuehrung()
    {
        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));
        return cliRunnerMock;
    }

    /// <summary>Erstellt einen IGitPlugin-Mock, dessen CloneRepositoryAsync das Zielverzeichnis inklusive Marker-Datei anlegt, dessen GetRemoteBranchesAsync standardmäßig eine leere Liste liefert (Branch existiert nicht remote) und dessen ResolveEffectiveRepositoryPathAsync den übergebenen Pfad unverändert zurückgibt.</summary>
    /// <returns>Ein Mock, der einen erfolgreichen Klon sowie die für ErstelleProjektbranchAsync benötigten Standardantworten simuliert.</returns>
    public static Mock<IGitPlugin> CreateGitPluginMockMitErfolgreichemKlon()
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock
            .Setup(p => p.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, zielPfad, _) =>
            {
                Directory.CreateDirectory(zielPfad);
                File.WriteAllText(Path.Combine(zielPfad, ".git-marker"), "cloned");
            })
            .Returns(Task.CompletedTask);
        gitPluginMock
            .Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        gitPluginMock.SetupPassthroughResolveEffectiveRepositoryPath();
        return gitPluginMock;
    }

    /// <summary>Erstellt einen PluginSelectionService, der bei der Auflösung des SCM-Plugins stets gitPlugin liefert (einziges registriertes und zugleich Default-Plugin), analog zum in EntwicklungsprozessServiceTests etablierten Muster.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext (für PluginDefaultSettingsService/PluginActivationService).</param>
    /// <param name="gitPlugin">Das IGitPlugin, das als einziges verfügbares und als Default-SCM-Plugin registriert wird.</param>
    /// <returns>Ein einsatzbereiter PluginSelectionService.</returns>
    public static PluginSelectionService CreatePluginSelectionService(SoftwareschmiededDbContext db, IGitPlugin gitPlugin)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([gitPlugin]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(gitPlugin);

        var defaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var activationService = new PluginActivationService(
            new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance),
            pluginManagerMock.Object,
            NullLogger<PluginActivationService>.Instance);
        return new PluginSelectionService(pluginManagerMock.Object, defaultSettingsService, activationService, NullLogger<PluginSelectionService>.Instance);
    }

    /// <summary>Erstellt einen AutonomAufgabenInitialisierungsService mit Standard-Options für Tests.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="cliRunner">Der zu verwendende ICliRunner.</param>
    /// <param name="gitPlugin">Das IGitPlugin, über das der (via <see cref="CreatePluginSelectionService"/> gebaute) PluginSelectionService auflöst.</param>
    /// <param name="options">Die zu verwendenden AutonomAufgabenOptions, oder null für Standard-Options (Enabled = true).</param>
    /// <returns>Ein einsatzbereiter AutonomAufgabenInitialisierungsService.</returns>
    public static AutonomAufgabenInitialisierungsService CreateService(SoftwareschmiededDbContext db, ICliRunner cliRunner, IGitPlugin gitPlugin, AutonomAufgabenOptions? options = null)
        => new(
            db,
            cliRunner,
            CreatePluginSelectionService(db, gitPlugin),
            new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance),
            Options.Create(options ?? new AutonomAufgabenOptions()),
            NullLogger<AutonomAufgabenInitialisierungsService>.Instance);

    /// <summary>Erstellt einen ProjektleiterAgentService mit gemockten Governance-/Git-Provisionierungs-Abhängigkeiten sowie einem
    /// prozesslosen KiAusfuehrungsService (<see cref="TestKiAusfuehrungsServiceFactory"/>, sofern <paramref name="kiAusfuehrungsService"/>
    /// nicht übergeben wird) und einem auf ein einziges KI-Plugin aufgelösten PluginSelectionService für Tests.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="kiAusfuehrungsService">Optionaler, bereits vorhandener KiAusfuehrungsService (z. B. um denselben Service auch für ein zugehöriges AutonomAufgabeDetailViewModel zu verwenden); wird sonst neu erstellt.</param>
    /// <param name="options">Die zu verwendenden AutonomAufgabenOptions, oder null für Standard-Options (Enabled = true).</param>
    /// <returns>Ein einsatzbereiter ProjektleiterAgentService.</returns>
    public static ProjektleiterAgentService CreateProjektleiterAgentService(SoftwareschmiededDbContext db, KiAusfuehrungsService? kiAusfuehrungsService = null, AutonomAufgabenOptions? options = null)
        => new(
            db,
            new UnteragentGovernanceService(NullLogger<UnteragentGovernanceService>.Instance),
            new UnteragentGitProvisioningService(new Mock<ICliRunner>().Object, new Mock<IGitPlugin>().Object, NullLogger<UnteragentGitProvisioningService>.Instance),
            kiAusfuehrungsService ?? TestKiAusfuehrungsServiceFactory.Create(),
            ProjektleiterAgentServiceTestDatenFactory.ErstellePluginSelectionServiceMitKiPlugin(db).PluginSelectionService,
            new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance),
            Options.Create(options ?? new AutonomAufgabenOptions()),
            NullLogger<ProjektleiterAgentService>.Instance);

    /// <summary>Erstellt ein einsatzbereites AutonomAufgabeDetailViewModel für <paramref name="aufgabe"/>/<paramref name="konfiguration"/> mit minimalen (aber echten) Abhängigkeiten. Der intern erstellte ProjektleiterAgentService und das ViewModel selbst teilen sich denselben KiAusfuehrungsService, damit CLI-Start und CliIsRunning-Tracking konsistent bleiben.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="aufgabe">Die Aufgabe, für die das ViewModel erstellt wird.</param>
    /// <param name="konfiguration">Die zugehörige AutonomAufgabeKonfiguration.</param>
    /// <returns>Ein einsatzbereites AutonomAufgabeDetailViewModel.</returns>
    public static AutonomAufgabeDetailViewModel CreateAutonomAufgabeDetailViewModel(
        SoftwareschmiededDbContext db, Aufgabe aufgabe, AutonomAufgabeKonfiguration konfiguration)
    {
        var kiAusfuehrungsService = TestKiAusfuehrungsServiceFactory.Create();
        return new AutonomAufgabeDetailViewModel(
            aufgabe,
            konfiguration,
            CreateProjektleiterAgentService(db, kiAusfuehrungsService),
            new SessionManagementService(db, NullLogger<SessionManagementService>.Instance),
            kiAusfuehrungsService,
            NullLogger<AutonomAufgabeDetailViewModel>.Instance);
    }

    /// <summary>
    /// Erstellt einen IServiceProvider mit den für <c>AutonomAufgabeStartService.StarteAsync</c> benötigten
    /// Registrierungen (AutonomAufgabeInitialisierungsDialogViewModel, ProjektleiterAgentService,
    /// SessionManagementService, KiAusfuehrungsService, ILogger&lt;AutonomAufgabeDetailViewModel&gt;).
    /// ProjektleiterAgentService und der direkt auflösbare KiAusfuehrungsService teilen sich dieselbe Instanz.
    /// </summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="initialisierungsService">Der für den Initialisierungsdialog zu verwendende Service.</param>
    /// <param name="pluginManager">Der IPluginManager für den Initialisierungsdialog.</param>
    /// <returns>Ein einsatzbereiter IServiceProvider.</returns>
    public static IServiceProvider CreateAutonomAufgabeStartServiceProvider(
        SoftwareschmiededDbContext db, AutonomAufgabenInitialisierungsService initialisierungsService, IPluginManager pluginManager)
    {
        var promptVorlagenService = new PromptVorlagenService(db, NullLogger<PromptVorlagenService>.Instance);
        var promptVorlagenPlatzhalterService = new PromptVorlagenPlatzhalterService();
        var kiAusfuehrungsService = TestKiAusfuehrungsServiceFactory.Create();

        return new ServiceCollection()
            .AddTransient(_ => new AutonomAufgabeInitialisierungsDialogViewModel(
                initialisierungsService,
                Options.Create(new AutonomAufgabenOptions()),
                NullLogger<AutonomAufgabeInitialisierungsDialogViewModel>.Instance,
                pluginManager,
                promptVorlagenService,
                promptVorlagenPlatzhalterService))
            .AddSingleton(kiAusfuehrungsService)
            .AddTransient(sp => CreateProjektleiterAgentService(db, sp.GetRequiredService<KiAusfuehrungsService>()))
            .AddTransient(_ => new SessionManagementService(db, NullLogger<SessionManagementService>.Instance))
            .AddTransient(_ => (ILogger<AutonomAufgabeDetailViewModel>)NullLogger<AutonomAufgabeDetailViewModel>.Instance)
            .BuildServiceProvider();
    }

    /// <summary>Erstellt ein Projekt und fügt es dem Datenbankkontext hinzu, ohne zu speichern.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <returns>Die Id des neu angelegten Projekts.</returns>
    public static Guid ErstelleProjekt(SoftwareschmiededDbContext db)
    {
        var projektId = Guid.NewGuid();
        db.Projekte.Add(new Projekt
        {
            Id = projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        return projektId;
    }

    /// <summary>Erstellt eine Aufgabe mit lokalem Klon-Verzeichnis (wird auf der Festplatte angelegt) und einem verknüpften GitRepository (RepositoryUrl zeigt auf dasselbe Verzeichnis) und fügt sie dem Datenbankkontext hinzu, ohne zu speichern.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="projektId">Die Id des Projekts, dem die Aufgabe zugeordnet wird.</param>
    /// <param name="testRoot">Das Arbeitsverzeichnis, aus dessen Pfad das lokale Klon-Quellverzeichnis (testRoot + "-quelle") abgeleitet wird.</param>
    /// <param name="titel">Der Titel der Aufgabe.</param>
    /// <param name="branchName">Der BranchName der Aufgabe, oder null.</param>
    /// <returns>Die neu angelegte, noch nicht gespeicherte Aufgabe.</returns>
    public static Aufgabe ErstelleAufgabeMitLokalemKlon(
        SoftwareschmiededDbContext db, Guid projektId, string testRoot, string titel, string? branchName = null)
    {
        var quellRepo = testRoot + "-quelle";
        Directory.CreateDirectory(quellRepo);

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = titel,
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            LokalerKlonPfad = quellRepo,
            BranchName = branchName,
            GitRepository = new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjektId = projektId,
                PluginTyp = "TestGitPlugin",
                RepositoryUrl = quellRepo,
                RepositoryName = "quelle"
            }
        };
        db.Aufgaben.Add(aufgabe);

        return aufgabe;
    }
}
