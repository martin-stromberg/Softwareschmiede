using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt OS-freie KiAusfuehrungsService-Instanzen fuer regulaere Tests.</summary>
public static class TestKiAusfuehrungsServiceFactory
{
    /// <summary>Erstellt einen KiAusfuehrungsService mit einem deterministischen PseudoConsole-Launcher.</summary>
    public static KiAusfuehrungsService Create()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        return new KiAusfuehrungsService(
            NullLogger<KiAusfuehrungsService>.Instance,
            NullLoggerFactory.Instance,
            scopeFactoryMock.Object,
            new DeterministicPseudoConsoleProcessLauncher());
    }

    private sealed class DeterministicPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
    {
        public (Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle) Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand, ITerminalOutputSink? outputSink = null)
        {
            var process = Process.GetCurrentProcess();
            var session = new PseudoConsoleSession(
                NullPseudoConsoleHandle.Instance,
                process,
                new MemoryStream(),
                new MemoryStream(),
                outputSink: outputSink);
            return (process, session, IntPtr.Zero);
        }
    }
}
