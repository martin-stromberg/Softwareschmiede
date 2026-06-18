using FluentAssertions;

namespace Softwareschmiede.Tests.Components.Pages.Aufgaben;

public sealed class AufgabeDetailRecoveryTests
{
    [Fact]
    public void AufgabeDetailMarkup_ShouldContainRecoveryActionAndConfirmation()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor");
        var markup = File.ReadAllText(razorPath);
        var codeBehindPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Pages", "Aufgaben", "AufgabeDetail.razor.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);
        var recoveryServicePath = Path.Combine(root, "src", "Softwareschmiede", "Application", "Services", "AufgabeRecoveryService.cs");
        var recoveryService = File.ReadAllText(recoveryServicePath);

        markup.Should().Contain("Aufgabe wiederherstellen");
        markup.Should().Contain("Aufgabe Detail Register");
        markup.Should().Contain(">Aufgabe</button>");
        markup.Should().Contain(">Ausführung</button>");
        markup.Should().Contain(">Projektverzeichnis</button>");
        markup.Should().NotContain("stat-label\">Ansicht");
        markup.Should().Contain("@if (IsRecoveryStatus)");
        markup.Should().Contain("disabled=\"@(_processing || !_recoveryAllowed)\"");
        markup.Should().Contain("_showRecoveryConfirm");
        codeBehind.Should().Contain("AufgabeRecoveryService.IstRecoveryStatus(_aufgabe.Status)");
        recoveryService.Should().Contain("AufgabeStatus.InArbeit or AufgabeStatus.Wartend");
        codeBehind.Should().Contain("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Softwareschmiede.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with Softwareschmiede.slnx not found.");
    }
}
