using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für den Dialog zur Issue-Anlage.</summary>
public sealed class IssueCreateDialogViewModel : ViewModelBase
{
    private readonly IPluginManager _pluginManager;
    private readonly PluginActivationService? _pluginActivationService;
    private readonly ILogger<IssueCreateDialogViewModel> _logger;
    private IIssueCreateProvider? _issueProvider;
    private IIssueTemplateProvider? _templateProvider;
    private Func<bool>? _issueAlreadyAssigned;
    private Func<CancellationToken, Task<bool>>? _issueAlreadyAssignedLive;
    private string _repositoryId = string.Empty;
    private string? _originalRequirement;
    private IssueTemplate? _selectedTemplate;
    private string? _title;
    private string? _body;
    private string? _selectedKiPluginPrefix;
    private string? _errorMessage;
    private bool _isLoadingTemplates;
    private bool _isSubmitting;
    private bool _isGenerating;

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = erfolgreich, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Geladene Provider-Templates.</summary>
    public ObservableCollection<IssueTemplate> Templates { get; } = new();

    /// <summary>Verfügbare KI-Provider mit Textgenerierungsfähigkeit.</summary>
    public ObservableCollection<string> VerfuegbareKiPlugins { get; } = new();

    /// <summary>Das erfolgreich beim Provider angelegte Issue.</summary>
    public Issue? CreatedIssue { get; private set; }

    /// <summary>Ausgewähltes Template.</summary>
    public IssueTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (!SetProperty(ref _selectedTemplate, value))
            {
                return;
            }

