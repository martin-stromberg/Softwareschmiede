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
public sealed class ProjectDetailViewModelTests_Initializationscript : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly DirectoryStructureBrowserService _directoryStructureService;

    /// <summary>ProjectDetailViewModelTests_Initialisierungsskript.</summary>
    public ProjectDetailViewModelTests_Initializationscript()
    {
        _db = TestDbContextFactory.Create();
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
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
            _projectService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance,
            _directoryStructureService);

    private async Task<(Softwareschmiede.Domain.Entities.Project Project, GitRepository Repository)> CreateProjectWithRepositoryAsync()
    {
        var project = await _projectService.CreateAsync("Initialisierungsskript-Test-Projekt", null);
        var repository = await _projectService.AddRepositoryAsync(
            project.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo",
            ct: CancellationToken.None);

        return (project, repository);
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
    public async Task LoadInitializationscriptSuggestionsAsync_ShouldFetchFromRemote()
    {
        var (project, _) = await CreateProjectWithRepositoryAsync();
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
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();

        sut.IsEditingInitializationScript.Should().BeTrue();
        sut.InitializationScriptSuggestions.Should().Contain(["scripts/init.ps1", "scripts/setup.sh"]);
        sut.InitializationScriptSuggestions.Should().NotContain(["scripts", "README.md"]);
        sut.InitializationScriptLoadingFailed.Should().BeFalse();
    }

    /// <summary>Ist am Repository ein Basis-Branch konfiguriert, wird dieser (statt des Remote-Standard-Branches) beim Laden der Vorschläge verwendet.</summary>
    [Fact]
    public async Task LoadInitializationscriptSuggestionsAsync_ShouldUseConfiguredBaseBranch()
    {
        var (project, repository) = await CreateProjectWithRepositoryAsync();
        await _projectService.UpdateRepositorySourceBranchAsync(repository.Id, "develop", CancellationToken.None);
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), "develop"))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([new RepositoryDirectoryEntry("init.sh", IsDirectory: false)]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();

        pluginMock.Verify(
            p => p.GetRepositoryStructureLoadResultAsync(repository.RepositoryUrl!, It.IsAny<int>(), It.IsAny<CancellationToken>(), "develop"),
            Times.Once);
        sut.InitializationScriptSuggestions.Should().Contain("init.sh");
    }

    /// <summary>Die gefilterte Sicht auf die Vorschlagsliste engt sich beim Eingeben von Text auf passende Einträge ein (Live-Filter/Suchfeld-Verhalten).</summary>
    [Fact]
    public async Task InitializationscriptSuggestionsView_ShouldFilter_WhenSelectedTextChanges()
    {
        var (project, _) = await CreateProjectWithRepositoryAsync();
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success(
            [
                new RepositoryDirectoryEntry("scripts/init.ps1", IsDirectory: false),
                new RepositoryDirectoryEntry("scripts/setup.sh", IsDirectory: false),
                new RepositoryDirectoryEntry("tools/build.cmd", IsDirectory: false)
            ]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();

        sut.InitializationScriptSuggestionsView.Cast<string>().Should().HaveCount(3);

        sut.SelectedInitializationScript = "scripts/";

        sut.InitializationScriptSuggestionsView.Cast<string>().Should().BeEquivalentTo(["scripts/init.ps1", "scripts/setup.sh"]);
    }

    /// <summary>Schlägt der Remote-Zugriff fehl, bleibt die UI responsiv und der Fehler wird über ein Flag signalisiert.</summary>
    [Fact]
    public async Task LoadInitializationscriptSuggestionsAsync_ShouldHandleNetworkError_Gracefully()
    {
        var (project, _) = await CreateProjectWithRepositoryAsync();
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Netzwerkfehler"));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var act = () => ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();

        await act.Should().NotThrowAsync();
        sut.InitializationScriptLoadingFailed.Should().BeTrue();
        sut.InitializationScriptSuggestions.Should().BeEmpty();
    }

    /// <summary>Speichern des ausgewählten Skripts persistiert den Wert über den ProjektService.</summary>
    [Fact]
    public async Task SaveInitializationscriptAsync_ShouldPersist_SelectedScript()
    {
        var (project, repository) = await CreateProjectWithRepositoryAsync();
        await _projectService.SaveRepositoryInitializationScriptAsync(repository.Id, "scripts/old.ps1", CancellationToken.None);
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.SelectedInitializationScript.Should().Be("scripts/old.ps1");

        await ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();
        sut.SelectedInitializationScript = "scripts/new.ps1";

        await ((AsyncRelayCommand)sut.SaveInitializationScriptCommand).ExecuteAsync();

        sut.IsEditingInitializationScript.Should().BeFalse();
        sut.SelectedInitializationScript.Should().Be("scripts/new.ps1");
        var persisted = await _projectService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        persisted!.Repositories.Single(r => r.Id == repository.Id).InitializationConfiguration!.InitializationScriptRelativePath.Should().Be("scripts/new.ps1");
    }

    /// <summary>Ist noch keine Konfiguration vorhanden, legt das Speichern eine neue an.</summary>
    [Fact]
    public async Task SaveInitializationscriptAsync_ShouldCreateConfiguration_IfNotExists()
    {
        var (project, repository) = await CreateProjectWithRepositoryAsync();
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.SelectedInitializationScript.Should().BeNull();

        await ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();
        sut.SelectedInitializationScript = "scripts/init.ps1";

        await ((AsyncRelayCommand)sut.SaveInitializationScriptCommand).ExecuteAsync();

        var persisted = await _projectService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        var configuration = persisted!.Repositories.Single(r => r.Id == repository.Id).InitializationConfiguration;
        configuration.Should().NotBeNull();
        configuration!.InitializationScriptRelativePath.Should().Be("scripts/init.ps1");
        configuration.Active.Should().BeTrue();
    }

    /// <summary>Das Cancel der Bearbeitung verwirft Änderungen und schließt den Edit-Modus, ohne zu speichern.</summary>
    [Fact]
    public async Task CancelInitializationscriptEdit_ShouldDiscardChanges()
    {
        var (project, repository) = await CreateProjectWithRepositoryAsync();
        await _projectService.SaveRepositoryInitializationScriptAsync(repository.Id, "scripts/init.ps1", CancellationToken.None);
        var pluginMock = CreatePluginMock("Softwareschmiede.GitHub");
        pluginMock.Setup(p => p.GetRepositoryStructureLoadResultAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryStructureLoadResult.Success([]));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.LoadInitializationScriptSuggestionsCommand).ExecuteAsync();
        sut.SelectedInitializationScript = "scripts/geaendert.ps1";

        ((RelayCommand)sut.CancelInitializationScriptEditCommand).Execute(null);

        sut.IsEditingInitializationScript.Should().BeFalse();
        sut.SelectedInitializationScript.Should().Be("scripts/init.ps1");
        var persisted = await _projectService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        persisted!.Repositories.Single(r => r.Id == repository.Id).InitializationConfiguration!.InitializationScriptRelativePath.Should().Be("scripts/init.ps1");
    }
}
