using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt PseudoConsoleSession-Instanzen ohne echte ConPTY-Handles fuer regulaere Tests.</summary>
public static class TestPseudoConsoleSessionFactory
{
    /// <summary>Erstellt eine Session mit No-Op-PseudoConsole und aktuellem Testprozess.</summary>
    /// <param name="inputStream">Der an den (simulierten) Prozess gebundene Eingabe-Stream.</param>
    /// <param name="outputStream">Der vom (simulierten) Prozess gelesene Ausgabe-Stream.</param>
    /// <param name="logger">Optionaler Logger.</param>
    /// <param name="outputSink">Optionale Ausgabe-Senke (Rohbytes/Protokollierung).</param>
    /// <param name="options">Optionale <see cref="TerminalSessionOptions"/>-Einstellungen (Default: <c>new()</c>) —
    /// Test-Doubles ohne <c>IOptions</c>-Injektion.</param>
    /// <param name="promptSubmitDelay">Optionale Submit-Verzögerung (<see cref="PseudoConsoleSessionContext.PromptSubmitDelay"/>).</param>
    /// <param name="promptSubmitRepeatCount">Optionale Submit-Wiederholungen (<see cref="PseudoConsoleSessionContext.PromptSubmitRepeatCount"/>).</param>
    /// <param name="promptSubmitRepeatInterval">Abstand der Submit-Wiederholungen (<see cref="PseudoConsoleSessionContext.PromptSubmitRepeatInterval"/>).</param>
    public static PseudoConsoleSession Create(
        Stream inputStream,
        Stream outputStream,
        ILogger? logger = null,
        ITerminalOutputSink? outputSink = null,
        TerminalSessionOptions? options = null,
        TimeSpan promptSubmitDelay = default,
        int promptSubmitRepeatCount = 0,
        TimeSpan promptSubmitRepeatInterval = default)
        => new(
            NullPseudoConsoleHandle.Instance,
            Process.GetCurrentProcess(),
            inputStream,
            outputStream,
            new PseudoConsoleSessionContext
            {
                Logger = logger,
                OutputSink = outputSink,
                Options = options ?? new TerminalSessionOptions(),
                PromptSubmitDelay = promptSubmitDelay,
                PromptSubmitRepeatCount = promptSubmitRepeatCount,
                PromptSubmitRepeatInterval = promptSubmitRepeatInterval,
            });

    /// <summary>Erstellt eine Session mit kontrollierbarer Zeitquelle und No-Op-PseudoConsole.</summary>
    /// <param name="inputStream">Der an den (simulierten) Prozess gebundene Eingabe-Stream.</param>
    /// <param name="outputStream">Der vom (simulierten) Prozess gelesene Ausgabe-Stream.</param>
    /// <param name="timeProvider">Die zu verwendende Zeitquelle.</param>
    /// <param name="waitingThreshold">Warteschwelle für den Runtime-Status.</param>
    /// <param name="logger">Optionaler Logger.</param>
    /// <param name="outputSink">Optionale Ausgabe-Senke.</param>
    /// <param name="options">Optionale <see cref="TerminalSessionOptions"/>-Einstellungen.</param>
    public static PseudoConsoleSession Create(
        Stream inputStream,
        Stream outputStream,
        TimeProvider timeProvider,
        TimeSpan waitingThreshold,
        ILogger? logger = null,
        ITerminalOutputSink? outputSink = null,
        TerminalSessionOptions? options = null)
        => new(
            NullPseudoConsoleHandle.Instance,
            Process.GetCurrentProcess(),
            inputStream,
            outputStream,
            new PseudoConsoleSessionContext
            {
                Logger = logger,
                OutputSink = outputSink,
                Options = options ?? new TerminalSessionOptions(),
                TimeProvider = timeProvider,
                WaitingThreshold = waitingThreshold,
            });
}
