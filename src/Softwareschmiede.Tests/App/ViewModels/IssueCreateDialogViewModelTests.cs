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
        sut.UpdateTaskDescriptionAfterCreate.Should().BeFalse();
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>Die Option zur Aufgabenbeschreibung ist setzbar und meldet PropertyChanged.</summary>
    [Fact]
    public void UpdateTaskDescriptionAfterCreate_ShouldBeSettableAndRaisePropertyChanged()
    {
        var sut = CreateSut();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Beschreibung", null);

        using var monitor = sut.Monitor();
        sut.UpdateTaskDescriptionAfterCreate = true;

        sut.UpdateTaskDescriptionAfterCreate.Should().BeTrue();
        monitor.Should().RaisePropertyChangeFor(x => x.UpdateTaskDescriptionAfterCreate);
    }

    /// <summary>Initialize setzt die Option pro Dialoglauf auf den konservativen Standard zurück.</summary>
    [Fact]
    public void Initialize_ShouldResetUpdateTaskDescriptionAfterCreate()
    {
        var sut = CreateSut();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Beschreibung", null);
        sut.UpdateTaskDescriptionAfterCreate = true;

        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Andere Aufgabe", "Andere Beschreibung", null);

        sut.UpdateTaskDescriptionAfterCreate.Should().BeFalse();
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
        sut.UpdateTaskDescriptionAfterCreate = true;
        bool? closed = null;
        sut.CloseRequested += (_, result) => closed = result;

        await ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();

        _issueProvider.Request.Should().Be(new IssueCreateRequest("Aufgabe", "Original"));
        sut.UpdateTaskDescriptionAfterCreate.Should().BeTrue();
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

        await ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();

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

        await ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();

        sut.CanSubmit.Should().BeFalse();
        _issueProvider.Request.Should().BeNull();
    }

    /// <summary>Provider-Fehler bleiben im Dialog, behalten die Eingaben und schliessen nicht erfolgreich.</summary>
    [Fact]
    public async Task Submit_ShouldShowErrorAndKeepInputs_WhenProviderReturnsFailure()
    {
        var sut = CreateSut();
        _issueProvider.Result = IssueCreateResult.Failed("Berechtigung fehlt");
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);
        bool? closed = null;
        sut.CloseRequested += (_, result) => closed = result;

        await ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();

        _issueProvider.Request.Should().Be(new IssueCreateRequest("Aufgabe", "Original"));
        sut.CreatedIssue.Should().BeNull();
        sut.ErrorMessage.Should().Be("Berechtigung fehlt");
        sut.Title.Should().Be("Aufgabe");
        sut.Body.Should().Be("Original");
        closed.Should().BeNull();
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>Unerwartete Provider-Ausnahmen werden als Dialogfehler angezeigt und erzeugen kein Issue.</summary>
    [Fact]
    public async Task Submit_ShouldShowErrorAndKeepDialogOpen_WhenProviderThrows()
    {
        var sut = CreateSut();
        _issueProvider.ExceptionToThrow = new InvalidOperationException("Netzwerkfehler");
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);
        bool? closed = null;
        sut.CloseRequested += (_, result) => closed = result;

        await ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();

        sut.CreatedIssue.Should().BeNull();
        sut.ErrorMessage.Should().Contain("Netzwerkfehler");
        closed.Should().BeNull();
        sut.IsSubmitting.Should().BeFalse();
    }

    /// <summary>Cancellation waehrend Submit wird nicht als Providerfehler angezeigt und hinterlaesst kein Ergebnis.</summary>
    [Fact]
    public async Task Submit_ShouldResetSubmittingWithoutCreatedIssue_WhenProviderIsCancelled()
    {
        var sut = CreateSut();
        _issueProvider.ExceptionToThrow = new OperationCanceledException();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        await ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();

        sut.CreatedIssue.Should().BeNull();
        sut.ErrorMessage.Should().BeNull();
        sut.IsSubmitting.Should().BeFalse();
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>Doppelte Submit-Ausfuehrungen waehrend eines laufenden Provider-Aufrufs erzeugen nur ein Issue.</summary>
    [Fact]
    public async Task Submit_ShouldIgnoreSecondExecution_WhileProviderCallIsRunning()
    {
        var sut = CreateSut();
        _issueProvider.Release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        var first = ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();
        await _issueProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        sut.CanSubmit.Should().BeFalse();
        sut.CreateCommand.CanExecute(null).Should().BeFalse();

        var second = ((AsyncRelayCommand)sut.CreateCommand).ExecuteAsync();
        _issueProvider.Release.SetResult(null);
        await Task.WhenAll(first, second);

        _issueProvider.CallCount.Should().Be(1);
        sut.CreatedIssue.Should().NotBeNull();
    }

    /// <summary>Cancellation beim Template-Laden setzt den Ladezustand zurueck und blockiert Submit nicht.</summary>
    [Fact]
    public async Task LoadTemplatesAsync_ShouldResetLoading_WhenCancelled()
    {
        var sut = CreateSut();
        _templateProvider.ExceptionToThrow = new OperationCanceledException();
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", null);

        await ((AsyncRelayCommand)sut.LoadTemplatesCommand).ExecuteAsync();

        sut.IsLoadingTemplates.Should().BeFalse();
        sut.Templates.Should().BeEmpty();
        sut.ErrorMessage.Should().BeNull();
        sut.CanSubmit.Should().BeTrue();
    }

    /// <summary>KI-Ausfüllhilfe übernimmt das generierte Ergebnis in den editierbaren Body.</summary>
    [Fact]
    public async Task KiAusfuellen_ShouldReplaceBodyWithGeneratedText()
    {
        var aiPlugin = new FakeKiPlugin();
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "FakeKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        await ((AsyncRelayCommand)sut.AiAusfuellenCommand).ExecuteAsync();

        aiPlugin.ReceivedTemplateBody.Should().Be("Template");
        aiPlugin.ReceivedOriginalRequirement.Should().Be("Original");
        sut.Body.Should().Be("KI-Ergebnis: Template / Original");
    }

    /// <summary>KI-Ausfüllhilfe übernimmt deutsche Umlaute und Sonderzeichen unverändert in den editierbaren Body.</summary>
    [Fact]
    public async Task KiAusfuellen_ShouldKeepGermanSpecialCharactersInBody()
    {
        var generatedBody = "KI-Ergebnis: plötzlich für Selbstwertgefühl, Größe, Straße, äöüß & €.";
        var aiPlugin = new FakeKiPlugin(generatedBody);
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "FakeKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        await ((AsyncRelayCommand)sut.AiAusfuellenCommand).ExecuteAsync();

        sut.Body.Should().Be(generatedBody);
        sut.Body.Should().NotContain("plÃ");
        sut.Body.Should().NotContain("fÃ");
        sut.Body.Should().NotContain("SelbstwertgefÃ");
    }

    /// <summary>Alle verfügbaren KI-Plugins mit Textgenerator-Fähigkeit werden in der Auswahl angezeigt.</summary>
    [Fact]
    public void Initialize_ShouldListAllAvailableTextGenerators_InKiPluginSelection()
    {
        var codexPlugin = new FakeKiPlugin { PluginPrefixOverride = "Softwareschmiede.Codex" };
        var claudePlugin = new FakeKiPlugin { PluginPrefixOverride = "Softwareschmiede.ClaudeCli" };
        var devinPlugin = new FakeKiPlugin { PluginPrefixOverride = "Softwareschmiede.Devin" };
        var copilotPlugin = new FakeKiPlugin { PluginPrefixOverride = "Softwareschmiede.GitHubCopilot" };
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins())
            .Returns([codexPlugin, claudePlugin, devinPlugin, copilotPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);

        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "Softwareschmiede.Devin");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        sut.AvailableAiPlugins.Should().BeEquivalentTo([
            "Softwareschmiede.Codex",
            "Softwareschmiede.ClaudeCli",
            "Softwareschmiede.Devin",
            "Softwareschmiede.GitHubCopilot"
        ], options => options.WithStrictOrdering());
        sut.SelectedAiPluginPrefix.Should().Be("Softwareschmiede.Devin");
        sut.CanUseAi.Should().BeTrue();
    }

    /// <summary>KI-Aktion bleibt deaktiviert, wenn nur KI-Plugins ohne Textgenerator-Fähigkeit verfügbar sind.</summary>
    [Fact]
    public void Initialize_ShouldHideKiAction_WhenNoTextGeneratorIsAvailable()
    {
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([new FakePlainKiPlugin()]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);

        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "PlainKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");

        sut.AvailableAiPlugins.Should().BeEmpty();
        sut.SelectedAiPluginPrefix.Should().BeNull();
        sut.CanUseAi.Should().BeFalse();
    }

    /// <summary>FiltereAktivePluginsAsync entfernt deaktivierte Plugins aus VerfuegbareKiPlugins.</summary>
    [Fact]
    public async Task FiltereAktivePluginsAsync_ShouldRemoveDeactivatedPlugins()
    {
        var aktivesPlugin = new FakeKiPlugin();
        var deaktiviertesPlugin = new FakeFailingKiPlugin();
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([aktivesPlugin, deaktiviertesPlugin]);
        await using var db = Softwareschmiede.Tests.Helpers.TestDbContextFactory.Create();
        var settingService = new Softwareschmiede.Application.Services.AppSettingService(
            db, NullLogger<Softwareschmiede.Application.Services.AppSettingService>.Instance);
        var activationService = new Softwareschmiede.Application.Services.PluginActivationService(
            settingService, _pluginManagerMock.Object, NullLogger<Softwareschmiede.Application.Services.PluginActivationService>.Instance);
        await activationService.SetPluginEnabledAsync("FailKi", false, TestContext.Current.CancellationToken);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance, activationService);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "FakeKi");

        await sut.FilterActivePluginsAsync(TestContext.Current.CancellationToken);

        sut.AvailableAiPlugins.Should().ContainSingle();
        sut.AvailableAiPlugins.Should().Contain("FakeKi");
        sut.SelectedAiPluginPrefix.Should().Be("FakeKi");
    }

    /// <summary>KI-Fehler bleiben im Dialog und verlieren den bisherigen Body nicht.</summary>
    [Fact]
    public async Task KiAusfuellen_ShouldKeepBodyAndShowError_WhenGeneratorFails()
    {
        var aiPlugin = new FakeFailingKiPlugin();
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "FailKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");
        sut.Body = "Bisheriger Body";

        await ((AsyncRelayCommand)sut.AiAusfuellenCommand).ExecuteAsync();

        sut.Body.Should().Be("Bisheriger Body");
        sut.ErrorMessage.Should().Contain("KI-Ausfüllhilfe fehlgeschlagen");
    }

    /// <summary>Cancellation waehrend der KI-Ausfuellhilfe erhaelt den bisherigen Body und setzt den Laufzustand zurueck.</summary>
    [Fact]
    public async Task KiAusfuellen_ShouldKeepBodyAndResetGenerating_WhenCancelled()
    {
        var aiPlugin = new FakeCancellingKiPlugin();
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        var sut = new IssueCreateDialogViewModel(_pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        sut.Initialize(_issueProvider, _templateProvider, "owner/repo", "Aufgabe", "Original", "CancelKi");
        sut.SelectedTemplate = new IssueTemplate("Bug", "Template");
        sut.Body = "Bisheriger Body";

        await ((AsyncRelayCommand)sut.AiAusfuellenCommand).ExecuteAsync();

        sut.Body.Should().Be("Bisheriger Body");
        sut.ErrorMessage.Should().BeNull();
        sut.IsGenerating.Should().BeFalse();
        sut.CanUseAi.Should().BeTrue();
    }

    private sealed class FakeIssueProvider : IIssueCreateProvider
    {
        public IssueCreateRequest? Request { get; private set; }
        public int CallCount { get; private set; }
        public IssueCreateResult? Result { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public TaskCompletionSource<object?> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?>? Release { get; set; }

        public Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult(true);

        public async Task<IssueCreateResult> CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct = default)
        {
            CallCount++;
            Started.TrySetResult(null);
            if (Release is not null)
            {
                await Release.Task.WaitAsync(ct);
            }

            Request = request;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Result ?? IssueCreateResult.Success(new Issue(7, request.Title, request.Body, [], null, "https://example.test/issues/7"));
        }
    }

    private sealed class FakeTemplateProvider : IIssueTemplateProvider
    {
        public IssueTemplateLoadResult Result { get; set; } = IssueTemplateLoadResult.Success([]);
        public Exception? ExceptionToThrow { get; set; }

        public Task<IssueTemplateLoadResult> GetIssueTemplatesAsync(string repositoryId, CancellationToken ct = default)
            => ExceptionToThrow is not null
                ? Task.FromException<IssueTemplateLoadResult>(ExceptionToThrow)
                : Task.FromResult(Result);
    }

    private sealed class FakeKiPlugin(string? generatedBody = null) : IAiPlugin, IIssueTemplateTextGenerator
    {
        public string? ReceivedTemplateBody { get; private set; }
        public string? ReceivedOriginalRequirement { get; private set; }
        public string PluginName => "Fake KI";
        public string PluginPrefixOverride { get; set; } = "FakeKi";
        public string PluginPrefix => PluginPrefixOverride;
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid workItemId) => "Fake KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Softwareschmiede.Domain.Enums.TerminalProviderCapabilities TerminalCapabilities => Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty;
        public Task<Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec> GetTerminalStartSpecAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
            => Task.FromResult(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec { FileName = "cmd.exe", WorkingDirectory = localRepoPath, PluginName = PluginName });
        public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
        {
            ReceivedTemplateBody = templateBody;
            ReceivedOriginalRequirement = originalRequirement;
            return Task.FromResult(generatedBody ?? $"KI-Ergebnis: {templateBody} / {originalRequirement}");
        }
    }

    private sealed class FakeFailingKiPlugin : IAiPlugin, IIssueTemplateTextGenerator
    {
        public string PluginName => "Fail KI";
        public string PluginPrefix => "FailKi";
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid workItemId) => "Fail KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Softwareschmiede.Domain.Enums.TerminalProviderCapabilities TerminalCapabilities => Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty;
        public Task<Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec> GetTerminalStartSpecAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
            => Task.FromResult(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec { FileName = "cmd.exe", WorkingDirectory = localRepoPath, PluginName = PluginName });
        public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
            => throw new InvalidOperationException("Providerfehler");
    }

    private sealed class FakeCancellingKiPlugin : IAiPlugin, IIssueTemplateTextGenerator
    {
        public string PluginName => "Cancel KI";
        public string PluginPrefix => "CancelKi";
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid workItemId) => "Cancel KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Softwareschmiede.Domain.Enums.TerminalProviderCapabilities TerminalCapabilities => Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty;
        public Task<Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec> GetTerminalStartSpecAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
            => Task.FromResult(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec { FileName = "cmd.exe", WorkingDirectory = localRepoPath, PluginName = PluginName });
        public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
            => throw new OperationCanceledException(ct);
    }

    private sealed class FakePlainKiPlugin : IAiPlugin
    {
        public string PluginName => "Plain KI";
        public string PluginPrefix => "PlainKi";
        public PluginType PluginType => PluginType.DevelopmentAutomation;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default) => Task.FromResult(new ProcessStartInfo());
        public string GetProcessWindowTitle(Guid workItemId) => "Plain KI";
        public bool SupportsSessionContinuation() => false;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Softwareschmiede.Domain.Enums.TerminalProviderCapabilities TerminalCapabilities => Softwareschmiede.Domain.Enums.TerminalProviderCapabilities.SupportsPty;
        public Task<Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec> GetTerminalStartSpecAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
            => Task.FromResult(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec { FileName = "cmd.exe", WorkingDirectory = localRepoPath, PluginName = PluginName });
    }
}
