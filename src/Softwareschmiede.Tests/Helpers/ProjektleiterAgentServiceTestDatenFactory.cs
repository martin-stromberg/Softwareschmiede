using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt die für ProjektleiterAgentService-Tests benötigten Testdaten (Aufgabe, AutonomAufgabeKonfiguration, UnteragentSpezifikation).</summary>
internal static class ProjektleiterAgentServiceTestDatenFactory
{
    /// <summary>Erstellt und persistiert eine Aufgabe samt AutonomAufgabeKonfiguration inkl. plan.md/progress.md/state.json im übergebenen Arbeitsverzeichnis.</summary>
    /// <param name="db">Der zu verwendende Datenbankkontext.</param>
    /// <param name="projektId">Die Id des Projekts, dem die Aufgabe zugeordnet wird.</param>
    /// <param name="testRoot">Das Arbeitsverzeichnis, in dem plan.md/progress.md/state.json angelegt werden.</param>
    /// <returns>Die erstellte Aufgabe und die zugehörige AutonomAufgabeKonfiguration.</returns>
    public static async Task<(Aufgabe Aufgabe, AutonomAufgabeKonfiguration Konfiguration)> ErstelleAutonomeAufgabeAsync(
        SoftwareschmiededDbContext db, Guid projektId, string testRoot)
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Autonome Testaufgabe",
            Status = AufgabeStatus.Gestartet,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.AutonomAufgabe,
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
        await db.SaveChangesAsync();

        await File.WriteAllTextAsync(Path.Combine(testRoot, "plan.md"), "# Plan\n");
        await File.WriteAllTextAsync(Path.Combine(testRoot, "progress.md"), "# Fortschritt\n");
        await File.WriteAllTextAsync(Path.Combine(testRoot, "state.json"), "{\"subagents\":[]}");

        return (aufgabe, konfiguration);
    }

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
