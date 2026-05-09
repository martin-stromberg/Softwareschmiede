namespace Softwareschmiede.Components.Pages.Projekte;

using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

public partial class ProjektDetail
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private AufgabeService AufgabeService { get; set; } = null!;
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
    private string _repoUrl = string.Empty;
    private string _repoName = string.Empty;
    private string? _repoFehler;

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
        }
        _loading = false;
    }

    private void NeuAufgabe() => NavigationManager.NavigateTo($"/projekte/{Id}/aufgaben/neu");

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
        NavigationManager.NavigateTo("/projekte");
    }

    private async Task AddRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_repoUrl) || string.IsNullOrWhiteSpace(_repoName))
        {
            _repoFehler = "URL und Name sind Pflichtfelder.";
            return;
        }
        await ProjektService.AddRepositoryAsync(Id, "GitHub", _repoUrl.Trim(), _repoName.Trim());
        _repoUrl = string.Empty;
        _repoName = string.Empty;
        _showRepoForm = false;
        await LadeAsync();
    }

    private void ZurAufgabe(Guid aufgabeId) => NavigationManager.NavigateTo($"/aufgaben/{aufgabeId}");
}
