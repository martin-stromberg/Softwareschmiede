using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die Initialisierungsskript-Bearbeitung in <see cref="ProjectDetailViewModel"/>.</summary>
public sealed class ProjectDetailViewModelTests_Initialisierungsskript : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly DirectoryStructureBrowserService _directoryStructureService;

    /// <summary>ProjectDetailViewModelTests_Initialisierungsskript.</summary>
    public ProjectDetailViewModelTests_Initialisierungsskript()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _directoryStructureService = new DirectoryStructureBrowserService(
            _cache, Options.Create(new DirectoryStructureOptions()), NullLogger<DirectoryStructureBrowserService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    private ProjectDetailViewModel CreateSut() =>
        new(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance,
            _directoryStructureService);

    private async Task<(Softwareschmiede.Domain.Entities.Projekt Projekt, GitRepository Repository)> ErstelleProjektMitRepositoryAsync()
    {
        var projekt = await _projektService.CreateAsync("Initialisierungsskript-Test-Projekt", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo",
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

    /// <summary>Das Laden der Vorschläge ruft das SCM-Plugin ab und filtert auf ausführbare Dateien.</summary>
    [Fact]
    public async Task LoadInitialisierungsskriptSuggestionenAsync_ShouldFetchFromRemote()
    {
        var (projekt, _) = await ErstelleProjektMitRepositoryAsync();
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success(
            [
                new RepositoryDirectoryEntry("scripts", IsDirectory: true),
                new RepositoryDirectoryEntry("scripts/init.ps1", IsDirectory: false),
                new RepositoryDirectoryEntry("scripts/setup.sh", IsDirectory: false),
                new RepositoryDirectoryEntry("README.md", IsDirectory: false)
            ]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.LoadInitialisierungsskriptSuggestionenCommand).ExecuteAsync();

        sut.IsEditingInitialisierungsskript.Should().BeTrue();
        sut.InitialisierungsskriptSuggestionen.Should().Contain(["scripts/init.ps1", "scripts/setup.sh"]);
        sut.InitialisierungsskriptSuggestionen.Should().NotContain(["scripts", "README.md"]);
        sut.InitialisierungsskriptLoadingFailed.Should().BeFalse();
    }

    /// <summary>Schlägt der Remote-Zugriff fehl, bleibt die UI responsiv und der Fehler wird über ein Flag signalisiert.</summary>
    [Fact]
    public async Task LoadInitialisierungsskriptSuggestionenAsync_ShouldHandleNetworkError_Gracefully()
    {
        var (projekt, _) = await ErstelleProjektMitRepositoryAsync();
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Netzwerkfehler"));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var act = () => ((AsyncRelayCommand)sut.LoadInitialisierungsskriptSuggestionenCommand).ExecuteAsync();

        await act.Should().NotThrowAsync();
        sut.InitialisierungsskriptLoadingFailed.Should().BeTrue();
        sut.InitialisierungsskriptSuggestionen.Should().BeEmpty();
    }

    /// <summary>Speichern des ausgewählten Skripts persistiert den Wert über den ProjektService.</summary>
    [Fact]
    public async Task SaveInitialisierungsskriptAsync_ShouldPersist_SelectedScript()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync();
        await _projektService.SaveRepositoryInitialisierungskriptAsync(repository.Id, "scripts/old.ps1", CancellationToken.None);
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.SelectedInitialisierungsskript.Should().Be("scripts/old.ps1");

        await ((AsyncRelayCommand)sut.LoadInitialisierungsskriptSuggestionenCommand).ExecuteAsync();
        sut.SelectedInitialisierungsskript = "scripts/new.ps1";

        await ((AsyncRelayCommand)sut.SaveInitialisierungsskriptCommand).ExecuteAsync();

        sut.IsEditingInitialisierungsskript.Should().BeFalse();
        sut.SelectedInitialisierungsskript.Should().Be("scripts/new.ps1");
        var persisted = await _projektService.GetDetailAsync(projekt.Id);
        persisted!.Repositories.Single(r => r.Id == repository.Id).InitialisierungKonfiguration!.InitialisierungsskriptRelativePath.Should().Be("scripts/new.ps1");
    }

    /// <summary>Ist noch keine Konfiguration vorhanden, legt das Speichern eine neue an.</summary>
    [Fact]
    public async Task SaveInitialisierungsskriptAsync_ShouldCreateConfiguration_IfNotExists()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync();
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.SelectedInitialisierungsskript.Should().BeNull();

        await ((AsyncRelayCommand)sut.LoadInitialisierungsskriptSuggestionenCommand).ExecuteAsync();
        sut.SelectedInitialisierungsskript = "scripts/init.ps1";

        await ((AsyncRelayCommand)sut.SaveInitialisierungsskriptCommand).ExecuteAsync();

        var persisted = await _projektService.GetDetailAsync(projekt.Id);
        var konfiguration = persisted!.Repositories.Single(r => r.Id == repository.Id).InitialisierungKonfiguration;
        konfiguration.Should().NotBeNull();
        konfiguration!.InitialisierungsskriptRelativePath.Should().Be("scripts/init.ps1");
        konfiguration.Aktiv.Should().BeTrue();
    }

    /// <summary>Das Abbrechen der Bearbeitung verwirft Änderungen und schließt den Edit-Modus, ohne zu speichern.</summary>
    [Fact]
    public async Task CancelInitialisierungsskriptEdit_ShouldDiscardChanges()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync();
        await _projektService.SaveRepositoryInitialisierungskriptAsync(repository.Id, "scripts/init.ps1", CancellationToken.None);
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.LoadInitialisierungsskriptSuggestionenCommand).ExecuteAsync();
        sut.SelectedInitialisierungsskript = "scripts/geaendert.ps1";

        ((RelayCommand)sut.CancelInitialisierungsskriptEditCommand).Execute(null);

        sut.IsEditingInitialisierungsskript.Should().BeFalse();
        sut.SelectedInitialisierungsskript.Should().Be("scripts/init.ps1");
        var persisted = await _projektService.GetDetailAsync(projekt.Id);
        persisted!.Repositories.Single(r => r.Id == repository.Id).InitialisierungKonfiguration!.InitialisierungsskriptRelativePath.Should().Be("scripts/init.ps1");
    }
}
