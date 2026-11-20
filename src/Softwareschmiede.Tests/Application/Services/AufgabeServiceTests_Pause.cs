using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="AufgabeService.SetPauseAsync"/> und die Persistenz von <see cref="WorkItem.PausedUntilUtc"/> (Issue 151).</summary>
public sealed class AufgabeServiceTests_Pause : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AufgabeService _sut;
    private readonly Guid _projektId = new Guid("22222222-2222-2222-2222-222222222222");

    /// <summary>AufgabeServiceTests_Pause.</summary>
    public AufgabeServiceTests_Pause()
    {
        _db = TestDbContextFactory.Create();
        _sut = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));

        _db.Projects.Add(new Project
        {
            Id = _projektId,
            Name = "Pause-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>SetPauseAsync persistiert den Pause-Endzeitpunkt und schreibt einen SystemMeldung-Eintrag.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldPersistPausiertBisUtc_AndWriteSystemLog()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Pausierbare Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var pausiertBis = DateTimeOffset.UtcNow.AddHours(2);

        await _sut.SetPauseAsync(workItem.Id, pausiertBis, TestContext.Current.CancellationToken);

        var geladen = await _db.WorkItems.AsNoTracking().SingleAsync(a => a.Id == workItem.Id, cancellationToken: TestContext.Current.CancellationToken);
        geladen.PausedUntilUtc.Should().NotBeNull();
        geladen.PausedUntilUtc!.Value.Should().BeCloseTo(pausiertBis, TimeSpan.FromSeconds(5));

        var eintraege = await _db.LogEntries
            .Where(e => e.WorkItemId == workItem.Id && e.Type == LogEntryType.SystemMessage)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        eintraege.Should().ContainSingle(e => e.Content.Contains("pausiert", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>SetPauseAsync mit null hebt eine bestehende Pause auf und protokolliert das Aufheben.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldClearPause_WhenNull()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Pausierte Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        await _sut.SetPauseAsync(workItem.Id, null, TestContext.Current.CancellationToken);

        var geladen = await _db.WorkItems.AsNoTracking().SingleAsync(a => a.Id == workItem.Id, cancellationToken: TestContext.Current.CancellationToken);
        geladen.PausedUntilUtc.Should().BeNull();

        var eintraege = await _db.LogEntries
            .Where(e => e.WorkItemId == workItem.Id && e.Type == LogEntryType.SystemMessage)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        eintraege.Should().Contain(e => e.Content.Contains("aufgehoben", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>SetPauseAsync erlaubt die Stati Neu, Gestartet und Wartend.</summary>
    [Theory]
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    public async Task SetPauseAsync_ShouldAllowActiveStatuses(WorkItemStatus status)
    {
        var workItem = await _sut.CreateAsync(_projektId, $"Aufgabe {status}", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.Status = status;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _sut.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddMinutes(30));

        await act.Should().NotThrowAsync();
        (await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc.Should().NotBeNull();
    }

    /// <summary>SetPauseAsync lehnt nicht-pausierbare Stati (Beendet, Archiviert) ab.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Archived)]
    public async Task SetPauseAsync_ShouldThrow_WhenStatusNotPausable(WorkItemStatus status)
    {
        var workItem = await _sut.CreateAsync(_projektId, $"Aufgabe {status}", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.Status = status;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _sut.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht pausiert*");
    }

    /// <summary>SetPauseAsync lehnt einen Zeitpunkt in der Vergangenheit ab.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldThrow_WhenTimestampInPast()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Aufgabe", null, ct: TestContext.Current.CancellationToken);

        var act = () => _sut.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddMinutes(-1));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Zukunft*");
    }

    /// <summary>SetPauseAsync wirft bei unbekannter Aufgabe.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldThrow_WhenAufgabeNotFound()
    {
        var act = () => _sut.SetPauseAsync(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
