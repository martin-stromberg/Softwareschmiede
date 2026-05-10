using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

public sealed class PluginManagerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"plugin-manager-{Guid.NewGuid():N}");

    [Fact]
    public void GetSourceCodeManagementPlugins_ShouldReturnEmpty_WhenPluginDirectoryMissing()
    {
        var sut = CreateSut(Path.Combine(_tempDirectory, "missing"));

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        sut.GetDevelopmentAutomationPlugins().Should().BeEmpty();
    }

    [Fact]
    public void GetSourceCodeManagementPlugins_ShouldSkipInvalidDll_WhenBadDllExists()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "invalid.dll"), "not a valid assembly");

        var sut = CreateSut(_tempDirectory);

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        sut.GetDevelopmentAutomationPlugins().Should().BeEmpty();
    }

    [Fact]
    public void GetSourceCodeManagementPlugins_ShouldLoadGitAndKiPlugins_WhenValidPluginDllsExist()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(GitHubPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHub.dll"), overwrite: true);
        File.Copy(typeof(GitHubCopilotPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHubCopilot.dll"), overwrite: true);

        var sut = CreateSut(_tempDirectory);

        var scmPlugins = sut.GetSourceCodeManagementPlugins();
        var kiPlugins = sut.GetDevelopmentAutomationPlugins();

        scmPlugins.Should().ContainSingle(p => p.PluginName == "GitHub");
        kiPlugins.Should().ContainSingle(p => p.PluginName == "GitHub Copilot");
    }

    [Fact]
    public void GetDefaultSourceCodeManagementPlugin_ShouldThrow_WhenNoPluginLoaded()
    {
        var sut = CreateSut(Path.Combine(_tempDirectory, "missing"));

        var act = () => sut.GetDefaultSourceCodeManagementPlugin();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Source-Code-Management-Plugin*");
    }

    [Fact]
    public void GetDefaultDevelopmentAutomationPlugin_ShouldThrow_WhenNoPluginLoaded()
    {
        var sut = CreateSut(Path.Combine(_tempDirectory, "missing"));

        var act = () => sut.GetDefaultDevelopmentAutomationPlugin();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Development-Automation-Plugin*");
    }

    [Fact]
    public void GetSourceCodeManagementPlugins_ShouldNotDuplicatePlugins_WhenCalledMultipleTimes()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(GitHubPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHub.dll"), overwrite: true);
        File.Copy(typeof(GitHubCopilotPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHubCopilot.dll"), overwrite: true);

        var sut = CreateSut(_tempDirectory);

        var firstCall = sut.GetSourceCodeManagementPlugins();
        var secondCall = sut.GetSourceCodeManagementPlugins();
        var kiFirst = sut.GetDevelopmentAutomationPlugins();
        var kiSecond = sut.GetDevelopmentAutomationPlugins();

        firstCall.Should().HaveCount(1);
        secondCall.Should().HaveCount(1);
        kiFirst.Should().HaveCount(1);
        kiSecond.Should().HaveCount(1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static PluginManager CreateSut(string pluginDirectory)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(new Mock<ICliRunner>().Object)
            .AddSingleton(new Mock<ICredentialStore>().Object)
            .BuildServiceProvider();
        return new PluginManager(services, NullLogger<PluginManager>.Instance, pluginDirectory);
    }
}
