using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ProtokollService.</summary>
public sealed class ProtokollServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ILogger<ProtokollService>> _loggerMock;
    private readonly ProtokollService _sut;
    private readonly Guid _aufgabeId = new Guid("22222222-2222-2222-2222-222222222222");
    private readonly Guid _projektId = new Guid("33333333-3333-3333-3333-333333333333");

    public ProtokollServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<ProtokollService>>();
        _sut = new ProtokollService(_db, _loggerMock.Object);

        // Seed Projekt und Aufgabe für FK
        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Protokoll-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.Aufgaben.Add(new Softwareschmiede.Domain.Entities.Aufgabe
        {
            Id = _aufgabeId,
            ProjektId = _projektId,
            Titel = "Protokoll-Testaufgabe",
            Status = AufgabeStatus.Offen,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>AddEintragAsync erstellt und speichert einen Protokolleintrag.</summary>
    [Fact]
    public async Task AddEintragAsync_ShouldCreateAndPersistProtokollEintrag_WhenCalledWithValidData()
    {
        // Arrange & Act
        var result = await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.Prompt, "Testinhalt", "TestAgent");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.AufgabeId.Should().Be(_aufgabeId);
        result.Typ.Should().Be(ProtokollTyp.Prompt);
        result.Inhalt.Should().Be("Testinhalt");
        result.AgentName.Should().Be("TestAgent");
    }

    /// <summary>GetByAufgabeAsync gibt Einträge chronologisch sortiert zurück.</summary>
    [Fact]
    public async Task GetByAufgabeAsync_ShouldReturnEntriesOrderedByZeitstempel_WhenMultipleEntriesExist()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.GitAktion, "Erster Eintrag", null);
        await Task.Delay(10); // leichte Verzögerung für unterschiedliche Zeitstempel
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.KiAntwort, "Zweiter Eintrag", "Agent");

        // Act
        var result = await _sut.GetByAufgabeAsync(_aufgabeId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Inhalt.Should().Be("Erster Eintrag");
        result[1].Inhalt.Should().Be("Zweiter Eintrag");
    }

    /// <summary>AddTestErgebnisseAsync erstellt Protokolleintrag mit TestErgebnissen.</summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldCreateEintragWithTestErgebnisse_WhenAllTestsPassed()
    {
        // Arrange
        var testResult = new TestResult(
            Bestanden: true,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("Test1", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(100)),
                new TestErgebnisInfo("Test2", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(200))
            });

        // Act
        var result = await _sut.AddTestErgebnisseAsync(_aufgabeId, testResult);

        // Assert
        result.Typ.Should().Be(ProtokollTyp.TestErgebnis);
        result.Inhalt.Should().Contain("2");
        result.TestErgebnisse.Should().HaveCount(2);
    }

    /// <summary>AddTestErgebnisseAsync erstellt korrekten Inhalt wenn Tests fehlgeschlagen.</summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldCreateFailureMessage_WhenSomeTestsFailed()
    {
        // Arrange
        var testResult = new TestResult(
            Bestanden: false,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("Test1", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(100)),
                new TestErgebnisInfo("Test2", TestStatus.Fehlgeschlagen, "Assert failed", TimeSpan.FromMilliseconds(50))
            });

        // Act
        var result = await _sut.AddTestErgebnisseAsync(_aufgabeId, testResult);

        // Assert
        result.Inhalt.Should().Contain("fehlgeschlagen");
    }

    /// <summary>AddStatusUebergangAsync erstellt einen Statusübergangs-Eintrag.</summary>
    [Fact]
    public async Task AddStatusUebergangAsync_ShouldCreateStatusUebergangEintrag_WhenCalled()
    {
        // Arrange & Act
        var result = await _sut.AddStatusUebergangAsync(
            _aufgabeId,
            AufgabeStatus.Offen,
            AufgabeStatus.InBearbeitung);

        // Assert
        result.Typ.Should().Be(ProtokollTyp.StatusUebergang);
        result.Inhalt.Should().Contain("Offen");
        result.Inhalt.Should().Contain("InBearbeitung");
    }

    /// <summary>SuchenAsync findet Einträge anhand des Suchbegriffs im Inhalt.</summary>
    [Fact]
    public async Task SuchenAsync_ShouldReturnMatchingEntries_WhenSuchbegriffMatchesInhalt()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.Prompt, "Fehlerhafte Implementierung", null);
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.Prompt, "Gute Implementierung", null);
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.Prompt, "Kein Treffer hier", null);

        // Act
        var result = await _sut.SuchenAsync(_aufgabeId, "Implementierung");

        // Assert
        result.Should().HaveCount(2);
    }

    /// <summary>SuchenAsync findet Einträge anhand des Suchbegriffs im AgentName.</summary>
    [Fact]
    public async Task SuchenAsync_ShouldReturnMatchingEntries_WhenSuchbegriffMatchesAgentName()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.KiAntwort, "Inhalt A", "CopilotAgent");
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.KiAntwort, "Inhalt B", "AndererAgent");

        // Act
        var result = await _sut.SuchenAsync(_aufgabeId, "Copilot");

        // Assert
        result.Should().HaveCount(1);
        result[0].AgentName.Should().Be("CopilotAgent");
    }

    /// <summary>SuchenAsync gibt leere Liste zurück wenn kein Treffer.</summary>
    [Fact]
    public async Task SuchenAsync_ShouldReturnEmptyList_WhenNoMatchFound()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, ProtokollTyp.Prompt, "Kein Treffer", null);

        // Act
        var result = await _sut.SuchenAsync(_aufgabeId, "XYZNOTFOUND");

        // Assert
        result.Should().BeEmpty();
    }
}
