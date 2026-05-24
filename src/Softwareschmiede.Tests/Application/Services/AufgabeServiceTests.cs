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
        result.Status.Should().Be(AufgabeStatus.Offen);
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

    /// <summary>StartenAsync setzt Status auf InBearbeitung und setzt Branch und KlonPfad.</summary>
    [Fact]
    public async Task StartenAsync_ShouldSetStatusInBearbeitungAndBranchName_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu startende Aufgabe", null);

        // Act
        await _sut.StartenAsync(aufgabe.Id, "feature/test-branch", "/tmp/klon");

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.InBearbeitung);
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

    /// <summary>KiAktiviertAsync setzt Status auf KiAktiv.</summary>
    [Fact]
    public async Task KiAktiviertAsync_ShouldSetStatusKiAktiv_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "KI-Aufgabe", null);

        // Act
        await _sut.KiAktiviertAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.KiAktiv);
    }

    /// <summary>KiAbgeschlossenAsync setzt Status zurück auf InBearbeitung.</summary>
    [Fact]
    public async Task KiAbgeschlossenAsync_ShouldSetStatusInBearbeitung_WhenAufgabeIsKiAktiv()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "KI fertig Aufgabe", null);
        await _sut.KiAktiviertAsync(aufgabe.Id);

        // Act
        await _sut.KiAbgeschlossenAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.InBearbeitung);
    }

    /// <summary>AbschliessenAsync setzt Status auf Abgeschlossen, löscht Branch und KlonPfad und setzt AbschlussDatum.</summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusAbgeschlossenAndClearBranch_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Abzuschließende Aufgabe", null);
        await _sut.StartenAsync(aufgabe.Id, "feature/branch", "/tmp/klon");

        // Act
        await _sut.AbschliessenAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Abgeschlossen);
        result.BranchName.Should().BeNull();
        result.LokalerKlonPfad.Should().BeNull();
        result.AbschlussDatum.Should().NotBeNull();
    }

    /// <summary>AbbrechenAsync setzt Status zurück auf Offen und löscht Branch und KlonPfad.</summary>
    [Fact]
    public async Task AbbrechenAsync_ShouldSetStatusOffenAndClearBranch_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Abzubrechende Aufgabe", null);
        await _sut.StartenAsync(aufgabe.Id, "feature/branch-to-abort", "/tmp/klon-abort");

        // Act
        await _sut.AbbrechenAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Offen);
        result.BranchName.Should().BeNull();
        result.LokalerKlonPfad.Should().BeNull();
    }

    /// <summary>FehlgeschlagenAsync setzt Status auf Fehlgeschlagen.</summary>
    [Fact]
    public async Task FehlgeschlagenAsync_ShouldSetStatusFehlgeschlagen_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Fehlgeschlagene Aufgabe", null);

        // Act
        await _sut.FehlgeschlagenAsync(aufgabe.Id);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Fehlgeschlagen);
    }

    /// <summary>UpdateAsync aktualisiert Titel und AgentenInfos.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldUpdateTitelAndAgentenInfos_WhenAufgabeExists()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Alter Titel", "Alte Beschreibung");

        // Act
        await _sut.UpdateAsync(aufgabe.Id, "Neuer Titel", "Neue Beschreibung", "mein-paket", "mein-agent");

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Titel.Should().Be("Neuer Titel");
        result.AnforderungsBeschreibung.Should().Be("Neue Beschreibung");
        result.AgentenpaketName.Should().Be("mein-paket");
        result.AgentenName.Should().Be("mein-agent");
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

    /// <summary>VerwerfenAsync archiviert eine offene Aufgabe.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldSetStatusArchiviert_WhenOffeneAufgabeArchiviertWird()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu verwerfende Aufgabe", null);

        // Act
        await _sut.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Archivieren);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Archiviert);
    }

    /// <summary>VerwerfenAsync löscht eine offene Aufgabe dauerhaft.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldRemoveAufgabe_WhenOffeneAufgabeGeloeschtWird()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Zu verwerfende Aufgabe", null);

        // Act
        await _sut.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Loeschen);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result.Should().BeNull();
    }

    /// <summary>VerwerfenAsync wirft eine Ausnahme, wenn die Aufgabe nicht offen ist.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldThrowInvalidOperationException_WhenAufgabeIsNotOffen()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Bereits gestartete Aufgabe", null);
        await _sut.StartenAsync(aufgabe.Id, "feature/test", @"C:\klon");

        // Act
        var act = () => _sut.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Archivieren);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nur offene Aufgaben können verworfen werden.");
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
        await _sut.StatusSetzenAsync(aufgabe.Id, AufgabeStatus.Fehlgeschlagen);

        // Assert
        var result = await _sut.GetByIdAsync(aufgabe.Id);
        result!.Status.Should().Be(AufgabeStatus.Fehlgeschlagen);
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
