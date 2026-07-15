using FluentAssertions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdateProgressViewModel"/>.</summary>
public sealed class UpdateProgressViewModelTests
{
    /// <summary>Fortschrittsmeldungen aktualisieren Phase, Prozentwert und indeterminaten Zustand.</summary>
    [Fact]
    public void Apply_ShouldUpdateProgressState()
    {
        var sut = new UpdateProgressViewModel();

        sut.Apply(new UpdatePreparationProgress(UpdatePreparationPhase.Download, 42, "Lädt"));

        sut.PhaseText.Should().Be("Download");
        sut.Percent.Should().Be(42);
        sut.IsIndeterminate.Should().BeFalse();
        sut.Message.Should().Be("Lädt");
    }

    /// <summary>Fehler erlauben das Schließen des Dialogs.</summary>
    [Fact]
    public void SetError_ShouldEnableClose()
    {
        var sut = new UpdateProgressViewModel();

        sut.SetError("Fehler");

        sut.HasError.Should().BeTrue();
        sut.CanClose.Should().BeTrue();
        sut.Message.Should().Be("Fehler");
    }

    /// <summary>Der Abbrechen-Command triggert die angebundene Cancellation und deaktiviert sich.</summary>
    [Fact]
    public void CancelCommand_ShouldInvokeCancellationAndDisableCancel()
    {
        var canceled = false;
        var sut = new UpdateProgressViewModel(() => canceled = true);

        sut.CancelCommand.Execute(null);

        canceled.Should().BeTrue();
        sut.CanCancel.Should().BeFalse();
        sut.Message.Should().Be("Update-Vorbereitung wird abgebrochen.");
    }

    /// <summary>Beim Start des externen Updaters darf der Fortschrittsdialog den App-Shutdown nicht blockieren.</summary>
    [Fact]
    public void MarkUpdaterStarting_ShouldEnableCloseAndDisableCancel()
    {
        var sut = new UpdateProgressViewModel();

        sut.MarkUpdaterStarting();

        sut.CanCancel.Should().BeFalse();
        sut.CanClose.Should().BeTrue();
        sut.Message.Should().Be("Update wird gestartet. Die Anwendung wird beendet.");
    }
}
