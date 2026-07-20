using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für <see cref="IssueCreateDialogViewModel"/>.</summary>
public sealed class IssueCreateDialogViewModelTests
{
    private readonly Mock<IPluginManager> _pluginManagerMock = new();
    private readonly FakeIssueProvider _issueProvider = new();
    private readonly FakeTemplateProvider _templateProvider = new();

    private IssueCreateDialogViewModel CreateSut()
    {
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        return new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
    }

    /// <summary>Initialisiert Titel und Body aus der Aufgabe.</summary>
    [Fact]
    public void Initialize_ShouldUseTaskTitleAndOriginalRequirement()
    {
        var sut = CreateSut();

        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Beschreibung", null);

        sut.Title.Should().Be("Aufgabe");
        sut.Body.Should().Be("Beschreibung");
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>Whitespace-Beschreibung wird als leerer Body behandelt.</summary>
    [Fact]
    public void Initialize_ShouldUseEmptyBody_WhenOriginalRequirementIsWhitespace()
    {
        var sut = CreateSut();

        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "   ", null);

        sut.Body.Should().BeEmpty();
    }

    /// <summary>Template-Auswahl setzt Template, Trennlinie und Originalanforderung zusammen.</summary>
    [Fact]
    public void SelectedTemplate_ShouldComposeBodyWithOriginalRequirement()
    {
        var sut = CreateSut();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        sut.Body.Should().Be("Template\n\n---\n\nOriginalanforderung:\nOriginal");
    }

    /// <summary>Templates werden geladen und Nichtunterstützung blockiert den No-Template-Pfad nicht.</summary>
    [Fact]
    public async Task LoadTemplatesAsync_ShouldKeepSubmitEnabled_WhenNoTemplatesAvailable()
    {
        var sut = CreateSut();
        _templateProvider.Result = IssueTemplateLoadResult.NotSupported();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        await ((AsyncRelayCommand)sut.LoadTemplatesCommand).ExecuteAsync();

        sut.Templates.Should().BeEmpty();
        sut.ErrorMessage.Should().BeNull();
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>Template-Ladefehler zeigt eine Meldung, blockiert die Anlage ohne Template aber nicht.</summary>
    [Fact]
    public async Task LoadTemplatesAsync_ShouldShowErrorAndKeepSubmitEnabled_WhenProviderFails()
    {
        var sut = CreateSut();
        _templateProvider.Result = IssueTemplateLoadResult.Failed("Netzwerkfehler");
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        await ((AsyncRelayCommand)sut.LoadTemplatesCommand).ExecuteAsync();

        sut.Templates.Should().BeEmpty();
        sut.ErrorMessage.Should().Be("Netzwerkfehler");
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>Erneute Template-Auswahl ersetzt das Template, erhält aber die Originalanforderung.</summary>
    [Fact]
    public void SelectedTemplate_ShouldRecomposeBodyAndKeepOriginalRequirement_WhenTemplateChanges()
    {
        var sut = CreateSut();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        sut.SelectedTemplate = new IssueTemplate("Bug", "Template A");
        sut.Body = "Manuell bearbeitet";
        sut.SelectedTemplate = new IssueTemplate("Feature", "Template B");

        sut.Body.Should().Be("Template B\n\n---\n\nOriginalanforderung:\nOriginal");
    }

    /// <summary>Submit erstellt beim Provider und schließt den Dialog nur bei Erfolg.</summary>
    [Fact]
    public async Task Submit_ShouldCreateIssueAndClose_WhenProviderSucceeds()
    {
        var sut = CreateSut();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);
        bool? closed = null;
        sut.CloseRequested += (_, result) => closed = result;

        await ((AsyncRelayCommand)sut.ErstellenCommand).ExecuteAsync();

        _issueProvider.Request.Should().Be(new IssueCreateRequest("Aufgabe", "Original"));
        sut.CreatedIssue.Should().NotBeNull();
        closed.Should().BeTrue();
    }

    /// <summary>Submit legt kein Provider-Issue an, wenn live bereits eine Referenz existiert.</summary>
    [Fact]
    public async Task Submit_ShouldNotCreateIssue_WhenIssueWasAssignedInParallel()
    {
        var sut = CreateSut();
        sut.Initialize(
            _issueProvider,
            _templateProvider,
            "owner/repo",
            "Aufgabe",
            "Original",
            null,
            issueAlreadyAssignedLive: _ => Task.FromResult(true));

        await ((AsyncRelayCommand)sut.ErstellenCommand).ExecuteAsync();

        _issueProvider.Request.Should().BeNull();
        sut.ErrorMessage.Should().Contain("bereits ein Issue");
    }

    /// <summary>Submit ist bei fehlendem Titel deaktiviert und ruft den Provider nicht auf.</summary>
    [Fact]
    public async Task Submit_ShouldNotCreateIssue_WhenTitleIsEmpty()
    {
        var sut = CreateSut();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);
        sut.Title = " ";

        await ((AsyncRelayCommand)sut.ErstellenCommand).ExecuteAsync();

        sut.CanSubmit.Should().BeFalse();
        _issueProvider.Request.Should().BeNull();
    }

