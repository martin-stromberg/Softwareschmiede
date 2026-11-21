using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>E2E-Tests für den Export der CLI-Rohausgabe aus der TaskDetailView.</summary>
public partial class End2EndTest
{
    /// <summary>CLI-Rohausgabe kann über den Save-Dialog als *.raw-Datei exportiert werden.</summary>
    protected void CliRawExport_CreatesRawFileWithCliOutput_HappyPath_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        const string projectName = "CliRawExport-Happy-Projekt";
        SetupProjectWithNewWorkItem(mainWindow, "CliRawExport-Happy-Repo", projectName);
        var expectedCliLine = $"CliRawExport-{Guid.NewGuid():N}";
        SeedCliOutputForCurrentWorkItem(expectedCliLine);

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", forProjectUse: false);
        taskDetail.WaitForCliRunning();
        taskDetail.SwitchPanel("CliViewButton");

        var exportPath = Path.Combine(Path.GetTempPath(), $"cli-raw-export-{Guid.NewGuid():N}.raw");

        try
        {
            taskDetail.ExportCliRaw(exportPath);

            var deadline = DateTime.UtcNow + Medium;
            while (DateTime.UtcNow < deadline && !File.Exists(exportPath))
                Thread.Sleep(200);

            File.Exists(exportPath).Should().BeTrue();
            File.ReadAllText(exportPath).Should().Contain(expectedCliLine);
            new ErrorView(mainWindow).IsVisible.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(exportPath))
                File.Delete(exportPath);

            TryCloseTaskDetail(taskDetail);
            DeleteProjectInDatabase(projectName);
        }
    }

    /// <summary>Abbruch des Save-Dialogs erzeugt keine Datei und zeigt keinen Fehlerbanner.</summary>
    protected void CliRawExport_CancelCreatesNoFileAndNoErrorbanner_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        const string projectName = "CliRawExport-Cancel-Projekt";
        SetupProjectWithNewWorkItem(mainWindow, "CliRawExport-Cancel-Repo", projectName);

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", forProjectUse: false);
        taskDetail.WaitForCliRunning();
        taskDetail.SwitchPanel("CliViewButton");

        var exportPath = Path.Combine(Path.GetTempPath(), $"cli-raw-export-cancel-{Guid.NewGuid():N}.raw");

        try
        {
            taskDetail.ExportCliRaw(null);

            File.Exists(exportPath).Should().BeFalse();
            new ErrorView(mainWindow).IsVisible.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(exportPath))
                File.Delete(exportPath);

            TryCloseTaskDetail(taskDetail);
            DeleteProjectInDatabase(projectName);
        }
    }

    private void SeedCliOutputForCurrentWorkItem(string output)
    {
        using var db = OpenTestDbContext();
        var workItemId = db.WorkItems
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.Id)
            .First();

        db.LogEntries.Add(new LogEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Type = LogEntryType.CliOutput,
            Content = output,
            Timestamp = DateTimeOffset.UtcNow
        });

        db.SaveChanges();
    }

    private static void TryCloseTaskDetail(TaskDetailView taskDetail)
    {
        try
        {
            taskDetail.ForceClose(recurseToDashboard: false);
        }
        catch
        {
        }
    }

    private void DeleteProjectInDatabase(string projectName)
    {
        using var db = OpenTestDbContext();
        var project = db.Projects.SingleOrDefault(p => p.Name == projectName);
        if (project is null)
            return;

        db.Projects.Remove(project);
        db.SaveChanges();
    }
}
