using FluentAssertions;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>
/// Tests der Shutdown-Kommando-Abbildung in <see cref="SystemShutdownService"/>
/// (<see cref="SystemShutdownService.ResolveShutdownCommand"/>): Windows mit/ohne Force-Flag
/// sowie die unveränderten Linux-/macOS-Kommandos.
/// </summary>
public sealed class SystemShutdownServiceTests
{
    /// <summary>Auf Windows liefert das Erzwingen-Flag das Forced-Shutdown-Kommando.</summary>
    [SkippableFact]
    public void ResolveShutdownCommand_WindowsMitErzwingen_LiefertForcedShutdown()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-spezifisches Mapping.");

        var (fileName, arguments) = SystemShutdownService.ResolveShutdownCommand(erzwingen: true);

        fileName.Should().Be("shutdown");
        arguments.Should().Be("/s /f /t 0");
    }

    /// <summary>Auf Windows liefert der Shutdown ohne Erzwingen das reguläre Kommando.</summary>
    [SkippableFact]
    public void ResolveShutdownCommand_WindowsOhneErzwingen_LiefertRegulaeresShutdown()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-spezifisches Mapping.");

        var (fileName, arguments) = SystemShutdownService.ResolveShutdownCommand(erzwingen: false);

        fileName.Should().Be("shutdown");
        arguments.Should().Be("/s /t 0");
    }

    /// <summary>Unter Linux bleibt das Shutdown-Kommando unverändert <c>shutdown -h now</c>.</summary>
    [SkippableFact]
    public void ResolveShutdownCommand_Linux_BleibtUnveraendert()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "Linux-spezifisches Mapping.");

        var (fileName, arguments) = SystemShutdownService.ResolveShutdownCommand(erzwingen: true);

        fileName.Should().Be("shutdown");
        arguments.Should().Be("-h now");
    }

    /// <summary>Unter macOS bleibt das AppleScript-Kommando unverändert.</summary>
    [SkippableFact]
    public void ResolveShutdownCommand_MacOS_BleibtUnveraendert()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "macOS-spezifisches Mapping.");

        var (fileName, arguments) = SystemShutdownService.ResolveShutdownCommand(erzwingen: true);

        fileName.Should().Be("osascript");
        arguments.Should().Contain("System Events");
        arguments.Should().Contain("shut down");
    }
}
