namespace Softwareschmiede.Components.Pages.Aufgaben;

using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

public partial class AufgabeDetail : IDisposable
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private AufgabeService AufgabeService { get; set; } = null!;
    [Inject] private EntwicklungsprozessService EntwicklungsprozessService { get; set; } = null!;
    [Inject] private KiAusfuehrungsService KiAusfuehrungsService { get; set; } = null!;
    [Inject] private GitOrchestrationService GitService { get; set; } = null!;
    [Inject] private ProtokollService ProtokollService { get; set; } = null!;
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private IAgentPackageService AgentPackageService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ILogger<AufgabeDetail> _logger { get; set; } = null!;

    private bool _loading = true;
    private bool _processing;
    private Aufgabe? _aufgabe;
    private List<Protokolleintrag> _protokoll = [];
    private List<AgentPackageInfo> _agentenpakete = [];
    private List<AgentInfo> _agenten = [];
    private List<string> _streamingLines = [];

    // Subscription auf laufende KI-Session (wird beim Dispose freigegeben)
    private IDisposable? _kiSubscription;

    // Forms
    private bool _showCommitForm;
    private bool _showPushPullButtons;
    private bool _showResetForm;
    private bool _showPullRequestForm;
    private bool _showAbbrechenConfirm;
    private bool _showArchivierenConfirm;
    private bool _showDeleteConfirm;
    private bool _showStartDialog;
    private bool _editAnforderung;
    private bool _loadingBranches;
    private string _anforderungInput = string.Empty;
    private string _selectedPaketName = string.Empty;
    private string _selectedBranchName = string.Empty;
    private List<string> _remoteBranches = [];
    private string _selectedAgentName = string.Empty;
    private string _kiAgentName = string.Empty;
    private string _prompt = string.Empty;
    private string _folgePrompt = string.Empty;
    private string _commitMessage = string.Empty;
    private string _resetType = "mixed";
    private string? _resetRef;
    private string _prRepositoryId = string.Empty;
    private string _prTitel = string.Empty;
    private string? _prBody;
    private string? _fehler;
    private string? _erfolg;

    // TODO: Liste kann bei Bedarf aus der Plugin-Konfiguration dynamisch befüllt werden
    private static readonly IReadOnlyList<(string Value, string Label)> _verfuegbareModelle =
    [
        ("", "– Automatisch (GitHub wählt) –"),
        ("gpt-4o", "GPT-4o"),
        ("gpt-4.1", "GPT-4.1"),
        ("claude-3-7-sonnet", "Claude 3.7 Sonnet"),
        ("claude-sonnet-4-5", "Claude Sonnet 4.5"),
        ("gemini-2.0-flash", "Gemini 2.0 Flash"),
        ("o3", "o3"),
        ("o4-mini", "o4-mini"),
    ];

    private string _selectedModel = string.Empty;

    private CancellationTokenSource _cts = new();

    protected override async Task OnInitializedAsync()
    {
        await LadeAsync();
        _agentenpakete = (await AgentPackageService.GetPackagesAsync()).ToList();

        // Wenn beim Seitenaufruf ein KI-Lauf im Hintergrund läuft: pufferlosen Stand wiederherstellen
        // und auf neue Zeilen subscriben, damit die Ausgabe live weiterläuft.
        if (KiAusfuehrungsService.IsRunning(Id))
        {
            _processing = true;
            _streamingLines = [.. KiAusfuehrungsService.GetBufferedLines(Id)];
            KiLiveSubscribieren();
        }
    }

    private async Task LadeAsync()
    {
        _loading = true;
        _aufgabe = await AufgabeService.GetDetailAsync(Id);
        if (_aufgabe is not null)
        {
            _protokoll = (await ProtokollService.GetByAufgabeAsync(Id)).ToList();
            _prTitel = _aufgabe.Titel;
            // Agenten laden wenn Paket gesetzt
            if (!string.IsNullOrEmpty(_aufgabe.AgentenpaketName))
            {
                _selectedPaketName = _aufgabe.AgentenpaketName;
                await AgentenLadenAsync();
            }

            // Ausgewählten Agenten wiederherstellen
            if (!string.IsNullOrEmpty(_aufgabe.AgentenName))
            {
                _kiAgentName = _aufgabe.AgentenName;
                _selectedAgentName = _aufgabe.AgentenName;
            }

            // Anforderungsbeschreibung als initialen Prompt vorbelegen, solange noch kein Prompt gesendet wurde
            if (!string.IsNullOrWhiteSpace(_aufgabe.AnforderungsBeschreibung)
                && string.IsNullOrWhiteSpace(_prompt)
                && !_protokoll.Any(p => p.Typ == ProtokollTyp.Prompt))
            {
                _prompt = _aufgabe.AnforderungsBeschreibung;
            }
        }
        _loading = false;
    }

    private async Task AgentenLadenAsync()
    {
        if (string.IsNullOrEmpty(_selectedPaketName)) { _agenten = []; return; }
        var paket = await AgentPackageService.GetPackageAsync(_selectedPaketName);
        _agenten = paket?.Agenten.ToList() ?? [];
    }

    private void AnforderungBearbeitenStarten()
    {
        _anforderungInput = _aufgabe?.AnforderungsBeschreibung ?? string.Empty;
        _editAnforderung = true;
    }

    private void AnforderungBearbeitenAbbrechen()
    {
        _editAnforderung = false;
        _anforderungInput = string.Empty;
    }

    private async Task AnforderungSpeichernAsync()
    {
        if (_aufgabe is null) { return; }

        _processing = true;
        _fehler = null;
        try
        {
            await AufgabeService.UpdateAsync(Id, _aufgabe.Titel, _anforderungInput, _aufgabe.AgentenpaketName, _aufgabe.AgentenName);
            _editAnforderung = false;
            _erfolg = "Anforderungsbeschreibung gespeichert.";
            await LadeAsync();
            await ClearErfolgAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async void StartDialogOeffnen()
    {
        _fehler = null;
        _selectedBranchName = string.Empty;
        _remoteBranches = [];
        _showStartDialog = true;

        // Remote-Branches im Hintergrund laden
        await LadeRemoteBranchesAsync();
    }

    private async Task LadeRemoteBranchesAsync()
    {
        _loadingBranches = true;
        StateHasChanged();

        try
        {
            var projekt = await ProjektService.GetDetailAsync(_aufgabe!.ProjektId);
            var repo = _aufgabe.GitRepositoryId is not null
                ? projekt?.Repositories.FirstOrDefault(r => r.Id == _aufgabe.GitRepositoryId)
                : projekt?.Repositories.FirstOrDefault(r => r.Aktiv);

            if (repo is not null)
            {
                _remoteBranches = (await EntwicklungsprozessService.GetRemoteBranchesAsync(repo.RepositoryUrl, _cts.Token)).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote-Branches konnten nicht geladen werden.");
            // Kein Fehler zeigen – Branch-Auswahl einfach weglassen
        }
        finally
        {
            _loadingBranches = false;
            StateHasChanged();
        }
    }

    private void StartDialogSchliessen()
    {
        if (_processing) { return; }
        _showStartDialog = false;
    }

    private async Task ProzessStartenAsync()
    {
        _processing = true;
        _fehler = null;
        try
        {
            // Agentenpaket + Agent speichern
            if (!string.IsNullOrEmpty(_selectedPaketName))
                await AufgabeService.UpdateAsync(Id, _aufgabe!.Titel, _aufgabe.AnforderungsBeschreibung, _selectedPaketName, _selectedAgentName);

            // Repository-URL ermitteln: erst verknüpftes Repo, dann erstes aktives des Projekts
            var projekt = await ProjektService.GetDetailAsync(_aufgabe!.ProjektId);
            var repo = _aufgabe.GitRepositoryId is not null
                ? projekt?.Repositories.FirstOrDefault(r => r.Id == _aufgabe.GitRepositoryId)
                : projekt?.Repositories.FirstOrDefault(r => r.Aktiv);

            if (repo is null)
            {
                _fehler = "Kein aktives Repository im Projekt vorhanden. Bitte zuerst ein Repository anlegen.";
                return;
            }

            await EntwicklungsprozessService.ProzessStartenAsync(Id, repo.RepositoryUrl, string.IsNullOrEmpty(_selectedBranchName) ? null : _selectedBranchName, _cts.Token);
            _showStartDialog = false;
            _erfolg = "Entwicklungsumgebung erfolgreich gestartet.";
            await LadeAsync();
            await ClearErfolgAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task KiStartenAsync()
    {
        await KiMitPromptStartenAsync(_prompt);
        _prompt = string.Empty;
    }

    private async Task FolgePromptAsync()
    {
        await KiMitPromptStartenAsync(_folgePrompt);
        _folgePrompt = string.Empty;
    }

    private async Task KiMitPromptStartenAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;

        // Keinen Agenten-Namen übergeben wenn kein passender Agent gefunden wurde –
        // AgentInfo mit leerem Name führt dazu, dass --agent weggelassen wird.
        var agent = _agenten.FirstOrDefault(a => a.Name == _kiAgentName)
            ?? _agenten.FirstOrDefault()
            ?? new AgentInfo(string.Empty, null, string.Empty);

        _processing = true;
        _fehler = null;
        _streamingLines = [];

        // Bisherige Subscription aufräumen
        _kiSubscription?.Dispose();
        _kiSubscription = null;

        // Bereinigt eine ggf. bereits abgeschlossene Session, damit eine neue angelegt wird
        KiAusfuehrungsService.SessionBereinigen(Id);

        // Hintergrundlauf starten – kehrt sofort zurück
        KiAusfuehrungsService.StartKiLauf(
            Id,
            prompt,
            agent,
            model: string.IsNullOrEmpty(_selectedModel) ? null : _selectedModel,
            onStarted: () => InvokeAsync(StateHasChanged),
            onCompleted: fehler => InvokeAsync(async () =>
            {
                _processing = false;
                _kiSubscription?.Dispose();
                _kiSubscription = null;
                if (fehler)
                    _fehler = "KI-Ausführung fehlgeschlagen. Siehe Protokoll für Details.";
                await LadeAsync(); // Protokoll neu laden nach Abschluss
                StateHasChanged();
            }));

        // Auf Live-Ausgabe subscriben – neue Zeilen werden direkt in _streamingLines eingefügt
        KiLiveSubscribieren();

        StateHasChanged();
        await Task.CompletedTask;
    }

    /// <summary>Subscribed auf neue Ausgabezeilen des laufenden KI-Hintergrundlaufs.</summary>
    private void KiLiveSubscribieren()
    {
        _kiSubscription?.Dispose();
        _kiSubscription = KiAusfuehrungsService.Subscribe(Id, line =>
        {
            _streamingLines.Add(line);
            // UI in den Blazor-Circuit-Thread marshalieren
            InvokeAsync(StateHasChanged);
        });
    }

    private async Task CommitAsync()
    {
        if (string.IsNullOrWhiteSpace(_commitMessage)) { _fehler = "Commit-Nachricht ist Pflichtfeld."; return; }
        _processing = true;
        try
        {
            await GitService.CommitAsync(Id, _commitMessage, _cts.Token);
            _commitMessage = string.Empty;
            _showCommitForm = false;
            _erfolg = "Commit erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task PushAsync()
    {
        _processing = true;
        try
        {
            await GitService.PushAsync(Id, _cts.Token);
            _erfolg = "Push erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task PullAsync()
    {
        _processing = true;
        try
        {
            await GitService.PullAsync(Id, _cts.Token);
            _erfolg = "Pull erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task ResetAsync()
    {
        _processing = true;
        try
        {
            await GitService.ResetAsync(Id, _resetType, _resetRef, _cts.Token);
            _showResetForm = false;
            _erfolg = $"git reset --{_resetType} erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task PullRequestErstellenAsync()
    {
        if (string.IsNullOrWhiteSpace(_prTitel))
        {
            _fehler = "Titel ist ein Pflichtfeld.";
            return;
        }
        _processing = true;
        try
        {
            // Repository-ID wird automatisch aus der Aufgabe ermittelt
            var pr = await GitService.PullRequestErstellenAsync(
                Id,
                repositoryIdOverride: !string.IsNullOrWhiteSpace(_prRepositoryId) ? _prRepositoryId : null,
                title: _prTitel,
                body: _prBody ?? string.Empty,
                ct: _cts.Token);
            _showPullRequestForm = false;
            _prRepositoryId = string.Empty;
            _prTitel = string.Empty;
            _prBody = null;
            _erfolg = $"Pull Request #{pr.Nummer} erstellt: {pr.Url}";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task AbschliessenAsync()
    {
        _processing = true;
        try
        {
            await EntwicklungsprozessService.AbschliessenAsync(Id, _cts.Token);
            _erfolg = "Aufgabe abgeschlossen.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task ArchivierenAsync()
    {
        _processing = true;
        _showArchivierenConfirm = false;
        try
        {
            await AufgabeService.ArchivierenAsync(Id, _cts.Token);
            _erfolg = "Aufgabe archiviert.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task AufgabeLoeschenAsync()
    {
        _processing = true;
        _showDeleteConfirm = false;
        try
        {
            await AufgabeService.DeleteAsync(Id, _cts.Token);
            if (_aufgabe is not null)
                NavigationManager.NavigateTo($"/projekte/{_aufgabe.ProjektId}");
        }
        catch (Exception ex) { _fehler = ex.Message; _processing = false; }
    }

    private async Task AbbrechenAsync()
    {
        _processing = true;
        _showAbbrechenConfirm = false;
        try
        {
            await EntwicklungsprozessService.AbbrechenAsync(Id, _cts.Token);
            _erfolg = "Aufgabe abgebrochen.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private void Zurueck()
    {
        if (_aufgabe is not null)
            NavigationManager.NavigateTo($"/projekte/{_aufgabe.ProjektId}");
        else
            NavigationManager.NavigateTo("/projekte");
    }

    private static string GetProtokollCssClass(ProtokollTyp typ) => typ switch
    {
        ProtokollTyp.Prompt => "protokoll-prompt",
        ProtokollTyp.KiAntwort => "protokoll-antwort",
        ProtokollTyp.StatusUebergang => "protokoll-status",
        ProtokollTyp.GitAktion => "protokoll-git",
        ProtokollTyp.TestErgebnis => "protokoll-test",
        _ => ""
    };

    private static string GetProtokollLabel(ProtokollTyp typ) => typ switch
    {
        ProtokollTyp.Prompt => "Prompt",
        ProtokollTyp.KiAntwort => "KI",
        ProtokollTyp.StatusUebergang => "Status",
        ProtokollTyp.GitAktion => "Git",
        ProtokollTyp.TestErgebnis => "Test",
        _ => typ.ToString()
    };

    private async Task ClearErfolgAsync()
    {
        await Task.Delay(4000);
        _erfolg = null;
        StateHasChanged();
    }

    public void Dispose()
    {
        // Subscription freigeben: Background-Task läuft weiter, nur der Live-Feed wird getrennt
        _kiSubscription?.Dispose();
        // _cts wird nur für sonstige UI-Operationen (nicht für KI-Hintergrundlauf) verwendet
        _cts.Cancel();
        _cts.Dispose();
    }
}
