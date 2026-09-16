using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="AufgabeService.SetPauseAsync"/> und die Persistenz von <see cref="Aufgabe.PausiertBisUtc"/> (Issue 151).</summary>
public sealed class AufgabeServiceTests_Pause : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _sut;
    private readonly Guid _projektId = new Guid("22222222-2222-2222-2222-222222222222");

    /// <summary>AufgabeServiceTests_Pause.</summary>
    public AufgabeServiceTests_Pause()
    {
        _db = TestDbContextFactory.Create();
        _sut = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Pause-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>SetPauseAsync persistiert den Pause-Endzeitpunkt und schreibt einen SystemMeldung-Eintrag.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldPersistPausiertBisUtc_AndWriteSystemLog()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Pausierbare Aufgabe", null);
        var pausiertBis = DateTimeOffset.UtcNow.AddHours(2);

        await _sut.SetPauseAsync(aufgabe.Id, pausiertBis);

        var geladen = await _db.Aufgaben.AsNoTracking().SingleAsync(a => a.Id == aufgabe.Id);
        geladen.PausiertBisUtc.Should().NotBeNull();
        geladen.PausiertBisUtc!.Value.Should().BeCloseTo(pausiertBis, TimeSpan.FromSeconds(5));

        var eintraege = await _db.Protokolleintraege
            .Where(e => e.AufgabeId == aufgabe.Id && e.Typ == ProtokollTyp.SystemMeldung)
            .ToListAsync();
        eintraege.Should().ContainSingle(e => e.Inhalt.Contains("pausiert", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>SetPauseAsync mit null hebt eine bestehende Pause auf und protokolliert das Aufheben.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldClearPause_WhenNull()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Pausierte Aufgabe", null);
        await _sut.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));

        await _sut.SetPauseAsync(aufgabe.Id, null);

        var geladen = await _db.Aufgaben.AsNoTracking().SingleAsync(a => a.Id == aufgabe.Id);
        geladen.PausiertBisUtc.Should().BeNull();

        var eintraege = await _db.Protokolleintraege
            .Where(e => e.AufgabeId == aufgabe.Id && e.Typ == ProtokollTyp.SystemMeldung)
            .ToListAsync();
        eintraege.Should().Contain(e => e.Inhalt.Contains("aufgehoben", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>SetPauseAsync erlaubt die Stati Neu, Gestartet und Wartend.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Neu)]
    [InlineData(AufgabeStatus.Gestartet)]
    [InlineData(AufgabeStatus.Wartend)]
    public async Task SetPauseAsync_ShouldAllowActiveStatuses(AufgabeStatus status)
    {
        var aufgabe = await _sut.CreateAsync(_projektId, $"Aufgabe {status}", null);
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.Status = status;
        await _db.SaveChangesAsync();

        var act = () => _sut.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddMinutes(30));

        await act.Should().NotThrowAsync();
        (await _db.Aufgaben.FindAsync(aufgabe.Id))!.PausiertBisUtc.Should().NotBeNull();
    }

    /// <summary>SetPauseAsync lehnt nicht-pausierbare Stati (Beendet, Archiviert) ab.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Beendet)]
    [InlineData(AufgabeStatus.Archiviert)]
    public async Task SetPauseAsync_ShouldThrow_WhenStatusNotPausable(AufgabeStatus status)
    {
        var aufgabe = await _sut.CreateAsync(_projektId, $"Aufgabe {status}", null);
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.Status = status;
        await _db.SaveChangesAsync();

        var act = () => _sut.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht pausiert*");
    }

    /// <summary>SetPauseAsync lehnt Autonome Aufgaben ab — deren Pausierung läuft über den Projektleiter-Agenten (SessionPauseUtc), nicht über PausiertBisUtc.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldThrow_WhenAufgabeIstAutonom()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Autonome Aufgabe", null);
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.Status = AufgabeStatus.Gestartet;
        _db.AutonomAufgabeKonfigurationen.Add(new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = @"C:\arbeitsverzeichnis\permissions.json",
            ArbeitsverzeichnisPfad = @"C:\arbeitsverzeichnis"
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Autonom*");
        (await _db.Aufgaben.FindAsync(aufgabe.Id))!.PausiertBisUtc.Should().BeNull();
    }

    /// <summary>SetPauseAsync lehnt einen Zeitpunkt in der Vergangenheit ab.</summary>
    [Fact]
    public async Task SetPauseAsync_ShouldThrow_WhenTimestampInPast()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Aufgabe", null);

        var act = () => _sut.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddMinutes(-1));

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
