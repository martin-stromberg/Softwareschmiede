using FluentAssertions;
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

/// <summary>Tests für die Basis-Branch-Bearbeitung in <see cref="ProjectDetailViewModel"/>.</summary>
public sealed class ProjectDetailViewModelTests_BasisBranch : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests_BasisBranch.</summary>
    public ProjectDetailViewModelTests_BasisBranch()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private ProjectDetailViewModel CreateSut() =>
        new(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);

    private async Task<(Softwareschmiede.Domain.Entities.Projekt Projekt, GitRepository Repository)> ErstelleProjektMitRepositoryAsync(string? defaultSourceBranchName = null)
    {
        var projekt = await _projektService.CreateAsync("Basis-Branch-Test-Projekt", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo",
            defaultSourceBranchName: defaultSourceBranchName,
            ct: CancellationToken.None);

        return (projekt, repository);
    }

    private static Mock<IGitPlugin> CreatePluginMock(string pluginPrefix)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginPrefix).Returns(pluginPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        return mock;
    }

    /// <summary>Beim Auswählen eines Repositories wird der konfigurierte Basis-Branch geladen.</summary>
    [Fact]
    public async Task ProjectDetailVM_SelectedRepository_ShouldLoadSourceBranchName()
    {
        var (projekt, _) = await ErstelleProjektMitRepositoryAsync(defaultSourceBranchName: "staging");
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.SelectedRepositorySourceBranchName.Should().Be("staging");
    }

    /// <summary>Speichern des bearbeiteten Basis-Branches ruft ProjektService.UpdateRepositorySourceBranchAsync auf und persistiert den neuen Wert.</summary>
    [Fact]
    public async Task ProjectDetailVM_SaveSourceBranch_ShouldCallService()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync(defaultSourceBranchName: "main");
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.EditSourceBranchCommand).ExecuteAsync();
        sut.SelectedRepositorySourceBranchName = "staging";

        await ((AsyncRelayCommand)sut.SaveSourceBranchCommand).ExecuteAsync();

        sut.IsEditingSourceBranch.Should().BeFalse();
        sut.SelectedRepositorySourceBranchName.Should().Be("staging");
        var persisted = await _projektService.GetDetailAsync(projekt.Id);
        persisted!.Repositories.Single(r => r.Id == repository.Id).DefaultSourceBranchName.Should().Be("staging");
    }

    /// <summary>Speichern eines Basis-Branches, der nicht in der geladenen Branch-Liste enthalten ist, schlägt fehl und bricht das Speichern ab.</summary>
    [Fact]
    public async Task ProjectDetailVM_SaveSourceBranch_ShouldFail_WhenBranchDoesNotExist()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync(defaultSourceBranchName: "main");
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "staging"]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.EditSourceBranchCommand).ExecuteAsync();
        sut.SelectedRepositorySourceBranchName = "unbekannter-branch";

        await ((AsyncRelayCommand)sut.SaveSourceBranchCommand).ExecuteAsync();

        sut.IsEditingSourceBranch.Should().BeTrue();
        sut.SourceBranchInputError.Should().NotBeNullOrEmpty();
        var persisted = await _projektService.GetDetailAsync(projekt.Id);
        persisted!.Repositories.Single(r => r.Id == repository.Id).DefaultSourceBranchName.Should().Be("main");
    }

    /// <summary>Das Öffnen des Edit-Modus lädt die verfügbaren Branches über das zugeordnete SCM-Plugin.</summary>
    [Fact]
    public async Task ProjectDetailVM_EditSourceBranchMode_ShouldLoadAvailableBranches()
    {
        var (projekt, _) = await ErstelleProjektMitRepositoryAsync(defaultSourceBranchName: "main");
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "staging"]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.EditSourceBranchCommand).ExecuteAsync();

        sut.IsEditingSourceBranch.Should().BeTrue();
        sut.AvailableSourceBranchesForEdit.Should().Contain(["main", "staging"]);
    }

    /// <summary>Das Abbrechen der Bearbeitung verwirft Änderungen und schließt den Edit-Modus, ohne zu speichern.</summary>
    [Fact]
    public async Task ProjectDetailVM_CancelSourceBranchEdit_ShouldDiscardChanges()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync(defaultSourceBranchName: "main");
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.EditSourceBranchCommand).ExecuteAsync();
        sut.SelectedRepositorySourceBranchName = "staging";

        ((RelayCommand)sut.CancelSourceBranchEditCommand).Execute(null);

        sut.IsEditingSourceBranch.Should().BeFalse();
        sut.SelectedRepositorySourceBranchName.Should().Be("main");
        var persisted = await _projektService.GetDetailAsync(projekt.Id);
        persisted!.Repositories.Single(r => r.Id == repository.Id).DefaultSourceBranchName.Should().Be("main");
    }
}
