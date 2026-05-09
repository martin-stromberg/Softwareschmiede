namespace Softwareschmiede.Components.Pages;

using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

public partial class AgentenpaketeSeite
{
    [Inject] private IAgentPackageService AgentPackageService { get; set; } = null!;
    [Inject] private IAgentPackageFileService AgentPackageFileService { get; set; } = null!;

    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private bool _loading = true;
    private List<FileTreeNode> _packageRoots = [];
    private FileTreeNode? _selectedNode;

    // File content
    private string _editContent = string.Empty;
    private string _markdownHtml = string.Empty;
    private bool _isMarkdownFile;
    private bool _isEditing;
    private bool _isSaving;
    private bool _loadingFile;

    // Messages
    private string? _successMessage;
    private string? _errorMessage;
    private string? _formError;

    // UI state for forms
    private bool _showCreatePackage;
    private bool _showRename;
    private bool _showDeleteConfirm;
    private bool _showCreateDir;
    private bool _showCreateFile;
    private bool _showUpload;
    private bool _uploading;

    // Form inputs
    private string _newPackageName = string.Empty;
    private string _renameInput = string.Empty;
    private string _newItemName = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadPackagesAsync();
        _loading = false;
    }

    private async Task LoadPackagesAsync()
    {
        var packages = await AgentPackageService.GetPackagesAsync();
        _packageRoots = packages
            .Select(p => new FileTreeNode
            {
                Name = p.Name,
                RelativePath = string.Empty,
                PackageName = p.Name,
                IsDirectory = true,
                IsExpanded = false
            })
            .OrderBy(n => n.Name)
            .ToList();
    }

    private IEnumerable<(FileTreeNode Node, int Depth)> FlattenedTree
    {
        get
        {
            foreach (var root in _packageRoots)
            {
                yield return (root, 0);
                if (root.IsExpanded)
                {
                    foreach (var item in FlattenNode(root, 1))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private static IEnumerable<(FileTreeNode Node, int Depth)> FlattenNode(FileTreeNode node, int depth)
    {
        foreach (var child in node.Children)
        {
            yield return (child, depth);
            if (child.IsDirectory && child.IsExpanded)
            {
                foreach (var item in FlattenNode(child, depth + 1))
                {
                    yield return item;
                }
            }
        }
    }

    private bool IsSelected(FileTreeNode node) =>
        _selectedNode is not null &&
        _selectedNode.PackageName == node.PackageName &&
        _selectedNode.RelativePath == node.RelativePath &&
        _selectedNode.IsDirectory == node.IsDirectory;

    private async Task SelectNodeAsync(FileTreeNode node)
    {
        ResetFormStates();
        _selectedNode = node;

        if (!node.IsDirectory)
        {
            await LoadFileContentAsync(node);
        }
        else if (node.IsPackageRoot && !node.IsExpanded)
        {
            await ExpandNodeAsync(node);
        }
    }

    private async Task ToggleExpandAsync(FileTreeNode node)
    {
        if (!node.IsExpanded && node.Children.Count == 0)
        {
            await ExpandNodeAsync(node);
        }
        else
        {
            node.IsExpanded = !node.IsExpanded;
        }
    }

    private async Task ExpandNodeAsync(FileTreeNode node)
    {
        try
        {
            var tree = await AgentPackageFileService.BuildPackageTreeAsync(node.PackageName);
            if (node.IsPackageRoot)
            {
                node.Children.Clear();
                node.Children.AddRange(tree.Children);
            }
            else
            {
                var subTree = FindNode(tree, node.RelativePath);
                if (subTree is not null)
                {
                    node.Children.Clear();
                    node.Children.AddRange(subTree.Children);
                }
            }
            node.IsExpanded = true;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Fehler beim Laden: {ex.Message}";
        }
    }

    private static FileTreeNode? FindNode(FileTreeNode root, string relativePath)
    {
        if (relativePath == string.Empty || root.RelativePath == relativePath)
        {
            return root;
        }
        foreach (var child in root.Children)
        {
            var found = FindNode(child, relativePath);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private async Task LoadFileContentAsync(FileTreeNode node)
    {
        _loadingFile = true;
        _editContent = string.Empty;
        _markdownHtml = string.Empty;
        _isMarkdownFile = node.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        _isEditing = !_isMarkdownFile;

        try
        {
            var content = await AgentPackageFileService.ReadFileAsync(node.PackageName, node.RelativePath);
            _editContent = content;

            if (_isMarkdownFile)
            {
                _markdownHtml = Markdown.ToHtml(content, MarkdownPipeline);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Datei konnte nicht geladen werden: {ex.Message}";
        }
        finally
        {
            _loadingFile = false;
        }
    }

    private void SwitchToEdit()
    {
        _isEditing = true;
    }

    private void SwitchToPreview()
    {
        _isEditing = false;
        _markdownHtml = Markdown.ToHtml(_editContent, MarkdownPipeline);
    }

    private async Task SaveFileAsync()
    {
        if (_selectedNode is null) { return; }

        _isSaving = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await AgentPackageFileService.WriteFileAsync(_selectedNode.PackageName, _selectedNode.RelativePath, _editContent);
            _successMessage = "Datei gespeichert.";

            if (_isMarkdownFile && !_isEditing)
            {
                _markdownHtml = Markdown.ToHtml(_editContent, MarkdownPipeline);
            }

            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Fehler beim Speichern: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    // --- Create Package ---
    private async Task CreatePackageAsync()
    {
        _formError = null;
        if (string.IsNullOrWhiteSpace(_newPackageName))
        {
            _formError = "Paketname darf nicht leer sein.";
            return;
        }

        try
        {
            await AgentPackageFileService.CreatePackageAsync(_newPackageName.Trim());
            _showCreatePackage = false;
            var createdName = _newPackageName.Trim();
            _newPackageName = string.Empty;
            await LoadPackagesAsync();
            _successMessage = $"Paket '{createdName}' wurde erstellt.";
            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _formError = ex.Message;
        }
    }

    private async Task OnNewPackageKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CreatePackageAsync();
        }
    }

    // --- Create Directory ---
    private async Task CreateDirectoryAsync()
    {
        _formError = null;
        if (_selectedNode is null || string.IsNullOrWhiteSpace(_newItemName))
        {
            _formError = "Verzeichnisname darf nicht leer sein.";
            return;
        }

        try
        {
            var newRelPath = _selectedNode.RelativePath.Length > 0
                ? $"{_selectedNode.RelativePath}/{_newItemName.Trim()}"
                : _newItemName.Trim();

            await AgentPackageFileService.CreateDirectoryAsync(_selectedNode.PackageName, newRelPath);
            _showCreateDir = false;
            _newItemName = string.Empty;
            await RefreshPackageTreeAsync(_selectedNode.PackageName);
            _successMessage = "Verzeichnis erstellt.";
            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _formError = ex.Message;
        }
    }

    // --- Create Empty File ---
    private async Task CreateEmptyFileAsync()
    {
        _formError = null;
        if (_selectedNode is null || string.IsNullOrWhiteSpace(_newItemName))
        {
            _formError = "Dateiname darf nicht leer sein.";
            return;
        }

        try
        {
            var newRelPath = _selectedNode.RelativePath.Length > 0
                ? $"{_selectedNode.RelativePath}/{_newItemName.Trim()}"
                : _newItemName.Trim();

            await AgentPackageFileService.CreateEmptyFileAsync(_selectedNode.PackageName, newRelPath);
            _showCreateFile = false;
            _newItemName = string.Empty;
            await RefreshPackageTreeAsync(_selectedNode.PackageName);
            _successMessage = "Datei erstellt.";
            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _formError = ex.Message;
        }
    }

    // --- Upload File ---
    private async Task HandleFileUpload(InputFileChangeEventArgs e)
    {
        if (_selectedNode is null) { return; }

        _uploading = true;
        _formError = null;

        try
        {
            foreach (var file in e.GetMultipleFiles(10))
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                await AgentPackageFileService.UploadFileAsync(
                    _selectedNode.PackageName,
                    _selectedNode.RelativePath,
                    file.Name,
                    stream);
            }

            _showUpload = false;
            await RefreshPackageTreeAsync(_selectedNode.PackageName);
            _successMessage = "Datei(en) hochgeladen.";
            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _formError = $"Upload fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            _uploading = false;
        }
    }

    // --- Rename ---
    private void ShowRename()
    {
        if (_selectedNode is null) { return; }
        _renameInput = _selectedNode.Name;
        _showRename = true;
        _formError = null;
    }

    private void CancelRename()
    {
        _showRename = false;
        _renameInput = string.Empty;
        _formError = null;
    }

    private async Task ExecuteRenameAsync()
    {
        _formError = null;
        if (_selectedNode is null || string.IsNullOrWhiteSpace(_renameInput))
        {
            _formError = "Neuer Name darf nicht leer sein.";
            return;
        }

        var newName = _renameInput.Trim();
        if (newName == _selectedNode.Name)
        {
            CancelRename();
            return;
        }

        try
        {
            if (_selectedNode.IsPackageRoot)
            {
                var oldName = _selectedNode.PackageName;
                await AgentPackageFileService.RenamePackageAsync(oldName, newName);
                await LoadPackagesAsync();
                _selectedNode = _packageRoots.FirstOrDefault(r => r.Name == newName);
            }
            else if (_selectedNode.IsDirectory)
            {
                await AgentPackageFileService.RenameDirectoryAsync(_selectedNode.PackageName, _selectedNode.RelativePath, newName);
                await RefreshPackageTreeAndReselectAsync(_selectedNode.PackageName, _selectedNode.RelativePath, newName, true);
            }
            else
            {
                await AgentPackageFileService.RenameFileAsync(_selectedNode.PackageName, _selectedNode.RelativePath, newName);
                await RefreshPackageTreeAndReselectAsync(_selectedNode.PackageName, _selectedNode.RelativePath, newName, false);
            }

            _showRename = false;
            _renameInput = string.Empty;
            _successMessage = "Umbenannt.";
            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _formError = ex.Message;
        }
    }

    // --- Delete ---
    private async Task ExecuteDeleteAsync()
    {
        if (_selectedNode is null) { return; }

        var packageName = _selectedNode.PackageName;

        try
        {
            if (_selectedNode.IsPackageRoot)
            {
                await AgentPackageFileService.DeletePackageAsync(packageName);
                _selectedNode = null;
                await LoadPackagesAsync();
            }
            else if (_selectedNode.IsDirectory)
            {
                await AgentPackageFileService.DeleteDirectoryAsync(packageName, _selectedNode.RelativePath);
                _selectedNode = null;
                await RefreshPackageTreeAsync(packageName);
            }
            else
            {
                await AgentPackageFileService.DeleteFileAsync(packageName, _selectedNode.RelativePath);
                _selectedNode = null;
                await RefreshPackageTreeAsync(packageName);
            }

            _showDeleteConfirm = false;
            _successMessage = "Gelöscht.";
            await ClearSuccessAfterDelayAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Fehler beim Löschen: {ex.Message}";
            _showDeleteConfirm = false;
        }
    }

    // --- Tree refresh helpers ---
    private async Task RefreshPackageTreeAsync(string packageName)
    {
        var root = _packageRoots.FirstOrDefault(r => r.PackageName == packageName);
        if (root is null) { return; }

        if (!root.IsExpanded) { return; }

        try
        {
            var tree = await AgentPackageFileService.BuildPackageTreeAsync(packageName);
            root.Children.Clear();
            root.Children.AddRange(tree.Children);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Fehler beim Aktualisieren: {ex.Message}";
        }
    }

    private async Task RefreshPackageTreeAndReselectAsync(string packageName, string oldRelPath, string newName, bool isDirectory)
    {
        var parentPath = Path.GetDirectoryName(oldRelPath)?.Replace('\\', '/') ?? string.Empty;
        var newRelPath = parentPath.Length > 0 ? $"{parentPath}/{newName}" : newName;

        await RefreshPackageTreeAsync(packageName);

        var root = _packageRoots.FirstOrDefault(r => r.PackageName == packageName);
        if (root is not null)
        {
            _selectedNode = FindNode(root, newRelPath);
            if (_selectedNode is not null && !isDirectory)
            {
                await LoadFileContentAsync(_selectedNode);
            }
        }
    }

    private void ResetFormStates()
    {
        _showRename = false;
        _showDeleteConfirm = false;
        _showCreateDir = false;
        _showCreateFile = false;
        _showUpload = false;
        _formError = null;
        _errorMessage = null;
    }

    private async Task ClearSuccessAfterDelayAsync()
    {
        await Task.Delay(3000);
        _successMessage = null;
        StateHasChanged();
    }
}
