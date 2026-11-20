using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Stdout-Streaming und Protokoll-Eintrag in UI.</summary>
public sealed class ProtocolLoggingServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly LogService _logService;
    private readonly WorkItemService _workItemService;
    private readonly ProjectService _projektService;
    private Guid _aufgabeId;

    /// <summary>ProtocolLoggingServiceIntegrationTests.</summary>
    public ProtocolLoggingServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _projektService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private async Task SeedAufgabeAsync()
    {
        var project = await _projektService.CreateAsync("Log-Projekt", null);
        var workItem = await _workItemService.CreateAsync(project.Id, "Log-Aufgabe", null);
        _aufgabeId = workItem.Id;
    }

    /// <summary><summary>AddCliOutputAsync_StreichtAusgabeInProtokoll.</summary>.</summary>
    [Fact]
    public async Task AddCliOutputAsync_StreichtAusgabeInProtokoll()
    {
        await SeedAufgabeAsync();

        await _logService.AddCliOutputAsync(_aufgabeId, "Zeile 1 der Ausgabe", TestContext.Current.CancellationToken);
        await _logService.AddCliOutputAsync(_aufgabeId, "Zeile 2 der Ausgabe", TestContext.Current.CancellationToken);

        var entries = await _logService.GetByWorkItemAsync(_aufgabeId, TestContext.Current.CancellationToken);
        entries.Should().HaveCount(2);
        entries.All(e => e.Type == LogEntryType.CliOutput).Should().BeTrue();
    }

    /// <summary><summary>AddCliOutputAsync_MehrereZeilen_SindInReihenfolgeGespeichert.</summary>.</summary>
    [Fact]
    public async Task AddCliOutputAsync_MehrereZeilen_SindInReihenfolgeGespeichert()
    {
        await SeedAufgabeAsync();
        var lines = Enumerable.Range(1, 5).Select(i => $"Ausgabezeile {i}").ToList();

        foreach (var line in lines)
            await _logService.AddCliOutputAsync(_aufgabeId, line, TestContext.Current.CancellationToken);

        var entries = await _logService.GetByWorkItemAsync(_aufgabeId, TestContext.Current.CancellationToken);
        entries.Should().HaveCount(5);
        for (var i = 0; i < lines.Count; i++)
            entries[i].Content.Should().Be(lines[i]);
    }

    /// <summary><summary>AddStatusUebergangAsync_ErstelltEintragImProtokoll.</summary>.</summary>
    [Fact]
    public async Task AddStatusUebergangAsync_ErstelltEintragImProtokoll()
    {
        await SeedAufgabeAsync();

        await _logService.AddStatusTransitionAsync(_aufgabeId, WorkItemStatus.New, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        var entries = await _logService.GetByWorkItemAsync(_aufgabeId, TestContext.Current.CancellationToken);
        entries.Should().HaveCount(1);
        entries[0].Type.Should().Be(LogEntryType.StatusTransition);
        entries[0].Content.Should().Contain("Gestartet");
    }
}
