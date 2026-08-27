using Moq;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erweiterungsmethoden für <see cref="Mock{IGitPlugin}"/>, die in mehreren Testklassen wiederverwendete Standard-Setups bündeln.</summary>
internal static class GitPluginMockExtensions
{
    /// <summary>
    /// Richtet <see cref="IGitPlugin.ResolveEffectiveRepositoryPathAsync"/> als reinen Passthrough ein, der den
    /// übergebenen Pfad unverändert zurückgibt (Standardverhalten aller Plugins außer <c>LocalDirectoryPlugin</c>
    /// im InSourceDirectory-Modus).
    /// </summary>
    /// <param name="gitPluginMock">Der zu konfigurierende Mock.</param>
    /// <returns>Derselbe Mock, zur Verkettung weiterer Setups.</returns>
    public static Mock<IGitPlugin> SetupPassthroughResolveEffectiveRepositoryPath(this Mock<IGitPlugin> gitPluginMock)
    {
        gitPluginMock
            .Setup(p => p.ResolveEffectiveRepositoryPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((pfad, _) => Task.FromResult(pfad));
        return gitPluginMock;
    }
}
