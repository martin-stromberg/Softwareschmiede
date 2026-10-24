using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>End-to-end dispatch of an SCM event to a Devin-bound task through the proven channel.</summary>
public sealed class ScmEventDispatchTests_Devin
{
    /// <summary>Readiness, channel proof and prompt delivery reach Zugestellt for a Devin task.</summary>
    [Fact]
    public async Task DevinTask_WithGrantingStartContract_DeliversEvent()
    {
        var prompts = new List<string>();
        var fixture = default(ScmDispatchFixture);
        fixture = new ScmDispatchFixture(ki: ScmKiProfile.Devin(onWritePrompt: async prompt =>
        {
            if (prompt.Contains("challengePath", StringComparison.Ordinal)) await ScmDispatchFixture.CompleteProbeAsync(prompt);
            else prompts.Add(prompt);
            // A real CLI redraws and returns to its prompt after a turn; only new output
            // revisions let the adapter leave the busy state again.
            fixture!.Sessions.Values.Single().Raise(x => x.BufferChanged += null, EventArgs.Empty);
        }));
        try
        {
            await fixture.InitializeAsync();
            var (taskId, eventId) = await fixture.AddAsync(configured: true);

            // First dispatch sends the content-free channel probe; the next dispatch reads the
            // completed proof, prepares the run-bound attempt and writes the event prompt.
            ScmIncomingEvent item;
            var attempts = 0;
            do
            {
                await fixture.DispatchAsync(eventId);
                item = await fixture.EventAsync(eventId);
            } while (item.Status == "Wartend" && ++attempts < 5);

            item.Status.Should().Be("Zugestellt");
            item.DeliveryAttemptId.Should().NotBeNull();
            item.CliRunId.Should().NotBeNullOrWhiteSpace();
            fixture.Sessions.Should().ContainKey(taskId);
            prompts.Should().ContainSingle("only the event prompt itself is recorded; the probe ran through the file tool");
            prompts[0].Should().Contain("\"request\"").And.NotContain("challengePath");
        }
        finally
        {
            if (fixture is not null) await fixture.DisposeAsync();
        }
    }

    /// <summary>A Devin patch release inside the supported range delivers like the pinned version.</summary>
    [Fact]
    public async Task DevinTask_WithSupportedPatchVersion_DeliversEvent()
    {
        var prompts = new List<string>();
        var fixture = default(ScmDispatchFixture);
        fixture = new ScmDispatchFixture(ki: ScmKiProfile.Devin(banner: "Devin CLI\nv3000.11.205", onWritePrompt: async prompt =>
        {
            if (prompt.Contains("challengePath", StringComparison.Ordinal)) await ScmDispatchFixture.CompleteProbeAsync(prompt);
            else prompts.Add(prompt);
            fixture!.Sessions.Values.Single().Raise(x => x.BufferChanged += null, EventArgs.Empty);
        }));
        try
        {
            await fixture.InitializeAsync();
            var (taskId, eventId) = await fixture.AddAsync(configured: true);

            ScmIncomingEvent item;
            var attempts = 0;
            do
            {
                await fixture.DispatchAsync(eventId);
                item = await fixture.EventAsync(eventId);
            } while (item.Status == "Wartend" && ++attempts < 5);

            item.Status.Should().Be("Zugestellt");
            fixture.Sessions.Should().ContainKey(taskId);
            prompts.Should().ContainSingle();
        }
        finally
        {
            if (fixture is not null) await fixture.DisposeAsync();
        }
    }

    /// <summary>An out-of-range Devin version waits with an honest reason naming CLI and version.</summary>
    [Fact]
    public async Task DevinTask_WithUnsupportedVersion_WaitsWithHonestReason()
    {
        await using var fixture = new ScmDispatchFixture(ki: ScmKiProfile.Devin(banner: "Devin CLI\nv3000.12.7"));
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        item.Status.Should().Be("Wartend");
        item.Reason.Should().Contain("Devin CLI").And.Contain("v3000.12.7").And.Contain("nicht unterstützt");
        fixture.Prompts.Should().BeEmpty();
    }

    /// <summary>A non-granting Devin mode leaves the event waiting; no prompt and no probe is sent.</summary>
    [Fact]
    public async Task DevinTask_WithAutoPermissionMode_WaitsWithoutPrompt()
    {
        await using var fixture = new ScmDispatchFixture(ki: ScmKiProfile.Devin("--permission-mode auto --respect-workspace-trust false"));
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        item.Status.Should().Be("Wartend");
        item.Reason.Should().Contain("Antwortkanal für diese CLI nicht verfügbar");
        fixture.Prompts.Should().BeEmpty();
    }

    /// <summary>The environment-provided permission mode is part of the evaluated start contract.</summary>
    [Theory]
    [InlineData("dangerous", ScmExchangeAccessStatus.Supported)]
    [InlineData("bypass", ScmExchangeAccessStatus.Supported)]
    [InlineData("auto", ScmExchangeAccessStatus.Unknown)]
    [InlineData("unexpected", ScmExchangeAccessStatus.Unknown)]
    public void DevinEnvironmentPermissionMode_IsEvaluated(string envMode, ScmExchangeAccessStatus expected)
    {
        var spec = new TerminalSessionStartSpec
        {
            WorkingDirectory = Path.GetTempPath(),
            EnvironmentVariables = new Dictionary<string, string?> { ["DEVIN_PERMISSION_MODE"] = envMode },
        };

        ScmCliAccessEvaluator.Evaluate("Softwareschmiede.Devin", spec, Guid.NewGuid(), []).Status.Should().Be(expected);
    }

    /// <summary>An explicit argument wins over a weaker environment-provided mode.</summary>
    [Fact]
    public void DevinPermissionModeArgument_OverridesEnvironment()
    {
        var spec = new TerminalSessionStartSpec
        {
            WorkingDirectory = Path.GetTempPath(),
            Arguments = "--permission-mode accept-edits",
            EnvironmentVariables = new Dictionary<string, string?> { ["DEVIN_PERMISSION_MODE"] = "auto" },
        };

        ScmCliAccessEvaluator.Evaluate("Softwareschmiede.Devin", spec, Guid.NewGuid(), []).Status.Should().Be(ScmExchangeAccessStatus.Supported);
    }
}
