using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.Services;

/// <summary>Unit-Tests für AutonomAufgabeStartService, insbesondere den Fehlerpfad von StarteAsync.</summary>
public sealed class AutonomAufgabeStartServiceTests : IDisposable
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>AutonomAufgabeStartServiceTests.</summary>
    public AutonomAufgabeStartServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>
    /// StarteAsync fängt Fehler ab, die beim Auflösen des Initialisierungsdialogs auftreten (hier: fehlende
    /// Registrierung von AutonomAufgabeInitialisierungsDialogViewModel im IServiceProvider-Mock), und gibt dabei
    /// weiterhin die bereits geladene Aufgabe zurück statt null, damit die aufrufende Detail-Ansicht nicht einen
    /// veralteten Stand anzeigt.
    /// </summary>
    [Fact]
    public async Task StarteAsync_GibtBereitsGeladeneAufgabeZurueck_BeiFehlerWaehrendInitialisierung()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung", null);

        var dialogServiceMock = new Mock<IDialogService>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        // Bewusst keine Registrierung von AutonomAufgabeInitialisierungsDialogViewModel:
        // GetRequiredService wirft eine InvalidOperationException, die StarteAsync abfangen muss.

        var sut = new AutonomAufgabeStartService(
            serviceProviderMock.Object,
            dialogServiceMock.Object,
            _aufgabeService,
            NullLogger<AutonomAufgabeStartService>.Instance);

        var ergebnis = await sut.StarteAsync(aufgabe, CancellationToken.None);

        ergebnis.Should().NotBeNull();
        ergebnis!.FehlerMeldung.Should().NotBeNullOrEmpty();
        ergebnis.AktualisierteAufgabe.Should().NotBeNull();
        ergebnis.AktualisierteAufgabe!.Id.Should().Be(aufgabe.Id);
    }

    /// <summary>
    /// StarteAsync gibt bei erfolgreicher Initialisierung das erzeugte AutonomAufgabeDetailViewModel über
    /// AutonomAufgabeStartResult.DetailViewModel zurück, statt (wie vor der UI-Integration in
    /// TaskDetailView) einen separaten Dialog anzuzeigen: IDialogService.ShowAutonomAufgabeDetailAsync
    /// existiert nicht mehr, daher genügt hier der Nachweis, dass ausschließlich der
    /// Initialisierungsdialog (ShowAutonomAufgabeInitialisierungsDialogAsync) aufgerufen wird.
    /// </summary>
    [Fact]
    public async Task StarteAsync_GibtErstelltesDetailViewModelImResultZurueck_OhneDialogAufruf()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "autonom_start_svc_" + Guid.NewGuid().ToString("N"));
        try
        {
            var aufgabe = AutonomAufgabenInitialisierungsServiceTestFactory.ErstelleAufgabeMitLokalemKlon(
                _db, _projektId, testRoot, "Testaufgabe für Happy Path");
            await _db.SaveChangesAsync();

            var cliRunnerMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateCliRunnerMockMitErfolgreicherGitAusfuehrung();
            var gitPluginMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateGitPluginMockMitErfolgreichemKlon();
            var initialisierungsService = AutonomAufgabenInitialisierungsServiceTestFactory.CreateService(_db, cliRunnerMock.Object, gitPluginMock.Object);

            var pluginManagerMock = new Mock<IPluginManager>();
            pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);

            var arbeitsverzeichnisPfad = Path.Combine(testRoot, "arbeitsverzeichnis");
            Directory.CreateDirectory(arbeitsverzeichnisPfad);

            var konfiguration = new AutonomAufgabeKonfiguration
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabe.Id,
                ProjektBranchName = "feature/autonom",
                InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
                PermissionsJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "permissions.json"),
                ArbeitsverzeichnisPfad = arbeitsverzeichnisPfad
            };

            var dialogServiceMock = new Mock<IDialogService>();
            dialogServiceMock
                .Setup(d => d.ShowAutonomAufgabeInitialisierungsDialogAsync(It.IsAny<AutonomAufgabeInitialisierungsDialogViewModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(konfiguration);

            var serviceProvider = AutonomAufgabenInitialisierungsServiceTestFactory.CreateAutonomAufgabeStartServiceProvider(
                _db, initialisierungsService, pluginManagerMock.Object);

            var sut = new AutonomAufgabeStartService(
                serviceProvider,
                dialogServiceMock.Object,
                _aufgabeService,
                NullLogger<AutonomAufgabeStartService>.Instance);

            var ergebnis = await sut.StarteAsync(aufgabe, CancellationToken.None);

            ergebnis.Should().NotBeNull();
            ergebnis!.FehlerMeldung.Should().BeNull();
            ergebnis.DetailViewModel.Should().NotBeNull();
            dialogServiceMock.Verify(
                d => d.ShowAutonomAufgabeInitialisierungsDialogAsync(It.IsAny<AutonomAufgabeInitialisierungsDialogViewModel>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }
    }
}
