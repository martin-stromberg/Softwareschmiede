using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die projektbezogene Backmerge-Option in <see cref="ProjectDetailViewModel"/>.</summary>
public sealed class ProjectDetailViewModelTests_Backmerge : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly AppEinstellungService _einstellungService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests_Backmerge.</summary>
    public ProjectDetailViewModelTests_Backmerge()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        KonfiguriereEinstellungsScope();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private void KonfiguriereEinstellungsScope()
    {
        var providerMock = new Mock<IServiceProvider>();
        providerMock.Setup(p => p.GetService(typeof(AppEinstellungService))).Returns(_einstellungService);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
    }

    private ProjectDetailViewModel CreateSut() =>
        new(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);

    private async Task<Projekt> ErstelleProjektMitRepositoryAsync(string? basisBranch = "staging")
    {
        var projekt = await _projektService.CreateAsync("Backmerge-Test-Projekt", null);
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo",
            defaultSourceBranchName: basisBranch,
            ct: CancellationToken.None);
        return projekt;
    }

    private static async Task LadenAsync(ProjectDetailViewModel sut, Guid projektId)
    {
        sut.ProjektId = projektId;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
    }

    /// <summary>Im Modus „Projektbezogen aktivierbar“ ist die Option bei konfiguriertem Basis-Branch sichtbar
    /// und der gespeicherte Projekt-Schalter wird geladen.</summary>
    [Fact]
    public async Task LadenAsync_ProjektbezogenerModus_OptionSichtbarUndFlagGeladen()
    {
        var projekt = await ErstelleProjektMitRepositoryAsync();
        await _einstellungService.SetScmBackmergeModusAsync(ScmBackmergeModus.Projektbezogen);
        await _einstellungService.SetBackmergeProjektAktivAsync(projekt.Id, true);
        var sut = CreateSut();

        await LadenAsync(sut, projekt.Id);

        sut.IsBackmergeOptionVisible.Should().BeTrue();
        sut.IsBackmergeAutoMergeEnabled.Should().BeTrue();
    }

    /// <summary>Ohne Modus „Projektbezogen aktivierbar“ bleibt die Option verborgen.</summary>
    [Theory]
    [InlineData(ScmBackmergeModus.Deaktiviert)]
    [InlineData(ScmBackmergeModus.Global)]
    public async Task LadenAsync_NichtProjektbezogenerModus_OptionVerborgen(ScmBackmergeModus modus)
    {
        var projekt = await ErstelleProjektMitRepositoryAsync();
        await _einstellungService.SetScmBackmergeModusAsync(modus);
        var sut = CreateSut();

        await LadenAsync(sut, projekt.Id);

        sut.IsBackmergeOptionVisible.Should().BeFalse();
    }

    /// <summary>Ohne konfigurierten Basis-Branch bleibt die Option trotz projektbezogenem Modus verborgen.</summary>
    [Fact]
    public async Task LadenAsync_OhneBasisBranch_OptionVerborgen()
    {
        var projekt = await ErstelleProjektMitRepositoryAsync(basisBranch: null);
        await _einstellungService.SetScmBackmergeModusAsync(ScmBackmergeModus.Projektbezogen);
        var sut = CreateSut();

        await LadenAsync(sut, projekt.Id);

        sut.IsBackmergeOptionVisible.Should().BeFalse();
    }

    /// <summary>Das Umschalten der Option persistiert die Projekt-Freigabe sofort.</summary>
    [Fact]
    public async Task IsBackmergeAutoMergeEnabled_Umschalten_PersistiertProjektFlag()
    {
        var projekt = await ErstelleProjektMitRepositoryAsync();
        await _einstellungService.SetScmBackmergeModusAsync(ScmBackmergeModus.Projektbezogen);
        var sut = CreateSut();
        await LadenAsync(sut, projekt.Id);

        sut.IsBackmergeAutoMergeEnabled = true;

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await _einstellungService.GetBackmergeProjektAktivAsync(projekt.Id))
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "die Freigabe muss zeitnah persistiert werden");
            await Task.Delay(50);
        }
    }
}
