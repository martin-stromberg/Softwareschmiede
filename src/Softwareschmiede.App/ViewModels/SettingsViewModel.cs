using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using System.Windows.Input;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Einstellungsseite.</summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private const string CodexPluginPrefix = "Softwareschmiede.Codex";
    private const string GitHubPluginPrefix = "Softwareschmiede.GitHub";
    private const string GitHubAgentTokenFieldKey = "AgentToken";
    private const string CommandLineParameterKey = "CommandLineParameters";
    private const string ParallelLimitGroupName = "Ausführungsbegrenzung";

    /// <summary>Host-seitig deklariertes Limit-Feld für KI-Plugins; persistiert über
    /// <see cref="ScmEventExecutionLimitService.PluginLimitKey(string)"/> in <see cref="AppSettingService"/>,
    /// nicht über den pluginseitigen Credential Store.</summary>
    private static readonly PluginSettingField HostParallelLimitField = new(
        "ParallelLimit",
        "Maximale parallele Ausführungen",
        PluginSettingFieldType.Integer,
        Placeholder: "unbegrenzt",
        Description: "Ganzzahl ≥ 1; leer = unbegrenzt. Begrenzt die laufenden CLI-Ausführungen dieses KI-Plugins zusätzlich zum globalen Limit in „Aufgaben ausführen“.");

    private readonly AppSettingService _settingService;
    private readonly WorkingDirectorySettingsService _workingDirectoryService;
    private readonly DarkModeService _darkModeService;
    private readonly IPluginManager _pluginManager;
    private readonly PluginActivationService _pluginActivationService;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptTemplateService _promptTemplatesService;
    private readonly ProjectService _projectService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly RemoteStatusConnectionInfo _remoteStatusInfo;
    private readonly ICredentialStore _credentialStore;
    private readonly List<Guid> _deletedPromptTemplatesIds = [];

    private string? _workingDirectory;
    private string _designMode;
    private string? _defaultAiPlugin;
    private IGitPlugin? _defaultScmPlugin;
    private NotificationMode _notificationMode;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;
    private PluginActivationEntry? _selectedPlugin;
    private IReadOnlyList<PluginSettingGroupEntry> _selectedPluginSettings = [];
    private bool _isScmAiPluginContentVisible;
    private bool _isIdePluginContentVisible;
    private List<string> _idePluginOrder = [];
    private PluginActivationEntry? _selectedIdePlugin;
    private IReadOnlyList<PluginSettingGroupEntry>? _selectedIdePluginSettings;
    private bool _isScmMonitoringEnabled = true;
    private bool _isScmTaskCompletionEnabled = true;
    private string _scmTaskCompletionWaitMinutes = AppSettingService.ScmTaskCompletionDefaultWaitMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private string _scmTriggerKeyword = ScmEventValidationService.DefaultTriggerKeyword;
    private string _scmDelayKeyword = ScmDelayDirective.DefaultDelayKeyword;
    private string _maxParallelExecutions = string.Empty;
    private bool _isIssuePreparationEnabled;
    private bool _isIssuePreparationCommentEnabled = true;
    private IssuePreparationAiPluginOption? _selectedIssuePreparationAiPlugin;
    private readonly Dictionary<string, string?> _aiPluginParallelLimit = new(StringComparer.Ordinal);
    private string _scmPromptAppendixGeneral = string.Empty;
    private string _scmPromptAppendixIssue = string.Empty;
    private string _scmPromptAppendixIssueComment = string.Empty;
    private string _scmPromptAppendixPullRequest = string.Empty;
    private string _scmPromptAppendixPullRequestComment = string.Empty;
    private UpdateModeOption? _selectedUpdateMode = UpdateModeOption.ForMode(UpdateMode.CheckOnly);
    private UpdatePathOption? _selectedUpdatePath = UpdatePathOption.ForPath(null);
    private ScmBackmergeModeOption? _selectedBackmergeMode = ScmBackmergeModeOption.ForMode(ScmBackmergeMode.Disabled);
    private ScmAccountModeOption? _selectedAccountMode = ScmAccountModeOption.ForMode(ScmAccountMode.UserAccount);
    private ScmTaskCompletionCheckModeOption? _selectedScmTaskCompletionCheckmode = ScmTaskCompletionCheckModeOption.ForMode(ScmTaskCompletionCheckMode.EventDriven);
    private bool _isScmBackmergeDraftActivation;
    private bool _isScmBackmergeAdminMerge;
    private bool _isRemoteStatusEnabled;
    private string _remoteStatusUrl = string.Empty;
    private string _remoteStatusInstanceName = string.Empty;
    private string _remoteStatusInstanceId = string.Empty;
    private string _remoteStatusToken = string.Empty;
    private string _remoteStatusConnectionText = "deaktiviert";

    /// <summary>Wird direkt nach erfolgreichem Speichern der Update--Einstellungen mit dem gespeicherten Snapshot ausgelöst.</summary>
    public event EventHandler<UpdateSettings>? UpdateSettingsSaved;

    /// <summary>Feste Auswahlliste der Update-Modi (Steuerwert + Anzeige-Label).</summary>
    public IReadOnlyList<UpdateModeOption> UpdateModeOptions => UpdateModeOption.All;

    /// <summary>Aktuell ausgewählte Update-Modus-Option aus <see cref="UpdateModeOptions"/>.</summary>
    public UpdateModeOption? SelectedUpdateMode
    {
        get => _selectedUpdateMode;
        set => SetProperty(ref _selectedUpdateMode, value);
    }

    /// <summary>Feste Auswahlliste der Update-Pfade (Steuerwert + Anzeige-Label).</summary>
    public IReadOnlyList<UpdatePathOption> UpdatePathOptions => UpdatePathOption.All;

    /// <summary>Aktuell ausgewählte Update-Pfad-Option aus <see cref="UpdatePathOptions"/>.</summary>
    public UpdatePathOption? SelectedUpdatePath
    {
        get => _selectedUpdatePath;
        set
        {
            if (SetProperty(ref _selectedUpdatePath, value))
                OnPropertyChanged(nameof(DerivedUpdateChannel));
        }
    }

    /// <summary>Schreibgeschützter Anzeigetext des aus der Pfad-Auswahl resultierenden exklusiven UpdateChannels.</summary>
    public string DerivedUpdateChannel
        => UpdatePathChannelResolver.ResolveChannel(SelectedUpdatePath?.Path) == UpdateChannel.ReleaseCandidate
            ? UpdatePathText.ChannelReleaseCandidate
            : UpdatePathText.ChannelStable;

    /// <summary>Feste Auswahlliste der Backmerge-Modi (Steuerwert + Anzeige-Label).</summary>
    public IReadOnlyList<ScmBackmergeModeOption> BackmergeModeOptions => ScmBackmergeModeOption.All;

    /// <summary>Aktuell ausgewählte Backmerge-Modus-Option aus <see cref="BackmergeModeOptions"/>.</summary>
    public ScmBackmergeModeOption? SelectedBackmergeMode
    {
        get => _selectedBackmergeMode;
        set
        {
            if (!SetProperty(ref _selectedBackmergeMode, value)) return;
            OnPropertyChanged(nameof(IsProcessRunsFieldEnabled));
            OnPropertyChanged(nameof(ProcessRunsStatusText));
        }
    }

    /// <summary>Feste Auswahlliste der SCM-Überwachungsmodi (Steuerwert + Anzeige-Label).</summary>
    public IReadOnlyList<ScmAccountModeOption> AccountModeOptions => ScmAccountModeOption.All;

    /// <summary>Aktuell ausgewählte Überwachungsmodus-Option aus <see cref="AccountModeOptions"/>.</summary>
    public ScmAccountModeOption? SelectedAccountMode
    {
        get => _selectedAccountMode;
        set
        {
            if (SetProperty(ref _selectedAccountMode, value))
                OnPropertyChanged(nameof(IsTriggerKeywordVisible));
        }
    }

    /// <summary>Feste Auswahlliste der Prüfmodi der SCM-Abschlussüberwachung (Steuerwert + Anzeige-Label).</summary>
    public IReadOnlyList<ScmTaskCompletionCheckModeOption> ScmTaskCompletionCheckmodeOptions => ScmTaskCompletionCheckModeOption.All;

    /// <summary>Aktuell ausgewählte Prüfmodus-Option aus <see cref="ScmTaskCompletionCheckmodeOptions"/>.</summary>
    public ScmTaskCompletionCheckModeOption? SelectedScmTaskCompletionCheckmode
    {
        get => _selectedScmTaskCompletionCheckmode;
        set => SetProperty(ref _selectedScmTaskCompletionCheckmode, value);
    }

    /// <summary>Gibt an, ob Draft-Backmerge-Pull-Requests vor dem Merge automatisch aktiviert werden.</summary>
    public bool IsScmBackmergeDraftActivation
    {
        get => _isScmBackmergeDraftActivation;
        set => SetProperty(ref _isScmBackmergeDraftActivation, value);
    }

    /// <summary>Gibt an, ob nach einem blockierten regulären Merge ein Admin-Merge-Fallback erlaubt ist.</summary>
    public bool IsScmBackmergeAdminMerge
    {
        get => _isScmBackmergeAdminMerge;
        set => SetProperty(ref _isScmBackmergeAdminMerge, value);
    }

    /// <summary>Arbeitsverzeichnis für Repository-Klone.</summary>
    public string? WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    /// <summary>Aktuell gewählter Design-Modus (z. B. "Hell", "Dunkel", "System").</summary>
    public string DesignMode
    {
        get => _designMode;
        set => SetProperty(ref _designMode, value);
    }

    /// <summary>Alle verfügbaren Design-Modi.</summary>
    /// <value>Die Liste der vom Dark-Mode-Service bereitgestellten Modi.</value>
    public IEnumerable<string> DesignModes => _darkModeService.GetAvailableModes();

    /// <summary>Standard-KI-Plugin-Prefix.</summary>
    public string? DefaultAiPlugin
    {
        get => _defaultAiPlugin;
        set => SetProperty(ref _defaultAiPlugin, value);
    }

    /// <summary>Alle verfügbaren SCM-Plugins.</summary>
    public IReadOnlyList<IGitPlugin> ScmPlugins { get; private set; } = [];

    /// <summary>Alle verfügbaren KI-Plugins.</summary>
    public IReadOnlyList<IAiPlugin> AiPlugins { get; private set; } = [];

    /// <summary>Aktuell gewähltes Standard-SCM-Plugin.</summary>
    public IGitPlugin? DefaultScmPlugin
    {
        get => _defaultScmPlugin;
        set => SetProperty(ref _defaultScmPlugin, value);
    }

    /// <summary>Benachrichtigungsmodus.</summary>
    public NotificationMode NotificationMode
    {
        get => _notificationMode;
        set => SetProperty(ref _notificationMode, value);
    }

    /// <summary>Gibt an, ob Daten geladen werden.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Fehlermeldung.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Erfolgsmeldung nach dem Speichern.</summary>
    public string? SuccessMessage
    {
        get => _successMessage;
        private set => SetProperty(ref _successMessage, value);
    }

    /// <summary>SCM-Plugins mit Aktivierungsstatus für das Plugins-Register.</summary>
    public ObservableCollection<PluginActivationEntry> SourceCodeManagementPlugins { get; } = [];

    /// <summary>KI-Plugins mit Aktivierungsstatus für das Plugins-Register.</summary>
    public ObservableCollection<PluginActivationEntry> DevelopmentAutomationPlugins { get; } = [];

    /// <summary>Im Plugins-Register ausgewählter Eintrag.</summary>
    public PluginActivationEntry? SelectedPlugin
    {
        get => _selectedPlugin;
        private set => SetProperty(ref _selectedPlugin, value);
    }

    /// <summary>Einstellungsgruppen des im Plugins-Register ausgewählten Plugins.</summary>
    public IReadOnlyList<PluginSettingGroupEntry> SelectedPluginSettings
    {
        get => _selectedPluginSettings;
        private set => SetProperty(ref _selectedPluginSettings, value);
    }

    /// <summary>Gibt an, ob im Inhaltsbereich die Einstellungen des ausgewählten SCM-/KI-Plugins angezeigt werden (exklusiv zu <see cref="IsIdePluginContentVisible"/>).</summary>
    public bool IsScmAiPluginContentVisible
    {
        get => _isScmAiPluginContentVisible;
        private set => SetProperty(ref _isScmAiPluginContentVisible, value);
    }

    /// <summary>Gibt an, ob im Inhaltsbereich die Einstellungen des ausgewählten IDE-Plugins angezeigt werden (exklusiv zu <see cref="IsScmAiPluginContentVisible"/>).</summary>
    public bool IsIdePluginContentVisible
    {
        get => _isIdePluginContentVisible;
        private set => SetProperty(ref _isIdePluginContentVisible, value);
    }

    /// <summary>Alle verfügbaren IDE-Plugins.</summary>
    public IReadOnlyList<IIdePlugin> IdePlugins { get; private set; } = [];

    /// <summary>IDE-Plugins mit Aktivierungsstatus für das Plugins-Register.</summary>
    public ObservableCollection<PluginActivationEntry> DevelopmentEnvironmentPlugins { get; } = [];

    /// <summary>Aktuell höchstpriorisiertes IDE-Plugin gemäß <see cref="IdePluginOrder"/>.</summary>
    public string? DefaultIdePlugin => IdePluginOrder.FirstOrDefault();

    /// <summary>Reihenfolge der IDE-Plugin-Prefixe (aus Setting <c>plugins.ide.order</c>).</summary>
    public List<string> IdePluginOrder
    {
        get => _idePluginOrder;
        private set => SetProperty(ref _idePluginOrder, value);
    }

    /// <summary>Im IDE-Plugins-Register ausgewählter Eintrag.</summary>
    public PluginActivationEntry? SelectedIdePlugin
    {
        get => _selectedIdePlugin;
        private set => SetProperty(ref _selectedIdePlugin, value);
    }

    /// <summary>Einstellungsgruppen des im IDE-Plugins-Register ausgewählten Plugins.</summary>
    public IReadOnlyList<PluginSettingGroupEntry>? SelectedIdePluginSettings
    {
        get => _selectedIdePluginSettings;
        private set => SetProperty(ref _selectedIdePluginSettings, value);
    }

    /// <summary>Gibt an, ob die SCM-Ereignisüberwachung aktiviert ist (Laufzeit-Schalter, ohne Neustart wirksam).</summary>
    public bool IsScmMonitoringEnabled
    {
        get => _isScmMonitoringEnabled;
        set
        {
            if (!SetProperty(ref _isScmMonitoringEnabled, value)) return;
            OnPropertyChanged(nameof(IsMonitoringModeEnabled));
            OnPropertyChanged(nameof(IsTriggerKeywordVisible));
            OnPropertyChanged(nameof(IsDelayKeywordVisible));
            OnPropertyChanged(nameof(IsPromptAppendVisible));
            OnPropertyChanged(nameof(WorkItemsExecuteStatusText));
        }
    }

    /// <summary>Gibt an, ob die Abschlussüberwachung von Aufgaben mit SCM-Referenz aktiviert ist (Laufzeit-Schalter, ohne Neustart wirksam).</summary>
    public bool IsScmTaskCompletionEnabled
    {
        get => _isScmTaskCompletionEnabled;
        set
        {
            if (!SetProperty(ref _isScmTaskCompletionEnabled, value)) return;
            OnPropertyChanged(nameof(IsTaskCompletionFieldEnabled));
            OnPropertyChanged(nameof(WorkItemsExitStatusText));
        }
    }

    /// <summary>Der Überwachungsmodus ist nur bei aktivierter Ereignisüberwachung bedienbar.</summary>
    public bool IsMonitoringModeEnabled => IsScmMonitoringEnabled;

    /// <summary>Das Trigger-Keyword wird nur bei aktivierter Ereignisüberwachung im Benutzerkonto-Modus benötigt.</summary>
    public bool IsTriggerKeywordVisible => IsScmMonitoringEnabled && SelectedAccountMode?.Mode == ScmAccountMode.UserAccount;

    /// <summary>Das Verzögerungs-Keyword ist in beiden Kontomodi wirksam, weil es aus dem
    /// jeweils akzeptierten Triggertext gelesen wird; sichtbar bei aktiver Überwachung.</summary>
    public bool IsDelayKeywordVisible => IsScmMonitoringEnabled;

    /// <summary>Die Prompt-Anhänge sind nur bei aktivierter Ereignisüberwachung relevant.</summary>
    public bool IsPromptAppendVisible => IsScmMonitoringEnabled;

    /// <summary>Prüfmodus und Wartezeit sind nur bei aktivierter Abschlussüberwachung bedienbar.</summary>
    public bool IsTaskCompletionFieldEnabled => IsScmTaskCompletionEnabled;

    /// <summary>Die Backmerge-Optionen sind nur bei aktiviertem Backmerge-Modus bedienbar.</summary>
    public bool IsProcessRunsFieldEnabled => SelectedBackmergeMode?.Mode != ScmBackmergeMode.Disabled;

    /// <summary>Statuszeile des „Aufgaben ausführen“-Bereichs im Ausklapp-Header.</summary>
    public string WorkItemsExecuteStatusText => IsScmMonitoringEnabled ? "aktiviert" : "deaktiviert";

    /// <summary>Statuszeile des „Aufgaben beenden“-Bereichs im Ausklapp-Header.</summary>
    public string WorkItemsExitStatusText => IsScmTaskCompletionEnabled ? "aktiviert" : "deaktiviert";

    /// <summary>Statuszeile des „Prozessabläufe“-Bereichs im Ausklapp-Header.</summary>
    public string ProcessRunsStatusText => SelectedBackmergeMode?.Mode != ScmBackmergeMode.Disabled ? "aktiviert" : "deaktiviert";

    /// <summary>Gibt an, ob die Remote-Übersicht (Status-Relay) aktiviert ist (Laufzeit-Schalter, ohne Neustart wirksam).</summary>
    public bool IsRemoteStatusEnabled
    {
        get => _isRemoteStatusEnabled;
        set
        {
            if (!SetProperty(ref _isRemoteStatusEnabled, value)) return;
            OnPropertyChanged(nameof(IsRemoteStatusFieldEnabled));
            OnPropertyChanged(nameof(RemoteStatusHeaderStatusText));
        }
    }

    /// <summary>Basis-URL des Status-Servers (z. B. <c>http://localhost:5055</c> oder <c>https://status.example.org</c>).</summary>
    public string RemoteStatusUrl
    {
        get => _remoteStatusUrl;
        set => SetProperty(ref _remoteStatusUrl, value);
    }

    /// <summary>Optionaler Anzeigename dieser Instanz auf der Status-Seite.</summary>
    public string RemoteStatusInstanceName
    {
        get => _remoteStatusInstanceName;
        set => SetProperty(ref _remoteStatusInstanceName, value);
    }

    /// <summary>Die systemseitig vergebene Instanz-ID (read-only Anzeige).</summary>
    public string RemoteStatusInstanceId
    {
        get => _remoteStatusInstanceId;
        private set => SetProperty(ref _remoteStatusInstanceId, value);
    }

    /// <summary>Das Relay-Zugangstoken; wird im CredentialStore persistiert und über die PasswordBox eingegeben.</summary>
    public string RemoteStatusToken
    {
        get => _remoteStatusToken;
        set
        {
            if (!SetProperty(ref _remoteStatusToken, value)) return;
            OnPropertyChanged(nameof(HasRemoteStatusToken));
        }
    }

    /// <summary>Ob ein Relay-Token bekannt ist (gespeichert oder frisch eingegeben);
    /// steuert den „bereits gespeichert"-Indikator neben der PasswordBox.</summary>
    public bool HasRemoteStatusToken => !string.IsNullOrEmpty(RemoteStatusToken);

    /// <summary>Verständliche Statuszeile der Relay-Verbindung (aus <see cref="RemoteStatusConnectionInfo"/>).</summary>
    public string RemoteStatusConnectionText
    {
        get => _remoteStatusConnectionText;
        private set => SetProperty(ref _remoteStatusConnectionText, value);
    }

    /// <summary><see langword="true"/>, wenn der Verbindungsstatus einen Fehler meldet
    /// (für die farbliche Hervorhebung der Statuszeile).</summary>
    public bool IsRemoteStatusConnectionError
        => _remoteStatusInfo.State is RemoteStatusConnectionState.Error
            or RemoteStatusConnectionState.ConfigurationError;

    /// <summary>URL-/Token-/Namens-Felder sind nur bei aktivierter Remote-Übersicht bedienbar
    /// (gesperrt statt ausgeblendet — Instanz-ID und Verbindungsstatus bleiben sichtbar).</summary>
    public bool IsRemoteStatusFieldEnabled => IsRemoteStatusEnabled;

    /// <summary>Statuszeile des „Remote-Übersicht“-Bereichs im Ausklapp-Header.</summary>
    public string RemoteStatusHeaderStatusText => IsRemoteStatusEnabled ? "aktiviert" : "deaktiviert";

    /// <summary>Wartezeit in Minuten zwischen dem erkannten Referenz-Schluss und dem automatischen Beenden der Aufgabe.</summary>
    public string ScmTaskCompletionWaitMinutes
    {
        get => _scmTaskCompletionWaitMinutes;
        set => SetProperty(ref _scmTaskCompletionWaitMinutes, value);
    }

    /// <summary>Das Trigger-Keyword, mit dem SCM-Ereignisse markiert werden (Standard: <c>$Softwareschmiede</c>).</summary>
    public string ScmTriggerKeyword
    {
        get => _scmTriggerKeyword;
        set => SetProperty(ref _scmTriggerKeyword, value);
    }

    /// <summary>Das Verzögerungs-Keyword, mit dem SCM-Ereignisse eine zeitversetzte Ausführung
    /// anfordern (Standard: <c>#verzögert</c>).</summary>
    public string ScmDelayKeyword
    {
        get => _scmDelayKeyword;
        set => SetProperty(ref _scmDelayKeyword, value);
    }

    /// <summary>Globales Limit paralleler CLI-Ausführungen für SCM-Ereignisse
    /// (leer = unbegrenzt; gültig: Ganzzahl ≥ 1).</summary>
    public string MaxParallelExecutions
    {
        get => _maxParallelExecutions;
        set => SetProperty(ref _maxParallelExecutions, value);
    }

    /// <summary>Gibt an, ob die automatische Issue-Aufbereitung aktiviert ist (Laufzeit-Schalter).</summary>
    public bool IsIssuePreparationEnabled
    {
        get => _isIssuePreparationEnabled;
        set
        {
            if (!SetProperty(ref _isIssuePreparationEnabled, value)) return;
            OnPropertyChanged(nameof(IsIssuePreparationFieldEnabled));
            OnPropertyChanged(nameof(IssuePreparationStatusText));
        }
    }

    /// <summary>Gibt an, ob die Analyseergebnisse zusätzlich als Issue-Kommentar dokumentiert werden.</summary>
    public bool IsIssuePreparationCommentEnabled
    {
        get => _isIssuePreparationCommentEnabled;
        set => SetProperty(ref _isIssuePreparationCommentEnabled, value);
    }

    /// <summary>Ausgewähltes Funktions-Plugin der Issue-Aufbereitung
    /// (<see cref="IssuePreparationAiPluginOption.PluginPrefix"/> = <c>null</c> bedeutet:
    /// Fallback auf die globale Standard-CLI).</summary>
    public IssuePreparationAiPluginOption? SelectedIssuePreparationAiPlugin
    {
        get => _selectedIssuePreparationAiPlugin;
        set => SetProperty(ref _selectedIssuePreparationAiPlugin, value);
    }

    /// <summary>Verfügbare KI-Plugin-Auswahl für die Aufbereitung, beginnend mit dem
    /// Standard-Eintrag für den Fallback auf die globale Standard-CLI.</summary>
    public ObservableCollection<IssuePreparationAiPluginOption> IssuePreparationAiPluginOptions { get; } = [];

    /// <summary>Überwachte Repositories mit Aufbereitungs-Auswahl (persistiertes Flag
    /// <see cref="GitRepository.IssuePreparationEnabled"/>).</summary>
    public ObservableCollection<IssuePreparationRepositoryEntry> IssuePreparationRepositories { get; } = [];

    /// <summary>Die Detail-Felder der Aufbereitung sind nur bei aktivierter Funktion bedienbar.</summary>
    public bool IsIssuePreparationFieldEnabled => IsIssuePreparationEnabled;

    /// <summary>Statuszeile des „Issue-Aufbereitung“-Bereichs im Ausklapp-Header.</summary>
    public string IssuePreparationStatusText => IsIssuePreparationEnabled ? "aktiviert" : "deaktiviert";

    /// <summary>Prompt-Anhang, der bei jeder SCM-Ereignisart an den Prompt angehängt wird (leer = keiner).</summary>
    public string ScmPromptAppendixGeneral
    {
        get => _scmPromptAppendixGeneral;
        set => SetProperty(ref _scmPromptAppendixGeneral, value);
    }

    /// <summary>Prompt-Anhang für SCM-Ereignisse aus Issue-Beschreibungen (leer = keiner).</summary>
    public string ScmPromptAppendixIssue
    {
        get => _scmPromptAppendixIssue;
        set => SetProperty(ref _scmPromptAppendixIssue, value);
    }

    /// <summary>Prompt-Anhang für SCM-Ereignisse aus Kommentaren auf Issues (leer = keiner).</summary>
    public string ScmPromptAppendixIssueComment
    {
        get => _scmPromptAppendixIssueComment;
        set => SetProperty(ref _scmPromptAppendixIssueComment, value);
    }

    /// <summary>Prompt-Anhang für SCM-Ereignisse aus Pull-Request-Beschreibungen (leer = keiner).</summary>
    public string ScmPromptAppendixPullRequest
    {
        get => _scmPromptAppendixPullRequest;
        set => SetProperty(ref _scmPromptAppendixPullRequest, value);
    }

    /// <summary>Prompt-Anhang für SCM-Ereignisse aus Kommentaren und Reviews auf Pull Requests (leer = keiner).</summary>
    public string ScmPromptAppendixPullRequestComment
    {
        get => _scmPromptAppendixPullRequestComment;
        set => SetProperty(ref _scmPromptAppendixPullRequestComment, value);
    }

    /// <summary>Editierbare Promptvorlagen.</summary>
    public ObservableCollection<PromptTemplateEntry> PromptTemplates { get; } = [];

    /// <summary>Lädt alle Einstellungen.</summary>
    public ICommand LoadCommand { get; }

    /// <summary>Speichert alle Einstellungen.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Verwirft alle nicht gespeicherten Einstellungen.</summary>
    public ICommand DiscardCommand { get; }

    /// <summary>Wird ausgelöst wenn der Nutzer ein SCM-Plugin wählt. Lädt die Einstellungsgruppen des Plugins.</summary>
    public ICommand ScmPluginSelectedCommand { get; }

    /// <summary>Wird ausgelöst wenn der Nutzer ein KI-Plugin wählt. Lädt die Einstellungsgruppen des Plugins.</summary>
    public ICommand AiPluginSelectedCommand { get; }

    /// <summary>Wird ausgelöst wenn der Nutzer im Plugins-Register einen Listeneintrag wählt. Lädt dessen Einstellungsgruppen.</summary>
    public ICommand PluginSelectedCommand { get; }

    /// <summary>Wird ausgelöst wenn der Nutzer im IDE-Plugins-Register einen Listeneintrag wählt. Lädt dessen Einstellungsgruppen.</summary>
    public ICommand IdePluginSelectedCommand { get; }

    /// <summary>Verschiebt das ausgewählte IDE-Plugin in der Reihenfolge nach oben.</summary>
    public ICommand IdePluginMoveUpCommand { get; }

    /// <summary>Verschiebt das ausgewählte IDE-Plugin in der Reihenfolge nach unten.</summary>
    public ICommand IdePluginMoveDownCommand { get; }

    /// <summary>Fügt eine neue Promptvorlage hinzu.</summary>
    public ICommand AddPromptTemplateCommand { get; }

    /// <summary>Entfernt eine Promptvorlage aus der Liste.</summary>
    public ICommand DeletePromptTemplateCommand { get; }

    /// <summary>Öffnet das nicht-modale Konsolentestfenster (Diagnose-Werkzeug für das CLI-Ausgabe-Replay).</summary>
    public ICommand OpenConsoleTestCommand { get; }

    /// <inheritdoc cref="SettingsViewModel"/>
    public SettingsViewModel(
        AppSettingService settingService,
        WorkingDirectorySettingsService workingDirectoryService,
        DarkModeService darkModeService,
        IPluginManager pluginManager,
        PluginActivationService pluginActivationService,
        PluginSettingsService pluginSettingsService,
        PromptTemplateService promptTemplatesService,
        ProjectService projectService,
        ILogger<SettingsViewModel> logger,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        RemoteStatusConnectionInfo remoteStatusInfo,
        ICredentialStore credentialStore)
    {
        _settingService = settingService;
        _workingDirectoryService = workingDirectoryService;
        _darkModeService = darkModeService;
        _pluginManager = pluginManager;
        _pluginActivationService = pluginActivationService;
        _pluginSettingsService = pluginSettingsService;
        _promptTemplatesService = promptTemplatesService;
        _projectService = projectService;
        _logger = logger;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _remoteStatusInfo = remoteStatusInfo;
        _credentialStore = credentialStore;

        _designMode = darkModeService.Current;
        _darkModeService.ModeChanged += OnDarkModeChanged;
        _remoteStatusConnectionText = _remoteStatusInfo.BuildStatusText();
        _remoteStatusInfo.StateChanged += OnRemoteStatusStateChanged;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DiscardCommand = new AsyncRelayCommand(DiscardAsync);
        ScmPluginSelectedCommand = new RelayCommand<IGitPlugin>(SelectPluginByReference);
        AiPluginSelectedCommand = new RelayCommand<IAiPlugin>(SelectPluginByReference);
        PluginSelectedCommand = new RelayCommand<PluginActivationEntry>(entry =>
        {
            if (entry is not null)
                LoadSelectedPluginSettings(entry);
        });
        IdePluginSelectedCommand = new RelayCommand<PluginActivationEntry>(entry =>
        {
            if (entry is not null)
                LoadSelectedIdePluginSettings(entry);
        });
        IdePluginMoveUpCommand = new RelayCommand<PluginActivationEntry>(entry => MoveIdePlugin(entry, -1), entry => CanMoveIdePlugin(entry, -1));
        IdePluginMoveDownCommand = new RelayCommand<PluginActivationEntry>(entry => MoveIdePlugin(entry, 1), entry => CanMoveIdePlugin(entry, 1));
        AddPromptTemplateCommand = new RelayCommand(AddPromptTemplate);
        DeletePromptTemplateCommand = new RelayCommand<PromptTemplateEntry>(DeletePromptTemplate, entry => entry is not null);
        OpenConsoleTestCommand = new AsyncRelayCommand(OpenConsoleTestAsync);
    }

    private async Task OpenConsoleTestAsync(CancellationToken ct)
    {
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<ConsoleTestViewModel>();
            await _dialogService.ShowConsoleTestDialogAsync(viewModel, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Das Konsolentestfenster konnte nicht geöffnet werden.");
            ErrorMessage = $"Konsolentestfenster konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _workingDirectory = await _workingDirectoryService.GetWorkingDirectoryAsync(ct);
            OnPropertyChanged(nameof(WorkingDirectory));

            _defaultAiPlugin = await _settingService.GetSettingAsync(AppSettingService.DefaultAiPluginKey, ct);
            OnPropertyChanged(nameof(DefaultAiPlugin));

            var savedMode = await _settingService.GetSettingAsync(AppSettingService.DesignModeKey, ct);
            _designMode = savedMode ?? _darkModeService.Current;
            OnPropertyChanged(nameof(DesignMode));

            ScmPlugins = _pluginManager.GetSourceCodeManagementPlugins();
            OnPropertyChanged(nameof(ScmPlugins));

            AiPlugins = _pluginManager.GetDevelopmentAutomationPlugins();
            OnPropertyChanged(nameof(AiPlugins));

            IdePlugins = _pluginManager.GetIdePlugins();
            OnPropertyChanged(nameof(IdePlugins));

            await LoadPluginActivationAsync(ct);
            await LoadIdePluginActivationAsync(ct);

            var savedScmPluginName = await _settingService.GetSettingAsync(AppSettingService.DefaultScmPluginKey, ct);
            _defaultScmPlugin = ScmPlugins.FirstOrDefault(p => p.PluginName == savedScmPluginName)
                ?? ScmPlugins.FirstOrDefault();
            OnPropertyChanged(nameof(DefaultScmPlugin));

            await LoadPromptTemplatesAsync(ct);

            IsScmMonitoringEnabled = await _settingService.GetScmMonitoringEnabledAsync(ct);
            IsScmTaskCompletionEnabled = await _settingService.GetScmTaskCompletionEnabledAsync(ct);

            // Remote-Übersicht: Schalter, Server-URL, Anzeigename, Instanz-ID und Token;
            // die Instanz-ID wird bei aktivierter Übersicht systemseitig erzeugt.
            IsRemoteStatusEnabled = await _settingService.GetRemoteStatusEnabledAsync(ct);
            RemoteStatusUrl = await _settingService.GetRemoteStatusUrlAsync(ct) ?? string.Empty;
            RemoteStatusInstanceName = await _settingService.GetRemoteStatusInstanceNameAsync(ct) ?? string.Empty;
            RemoteStatusInstanceId = IsRemoteStatusEnabled
                ? await _settingService.GetOrCreateRemoteStatusInstanceIdAsync(ct)
                : (await _settingService.GetSettingAsync(AppSettingService.RemoteStatusInstanceIdKey, ct)) ?? string.Empty;
            RemoteStatusToken = _credentialStore.GetCredential(
                AppSettingService.RemoteStatusRelayTokenCredentialTarget) ?? string.Empty;
            RemoteStatusConnectionText = _remoteStatusInfo.BuildStatusText();
            ScmTaskCompletionWaitMinutes = (await _settingService.GetScmTaskCompletionWaitMinutesAsync(ct))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            ScmTriggerKeyword = await _settingService.GetScmTriggerKeywordAsync(ct);
            ScmDelayKeyword = await _settingService.GetScmDelayKeywordAsync(ct);

            var globalLimit = await _settingService.GetIntSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, ct);
            MaxParallelExecutions = globalLimit is >= 1
                ? globalLimit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;

            // Issue-Aufbereitung: Funktionsschalter, Funktions-Plugin (leer = globale
            // Standard-CLI), Repository-Auswahl und Kommentar-Schalter.
            IsIssuePreparationEnabled = await _settingService.GetScmIssuePreparationEnabledAsync(ct);
            IsIssuePreparationCommentEnabled = await _settingService.GetScmIssuePreparationCommentEnabledAsync(ct);
            var preparationPrefix = await _settingService.GetScmIssuePreparationPluginPrefixAsync(ct);
            IssuePreparationAiPluginOptions.Clear();
            IssuePreparationAiPluginOptions.Add(IssuePreparationAiPluginOption.Default);
            foreach (var aiPlugin in AiPlugins)
                IssuePreparationAiPluginOptions.Add(new IssuePreparationAiPluginOption(aiPlugin.PluginPrefix, aiPlugin.PluginName));
            SelectedIssuePreparationAiPlugin = IssuePreparationAiPluginOptions
                .FirstOrDefault(x => string.Equals(x.PluginPrefix, preparationPrefix, StringComparison.Ordinal))
                ?? IssuePreparationAiPluginOption.Default;

            IssuePreparationRepositories.Clear();
            foreach (var repository in await _projectService.GetAllRepositoriesAsync(ct))
                IssuePreparationRepositories.Add(new IssuePreparationRepositoryEntry(repository, repository.IssuePreparationEnabled));
            _aiPluginParallelLimit.Clear();
            var pluginLimitValues = await _settingService.GetSettingsAsync(
                AiPlugins.Select(x => ScmEventExecutionLimitService.PluginLimitKey(x.PluginPrefix)).ToList(), ct);
            foreach (var aiPlugin in AiPlugins)
                _aiPluginParallelLimit[aiPlugin.PluginPrefix] =
                    pluginLimitValues.GetValueOrDefault(ScmEventExecutionLimitService.PluginLimitKey(aiPlugin.PluginPrefix));

            var promptAppend = await _settingService.GetScmPromptAppendicesAsync(ct);
            ScmPromptAppendixGeneral = promptAppend.GetValueOrDefault(AppSettingService.ScmPromptAppendGeneralKey) ?? string.Empty;
            ScmPromptAppendixIssue = promptAppend.GetValueOrDefault(AppSettingService.ScmPromptAppendIssueKey) ?? string.Empty;
            ScmPromptAppendixIssueComment = promptAppend.GetValueOrDefault(AppSettingService.ScmPromptAppendIssueCommentKey) ?? string.Empty;
            ScmPromptAppendixPullRequest = promptAppend.GetValueOrDefault(AppSettingService.ScmPromptAppendPullRequestKey) ?? string.Empty;
            ScmPromptAppendixPullRequestComment = promptAppend.GetValueOrDefault(AppSettingService.ScmPromptAppendPullRequestCommentKey) ?? string.Empty;

            // Die öffentlichen Setter benachrichtigen die abgeleiteten Anzeige-Properties
            // (Sichtbarkeit, Enabled, Panel-Status) — nach „Verwerfen“ bleibt die UI sonst
            // auf dem Stand der verworfenen Auswahl.
            SelectedBackmergeMode = ScmBackmergeModeOption.ForMode(await _settingService.GetScmBackmergeModeAsync(ct));
            SelectedAccountMode = ScmAccountModeOption.ForMode(await _settingService.GetScmAccountModeAsync(ct));
            SelectedScmTaskCompletionCheckmode = ScmTaskCompletionCheckModeOption.ForMode(
                await _settingService.GetScmTaskCompletionCheckModeAsync(ct));
            IsScmBackmergeDraftActivation = await _settingService.GetScmBackmergeDraftActivationAsync(ct);
            IsScmBackmergeAdminMerge = await _settingService.GetScmBackmergeAdminMergeAsync(ct);

            var updateSettings = await _settingService.GetUpdateSettingsAsync(ct);
            _selectedUpdateMode = UpdateModeOption.ForMode(updateSettings.Mode);
            OnPropertyChanged(nameof(SelectedUpdateMode));
            SelectedUpdatePath = UpdatePathOption.ForPath(updateSettings.UpdatePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Einstellungen.");
            ErrorMessage = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            if (!ValidateRequiredFields())
                return;

            if (SelectedUpdateMode is not { Mode: var updateMode })
            {
                ErrorMessage = "Der ausgewählte Update-Modus ist ungültig.";
                return;
            }

            if (SelectedUpdatePath is not { Path: var updatePath })
            {
                ErrorMessage = "Der ausgewählte Update-Pfad ist ungültig.";
                return;
            }

            if (SelectedBackmergeMode is not { Mode: var backmergeMode })
            {
                ErrorMessage = "Der ausgewählte Backmerge-Modus ist ungültig.";
                return;
            }

            if (SelectedAccountMode is not { Mode: var accountMode })
            {
                ErrorMessage = "Der ausgewählte Überwachungsmodus ist ungültig.";
                return;
            }

            if (SelectedScmTaskCompletionCheckmode is not { Mode: var checkMode })
            {
                ErrorMessage = "Der ausgewählte Prüfmodus ist ungültig.";
                return;
            }

            var updateSettings = new UpdateSettings(updateMode, updatePath);
            await _settingService.SetUpdateSettingsAsync(updateSettings, ct);
            UpdateSettingsSaved?.Invoke(this, updateSettings);

            foreach (var entry in SourceCodeManagementPlugins.Concat(DevelopmentAutomationPlugins).Concat(DevelopmentEnvironmentPlugins))
                await _pluginActivationService.SetPluginEnabledAsync(entry.PluginPrefix, entry.IsEnabled, ct);

            await SavePluginSettingsAsync(_selectedPlugin?.Plugin, _selectedPluginSettings, ct);

            await _workingDirectoryService.SaveWorkingDirectoryAsync(_workingDirectory, ct);
            await _settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, _defaultAiPlugin, ct);
            await _settingService.SetSettingAsync(AppSettingService.DefaultScmPluginKey, _defaultScmPlugin?.PluginName, ct);
            await SavePromptTemplatesAsync(ct);

            await _settingService.SetBoolSettingAsync(
                AppSettingService.ScmMonitoringEnabledKey,
                IsScmMonitoringEnabled,
                ct);
            await _settingService.SetBoolSettingAsync(
                AppSettingService.ScmTaskCompletionEnabledKey,
                IsScmTaskCompletionEnabled,
                ct);
            // Bei deaktivierter Abschlussüberwachung ist die Wartezeit nicht wirksam und wird
            // nicht überschrieben — ihr Feld ist dann deaktiviert und unvalidiert.
            if (IsScmTaskCompletionEnabled)
            {
                await _settingService.SetIntSettingAsync(
                    AppSettingService.ScmTaskCompletionWaitMinutesKey,
                    int.Parse(ScmTaskCompletionWaitMinutes.Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    ct);
            }
            await _settingService.SetSettingAsync(
                AppSettingService.ScmTriggerKeywordKey,
                ScmTriggerKeyword.Trim(),
                ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmDelayKeywordKey,
                ScmDelayKeyword.Trim(),
                ct);
            await _settingService.SetSettingAsync(
                ScmEventExecutionLimitService.GlobalLimitKey,
                NullWhenEmpty(MaxParallelExecutions?.Trim()),
                ct);
            await _settingService.SetBoolSettingAsync(
                AppSettingService.ScmIssuePreparationEnabledKey,
                IsIssuePreparationEnabled,
                ct);
            await _settingService.SetBoolSettingAsync(
                AppSettingService.ScmIssuePreparationCommentEnabledKey,
                IsIssuePreparationCommentEnabled,
                ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmIssuePreparationPluginKey,
                SelectedIssuePreparationAiPlugin?.PluginPrefix,
                ct);
            foreach (var repositoryEntry in IssuePreparationRepositories)
                await _projectService.SetIssuePreparationEnabledAsync(repositoryEntry.Repository.Id, repositoryEntry.IsSelected, ct);
            await _settingService.SetScmBackmergeModeAsync(backmergeMode, ct);
            await _settingService.SetScmAccountModeAsync(accountMode, ct);
            await _settingService.SetScmTaskCompletionCheckModeAsync(checkMode, ct);
            await _settingService.SetScmBackmergeDraftActivationAsync(IsScmBackmergeDraftActivation, ct);
            await _settingService.SetScmBackmergeAdminMergeAsync(IsScmBackmergeAdminMerge, ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmPromptAppendGeneralKey,
                NullWhenEmpty(ScmPromptAppendixGeneral),
                ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmPromptAppendIssueKey,
                NullWhenEmpty(ScmPromptAppendixIssue),
                ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmPromptAppendIssueCommentKey,
                NullWhenEmpty(ScmPromptAppendixIssueComment),
                ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmPromptAppendPullRequestKey,
                NullWhenEmpty(ScmPromptAppendixPullRequest),
                ct);
            await _settingService.SetSettingAsync(
                AppSettingService.ScmPromptAppendPullRequestCommentKey,
                NullWhenEmpty(ScmPromptAppendixPullRequestComment),
                ct);

            // Remote-Übersicht: Schalter, URL und Anzeigename über die generische
            // Einstellungstabelle; das Relay-Token getrennt im CredentialStore.
            await _settingService.SetRemoteStatusEnabledAsync(IsRemoteStatusEnabled, ct);
            await _settingService.SetRemoteStatusUrlAsync(NullWhenEmpty(RemoteStatusUrl?.Trim()), ct);
            await _settingService.SetRemoteStatusInstanceNameAsync(NullWhenEmpty(RemoteStatusInstanceName?.Trim()), ct);
            if (IsRemoteStatusEnabled)
                RemoteStatusInstanceId = await _settingService.GetOrCreateRemoteStatusInstanceIdAsync(ct);
            if (string.IsNullOrWhiteSpace(RemoteStatusToken))
            {
                _credentialStore.DeleteCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget);
            }
            else
            {
                try
                {
                    _credentialStore.SetCredential(
                        AppSettingService.RemoteStatusRelayTokenCredentialTarget,
                        RemoteStatusToken.Trim());
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "Das Relay-Token der Remote-Übersicht konnte nicht gespeichert werden.");
                    ErrorMessage = $"Das Relay-Token konnte nicht gespeichert werden: {ex.Message}";
                    return;
                }
            }

            try
            {
                await _darkModeService.SetModeAsync(DesignMode, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Design-Modus konnte nicht angewendet werden.");
            }

            SuccessMessage = "Settings gespeichert.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Einstellungen.");
            ErrorMessage = $"Fehler beim Speichern: {ex.Message}";
        }
    }

    private async Task DiscardAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
    }

    private void LoadSelectedPluginSettings(PluginActivationEntry entry)
    {
        SelectedPlugin = entry;
        SelectedPluginSettings = LoadPluginSettings(entry.Plugin);
        IsScmAiPluginContentVisible = true;
        IsIdePluginContentVisible = false;
    }

    /// <summary>Sucht den zu <paramref name="plugin"/> gehörenden Aktivierungseintrag über beide Kategorien und lädt dessen Settings.</summary>
    /// <param name="plugin">Das über eine der Standard-Plugin-ComboBoxen ausgewählte Plugin.</param>
    private void SelectPluginByReference(IPlugin? plugin)
    {
        var entry = SourceCodeManagementPlugins.Concat(DevelopmentAutomationPlugins).FirstOrDefault(e => e.Plugin == plugin);
        if (entry is not null)
            LoadSelectedPluginSettings(entry);
    }

    private async Task LoadPluginActivationAsync(CancellationToken ct)
    {
        SourceCodeManagementPlugins.Clear();
        foreach (var plugin in ScmPlugins)
        {
            var enabled = await _pluginActivationService.IsPluginEnabledAsync(plugin.PluginPrefix, ct);
            SourceCodeManagementPlugins.Add(new PluginActivationEntry(plugin, enabled));
        }

        DevelopmentAutomationPlugins.Clear();
        foreach (var plugin in AiPlugins)
        {
            var enabled = await _pluginActivationService.IsPluginEnabledAsync(plugin.PluginPrefix, ct);
            DevelopmentAutomationPlugins.Add(new PluginActivationEntry(plugin, enabled));
        }
    }

    private void LoadSelectedIdePluginSettings(PluginActivationEntry entry)
    {
        SelectedIdePlugin = entry;
        SelectedIdePluginSettings = LoadPluginSettings(entry.Plugin);
        IsIdePluginContentVisible = true;
        IsScmAiPluginContentVisible = false;
    }

    private async Task LoadIdePluginActivationAsync(CancellationToken ct)
    {
        foreach (var existingEntry in DevelopmentEnvironmentPlugins)
            existingEntry.PropertyChanged -= OnIdePluginEntryPropertyChanged;
        DevelopmentEnvironmentPlugins.Clear();

        var orderSetting = await _settingService.GetSettingAsync(AppSettingService.IdePluginOrderKey, ct);
        var order = ParseIdePluginOrder(orderSetting, IdePlugins);

        foreach (var prefix in order)
        {
            var plugin = IdePlugins.FirstOrDefault(p => string.Equals(p.PluginPrefix, prefix, StringComparison.OrdinalIgnoreCase));
            if (plugin is null)
                continue;

            var enabled = await _pluginActivationService.IsPluginEnabledAsync(plugin.PluginPrefix, ct);
            var entry = new PluginActivationEntry(plugin, enabled);
            entry.PropertyChanged += OnIdePluginEntryPropertyChanged;
            DevelopmentEnvironmentPlugins.Add(entry);
        }

        IdePluginOrder = DevelopmentEnvironmentPlugins.Select(e => e.PluginPrefix).ToList();
        OnPropertyChanged(nameof(DefaultIdePlugin));
    }

    private static List<string> ParseIdePluginOrder(string? orderSetting, IReadOnlyList<IIdePlugin> availablePlugins)
    {
        var discoveryOrder = availablePlugins.Select(p => p.PluginPrefix).ToList();
        return IdePluginOrderResolver.Apply(discoveryOrder, orderSetting);
    }

    private void OnIdePluginEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(PluginActivationEntry.IsEnabled), StringComparison.Ordinal))
            return;

        if (sender is not PluginActivationEntry entry)
            return;

        if (!entry.IsEnabled && DevelopmentEnvironmentPlugins.All(p => !p.IsEnabled))
        {
            entry.IsEnabled = true;
            ErrorMessage = "Mindestens ein IDE-Plugin muss aktiv bleiben.";
        }
    }

    private bool CanMoveIdePlugin(PluginActivationEntry? entry, int direction)
    {
        if (entry is null)
            return false;

        var index = DevelopmentEnvironmentPlugins.IndexOf(entry);
        if (index < 0)
            return false;

        var targetIndex = index + direction;
        return targetIndex >= 0 && targetIndex < DevelopmentEnvironmentPlugins.Count;
    }

    private void MoveIdePlugin(PluginActivationEntry? entry, int direction)
    {
        if (!CanMoveIdePlugin(entry, direction))
            return;

        var index = DevelopmentEnvironmentPlugins.IndexOf(entry!);
        DevelopmentEnvironmentPlugins.Move(index, index + direction);

        IdePluginOrder = DevelopmentEnvironmentPlugins.Select(e => e.PluginPrefix).ToList();
        OnPropertyChanged(nameof(DefaultIdePlugin));

        PersistiereIdePluginOrderAsync().SafeFireAndForget(_logger, "SettingsViewModel.PersistiereIdePluginOrderAsync");
    }

    private async Task PersistiereIdePluginOrderAsync()
    {
        try
        {
            await _settingService.SetSettingAsync(AppSettingService.IdePluginOrderKey, string.Join(",", IdePluginOrder));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der IDE-Plugin-Reihenfolge.");
        }
    }

    private IReadOnlyList<PluginSettingGroupEntry> LoadPluginSettings(IPlugin plugin)
    {
        var groups = plugin.GetSettingGroups()
            .Select(group => new PluginSettingGroupEntry(
                group.GroupName,
                group.Fields.Select(field => new PluginSettingEntry(
                    field,
                    _pluginSettingsService.GetValue(plugin, field),
                    useDefaultValue: !IsCodexCommandLineParameters(plugin, field)))
                .ToList()))
            .ToList();
        // Das Plugin-Parallel-Limit ist eine Host-Einstellung (AppEinstellung), kein
        // plugindeklariertes Credential-Store-Feld — daher wird es hier eingespeist.
        if (plugin is IAiPlugin)
            groups.Add(new PluginSettingGroupEntry(ParallelLimitGroupName,
                [new PluginSettingEntry(HostParallelLimitField,
                    _aiPluginParallelLimit.GetValueOrDefault(plugin.PluginPrefix),
                    useDefaultValue: false)]));
        return groups;
    }

    private static bool IsCodexCommandLineParameters(IPlugin plugin, PluginSettingField field) =>
        string.Equals(plugin.PluginPrefix, CodexPluginPrefix, StringComparison.Ordinal)
        && string.Equals(field.Key, CommandLineParameterKey, StringComparison.Ordinal);

    private async Task SavePluginSettingsAsync(IPlugin? plugin, IReadOnlyList<PluginSettingGroupEntry> settings, CancellationToken ct)
    {
        if (plugin is null)
            return;

        foreach (var group in settings)
        {
            foreach (var entry in group.Entries)
            {
                if (ReferenceEquals(entry.Field, HostParallelLimitField))
                {
                    var value = NullWhenEmpty(entry.Value.Trim());
                    await _settingService.SetSettingAsync(
                        ScmEventExecutionLimitService.PluginLimitKey(plugin.PluginPrefix), value, ct);
                    _aiPluginParallelLimit[plugin.PluginPrefix] = value;
                    continue;
                }
                _pluginSettingsService.SetValue(plugin, entry.Field, entry.Value);
            }
        }
    }

    private bool ValidateRequiredFields()
    {
        return ValidatePluginActivation()
            && ValidateRequiredFieldsForSettings(_selectedPluginSettings)
            && ValidatePromptTemplates()
            && ValidateScmTriggerKeyword()
            && ValidateScmDelayKeyword()
            && ValidateMaxParallelExecutions()
            && ValidateScmTaskCompletionWait()
            && ValidateScmAccountMode();
    }

    /// <summary>Im Kontomodus „Separates Konto“ ist ein effektives Agent-Token Pflicht — ein gerade
    /// eingegebener, noch ungespeicherter Wert im geöffneten GitHub-Einstellungspanel zählt bereits.</summary>
    private bool ValidateScmAccountMode()
    {
        // Der Überwachungsmodus ist nur bei aktivierter Ereignisüberwachung wirksam.
        if (!IsScmMonitoringEnabled || SelectedAccountMode?.Mode != ScmAccountMode.SeparateAccount)
            return true;

        var github = SourceCodeManagementPlugins.FirstOrDefault(x =>
            string.Equals(x.Plugin.PluginPrefix, GitHubPluginPrefix, StringComparison.Ordinal));
        var field = github?.Plugin.GetSettingGroups()
            .SelectMany(g => g.Fields)
            .FirstOrDefault(f => string.Equals(f.Key, GitHubAgentTokenFieldKey, StringComparison.Ordinal));
        var effektiv = github is null || field is null
            ? null
            : _selectedPluginSettings.SelectMany(g => g.Entries)
                .Where(e => e.Field == field)
                .Select(e => e.Value)
                .FirstOrDefault()
              ?? _pluginSettingsService.GetValue(github.Plugin, field);

        if (string.IsNullOrWhiteSpace(effektiv))
        {
            ErrorMessage = "Für den Überwachungsmodus „Separates Konto“ ist ein Personal Access Token des separaten Kontos erforderlich. Tragen Sie ihn unter Einstellungen → Plugins → GitHub → Authentifizierung im Feld „Personal Access Token des separaten Kontos“ ein.";
            return false;
        }

        return true;
    }

    /// <summary>Die Abschluss-Wartezeit muss eine nicht negative Ganzzahl in Minuten sein;
    /// bei deaktivierter Abschlussüberwachung ist das Feld deaktiviert und nicht wirksam.</summary>
    private bool ValidateScmTaskCompletionWait()
    {
        if (!IsTaskCompletionFieldEnabled)
            return true;

        if (!int.TryParse(ScmTaskCompletionWaitMinutes?.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var minutes) || minutes < 0)
        {
            ErrorMessage = $"Die Wartezeit für die SCM-Abschlussüberwachung muss eine nicht negative Ganzzahl in Minuten sein. Standard: {AppSettingService.ScmTaskCompletionDefaultWaitMinutes}";
            return false;
        }

        return true;
    }

    /// <summary>Das globale Parallel-Limit ist entweder leer (unbegrenzt) oder eine Ganzzahl ≥ 1.</summary>
    private bool ValidateMaxParallelExecutions()
    {
        var value = MaxParallelExecutions?.Trim();
        if (!string.IsNullOrEmpty(value)
            && (!int.TryParse(value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var limit) || limit < 1))
        {
            ErrorMessage = "Das globale Limit paralleler CLI-Ausführungen muss leer (unbegrenzt) oder eine Ganzzahl größer oder gleich 1 sein.";
            return false;
        }

        return true;
    }

    /// <summary>Leere oder nur aus Leerraum bestehende Prompt-Anhänge werden nicht gespeichert (leer = nichts anhängen).</summary>
    private static string? NullWhenEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Das Trigger-Keyword darf nicht leer sein; der Dienst fällt sonst auf den Standard
    /// zurück. Das Feld ist nur wirksam, wenn es auch sichtbar ist (aktive Überwachung im
    /// Benutzerkonto-Modus).</summary>
    private bool ValidateScmTriggerKeyword()
    {
        if (!IsTriggerKeywordVisible)
            return true;

        if (string.IsNullOrWhiteSpace(ScmTriggerKeyword))
        {
            ErrorMessage = $"Das Trigger-Keyword für SCM-Ereignisse darf nicht leer sein. Standard: {ScmEventValidationService.DefaultTriggerKeyword}";
            return false;
        }

        return true;
    }

    /// <summary>Das Verzögerungs-Keyword darf nicht leer sein; der Dienst fällt sonst auf den
    /// Standard zurück. Das Feld ist nur wirksam, wenn die Ereignisüberwachung aktiv ist.</summary>
    private bool ValidateScmDelayKeyword()
    {
        if (!IsDelayKeywordVisible)
            return true;

        if (string.IsNullOrWhiteSpace(ScmDelayKeyword))
        {
            ErrorMessage = $"Das Verzögerungs-Keyword für SCM-Ereignisse darf nicht leer sein. Standard: {ScmDelayDirective.DefaultDelayKeyword}";
            return false;
        }

        return true;
    }

    private bool ValidatePluginActivation()
    {
        if (SourceCodeManagementPlugins.Count > 0 && SourceCodeManagementPlugins.All(entry => !entry.IsEnabled))
        {
            ErrorMessage = "Mindestens ein Quellcodeverwaltungs-Plugin muss aktiv bleiben.";
            return false;
        }

        if (DevelopmentAutomationPlugins.Count > 0 && DevelopmentAutomationPlugins.All(entry => !entry.IsEnabled))
        {
            ErrorMessage = "Mindestens ein KI-Plugin muss aktiv bleiben.";
            return false;
        }

        if (DevelopmentEnvironmentPlugins.Count > 0 && DevelopmentEnvironmentPlugins.All(entry => !entry.IsEnabled))
        {
            ErrorMessage = "Mindestens ein IDE-Plugin muss aktiv bleiben.";
            return false;
        }

        return true;
    }

    private bool ValidateRequiredFieldsForSettings(IReadOnlyList<PluginSettingGroupEntry> settings)
    {
        foreach (var group in settings)
        {
            foreach (var entry in group.Entries)
            {
                if (entry.Field.IsRequired && string.IsNullOrEmpty(entry.Value))
                {
                    ErrorMessage = $"Pflichtfeld '{entry.Field.Label}' darf nicht leer sein.";
                    return false;
                }

                if (entry.Field.FieldType == PluginSettingFieldType.Integer
                    && !string.IsNullOrEmpty(entry.Value)
                    && (!int.TryParse(entry.Value, out var number)
                        || (ReferenceEquals(entry.Field, HostParallelLimitField) && number < 1)))
                {
                    ErrorMessage = ReferenceEquals(entry.Field, HostParallelLimitField)
                        ? $"Feld '{entry.Field.Label}' muss leer (unbegrenzt) oder eine Ganzzahl größer oder gleich 1 sein."
                        : $"Feld '{entry.Field.Label}' muss eine gültige Ganzzahl sein.";
                    return false;
                }

                if (entry.Field.FieldType == PluginSettingFieldType.Enum
                    && !string.IsNullOrEmpty(entry.Value)
                    && entry.Field.EnumOptions is not null
                    && !entry.Field.EnumOptions.Contains(entry.Value))
                {
                    ErrorMessage = $"Feld '{entry.Field.Label}' enthält einen ungültigen Wert.";
                    return false;
                }
            }
        }

        return true;
    }

    private async Task LoadPromptTemplatesAsync(CancellationToken ct)
    {
        var templates = await _promptTemplatesService.GetAllAsync(ct);
        PromptTemplates.Clear();
        _deletedPromptTemplatesIds.Clear();

        foreach (var template in templates)
        {
            PromptTemplates.Add(new PromptTemplateEntry(
                template.Id,
                template.Name,
                template.PromptText,
                false));
        }
    }

    private async Task SavePromptTemplatesAsync(CancellationToken ct)
    {
        foreach (var id in _deletedPromptTemplatesIds)
            await _promptTemplatesService.DeleteAsync(id, ct);

        foreach (var entry in PromptTemplates)
        {
            if (entry.IsNew)
            {
                await _promptTemplatesService.CreateAsync(entry.Name, entry.PromptText, ct);
            }
            else
            {
                await _promptTemplatesService.UpdateAsync(entry.Id, entry.Name, entry.PromptText, ct);
            }
        }

        _deletedPromptTemplatesIds.Clear();
        await LoadPromptTemplatesAsync(ct);
    }

    private bool ValidatePromptTemplates()
    {
        foreach (var template in PromptTemplates)
        {
            if (string.IsNullOrWhiteSpace(template.Name))
            {
                ErrorMessage = "Der Name einer Promptvorlage darf nicht leer sein.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(template.PromptText))
            {
                ErrorMessage = $"Der Prompttext der Vorlage '{template.Name}' darf nicht leer sein.";
                return false;
            }
        }

        return true;
    }

    private void AddPromptTemplate()
    {
        PromptTemplates.Add(new PromptTemplateEntry(Guid.NewGuid(), "Neue Promptvorlage", string.Empty, true));
    }

    private void DeletePromptTemplate(PromptTemplateEntry? entry)
    {
        if (entry is null)
            return;

        if (!entry.IsNew)
            _deletedPromptTemplatesIds.Add(entry.Id);

        PromptTemplates.Remove(entry);
    }

    private void OnDarkModeChanged(string mode) => DesignMode = mode;

    private void OnRemoteStatusStateChanged(object? sender, EventArgs e)
    {
        RemoteStatusConnectionText = _remoteStatusInfo.BuildStatusText();
        OnPropertyChanged(nameof(IsRemoteStatusConnectionError));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _darkModeService.ModeChanged -= OnDarkModeChanged;
        _remoteStatusInfo.StateChanged -= OnRemoteStatusStateChanged;

        foreach (var entry in DevelopmentEnvironmentPlugins)
            entry.PropertyChanged -= OnIdePluginEntryPropertyChanged;
    }
}

