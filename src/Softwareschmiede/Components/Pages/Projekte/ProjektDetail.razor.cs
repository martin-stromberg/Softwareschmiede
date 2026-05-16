namespace Softwareschmiede.Components.Pages.Projekte;

using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

public partial class ProjektDetail
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private AufgabeService AufgabeService { get; set; } = null!;
    [Inject] private IPluginManager PluginManager { get; set; } = null!;
    [Inject] private PluginSelectionService PluginSelectionService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private bool _loading = true;
    private Projekt? _projekt;
    private List<Aufgabe> _aufgaben = [];
    private List<Aufgabe> _aufgabenArchiviert = [];
    private bool _showArchiviertPanel;
    private bool _showEditForm;
    private bool _showDeleteConfirm;
    private bool _showRepoForm;
    private string _editName = string.Empty;
    private string? _editBeschreibung;
    private string? _fehler;
    private IReadOnlyList<IGitPlugin> _sourceCodePlugins = [];
    private IReadOnlyList<PluginSettingField> _repositoryLinkFields = [];
    private readonly Dictionary<string, string> _repositoryFieldValues = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedRepositoryPluginPrefix;
    private string? _repoFehler;
    private bool _showStartKonfigurationForm;
    private Guid? _selectedStartKonfigurationRepositoryId;
    private string _startScriptRelativePath = string.Empty;
    private bool _startKonfigurationAktiv = true;
    private string? _startKonfigurationFehler;

    protected override async Task OnInitializedAsync()
    {
        await LadeAsync();
    }

    private async Task LadeAsync()
    {
        _loading = true;
        _projekt = await ProjektService.GetDetailAsync(Id);
        if (_projekt is not null)
        {
            _aufgaben = (await AufgabeService.GetByProjektAsync(Id)).ToList();
            _aufgabenArchiviert = (await AufgabeService.GetArchiviertByProjektAsync(Id)).ToList();
            _editName = _projekt.Name;
            _editBeschreibung = _projekt.Beschreibung;
            await LadeRepositoryPluginAuswahlAsync();
        }
        _loading = false;
    }

    private void NeuAufgabe() => NavigationManager.NavigateTo($"projekte/{Id}/aufgaben/neu");

    private async Task ArchivierenAsync()
    {
        await ProjektService.ArchivierenAsync(Id);
        await LadeAsync();
    }

    private async Task UpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(_editName)) { _fehler = "Name ist Pflichtfeld."; return; }
        await ProjektService.UpdateAsync(Id, _editName, _editBeschreibung);
        _showEditForm = false;
        await LadeAsync();
    }

    private async Task DeleteAsync()
    {
        await ProjektService.DeleteAsync(Id);
        NavigationManager.NavigateTo("projekte");
    }

    private async Task ToggleRepositoryFormAsync()
    {
        _showRepoForm = !_showRepoForm;
        _repoFehler = null;

        if (_showRepoForm)
        {
            await LadeRepositoryPluginAuswahlAsync();
        }
    }

    private async Task OnRepositoryPluginChangedAsync(ChangeEventArgs eventArgs)
    {
        _selectedRepositoryPluginPrefix = eventArgs.Value?.ToString();
        await LadeRepositoryPluginAuswahlAsync();
    }

    private async Task AddRepositoryAsync()
    {
        var selectedPlugin = ResolveSelectedRepositoryPlugin();
        if (selectedPlugin is null)
        {
            _repoFehler = "Es ist kein SourceCode-Plugin verfügbar.";
            return;
        }

        if (_repositoryLinkFields.Count == 0)
        {
            _repoFehler = $"Plugin '{selectedPlugin.PluginName}' liefert keine Eingabefelder für die Repository-Verknüpfung.";
            return;
        }

        var normalizedFieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in _repositoryLinkFields)
        {
            var value = GetRepositoryFieldValue(field.Key).Trim();
            if (field.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                _repoFehler = $"{field.Label} ist ein Pflichtfeld.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                normalizedFieldValues[field.Key] = value;
            }
        }

        await ProjektService.AddRepositoryAsync(Id, selectedPlugin.PluginPrefix, normalizedFieldValues);
        ResetRepositoryFieldValues();
        _showRepoForm = false;
        await LadeAsync();
    }

    private void StartKonfigurationBearbeiten(Guid repositoryId)
    {
        var repository = _projekt?.Repositories.FirstOrDefault(r => r.Id == repositoryId);
        if (repository is null)
        {
            return;
        }

        _selectedStartKonfigurationRepositoryId = repositoryId;
        _startScriptRelativePath = repository.StartKonfiguration?.StartScriptRelativePath ?? string.Empty;
        _startKonfigurationAktiv = repository.StartKonfiguration?.Aktiv ?? true;
        _startKonfigurationFehler = null;
        _showRepoForm = false;
        _showStartKonfigurationForm = true;
    }

    private async Task SpeichereStartKonfigurationAsync()
    {
        if (_selectedStartKonfigurationRepositoryId is null)
        {
            _startKonfigurationFehler = "Kein Repository ausgewählt.";
            return;
        }

        try
        {
            await ProjektService.SaveRepositoryStartKonfigurationAsync(
                _selectedStartKonfigurationRepositoryId.Value,
                _startScriptRelativePath,
                _startKonfigurationAktiv);

            _showStartKonfigurationForm = false;
            await LadeAsync();
        }
        catch (Exception ex)
        {
            _startKonfigurationFehler = ex.Message;
        }
    }

    private async Task LadeRepositoryPluginAuswahlAsync()
    {
        _sourceCodePlugins = PluginManager.GetSourceCodeManagementPlugins();
        if (_sourceCodePlugins.Count == 0)
        {
            _selectedRepositoryPluginPrefix = null;
            _repositoryLinkFields = [];
            _repositoryFieldValues.Clear();
            return;
        }

        var resolvedPlugin = await PluginSelectionService.ResolveSourceCodeManagementPluginAsync(_selectedRepositoryPluginPrefix);
        _selectedRepositoryPluginPrefix = resolvedPlugin.PluginPrefix;
        ApplyRepositoryFieldSchema(resolvedPlugin);
    }

    private IGitPlugin? ResolveSelectedRepositoryPlugin()
    {
        if (_sourceCodePlugins.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_selectedRepositoryPluginPrefix))
        {
            return _sourceCodePlugins[0];
        }

        return _sourceCodePlugins.FirstOrDefault(plugin =>
                   string.Equals(plugin.PluginPrefix, _selectedRepositoryPluginPrefix, StringComparison.OrdinalIgnoreCase))
               ?? _sourceCodePlugins[0];
    }

    private void ApplyRepositoryFieldSchema(IGitPlugin plugin)
    {
        _repositoryLinkFields = plugin.GetRepositoryLinkFields();
        var allowedKeys = _repositoryLinkFields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _repositoryFieldValues.Keys.Where(key => !allowedKeys.Contains(key)).ToList())
        {
            _repositoryFieldValues.Remove(key);
        }

        foreach (var field in _repositoryLinkFields)
        {
            _repositoryFieldValues.TryAdd(field.Key, string.Empty);
        }
    }

    private string GetRepositoryFieldValue(string fieldKey)
        => _repositoryFieldValues.GetValueOrDefault(fieldKey, string.Empty);

    private void SetRepositoryFieldValue(string fieldKey, string value)
        => _repositoryFieldValues[fieldKey] = value;

    private static string GetRepositoryInputType(PluginSettingField field)
        => field.FieldType switch
        {
            PluginSettingFieldType.Url => "url",
            PluginSettingFieldType.Integer => "number",
            _ => "text"
        };

    private static bool IsWebUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private void ResetRepositoryFieldValues()
    {
        foreach (var field in _repositoryLinkFields)
        {
            _repositoryFieldValues[field.Key] = string.Empty;
        }
    }

    private void ZurAufgabe(Guid aufgabeId) => NavigationManager.NavigateTo($"aufgaben/{aufgabeId}");
}
