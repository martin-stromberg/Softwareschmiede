using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Deterministisches KI-Simulator-Plugin für Tests und lokale Entwicklung.</summary>
public sealed class KiSimulatorPlugin : CliKiPluginBase, IIssueTemplateTextGenerator
{
    private readonly ILogger<KiSimulatorPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "KI Simulator";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "simulator";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.KiSimulator";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public override string PluginSymbol => "🧪";

    /// <summary>Erstellt eine neue Instanz von <see cref="KiSimulatorPlugin"/>.</summary>
    public KiSimulatorPlugin(ILogger<KiSimulatorPlugin> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => false;

    /// <inheritdoc/>
    public override Task<bool> CheckHealthAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
        => Task.FromResult($"KI-Simulator Ergebnis fuer Template:\n{templateBody}");

    /// <inheritdoc/>
    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    {
        _logger.LogInformation(
            "KiSimulator BuildProcessStartInfo (Repo: {RepoPath}, Parameters: {Parameters}).",
            localRepoPath,
            parameters);

        var fixtureDirectory = GetScmFixtureDirectory();
        if (fixtureDirectory is not null || string.Equals(parameters?.Trim(), "--scm-exchange", StringComparison.Ordinal))
        {
            if (fixtureDirectory is not null && File.Exists(Path.Combine(fixtureDirectory, "fail-start")))
                throw new InvalidOperationException("KI-Simulator: kontrollierter Startfehler.");
            var scm = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(ScmSimulatorProgram)),
                WorkingDirectory = localRepoPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            scm.EnvironmentVariables["SOFTWARESCHMIEDE_SCM_SIMULATOR"] = "1";
            if (fixtureDirectory is not null)
            {
                scm.EnvironmentVariables["SOFTWARESCHMIEDE_SCM_SIMULATOR_FIXTURE"] = fixtureDirectory;
                if (File.Exists(Path.Combine(fixtureDirectory, "read-only")))
                    scm.EnvironmentVariables["SOFTWARESCHMIEDE_SCM_SIMULATOR_READONLY"] = "1";
            }
            return scm;
        }

        // Interaktive Shell statt /c-One-Shot: /k gibt das Banner aus und bleibt geöffnet,
        // damit stdin-Befehle (echo-Marker, exit) zeilenweise ausgeführt werden.
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/k \"echo KI-Simulator läuft...\"",
            WorkingDirectory = localRepoPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
    }

    private static string? GetScmFixtureDirectory()
    {
        var configPath = Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_SCM_TEST_CONFIG");
        var databasePath = Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH");
        if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(databasePath)) return null;
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = Path.GetFullPath(config.RootElement.GetProperty("TestRoot").GetString()!);
        var relativeDatabase = Path.GetRelativePath(root, Path.GetFullPath(databasePath));
        if (Path.IsPathRooted(relativeDatabase) || relativeDatabase == ".." || relativeDatabase.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Simulator-Fixture benötigt eine isolierte Testdatenbank.");
        return Path.Combine(root, "simulator");
    }

