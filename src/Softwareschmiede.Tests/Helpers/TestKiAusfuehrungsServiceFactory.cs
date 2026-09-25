using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt OS-freie KiAusfuehrungsService-Instanzen fuer regulaere Tests.</summary>
public static class TestKiAusfuehrungsServiceFactory
{
    /// <summary>Erstellt einen KiAusfuehrungsService mit einer deterministischen, In-Memory-Session
    /// (Prozess = CurrentProcess, Streams = MemoryStream — kein echter Prozessstart).</summary>
    public static KiAusfuehrungsService Create()
        => Create(new Mock<IServiceScopeFactory>().Object);

    /// <summary>Erstellt einen KiAusfuehrungsService mit eigener ScopeFactory und optionalem
    /// Launcher-Double (wird über <see cref="TestTerminalSessionFactory"/> in die
    /// <see cref="ITerminalSessionFactory"/>-Naht verpackt).</summary>
    /// <param name="scopeFactory">ScopeFactory-Mock oder echter DI-Provider.</param>
    /// <param name="logger">Optionaler Logger für den Service.</param>
    /// <param name="launcher">Optionaler Launcher für den Terminal-Session-Pfad; Default ist der
    /// deterministische In-Memory-Launcher.</param>
    public static KiAusfuehrungsService Create(
        IServiceScopeFactory scopeFactory,
        ILogger<KiAusfuehrungsService>? logger = null,
        IPseudoConsoleProcessLauncher? launcher = null)
        => new(
            logger ?? NullLogger<KiAusfuehrungsService>.Instance,
            NullLoggerFactory.Instance,
            scopeFactory,
            new TestTerminalSessionFactory(launcher));
}