            if (value is not null)
            {
                Body = ComposeTemplateBody(value.Body, _originalRequirement);
            }

            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Issue-Titel.</summary>
    public string? Title
    {
        get => _title;
        set
        {
            SetProperty(ref _title, value);
            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Issue-Beschreibung.</summary>
    public string? Body
    {
        get => _body;
        set
        {
            SetProperty(ref _body, value);
            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Ausgewähltes KI-Plugin.</summary>
    public string? SelectedKiPluginPrefix
    {
        get => _selectedKiPluginPrefix;
        set
        {
            SetProperty(ref _selectedKiPluginPrefix, value);
            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Fehlermeldung im Dialog.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Gibt an, ob Templates geladen werden.</summary>
    public bool IsLoadingTemplates
    {
        get => _isLoadingTemplates;
        private set
        {
            SetProperty(ref _isLoadingTemplates, value);
            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Gibt an, ob ein Issue erstellt wird.</summary>
    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set
        {
            SetProperty(ref _isSubmitting, value);
            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Gibt an, ob die KI-Ausfüllhilfe läuft.</summary>
    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            SetProperty(ref _isGenerating, value);
            AktualisiereAbhaengigeZustaende();
        }
    }

    /// <summary>Gibt an, ob Templates auswählbar sind.</summary>
    public bool HasTemplates => Templates.Count > 0;

    /// <summary>Gibt an, ob das Issue abgesendet werden kann.</summary>
    public bool CanSubmit => _issueProvider is not null
        && !IsLoadingTemplates
        && !IsSubmitting
        && !IsGenerating
        && !(_issueAlreadyAssigned?.Invoke() ?? false)
        && !string.IsNullOrWhiteSpace(Title);

    /// <summary>Gibt an, ob die KI-Ausfüllhilfe genutzt werden kann.</summary>
    public bool CanUseAi => SelectedTemplate is not null
        && !string.IsNullOrWhiteSpace(SelectedKiPluginPrefix)
        && !IsLoadingTemplates
        && !IsSubmitting
        && !IsGenerating
        && FindSelectedTextGenerator() is not null;

    /// <summary>Lädt Templates.</summary>
    public ICommand LoadTemplatesCommand { get; }

    /// <summary>Füllt das Template per KI aus.</summary>
    public ICommand KiAusfuellenCommand { get; }

    /// <summary>Legt das Issue beim Provider an.</summary>
    public ICommand ErstellenCommand { get; }

    /// <summary>Bricht den Dialog ab.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <inheritdoc cref="IssueCreateDialogViewModel"/>
    public IssueCreateDialogViewModel(
        IPluginManager pluginManager,
        ILogger<IssueCreateDialogViewModel> logger,
        PluginActivationService? pluginActivationService = null)
    {
        _pluginManager = pluginManager;
        _logger = logger;
        _pluginActivationService = pluginActivationService;

        LoadTemplatesCommand = new AsyncRelayCommand(LoadTemplatesAsync);
        KiAusfuellenCommand = new AsyncRelayCommand(KiAusfuellenAsync, () => CanUseAi);
        ErstellenCommand = new AsyncRelayCommand(ErstellenAsync, () => CanSubmit);
        AbbrechenCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false), () => !IsSubmitting && !IsGenerating);
    }

    /// <summary>Initialisiert den Dialog mit Provider- und Aufgabendaten.</summary>
    public void Initialize(
        IIssueCreateProvider issueProvider,
        IIssueTemplateProvider? templateProvider,
        string repositoryId,
        string? initialTitle,
        string? originalRequirement,
        string? preferredKiPluginPrefix,
        Func<bool>? issueAlreadyAssigned = null,
        Func<CancellationToken, Task<bool>>? issueAlreadyAssignedLive = null)
    {
        _issueProvider = issueProvider;
        _templateProvider = templateProvider;
        _repositoryId = repositoryId;
        _originalRequirement = string.IsNullOrWhiteSpace(originalRequirement) ? null : originalRequirement;
        _issueAlreadyAssigned = issueAlreadyAssigned;
        _issueAlreadyAssignedLive = issueAlreadyAssignedLive;

        CreatedIssue = null;
        ErrorMessage = null;
        Templates.Clear();
        Title = initialTitle;
        Body = _originalRequirement ?? string.Empty;
        SelectedTemplate = null;

        VerfuegbareKiPlugins.Clear();
        foreach (var prefix in _pluginManager.GetDevelopmentAutomationPlugins()
                     .Where(p => p is IIssueTemplateTextGenerator)
                     .Select(p => p.PluginPrefix)
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            VerfuegbareKiPlugins.Add(prefix);
        }

        SelectedKiPluginPrefix = VerfuegbareKiPlugins.FirstOrDefault(p =>
                                     string.Equals(p, preferredKiPluginPrefix, StringComparison.OrdinalIgnoreCase))
                                 ?? VerfuegbareKiPlugins.FirstOrDefault();
        AktualisiereAbhaengigeZustaende();
    }

    /// <summary>Initialisiert den Dialog wie <see cref="Initialize"/> und filtert <see cref="VerfuegbareKiPlugins"/>
    /// anschließend in einem Schritt auf aktive Plugins, sodass beide Schritte nicht mehr einzeln in der
    /// richtigen Reihenfolge aufgerufen werden müssen.</summary>
    /// <param name="issueProvider">Der Provider zum Anlegen des Issues.</param>
    /// <param name="templateProvider">Optionaler Provider für Vorlagen.</param>
    /// <param name="repositoryId">Die Kennung des Repositories.</param>
    /// <param name="initialTitle">Der initial vorbelegte Titel.</param>
    /// <param name="originalRequirement">Die ursprüngliche Anforderungsbeschreibung.</param>
    /// <param name="preferredKiPluginPrefix">Der bevorzugte KI-Plugin-Prefix.</param>
    /// <param name="issueAlreadyAssigned">Prüfung, ob bereits ein Issue zugeordnet ist.</param>
    /// <param name="issueAlreadyAssignedLive">Asynchrone Live-Prüfung, ob bereits ein Issue zugeordnet ist.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task InitializeAsync(
        IIssueCreateProvider issueProvider,
        IIssueTemplateProvider? templateProvider,
        string repositoryId,
        string? initialTitle,
        string? originalRequirement,
        string? preferredKiPluginPrefix,
        Func<bool>? issueAlreadyAssigned = null,
        Func<CancellationToken, Task<bool>>? issueAlreadyAssignedLive = null,
        CancellationToken ct = default)
    {
        Initialize(
            issueProvider,
            templateProvider,
            repositoryId,
            initialTitle,
            originalRequirement,
            preferredKiPluginPrefix,
            issueAlreadyAssigned,
            issueAlreadyAssignedLive);
        await FiltereAktivePluginsAsync(ct);
    }

    /// <summary>Filtert <see cref="VerfuegbareKiPlugins"/> zusätzlich auf aktive Plugins. Ohne konfigurierten <see cref="PluginActivationService"/> bleibt die Liste unverändert.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task FiltereAktivePluginsAsync(CancellationToken ct = default)
    {
        if (_pluginActivationService is null)
        {
            return;
        }

        var aktivePrefixe = (await _pluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync(ct))
            .Select(p => p.PluginPrefix)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deaktiviertePrefixe = VerfuegbareKiPlugins.Where(p => !aktivePrefixe.Contains(p)).ToList();
        foreach (var prefix in deaktiviertePrefixe)
        {
            VerfuegbareKiPlugins.Remove(prefix);
        }

        if (SelectedKiPluginPrefix is not null && !aktivePrefixe.Contains(SelectedKiPluginPrefix))
        {
            SelectedKiPluginPrefix = VerfuegbareKiPlugins.FirstOrDefault();
        }
    }

    /// <summary>Setzt Template-Inhalt und Originalanforderung deterministisch zusammen.</summary>
    public static string ComposeTemplateBody(string templateBody, string? originalRequirement)
    {
        var result = $"{templateBody}\n\n---\n\nOriginalanforderung:";
        if (!string.IsNullOrWhiteSpace(originalRequirement))
        {
            result += $"\n{originalRequirement}";
        }

        return result;
    }

    private async Task LoadTemplatesAsync(CancellationToken ct)
    {
        if (_templateProvider is null)
        {
            return;
        }

        IsLoadingTemplates = true;
        ErrorMessage = null;
        Templates.Clear();
        try
        {
            var result = await _templateProvider.GetIssueTemplatesAsync(_repositoryId, ct);
            if (result.Status == IssueTemplateLoadResultStatus.Failed)
            {
                ErrorMessage = result.ErrorMessage ?? "Issue-Templates konnten nicht geladen werden.";
                return;
            }

            foreach (var template in result.Templates)
            {
                Templates.Add(template);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue-Templates konnten nicht geladen werden.");
            ErrorMessage = "Issue-Templates konnten nicht geladen werden. Die Anlage ohne Template ist möglich.";
        }
        finally
        {
            IsLoadingTemplates = false;
            OnPropertyChanged(nameof(HasTemplates));
        }
    }

    private async Task KiAusfuellenAsync(CancellationToken ct)
    {
        var generator = FindSelectedTextGenerator();
        if (generator is null || SelectedTemplate is null)
        {
            return;
        }

        IsGenerating = true;
        ErrorMessage = null;
        try
        {
            Body = await generator.FillIssueTemplateAsync(SelectedTemplate.Body, _originalRequirement, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KI-Ausfüllhilfe für Issue-Template fehlgeschlagen.");
            ErrorMessage = $"KI-Ausfüllhilfe fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task ErstellenAsync(CancellationToken ct)
    {
        if (_issueProvider is null || !CanSubmit)
        {
            return;
        }

        IsSubmitting = true;
        ErrorMessage = null;
        try
        {
            if (_issueAlreadyAssignedLive is not null
                ? await _issueAlreadyAssignedLive(ct)
                : _issueAlreadyAssigned?.Invoke() == true)
            {
                ErrorMessage = "Der Aufgabe ist bereits ein Issue zugeordnet.";
                return;
            }

            var result = await _issueProvider.CreateIssueAsync(
                _repositoryId,
                new IssueCreateRequest(Title!.Trim(), Body ?? string.Empty),
                ct);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Issue konnte nicht erstellt werden.";
                return;
            }

            CreatedIssue = result.Issue;
            CloseRequested?.Invoke(this, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Issue konnte nicht erstellt werden.");
            ErrorMessage = $"Issue konnte nicht erstellt werden: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private IIssueTemplateTextGenerator? FindSelectedTextGenerator()
    {
        if (string.IsNullOrWhiteSpace(SelectedKiPluginPrefix))
        {
            return null;
        }

        return _pluginManager.GetDevelopmentAutomationPlugins()
            .FirstOrDefault(p => string.Equals(p.PluginPrefix, SelectedKiPluginPrefix, StringComparison.OrdinalIgnoreCase))
            as IIssueTemplateTextGenerator;
    }

    private void AktualisiereAbhaengigeZustaende()
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanUseAi));
        RelayCommand.Refresh();
    }
}
