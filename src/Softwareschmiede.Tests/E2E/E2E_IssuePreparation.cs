using FlaUI.Core.AutomationElements;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die Issue-Aufbereitung (Issue 412, Schritt 6): Funktionsschalter,
/// Funktions-Plugin, Repository-Auswahl und Kommentar-Schalter werden über die UI
/// im Expander „Automatisierung → Issue-Aufbereitung“ gesetzt und persistent gespeichert.
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt den Einstellungs-Benutzerfluss der Issue-Aufbereitung konsolidiert durch:
    /// Expander öffnen, Funktion aktivieren, Funktions-Plugin wählen, Repository markieren,
    /// Kommentar-Schalter deaktivieren, speichern, neu öffnen und UI- plus
    /// AppEinstellung-/Repository-Persistenz prüfen. Im finally werden alle Werte auf die
    /// Auslieferungs-Defaults zurückgesetzt (Funktion aus, Kommentar an, Standard-Plugin,
    /// Repository-Flags gelöscht).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Settings_ConfigureIssuePreparation_E2E(Window mainWindow)
    {
        const string repositoryName = "aufbereitung-e2e-repo";
        var repositoryId = Guid.NewGuid();
        using (var db = OpenTestDbContext())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "Aufbereitung-E2E" };
            db.AddRange(project, new GitRepository
            {
                Id = repositoryId,
                ProjectId = project.Id,
                PluginType = "Softwareschmiede.GitHub",
                RepositoryUrl = "https://github.com/owner/" + repositoryName,
                RepositoryName = repositoryName,
                Active = true
            });
            db.SaveChanges();
        }

        var settings = new SettingsView(mainWindow).ForceShow();
        string? selectedPluginLabel = null;
        try
        {
            // Ausgangszustand: Funktion aus (Standard), Statuszeile „deaktiviert“.
            settings.OpenIssuePreparation();
            Assert.Equal("deaktiviert", settings.GetIssuePreparationStatus());
            Assert.False(settings.IsIssuePreparationEnabled());
            Assert.True(settings.IsIssuePreparationCommentEnabled());

            // Funktion aktivieren und alle vier Settings setzen
            // (die Felder sind bei deaktivierter Funktion gesperrt).
            settings.SetIssuePreparationEnabled(true);
            Assert.Equal("aktiviert", settings.GetIssuePreparationStatus());
            Assert.Contains("Globale Standard-CLI verwenden", settings.GetIssuePreparationPluginOptions());
            var options = settings.GetIssuePreparationPluginOptions();
            selectedPluginLabel = options.FirstOrDefault(x => x != "Globale Standard-CLI verwenden")
                ?? "Globale Standard-CLI verwenden";
            settings.SetIssuePreparationPlugin(selectedPluginLabel);
            settings.SetIssuePreparationRepository(repositoryName, true);
            settings.SetIssuePreparationCommentEnabled(false);
            settings.SaveSettings();

            // Persistenz-Nachweis über UI-Neuladen.
            var settingsReopened = settings.Menu.NavigateToSettings();
            settingsReopened.OpenIssuePreparation();
            Assert.True(settingsReopened.IsIssuePreparationEnabled());
            Assert.Equal("aktiviert", settingsReopened.GetIssuePreparationStatus());
            Assert.Equal(selectedPluginLabel, settingsReopened.GetIssuePreparationPlugin());
            Assert.True(settingsReopened.IsIssuePreparationRepositorySelected(repositoryName));
            Assert.False(settingsReopened.IsIssuePreparationCommentEnabled());

            // Persistenz-Nachweis in AppEinstellungen und Repository-Flag.
            using (var db = OpenTestDbContext())
            {
                Assert.Equal("True", db.AppSettings
                    .Where(e => e.Key == AppSettingService.ScmIssuePreparationEnabledKey)
                    .Select(e => e.Value).FirstOrDefault());
                Assert.Equal("False", db.AppSettings
                    .Where(e => e.Key == AppSettingService.ScmIssuePreparationCommentEnabledKey)
                    .Select(e => e.Value).FirstOrDefault());
                Assert.True(db.GitRepositories.Single(r => r.Id == repositoryId).IssuePreparationEnabled);
            }
        }
        finally
        {
            // Auslieferungs-Defaults wiederherstellen, damit Folgeszenarien sauber starten.
            try
            {
                settings.ForceShow();
                settings.OpenIssuePreparation();
                settings.SetIssuePreparationEnabled(false);
                settings.SetIssuePreparationPlugin("Globale Standard-CLI verwenden");
                settings.SetIssuePreparationRepository(repositoryName, false);
                settings.SetIssuePreparationCommentEnabled(true);
                settings.SaveSettings();
                settings.Menu.NavigateToDashboard();
            }
            catch
            {
                try
                {
                    using var db = OpenTestDbContext();
                    db.AppSettings
                        .Where(e => e.Key == AppSettingService.ScmIssuePreparationEnabledKey
                            || e.Key == AppSettingService.ScmIssuePreparationCommentEnabledKey
                            || e.Key == AppSettingService.ScmIssuePreparationPluginKey)
                        .ToList()
                        .ForEach(e => e.Value = null);
                    db.SaveChanges();
                }
                catch { }
            }
        }
    }
}
