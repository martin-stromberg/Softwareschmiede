using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.IntegrationTests.Services;

public sealed partial class ScmEventCliExchangeAccessTests
{
    /// <summary>A normal CLI parent delegates the actual access probe and channel I/O to its restricted file-tool child.</summary>
    [Theory]
    [InlineData("Codex", true)]
    [InlineData("Codex", false)]
    [InlineData("ClaudeCli", true)]
    [InlineData("ClaudeCli", false)]
    public async Task RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData(string provider, bool rename)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The mandatory restricted process proof requires Windows and must not silently skip.");
        var workspace = Directory.CreateDirectory(Path.Combine(_root, "workspace")).FullName;
        var appData = Directory.CreateDirectory(Path.Combine(_root, "app-data")).FullName;
        var sid = "S-1-5-21-" + string.Join('-', Enumerable.Range(0, 4).Select(_ => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4))));
        ScmRestrictedProcessAccess.ValidateRestrictingSid(sid);
        RestrictDirectory(workspace, sid);
        RestrictDirectory(appData);
        await File.WriteAllTextAsync(Path.Combine(appData, "host-secret.txt"), "host-only");
        await GitInit(workspace);

        var request = new ScmExchangeRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "$Softwareschmiede requirement", "IssueComment", "owner/repo", "https://github.com/owner/repo/issues/7#issuecomment-9");
        var directory = Path.Combine(workspace, ScmEventExchangeProtocol.DirectoryName, request.CliRunId.ToString("D"), request.AttemptId.ToString("D"));
        var actionId = Guid.NewGuid();
        var diagnostic = Path.Combine(workspace, "child-error.txt");
        static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        var command = $$"""
            $ErrorActionPreference = 'Stop'
            $channel = {{Quote(directory)}}
            $workspace = {{Quote(workspace)}}
            try {
              $deadline = [DateTime]::UtcNow.AddSeconds(60)
              $probePromptPath = [IO.Path]::Combine($workspace, 'probe-prompt.json')
              while (-not [IO.File]::Exists($probePromptPath)) {
                if ([DateTime]::UtcNow -gt $deadline) { throw 'Host did not request file-tool probe' }
                Start-Sleep -Milliseconds 25
              }
              $probe = [IO.File]::ReadAllText($probePromptPath) | ConvertFrom-Json
              $challenge = [IO.File]::ReadAllBytes($probe.challengePath)
              [IO.File]::WriteAllBytes($probe.temporaryProofPath, $challenge)
              [IO.File]::Move($probe.temporaryProofPath, $probe.proofPath)
              [IO.File]::Move($probe.challengePath, $probe.readChallengePath)
              while (-not [IO.File]::Exists([IO.Path]::Combine($workspace, 'go'))) {
                if ([DateTime]::UtcNow -gt $deadline) { throw 'Host did not prepare channel' }
                Start-Sleep -Milliseconds 25
              }
              $denied = $false
              try { [IO.File]::WriteAllText({{Quote(Path.Combine(appData, "forbidden.txt"))}}, 'must fail') }
              catch [System.UnauthorizedAccessException] { $denied = $true }
              if (-not $denied) { throw 'Restriction NOT enforced against app-data' }
              $req = [IO.File]::ReadAllText([IO.Path]::Combine($channel, 'request.json')) | ConvertFrom-Json
              function Publish($path, $value) {
                $json = $value | ConvertTo-Json -Depth 10 -Compress
                if ({{(rename ? "$true" : "$false")}}) {
                  [IO.File]::WriteAllText($path + '.tmp', $json)
                  [IO.File]::Move($path + '.tmp', $path)
                } else { [IO.File]::WriteAllText($path, $json) }
              }
              $accepted = @{ protocolVersion=1; eventId=$req.eventId; attemptId=$req.attemptId; cliRunId=$req.cliRunId }
              Publish ([IO.Path]::Combine($channel, 'accepted.json')) $accepted
              $action = $accepted.Clone()
              $action.actionId = '{{actionId}}'
              $action.kind = 'comment'
              $action.body = 'Restricted process reply'
              Publish ([IO.Path]::Combine($channel, 'actions', '{{actionId}}.json')) $action
              $resultPath = [IO.Path]::Combine($channel, 'results', '{{actionId}}.json')
              while (-not [IO.File]::Exists($resultPath)) {
                if ([DateTime]::UtcNow -gt $deadline) { throw 'Host did not acknowledge remote action' }
                Start-Sleep -Milliseconds 25
              }
              $result = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
              if (-not $result.success -or $result.eventId -ne $req.eventId) { throw 'Host action failed' }
              $complete = $accepted.Clone()
              $complete.actionIds = @('{{actionId}}')
              $complete.noRemoteAction = $false
              Publish ([IO.Path]::Combine($channel, 'complete.json')) $complete
              exit 0
            } catch {
              [IO.File]::WriteAllText({{Quote(diagnostic)}}, $_.Exception.ToString())
              exit 1
            }
            """;
        using var process = ScmRestrictedWindowsProcess.Start(workspace, command, sid);
        try
        {
            var (prefix, originalSpec) = await Spec(provider, provider == "Codex" ? "--sandbox workspace-write" : "--permission-mode acceptEdits");
            var spec = originalSpec with { WorkingDirectory = workspace };
            var session = Session(provider == "Codex" ? "OpenAI Codex (v0.114.0)" : "Claude Code v2.1.0", provider == "Codex" ? "›" : "❯");
            session.Object.Process.Id.Should().NotBe(process.Id, "the CLI parent is distinct from the restricted tool child");
            ScmRestrictedProcessAccess.Capture(session.Object.Process).Should().BeEmpty("the normal parent cannot prove the tool token");
            session.Setup(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string prompt, CancellationToken token) => File.WriteAllTextAsync(Path.Combine(workspace, "probe-prompt.json"), prompt, token));
            using var adapter = new ScmPromptSessionAdapter(request.AufgabeId, request.CliRunId, prefix, spec, session.Object, [], () => !process.HasExited);
            var access = await adapter.CheckExchangeAccessAsync(directory);
            access.Status.Should().Be(ScmExchangeAccessStatus.Unknown, "only the real file tool may complete the probe");
            var probeDeadline = DateTime.UtcNow.AddSeconds(30);
            while (access.Status == ScmExchangeAccessStatus.Unknown && !process.HasExited && DateTime.UtcNow < probeDeadline)
            {
                await Task.Delay(25);
                access = await adapter.CheckExchangeAccessAsync(directory);
            }
            access.Status.Should().Be(ScmExchangeAccessStatus.Supported, access.Reason);
            access.RestrictedWriteSids.Should().ContainSingle().Which.Should().Be(sid);
            session.Raise(value => value.BufferChanged += null, EventArgs.Empty);
            adapter.ReadyStatus.Should().Be(ScmPromptReadyStatus.Ready);
            var protocol = new ScmEventExchangeProtocol();
            var attempt = await protocol.PrepareAsync(request, workspace, restrictedWriteSids: access.RestrictedWriteSids);
            await File.WriteAllTextAsync(Path.Combine(workspace, "go"), "go");
            var deadline = DateTime.UtcNow.AddSeconds(30);
            ScmExchangeMessages messages;
            do
            {
                messages = await protocol.ReadAsync(attempt);
                if (messages.Actions.Count == 1) break;
                if (process.HasExited) break;
                await Task.Delay(25);
            } while (DateTime.UtcNow < deadline);
            var error = File.Exists(diagnostic) ? await File.ReadAllTextAsync(diagnostic) : messages.Diagnostic;
            messages.Actions.Should().ContainSingle(error);
            messages.Complete.Should().BeNull("completion must wait for host acknowledgement");
            messages.Accepted.Should().NotBeNull();
            messages.Actions[0].Body.Should().Be("Restricted process reply");
            // The host responder uses the stored origin; child documents cannot select another remote target.
            request.OriginUrl.Should().Be("https://github.com/owner/repo/issues/7#issuecomment-9");
            await protocol.WriteResultAsync(attempt, new ScmExchangeActionResult(1, request.EventId, request.AttemptId, request.CliRunId, actionId, true, "comment-1"));
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            process.ExitCode.Should().Be(0, File.Exists(diagnostic) ? await File.ReadAllTextAsync(diagnostic) : "restricted child must finish");
            await protocol.ReadAsync(attempt);
            (await protocol.ReadAsync(attempt)).Complete!.ActionIds.Should().Equal(actionId);
            File.Exists(Path.Combine(appData, "forbidden.txt")).Should().BeFalse();
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    /// <summary>Real accounts, local-domain identities and broad token helpers never become channel grants.</summary>
    [Fact]
    public void RestrictingSid_RejectsAccountsAndBroadPrincipals()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The SID proof requires Windows.");
        using var identity = WindowsIdentity.GetCurrent();
        string[] rejected = [identity.User!.Value, identity.User.AccountDomainSid!.Value + "-4294967294",
            "S-1-1-0", "S-1-5-11", "S-1-5-32-545", "S-1-5-5-1-2", "S-1-15-3-1024-1-2-3-4-5-6-7-8"];
        foreach (var sid in rejected)
        {
            Action validate = () => ScmRestrictedProcessAccess.ValidateRestrictingSid(sid);
            validate.Should().Throw<UnauthorizedAccessException>(sid + " must never be a dedicated channel grant");
        }
    }

    /// <summary>A real restricted child denied by the workspace cannot claim access through its normal CLI parent.</summary>
    [Theory]
    [InlineData("Codex")]
    [InlineData("ClaudeCli")]
    public async Task RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel(string provider)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The restricted process proof requires Windows.");
        await GitInit(_root);
        var sid = "S-1-5-21-" + string.Join('-', Enumerable.Range(0, 4).Select(_ => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4))));
        // The child may initialize in this workspace, but its non-inheritable right must never
        // be promoted into an explicit grant on the private channel's descendants.
        RestrictDirectory(_root, sid, inheritCapability: false);
        var (prefix, spec) = await Spec(provider, provider == "Codex" ? "--sandbox workspace-write" : "--permission-mode acceptEdits");
        var session = Session(provider == "Codex" ? "OpenAI Codex (v0.114.0)" : "Claude Code v2.1.0", provider == "Codex" ? "›" : "❯");
        using var adapter = new ScmPromptSessionAdapter(Guid.NewGuid(), Guid.NewGuid(), prefix, spec, session.Object, [], () => true);
        var channel = Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName);
        (await adapter.CheckExchangeAccessAsync(channel)).Status.Should().Be(ScmExchangeAccessStatus.Unknown);
        var sent = (string)session.Invocations.Single(call => call.Method.Name == nameof(ITerminalSession.WritePromptAsync)).Arguments[0]!;
        using var document = JsonDocument.Parse(sent);
        static string Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
        var challenge = document.RootElement.GetProperty("challengePath").GetString()!;
        var temporary = document.RootElement.GetProperty("temporaryProofPath").GetString()!;
        var command = $$"""
            $ErrorActionPreference = 'Stop'
            try {
              $bytes = [IO.File]::ReadAllBytes({{Quote(challenge)}})
              [IO.File]::WriteAllBytes({{Quote(temporary)}}, $bytes)
              exit 9
            } catch [System.UnauthorizedAccessException] { exit 5 }
            catch { [IO.File]::WriteAllText({{Quote(Path.Combine(_root, "denial-error.txt"))}}, $_.Exception.ToString()); exit 6 }
            """;
        using var process = ScmRestrictedWindowsProcess.Start(_root, command, sid);
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            process.ExitCode.Should().Be(5, File.Exists(Path.Combine(_root, "denial-error.txt"))
                ? await File.ReadAllTextAsync(Path.Combine(_root, "denial-error.txt")) : "the actual file tool is denied despite a normal CLI parent");
            File.Exists(temporary).Should().BeFalse();
            var access = await adapter.CheckExchangeAccessAsync(channel);
            access.Status.Should().Be(ScmExchangeAccessStatus.Unknown);
            access.RestrictedWriteSids.Should().BeEmpty();
            session.Verify(value => value.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
                "a failed probe must not trigger another prompt or an SCM event");
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictDirectory(string path, string? restrictedSid = null, bool inheritCapability = true)
    {
        var user = WindowsIdentity.GetCurrent().User!;
        var acl = new DirectorySecurity();
        acl.SetOwner(user);
        acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        if (restrictedSid is not null)
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(restrictedSid), FileSystemRights.Modify | FileSystemRights.Synchronize,
                inheritCapability ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }

    private static async Task GitInit(string directory)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("init");
        start.ArgumentList.Add("--quiet");
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(0);
    }
}
