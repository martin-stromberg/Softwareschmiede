using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt PseudoConsoleSession-Instanzen ohne echte ConPTY-Handles fuer regulaere Tests.</summary>
public static class TestPseudoConsoleSessionFactory
{
    /// <summary>Erstellt eine Session mit No-Op-PseudoConsole und aktuellem Testprozess.</summary>
    public static PseudoConsoleSession Create(Stream inputStream, Stream outputStream, ILogger? logger = null)
        => new(
            NullPseudoConsoleHandle.Instance,
            Process.GetCurrentProcess(),
            inputStream,
            outputStream,
            logger);

    /// <summary>Erstellt eine Session mit kontrollierbarer Zeitquelle und No-Op-PseudoConsole.</summary>
    public static PseudoConsoleSession Create(
        Stream inputStream,
        Stream outputStream,
        TimeProvider timeProvider,
        TimeSpan waitingThreshold,
        ILogger? logger = null)
        => new(
            NullPseudoConsoleHandle.Instance,
            Process.GetCurrentProcess(),
            inputStream,
            outputStream,
            timeProvider,
            waitingThreshold,
            logger);
}
