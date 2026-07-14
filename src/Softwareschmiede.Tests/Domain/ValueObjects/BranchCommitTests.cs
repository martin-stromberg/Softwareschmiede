using System.ComponentModel;
using FluentAssertions;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.ValueObjects;

/// <summary>Tests für <see cref="BranchCommit"/>.</summary>
public sealed class BranchCommitTests
{
    /// <summary>Das Setzen von IsLoadingFiles löst PropertyChanged aus, damit WPF-Bindings den Ladezustand anzeigen können.</summary>
    [Fact]
    public void IsLoadingFiles_Setzen_LoestPropertyChangedAus()
    {
        var commit = new BranchCommit();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)commit).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        commit.IsLoadingFiles = true;

        raised.Should().Contain(nameof(BranchCommit.IsLoadingFiles));
    }

    /// <summary>Das Setzen von ErrorMessage löst PropertyChanged aus, damit WPF-Bindings die Fehlermeldung anzeigen können.</summary>
    [Fact]
    public void ErrorMessage_Setzen_LoestPropertyChangedAus()
    {
        var commit = new BranchCommit();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)commit).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        commit.ErrorMessage = "kaputt";

        raised.Should().Contain(nameof(BranchCommit.ErrorMessage));
    }

    /// <summary>Das erneute Setzen desselben Werts löst PropertyChanged nicht erneut aus.</summary>
    [Fact]
    public void IsLoadingFiles_GleicherWert_LoestPropertyChangedNichtErneutAus()
    {
        var commit = new BranchCommit { IsLoadingFiles = true };
        var raised = 0;
        ((INotifyPropertyChanged)commit).PropertyChanged += (_, _) => raised++;

        commit.IsLoadingFiles = true;

        raised.Should().Be(0);
    }
}
