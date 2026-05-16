namespace Softwareschmiede.Components.Pages.Projekte;

using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

public partial class ProjektListe
{
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private bool _loading = true;
    private bool _saving;
    private bool _showForm;
    private Guid? _editId;
    private string _formName = string.Empty;
    private string? _formBeschreibung;
    private string? _fehler;
    private List<Projekt> _projekte = [];

    protected override async Task OnInitializedAsync()
    {
        _projekte = (await ProjektService.GetAllAsync()).ToList();
        _loading = false;
    }

    private void NeuAnlegen()
    {
        _editId = null;
        _formName = string.Empty;
        _formBeschreibung = null;
        _fehler = null;
        _showForm = true;
    }

    private async Task SpeichernAsync()
    {
        if (string.IsNullOrWhiteSpace(_formName))
        {
            _fehler = "Name ist Pflichtfeld.";
            return;
        }
        _saving = true;
        _fehler = null;
        try
        {
            await ProjektService.CreateAsync(_formName.Trim(), _formBeschreibung?.Trim());
            _projekte = (await ProjektService.GetAllAsync()).ToList();
            _showForm = false;
        }
        catch (Exception ex)
        {
            _fehler = ex.Message;
        }
        finally { _saving = false; }
    }

    private void FormAbbrechen() { _showForm = false; _fehler = null; }

    private void ZurDetail(Guid id) => NavigationManager.NavigateTo($"projekte/{id}");
}
