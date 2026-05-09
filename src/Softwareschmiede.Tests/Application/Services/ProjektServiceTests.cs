using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ProjektService.</summary>
public sealed class ProjektServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ILogger<ProjektService>> _loggerMock;
    private readonly ProjektService _sut;

    public ProjektServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<ProjektService>>();
        _sut = new ProjektService(_db, _loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>CreateAsync erstellt ein neues Projekt mit Aktiv-Status und gibt es zurück.</summary>
    [Fact]
    public async Task CreateAsync_ShouldCreateProjektWithAktivStatus_WhenCalledWithValidName()
    {
        // Arrange
        const string name = "Testprojekt";
        const string beschreibung = "Eine Beschreibung";

        // Act
        var result = await _sut.CreateAsync(name, beschreibung);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(name);
        result.Beschreibung.Should().Be(beschreibung);
        result.Status.Should().Be(ProjektStatus.Aktiv);
        result.ErstellungsDatum.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>CreateAsync persistiert das Projekt in der Datenbank.</summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistProjekt_WhenCalledWithValidName()
    {
        // Arrange & Act
        var result = await _sut.CreateAsync("Persistiertes Projekt", null);

        // Assert
        var persisted = await _sut.GetByIdAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Persistiertes Projekt");
    }

    /// <summary>GetAllAsync gibt alle Projekte alphabetisch sortiert zurück.</summary>
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllProjekteOrderedByName_WhenMultipleProjekteExist()
    {
        // Arrange
        await _sut.CreateAsync("Zebra Projekt", null);
        await _sut.CreateAsync("Alpha Projekt", null);
        await _sut.CreateAsync("Mitte Projekt", null);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alpha Projekt");
        result[1].Name.Should().Be("Mitte Projekt");
        result[2].Name.Should().Be("Zebra Projekt");
    }

    /// <summary>GetByIdAsync gibt null zurück wenn kein Projekt mit der ID existiert.</summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _sut.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>UpdateAsync aktualisiert Name und Beschreibung des Projekts.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldUpdateNameAndBeschreibung_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Alter Name", "Alte Beschreibung");

        // Act
        var result = await _sut.UpdateAsync(projekt.Id, "Neuer Name", "Neue Beschreibung");

        // Assert
        result.Name.Should().Be("Neuer Name");
        result.Beschreibung.Should().Be("Neue Beschreibung");
    }

    /// <summary>UpdateAsync wirft InvalidOperationException wenn Projekt nicht gefunden.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.UpdateAsync(nonExistentId, "Name", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentId}*");
    }

    /// <summary>ArchivierenAsync setzt den Status auf Archiviert.</summary>
    [Fact]
    public async Task ArchivierenAsync_ShouldSetStatusToArchiviert_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Zu archivierendes Projekt", null);

        // Act
        await _sut.ArchivierenAsync(projekt.Id);

        // Assert
        var result = await _sut.GetByIdAsync(projekt.Id);
        result!.Status.Should().Be(ProjektStatus.Archiviert);
    }

    /// <summary>ArchivierenAsync wirft InvalidOperationException wenn Projekt nicht gefunden.</summary>
    [Fact]
    public async Task ArchivierenAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.ArchivierenAsync(nonExistentId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>DeleteAsync entfernt das Projekt aus der Datenbank.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveProjekt_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Zu löschendes Projekt", null);

        // Act
        await _sut.DeleteAsync(projekt.Id);

        // Assert
        var result = await _sut.GetByIdAsync(projekt.Id);
        result.Should().BeNull();
    }

    /// <summary>DeleteAsync wirft InvalidOperationException wenn Projekt nicht gefunden.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.DeleteAsync(nonExistentId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>AddRepositoryAsync fügt ein Repository zum Projekt hinzu.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldAddRepository_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt mit Repository", null);

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/repo",
            "test/repo");

        // Assert
        repo.Should().NotBeNull();
        repo.ProjektId.Should().Be(projekt.Id);
        repo.RepositoryUrl.Should().Be("https://github.com/test/repo");
        repo.RepositoryName.Should().Be("test/repo");
        repo.Aktiv.Should().BeTrue();
    }

    /// <summary>RemoveRepositoryAsync entfernt ein Repository aus dem Projekt.</summary>
    [Fact]
    public async Task RemoveRepositoryAsync_ShouldRemoveRepository_WhenRepositoryExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt für Repository-Entfernung", null);
        var repo = await _sut.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/test/repo2", "test/repo2");

        // Act
        await _sut.RemoveRepositoryAsync(repo.Id);

        // Assert
        var detail = await _sut.GetDetailAsync(projekt.Id);
        detail!.Repositories.Should().BeEmpty();
    }
}
