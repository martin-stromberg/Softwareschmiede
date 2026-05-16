namespace Softwareschmiede.Components.Pages.Aufgaben;

using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

public partial class NeueAufgabe
{
    [Parameter] public Guid ProjektId { get; set; }
    [Inject] private AufgabeService AufgabeService { get; set; } = null!;
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private GitOrchestrationService GitService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private bool _saving;
    private bool _loadingIssues;
    private string _issuesFehler = string.Empty;
    private string _titel = string.Empty;
    private string? _beschreibung;
    private string? _fehler;
    private Guid? _repositoryId;
    private List<GitRepository> _repositories = [];
    private List<Issue> _issues = [];
    private Issue? _selectedIssue;

    protected override async Task OnInitializedAsync()
    {
        var projekt = await ProjektService.GetDetailAsync(ProjektId);
        if (projekt is not null)
        {
            _repositories = projekt.Repositories.Where(r => r.Aktiv).ToList();
            // Erstes aktives Repository automatisch vorbelegen
            _repositoryId = _repositories.FirstOrDefault()?.Id;
        }
        // Issues asynchron laden
        _ = LadeIssuesAsync();
    }

    private async Task LadeIssuesAsync()
    {
        if (_repositories.Count == 0) return;
        _loadingIssues = true;
        _issuesFehler = string.Empty;
        StateHasChanged();
        try
        {
            var ersteRepo = _repositories.First();
            var issues = await GitService.IssuesAbrufenAsync(ersteRepo.RepositoryName);
            _issues = issues.ToList();
        }
        catch (Exception ex)
        {
            _issuesFehler = $"Issues konnten nicht geladen werden: {ex.Message}";
        }
        finally
        {
            _loadingIssues = false;
            StateHasChanged();
        }
    }

    private void IssueGewaehlt(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var nummer)) { _selectedIssue = null; return; }
        _selectedIssue = _issues.FirstOrDefault(i => i.Nummer == nummer);
        if (_selectedIssue is not null)
        {
            _titel = _selectedIssue.Titel;
            _beschreibung = _selectedIssue.Body;
        }
    }

    private async Task ErstellenAsync()
    {
        if (string.IsNullOrWhiteSpace(_titel)) { _fehler = "Titel ist Pflichtfeld."; return; }
        _saving = true;
        _fehler = null;
        try
        {
            Aufgabe aufgabe;
            if (_selectedIssue is not null)
                aufgabe = await AufgabeService.CreateFromIssueAsync(ProjektId, _selectedIssue, _repositoryId);
                else
                    aufgabe = await AufgabeService.CreateAsync(ProjektId, _titel.Trim(), _beschreibung?.Trim(), _repositoryId);
                NavigationManager.NavigateTo($"aufgaben/{aufgabe.Id}");
        }
        catch (Exception ex)
        {
            _fehler = ex.Message;
            _saving = false;
        }
    }

    private void Zurueck() => NavigationManager.NavigateTo($"projekte/{ProjektId}");
}
