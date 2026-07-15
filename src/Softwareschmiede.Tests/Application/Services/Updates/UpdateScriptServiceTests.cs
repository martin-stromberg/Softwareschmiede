using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdateScriptService"/>.</summary>
public sealed class UpdateScriptServiceTests
{
    /// <summary>Das Skript enthält Parameter und Kopier-/Startlogik für den externen Austausch.</summary>
    [Fact]
    public async Task CreateScriptAsync_ShouldWritePowerShellUpdaterScript()
    {
        using var temp = new TempDirectory();
        var sut = new UpdateScriptService(
            new CapturingLauncher(),
            Options.Create(new UpdateOptions()),
            NullLogger<UpdateScriptService>.Instance,
            temp.Path);

        var scriptPath = await sut.CreateScriptAsync(temp.Path, Path.Combine(temp.Path, "extracted"), "Softwareschmiede.exe", Path.Combine(temp.Path, "update.log"));

        var script = await File.ReadAllTextAsync(scriptPath);
        script.Should().Contain("param(");
        script.Should().Contain("[int]$AppPid");
        script.Should().Contain("[string]$TargetDirectory");
        script.Should().Contain("[string]$ExtractedDirectory");
        script.Should().Contain("[string]$ExecutablePath");
        script.Should().Contain("[string]$LogPath");
        script.Should().Contain("Write-UpdateLog");
        script.Should().Contain("$process.WaitForExit(15000)");
        script.Should().Contain("Stop-Process -Id $AppPid -Force");
        script.Should().Contain("Copy-Item");
        script.Should().Contain("Start-Process -FilePath $ExecutablePath -WorkingDirectory $TargetDirectory");
        script.Should().Contain("Update erfolgreich abgeschlossen.");
        script.Should().Contain("Update fehlgeschlagen:");
    }

    /// <summary>Der Prozessstarter erhält Argumente ohne Shell-String-Verkettung und setzt Elevation durch.</summary>
    [Fact]
    public async Task StartScriptAsync_ShouldPassArgumentsToLauncher()
    {
        using var temp = new TempDirectory();
        var launcher = new CapturingLauncher();
        var sut = new UpdateScriptService(
            launcher,
            Options.Create(new UpdateOptions()),
            NullLogger<UpdateScriptService>.Instance,
            temp.Path);
        var preparation = new UpdatePreparationResult(
            Path.Combine(temp.Path, "download", "release.zip"),
            Path.Combine(temp.Path, "extracted", "1.2.3"),
            Path.Combine(temp.Path, "updates", "update.ps1"),
            Path.Combine(temp.Path, "updates", "update.log"),
            RequiresElevation: true);

        await sut.StartScriptAsync(preparation);

        launcher.FileName.Should().NotBeNullOrWhiteSpace();
        launcher.RunElevated.Should().BeTrue();
        launcher.Arguments.Should().Contain("-File");
        launcher.Arguments.Should().Contain(preparation.ScriptPath);
        launcher.Arguments.Should().Contain("-AppPid");
        launcher.Arguments.Should().Contain(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        launcher.Arguments.Should().Contain("-TargetDirectory");
        launcher.Arguments.Should().Contain(temp.Path);
        launcher.Arguments.Should().Contain("-ExtractedDirectory");
        launcher.Arguments.Should().Contain(preparation.ExtractedDirectory);
        launcher.Arguments.Should().Contain("-ExecutablePath");
        launcher.Arguments.Should().Contain(Path.Combine(temp.Path, "Softwareschmiede.exe"));
        launcher.Arguments.Should().Contain("-LogPath");
        launcher.Arguments.Should().Contain(preparation.LogPath);
    }

    /// <summary>Wenn der erste PowerShell-Kandidat fehlt, wird ein verfügbarer Fallback verwendet.</summary>
    [Fact]
    public async Task StartScriptAsync_ShouldFallbackToPwshCandidate_WhenPowerShellCandidateIsMissing()
    {
        using var temp = new TempDirectory();
        var pwshPath = Path.Combine(temp.Path, "pwsh.exe");
        await File.WriteAllTextAsync(pwshPath, string.Empty);
        var launcher = new CapturingLauncher();
        var sut = new UpdateScriptService(
            launcher,
            Options.Create(new UpdateOptions()),
            NullLogger<UpdateScriptService>.Instance,
            temp.Path,
            [Path.Combine(temp.Path, "missing-powershell.exe"), pwshPath]);
        var preparation = CreatePreparation(temp.Path, requiresElevation: false);

        await sut.StartScriptAsync(preparation);

        launcher.FileName.Should().Be(pwshPath);
    }

    /// <summary>Ein fehlgeschlagener Prozessstart wird als Exception an den Update-Service gemeldet.</summary>
    [Fact]
    public async Task StartScriptAsync_ShouldThrow_WhenLauncherReportsNoProcess()
    {
        using var temp = new TempDirectory();
        var sut = new UpdateScriptService(
            new FailingLauncher(),
            Options.Create(new UpdateOptions()),
            NullLogger<UpdateScriptService>.Instance,
            temp.Path);
        var preparation = CreatePreparation(temp.Path, requiresElevation: false);

        var act = () => sut.StartScriptAsync(preparation);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht gestartet*");
    }

    private static UpdatePreparationResult CreatePreparation(string basePath, bool requiresElevation)
        => new(
            Path.Combine(basePath, "download", "release.zip"),
            Path.Combine(basePath, "extracted path", "1.2.3"),
            Path.Combine(basePath, "updates path", "update.ps1"),
            Path.Combine(basePath, "updates path", "update.log"),
            requiresElevation);

    private sealed class CapturingLauncher : IUpdateProcessLauncher
    {
        public string? FileName { get; private set; }

        public List<string> Arguments { get; } = [];

        public bool RunElevated { get; private set; }

        public bool Start(string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated)
        {
            FileName = fileName;
            Arguments.AddRange(arguments);
            RunElevated = runElevated;
            return true;
        }
    }

    private sealed class FailingLauncher : IUpdateProcessLauncher
    {
        public bool Start(string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated) => false;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
