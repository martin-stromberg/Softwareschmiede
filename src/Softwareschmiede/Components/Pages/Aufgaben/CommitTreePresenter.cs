using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Components.Pages.Aufgaben;

/// <summary>Testbare Zustandslogik für Commit-Knoten im Explorer.</summary>
public sealed class CommitTreePresenter
{
    public async Task ToggleCommitAsync(
        BranchCommit commit,
        Func<BranchCommit, CancellationToken, Task<IReadOnlyList<WorkspaceFileNode>>> loadFilesAsync,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(loadFilesAsync);

        if (commit.IsExpanded)
        {
            commit.IsExpanded = false;
            return;
        }

        commit.IsExpanded = true;
        if (commit.ChildrenLoaded)
        {
            return;
        }

        await LoadCommitFilesAsync(commit, loadFilesAsync, ct);
    }

    public async Task RetryLoadCommitFilesAsync(
        BranchCommit commit,
        Func<BranchCommit, CancellationToken, Task<IReadOnlyList<WorkspaceFileNode>>> loadFilesAsync,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(loadFilesAsync);

        commit.ErrorMessage = null;
        await LoadCommitFilesAsync(commit, loadFilesAsync, ct);
    }

    public static bool RequiresCommitPreview(WorkspaceFileNode node)
        => !string.IsNullOrWhiteSpace(node.CommitSha);

    public IReadOnlyList<WorkspaceNodeRow> GetVisibleCommitRows(BranchCommit commit, int baseDepth = 1)
    {
        ArgumentNullException.ThrowIfNull(commit);

        if (!commit.IsExpanded)
        {
            return [];
        }

        return commit.Files
            .SelectMany(node => FlattenNode(node, baseDepth))
            .ToList();
    }

    private static async Task LoadCommitFilesAsync(
        BranchCommit commit,
        Func<BranchCommit, CancellationToken, Task<IReadOnlyList<WorkspaceFileNode>>> loadFilesAsync,
        CancellationToken ct)
    {
        if (commit.IsLoadingFiles)
        {
            return;
        }

        commit.IsLoadingFiles = true;
        commit.ErrorMessage = null;
        try
        {
            var files = await loadFilesAsync(commit, ct);
            commit.Files = files.ToList();
            commit.ChildrenLoaded = true;
        }
        catch (Exception ex)
        {
            commit.ErrorMessage = ex.Message;
            commit.ChildrenLoaded = false;
            commit.Files = [];
        }
        finally
        {
            commit.IsLoadingFiles = false;
        }
    }

    private static IEnumerable<WorkspaceNodeRow> FlattenNode(WorkspaceFileNode node, int depth)
    {
        yield return new WorkspaceNodeRow(node, depth);

        if (node.IsDirectory && node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                foreach (var row in FlattenNode(child, depth + 1))
                {
                    yield return row;
                }
            }
        }
    }
}
