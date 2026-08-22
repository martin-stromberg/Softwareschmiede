using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für <see cref="AufgabeExtensions.IstAutonom"/>, den alleinigen Modus-Indikator (regulär/autonom).</summary>
public sealed class AufgabeTests_IstAutonom
{
    /// <summary>IstAutonom liefert false, wenn AutonomKonfiguration nicht gesetzt (bzw. nicht geladen) ist.</summary>
    [Fact]
    public void IstAutonom_ShouldReturnFalse_WhenAutonomKonfigurationIsNull()
    {
        // Arrange
        var aufgabe = new Aufgabe { AutonomKonfiguration = null };

        // Act & Assert
        aufgabe.IstAutonom().Should().BeFalse();
    }

    /// <summary>IstAutonom liefert true, wenn AutonomKonfiguration gesetzt ist.</summary>
    [Fact]
    public void IstAutonom_ShouldReturnTrue_WhenAutonomKonfigurationIsSet()
    {
        // Arrange
        var aufgabe = new Aufgabe
        {
            AutonomKonfiguration = new AutonomAufgabeKonfiguration
            {
                Id = Guid.NewGuid(),
                ProjektBranchName = "feature/autonom",
                InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
                PermissionsJsonPfad = @"C:\arbeitsverzeichnis\permissions.json",
                ArbeitsverzeichnisPfad = @"C:\arbeitsverzeichnis"
            }
        };

        // Act & Assert
        aufgabe.IstAutonom().Should().BeTrue();
    }

    /// <summary>
    /// Verifiziert die im Refactoring-Plan vorausgesetzte EF-Relationship-Fixup: Wenn eine Aufgabe und ihre
    /// AutonomAufgabeKonfiguration im selben DbContext getrackt werden (wie es
    /// <see cref="ProjektleiterAgentServiceTestDatenFactory.ErstelleAufgabeUndKonfiguration"/> tut), setzt EF Core
    /// die Navigation Property <see cref="Aufgabe.AutonomKonfiguration"/> automatisch, ohne dass sie explizit
    /// zugewiesen werden muss.
    /// </summary>
    [Fact]
    public void IstAutonom_ShouldBeTrue_AfterErstelleAufgabeUndKonfiguration()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "AufgabeIstAutonomFixup", Guid.NewGuid().ToString("N"));

        // Act
        var (aufgabe, _) = ProjektleiterAgentServiceTestDatenFactory.ErstelleAufgabeUndKonfiguration(db, Guid.NewGuid(), testRoot);

        // Assert
        aufgabe.IstAutonom().Should().BeTrue();
    }
}
