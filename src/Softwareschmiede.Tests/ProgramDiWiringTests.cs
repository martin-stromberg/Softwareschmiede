using FluentAssertions;

namespace Softwareschmiede.Tests;

public sealed class ProgramDiWiringTests
{
    [Fact]
    public void Program_ShouldRegisterAutoShutdownFeatureServices()
    {
        var root = FindRepositoryRoot();
        var programPath = Path.Combine(root, "src", "Softwareschmiede", "Program.cs");
        var source = File.ReadAllText(programPath);

        source.Should().Contain("builder.Services.AddSingleton<KiAusfuehrungsService>();");
        source.Should().Contain("builder.Services.AddSingleton<IRunningAutomationStatusSource>(sp => sp.GetRequiredService<KiAusfuehrungsService>());");
        source.Should().Contain("builder.Services.AddSingleton<IAutoShutdownOrchestrator, AutoShutdownOrchestrator>();");
        source.Should().Contain("builder.Services.AddSingleton<ISystemShutdownService, SystemShutdownService>();");
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
