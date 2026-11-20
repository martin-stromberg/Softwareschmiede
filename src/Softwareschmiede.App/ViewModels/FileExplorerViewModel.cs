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
    private const int InitialLoadDepth = 2;

    private readonly IGitWorkspaceBrowserService _gitWorkspaceBrowserService;
    private readonly ITextDiffService _textDiffService;
    private readonly ILogger<FileExplorerViewModel> _logger;
    private readonly Action<Action> _dispatcherInvoke;

    private string? _repositoryPath;
    private WorkspaceFileNode? _selectedNode;
    private string? _fileContent;
    private FileBrowserViewMode _currentMode = FileBrowserViewMode.Default;
    private CancellationTokenSource? _fileLoadCts;
    private int _currentChangeIndex = -1;

    /// <summary>Wurzelknoten des Arbeitsbaums im Standardmodus.</summary>
    public ObservableCollection<WorkspaceFileNode> RootNodes { get; } = new();

    /// <summary>Commits des aktuellen Branches im Vergleichsmodus.</summary>
    public ObservableCollection<BranchCommit> CommitGroup { get; } = new();

    /// <summary>Zeilen des aktuell angezeigten Diffs im Vergleichsmodus.</summary>
    public ObservableCollection<TextDiffLine> DiffLines { get; } = new();

    /// <summary>Aktuell im Baum ausgewählter Knoten. Das Setzen lädt die Dateivorschau bzw. den Diff.</summary>
    public WorkspaceFileNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetProperty(ref _selectedNode, value))
                return;

            _fileLoadCts?.Cancel();
            _fileLoadCts?.Dispose();
            _fileLoadCts = new CancellationTokenSource();
            LoadFileAsync(value, _fileLoadCts.Token).SafeFireAndForget(_logger, "FileExplorerViewModel.LoadFileAsync");
        }
    }

    /// <summary>Inhalt bzw. Hinweistext der aktuell ausgewählten Datei.</summary>
    public string? FileContent
    {
        get => _fileContent;
        private set => SetProperty(ref _fileContent, value);
    }

    /// <summary>Aktuell gewählter Anzeigemodus des Explorers.</summary>
    public FileBrowserViewMode CurrentMode
    {
        get => _currentMode;
        private set => SetProperty(ref _currentMode, value);
    }

    /// <summary>Gibt an, ob für die aktuelle Auswahl ein Diff angezeigt werden soll (statt reinem Dateiinhalt).</summary>
    public bool ShowsDiffView => DiffLines.Count > 0;

    /// <summary>Wechselt in den Standardmodus und lädt den vollständigen Arbeitsbaum neu.</summary>
    public ICommand DefaultViewCommand { get; }

    /// <summary>Wechselt in den Vergleichsmodus und lädt die Branch-Commits.</summary>
    public ICommand CompareCommand { get; }

    /// <summary>Lädt den aktuellen Modus (Baum bzw. Commits) neu.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Springt im Diff zur nächsten Änderung (Added/Removed/Modified-Block).</summary>
    public ICommand NextChangeCommand { get; }

    /// <summary>Springt im Diff zur vorherigen Änderung (Added/Removed/Modified-Block).</summary>
    public ICommand PreviousChangeCommand { get; }

    /// <summary>Öffnet die aktuell angezeigte Datei mit der Standardanwendung des Betriebssystems.</summary>
    public ICommand OpenFileWithDefaultappCommand { get; }

    /// <summary>Wird ausgelöst, wenn per Navigation zu einer Änderung gesprungen wurde, mit dem Index der Zielzeile in <see cref="DiffLines"/>.</summary>
    public event Action<int>? DiffLineFocused;

    /// <summary>Gibt an, ob die aktuell im Baum ausgewählte Datei mit der Standardanwendung geöffnet werden kann (kein
    /// Verzeichnis, nicht gelöscht, kein Commit-Knoten aus dem Vergleichsmodus – nur dann stimmt die angezeigte
    /// Vorschau mit der Datei auf der Festplatte überein).</summary>
    private bool OpenCanCurrentFile => _selectedNode is { IsDirectory: false, IsDeleted: false, CommitSha: null };

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

        DefaultViewCommand = new AsyncRelayCommand(DefaultViewAsync);
        CompareCommand = new AsyncRelayCommand(CompareAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        NextChangeCommand = new RelayCommand(() => NavigateChange(vorwaerts: true), () => ShowsDiffView);
        PreviousChangeCommand = new RelayCommand(() => NavigateChange(vorwaerts: false), () => ShowsDiffView);
        OpenFileWithDefaultappCommand = new RelayCommand(OpenFileWithDefaultapp, () => OpenCanCurrentFile);
    }

    /// <summary>Setzt das Repository-Verzeichnis, wechselt in den Standardmodus und lädt den Arbeitsbaum.</summary>
    /// <param name="repositoryPath">Pfad des geklonten Repositories, oder <c>null</c>/leer wenn noch kein Repository vorhanden ist.</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task InitializeAsync(string? repositoryPath, CancellationToken ct = default)
    {
        _repositoryPath = repositoryPath;
        CurrentMode = FileBrowserViewMode.Default;
        ResetSelectionAndLadevorgang();

        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            RootNodes.Clear();
            CommitGroup.Clear();
            return;
        }

        await LoadWorkingTreeAsync(ct);
    }

    /// <summary>Lädt die geänderten Dateien eines Commits nach, sofern sie noch nicht geladen wurden.</summary>
    /// <param name="commit">Der aufzuklappende Commit-Knoten.</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task ExpandCommitAsync(BranchCommit commit, CancellationToken ct = default)
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

    /// <summary>Lädt die Kinder eines Verzeichnisknotens nach, sofern sie noch nicht geladen wurden.</summary>
    /// <param name="node">Der aufzuklappende Verzeichnisknoten.</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task LoadChildrenAsync(WorkspaceFileNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!node.IsDirectory || node.ChildrenLoaded || string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var kinder = await _gitWorkspaceBrowserService.LoadSubtreeAsync(_repositoryPath, node.RelativePath, node.Depth + 1, ct);

            _dispatcherInvoke(() =>
            {
                node.Children.ReplaceAll(kinder);
                node.ChildrenLoaded = true;
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kinder für {RelativePath} konnten nicht geladen werden.", node.RelativePath);
        }
    }

    /// <summary>Räumt beim Zuklappen eines Verzeichnisknotens dessen Groß-Enkel-Knoten auf, sodass pro Verzeichnis stets eine Ebene mehr geladen als sichtbar ist.</summary>
    /// <param name="node">Der zugeklappte Verzeichnisknoten.</param>
    public void CleanNode(WorkspaceFileNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        foreach (var kind in node.Children)
        {
            if (kind.IsDirectory && kind.ChildrenLoaded)
            {
                kind.Children.ReplaceAll([WorkspaceFileNode.CreatePlaceholder(kind.Depth + 1)]);
                kind.ChildrenLoaded = false;
            }
        }
    }

    private async Task LoadFileAsync(WorkspaceFileNode? node, CancellationToken ct)
    {
        if (node is null || node.IsDirectory || node.IsPlaceholder)
        {
            FileContent = null;
            ClearDiffLines();
            return;
        }

        if (string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var preview = node.CommitSha is not null
                ? await _gitWorkspaceBrowserService.LoadCommitPreviewAsync(_repositoryPath, node, ct)
                : await _gitWorkspaceBrowserService.LoadPreviewAsync(_repositoryPath, node, ct);

            _dispatcherInvoke(() =>
            {
                if (preview.IsBinary || preview.IsTooBig)
                {
                    FileContent = preview.Hint;
                    ClearDiffLines();
                    return;
                }

                if (node.CommitSha is not null)
                {
                    FileContent = preview.CurrentContent;
                    var diff = _textDiffService.BuildDiff(preview.OriginalContent, preview.CurrentContent);
                    SetDiffLines(diff.Lines);
                }
                else
                {
                    FileContent = preview.CurrentContent;
                    ClearDiffLines();
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dateivorschau für {RelativePath} konnte nicht geladen werden.", node.RelativePath);
            _dispatcherInvoke(() =>
            {
                FileContent = "Datei konnte nicht geladen werden.";
                ClearDiffLines();
            });
        }
    }

    private async Task DefaultViewAsync(CancellationToken ct)
    {
        CurrentMode = FileBrowserViewMode.Default;
        await LoadWorkingTreeAsync(ct);
    }

    private async Task CompareAsync(CancellationToken ct)
    {
        CurrentMode = FileBrowserViewMode.Compare;
        await LoadCommitsAsync(ct);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        RootNodes.Clear();
        CommitGroup.Clear();
        ResetSelectionAndLadevorgang();

        if (CurrentMode == FileBrowserViewMode.Compare)
            await LoadCommitsAsync(ct);
        else
            await LoadWorkingTreeAsync(ct);
    }

    private async Task LoadWorkingTreeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var nodes = await _gitWorkspaceBrowserService.LoadWorkingTreeAsync(_repositoryPath, InitialLoadDepth, ct);
            _dispatcherInvoke(() =>
            {
                RootNodes.Clear();
                foreach (var node in nodes)
                    RootNodes.Add(node);
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

    private async Task LoadCommitsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath))
            return;

        try
        {
            var snapshot = await _gitWorkspaceBrowserService.LoadSnapshotAsync(_repositoryPath, ct);
            _dispatcherInvoke(() =>
            {
                CommitGroup.Clear();
                foreach (var commit in snapshot.BranchCommits)
                    CommitGroup.Add(commit);
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

    private void SetDiffLines(IEnumerable<TextDiffLine> lines)
    {
        DiffLines.Clear();
        foreach (var line in lines)
            DiffLines.Add(line);
        _currentChangeIndex = -1;
        OnPropertyChanged(nameof(ShowsDiffView));
    }

    private void ClearDiffLines()
    {
        _currentChangeIndex = -1;

        if (DiffLines.Count == 0)
            return;

        DiffLines.Clear();
        OnPropertyChanged(nameof(ShowsDiffView));
    }

    private void NavigateChange(bool vorwaerts)
    {
        var blockStartIndizes = ResolveChangeBlockStartIndizes();
        if (blockStartIndizes.Count == 0)
            return;

        var currentPosition = blockStartIndizes.IndexOf(_currentChangeIndex);
        int newPosition;
        if (currentPosition < 0)
        {
            newPosition = vorwaerts ? 0 : blockStartIndizes.Count - 1;
        }
        else
        {
            newPosition = vorwaerts
                ? (currentPosition + 1) % blockStartIndizes.Count
                : (currentPosition - 1 + blockStartIndizes.Count) % blockStartIndizes.Count;
        }

        _currentChangeIndex = blockStartIndizes[newPosition];
        DiffLineFocused?.Invoke(_currentChangeIndex);
    }

    private void OpenFileWithDefaultapp()
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath) || !OpenCanCurrentFile)
            return;

        var node = _selectedNode!;

        try
        {
            var completePath = Path.GetFullPath(Path.Combine(_repositoryPath, node.RelativePath));
            Process.Start(new ProcessStartInfo
            {
                FileName = completePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Datei {RelativePath} konnte nicht mit der Standardanwendung geöffnet werden.", node.RelativePath);
        }
    }

    private void ResetSelectionAndLadevorgang()
    {
        _fileLoadCts?.Cancel();
        _fileLoadCts?.Dispose();
        _fileLoadCts = null;
        _selectedNode = null;
        OnPropertyChanged(nameof(SelectedNode));
        FileContent = null;
        ClearDiffLines();
    }

    private List<int> ResolveChangeBlockStartIndizes()
    {
        var indizes = new List<int>();
        var inBlock = false;
        for (var i = 0; i < DiffLines.Count; i++)
        {
            var isChange = DiffLines[i].Status != DiffLineStatus.Context;
            if (isChange && !inBlock)
            {
                indizes.Add(i);
                inBlock = true;
            }
            else if (!isChange)
            {
                inBlock = false;
            }
        }

        return indizes;
    }
}
