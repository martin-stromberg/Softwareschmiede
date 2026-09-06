using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>E2E-Tests für den Export der CLI-Rohausgabe aus der TaskDetailView.</summary>
public partial class End2EndTest
{
    /// <summary>CLI-Rohausgabe kann über den Save-Dialog als *.raw-Datei exportiert werden.</summary>
    protected void CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        SetupProjectMitNeuerAufgabe(mainWindow, "CliRawExport-Happy-Repo", "CliRawExport-Happy-Projekt");

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();
        taskDetail.SwitchPanel("InfoCliToggle");
        taskDetail.WaitForLogEntry("CliOutput");
        taskDetail.SwitchPanel("CliViewButton");

        var exportPfad = Path.Combine(Path.GetTempPath(), $"cli-raw-export-{Guid.NewGuid():N}.raw");

        try
        {
            taskDetail.ExportCliRaw(exportPfad);

            var deadline = DateTime.UtcNow + Medium;
            while (DateTime.UtcNow < deadline && !File.Exists(exportPfad))
                Thread.Sleep(200);

            File.Exists(exportPfad).Should().BeTrue();
            File.ReadAllText(exportPfad).Should().NotBeNullOrWhiteSpace();
            new ErrorView(mainWindow).IsVisible.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(exportPfad))
                File.Delete(exportPfad);

            taskDetail.ForceClose(recurseToDashboard: false);
            var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
            projectDetail.DeleteProject();
        }
    }

    /// <summary>Abbruch des Save-Dialogs erzeugt keine Datei und zeigt keinen Fehlerbanner.</summary>
    protected void CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        SetupProjectMitNeuerAufgabe(mainWindow, "CliRawExport-Cancel-Repo", "CliRawExport-Cancel-Projekt");

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();
        taskDetail.SwitchPanel("CliViewButton");

        var exportPfad = Path.Combine(Path.GetTempPath(), $"cli-raw-export-cancel-{Guid.NewGuid():N}.raw");

        try
        {
            taskDetail.ExportCliRaw(null);

            File.Exists(exportPfad).Should().BeFalse();
            new ErrorView(mainWindow).IsVisible.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(exportPfad))
                File.Delete(exportPfad);

            taskDetail.ForceClose(recurseToDashboard: false);
            var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
            projectDetail.DeleteProject();
        }
    }
}
