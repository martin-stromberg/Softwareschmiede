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
    protected void CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        const string projektName = "CliRawExport-Happy-Projekt";
        SetupProjectMitNeuerAufgabe(mainWindow, "CliRawExport-Happy-Repo", projektName);
        var erwarteteCliZeile = $"CliRawExport-{Guid.NewGuid():N}";
        SeedCliOutputForAktuelleAufgabe(erwarteteCliZeile);

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();
        taskDetail.SwitchPanel("CliViewButton");

        var exportPfad = Path.Combine(Path.GetTempPath(), $"cli-raw-export-{Guid.NewGuid():N}.raw");

        try
        {
            taskDetail.ExportCliRaw(exportPfad);

            var deadline = DateTime.UtcNow + Medium;
            while (DateTime.UtcNow < deadline && !File.Exists(exportPfad))
                Thread.Sleep(200);

            File.Exists(exportPfad).Should().BeTrue();
            File.ReadAllText(exportPfad).Should().Contain(erwarteteCliZeile);
            new ErrorView(mainWindow).IsVisible.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(exportPfad))
                File.Delete(exportPfad);

            TryCloseTaskDetail(taskDetail);
            DeleteProjectInDatenbank(projektName);
        }
    }

    /// <summary>Abbruch des Save-Dialogs erzeugt keine Datei und zeigt keinen Fehlerbanner.</summary>
    protected void CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        const string projektName = "CliRawExport-Cancel-Projekt";
        SetupProjectMitNeuerAufgabe(mainWindow, "CliRawExport-Cancel-Repo", projektName);

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

            TryCloseTaskDetail(taskDetail);
            DeleteProjectInDatenbank(projektName);
        }
    }

    private void SeedCliOutputForAktuelleAufgabe(string output)
    {
        using var db = OpenTestDbContext();
        var aufgabeId = db.Aufgaben
            .OrderByDescending(a => a.ErstellungsDatum)
            .Select(a => a.Id)
            .First();

        db.Protokolleintraege.Add(new Protokolleintrag
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            Typ = ProtokollTyp.CliOutput,
            Inhalt = output,
            Zeitstempel = DateTimeOffset.UtcNow
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

    private void DeleteProjectInDatenbank(string projektName)
    {
        using var db = OpenTestDbContext();
        var projekt = db.Projekte.SingleOrDefault(p => p.Name == projektName);
        if (projekt is null)
            return;

        db.Projekte.Remove(projekt);
        db.SaveChanges();
    }
}
