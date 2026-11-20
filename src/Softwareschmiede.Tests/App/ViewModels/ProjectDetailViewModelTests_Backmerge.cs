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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly AppSettingService _settingService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests_Backmerge.</summary>
    public ProjectDetailViewModelTests_Backmerge()
    {
        _db = TestDbContextFactory.Create();
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        ConfigureSettingScope();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private void ConfigureSettingScope()
    {
        var providerMock = new Mock<IServiceProvider>();
        providerMock.Setup(p => p.GetService(typeof(AppSettingService))).Returns(_settingService);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
    }

    private ProjectDetailViewModel CreateSut() =>
        new(
            _projectService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);

    private async Task<Project> CreateProjectWithRepositoryAsync(string? baseBranch = "staging")
    {
        var project = await _projectService.CreateAsync("Backmerge-Test-Projekt", null);
        await _projectService.AddRepositoryAsync(
            project.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo",
            defaultSourceBranchName: baseBranch,
            ct: CancellationToken.None);
        return project;
    }

    private static async Task LoadAsync(ProjectDetailViewModel sut, Guid projectId)
    {
        sut.ProjectId = projectId;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
    }

    /// <summary>Im Modus „Projektbezogen aktivierbar“ ist die Option bei konfiguriertem Basis-Branch sichtbar
    /// und der gespeicherte Projekt-Schalter wird geladen.</summary>
    [Fact]
    public async Task LoadAsync_ProjectspecificMode_OptionVisibleAndFlagLoaded()
    {
        var project = await CreateProjectWithRepositoryAsync();
        await _settingService.SetScmBackmergeModeAsync(ScmBackmergeMode.ProjectScoped, TestContext.Current.CancellationToken);
        await _settingService.SetBackmergeProjectEnabledAsync(project.Id, true, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await LoadAsync(sut, project.Id);

        sut.IsBackmergeOptionVisible.Should().BeTrue();
        sut.IsBackmergeAutoMergeEnabled.Should().BeTrue();
    }

    /// <summary>Ohne Modus „Projektbezogen aktivierbar“ bleibt die Option verborgen.</summary>
    [Theory]
    [InlineData(ScmBackmergeMode.Disabled)]
    [InlineData(ScmBackmergeMode.Global)]
    public async Task LoadAsync_NotProjectspecificMode_OptionHidden(ScmBackmergeMode mode)
    {
        var project = await CreateProjectWithRepositoryAsync();
        await _settingService.SetScmBackmergeModeAsync(mode, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await LoadAsync(sut, project.Id);

        sut.IsBackmergeOptionVisible.Should().BeFalse();
    }

    /// <summary>Ohne konfigurierten Basis-Branch bleibt die Option trotz projektbezogenem Modus verborgen.</summary>
    [Fact]
    public async Task LoadAsync_WithoutBaseBranch_OptionHidden()
    {
        var project = await CreateProjectWithRepositoryAsync(baseBranch: null);
        await _settingService.SetScmBackmergeModeAsync(ScmBackmergeMode.ProjectScoped, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await LoadAsync(sut, project.Id);

        sut.IsBackmergeOptionVisible.Should().BeFalse();
    }

    /// <summary>Das Umschalten der Option persistiert die Projekt-Freigabe sofort.</summary>
    [Fact]
    public async Task IsBackmergeAutoMergeEnabled_Toggle_PersistedProjectFlag()
    {
        var project = await CreateProjectWithRepositoryAsync();
        await _settingService.SetScmBackmergeModeAsync(ScmBackmergeMode.ProjectScoped, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        await LoadAsync(sut, project.Id);

        sut.IsBackmergeAutoMergeEnabled = true;

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await _settingService.GetBackmergeProjectEnabledAsync(project.Id, TestContext.Current.CancellationToken))
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "die Freigabe muss zeitnah persistiert werden");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
