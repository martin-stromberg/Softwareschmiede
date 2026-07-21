using FluentAssertions;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für den VisualStudioCodeLocator.</summary>
public sealed class VisualStudioCodeLocatorTests : IDisposable
{
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>Dispose.</summary>
    public void Dispose() => _tempDirectoryFixture.Dispose();

    /// <summary>PATH-Einträge bevorzugen eine daneben liegende Code.exe vor code.cmd.</summary>
    [Fact]
    public void Locate_BevorzugtCodeExeNebenBinPathVorCodeCmd()
    {
        var installDirectory = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_path");
        var binDirectory = Path.Combine(installDirectory, "bin");
        Directory.CreateDirectory(binDirectory);
        var executable = Path.Combine(installDirectory, "Code.exe");
        File.WriteAllText(executable, string.Empty);
        File.WriteAllText(Path.Combine(binDirectory, "code.cmd"), string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?> { ["PATH"] = binDirectory });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>code.cmd im PATH bleibt Fallback, wenn keine Code.exe gefunden wird.</summary>
    [Fact]
    public void Locate_FindetCodeCmdInPathWennCodeExeFehlt()
    {
        var pathDirectory = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_path");
        var executable = Path.Combine(pathDirectory, "code.cmd");
        File.WriteAllText(executable, string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?> { ["PATH"] = pathDirectory });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>Wenn code.cmd fehlt, wird code im PATH akzeptiert.</summary>
    [Fact]
    public void Locate_FindetCodeInPath()
    {
        var pathDirectory = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_path");
        var executable = Path.Combine(pathDirectory, "code");
        File.WriteAllText(executable, string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?> { ["PATH"] = pathDirectory });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>Typische Windows-Installationspfade bevorzugen Code.exe vor code.cmd.</summary>
    [Fact]
    public void Locate_BevorzugtCodeExeImBekanntenWindowsInstallationspfad()
    {
        var localAppData = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_localappdata");
        var executable = Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe");
        var command = Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(command)!);
        File.WriteAllText(executable, string.Empty);
        File.WriteAllText(command, string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?>
        {
            ["PATH"] = null,
            ["LOCALAPPDATA"] = localAppData
        });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>code.cmd im Benutzerinstallationspfad bleibt Fallback, wenn Code.exe fehlt.</summary>
    [Fact]
    public void Locate_FindetCodeCmdImBenutzerInstallationspfadWennCodeExeFehlt()
    {
        var localAppData = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_localappdata");
        var executable = Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?>
        {
            ["PATH"] = null,
            ["LOCALAPPDATA"] = localAppData
        });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>Die systemweite Installation unter ProgramFiles wird erkannt.</summary>
    [Fact]
    public void Locate_FindetSystemweitenProgramFilesInstallationspfad()
    {
        var programFiles = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_programfiles");
        var executable = Path.Combine(programFiles, "Microsoft VS Code", "bin", "code.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?>
        {
            ["PATH"] = null,
            ["LOCALAPPDATA"] = null,
            ["ProgramFiles"] = programFiles
        });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>Die systemweite 32-Bit-Installation unter ProgramFiles(x86) wird erkannt.</summary>
    [Fact]
    public void Locate_FindetSystemweitenProgramFilesX86Installationspfad()
    {
        var programFilesX86 = _tempDirectoryFixture.CreateTempDirectory("vscode_locator_programfilesx86");
        var executable = Path.Combine(programFilesX86, "Microsoft VS Code", "Code.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, string.Empty);
        var locator = CreateLocator(new Dictionary<string, string?>
        {
            ["PATH"] = null,
            ["LOCALAPPDATA"] = null,
            ["ProgramFiles"] = null,
            ["ProgramFiles(x86)"] = programFilesX86
        });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeTrue();
        availability.ExecutablePath.Should().Be(executable);
    }

    /// <summary>Ohne PATH und bekannte Pfade ist VS Code nicht verfügbar.</summary>
    [Fact]
    public void Locate_OhneTreffer_IstNichtVerfuegbar()
    {
        var locator = CreateLocator(new Dictionary<string, string?>
        {
            ["PATH"] = null,
            ["LOCALAPPDATA"] = null,
            ["ProgramFiles"] = null,
            ["ProgramFiles(x86)"] = null
        });

        var availability = locator.Locate();

        availability.IsAvailable.Should().BeFalse();
        availability.ExecutablePath.Should().BeNull();
    }

    private static VisualStudioCodeLocator CreateLocator(IReadOnlyDictionary<string, string?> environment)
        => new(name => environment.TryGetValue(name, out var value) ? value : null, File.Exists);
}
