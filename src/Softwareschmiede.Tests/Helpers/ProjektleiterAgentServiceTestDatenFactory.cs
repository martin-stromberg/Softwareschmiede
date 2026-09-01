using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt die für ProjektleiterAgentService-Tests benötigten Testdaten (Aufgabe, AutonomAufgabeKonfiguration, UnteragentSpezifikation).</summary>
internal static class ProjektleiterAgentServiceTestDatenFactory
{
    /// <summary>Erstellt einen gemockten <see cref="IKiPlugin"/> sowie einen darauf aufgelösten <see cref="PluginSelectionService"/>
    /// (einziges verfügbares KI-Plugin, wird daher unabhängig von <c>Aufgabe.KiPluginPrefix</c> aufgelöst). Für Tests von
    /// <see cref="ProjektleiterAgentService.StarteAgentAsync"/>, die einen echten (aber sicheren, prozesslosen) CLI-Start
    /// über <see cref="TestKiAusfuehrungsServiceFactory"/> auslösen.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext (für Plugin-Default-/Aktivierungs-Einstellungen).</param>
    /// <param name="supportsSessionContinuation">Ob der gemockte KI-Plugin Session-Fortsetzung (<c>--continue</c>) unterstützt.</param>
    /// <returns>Den gemockten KI-Plugin sowie den darauf konfigurierten <see cref="PluginSelectionService"/>.</returns>
    public static (Mock<IKiPlugin> KiPluginMock, PluginSelectionService PluginSelectionService) ErstellePluginSelectionServiceMitKiPlugin(
        SoftwareschmiededDbContext db, bool supportsSessionContinuation = false)
    {
        var kiPluginMock = new Mock<IKiPlugin>();
        kiPluginMock.SetupGet(p => p.PluginName).Returns("Test-KI-Plugin");
        kiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestKi");
        kiPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        kiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        kiPluginMock.Setup(p => p.SupportsSessionContinuation()).Returns(supportsSessionContinuation);
        kiPluginMock
            .Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true
            });

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([kiPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(kiPluginMock.Object);

        var defaultSettingsService = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var activationService = new PluginActivationService(
            new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance),
            pluginManagerMock.Object,
            NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            defaultSettingsService,
            activationService,
            NullLogger<PluginSelectionService>.Instance);

        return (kiPluginMock, pluginSelectionService);
    }

    /// <summary>Erstellt und persistiert eine Aufgabe samt AutonomAufgabeKonfiguration inkl. plan.md/progress.md/state.json im übergebenen Arbeitsverzeichnis.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="projektId">Die Id des Projekts, dem die Aufgabe zugeordnet wird.</param>
    /// <param name="testRoot">Das Arbeitsverzeichnis, in dem plan.md/progress.md/state.json angelegt werden.</param>
    /// <returns>Die erstellte Aufgabe und die zugehörige AutonomAufgabeKonfiguration.</returns>
    public static async Task<(Aufgabe Aufgabe, AutonomAufgabeKonfiguration Konfiguration)> ErstelleAutonomeAufgabeAsync(
        SoftwareschmiededDbContext db, Guid projektId, string testRoot)
    {
        var (aufgabe, konfiguration) = ErstelleAufgabeUndKonfiguration(db, projektId, testRoot);
        await db.SaveChangesAsync();

        await File.WriteAllTextAsync(Path.Combine(testRoot, "plan.md"), "# Plan\n");
        await File.WriteAllTextAsync(Path.Combine(testRoot, "progress.md"), "# Fortschritt\n");
        await File.WriteAllTextAsync(Path.Combine(testRoot, "state.json"), "{\"subagents\":[]}");

        return (aufgabe, konfiguration);
    }

    /// <summary>Erstellt eine Aufgabe samt AutonomAufgabeKonfiguration und fügt beide dem Datenbankkontext hinzu, ohne zu speichern und ohne Dateien im Arbeitsverzeichnis anzulegen.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="projektId">Die Id des Projekts, dem die Aufgabe zugeordnet wird.</param>
    /// <param name="testRoot">Das Arbeitsverzeichnis, das als ArbeitsverzeichnisPfad/PermissionsJsonPfad-Basis dient.</param>
    /// <returns>Die neu angelegte Aufgabe und die zugehörige AutonomAufgabeKonfiguration (beide noch nicht gespeichert).</returns>
    public static (Aufgabe Aufgabe, AutonomAufgabeKonfiguration Konfiguration) ErstelleAufgabeUndKonfiguration(
        SoftwareschmiededDbContext db, Guid projektId, string testRoot)
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Autonome Testaufgabe",
            Status = AufgabeStatus.Gestartet,
            // Modus (regulär/autonom) wird nicht mehr über AusfuehrungsStatus abgebildet, sondern über
            // AutonomKonfiguration != null (siehe Aufgabe.IstAutonom()). Aktiv steht hier für "Projektleiter-Agent
            // läuft bereits" — der Zustand, den die meisten Tests dieser Fabrik (ProjektleiterAgentServiceTests,
            // SessionManagementServiceTests, UnteragentGovernanceMonitoringServiceTests) voraussetzen.
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        db.Aufgaben.Add(aufgabe);

        var konfiguration = new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = Path.Combine(testRoot, "permissions.json"),
            TokenBudget = 500000,
            LaufzeitLimitMinuten = 480,
            PersistenzModus = PersistenzModus.Standard,
            ArbeitsverzeichnisPfad = testRoot
        };
        db.AutonomAufgabeKonfigurationen.Add(konfiguration);

        return (aufgabe, konfiguration);
    }

    /// <summary>Erstellt eine nicht persistierte AutonomAufgabeKonfiguration, die eine nicht existierende Aufgabe referenziert (für Fehlerpfad-Tests).</summary>
    /// <param name="testRoot">Das Arbeitsverzeichnis, das als ArbeitsverzeichnisPfad/PermissionsJsonPfad-Basis dient.</param>
    /// <returns>Eine neue, nicht gespeicherte AutonomAufgabeKonfiguration mit zufälliger, nicht existierender AufgabeId.</returns>
    public static AutonomAufgabeKonfiguration ErstelleKonfigurationFuerNichtExistierendeAufgabe(string testRoot) => new()
    {
        Id = Guid.NewGuid(),
        AufgabeId = Guid.NewGuid(),
        ProjektBranchName = "feature/autonom",
        InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
        PermissionsJsonPfad = Path.Combine(testRoot, "permissions.json"),
        TokenBudget = 500000,
        LaufzeitLimitMinuten = 480,
        PersistenzModus = PersistenzModus.Standard,
        ArbeitsverzeichnisPfad = testRoot
    };

    /// <summary>Erstellt eine (nicht persistierte) UnteragentSpezifikation für Tests.</summary>
    /// <param name="testRoot">Das Arbeitsverzeichnis, unter dem VerzeichnisPfad/ClonePfad abgeleitet werden.</param>
    /// <param name="autonomAufgabeId">Die Id der zugehörigen AutonomAufgabeKonfiguration.</param>
    /// <param name="suffix">Suffix zur Bildung eindeutiger Agent-/Task-/Branch-/Klon-Bezeichner.</param>
    /// <returns>Eine neue UnteragentSpezifikation.</returns>
    public static UnteragentSpezifikation ErstelleUnteragent(string testRoot, Guid autonomAufgabeId, string suffix = "001") => new()
    {
        Id = Guid.NewGuid(),
        AutonomAufgabeId = autonomAufgabeId,
        ExterneAgentId = $"agent-{suffix}",
        TaskId = $"task_{suffix}",
        Scope = "feature-backend",
        Prompt = "Implementiere das Backend.",
        VerzeichnisPfad = Path.Combine(testRoot, "tasks", $"task_{suffix}"),
        Branch = $"feature-unteragent-{suffix}",
        ClonePfad = Path.Combine(testRoot, "clones", $"repo_feature_{suffix}")
    };
}
