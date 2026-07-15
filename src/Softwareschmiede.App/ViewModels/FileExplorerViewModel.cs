using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Extensions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Presentation Model des Dateiexplorers: verwaltet Baum, Commits, Auswahl, Dateiinhalt, Diff-Zeilen und Modus.
/// </summary>
public sealed class FileExplorerViewModel : ViewModelBase
{
    private readonly IGitWorkspaceBrowserService _gitWorkspaceBrowserService;
    private readonly ITextDiffService _textDiffService;
    private readonly ILogger<FileExplorerViewModel> _logger;
    private readonly Action<Action> _dispatcherInvoke;

    private string? _repositoryPath;
    private WorkspaceFileNode? _ausgewaehlterKnoten;
    private string? _dateiInhalt;
    private DateibrowserAnsichtsmodus _aktuellerModus = DateibrowserAnsichtsmodus.Standard;
    private CancellationTokenSource? _dateiLadenCts;
    private int _aktuellerAenderungsIndex = -1;

    /// <summary>Wurzelknoten des Arbeitsbaums im Standardmodus.</summary>
    public ObservableCollection<WorkspaceFileNode> Wurzelknoten { get; } = new();

    /// <summary>Commits des aktuellen Branches im Vergleichsmodus.</summary>
    public ObservableCollection<BranchCommit> CommitGruppen { get; } = new();

    /// <summary>Zeilen des aktuell angezeigten Diffs im Vergleichsmodus.</summary>
    public ObservableCollection<TextDiffLine> DiffZeilen { get; } = new();

    /// <summary>Aktuell im Baum ausgewählter Knoten. Das Setzen lädt die Dateivorschau bzw. den Diff.</summary>
    public WorkspaceFileNode? AusgewaehlterKnoten
    {
        get => _ausgewaehlterKnoten;
        set
        {
            if (!SetProperty(ref _ausgewaehlterKnoten, value))
                return;

            _dateiLadenCts?.Cancel();
            _dateiLadenCts?.Dispose();
            _dateiLadenCts = new CancellationTokenSource();
            DateiLadenAsync(value, _dateiLadenCts.Token).SafeFireAndForget(_logger, "FileExplorerViewModel.DateiLadenAsync");
        }
    }

    /// <summary>Inhalt bzw. Hinweistext der aktuell ausgewählten Datei.</summary>
    public string? DateiInhalt
    {
        get => _dateiInhalt;
        private set => SetProperty(ref _dateiInhalt, value);
    }

    /// <summary>Aktuell gewählter Anzeigemodus des Explorers.</summary>
    public DateibrowserAnsichtsmodus AktuellerModus
    {
        get => _aktuellerModus;
        private set => SetProperty(ref _aktuellerModus, value);
    }

    /// <summary>Gibt an, ob für die aktuelle Auswahl ein Diff angezeigt werden soll (statt reinem Dateiinhalt).</summary>
    public bool ZeigtDiffAnsicht => DiffZeilen.Count > 0;

    /// <summary>Wechselt in den Standardmodus und lädt den vollständigen Arbeitsbaum neu.</summary>
    public ICommand StandardAnsichtCommand { get; }

    /// <summary>Wechselt in den Vergleichsmodus und lädt die Branch-Commits.</summary>
    public ICommand VergleichCommand { get; }

    /// <summary>Lädt den aktuellen Modus (Baum bzw. Commits) neu.</summary>
    public ICommand AktualisierenCommand { get; }

    /// <summary>Springt im Diff zur nächsten Änderung (Added/Removed/Modified-Block).</summary>
    public ICommand NaechsteAenderungCommand { get; }

    /// <summary>Springt im Diff zur vorherigen Änderung (Added/Removed/Modified-Block).</summary>
    public ICommand VorherigeAenderungCommand { get; }

    /// <summary>Öffnet die aktuell angezeigte Datei mit der Standardanwendung des Betriebssystems.</summary>
    public ICommand DateiMitStandardanwendungOeffnenCommand { get; }

    /// <summary>Wird ausgelöst, wenn per Navigation zu einer Änderung gesprungen wurde, mit dem Index der Zielzeile in <see cref="DiffZeilen"/>.</summary>
    public event Action<int>? DiffZeileFokussiert;

    /// <summary>Gibt an, ob die aktuell im Baum ausgewählte Datei mit der Standardanwendung geöffnet werden kann (kein
    /// Verzeichnis, nicht gelöscht, kein Commit-Knoten aus dem Vergleichsmodus – nur dann stimmt die angezeigte
    /// Vorschau mit der Datei auf der Festplatte überein).</summary>
    private bool KannAktuelleDateiOeffnen => _ausgewaehlterKnoten is { IsDirectory: false, IsDeleted: false, CommitSha: null };