    // Fixed program only. Remote text is parsed as JSON from stdin and is never evaluated as PowerShell.
    private const string ScmSimulatorProgram = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
        $scmRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::CurrentDirectory) '.softwareschmiede-scm-exchange')) + [IO.Path]::DirectorySeparatorChar
        $scmFixture = [Environment]::GetEnvironmentVariable('SOFTWARESCHMIEDE_SCM_SIMULATOR_FIXTURE')
        function Wait-ScmFixture([string] $gate) {
            if (-not [string]::IsNullOrWhiteSpace($scmFixture)) {
                while ([IO.File]::Exists((Join-Path $scmFixture ('hold-' + $gate)))) { [Threading.Thread]::Sleep(50) }
            }
        }
        function Get-ScmNativePath([string] $path) {
            if ($path.StartsWith('\\?\')) { return $path }
            if ($path.StartsWith('\\')) { return '\\?\UNC\' + $path.Substring(2) }
            return '\\?\' + $path
        }
        function Publish-ScmJson([string] $path, $value) {
            $temporary = [IO.Path]::Combine([IO.Path]::GetDirectoryName($path), ([Guid]::NewGuid().ToString('N') + '.tmp'))
            [IO.File]::WriteAllText((Get-ScmNativePath $temporary), ($value | ConvertTo-Json -Depth 16 -Compress), (New-Object System.Text.UTF8Encoding($false)))
            [IO.File]::Move((Get-ScmNativePath $temporary), (Get-ScmNativePath $path))
        }
        [Console]::WriteLine('SCM_SIMULATOR_PROTOCOL_V1')
        while ($true) {
            if ([string]::IsNullOrWhiteSpace($scmFixture) -or -not [IO.File]::Exists((Join-Path $scmFixture 'unknown-ready'))) {
                [Console]::Write('SCM_READY_V1')
            }
            $scmLine = [Console]::ReadLine()
            if ($null -eq $scmLine) { break }
            if ($scmLine -eq 'exit') { break }
            [Console]::WriteLine('')
            [Console]::WriteLine('SCM_BUSY_V1')
            try {
                $envelope = $scmLine | ConvertFrom-Json
                $requestPath = [IO.Path]::GetFullPath([string]$envelope.requestPath)
                if (-not $requestPath.StartsWith($scmRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid exchange path' }
                if ([IO.Path]::GetFileName($requestPath) -ne 'request.json') { throw 'Invalid request file' }
                $request = [IO.File]::ReadAllText((Get-ScmNativePath $requestPath)) | ConvertFrom-Json
                foreach ($key in @('eventId', 'attemptId', 'cliRunId')) {
                    if ([Guid]$request.$key -eq [Guid]::Empty -or $request.$key -ne $envelope.request.$key) { throw 'Correlation mismatch' }
                }
                $directory = [IO.Path]::GetDirectoryName($requestPath)
                if (-not [string]::IsNullOrWhiteSpace($scmFixture)) {
                    [IO.File]::AppendAllText((Join-Path $scmFixture 'prompts.jsonl'), ($scmLine + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
                    $ignore = Join-Path $scmFixture 'ignore-next-attempt'
                    if ([IO.File]::Exists($ignore)) {
                        [IO.File]::Delete($ignore)
                        # An independent worker of this actual CLI publishes a deliberately late,
                        # correctly correlated acknowledgement when the fixture releases this attempt.
                        $null = Start-Job -ArgumentList $scmFixture, $directory, $request.eventId, $request.attemptId, $request.cliRunId -ScriptBlock {
                            param($fixture, $directory, $eventId, $attemptId, $cliRunId)
                            $release = Join-Path $fixture ('complete-attempt-' + $attemptId)
                            while (-not [IO.File]::Exists($release)) { [Threading.Thread]::Sleep(50) }
                            $correlation = @{ protocolVersion = 1; eventId = $eventId; attemptId = $attemptId; cliRunId = $cliRunId }
                            $accepted = Join-Path $directory 'accepted.json'
                            $complete = Join-Path $directory 'complete.json'
                            if (-not $accepted.StartsWith('\\?\')) { $accepted = '\\?\' + $accepted; $complete = '\\?\' + $complete }
                            [IO.File]::WriteAllText(($accepted + '.tmp'), ($correlation | ConvertTo-Json -Compress))
                            [IO.File]::Move(($accepted + '.tmp'), $accepted)
                            $correlation.actionIds = @()
                            $correlation.noRemoteAction = $true
                            $correlation.reason = 'Kontrollierter verspäteter Abschluss ohne weitere Remoteaktion.'
                            [IO.File]::WriteAllText(($complete + '.tmp'), ($correlation | ConvertTo-Json -Compress))
                            [IO.File]::Move(($complete + '.tmp'), $complete)
                        }
                        continue
                    }
                }
                $correlation = @{ protocolVersion = 1; eventId = $request.eventId; attemptId = $request.attemptId; cliRunId = $request.cliRunId }
                Wait-ScmFixture 'accepted'
                Publish-ScmJson (Join-Path $directory 'accepted.json') $correlation
                $actionId = [Guid]::NewGuid().ToString('D')
                $action = $correlation.Clone()
                $action.actionId = $actionId
                $action.kind = 'comment'
                $action.body = 'KI-Simulator: SCM-Ereignis verarbeitet.'
                Publish-ScmJson (Join-Path $directory ('actions/' + $actionId + '.json')) $action
                $resultPath = Join-Path $directory ('results/' + $actionId + '.json')
                $deadline = [DateTime]::UtcNow.AddMinutes(30)
                $succeeded = $false
                while ([DateTime]::UtcNow -lt $deadline) {
                    if ([IO.File]::Exists((Get-ScmNativePath $resultPath))) {
                        try {
                            $result = [IO.File]::ReadAllText((Get-ScmNativePath $resultPath)) | ConvertFrom-Json
                            if ($result.success -eq $true -and $result.eventId -eq $request.eventId -and $result.attemptId -eq $request.attemptId -and $result.cliRunId -eq $request.cliRunId -and $result.actionId -eq $actionId) { $succeeded = $true; break }
                        } catch { }
                    }
                    [Threading.Thread]::Sleep(100)
                }
                if ($succeeded) {
                    Wait-ScmFixture 'complete'
                    $complete = $correlation.Clone()
                    $complete.actionIds = @($actionId)
                    $complete.noRemoteAction = $false
                    Publish-ScmJson (Join-Path $directory 'complete.json') $complete
                }
            } catch {
                if (-not [string]::IsNullOrWhiteSpace($scmFixture)) {
                    [IO.File]::AppendAllText((Join-Path $scmFixture 'errors.log'), ($_.Exception.Message + [Environment]::NewLine))
                }
                [Console]::WriteLine('SCM_SIMULATOR_EVENT_ERROR')
            }
        }
        """;
}
