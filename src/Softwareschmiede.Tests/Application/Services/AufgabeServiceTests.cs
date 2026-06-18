using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den AufgabeService.</summary>
public sealed class AufgabeServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ILogger<AufgabeService>> _loggerMock;
    private readonly AufgabeService _sut;
    private readonly Guid _projektId = new Guid("11111111-1111-1111-1111-111111111111");

    public AufgabeServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<AufgabeService>>();
        _sut = new AufgabeService(_db, _loggerMock.Object);

        // Seed a project for FK constraints
        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>CreateAsync erstellt eine Aufgabe mit Status Offen.</summary>
    [Fact]
    public async Task CreateAsync_ShouldCreateAufgabeWithStatusOffen_WhenCalledWithValidData()
    {
        // Arrange & Act
        var result = await _sut.CreateAsync(_projektId, "Test Aufgabe", "Beschreibung");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Titel.Should().Be("Test Aufgabe");
        result.Status.Should().Be(AufgabeStatus.Neu);
        result.ProjektId.Should().Be(_projektId);
    }

    /// <summary>CreateFromIssueAsync erstellt eine Aufgabe aus einem Issue mit IssueReferenz.</summary>
    [Fact]
    public async Task CreateFromIssueAsync_ShouldCreateAufgabeWithIssueReferenz_WhenCalledWithValidIssue()
    {
        // Arrange
        var issue = new Issue(
            Nummer: 42,
            Titel: "Fix login bug",
            Body: "Der Login funktioniert nicht.",
            Labels: new[] { "bug", "priority-high" },
            Milestone: "v1.0",
            IssueUrl: "https://github.com/test/repo/issues/42");

        // Act
        var result = await _sut.CreateFromIssueAsync(_projektId, issue);

        // Assert
        result.Titel.Should().Be("Fix login bug");
        result.IssueReferenz.Should().NotBeNull();
        result.IssueReferenz!.IssueNummer.Should().Be(42);
        result.IssueReferenz.Milestone.Should().Be("v1.0");
    }

    /// <summary>GetByProjektAsync gibt alle Aufgaben eines Projekts zurück.</summary>
    [Fact]
    public async Task GetByProjektAsync_ShouldReturnAufgabenForProjekt_WhenAufgabenExist()
    {
        // Arrange
        await _sut.CreateAsync(_projektId, "Aufgabe 1", null);
        await _sut.CreateAsync(_projektId, "Aufgabe 2", null);

        // Act
        var result = await _sut.GetByProjektAsync(_projektId);

        // Assert
        result.Should().HaveCount(2);
    }

    /// <summary>StartenAsync setzt Status auf Gestartet und setzt Branch und KlonPfad.</summary>
    [Fact]
    public async Task StartenAsync_ShouldSetStatusGestartetAndBranchName_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu startende Aufgabe", null);

        // Act
        await _sut.StartenAsync(aufgabe.Id, "feature/test-branch", "/tmp/klon");

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Gestartet);
        result.BranchName.Should().Be("feature/test-branch");
        result.LokalerKlonPfad.Should().Be("/tmp/klon");
    }

    /// <summary>GetLatestDiffResultIdForFileAsync liefert den neuesten dateispezifischen Diff auch bei unterschiedlicher Pfadnotation.</summary>
    [Fact]
    public async Task GetLatestDiffResultIdForFileAsync_ShouldReturnNewestMatchingDiff_WhenPathUsesDifferentSeparators()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Diff-Aufgabe", null);
        var older = CreateDiffResult(aufgabe.Id, @"src\module\alpha.cs", DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = CreateDiffResult(aufgabe.Id, "src/module/alpha.cs", DateTimeOffset.UtcNow);
        var otherFile = CreateDiffResult(aufgabe.Id, "src/module/beta.cs", DateTimeOffset.UtcNow.AddMinutes(1));

        _db.DiffResults.AddRange(older, newer, otherFile);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetLatestDiffResultIdForFileAsync(aufgabe.Id, "./src/module/alpha.cs");

        // Assert
        result.Should().Be(newer.Id);
    }

    /// <summary>GetLatestDiffResultIdForFileAsync liefert null, wenn kein Diff für die Datei vorhanden ist.</summary>
    [Fact]
    public async Task GetLatestDiffResultIdForFileAsync_ShouldReturnNull_WhenNoDiffForFileExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Diff-Aufgabe ohne Treffer", null);
        _db.DiffResults.Add(CreateDiffResult(aufgabe.Id, "src/module/existing.cs", DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetLatestDiffResultIdForFileAsync(aufgabe.Id, "src/module/missing.cs");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>StatusSetzenAsync setzt Status auf Gestartet via generische Methode.</summary>
    [Fact]
    public async Task StatusSetzenAsync_ShouldSetStatusGestartet_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "KI-Aufgabe", null);

        // Act
        await _sut.StatusSetzenAsync(aufgabe.Id, AufgabeStatus.Gestartet);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    /// <summary>AbschliessenAsync setzt Status auf Beendet und setzt AbschlussDatum.</summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusBeendetAndSetAbschlussDatum_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Abzuschließende Aufgabe", null);

        // Act
        await _sut.AbschliessenAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Beendet);
        result.AbschlussDatum.Should().NotBeNull();
    }

    /// <summary>UpdateAsync aktualisiert Titel, Agenteninfos und KI-Plugin.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldUpdateTitelAndAgentenInfos_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Alter Titel", "Alte Beschreibung");

        // Act
        await _sut.UpdateAsync(aufgabe.Id, "Neuer Titel", "Neue Beschreibung", "Softwareschmiede.Ki");

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Titel.Should().Be("Neuer Titel");
        result.AnforderungsBeschreibung.Should().Be("Neue Beschreibung");
        result.KiPluginPrefix.Should().Be("Softwareschmiede.Ki");
    }

    /// <summary>DeleteAsync entfernt die Aufgabe aus der Datenbank.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveAufgabe_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu löschende Aufgabe", null);

        // Act
        await _sut.DeleteAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result.Should().BeNull();
    }

    /// <summary>SavePromptVorschlagAsync speichert Prompt und Ausführungszeitpunkt.</summary>
    [Fact]
    public async Task SavePromptVorschlagAsync_ShouldPersistPromptAndSchedule_WhenValuesAreProvided()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Prompt-Vorschlag", null);
        var ausfuehrenAbUtc = new DateTimeOffset(2026, 06, 01, 11, 50, 0, TimeSpan.Zero);

        // Act
        await _sut.SavePromptVorschlagAsync(aufgabe.Id, "Mach nun bitte weiter.", ausfuehrenAbUtc);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.VorschlagPrompt.Should().Be("Mach nun bitte weiter.");
        result.VorschlagAusfuehrenAbUtc.Should().Be(ausfuehrenAbUtc);
    }

    /// <summary>SavePromptVorschlagAsync entfernt Zeitstempel, wenn der Prompt leer ist.</summary>
    [Fact]
    public async Task SavePromptVorschlagAsync_ShouldClearSchedule_WhenPromptIsWhitespace()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Prompt-Vorschlag löschen", null);
        await _sut.SavePromptVorschlagAsync(
            aufgabe.Id,
            "Mach nun bitte weiter.",
            new DateTimeOffset(2026, 06, 01, 11, 50, 0, TimeSpan.Zero));

        // Act
        await _sut.SavePromptVorschlagAsync(aufgabe.Id, "  ", new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero));

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.VorschlagPrompt.Should().BeNull();
        result.VorschlagAusfuehrenAbUtc.Should().BeNull();
    }

    /// <summary>ClearPromptVorschlagAsync entfernt Prompt und Ausführungszeitpunkt vollständig.</summary>
    [Fact]
    public async Task ClearPromptVorschlagAsync_ShouldRemovePromptAndSchedule_WhenValuesExist()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Prompt-Vorschlag entfernen", null);
        await _sut.SavePromptVorschlagAsync(
            aufgabe.Id,
            "Mach nun bitte weiter.",
            new DateTimeOffset(2026, 06, 01, 11, 50, 0, TimeSpan.Zero));

        // Act
        await _sut.ClearPromptVorschlagAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.VorschlagPrompt.Should().BeNull();
        result.VorschlagAusfuehrenAbUtc.Should().BeNull();
    }

    /// <summary>VerwerfenAsync archiviert eine neue Aufgabe.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldSetStatusArchiviert_WhenNeueAufgabeArchiviertWird()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu verwerfende Aufgabe", null);

        // Act
        await _sut.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Archivieren);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Archiviert);
    }

    /// <summary>VerwerfenAsync löscht eine neue Aufgabe dauerhaft.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldRemoveAufgabe_WhenNeueAufgabeGeloeschtWird()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu verwerfende Aufgabe", null);

        // Act
        await _sut.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Loeschen);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result.Should().BeNull();
    }

    /// <summary>VerwerfenAsync wirft eine Ausnahme, wenn die Aufgabe nicht neu ist.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldThrowInvalidOperationException_WhenAufgabeIsNotNeu()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Bereits gestartete Aufgabe", null);
        await _sut.StartenAsync(aufgabe.Id, "feature/test", @"C:\klon");

        // Act
        var act = () => _sut.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Archivieren);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nur neue Aufgaben können verworfen werden.");
    }

    /// <summary>StartenAsync wirft InvalidOperationException wenn Aufgabe nicht gefunden.</summary>
    [Fact]
    public async Task StartenAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.StartenAsync(nonExistentId, "branch", "/path");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>StatusSetzenAsync setzt den Status auf den angegebenen Wert.</summary>
    [Fact]
    public async Task StatusSetzenAsync_ShouldSetGivenStatus_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Status-Aufgabe", null);

        // Act
        await _sut.StatusSetzenAsync(aufgabe.Id, AufgabeStatus.Beendet);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Beendet);
    }

    /// <summary>TestHeartbeatUpdate: LastHeartbeatUtc wird aktualisiert; Alter wird korrekt berechnet.</summary>
    [Fact]
    public async Task TestHeartbeatUpdate()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Heartbeat-Aufgabe", null);
        aufgabe.LastHeartbeatUtc.Should().BeNull();

        // Act – Heartbeat aktualisieren
        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.UpdateHeartbeatAsync(aufgabe.Id);
        var nachUpdate = DateTimeOffset.UtcNow;

        // Assert – Heartbeat wurde gesetzt
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.LastHeartbeatUtc.Should().NotBeNull();
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));

        // Alter ist 0 Minuten (frischer Heartbeat)
        var ageMinutes = await _sut.GetHeartbeatAgeMinutesAsync(aufgabe.Id);
        ageMinutes.Should().NotBeNull();
        ageMinutes!.Value.Should().BeGreaterThanOrEqualTo(0);
        ageMinutes!.Value.Should().BeLessThan(2);
    }

    /// <summary>GetHeartbeatAgeMinutesAsync gibt null zurück wenn kein Heartbeat gesetzt.</summary>
    [Fact]
    public async Task GetHeartbeatAgeMinutesAsync_ShouldReturnNull_WhenNoHeartbeatSet()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "No-Heartbeat-Aufgabe", null);

        // Act
        var result = await _sut.GetHeartbeatAgeMinutesAsync(aufgabe.Id);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>TestStatusValidation: SetStatusAsync wirft InvalidStatusTransitionException bei ungültigem Übergang.</summary>
    [Fact]
    public async Task TestStatusValidation()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Validierungs-Aufgabe", null);

        // Act – ungültiger Übergang Neu → Beendet
        var act = () => _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);

        // Assert
        await act.Should().ThrowAsync<InvalidStatusTransitionException>()
            .WithMessage("*Neu*Beendet*");
    }

    /// <summary>SetStatusAsync erlaubt gültige Übergänge ohne Exception.</summary>
    [Fact]
    public async Task SetStatusAsync_ShouldSetStatus_WhenTransitionIsAllowed()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Gültiger-Übergang", null);

        // Act
        await _sut.SetStatusAsync(aufgabe.Id, AufgabeStatus.Gestartet);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    private static DiffResult CreateDiffResult(Guid aufgabeId, string filePath, DateTimeOffset generatedAt)
    {
        var diffResultId = Guid.NewGuid();
        var blockId = Guid.NewGuid();

        return new DiffResult
        {
            Id = diffResultId,
            AufgabeId = aufgabeId,
            FilePath = filePath,
            SourceVersion = "HEAD~1",
            TargetVersion = "HEAD",
            Status = DiffResultStatus.Generated,
            DiffType = DiffType.Full,
            GeneratedAt = generatedAt,
            GeneratedBy = nameof(AufgabeServiceTests),
            AddedLines = 1,
            RemovedLines = 0,
            ModifiedLines = 0,
            LineCount = 1,
            DiffBlocks =
            [
                new DiffBlock
                {
                    Id = blockId,
                    DiffResultId = diffResultId,
                    BlockType = DiffBlockType.Added,
                    BlockSequence = 0,
                    SourceStartLine = 1,
                    SourceEndLine = 1,
                    TargetStartLine = 1,
                    TargetEndLine = 1,
                    DiffLines =
                    [
                        new DiffLine
                        {
                            Id = Guid.NewGuid(),
                            DiffBlockId = blockId,
                            LineStatus = DiffLineStatus.Added,
                            Content = "added line",
                            TargetLineNumber = 1,
                            LineSequence = 0,
                        },
                    ],
                },
            ],
        };
    }
}