    /// <inheritdoc cref="FileExplorerViewModel"/>
    public FileExplorerViewModel(
        IGitWorkspaceBrowserService gitWorkspaceBrowserService,
        ITextDiffService textDiffService,
        ILogger<FileExplorerViewModel> logger,
        Action<Action>? dispatcherInvoke = null)
    {
        _gitWorkspaceBrowserService = gitWorkspaceBrowserService;
        _textDiffService = textDiffService;
        _logger = logger;
        _dispatcherInvoke = DispatcherInvokeFactory.Create(dispatcherInvoke);

        StandardAnsichtCommand = new AsyncRelayCommand(StandardAnsichtAsync);
        VergleichCommand = new AsyncRelayCommand(VergleichAsync);
        AktualisierenCommand = new AsyncRelayCommand(AktualisierenAsync);
        NaechsteAenderungCommand = new RelayCommand(() => AenderungNavigieren(vorwaerts: true), () => ZeigtDiffAnsicht);
        VorherigeAenderungCommand = new RelayCommand(() => AenderungNavigieren(vorwaerts: false), () => ZeigtDiffAnsicht);
        DateiMitStandardanwendungOeffnenCommand = new RelayCommand(DateiMitStandardanwendungOeffnen, () => KannAktuelleDateiOeffnen);
    }

    /// <summary>Setzt das Repository-Verzeichnis, wechselt in den Standardmodus und lädt den Arbeitsbaum.</summary>
    /// <param name="repositoryPath">Pfad des geklonten Repositories, oder <c>null</c>/leer wenn noch kein Repository vorhanden ist.</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task InitialisierenAsync(string? repositoryPath, CancellationToken ct = default)
    {
        _repositoryPath = repositoryPath;
        AktuellerModus = DateibrowserAnsichtsmodus.Standard;
        _dateiLadenCts?.Cancel();
        _dateiLadenCts?.Dispose();
        _dateiLadenCts = null;
        _ausgewaehlterKnoten = null;
        OnPropertyChanged(nameof(AusgewaehlterKnoten));
        DateiInhalt = null;
        ClearDiffZeilen();

        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            Wurzelknoten.Clear();
            CommitGruppen.Clear();
            return;
        }

