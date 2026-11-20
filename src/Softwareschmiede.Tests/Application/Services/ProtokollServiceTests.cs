using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;
using TestRunResult = Softwareschmiede.Domain.ValueObjects.TestRunResult;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ProtokollService.</summary>
public sealed class ProtokollServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly Mock<ILogger<ProtokollService>> _loggerMock;
    private readonly ProtokollService _sut;
    private readonly Guid _aufgabeId = new Guid("22222222-2222-2222-2222-222222222222");
    private readonly Guid _projektId = new Guid("33333333-3333-3333-3333-333333333333");

    /// <summary>ProtokollServiceTests.</summary>
    public ProtokollServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<ProtokollService>>();
        _sut = new ProtokollService(_db, _loggerMock.Object);

        // Seed Projekt und Aufgabe für FK
        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projektId,
            Name = "Protokoll-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.WorkItems.Add(new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = _aufgabeId,
            ProjectId = _projektId,
            Title = "Protokoll-Testaufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>AddEintragAsync erstellt und speichert einen Protokolleintrag.</summary>
    [Fact]
    public async Task AddEintragAsync_ShouldCreateAndPersistProtokollEintrag_WhenCalledWithValidData()
    {
        // Arrange & Act
        var result = await _sut.AddEintragAsync(_aufgabeId, LogEntryType.Prompt, "Testinhalt", "TestAgent", TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.WorkItemId.Should().Be(_aufgabeId);
        result.Type.Should().Be(LogEntryType.Prompt);
        result.Content.Should().Be("Testinhalt");
        result.AgentName.Should().Be("TestAgent");
    }

    /// <summary>GetByAufgabeAsync gibt Einträge chronologisch sortiert zurück.</summary>
    [Fact]
    public async Task GetByAufgabeAsync_ShouldReturnEntriesOrderedByZeitstempel_WhenMultipleEntriesExist()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.GitAction, "Erster Eintrag", null, TestContext.Current.CancellationToken);
        await Task.Delay(10, TestContext.Current.CancellationToken); // leichte Verzögerung für unterschiedliche Zeitstempel
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.AiResponse, "Zweiter Eintrag", "Agent", TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetByAufgabeAsync(_aufgabeId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result[0].Content.Should().Be("Erster Eintrag");
        result[1].Content.Should().Be("Zweiter Eintrag");
    }

    /// <summary>AddTestErgebnisseAsync erstellt Protokolleintrag mit TestErgebnissen.</summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldCreateEintragWithTestErgebnisse_WhenAllTestsPassed()
    {
        // Arrange
        var testResult = new TestRunResult(
            Passed: true,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("Test1", TestStatus.Passed, null, TimeSpan.FromMilliseconds(100)),
                new TestErgebnisInfo("Test2", TestStatus.Passed, null, TimeSpan.FromMilliseconds(200))
            });

        // Act
        var result = await _sut.AddTestErgebnisseAsync(_aufgabeId, testResult, TestContext.Current.CancellationToken);

        // Assert
        result.Type.Should().Be(LogEntryType.TestResult);
        result.Content.Should().Contain("2");
        result.TestResults.Should().HaveCount(2);
    }

    /// <summary>AddTestErgebnisseAsync erstellt korrekten Inhalt wenn Tests fehlgeschlagen.</summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldCreateFailureMessage_WhenSomeTestsFailed()
    {
        // Arrange
        var testResult = new TestRunResult(
            Passed: false,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("Test1", TestStatus.Passed, null, TimeSpan.FromMilliseconds(100)),
                new TestErgebnisInfo("Test2", TestStatus.Failed, "Assert failed", TimeSpan.FromMilliseconds(50))
            });

        // Act
        var result = await _sut.AddTestErgebnisseAsync(_aufgabeId, testResult, TestContext.Current.CancellationToken);

        // Assert
        result.Content.Should().Contain("fehlgeschlagen");
    }

    /// <summary>AddStatusUebergangAsync erstellt einen Statusübergangs-Eintrag.</summary>
    [Fact]
    public async Task AddStatusUebergangAsync_ShouldCreateStatusUebergangEintrag_WhenCalled()
    {
        // Arrange & Act
        var result = await _sut.AddStatusUebergangAsync(_aufgabeId, WorkItemStatus.New, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        // Assert
        result.Type.Should().Be(LogEntryType.StatusTransition);
        result.Content.Should().Contain("Neu");
        result.Content.Should().Contain("Gestartet");
    }

    /// <summary>SuchenAsync findet Einträge anhand des Suchbegriffs im Inhalt.</summary>
    [Fact]
    public async Task SuchenAsync_ShouldReturnMatchingEntries_WhenSuchbegriffMatchesInhalt()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.Prompt, "Fehlerhafte Implementierung", null, TestContext.Current.CancellationToken);
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.Prompt, "Gute Implementierung", null, TestContext.Current.CancellationToken);
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.Prompt, "Kein Treffer hier", null, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.SuchenAsync(_aufgabeId, "Implementierung", TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
    }

    /// <summary>SuchenAsync findet Einträge anhand des Suchbegriffs im AgentName.</summary>
    [Fact]
    public async Task SuchenAsync_ShouldReturnMatchingEntries_WhenSuchbegriffMatchesAgentName()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.AiResponse, "Inhalt A", "CopilotAgent", TestContext.Current.CancellationToken);
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.AiResponse, "Inhalt B", "AndererAgent", TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.SuchenAsync(_aufgabeId, "Copilot", TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].AgentName.Should().Be("CopilotAgent");
    }

    /// <summary>SuchenAsync gibt leere Liste zurück wenn kein Treffer.</summary>
    [Fact]
    public async Task SuchenAsync_ShouldReturnEmptyList_WhenNoMatchFound()
    {
        // Arrange
        await _sut.AddEintragAsync(_aufgabeId, LogEntryType.Prompt, "Kein Treffer", null, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.SuchenAsync(_aufgabeId, "XYZNOTFOUND", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>TestRateLimitMarkerParsing: Marker wird geparst und DateTimeOffset extrahiert.</summary>
    [Fact]
    public void TestRateLimitMarkerParsing()
    {
        // Gültiger Marker mit Zeitstempel
        var line = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-10T15:30:00Z]]";
        var found = ProtokollService.TryParseRateLimitMarker(line, out var resetUtc);

        found.Should().BeTrue();
        resetUtc.Should().NotBeNull();
        resetUtc!.Value.Year.Should().Be(2026);
        resetUtc!.Value.Month.Should().Be(6);
        resetUtc!.Value.Day.Should().Be(10);
        resetUtc!.Value.Hour.Should().Be(15);
        resetUtc!.Value.Minute.Should().Be(30);
    }

    /// <summary>Rate-Limit-Marker ohne Zeitstempel wird erkannt aber resetUtc bleibt null.</summary>
    [Fact]
    public void TryParseRateLimitMarker_WithoutTimestamp_ReturnsTrueButNullResetUtc()
    {
        var line = "[[SOFTWARESCHMIEDE_RATE_LIMIT]]";
        var found = ProtokollService.TryParseRateLimitMarker(line, out var resetUtc);

        found.Should().BeTrue();
        resetUtc.Should().BeNull();
    }

    /// <summary>Rate-Limit-Marker mit ungültigem Zeitstempel wird erkannt aber resetUtc bleibt null.</summary>
    [Fact]
    public void TryParseRateLimitMarker_WithInvalidTimestamp_ReturnsTrueButNullResetUtc()
    {
        var line = "[[SOFTWARESCHMIEDE_RATE_LIMIT:not-a-date]]";
        var found = ProtokollService.TryParseRateLimitMarker(line, out var resetUtc);

        found.Should().BeTrue();
        resetUtc.Should().BeNull();
    }

    /// <summary>Zeilen ohne Marker liefern false.</summary>
    [Fact]
    public void TryParseRateLimitMarker_WithNoMarker_ReturnsFalse()
    {
        var found = ProtokollService.TryParseRateLimitMarker("normale CLI-Ausgabe", out var resetUtc);

        found.Should().BeFalse();
        resetUtc.Should().BeNull();
    }

    /// <summary>Leere Zeile liefert false.</summary>
    [Fact]
    public void TryParseRateLimitMarker_WithEmptyLine_ReturnsFalse()
    {
        var found = ProtokollService.TryParseRateLimitMarker(string.Empty, out var resetUtc);

        found.Should().BeFalse();
        resetUtc.Should().BeNull();
    }

    /// <summary>AddCliOutputAsync speichert Ausgabe und erstellt zusätzlichen RateLimit-Eintrag bei Marker.</summary>
    [Fact]
    public async Task AddCliOutputAsync_ShouldCreateRateLimitEntry_WhenMarkerDetected()
    {
        var markerLine = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]";

        await _sut.AddCliOutputAsync(_aufgabeId, markerLine, TestContext.Current.CancellationToken);

        var eintraege = await _sut.GetByAufgabeAsync(_aufgabeId, TestContext.Current.CancellationToken);
        eintraege.Should().HaveCount(2);
        eintraege.Should().Contain(e => e.Type == LogEntryType.CliOutput);
        eintraege.Should().Contain(e => e.Type == LogEntryType.RateLimit);
    }

    /// <summary>AddCliOutputAsync speichert Ausgabezeile als CliOutput ohne Marker.</summary>
    [Fact]
    public async Task AddCliOutputAsync_ShouldCreateSingleEntry_WhenNoMarker()
    {
        await _sut.AddCliOutputAsync(_aufgabeId, "normale Ausgabe", TestContext.Current.CancellationToken);

        var eintraege = await _sut.GetByAufgabeAsync(_aufgabeId, TestContext.Current.CancellationToken);
        eintraege.Should().HaveCount(1);
        eintraege[0].Type.Should().Be(LogEntryType.CliOutput);
        eintraege[0].Content.Should().Be("normale Ausgabe");
    }
}