/// <summary>Editierbarer Eintrag fuer eine Promptvorlage in den Einstellungen.</summary>
public sealed class PromptTemplateEntry : ViewModelBase
{
    private string _name;
    private string _promptText;

    /// <inheritdoc cref="PromptTemplateEntry"/>
    public PromptTemplateEntry(Guid id, string name, string promptText, bool isNew)
    {
        Id = id;
        _name = name;
        _promptText = promptText;
        IsNew = isNew;
    }

    /// <summary>ID der Promptvorlage.</summary>
    public Guid Id { get; }

    /// <summary>Gibt an, ob der Eintrag noch nicht persistiert ist.</summary>
    public bool IsNew { get; }

    /// <summary>Anzeigename.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Prompttext.</summary>
    public string PromptText
    {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }
}

/// <summary>Auswahloption des Funktions-Plugins für die Issue-Aufbereitung.
/// <see cref="PluginPrefix"/> = <c>null</c> steht für den Standard-Eintrag
/// „Globale Standard-CLI verwenden" (Fallback-Kette der Plugin-Auflösung).</summary>
/// <param name="PluginPrefix">Der Plugin-Prefix oder <c>null</c> für die globale Standard-CLI.</param>
/// <param name="Label">Der Anzeigename der Option.</param>
public sealed record IssuePreparationAiPluginOption(string? PluginPrefix, string Label)
{
    /// <summary>Der Standard-Eintrag: Kein dediziertes Funktions-Plugin — es greift die
    /// Plugin-Auflösungskette (Projekt-Default → Global-Default → konfigurierter Standard).</summary>
    public static IssuePreparationAiPluginOption Default { get; } = new(null, "Globale Standard-CLI verwenden");

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>Editierbarer Repository-Eintrag der Issue-Aufbereitung (persistiertes Flag
/// <see cref="GitRepository.IssuePreparationEnabled"/>). Der Anzeigename ist der
/// Repository-Name — keine internen Kennungen als Benutzereingabe.</summary>
public sealed class IssuePreparationRepositoryEntry : ViewModelBase
{
    private bool _isSelected;

    /// <inheritdoc cref="IssuePreparationRepositoryEntry"/>
    public IssuePreparationRepositoryEntry(GitRepository repository, bool isSelected)
    {
        Repository = repository;
        _isSelected = isSelected;
    }

    /// <summary>Die zugehörige Repository-Entität.</summary>
    public GitRepository Repository { get; }

    /// <summary>Anzeigename (Repository-Name).</summary>
    public string RepositoryName => Repository.RepositoryName;

    /// <summary>Gibt an, ob neu angelegte Issues dieses Repositorys aufbereitet werden.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
