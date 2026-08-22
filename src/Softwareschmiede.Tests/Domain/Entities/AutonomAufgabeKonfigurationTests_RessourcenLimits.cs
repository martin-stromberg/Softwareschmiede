using FluentAssertions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Entities;

/// <summary>Tests für den <see cref="AutonomAufgabeKonfiguration.RessourcenLimits"/>-Wrapper.</summary>
public sealed class AutonomAufgabeKonfigurationTests_RessourcenLimits
{
    /// <summary>Der Getter liefert eine RessourcenLimits-Instanz mit den Werten von TokenBudget, TokenBudgetErweitert und LaufzeitLimitMinuten.</summary>
    [Fact]
    public void RessourcenLimits_Get_ShouldReturnInstance_WithTokenBudgetAndLaufzeitLimit()
    {
        // Arrange
        var konfiguration = new AutonomAufgabeKonfiguration
        {
            TokenBudget = 100_000,
            TokenBudgetErweitert = 150_000,
            LaufzeitLimitMinuten = 90
        };

        // Act
        var ressourcenLimits = konfiguration.RessourcenLimits;

        // Assert
        ressourcenLimits.Should().Be(new RessourcenLimits(100_000, 150_000, 90));
    }

    /// <summary>Der Setter schreibt TokenBudget, TokenBudgetErweitert und LaufzeitLimitMinuten korrekt in die zugrunde liegenden Felder.</summary>
    [Fact]
    public void RessourcenLimits_Set_ShouldWriteTokenBudgetAndLaufzeitLimit()
    {
        // Arrange
        var konfiguration = new AutonomAufgabeKonfiguration();

        // Act
        konfiguration.RessourcenLimits = new RessourcenLimits(200_000, null, 60);

        // Assert
        konfiguration.TokenBudget.Should().Be(200_000);
        konfiguration.TokenBudgetErweitert.Should().BeNull();
        konfiguration.LaufzeitLimitMinuten.Should().Be(60);
    }
}
