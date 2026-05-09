using FluentAssertions;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.ValueObjects;

/// <summary>Tests für den CliResult Value Object.</summary>
public sealed class CliResultTests
{
    /// <summary>IsSuccess gibt true zurück wenn ExitCode 0 ist.</summary>
    [Fact]
    public void IsSuccess_ShouldReturnTrue_WhenExitCodeIsZero()
    {
        // Arrange
        var result = new CliResult(0, "output", string.Empty);

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>IsSuccess gibt false zurück wenn ExitCode ungleich 0 ist.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(128)]
    public void IsSuccess_ShouldReturnFalse_WhenExitCodeIsNonZero(int exitCode)
    {
        // Arrange
        var result = new CliResult(exitCode, string.Empty, "error");

        // Act & Assert
        result.IsSuccess.Should().BeFalse();
    }
}