    /// <summary>KI-Ausfüllhilfe übernimmt das generierte Ergebnis in den editierbaren Body.</summary>
    [Fact]
    public async Task KiAusfuellen_ShouldReplaceBodyWithGeneratedText()
    {
        var kiPlugin = new FakeKiPlugin();
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "FakeKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        await ((AsyncRelayCommand)sut.KiAusfuellenCommand).ExecuteAsync();

        sut.Body.Should().Be("KI-Ergebnis: Template / Original");
    }

    /// <summary>KI-Aktion bleibt deaktiviert, wenn nur KI-Plugins ohne Textgenerator-Fähigkeit verfügbar sind.</summary>
    [Fact]
    public void Initialize_ShouldHideKiAction_WhenNoTextGeneratorIsAvailable()
    {
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([new FakePlainKiPlugin()]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);

        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "PlainKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        sut.VerfuegbareKiPlugins.Should().BeEmpty();
        sut.SelectedKiPluginPrefix.Should().BeNull();
        sut.CanUseAi.Should().BeFalse();
    }

    /// <summary>KI-Fehler bleiben im Dialog und verlieren den bisherigen Body nicht.</summary>
    [Fact]
    public async Task KiAusfuellen_ShouldKeepBodyAndShowError_WhenGeneratorFails()
    {
        var kiPlugin = new FakeFailingKiPlugin();
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([kiPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "FailKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");
        sut.Body = "Bisheriger Body";

        await ((AsyncRelayCommand)sut.KiAusfuellenCommand).ExecuteAsync();

        sut.Body.Should().Be("Bisheriger Body");
        sut.ErrorMessage.Should().Contain("KI-Ausfüllhilfe fehlgeschlagen");
    }

    private sealed class FakeIssueProvider : IIssueCreateProvider
    {
        public IssueCreateRequest? Request { get; private set; }

        public Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<IssueCreateResult> CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct = default)
        {
            Request = request;
            return Task.FromResult(IssueCreateResult.Success(new Issue(7, request.Title, request.Body, [], null, "https://example.test/issues/7")));
        }
    }

    private sealed class FakeTemplateProvider : IIssueTemplateProvider
    {
        public IssueTemplateLoadResult Result { get; set; } = IssueTemplateLoadResult.Success([]);

        public Task<IssueTemplateLoadResult> GetIssueTemplatesAsync(string repositoryId, CancellationToken ct = default)
            => Task.FromResult(Result);
    }

    private sealed class FakeKiPlugin : IKiPlugin, IIssueTemplateTextGenerator
    {
        public string PluginName => "Fake KI";
        public string PluginPrefix => "FakeKi";
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid aufgabeId) => "Fake KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
            => Task.FromResult($"KI-Ergebnis: {templateBody} / {originalRequirement}");
    }

    private sealed class FakeFailingKiPlugin : IKiPlugin, IIssueTemplateTextGenerator
    {
        public string PluginName => "Fail KI";
        public string PluginPrefix => "FailKi";
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid aufgabeId) => "Fail KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
            => throw new InvalidOperationException("Providerfehler");
    }

    private sealed class FakePlainKiPlugin : IKiPlugin
    {
        public string PluginName => "Plain KI";
        public string PluginPrefix => "PlainKi";
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid aufgabeId) => "Plain KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
