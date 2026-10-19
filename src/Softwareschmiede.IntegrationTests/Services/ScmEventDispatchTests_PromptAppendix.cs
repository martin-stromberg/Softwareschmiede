using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;
using Xunit;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>End-to-end dispatch of configured SCM prompt appendices: the combined suffix reaches the
/// CLI prompt as a dedicated field while the stored event text and request.json stay unchanged.</summary>
public sealed class ScmEventDispatchTests_PromptAppendix
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Jede Ereignisart erhält den allgemeinen Anhang gefolgt von ihrem spezifischen Anhang.
    /// PR-Reviews und Review-Kommentare fallen unter den PR-Kommentar-Anhang.</summary>
    [Fact]
    public async Task Dispatch_AppendsGeneralThenMatchingSpecific_PerEventKind()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendGeneralKey, "ALLGEMEIN");
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendIssueKey, "ISSUE");
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendIssueCommentKey, "ISSUE_KOMMENTAR");
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendPullRequestKey, "PR");
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendPullRequestCommentKey, "PR_KOMMENTAR");
        }

        var cases = new (ScmReferenceKind Kind, ScmContentKind Content, string Expected)[]
        {
            (ScmReferenceKind.Issue, ScmContentKind.Description, "ALLGEMEIN\n\nISSUE"),
            (ScmReferenceKind.Issue, ScmContentKind.IssueComment, "ALLGEMEIN\n\nISSUE_KOMMENTAR"),
            (ScmReferenceKind.PullRequest, ScmContentKind.Description, "ALLGEMEIN\n\nPR"),
            (ScmReferenceKind.PullRequest, ScmContentKind.IssueComment, "ALLGEMEIN\n\nPR_KOMMENTAR"),
            (ScmReferenceKind.PullRequest, ScmContentKind.PullRequestReview, "ALLGEMEIN\n\nPR_KOMMENTAR"),
        };
        var events = new List<Guid>();
        foreach (var (kind, content, _) in cases)
            events.Add((await fixture.AddAsync(configured: true, referenceKind: kind, contentKind: content)).EventId);
        foreach (var eventId in events)
            await fixture.DispatchAsync(eventId);

        Assert.Equal(cases.Length, fixture.Prompts.Count);
        for (var index = 0; index < cases.Length; index++)
        {
            using var document = JsonDocument.Parse(fixture.Prompts[index]);
            Assert.Equal(cases[index].Expected, document.RootElement.GetProperty("promptSuffix").GetString());
            Assert.Equal("$Softwareschmiede test", document.RootElement.GetProperty("request").GetProperty("body").GetString());
            var item = await fixture.EventAsync(events[index]);
            Assert.Equal("Zugestellt", item.Status);
            // Der gespeicherte Ereignistext und der unveränderliche Auftrag bleiben frei vom Anhang.
            Assert.Equal("$Softwareschmiede test", ScmEventStore.Observation(item).Body);
            var stored = JsonSerializer.Deserialize<ScmExchangeRequest>(
                await File.ReadAllTextAsync(Path.Combine(item.ExchangePath!, "request.json")), Json);
            Assert.Equal("$Softwareschmiede test", stored!.Body);
        }
    }

    /// <summary>Ohne konfigurierte Anhänge wird der bisherige Prompt ohne promptSuffix-Feld zugestellt.</summary>
    [Fact]
    public async Task Dispatch_WithoutConfiguredAppendix_OmitsSuffixField()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        var prompt = Assert.Single(fixture.Prompts);
        using var document = JsonDocument.Parse(prompt);
        Assert.False(document.RootElement.TryGetProperty("promptSuffix", out _));
    }

    /// <summary>Ist nur der allgemeine Anhang konfiguriert, wird genau er angehängt; ein nur aus
    /// Leerraum bestehender spezifischer Wert hängt nichts an.</summary>
    [Fact]
    public async Task Dispatch_GeneralOnlyAndBlankSpecific_AppendsJustGeneral()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendGeneralKey, "  ALLGEMEIN  ");
            await einstellungen.SetSettingAsync(AppEinstellungService.ScmPromptAppendIssueCommentKey, "   ");
        }
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        var prompt = Assert.Single(fixture.Prompts);
        using var document = JsonDocument.Parse(prompt);
        Assert.Equal("ALLGEMEIN", document.RootElement.GetProperty("promptSuffix").GetString());
    }

    /// <summary>Der Anhang ändert weder Deduplizierung noch Annahme: EventKey und Snapshot-Hash bleiben
    /// auf dem ursprünglichen Ereignistext gebildet.</summary>
    [Fact]
    public async Task Dispatch_AppendixDoesNotTouchEventIdentityOrDedup()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetSettingAsync(AppEinstellungService.ScmPromptAppendGeneralKey, "ALLGEMEIN");
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId);
        var observation = ScmEventStore.Observation(item);
        Assert.Equal(observation.EventKey, item.EventKey);
        var snapshot = await db.ScmContentSnapshots.SingleAsync(x => x.ContentKey == observation.ContentKey);
        Assert.Equal(observation.BodyHash, snapshot.BodyHash);
    }
}
