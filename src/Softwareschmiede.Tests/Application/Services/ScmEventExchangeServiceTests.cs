using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Exercises the real workspace-local exchange including Git exclusion and hostile messages.</summary>
public sealed class ScmEventExchangeServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scm-channel-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ScmEventExchangeProtocol _protocol = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Creates an isolated repository, without credentials or a remote.</summary>
    public ScmEventExchangeServiceTests()
    {
        Directory.CreateDirectory(_root);
        Git("init", "--quiet");
    }

    /// <summary>The full original text is JSON data and never terminal control bytes.</summary>
    [Fact]
    public async Task Prepare_ExcludesChannelAndEscapesAllTerminalControls()
    {
        var body = "\"remote\"\r\n" + new string(Enumerable.Range(0, 160).Where(value => value < 32 || value >= 127).Select(value => (char)value).ToArray());
        var request = Request(body);
        var attempt = await _protocol.PrepareAsync(request, _root);
        attempt.Prompt.Any(char.IsControl).Should().BeFalse();
        using var document = JsonDocument.Parse(attempt.Prompt);
        document.RootElement.GetProperty("request").GetProperty("body").GetString().Should().Be(body);
        JsonSerializer.Deserialize<ScmExchangeRequest>(await File.ReadAllTextAsync(attempt.RequestPath), Json).Should().Be(request);
        Git("check-ignore", "--", attempt.RequestPath).Should().Contain("request.json");
        Git("ls-files", "--", ScmEventExchangeProtocol.DirectoryName).Should().BeEmpty();
        Git("status", "--porcelain").Should().BeEmpty();
    }

    /// <summary>Ein konfigurierter Anhang erscheint als eigenes promptSuffix-Feld nur im Prompt —
    /// request.json und der unveränderliche Auftrag bleiben frei davon; Zeilenumbrüche bleiben JSON-Daten.</summary>
    [Fact]
    public async Task Prepare_PromptSuffixAppearsOnlyInPrompt_NotInStoredRequest()
    {
        var suffix = "Bearbeitungsregeln:\nImmer Tests ausführen.";
        var request = Request();
        var attempt = await _protocol.PrepareAsync(request, _root, promptSuffix: suffix);
        attempt.PromptSuffix.Should().Be(suffix);
        attempt.Prompt.Any(char.IsControl).Should().BeFalse("der Anhang muss einzeilig im JSON-Prompt kodiert bleiben");
        using var document = JsonDocument.Parse(attempt.Prompt);
        document.RootElement.GetProperty("promptSuffix").GetString().Should().Be(suffix);
        document.RootElement.GetProperty("request").GetProperty("body").GetString().Should().Be(request.Body);
        JsonSerializer.Deserialize<ScmExchangeRequest>(await File.ReadAllTextAsync(attempt.RequestPath), Json).Should().Be(request);
    }

    /// <summary>Ohne Anhang bleibt der Prompt unverändert: Es gibt kein leeres promptSuffix-Feld.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Prepare_EmptyPromptSuffix_OmitsField(string? suffix)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, promptSuffix: suffix);
        using var document = JsonDocument.Parse(attempt.Prompt);
        document.RootElement.TryGetProperty("promptSuffix", out _).Should().BeFalse();
    }

    /// <summary>Die lokale Kanalprobe ist kein SCM-Ereignis und erhält grundsätzlich keinen Anhang.</summary>
    [Fact]
    public void AccessProbePrompt_NeverCarriesAPromptSuffix()
    {
        var probe = new ScmExchangeAccessProbe(Guid.NewGuid(), "nonce", "fingerprint",
            Path.Combine(_root, "probe"), []);
        using var document = JsonDocument.Parse(ScmEventExchangeProtocol.BuildAccessProbePrompt(probe));
        document.RootElement.TryGetProperty("promptSuffix", out _).Should().BeFalse();
    }

    /// <summary>An occupied or tracked channel is never overwritten.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prepare_PreservesForeignAndTrackedContents(bool tracked)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_root, ScmEventExchangeProtocol.DirectoryName));
        var path = Path.Combine(directory.FullName, "foreign.txt");
        await File.WriteAllTextAsync(path, "belongs to the user");
        if (tracked) Git("add", "--", path);
        var prepare = () => _protocol.PrepareAsync(Request(), _root);
        await prepare.Should().ThrowAsync<IOException>();
        (await File.ReadAllTextAsync(path)).Should().Be("belongs to the user");
    }

    /// <summary>Two stable observations are required for direct Write publication.</summary>
    [Fact]
    public async Task Read_DoesNotExecutePartialOrFirstObservedMessages()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        var path = Path.Combine(attempt.DirectoryPath, "accepted.json");
        await File.WriteAllTextAsync(path, "{\"protocolVersion\":");
        (await _protocol.ReadAsync(attempt)).Accepted.Should().BeNull();
        await Write(path, new ScmExchangeAccepted(1, request.EventId, request.AttemptId, request.CliRunId));
        (await _protocol.ReadAsync(attempt)).Accepted.Should().BeNull();
        (await _protocol.ReadAsync(attempt)).Accepted.Should().NotBeNull();
        (await _protocol.ReadAsync(attempt)).Complete.Should().BeNull("acceptance does not imply completion");
    }

    /// <summary>Responses cannot change remote targets or correlate to another delivery attempt.</summary>
    [Theory]
    [InlineData("event")]
    [InlineData("attempt")]
    [InlineData("run")]
    [InlineData("version")]
    [InlineData("target")]
    public async Task Read_RejectsInvalidCorrelationAndUnknownRemoteTargets(string invalid)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        var id = Guid.NewGuid();
        var data = new Dictionary<string, object?>
        {
            ["protocolVersion"] = invalid == "version" ? 2 : 1,
            ["eventId"] = invalid == "event" ? Guid.NewGuid() : request.EventId,
            ["attemptId"] = invalid == "attempt" ? Guid.NewGuid() : request.AttemptId,
            ["cliRunId"] = invalid == "run" ? Guid.NewGuid() : request.CliRunId,
            ["actionId"] = id,
            ["kind"] = "comment",
            ["body"] = "reply",
        };
        if (invalid == "target") data["url"] = "https://example.invalid/another-repository";
        await Write(Path.Combine(attempt.DirectoryPath, "actions", id + ".json"), data);
        await _protocol.ReadAsync(attempt);
        var result = await _protocol.ReadAsync(attempt);
        result.Actions.Should().BeEmpty();
        result.Diagnostic.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Mutated request data never authorizes a later response.</summary>
    [Fact]
    public async Task Read_RejectsModifiedRequest()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        await Write(attempt.RequestPath, attempt.Request with { OriginUrl = "https://example.invalid/changed" });
        (await _protocol.ReadAsync(attempt)).Diagnostic.Should().Contain("verändert");
    }

    /// <summary>A correlated result may transition failure to success, but success is immutable.</summary>
    [Fact]
    public async Task Result_AllowsRetryButNeverChangesSuccess()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        var failed = new ScmExchangeActionResult(1, request.EventId, request.AttemptId, request.CliRunId, Guid.NewGuid(), false, Error: "403");
        await _protocol.WriteResultAsync(attempt, failed);
        var success = failed with { Success = true, Error = null, RemoteId = "comment-1" };
        await _protocol.WriteResultAsync(attempt, success);
        await _protocol.WriteResultAsync(attempt, success);
        var overwrite = () => _protocol.WriteResultAsync(attempt, success with { RemoteId = "comment-2" });
        await overwrite.Should().ThrowAsync<IOException>();
    }

    /// <summary>Finishing without a remote action requires an explicit reason.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("No change needed", true)]
    public async Task Complete_RequiresReasonForNoAction(string? reason, bool valid)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        await Write(Path.Combine(attempt.DirectoryPath, "complete.json"),
            new ScmExchangeComplete(1, request.EventId, request.AttemptId, request.CliRunId, [], true, reason));
        await _protocol.ReadAsync(attempt);
        var result = await _protocol.ReadAsync(attempt);
        (result.Complete is not null).Should().Be(valid);
    }

    /// <summary>Selbst über die SCM-Anbindung ausgeführte Remoteaktionen schließen ohne Kanalaktion
    /// ab — aber nur mit einem nicht leeren Ergebnisbericht; ein völlig belegloser Abschluss bleibt
    /// ein Protokollfehler.</summary>
    [Theory]
    [InlineData("Konflikt behoben und PR #7 per Admin-Merge gemergt.", true)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public async Task Complete_SelfExecutedRemoteActions_RequireReport(string? report, bool valid)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        await Write(Path.Combine(attempt.DirectoryPath, "complete.json"),
            new ScmExchangeComplete(1, request.EventId, request.AttemptId, request.CliRunId, [], false, Report: report));
        await _protocol.ReadAsync(attempt);
        var result = await _protocol.ReadAsync(attempt);
        (result.Complete is not null).Should().Be(valid);
        if (valid)
        {
            result.Complete!.Report.Should().Be(report);
        }
        else
        {
            result.Diagnostic.Should().Contain("Remoteaktionen");
        }
    }

    /// <summary>Kanalaktionen bleiben ohne report gültig; ein report kann sie zusätzlich dokumentieren.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Zusätzlich Label gesetzt.")]
    public async Task Complete_ChannelActions_StayValidWithOrWithoutReport(string? report)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        var actionId = Guid.NewGuid();
        await Write(Path.Combine(attempt.DirectoryPath, "complete.json"),
            new ScmExchangeComplete(1, request.EventId, request.AttemptId, request.CliRunId, [actionId], false, Report: report));
        await _protocol.ReadAsync(attempt);
        var result = await _protocol.ReadAsync(attempt);
        result.Complete.Should().NotBeNull();
        result.Complete!.ActionIds.Should().BeEquivalentTo([actionId]);
    }

    /// <summary>Eine im Abschluss gemeldete Pull-Request-Nummer wird gelesen und verknüpfbar;
    /// nicht positive Werte bleiben ein Protokollfehler.</summary>
    [Theory]
    [InlineData(7, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public async Task Complete_ReportedPullRequestNumber_IsValidated(int number, bool valid)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        var request = attempt.Request;
        await Write(Path.Combine(attempt.DirectoryPath, "complete.json"),
            new ScmExchangeComplete(1, request.EventId, request.AttemptId, request.CliRunId,
                [], false, Report: "PR angelegt.", PullRequestNumber: number));
        await _protocol.ReadAsync(attempt);
        var result = await _protocol.ReadAsync(attempt);
        (result.Complete is not null).Should().Be(valid);
        if (valid)
            result.Complete!.PullRequestNumber.Should().Be(number);
        else
            result.Diagnostic.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Der Prompt dokumentiert das pullRequestNumber-Feld für vom Agenten angelegte Pull Requests.</summary>
    [Fact]
    public async Task Prompt_DocumentsPullRequestNumberReporting()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        using var document = JsonDocument.Parse(attempt.Prompt);
        document.RootElement.GetProperty("instruction").GetString()!.Should().Contain("pullRequestNumber");
        document.RootElement.GetProperty("completeSchema").GetString()!.Should().Contain("pullRequestNumber");
    }

    /// <summary>Der Prompt weist den Agenten an, erlaubte Issue-/PR-Aktionen selbst über die
    /// SCM-Anbindung auszuführen, und dokumentiert das report-Feld; Repo- und Ursprungskontext
    /// bleiben Daten im request.</summary>
    [Fact]
    public async Task Prompt_InstructsSelfExecutedActionsWithRepositoryContext()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root);
        using var document = JsonDocument.Parse(attempt.Prompt);
        var instruction = document.RootElement.GetProperty("instruction").GetString()!;
        instruction.Should().Contain("request.repository");
        instruction.Should().Contain("request.originUrl");
        instruction.Should().Contain("GH_TOKEN");
        instruction.Should().Contain("selbst");
        instruction.Should().Contain("Admin-Merge");
        instruction.Should().Contain("noRemoteAction");
        document.RootElement.GetProperty("completeSchema").GetString().Should().Contain("report");
        document.RootElement.GetProperty("request").GetProperty("repository").GetString().Should().Be("owner/repo");
        document.RootElement.GetProperty("request").GetProperty("originUrl").GetString()
            .Should().Be("https://github.com/owner/repo/issues/1#issuecomment-1");
    }

    private static ScmExchangeRequest Request(string body = "$Softwareschmiede: requirement") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), body, "IssueComment", "owner/repo", "https://github.com/owner/repo/issues/1#issuecomment-1");

    private static Task Write<T>(string path, T value) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json));

    private string Git(params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = _root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return output;
    }

    /// <summary>Removes only the newly created isolated test repository.</summary>
    public void Dispose()
    {
        // Git object files are read-only on Windows, including objects created by the tracked-content case.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
