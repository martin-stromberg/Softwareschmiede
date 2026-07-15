using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Infrastructure.Services.Updates;

/// <summary>Erzeugt und startet das PowerShell-Skript für den finalen Dateiaustausch.</summary>
public sealed class UpdateScriptService : IUpdateScriptService
{
    private readonly IUpdateProcessLauncher _processLauncher;
    private readonly UpdateOptions _options;
    private readonly ILogger<UpdateScriptService> _logger;
    private readonly string _baseDirectory;
    private readonly IReadOnlyList<string> _powerShellCandidates;

    /// <inheritdoc cref="UpdateScriptService"/>
    public UpdateScriptService(
        IUpdateProcessLauncher processLauncher,
        IOptions<UpdateOptions> options,
        ILogger<UpdateScriptService> logger)
        : this(processLauncher, options, logger, AppContext.BaseDirectory)
    {
    }

    /// <summary>Erstellt den Service mit explizitem Basispfad für Tests.</summary>
    public UpdateScriptService(
        IUpdateProcessLauncher processLauncher,
        IOptions<UpdateOptions> options,
        ILogger<UpdateScriptService> logger,
        string baseDirectory,
        IReadOnlyList<string>? powerShellCandidates = null)
    {
        _processLauncher = processLauncher;
        _options = options.Value;
        _logger = logger;
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _powerShellCandidates = powerShellCandidates ?? GetDefaultPowerShellCandidates();
    }

    /// <inheritdoc/>
    public async Task<string> CreateScriptAsync(
        string targetDirectory,
        string extractedDirectory,
        string executableName,
        string logPath,
        CancellationToken ct = default)
    {
        var updateRoot = Path.Combine(_baseDirectory, _options.UpdateDirectoryName);
        Directory.CreateDirectory(updateRoot);
        var scriptPath = Path.Combine(updateRoot, "update.ps1");
        var script = BuildScript();
        await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
        return scriptPath;
    }

    /// <inheritdoc/>
    public Task StartScriptAsync(UpdatePreparationResult preparation, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var executable = FindPowerShellExecutable(_powerShellCandidates);
        var currentPid = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var executablePath = Path.Combine(_baseDirectory, _options.ExecutableName);
        var arguments = new[]
        {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            preparation.ScriptPath,
            "-AppPid",
            currentPid,
            "-TargetDirectory",
            _baseDirectory,
            "-ExtractedDirectory",
            preparation.ExtractedDirectory,
            "-ExecutablePath",
            executablePath,
            "-LogPath",
            preparation.LogPath
        };

        if (!_processLauncher.Start(executable, arguments, _baseDirectory, preparation.RequiresElevation))
            throw new InvalidOperationException("Update-Skript konnte nicht gestartet werden.");

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> GetDefaultPowerShellCandidates()
    {
        var windowsPowerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        return [windowsPowerShell, "powershell.exe", "pwsh.exe", "pwsh"];
    }

    private static string FindPowerShellExecutable(IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (IsExecutableAvailable(candidate))
                return candidate;
        }

        return "powershell.exe";
    }

    private static bool IsExecutableAvailable(string executable)
    {
        if (Path.IsPathFullyQualified(executable))
            return File.Exists(executable);

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [string.Empty];

        foreach (var pathEntry in pathEntries)
        {
            foreach (var extension in extensions)
            {
                var fileName = Path.HasExtension(executable) ? executable : executable + extension;
                if (File.Exists(Path.Combine(pathEntry, fileName)))
                    return true;
            }
        }

        return false;
    }

    private static string BuildScript()
    {
        return """
param(
    [Parameter(Mandatory = $true)][int]$AppPid,
    [Parameter(Mandatory = $true)][string]$TargetDirectory,
    [Parameter(Mandatory = $true)][string]$ExtractedDirectory,
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [Parameter(Mandatory = $true)][string]$LogPath
)

$ErrorActionPreference = 'Stop'

function Write-UpdateLog {
    param([string]$Message)
    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    Add-Content -LiteralPath $LogPath -Value "[$timestamp] $Message"
}

try {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
    Write-UpdateLog "Update-Skript gestartet."

    $process = Get-Process -Id $AppPid -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        Write-UpdateLog "Warte auf Beenden der Anwendung PID $AppPid."
        if (-not $process.WaitForExit(15000)) {
            Write-UpdateLog "Beende Anwendung nach Timeout."
            Stop-Process -Id $AppPid -Force -ErrorAction SilentlyContinue
        }
    }

    Write-UpdateLog "Kopiere Dateien aus '$ExtractedDirectory' nach '$TargetDirectory'."
    Get-ChildItem -LiteralPath $ExtractedDirectory -Force | Copy-Item -Destination $TargetDirectory -Recurse -Force

    Write-UpdateLog "Starte Anwendung '$ExecutablePath'."
    Start-Process -FilePath $ExecutablePath -WorkingDirectory $TargetDirectory
    Write-UpdateLog "Update erfolgreich abgeschlossen."
    exit 0
}
catch {
    Write-UpdateLog "Update fehlgeschlagen: $($_.Exception.Message)"
    exit 1
}
""";
    }
}

/// <summary>Startet externe Update-Prozesse über <see cref="ProcessStartInfo"/>.</summary>
public sealed class UpdateProcessLauncher : IUpdateProcessLauncher
{
    /// <inheritdoc/>
    public bool Start(string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = runElevated,
            Verb = runElevated ? "runas" : string.Empty
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            throw new InvalidOperationException($"Update-Prozess konnte nicht gestartet werden: {fileName}", ex);
        }
    }
}
