using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>End-to-End-Dispatch eines SCM-Ereignisses an eine Devin-gebundene Aufgabe über den bewährten Kanal.</summary>
public sealed class ScmEventDispatchTests_Devin
{
    /// <summary>Bereitschaft, Kanalnachweis und Prompt-Zustellung erreichen für eine Devin-Aufgabe den Status Zugestellt.</summary>
    [Fact]
    public async Task DevinTask_WithGrantingStartContract_DeliversEvent()
    {
        var prompts = new List<string>();
        var probes = new List<string>();
        var fixture = default(ScmDispatchFixture);
        fixture = new ScmDispatchFixture(ai: ScmAiProfile.Devin(onWritePrompt: async prompt =>
        {
            if (prompt.Contains("challengePath", StringComparison.Ordinal)) { probes.Add(prompt); await ScmDispatchFixture.CompleteProbeAsync(prompt); }
            else prompts.Add(prompt);
            // Eine echte CLI zeichnet neu und kehrt nach einem Turn zu ihrer Eingabeaufforderung zurück; nur neue Ausgabe
            // revisions let the adapter leave the busy state again.
            fixture!.Sessions.Values.Single().Raise(x => x.BufferChanged += null, EventArgs.Empty);
        }));
        try
        {
            await fixture.InitializeAsync();
            var (taskId, eventId) = await fixture.AddAsync(configured: true);

            // Der erste Dispatch sendet die inhaltsfreie Kanal-Probe; der nächste Dispatch liest den
            // abgeschlossenen Nachweis, bereitet den laufgebundenen Versuch vor und schreibt den Event-Prompt.
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
            // Der Handshake benennt die CLI-Sitzung nach der Aufgabe, damit Sitzungen unterscheidbar bleiben.
            probes.Should().ContainSingle().Which.Should().Contain("\"sessionName\":\"SCM task\"");
        }
        finally
        {
            if (fixture is not null) await fixture.DisposeAsync();
        }
    }

    /// <summary>Ein neueres Devin-Minor innerhalb des unterstützten Majors liefert wie die angepinnte Version.</summary>
    [Fact]
    public async Task DevinTask_WithNewerMinorVersion_DeliversEvent()
    {
        var prompts = new List<string>();
        var fixture = default(ScmDispatchFixture);
        fixture = new ScmDispatchFixture(ai: ScmAiProfile.Devin(banner: "Devin CLI\nv3000.12.7", onWritePrompt: async prompt =>
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

    /// <summary>Eine ausserhalb des Bereichs liegende Devin-Version wartet mit einem ehrlichen Grund, der CLI und Version nennt.</summary>
    [Fact]
    public async Task DevinTask_WithUnsupportedVersion_WaitsWithHonestReason()
    {
        await using var fixture = new ScmDispatchFixture(ai: ScmAiProfile.Devin(banner: "Devin CLI\nv3001.0.0"));
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        item.Status.Should().Be("Wartend");
        item.Reason.Should().Contain("Devin CLI").And.Contain("v3001.0.0").And.Contain("nicht unterstützt");
        fixture.Prompts.Should().BeEmpty();
    }

    /// <summary>Ein nicht freigebender Devin-Modus lässt das Ereignis warten; es wird weder ein Prompt noch eine Probe gesendet.</summary>
    [Fact]
    public async Task DevinTask_WithAutoPermissionMode_WaitsWithoutPrompt()
    {
        await using var fixture = new ScmDispatchFixture(ai: ScmAiProfile.Devin("--permission-mode auto --respect-workspace-trust false"));
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        item.Status.Should().Be("Wartend");
        item.Reason.Should().Contain("Antwortkanal für diese CLI nicht verfügbar");
        fixture.Prompts.Should().BeEmpty();
    }

    /// <summary>Der umgebungsseitig bereitgestellte Berechtigungsmodus ist Teil des ausgewerteten Startvertrags.</summary>
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
