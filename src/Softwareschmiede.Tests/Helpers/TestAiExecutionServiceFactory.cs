using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt OS-freie KiAusfuehrungsService-Instanzen fuer regulaere Tests.</summary>
public static class TestAiExecutionServiceFactory
{
    /// <summary>Erstellt einen KiAusfuehrungsService mit einer deterministischen, In-Memory-Session
    /// (Prozess = CurrentProcess, Streams = MemoryStream — kein echter Prozessstart).</summary>
    public static AiExecutionService Create()
        => Create(new Mock<IServiceScopeFactory>().Object);

    /// <summary>Erstellt einen KiAusfuehrungsService mit eigener ScopeFactory und optionalem
    /// Launcher-Double (wird über <see cref="TestTerminalSessionFactory"/> in die
    /// <see cref="ITerminalSessionFactory"/>-Naht verpackt).</summary>
    /// <param name="scopeFactory">ScopeFactory-Mock oder echter DI-Provider.</param>
    /// <param name="logger">Optionaler Logger für den Service.</param>
    /// <param name="launcher">Optionaler Launcher für den Terminal-Session-Pfad; Default ist der
    /// deterministische In-Memory-Launcher.</param>
    /// <param name="terminalOptions">Optionale <see cref="TerminalSessionOptions"/> (z. B. zum
    /// Deaktivieren der Rohbyte-Aufzeichnung via <c>AufzeichnungByteBudget</c>); Default: <c>new()</c>.</param>
    /// <param name="timeProvider">Optionale Zeitquelle für die Aufzeichnungs-Zeitstempel; Default: <see cref="TimeProvider.System"/>.</param>
    public static AiExecutionService Create(
        IServiceScopeFactory scopeFactory,
        ILogger<AiExecutionService>? logger = null,
        IPseudoConsoleProcessLauncher? launcher = null,
        TerminalSessionOptions? terminalOptions = null,
        TimeProvider? timeProvider = null)
        => new(
            logger ?? NullLogger<AiExecutionService>.Instance,
            NullLoggerFactory.Instance,
            scopeFactory,
            new TestTerminalSessionFactory(launcher),
            Options.Create(terminalOptions ?? new TerminalSessionOptions()),
            timeProvider ?? TimeProvider.System);
}
