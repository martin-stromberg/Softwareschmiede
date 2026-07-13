using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Ausführung der CLI in einem konfigurierten Arbeitsunterverzeichnis (Issue #98).
///
/// Der Testmodus lädt als SCM-Plugin ausschließlich <c>LocalDirectoryPlugin</c>. Die Dialog-Tests
/// verifizieren damit deterministisch denselben Result-Pfad für erfolgreiche Strukturabrufe und
/// Fehler-Fallbacks, den Remote-Plugins wie Bitbucket verwenden. Die Start-Tests hinterlegen das
/// Arbeitsverzeichnis weiterhin gezielt in der Test-Datenbank, weil dort die spätere CLI-Auswirkung
/// im Vordergrund steht.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_WorkingDirectory : WpfTestBase
{
    /// <summary>
    /// Szenario: Repository-Zuweisung mit erfolgreichem Strukturabruf.
    /// Erwartung: Die Auswahlbox zeigt Unterverzeichnisse und die Auswahl wird gespeichert.
    /// </summary>
    [Fact]
    public async Task RepositoryZuweisen_MitErfolgreichemStrukturabruf_ZeigtUndSpeichertArbeitsverzeichnis_E2E()
    {
        var sourceDirectory = CreateLocalSourceDirectory("WorkingDir-Assign-Success-Repo");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "WorkingDir-Assign-Success-Repo", "backend"));
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "WorkingDir-Assign-Success-Repo", "frontend"));

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory);
        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "WorkingDir-Assign-Success-Projekt");

        var dialog = OpenRepositoryAssignDialog(mainWindow);
        var repositoryItem = WaitForFirstRepositoryItem(dialog);
        repositoryItem.Click();

        var workingDirectoryBox = WaitForWorkingDirectoryComboBox(dialog);
        SelectComboBoxItemByClick(workingDirectoryBox, "backend", Short);

        ConfirmDialog(dialog, "Zuweisen");

        var saved = await WaitForSavedWorkingDirectoryAsync("backend");
        Assert.Equal("backend", saved);
    }

    /// <summary>
    /// Szenario: Repository-Zuweisung mit fehlgeschlagenem Strukturabruf.
    /// Erwartung: Eine TextBox erscheint und speichert einen manuellen relativen Pfad.
    /// </summary>
    [Fact]
    public async Task RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E()
    {
        var repositoryName = "WorkingDir-Assign-Fallback-Repo";
        var sourceDirectory = CreateLocalSourceDirectory(repositoryName);
        var repositoryPath = Path.Combine(sourceDirectory, repositoryName);

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory);
        NavigateToProjecten(mainWindow);
        CreateAndOpenProject(mainWindow, "WorkingDir-Assign-Fallback-Projekt");

        var dialog = OpenRepositoryAssignDialog(mainWindow);
        var repositoryItem = WaitForFirstRepositoryItem(dialog);

        Directory.Delete(repositoryPath, recursive: true);
        repositoryItem.Click();

        var manualInput = WaitForWorkingDirectoryTextBox(dialog);
        manualInput.AsTextBox().Text = @"manual\backend";

        ConfirmDialog(dialog, "Zuweisen");

        var saved = await WaitForSavedWorkingDirectoryAsync("manual/backend");
        Assert.Equal("manual/backend", saved);
    }

    /// <summary>
    /// Szenario: Arbeitsverzeichnis-Bearbeitung mit fehlgeschlagenem Strukturabruf.
    /// Erwartung: Der vorhandene manuelle Wert erscheint im Textfeld und kann bestätigt werden.
    /// </summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeiten_MitFehlgeschlagenemStrukturabruf_ZeigtUndBestaetigtVorhandenenWert_E2E()
    {
        var projektName = "WorkingDir-Edit-Fallback-Projekt";
        var repositoryName = "WorkingDir-Edit-Fallback-Repo";
        var repositoryUrl = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_missing_repo_{Guid.NewGuid():N}");
        const string existingWorkingDirectory = "legacy/backend";

        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        await SeedProjectRepositoryWithWorkingDirectoryAsync(projektName, repositoryName, repositoryUrl, existingWorkingDirectory);

        NavigateToProjecten(mainWindow);
        OpenProject(mainWindow, projektName);

        var bearbeitenButton = WaitForElement(mainWindow, cf => cf.ByName("ArbeitsverzeichnisBearbeiten"), Short);
        bearbeitenButton.AsButton().Click();

        var dialog = WaitForWindow("Arbeitsverzeichnis bearbeiten", Short);
        var manualInput = WaitForWorkingDirectoryTextBox(dialog);
        Assert.Equal(existingWorkingDirectory, manualInput.AsTextBox().Text);

        ConfirmDialog(dialog, "Speichern");

        var saved = await WaitForSavedWorkingDirectoryAsync(existingWorkingDirectory);
        Assert.Equal(existingWorkingDirectory, saved);
    }

    /// <summary>
    /// Szenario: Repository mit konfiguriertem Arbeitsunterverzeichnis wird gestartet.
    /// Erwartung: CLI startet erfolgreich (Stoppen-Button erscheint), kein Fehlerbanner.
    /// </summary>
    [SkippableFact]
    public async Task AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("WorkingDir-Repo", "WorkingDir-Projekt");

        await SeedWorkingDirectoryAsync("backend", createSubdirectory: true);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        var fehlerMeldung = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerMeldung);
    }

    /// <summary>
    /// Szenario: Konfiguriertes Arbeitsverzeichnis existiert nach dem Start nicht im Repository.
    /// Erwartung: Fehlerbanner erscheint, CLI startet nicht.
    /// </summary>
    /// <remarks>
    /// Regressionsabdeckung für den Fix in <see cref="WpfTestBase.WaitForElement"/>: Dieser Test
    /// wartet selbst auf das Element "FehlerMeldung" (Zeile unten), das zugleich die proaktive
    /// Fail-Fast-Diagnose in <c>WaitForElement</c> auslöst. Vor dem Fix führte das dazu, dass der Test
    /// fälschlich mit einer <see cref="InvalidOperationException"/> statt mit dem erwarteten Treffer
    /// abbrach (nicht-atomare UI-Automation-Aufrufe, siehe Doku an <c>WaitForElement</c>). Ein isolierter
    /// Unit-Test für <c>WaitForElement</c> selbst ist nicht sinnvoll möglich: <c>AutomationElement</c> und
    /// <c>ConditionFactory</c> sind eng an eine echte, native UI-Automation-Instanz (FlaUI/UIA3, COM-basiert)
    /// gekoppelt und bieten keine Testschnittstellen/-doubles für eine In-Memory-Simulation. Die Verifikation
    /// erfolgt daher über diesen und den analogen Path-Traversal-Test (beide mehrfach wiederholt grün, siehe
    /// continue.md).
    /// </remarks>
    [Fact]
    public async Task AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("WorkingDir-Missing-Repo", "WorkingDir-Missing-Projekt");

        await SeedWorkingDirectoryAsync("does-not-exist", createSubdirectory: false);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var fehlerBanner = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Medium);
        Assert.NotNull(fehlerBanner);

        var stoppenButton = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
        Assert.Null(stoppenButton);
    }

    /// <summary>
    /// Szenario: Konfiguriertes Arbeitsverzeichnis versucht, das Repository-Verzeichnis per Path-Traversal
    /// zu verlassen. Erwartung: Fehlerbanner erscheint, CLI startet nicht.
    /// </summary>
    [Fact]
    public async Task AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("WorkingDir-Traversal-Repo", "WorkingDir-Traversal-Projekt");

        await SeedWorkingDirectoryAsync(Path.Combine("..", "..", "etc"), createSubdirectory: false);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var fehlerBanner = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Medium);
        Assert.NotNull(fehlerBanner);

        var stoppenButton = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
        Assert.Null(stoppenButton);
    }

    /// <summary>
    /// Hinterlegt für das (einzige) zugewiesene Repository ein Arbeitsverzeichnis direkt in der
    /// Test-Datenbank und legt optional das entsprechende Unterverzeichnis im lokalen Quellordner an.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad, der als Arbeitsverzeichnis hinterlegt wird.</param>
    /// <param name="createSubdirectory">Ob das Unterverzeichnis im lokalen Quellordner tatsächlich angelegt werden soll.</param>
    private async Task SeedWorkingDirectoryAsync(string relativePath, bool createSubdirectory)
    {
        await using var db = OpenTestDbContext();
        var repository = db.GitRepositories.Single();

        if (createSubdirectory)
        {
            Directory.CreateDirectory(Path.Combine(repository.RepositoryUrl, relativePath));
        }

        db.Add(new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            WorkingDirectoryRelativePath = relativePath,
            Aktiv = true
        });
        await db.SaveChangesAsync();
    }

    private AutomationElement OpenRepositoryAssignDialog(AutomationElement mainWindow)
    {
        var zuweisenButton = WaitForElement(mainWindow, cf => cf.ByName("Zuweisen"), Short);
        zuweisenButton.AsButton().Click();
        return WaitForWindow("Repository zuweisen", Short);
    }

    private static AutomationElement WaitForFirstRepositoryItem(AutomationElement dialog)
    {
        AutomationElement[] items = [];
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var listBox = dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listBox is not null)
            {
                items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                if (items.Length > 0)
                    return items[0];
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Repository-Liste im Zuweisungsdialog enthielt kein Element innerhalb des Timeouts.");
    }

    private static AutomationElement WaitForWorkingDirectoryComboBox(AutomationElement dialog)
    {
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var comboBoxes = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
            if (comboBoxes.Length >= 2)
                return comboBoxes[1];

            Thread.Sleep(200);
        }

        throw new TimeoutException("Arbeitsverzeichnis-ComboBox wurde im Zuweisungsdialog nicht sichtbar.");
    }

    private static AutomationElement WaitForWorkingDirectoryTextBox(AutomationElement dialog)
    {
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var textBoxes = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
            if (textBoxes.Length == 1)
                return textBoxes[0];

            Thread.Sleep(200);
        }

        throw new TimeoutException("Arbeitsverzeichnis-TextBox wurde im Dialog nicht sichtbar.");
    }

    private static void ConfirmDialog(AutomationElement dialog, string buttonName)
    {
        var button = WaitForElement(dialog, cf => cf.ByName(buttonName), Short);
        button.AsButton().Click();
    }

    private async Task SeedProjectRepositoryWithWorkingDirectoryAsync(
        string projektName,
        string repositoryName,
        string repositoryUrl,
        string workingDirectory)
    {
        await using var db = OpenTestDbContext();
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = projektName,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            PluginTyp = "LocalDirectoryPlugin",
            RepositoryName = repositoryName,
            RepositoryUrl = repositoryUrl,
            Aktiv = true
        };
        var configuration = new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            WorkingDirectoryRelativePath = workingDirectory,
            Aktiv = true
        };

        db.Projekte.Add(projekt);
        db.GitRepositories.Add(repository);
        db.RepositoryStartKonfigurationen.Add(configuration);
        await db.SaveChangesAsync();
    }

    private async Task<string?> WaitForSavedWorkingDirectoryAsync(string expected)
    {
        var deadline = DateTime.UtcNow + Medium;
        string? saved = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            saved = db.RepositoryStartKonfigurationen.SingleOrDefault()?.WorkingDirectoryRelativePath;
            if (string.Equals(saved, expected, StringComparison.Ordinal))
                return saved;

            await Task.Delay(200);
        }

        return saved;
    }
}
