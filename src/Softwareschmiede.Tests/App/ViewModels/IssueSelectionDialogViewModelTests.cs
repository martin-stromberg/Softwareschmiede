using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für IssueSelectionDialogViewModel.</summary>
public sealed class IssueSelectionDialogViewModelTests
{
    private readonly Mock<IGitPlugin> _gitPluginMock;

    public IssueSelectionDialogViewModelTests()
    {
        _gitPluginMock = new Mock<IGitPlugin>();
    }

    private IssueSelectionDialogViewModel CreateSut()
        => new(_gitPluginMock.Object, NullLogger<IssueSelectionDialogViewModel>.Instance);

    private static Issue ErstelleIssue(int nummer = 1, string titel = "Titel")
        => new(nummer, titel, "Body", [], null, "https://github.com/test/repo/issues/" + nummer);

    /// <summary>LoadAsync füllt VerfuegbareIssues aus dem Plugin.</summary>
    [Fact]
    public async Task LoadAsync_PopulatesVerfuegbareIssues()
    {
        // Arrange
        var issues = new[] { ErstelleIssue(1), ErstelleIssue(2) };
        _gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);

        var sut = CreateSut();

        // Act
        await sut.LoadAsync("owner/repo");

        // Assert
        sut.VerfuegbareIssues.Should().HaveCount(2);
        sut.IsLoading.Should().BeFalse();
    }

    /// <summary>LoadAsync setzt IsLoading zurück auf false, auch wenn GetIssuesAsync eine Exception wirft.</summary>
    [Fact]
    public async Task LoadAsync_HandlesExceptionGracefully()
    {
        // Arrange
        _gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API-Fehler"));

        var sut = CreateSut();

        // Act
        await sut.LoadAsync("owner/repo");

        // Assert
        sut.IsLoading.Should().BeFalse();
        sut.VerfuegbareIssues.Should().BeEmpty();
    }

    /// <summary>Wenn ein Issue selektiert wird, wird KannBestaetigen true.</summary>
    [Fact]
    public void SelectedIssue_UpdatesKannBestaetigen()
    {
        // Arrange
        var sut = CreateSut();
        sut.KannBestaetigen.Should().BeFalse();

        // Act
        sut.SelectedIssue = ErstelleIssue();

        // Assert
        sut.KannBestaetigen.Should().BeTrue();
    }

    /// <summary>BestaetigenCommand löst CloseRequested mit true aus.</summary>
    [Fact]
    public async Task BestaetigenCommand_RaisesCloseRequestedWithTrue()
    {
        // Arrange
        var issues = new[] { ErstelleIssue() };
        _gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);

        var sut = CreateSut();
        await sut.LoadAsync("owner/repo");
        sut.SelectedIssue = sut.VerfuegbareIssues[0];

        bool? closedWith = null;
        sut.CloseRequested += (_, result) => closedWith = result;

        // Act
        sut.BestaetigenCommand.Execute(null);

        // Assert
        closedWith.Should().BeTrue();
    }

    /// <summary>AbbrechenCommand löst CloseRequested mit false aus.</summary>
    [Fact]
    public void AbbrechenCommand_RaisesCloseRequestedWithFalse()
    {
        // Arrange
        var sut = CreateSut();

        bool? closedWith = null;
        sut.CloseRequested += (_, result) => closedWith = result;

        // Act
        sut.AbbrechenCommand.Execute(null);

        // Assert
        closedWith.Should().BeFalse();
    }

    /// <summary>BestaetigenCommand.CanExecute ist false wenn kein Issue gewählt ist.</summary>
    [Fact]
    public void BestaetigenCommand_CanExecuteFalse_WennKeinIssueGewaehlt()
    {
        // Arrange
        var sut = CreateSut();

        // Assert
        sut.BestaetigenCommand.CanExecute(null).Should().BeFalse();
    }
}
