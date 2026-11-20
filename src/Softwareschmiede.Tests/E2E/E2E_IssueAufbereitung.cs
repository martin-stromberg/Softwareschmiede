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
    protected void Einstellungen_IssueAufbereitungKonfigurieren_E2E(Window mainWindow)
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
        string? gewaehltesPluginLabel = null;
        try
        {
            // Ausgangszustand: Funktion aus (Standard), Statuszeile „deaktiviert“.
            settings.OpenIssueAufbereitung();
            Assert.Equal("deaktiviert", settings.GetIssueAufbereitungStatus());
            Assert.False(settings.IsIssueAufbereitungEnabled());
            Assert.True(settings.IsIssueAufbereitungKommentarEnabled());

            // Funktion aktivieren und alle vier Einstellungen setzen
            // (die Felder sind bei deaktivierter Funktion gesperrt).
            settings.SetIssueAufbereitungEnabled(true);
            Assert.Equal("aktiviert", settings.GetIssueAufbereitungStatus());
            Assert.Contains("Globale Standard-CLI verwenden", settings.GetIssueAufbereitungPluginOptionen());
            var optionen = settings.GetIssueAufbereitungPluginOptionen();
            gewaehltesPluginLabel = optionen.FirstOrDefault(x => x != "Globale Standard-CLI verwenden")
                ?? "Globale Standard-CLI verwenden";
            settings.SetIssueAufbereitungPlugin(gewaehltesPluginLabel);
            settings.SetIssueAufbereitungRepository(repositoryName, true);
            settings.SetIssueAufbereitungKommentarEnabled(false);
            settings.SaveSettings();

            // Persistenz-Nachweis über UI-Neuladen.
            var settingsReopened = settings.Menu.NavigateToSettings();
            settingsReopened.OpenIssueAufbereitung();
            Assert.True(settingsReopened.IsIssueAufbereitungEnabled());
            Assert.Equal("aktiviert", settingsReopened.GetIssueAufbereitungStatus());
            Assert.Equal(gewaehltesPluginLabel, settingsReopened.GetIssueAufbereitungPlugin());
            Assert.True(settingsReopened.IsIssueAufbereitungRepositorySelected(repositoryName));
            Assert.False(settingsReopened.IsIssueAufbereitungKommentarEnabled());

            // Persistenz-Nachweis in AppEinstellungen und Repository-Flag.
            using (var db = OpenTestDbContext())
            {
                Assert.Equal("True", db.AppSettings
                    .Where(e => e.Key == AppEinstellungService.ScmIssueAufbereitungEnabledKey)
                    .Select(e => e.Value).FirstOrDefault());
                Assert.Equal("False", db.AppSettings
                    .Where(e => e.Key == AppEinstellungService.ScmIssueAufbereitungKommentarEnabledKey)
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
                settings.OpenIssueAufbereitung();
                settings.SetIssueAufbereitungEnabled(false);
                settings.SetIssueAufbereitungPlugin("Globale Standard-CLI verwenden");
                settings.SetIssueAufbereitungRepository(repositoryName, false);
                settings.SetIssueAufbereitungKommentarEnabled(true);
                settings.SaveSettings();
                settings.Menu.NavigateToDashboard();
            }
            catch
            {
                try
                {
                    using var db = OpenTestDbContext();
                    db.AppSettings
                        .Where(e => e.Key == AppEinstellungService.ScmIssueAufbereitungEnabledKey
                            || e.Key == AppEinstellungService.ScmIssueAufbereitungKommentarEnabledKey
                            || e.Key == AppEinstellungService.ScmIssueAufbereitungPluginKey)
                        .ToList()
                        .ForEach(e => e.Value = null);
                    db.SaveChanges();
                }
                catch { }
            }
        }
    }
}
