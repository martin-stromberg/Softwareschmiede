using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>PluginManagerTests.</summary>
public sealed class PluginManagerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"plugin-manager-{Guid.NewGuid():N}");

    /// <summary><summary>GetSourceCodeManagementPlugins_ShouldReturnEmpty_WhenPluginDirectoryMissing.</summary>.</summary>
    [Fact]
    /// <summary>GetSourceCodeManagementPlugins_ShouldReturnEmpty_WhenPluginDirectoryMissing.</summary>
    public void GetSourceCodeManagementPlugins_ShouldReturnEmpty_WhenPluginDirectoryMissing()
    {
        var sut = CreateSut(Path.Combine(_tempDirectory, "missing"));

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        sut.GetDevelopmentAutomationPlugins().Should().BeEmpty();
    }

    /// <summary><summary>GetSourceCodeManagementPlugins_ShouldSkipInvalidDll_WhenBadDllExists.</summary>.</summary>
    [Fact]
    /// <summary>GetSourceCodeManagementPlugins_ShouldSkipInvalidDll_WhenBadDllExists.</summary>
    public void GetSourceCodeManagementPlugins_ShouldSkipInvalidDll_WhenBadDllExists()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "invalid.dll"), "not a valid assembly");

        var sut = CreateSut(_tempDirectory);

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        sut.GetDevelopmentAutomationPlugins().Should().BeEmpty();
    }

    /// <summary><summary>GetSourceCodeManagementPlugins_ShouldLoadGitAndKiPlugins_WhenValidPluginDllsExist.</summary>.</summary>
    [Fact]
    /// <summary>GetSourceCodeManagementPlugins_ShouldLoadGitAndKiPlugins_WhenValidPluginDllsExist.</summary>
    public void GetSourceCodeManagementPlugins_ShouldLoadGitAndKiPlugins_WhenValidPluginDllsExist()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(GitHubPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHub.dll"), overwrite: true);
        File.Copy(typeof(LocalDirectoryPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.LocalDirectory.dll"), overwrite: true);
        File.Copy(typeof(GitHubCopilotPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHubCopilot.dll"), overwrite: true);
        File.Copy(typeof(ClaudeCliPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.ClaudeCli.dll"), overwrite: true);
        File.Copy(typeof(CodexPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.Codex.dll"), overwrite: true);
        File.Copy(typeof(KiSimulatorPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.KiSimulator.dll"), overwrite: true);

        var sut = CreateSut(_tempDirectory);

        var scmPlugins = sut.GetSourceCodeManagementPlugins();
        var kiPlugins = sut.GetDevelopmentAutomationPlugins();

        scmPlugins.Should().HaveCount(2);
        scmPlugins.Should().Contain(p => p.PluginName == "GitHub");
        scmPlugins.Should().Contain(p => p.PluginName == "Local Directory");
        kiPlugins.Should().HaveCount(4);
        kiPlugins.Should().Contain(p => p.PluginName == "GitHub Copilot");
        kiPlugins.Should().Contain(p => p.PluginName == "Claude CLI");
        kiPlugins.Should().Contain(p => p.PluginName == "Codex CLI");
        kiPlugins.Should().Contain(p => p.PluginName == "KI Simulator");
    }

    /// <summary><summary>GetDefaultSourceCodeManagementPlugin_ShouldThrow_WhenNoPluginLoaded.</summary>.</summary>
    [Fact]
    /// <summary>GetDefaultSourceCodeManagementPlugin_ShouldThrow_WhenNoPluginLoaded.</summary>
    public void GetDefaultSourceCodeManagementPlugin_ShouldThrow_WhenNoPluginLoaded()
    {
        var sut = CreateSut(Path.Combine(_tempDirectory, "missing"));

        var act = () => sut.GetDefaultSourceCodeManagementPlugin();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Source-Code-Management-Plugin*");
    }

    /// <summary><summary>GetDefaultDevelopmentAutomationPlugin_ShouldThrow_WhenNoPluginLoaded.</summary>.</summary>
    [Fact]
    /// <summary>GetDefaultDevelopmentAutomationPlugin_ShouldThrow_WhenNoPluginLoaded.</summary>
    public void GetDefaultDevelopmentAutomationPlugin_ShouldThrow_WhenNoPluginLoaded()
    {
        var sut = CreateSut(Path.Combine(_tempDirectory, "missing"));

        var act = () => sut.GetDefaultDevelopmentAutomationPlugin();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Development-Automation-Plugin*");
    }

    /// <summary><summary>GetDefaultDevelopmentAutomationPlugin_ShouldPreferCopilot_WhenMultipleKiPluginsLoaded.</summary>.</summary>
    [Fact]
    /// <summary>GetDefaultDevelopmentAutomationPlugin_ShouldPreferCopilot_WhenMultipleKiPluginsLoaded.</summary>
    public void GetDefaultDevelopmentAutomationPlugin_ShouldPreferCopilot_WhenMultipleKiPluginsLoaded()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(GitHubCopilotPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHubCopilot.dll"), overwrite: true);
        File.Copy(typeof(ClaudeCliPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.ClaudeCli.dll"), overwrite: true);
        File.Copy(typeof(CodexPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.Codex.dll"), overwrite: true);

        var sut = CreateSut(_tempDirectory);

        var result = sut.GetDefaultDevelopmentAutomationPlugin();

        result.PluginName.Should().Be("GitHub Copilot");
    }

    /// <summary><summary>GetDefaultDevelopmentAutomationPlugin_ShouldReturnClaude_WhenOnlyClaudePluginLoaded.</summary>.</summary>
    [Fact]
    /// <summary>GetDefaultDevelopmentAutomationPlugin_ShouldReturnClaude_WhenOnlyClaudePluginLoaded.</summary>
    public void GetDefaultDevelopmentAutomationPlugin_ShouldReturnClaude_WhenOnlyClaudePluginLoaded()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(ClaudeCliPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.ClaudeCli.dll"), overwrite: true);

        var sut = CreateSut(_tempDirectory);

        var result = sut.GetDefaultDevelopmentAutomationPlugin();

        result.PluginName.Should().Be("Claude CLI");
    }

    /// <summary><summary>GetSourceCodeManagementPlugins_ShouldNotDuplicatePlugins_WhenCalledMultipleTimes.</summary>.</summary>
    [Fact]
    /// <summary>GetSourceCodeManagementPlugins_ShouldNotDuplicatePlugins_WhenCalledMultipleTimes.</summary>
    public void GetSourceCodeManagementPlugins_ShouldNotDuplicatePlugins_WhenCalledMultipleTimes()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(GitHubPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHub.dll"), overwrite: true);
        File.Copy(typeof(LocalDirectoryPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.LocalDirectory.dll"), overwrite: true);
        File.Copy(typeof(GitHubCopilotPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHubCopilot.dll"), overwrite: true);
        File.Copy(typeof(ClaudeCliPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.ClaudeCli.dll"), overwrite: true);
        File.Copy(typeof(CodexPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.Codex.dll"), overwrite: true);
        File.Copy(typeof(KiSimulatorPlugin).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.KiSimulator.dll"), overwrite: true);

        var sut = CreateSut(_tempDirectory);

        var firstCall = sut.GetSourceCodeManagementPlugins();
        var secondCall = sut.GetSourceCodeManagementPlugins();
        var kiFirst = sut.GetDevelopmentAutomationPlugins();
        var kiSecond = sut.GetDevelopmentAutomationPlugins();

        firstCall.Should().HaveCount(2);
        secondCall.Should().HaveCount(2);
        kiFirst.Should().HaveCount(4);
        kiSecond.Should().HaveCount(4);
    }

    /// <summary>Dispose.</summary>
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
