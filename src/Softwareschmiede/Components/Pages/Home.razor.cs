using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Components.Pages;

public partial class Home
{
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private AufgabeService AufgabeService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private bool _loading = true;
    private int _aktiveProjekte;
    private int _offeneAufgaben;
    private int _kiAktiveAufgaben;
    private List<Aufgabe> _aktiveAufgaben = [];
    private List<Projekt> _projekte = [];

    protected override async Task OnInitializedAsync()
    {
        _projekte = (await ProjektService.GetAllAsync()).ToList();
        _aktiveProjekte = _projekte.Count(p => p.Status == ProjektStatus.Aktiv);

        var alleAufgaben = new List<Aufgabe>();
        foreach (var projekt in _projekte)
        {
            var aufgaben = await AufgabeService.GetByProjektAsync(projekt.Id);
            alleAufgaben.AddRange(aufgaben);
        }

        _offeneAufgaben = alleAufgaben.Count(a => a.Status == AufgabeStatus.Neu);
        _kiAktiveAufgaben = alleAufgaben.Count(a => a.Status == AufgabeStatus.InArbeit);
        _aktiveAufgaben = alleAufgaben
            .Where(a => a.Status is AufgabeStatus.Gestartet or AufgabeStatus.InArbeit or AufgabeStatus.Wartend)
            .OrderByDescending(a => a.ErstellungsDatum)
            .ToList();

        _loading = false;
    }

    private string GetProjektName(Guid projektId) =>
        _projekte.FirstOrDefault(p => p.Id == projektId)?.Name ?? "–";

    private void NavigateToAufgabe(Guid id) =>
        NavigationManager.NavigateTo($"aufgaben/{id}");
}
