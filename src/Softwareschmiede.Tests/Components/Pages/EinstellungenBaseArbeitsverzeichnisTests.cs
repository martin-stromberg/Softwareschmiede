using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Components.Pages;

/// <summary>EinstellungenBaseArbeitsverzeichnisTests.</summary>
public sealed class EinstellungenBaseArbeitsverzeichnisTests
{
    /// <summary><summary>OnInitializedAsync_ShouldLoadSavedWorkdirAndFallbackHint_WhenResolverUsesFallback.</summary>.</summary>
    [Fact]
    /// <summary>OnInitializedAsync_ShouldLoadSavedWorkdirAndFallbackHint_WhenResolverUsesFallback.</summary>
    public async Task OnInitializedAsync_ShouldLoadSavedWorkdirAndFallbackHint_WhenResolverUsesFallback()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var savedPath = Path.Combine(Path.GetTempPath(), $"ui-workdir-{Guid.NewGuid():N}");
        await settingsService.SaveArbeitsverzeichnisAsync(savedPath);

        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), true, "no-configured-path", null));

        var sut = CreateSut(db, settingsService, resolverMock.Object);

        await sut.InvokeOnInitializedAsync();

        sut.ArbeitsverzeichnisInput.Should().Be(Path.GetFullPath(savedPath));
        sut.ArbeitsverzeichnisFallbackHinweis.Should().Contain("Fallback verwendet");
    }

    /// <summary><summary>ArbeitsverzeichnisSpeichernAsync_ShouldSetValidationError_WhenInputIsInvalid.</summary>.</summary>
    [Fact]
    /// <summary>ArbeitsverzeichnisSpeichernAsync_ShouldSetValidationError_WhenInputIsInvalid.</summary>
    public async Task ArbeitsverzeichnisSpeichernAsync_ShouldSetValidationError_WhenInputIsInvalid()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));
        var sut = CreateSut(db, settingsService, resolverMock.Object);

        sut.InvokeArbeitsverzeichnisInputChanged("relative\\invalid");

        await sut.InvokeArbeitsverzeichnisSpeichernAsync();

        sut.ArbeitsverzeichnisValidationError.Should().NotBeNullOrEmpty();
        sut.ArbeitsverzeichnisStatusIsError.Should().BeTrue();
        sut.ArbeitsverzeichnisStatusMessage.Should().Be("Arbeitsverzeichnis konnte nicht gespeichert werden.");
    }

    /// <summary><summary>ArbeitsverzeichnisZuruecksetzenAsync_ShouldClearStoredWorkdir.</summary>.</summary>
    [Fact]
    /// <summary>ArbeitsverzeichnisZuruecksetzenAsync_ShouldClearStoredWorkdir.</summary>
    public async Task ArbeitsverzeichnisZuruecksetzenAsync_ShouldClearStoredWorkdir()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var configuredPath = Path.Combine(Path.GetTempPath(), $"ui-reset-{Guid.NewGuid():N}");
        await settingsService.SaveArbeitsverzeichnisAsync(configuredPath);

        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));
        var sut = CreateSut(db, settingsService, resolverMock.Object);
        await sut.InvokeOnInitializedAsync();

        await sut.InvokeArbeitsverzeichnisZuruecksetzenAsync();

        var reloaded = await settingsService.GetArbeitsverzeichnisAsync();
        reloaded.Should().BeNull();
        sut.ArbeitsverzeichnisInput.Should().BeEmpty();
        sut.ArbeitsverzeichnisStatusIsError.Should().BeFalse();
    }

    /// <summary><summary>OnInitializedAsync_ShouldLoadStoredDefaultPlugin_WhenPrefixIsValid.</summary>.</summary>
    [Fact]
    /// <summary>OnInitializedAsync_ShouldLoadStoredDefaultPlugin_WhenPrefixIsValid.</summary>
    public async Task OnInitializedAsync_ShouldLoadStoredDefaultPlugin_WhenPrefixIsValid()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var gitPlugin = CreateGitPlugin("GitHub", "Softwareschmiede.GitHub");
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.GitHub");

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            scmPlugins: [gitPlugin],
            kiPlugins: []);

        await sut.InvokeOnInitializedAsync();

        sut.GetDefaultPluginSelection(PluginType.SourceCodeManagement).Should().Be("Softwareschmiede.GitHub");
    }

    /// <summary><summary>StandardPluginSpeichernAsync_ShouldPersistValidSelection.</summary>.</summary>
    [Fact]
    /// <summary>StandardPluginSpeichernAsync_ShouldPersistValidSelection.</summary>
    public async Task StandardPluginSpeichernAsync_ShouldPersistValidSelection()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var gitPlugin = CreateGitPlugin("GitHub", "Softwareschmiede.GitHub");
        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            scmPlugins: [gitPlugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        sut.SetDefaultPluginSelection(PluginType.SourceCodeManagement, "Softwareschmiede.GitHub");

        await sut.InvokeStandardPluginSpeichernAsync(PluginType.SourceCodeManagement);

        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var saved = await defaultSettings.GetDefaultPluginPrefixAsync(PluginType.SourceCodeManagement);
        saved.Should().Be("Softwareschmiede.GitHub");
        sut.GetDefaultPluginStatusIsError(PluginType.SourceCodeManagement).Should().BeFalse();
    }

    /// <summary><summary>StandardPluginSpeichernAsync_ShouldSetError_WhenSelectionIsInvalidForType.</summary>.</summary>
    [Fact]
    /// <summary>StandardPluginSpeichernAsync_ShouldSetError_WhenSelectionIsInvalidForType.</summary>
    public async Task StandardPluginSpeichernAsync_ShouldSetError_WhenSelectionIsInvalidForType()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var gitPlugin = CreateGitPlugin("GitHub", "Softwareschmiede.GitHub");
        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            scmPlugins: [gitPlugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        sut.SetDefaultPluginSelection(PluginType.SourceCodeManagement, "Softwareschmiede.Unbekannt");

        await sut.InvokeStandardPluginSpeichernAsync(PluginType.SourceCodeManagement);

        sut.GetDefaultPluginStatusIsError(PluginType.SourceCodeManagement).Should().BeTrue();
        sut.GetDefaultPluginStatusMessage(PluginType.SourceCodeManagement).Should().Contain("Ungültige Plugin-Auswahl");
    }

    /// <summary><summary>OnInitializedAsync_ShouldFallbackForInvalidEnumSetting.</summary>.</summary>
    [Fact]
    /// <summary>OnInitializedAsync_ShouldFallbackForInvalidEnumSetting.</summary>
    public async Task OnInitializedAsync_ShouldFallbackForInvalidEnumSetting()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock
            .Setup(c => c.GetCredential("LocalDirectoryPlugin.WorkspaceMode"))
            .Returns("InvalidMode");

        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: ["InSourceDirectory", "SeparateWorkingDirectory"])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);

        await sut.InvokeOnInitializedAsync();

        sut.GetInputValue("LocalDirectoryPlugin.WorkspaceMode").Should().Be("SeparateWorkingDirectory");
        sut.GetFieldValidationMessage("LocalDirectoryPlugin.WorkspaceMode").Should().Contain("Ungültiger gespeicherter Wert");
    }

    /// <summary><summary>SpeichernAsync_ShouldPersistEnumValueAsStableString.</summary>.</summary>
    [Fact]
    /// <summary>SpeichernAsync_ShouldPersistEnumValueAsStableString.</summary>
    public async Task SpeichernAsync_ShouldPersistEnumValueAsStableString()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();

        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: ["InSourceDirectory", "SeparateWorkingDirectory"])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        sut.SetInputValue("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory");

        await sut.InvokeSpeichernAsync(plugin);

        credentialStoreMock.Verify(
            c => c.SetCredential("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory"),
            Times.Once);
    }

    /// <summary><summary>SpeichernAsync_ShouldRejectInvalidEnumSelection_AndNotPersist.</summary>.</summary>
    [Fact]
    /// <summary>SpeichernAsync_ShouldRejectInvalidEnumSelection_AndNotPersist.</summary>
    public async Task SpeichernAsync_ShouldRejectInvalidEnumSelection_AndNotPersist()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: ["InSourceDirectory", "SeparateWorkingDirectory"])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        sut.SetInputValue("LocalDirectoryPlugin.WorkspaceMode", "invalid-value");

        await sut.InvokeSpeichernAsync(plugin);

        sut.GetFieldValidationMessage("LocalDirectoryPlugin.WorkspaceMode").Should().Be("Ungültige Auswahl.");
        credentialStoreMock.Verify(c => c.SetCredential(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary><summary>ZuruecksetzenAsync_ShouldResetEnumToSeparateWorkingDirectory_WhenAvailable.</summary>.</summary>
    [Fact]
    /// <summary>ZuruecksetzenAsync_ShouldResetEnumToSeparateWorkingDirectory_WhenAvailable.</summary>
    public async Task ZuruecksetzenAsync_ShouldResetEnumToSeparateWorkingDirectory_WhenAvailable()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: ["InSourceDirectory", "SeparateWorkingDirectory"])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        sut.SetInputValue("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory");

        await sut.InvokeZuruecksetzenAsync(plugin);

        sut.GetInputValue("LocalDirectoryPlugin.WorkspaceMode").Should().Be("SeparateWorkingDirectory");
    }

    /// <summary><summary>ZuruecksetzenAsync_ShouldResetEnumToFirstOption_WhenSeparateModeNotPresent.</summary>.</summary>
    [Fact]
    /// <summary>ZuruecksetzenAsync_ShouldResetEnumToFirstOption_WhenSeparateModeNotPresent.</summary>
    public async Task ZuruecksetzenAsync_ShouldResetEnumToFirstOption_WhenSeparateModeNotPresent()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: ["InSourceDirectory", "PreviewOnly"])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();

        await sut.InvokeZuruecksetzenAsync(plugin);

        sut.GetInputValue("LocalDirectoryPlugin.WorkspaceMode").Should().Be("InSourceDirectory");
    }

    /// <summary><summary>OnInitializedAsync_ShouldTrimStoredEnumValue_WhenValid.</summary>.</summary>
    [Fact]
    /// <summary>OnInitializedAsync_ShouldTrimStoredEnumValue_WhenValid.</summary>
    public async Task OnInitializedAsync_ShouldTrimStoredEnumValue_WhenValid()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns(" InSourceDirectory ");

        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: ["InSourceDirectory", "SeparateWorkingDirectory"])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);

        await sut.InvokeOnInitializedAsync();

        sut.GetInputValue("LocalDirectoryPlugin.WorkspaceMode").Should().Be("InSourceDirectory");
    }

    /// <summary><summary>OnInitializedAsync_ShouldUseEmptyValue_WhenEnumOptionsMissing.</summary>.</summary>
    [Fact]
    /// <summary>OnInitializedAsync_ShouldUseEmptyValue_WhenEnumOptionsMissing.</summary>
    public async Task OnInitializedAsync_ShouldUseEmptyValue_WhenEnumOptionsMissing()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential("LocalDirectoryPlugin.WorkspaceMode")).Returns("InSourceDirectory");

        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: [])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);

        await sut.InvokeOnInitializedAsync();

        sut.GetInputValue("LocalDirectoryPlugin.WorkspaceMode").Should().BeEmpty();
    }

    /// <summary><summary>SpeichernAsync_ShouldSetValidationMessage_ForEnumWithoutOptions.</summary>.</summary>
    [Fact]
    /// <summary>SpeichernAsync_ShouldSetValidationMessage_ForEnumWithoutOptions.</summary>
    public async Task SpeichernAsync_ShouldSetValidationMessage_ForEnumWithoutOptions()
    {
        await using var db = CreateDb();
        var settingsService = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var resolverMock = new Mock<IArbeitsverzeichnisResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));

        var credentialStoreMock = new Mock<ICredentialStore>();
        var plugin = CreateGitPlugin(
            "Local Directory",
            "LocalDirectoryPlugin",
            [
                new PluginSettingGroup("Workspace",
                [
                    new PluginSettingField(
                        "WorkspaceMode",
                        "WorkspaceMode",
                        PluginSettingFieldType.Enum,
                        EnumOptions: [])
                ])
            ]);

        var sut = CreateSut(
            db,
            settingsService,
            resolverMock.Object,
            credentialStoreMock,
            scmPlugins: [plugin],
            kiPlugins: []);
        await sut.InvokeOnInitializedAsync();
        sut.SetInputValue("LocalDirectoryPlugin.WorkspaceMode", "InSourceDirectory");

        await sut.InvokeSpeichernAsync(plugin);

        sut.GetFieldValidationMessage("LocalDirectoryPlugin.WorkspaceMode").Should().Be("Ungültige Auswahl.");
        credentialStoreMock.Verify(c => c.SetCredential(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary><summary>GetEnumOptionDisplayLabel_ShouldReturnTranslatedLabel_ForWorkspaceModeOptions.</summary>.</summary>
    [Fact]
    /// <summary>GetEnumOptionDisplayLabel_ShouldReturnTranslatedLabel_ForWorkspaceModeOptions.</summary>
    public void GetEnumOptionDisplayLabel_ShouldReturnTranslatedLabel_ForWorkspaceModeOptions()
    {
        var field = new PluginSettingField(
            "WorkspaceMode",
            "WorkspaceMode",
            PluginSettingFieldType.Enum,
            EnumOptions: ["InSourceDirectory", "SeparateWorkingDirectory"]);

        var labelInSource = TestEinstellungenPage.InvokeEnumDisplayLabel(field, "InSourceDirectory");
        var labelSeparate = TestEinstellungenPage.InvokeEnumDisplayLabel(field, "SeparateWorkingDirectory");

        labelInSource.Should().Be("Direkt im Quellverzeichnis arbeiten");
        labelSeparate.Should().Be("Mit separatem Arbeitsverzeichnis arbeiten");
    }

    /// <summary><summary>EinstellungenMarkup_ShouldUseEnumDisplayLabelHelper_ForWorkspaceModeRendering.</summary>.</summary>
    [Fact]
    /// <summary>EinstellungenMarkup_ShouldUseEnumDisplayLabelHelper_ForWorkspaceModeRendering.</summary>
    public void EinstellungenMarkup_ShouldUseEnumDisplayLabelHelper_ForWorkspaceModeRendering()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Einstellungen.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("@GetEnumOptionDisplayLabel(field, enumOption)");
    }

    /// <summary><summary>EinstellungenMarkup_ShouldRenderPluginFieldsDynamically_WithoutHardcodedWorkingDirectoryField.</summary>.</summary>
    [Fact]
    /// <summary>EinstellungenMarkup_ShouldRenderPluginFieldsDynamically_WithoutHardcodedWorkingDirectoryField.</summary>
    public void EinstellungenMarkup_ShouldRenderPluginFieldsDynamically_WithoutHardcodedWorkingDirectoryField()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Einstellungen.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("@foreach (var field in group.Fields)");
        markup.Should().NotContain("LocalDirectoryPlugin.WorkingDirectory");
    }

    private static TestEinstellungenPage CreateSut(
        SoftwareschmiededDbContext db,
        ArbeitsverzeichnisSettingsService arbeitsverzeichnisSettings,
        IArbeitsverzeichnisResolver arbeitsverzeichnisResolver,
        Mock<ICredentialStore>? credentialStoreMock = null,
        IReadOnlyList<IGitPlugin>? scmPlugins = null,
        IReadOnlyList<IKiPlugin>? kiPlugins = null)
    {
        credentialStoreMock ??= new Mock<ICredentialStore>();
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns(scmPlugins ?? Array.Empty<IGitPlugin>());
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns(kiPlugins ?? Array.Empty<IKiPlugin>());
        var pluginDefaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginSelection = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            NullLogger<PluginSelectionService>.Instance);
        var pluginSettings = new PluginSettingsService(credentialStoreMock.Object, NullLogger<PluginSettingsService>.Instance);
        var benachrichtigungen = new BenachrichtigungsEinstellungenService(db);
        var benutzerkontextMock = new Mock<IBenutzerkontextService>();
        benutzerkontextMock.Setup(x => x.GetBenutzerId()).Returns("test-user");
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var sut = new TestEinstellungenPage();
        SetInjectedProperty(sut, "PluginManager", pluginManagerMock.Object);
        SetInjectedProperty(sut, "PluginSelection", pluginSelection);
        SetInjectedProperty(sut, "PluginSettings", pluginSettings);
        SetInjectedProperty(sut, "ArbeitsverzeichnisSettings", arbeitsverzeichnisSettings);
        SetInjectedProperty(sut, "ArbeitsverzeichnisResolver", arbeitsverzeichnisResolver);
        SetInjectedProperty(sut, "BenachrichtigungsEinstellungen", benachrichtigungen);
        SetInjectedProperty(sut, "Benutzerkontext", benutzerkontextMock.Object);
        SetInjectedProperty(sut, "JsRuntime", jsRuntimeMock.Object);
        SetInjectedProperty(sut, "Logger", NullLogger<EinstellungenBase>.Instance);
        return sut;
    }

    private static IGitPlugin CreateGitPlugin(string name, string prefix, IReadOnlyList<PluginSettingGroup>? groups = null)
    {
        var plugin = new Mock<IGitPlugin>();
        plugin.SetupGet(p => p.PluginName).Returns(name);
        plugin.SetupGet(p => p.PluginPrefix).Returns(prefix);
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        plugin.Setup(p => p.GetSettingGroups()).Returns(groups ?? []);
        return plugin.Object;
    }

    private static SoftwareschmiededDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SoftwareschmiededDbContext(options);
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var property = typeof(EinstellungenBase).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
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

        throw new DirectoryNotFoundException("Repository root with Softwareschmiede.slnx not found.");
    }

    private sealed class TestEinstellungenPage : EinstellungenBase
    {
        public string ArbeitsverzeichnisInput => _arbeitsverzeichnisInput;
        public string? ArbeitsverzeichnisValidationError => _arbeitsverzeichnisValidationError;
        public string? ArbeitsverzeichnisStatusMessage => _arbeitsverzeichnisStatusMessage;
        public bool ArbeitsverzeichnisStatusIsError => _arbeitsverzeichnisStatusIsError;
        public string? ArbeitsverzeichnisFallbackHinweis => _arbeitsverzeichnisFallbackHinweis;
        public string? GetDefaultPluginSelection(PluginType pluginType) => _defaultPluginSelections.GetValueOrDefault(pluginType);
        /// <summary>SetDefaultPluginSelection.</summary>
        public void SetDefaultPluginSelection(PluginType pluginType, string? pluginPrefix) => _defaultPluginSelections[pluginType] = pluginPrefix;
        /// <summary>GetDefaultPluginStatusMessage.</summary>
        public string GetDefaultPluginStatusMessage(PluginType pluginType) => _defaultPluginStatusMessages.GetValueOrDefault(pluginType, string.Empty);
        /// <summary>GetDefaultPluginStatusIsError.</summary>
        public bool GetDefaultPluginStatusIsError(PluginType pluginType) => _defaultPluginStatusIsError.GetValueOrDefault(pluginType);
        /// <summary>GetInputValue.</summary>
        public string GetInputValue(string stateKey) => _inputValues.GetValueOrDefault(stateKey, string.Empty);
        /// <summary>GetFieldValidationMessage.</summary>
        public string GetFieldValidationMessage(string stateKey) => _fieldValidationMessages.GetValueOrDefault(stateKey, string.Empty);
        /// <summary>SetInputValue.</summary>
        public void SetInputValue(string stateKey, string value) => _inputValues[stateKey] = value;

        /// <summary>InvokeOnInitializedAsync.</summary>
        public Task InvokeOnInitializedAsync() => OnInitializedAsync();
        /// <summary>InvokeArbeitsverzeichnisSpeichernAsync.</summary>
        public Task InvokeArbeitsverzeichnisSpeichernAsync() => ArbeitsverzeichnisSpeichernAsync();
        /// <summary>InvokeArbeitsverzeichnisInputChanged.</summary>
        public void InvokeArbeitsverzeichnisInputChanged(string value) => ArbeitsverzeichnisInputChanged(value);
        /// <summary>InvokeArbeitsverzeichnisZuruecksetzenAsync.</summary>
        public Task InvokeArbeitsverzeichnisZuruecksetzenAsync() => ArbeitsverzeichnisZuruecksetzenAsync();
        /// <summary>InvokeStandardPluginSpeichernAsync.</summary>
        public Task InvokeStandardPluginSpeichernAsync(PluginType pluginType) => StandardPluginSpeichernAsync(pluginType);
        /// <summary>InvokeSpeichernAsync.</summary>
        public Task InvokeSpeichernAsync(IPlugin plugin) => SpeichernAsync(plugin);
        /// <summary>InvokeZuruecksetzenAsync.</summary>
        public Task InvokeZuruecksetzenAsync(IPlugin plugin) => ZuruecksetzenAsync(plugin);
        /// <summary>InvokeEnumDisplayLabel.</summary>
        public static string InvokeEnumDisplayLabel(PluginSettingField field, string enumOption) => GetEnumOptionDisplayLabel(field, enumOption);
    }
}
