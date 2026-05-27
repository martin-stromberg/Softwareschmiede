using FluentAssertions;
using Softwareschmiede.Components.Pages.Aufgaben;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Components.Pages.Aufgaben;

public sealed class CommitTreePresenterTests
{
    [Fact]
    public async Task ToggleCommitAsync_ShouldLoadChildrenOnce_WhenExpandedFirstTime()
    {
        var presenter = new CommitTreePresenter();
        var commit = new BranchCommit { Sha = "abc" };
        var callCount = 0;

        await presenter.ToggleCommitAsync(commit, (_, _) =>
        {
            callCount++;
            IReadOnlyList<WorkspaceFileNode> nodes =
            [
                new WorkspaceFileNode { Name = "file.cs", RelativePath = "file.cs", CommitSha = "abc" },
            ];
            return Task.FromResult(nodes);
        });

        await presenter.ToggleCommitAsync(commit, (_, _) => Task.FromResult<IReadOnlyList<WorkspaceFileNode>>([]));
        await presenter.ToggleCommitAsync(commit, (_, _) => Task.FromResult<IReadOnlyList<WorkspaceFileNode>>([]));

        callCount.Should().Be(1);
        commit.ChildrenLoaded.Should().BeTrue();
        commit.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task RetryLoadCommitFilesAsync_ShouldSetErrorState_WhenLoaderFails()
    {
        var presenter = new CommitTreePresenter();
        var commit = new BranchCommit { Sha = "abc", IsExpanded = true };

        await presenter.RetryLoadCommitFilesAsync(commit, (_, _) => throw new InvalidOperationException("kaputt"));

        commit.ChildrenLoaded.Should().BeFalse();
        commit.IsLoadingFiles.Should().BeFalse();
        commit.ErrorMessage.Should().Be("kaputt");
    }

    [Fact]
    public void RequiresCommitPreview_ShouldReturnTrue_ForCommitNodes()
    {
        CommitTreePresenter.RequiresCommitPreview(new WorkspaceFileNode { RelativePath = "a.cs", CommitSha = "abc" }).Should().BeTrue();
        CommitTreePresenter.RequiresCommitPreview(new WorkspaceFileNode { RelativePath = "a.cs" }).Should().BeFalse();
    }

    [Fact]
    public void GetVisibleCommitRows_ShouldFlattenExpandedCommitTree()
    {
        var presenter = new CommitTreePresenter();
        var commit = new BranchCommit
        {
            Sha = "abc",
            IsExpanded = true,
            Files =
            [
                new WorkspaceFileNode
                {
                    Name = "src",
                    RelativePath = "src",
                    IsDirectory = true,
                    IsExpanded = true,
                    Children =
                    [
                        new WorkspaceFileNode
                        {
                            Name = "file.cs",
                            RelativePath = "src/file.cs",
                            IsDirectory = false,
                        },
                    ],
                },
            ],
        };

        var rows = presenter.GetVisibleCommitRows(commit);

        rows.Should().HaveCount(2);
        rows[0].Depth.Should().Be(1);
        rows[1].Depth.Should().Be(2);
    }

    [Fact]
    public void GetVisibleCommitRows_ShouldReturnEmpty_WhenCommitIsNotExpanded()
    {
        var presenter = new CommitTreePresenter();
        var commit = new BranchCommit
        {
            Sha = "abc",
            IsExpanded = false,
            Files =
            [
                new WorkspaceFileNode
                {
                    Name = "file.cs",
                    RelativePath = "file.cs",
                    IsDirectory = false,
                },
            ],
        };

        var rows = presenter.GetVisibleCommitRows(commit);

        rows.Should().BeEmpty();
    }
}
