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
}
