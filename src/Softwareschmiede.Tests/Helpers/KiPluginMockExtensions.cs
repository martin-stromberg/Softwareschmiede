using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Hilfsmethoden zum Aufsetzen der Terminal-Spec-Rückgabe auf <see cref="Mock{T}"/>-Plugins.</summary>
internal static class KiPluginMockExtensions
{
    /// <summary>Konfiguriert <see cref="IKiPlugin.GetTerminalStartSpecAsync"/> des Mocks mit einer einfachen
    /// <see cref="TerminalSessionStartSpec"/> (FileName/Arguments), damit der interaktive Startpfad in Tests
    /// ohne echte Plugin-Implementierung läuft.</summary>
    /// <param name="pluginMock">Der Plugin-Mock.</param>
    /// <param name="fileName">Der zurückzugebende Executable-Name (Default: <c>cmd.exe</c>).</param>
    /// <param name="arguments">Die zurückzugebenden Argumente.</param>
    /// <returns>Der Mock für verkettete Aufrufe.</returns>
    internal static Mock<IKiPlugin> SetupTerminalSpec(this Mock<IKiPlugin> pluginMock, string fileName = "cmd.exe", string arguments = "")
    {
        pluginMock
            .Setup(p => p.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TerminalSessionStartSpec
            {
                FileName = fileName,
                Arguments = arguments,
                PluginName = "TestPlugin",
            });
        return pluginMock;
    }
}
