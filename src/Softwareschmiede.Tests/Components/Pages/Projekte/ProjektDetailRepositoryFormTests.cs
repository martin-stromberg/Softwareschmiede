using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages.Projekte;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Components.Pages.Projekte;

public sealed class ProjektDetailRepositoryFormTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db = TestDbContextFactory.Create();

    [Fact]
    public void ProjektDetailMarkup_ShouldRenderDynamicRepositoryFields()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Projekte", "ProjektDetail.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("@foreach (var field in _repositoryLinkFields)");
        markup.Should().Contain("@GetRepositoryInputType(field)");
        markup.Should().Contain("SetRepositoryFieldValue(fieldKey");
    }

    [Fact]
    public async Task OnInitializedAsync_ShouldPreselectStoredDefaultPlugin_AndLoadItsFields()
    {
        var github = CreateGitPlugin(
            "GitHub",
            "Softwareschmiede.GitHub",
            [
                new PluginSettingField("RepositoryUrl", "Repository URL", PluginSettingFieldType.Url, IsRequired: true),
                new PluginSettingField("RepositoryName", "Repository Name", PluginSettingFieldType.Text, IsRequired: true)
            ]);
        var local = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingField("SourceDirectory", "Source Directory", PluginSettingFieldType.Text, IsRequired: true)
            ]);

        var sut = await CreateSutAsync([local, github], storedDefaultPluginPrefix: "Softwareschmiede.GitHub");

        await sut.InvokeOnInitializedAsync();

        GetPrivateField<string?>(sut, "_selectedRepositoryPluginPrefix").Should().Be("Softwareschmiede.GitHub");
        var fields = GetPrivateField<IReadOnlyList<PluginSettingField>>(sut, "_repositoryLinkFields");
        fields.Should().ContainSingle(f => f.Key == "RepositoryUrl" && f.IsRequired);
        fields.Should().ContainSingle(f => f.Key == "RepositoryName" && f.IsRequired);
    }

    [Fact]
    public async Task OnRepositoryPluginChangedAsync_ShouldRemoveObsoleteFieldValues()
    {
        var github = CreateGitPlugin(
            "GitHub",
            "Softwareschmiede.GitHub",
            [
                new PluginSettingField("RepositoryUrl", "Repository URL", PluginSettingFieldType.Url, IsRequired: true),
                new PluginSettingField("RepositoryName", "Repository Name", PluginSettingFieldType.Text, IsRequired: true)
            ]);
        var local = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingField("SourceDirectory", "Source Directory", PluginSettingFieldType.Text, IsRequired: true)
            ]);

        var sut = await CreateSutAsync([github, local], storedDefaultPluginPrefix: "Softwareschmiede.GitHub");
        await sut.InvokeOnInitializedAsync();

        var fieldValues = GetPrivateField<Dictionary<string, string>>(sut, "_repositoryFieldValues");
        fieldValues["RepositoryUrl"] = "https://github.com/owner/repo";
        fieldValues["LegacyField"] = "legacy";

        await InvokePrivateAsync(
            sut,
            "OnRepositoryPluginChangedAsync",
            new ChangeEventArgs { Value = "LocalDirectoryPlugin" });

        GetPrivateField<string?>(sut, "_selectedRepositoryPluginPrefix").Should().Be("LocalDirectoryPlugin");
        fieldValues.Should().ContainKey("SourceDirectory");
        fieldValues.Should().NotContainKey("RepositoryUrl");
        fieldValues.Should().NotContainKey("LegacyField");
    }

    [Fact]
    public async Task AddRepositoryAsync_ShouldSetError_WhenRequiredFieldIsMissing()
    {
        var github = CreateGitPlugin(
            "GitHub",
            "Softwareschmiede.GitHub",
            [
                new PluginSettingField("RepositoryUrl", "Repository URL", PluginSettingFieldType.Url, IsRequired: true),
                new PluginSettingField("RepositoryName", "Repository Name", PluginSettingFieldType.Text, IsRequired: true)
            ]);
        var sut = await CreateSutAsync([github], storedDefaultPluginPrefix: "Softwareschmiede.GitHub");
        await sut.InvokeOnInitializedAsync();

        await InvokePrivateAsync(sut, "AddRepositoryAsync");

        GetPrivateField<string?>(sut, "_repoFehler").Should().Contain("Pflichtfeld");
        _db.GitRepositories.Count().Should().Be(0);
    }

    [Fact]
    public async Task AddRepositoryAsync_ShouldSetError_WhenNoScmPluginsAvailable()
    {
        var sut = await CreateSutAsync([]);
        await sut.InvokeOnInitializedAsync();

        await InvokePrivateAsync(sut, "AddRepositoryAsync");

        GetPrivateField<string?>(sut, "_repoFehler").Should().Be("Es ist kein SourceCode-Plugin verfügbar.");
        _db.GitRepositories.Count().Should().Be(0);
    }

    public void Dispose() => _db.Dispose();

    private async Task<TestProjektDetailPage> CreateSutAsync(
        IReadOnlyList<IGitPlugin> scmPlugins,
        string? storedDefaultPluginPrefix = null)
    {
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Projekt für Repository-Form",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        _db.Projekte.Add(projekt);
        await _db.SaveChangesAsync();

        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns(scmPlugins);
        if (scmPlugins.Count > 0)
        {
            pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(scmPlugins[0]);
        }

        var defaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        if (!string.IsNullOrWhiteSpace(storedDefaultPluginPrefix))
        {
            await defaultSettingsService.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, storedDefaultPluginPrefix);
        }

        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            defaultSettingsService,
            NullLogger<PluginSelectionService>.Instance);

        var sut = new TestProjektDetailPage { Id = projekt.Id };
        SetInjectedProperty(sut, "ProjektService", projektService);
        SetInjectedProperty(sut, "AufgabeService", aufgabeService);
        SetInjectedProperty(sut, "PluginManager", pluginManagerMock.Object);
        SetInjectedProperty(sut, "PluginSelectionService", pluginSelectionService);
        SetInjectedProperty(sut, "NavigationManager", new TestNavigationManager());

        return sut;
    }

    private static IGitPlugin CreateGitPlugin(
        string pluginName,
        string pluginPrefix,
        IReadOnlyList<PluginSettingField> repositoryFields)
    {
        var plugin = new Mock<IGitPlugin>();
        plugin.SetupGet(p => p.PluginName).Returns(pluginName);
        plugin.SetupGet(p => p.PluginPrefix).Returns(pluginPrefix);
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        plugin.Setup(p => p.GetSettingGroups()).Returns([]);
        plugin.Setup(p => p.GetRepositoryLinkFields()).Returns(repositoryFields);
        return plugin.Object;
    }

    private static async Task InvokePrivateAsync(object target, string methodName, params object?[] args)
    {
        var method = typeof(ProjektDetail)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name.Equals(methodName, StringComparison.Ordinal)
                && m.GetParameters().Length == args.Length
                && m.GetParameters().Zip(args, (parameter, argument) => argument is null || parameter.ParameterType.IsInstanceOfType(argument)).All(x => x));
        method.Should().NotBeNull($"Method {methodName} should exist.");

        var result = method!.Invoke(target, args);
        if (result is Task task)
        {
            await task;
            return;
        }

        if (result is ValueTask valueTask)
        {
            await valueTask;
            return;
        }

        if (result is null)
        {
            return;
        }

        throw new InvalidOperationException($"{methodName} did not return Task or ValueTask.");
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = typeof(ProjektDetail).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Field {fieldName} should exist.");
        return (T)field!.GetValue(target)!;
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var property = typeof(ProjektDetail).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist for test setup.");
        property!.SetValue(target, value);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Softwareschmiede.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }

    private sealed class TestProjektDetailPage : ProjektDetail
    {
        public Task InvokeOnInitializedAsync() => OnInitializedAsync();
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
