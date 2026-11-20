using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>PluginManagerTests_Whitelist — Härtung der Plugin-Discovery (Issue #380):
/// nur whitelisted First-Party-Plugin-Assemblies werden geladen, unbekannte DLLs erzeugen
/// einen Warn-Log-Eintrag.</summary>
public sealed class PluginManagerTests_Whitelist : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"plugin-manager-whitelist-{Guid.NewGuid():N}");
    private readonly string? _previousNetrcPathOverride;

    /// <summary>PluginManagerTests_Whitelist.</summary>
    public PluginManagerTests_Whitelist()
    {
        // Gleiche Isolation wie in PluginManagerTests: die Plugin-Discovery instanziiert Plugins
        // über ActivatorUtilities; der netrcPath-Parameter ist dabei nicht erreichbar.
        _previousNetrcPathOverride = Environment.GetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable);
        Environment.SetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable,
            Path.Combine(_tempDirectory, "_netrc"));
    }

    /// <summary>Eine gültige, aber nicht whitelisted Assembly auf Top-Level wird nicht geladen
    /// und erzeugt einen Warn-Log-Eintrag.</summary>
    [Fact]
    public void GetSourceCodeManagementPlugins_WithUnknownDllOnTopLevel_SkipsDllAndLogsWarning()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(Mock).Assembly.Location, Path.Combine(_tempDirectory, "Moq.dll"));
        var loggerMock = new Mock<ILogger<PluginManager>>();
        var sut = CreateSut(_tempDirectory, loggerMock);

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        sut.GetDevelopmentAutomationPlugins().Should().BeEmpty();
        VerifyWarningLogged(loggerMock, Times.AtLeastOnce());
    }

    /// <summary>Eine fremde Assembly, die sich per Dateinamen als whitelisted Plugin tarnt,
    /// wird anhand des echten Assembly-Namens erkannt, nicht geladen und warn-geloggt.</summary>
    [Fact]
    public void GetSourceCodeManagementPlugins_WithMasqueradedDll_SkipsDllAndLogsWarning()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(typeof(Mock).Assembly.Location, Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.GitHub.dll"));
        var loggerMock = new Mock<ILogger<PluginManager>>();
        var sut = CreateSut(_tempDirectory, loggerMock);

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        VerifyWarningLogged(loggerMock, Times.AtLeastOnce());
    }

    /// <summary>Eine whitelisted DLL im gleichnamigen Unterordner wird geladen; fremde
    /// Dependency-DLLs im selben Ordner sind keine Ladekandidaten und erzeugen keine Warnung.</summary>
    [Fact]
    public void GetDevelopmentAutomationPlugins_WithWhitelistedDllInMatchingSubfolder_LoadsPlugin()
    {
        var pluginFolder = Path.Combine(_tempDirectory, "Softwareschmiede.Plugin.ClaudeCli");
        Directory.CreateDirectory(pluginFolder);
        File.Copy(typeof(ClaudeCliPlugin).Assembly.Location,
            Path.Combine(pluginFolder, "Softwareschmiede.Plugin.ClaudeCli.dll"));
        File.Copy(typeof(Mock).Assembly.Location, Path.Combine(pluginFolder, "helper.dll"));
        var loggerMock = new Mock<ILogger<PluginManager>>();
        var sut = CreateSut(_tempDirectory, loggerMock);

        var aiPlugins = sut.GetDevelopmentAutomationPlugins();

        aiPlugins.Should().ContainSingle(p => p.PluginName == "Claude CLI");
        VerifyWarningForFile(loggerMock, "helper.dll", Times.Never());
    }

    /// <summary>Eine Entry-DLL in einem unbekannten Unterordner ist zwar Ladekandidat,
    /// scheitert aber an der Whitelist und wird warn-geloggt statt geladen.</summary>
    [Fact]
    public void GetSourceCodeManagementPlugins_WithUnknownSubfolderEntryDll_SkipsDllAndLogsWarning()
    {
        var pluginFolder = Path.Combine(_tempDirectory, "Evil");
        Directory.CreateDirectory(pluginFolder);
        File.Copy(typeof(Mock).Assembly.Location, Path.Combine(pluginFolder, "Evil.dll"));
        var loggerMock = new Mock<ILogger<PluginManager>>();
        var sut = CreateSut(_tempDirectory, loggerMock);

        sut.GetSourceCodeManagementPlugins().Should().BeEmpty();
        sut.GetDevelopmentAutomationPlugins().Should().BeEmpty();
        VerifyWarningLogged(loggerMock, Times.AtLeastOnce());
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(GitHubPlugin.NetrcPathEnvironmentVariable, _previousNetrcPathOverride);
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static void VerifyWarningLogged(Mock<ILogger<PluginManager>> loggerMock, Times times)
        => loggerMock.Verify(l => l.Log(
            It.Is<LogLevel>(level => level == LogLevel.Warning),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private static void VerifyWarningForFile(Mock<ILogger<PluginManager>> loggerMock, string fileName, Times times)
        => loggerMock.Verify(l => l.Log(
            It.Is<LogLevel>(level => level == LogLevel.Warning),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v != null && v.ToString()!.Contains(fileName)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private static PluginManager CreateSut(string pluginDirectory, Mock<ILogger<PluginManager>>? loggerMock = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(new Mock<ICliRunner>().Object)
            .AddSingleton(new Mock<ICredentialStore>().Object)
            .AddSingleton(new Mock<IProcessStarter>().Object)
            .AddSingleton(new Mock<IVisualStudioCodeLocator>().Object)
            .BuildServiceProvider();
        return new PluginManager(services,
            (loggerMock ?? new Mock<ILogger<PluginManager>>()).Object, pluginDirectory);
    }
}