        await LadeArbeitsbaumAsync(ct);
    }

    /// <summary>Lädt die geänderten Dateien eines Commits nach, sofern sie noch nicht geladen wurden.</summary>
    /// <param name="commit">Der aufzuklappende Commit-Knoten.</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task CommitAufklappenAsync(BranchCommit commit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commit);

        if (string.IsNullOrWhiteSpace(_repositoryPath) || commit.ChildrenLoaded)
            return;

        try
        {
            commit.IsLoadingFiles = true;
            var files = await _gitWorkspaceBrowserService.LoadCommitFilesAsync(_repositoryPath, commit.Sha, ct);

            _dispatcherInvoke(() =>
            {
                commit.Files.ReplaceAll(files);
                commit.ChildrenLoaded = true;
                commit.IsLoadingFiles = false;
            });
        }
        catch (OperationCanceledException)
        {
            commit.IsLoadingFiles = false;
            throw;
        }
        catch (Exception ex)
        {
            commit.IsLoadingFiles = false;
            commit.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Dateien für Commit {CommitSha} konnten nicht geladen werden.", commit.Sha);
        }
    }

    private async Task DateiLadenAsync(WorkspaceFileNode? knoten, CancellationToken ct)
    {
        if (knoten is null || knoten.IsDirectory)
        {
            DateiInhalt = null;
            ClearDiffZeilen();
            return;
        }

        if (string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var preview = knoten.CommitSha is not null
                ? await _gitWorkspaceBrowserService.LoadCommitPreviewAsync(_repositoryPath, knoten, ct)
                : await _gitWorkspaceBrowserService.LoadPreviewAsync(_repositoryPath, knoten, ct);

            _dispatcherInvoke(() =>
            {
                if (preview.IsBinary || preview.IsTooBig)
                {
                    DateiInhalt = preview.Hint;
                    ClearDiffZeilen();
                    return;
                }

                if (knoten.CommitSha is not null)
                {
                    DateiInhalt = preview.CurrentContent;
                    var diff = _textDiffService.BuildDiff(preview.OriginalContent, preview.CurrentContent);
                    SetDiffZeilen(diff.Lines);
                }
                else
                {
                    DateiInhalt = preview.CurrentContent;
                    ClearDiffZeilen();
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dateivorschau für {RelativePath} konnte nicht geladen werden.", knoten.RelativePath);
            _dispatcherInvoke(() =>
            {
                DateiInhalt = "Datei konnte nicht geladen werden.";
                ClearDiffZeilen();
            });
        }
    }

    private async Task StandardAnsichtAsync(CancellationToken ct)
    {
        AktuellerModus = DateibrowserAnsichtsmodus.Standard;
        await LadeArbeitsbaumAsync(ct);
    }

    private async Task VergleichAsync(CancellationToken ct)
    {
        AktuellerModus = DateibrowserAnsichtsmodus.Vergleich;
        await LadeCommitsAsync(ct);
    }

    private async Task AktualisierenAsync(CancellationToken ct)
    {
        Wurzelknoten.Clear();
        CommitGruppen.Clear();
        DateiInhalt = null;
        ClearDiffZeilen();
        _dateiLadenCts?.Cancel();
        _dateiLadenCts?.Dispose();
        _dateiLadenCts = null;
        _ausgewaehlterKnoten = null;
        OnPropertyChanged(nameof(AusgewaehlterKnoten));

        if (AktuellerModus == DateibrowserAnsichtsmodus.Vergleich)
            await LadeCommitsAsync(ct);
        else
            await LadeArbeitsbaumAsync(ct);
    }

    private async Task LadeArbeitsbaumAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var nodes = await _gitWorkspaceBrowserService.LoadWorkingTreeAsync(_repositoryPath, ct);
            _dispatcherInvoke(() =>
            {
                Wurzelknoten.Clear();
                foreach (var node in nodes)
                    Wurzelknoten.Add(node);
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Arbeitsbaum für {RepositoryPath} konnte nicht geladen werden.", _repositoryPath);
        }
    }

    private async Task LadeCommitsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var snapshot = await _gitWorkspaceBrowserService.LoadSnapshotAsync(_repositoryPath, ct);
            _dispatcherInvoke(() =>
            {
                CommitGruppen.Clear();
                foreach (var commit in snapshot.BranchCommits)
                    CommitGruppen.Add(commit);
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Commits für {RepositoryPath} konnten nicht geladen werden.", _repositoryPath);
        }
    }

    private void SetDiffZeilen(IEnumerable<TextDiffLine> zeilen)
    {
        DiffZeilen.Clear();
        foreach (var zeile in zeilen)
            DiffZeilen.Add(zeile);
        _aktuellerAenderungsIndex = -1;
        OnPropertyChanged(nameof(ZeigtDiffAnsicht));
    }

    private void ClearDiffZeilen()
    {
        _aktuellerAenderungsIndex = -1;

        if (DiffZeilen.Count == 0)
            return;

        DiffZeilen.Clear();
        OnPropertyChanged(nameof(ZeigtDiffAnsicht));
    }

    private void AenderungNavigieren(bool vorwaerts)
    {
        var blockStartIndizes = ErmittleAenderungsBlockStartIndizes();
        if (blockStartIndizes.Count == 0)
            return;

        var aktuellePosition = blockStartIndizes.IndexOf(_aktuellerAenderungsIndex);
        int neuePosition;
        if (aktuellePosition < 0)
        {
            neuePosition = vorwaerts ? 0 : blockStartIndizes.Count - 1;
        }
        else
        {
            neuePosition = vorwaerts
                ? (aktuellePosition + 1) % blockStartIndizes.Count
                : (aktuellePosition - 1 + blockStartIndizes.Count) % blockStartIndizes.Count;
        }

        _aktuellerAenderungsIndex = blockStartIndizes[neuePosition];
        DiffZeileFokussiert?.Invoke(_aktuellerAenderungsIndex);
    }

    private void DateiMitStandardanwendungOeffnen()
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath) || !KannAktuelleDateiOeffnen)
            return;

        var knoten = _ausgewaehlterKnoten!;

        try
        {
            var vollstaendigerPfad = Path.GetFullPath(Path.Combine(_repositoryPath, knoten.RelativePath));
            Process.Start(new ProcessStartInfo
            {
                FileName = vollstaendigerPfad,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Datei {RelativePath} konnte nicht mit der Standardanwendung geöffnet werden.", knoten.RelativePath);
        }
    }

    private List<int> ErmittleAenderungsBlockStartIndizes()
    {
        var indizes = new List<int>();
        var inBlock = false;
        for (var i = 0; i < DiffZeilen.Count; i++)
        {
            var istAenderung = DiffZeilen[i].Status != DiffLineStatus.Context;
            if (istAenderung && !inBlock)
            {
                indizes.Add(i);
                inBlock = true;
            }
            else if (!istAenderung)
            {
                inBlock = false;
            }
        }

        return indizes;
    }
}
