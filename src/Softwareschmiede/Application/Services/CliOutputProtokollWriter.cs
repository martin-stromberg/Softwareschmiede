using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>Schreibt Terminal-Ausgabe einer Aufgabe nicht blockierend in das bestehende Aufgabenprotokoll.</summary>
public sealed class CliOutputProtokollWriter : ITerminalOutputSink
{
    internal const int QueueCapacity = 4096;
    private const int QueueWarningThreshold = 1000;
    private const int QueueWarningRepeatInterval = 1000;

    private readonly Guid _aufgabeId;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CliOutputProtokollWriter> _logger;
    private readonly Channel<string> _lines = Channel.CreateBounded<string>(new BoundedChannelOptions(QueueCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly CliOutputLineAccumulator _accumulator = new();
    private readonly object _sync = new();
    private readonly Task _workerTask;
    private int _queuedLineCount;
    private int _nextQueueWarningThreshold = QueueWarningThreshold;
    private int _completed;

    /// <summary>Erstellt einen Writer fuer eine konkrete Aufgabe.</summary>
    /// <param name="aufgabeId">Aufgabe, zu der die Ausgabe gehoert.</param>
    /// <param name="scopeFactory">Factory fuer scoped Zugriff auf <see cref="ProtokollService"/>.</param>
    /// <param name="logger">Logger fuer Persistenzfehler.</param>
    public CliOutputProtokollWriter(Guid aufgabeId, IServiceScopeFactory scopeFactory, ILogger<CliOutputProtokollWriter> logger)
    {
        _aufgabeId = aufgabeId;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _workerTask = Task.Run(ProcessLinesAsync);
    }

    /// <inheritdoc/>
    public void OnOutputChunk(ReadOnlySpan<byte> bytes)
    {
        lock (_sync)
        {
            if (Volatile.Read(ref _completed) != 0)
                return;

            foreach (var line in _accumulator.Append(bytes))
                TryQueueLine(line);
        }
    }

    /// <inheritdoc/>
    public void Complete()
    {
        lock (_sync)
        {
            CompleteLocked();
        }
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var hasTimeout = timeout > TimeSpan.Zero;
        var remainingTimeout = timeout;
        var stopwatch = hasTimeout ? Stopwatch.StartNew() : null;

        if (hasTimeout)
        {
            if (!Monitor.TryEnter(_sync, timeout))
            {
                _logger.LogWarning(
                    "Timeout beim Abschluss der CLI-Ausgabe fuer Aufgabe {AufgabeId}. Eine aktive Producer-Phase schreibt noch in die Queue. Noch ausstehende Zeilen: {QueuedLineCount}.",
                    _aufgabeId,
                    Volatile.Read(ref _queuedLineCount));
                return;
            }
        }
        else
        {
            Monitor.Enter(_sync);
        }

        try
        {
            CompleteLocked();
        }
        finally
        {
            Monitor.Exit(_sync);
        }

        if (hasTimeout)
        {
            remainingTimeout = timeout - stopwatch!.Elapsed;
            if (remainingTimeout <= TimeSpan.Zero)
            {
                _logger.LogWarning(
                    "Timeout beim Drain der CLI-Ausgabe fuer Aufgabe {AufgabeId}. Noch ausstehende Zeilen: {QueuedLineCount}.",
                    _aufgabeId,
                    Volatile.Read(ref _queuedLineCount));
                return;
            }
        }

        try
        {
            if (!hasTimeout)
            {
                await _workerTask.WaitAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await _workerTask.WaitAsync(remainingTimeout, ct).ConfigureAwait(false);
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(
                ex,
                "Timeout beim Drain der CLI-Ausgabe fuer Aufgabe {AufgabeId}. Noch ausstehende Zeilen: {QueuedLineCount}.",
                _aufgabeId,
                Volatile.Read(ref _queuedLineCount));
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Drain der CLI-Ausgabe fuer Aufgabe {AufgabeId} wurde abgebrochen. Noch ausstehende Zeilen: {QueuedLineCount}.",
                _aufgabeId,
                Volatile.Read(ref _queuedLineCount));
        }
    }

    private void CompleteLocked()
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        foreach (var line in _accumulator.Flush())
            TryQueueLine(line);

        _lines.Writer.TryComplete();
    }

    private void TryQueueLine(string line)
    {
        if (!_lines.Writer.TryWrite(line))
        {
            _logger.LogWarning(
                "CLI-Ausgabe-Queue fuer Aufgabe {AufgabeId} ist voll ({QueueCapacity} Zeilen). Terminal-Output-Reader wartet auf Persistenz.",
                _aufgabeId,
                QueueCapacity);

            while (!_lines.Writer.TryWrite(line))
            {
                if (!_lines.Writer.WaitToWriteAsync().AsTask().GetAwaiter().GetResult())
                    return;
            }
        }

        var queued = Interlocked.Increment(ref _queuedLineCount);
        var threshold = Volatile.Read(ref _nextQueueWarningThreshold);
        if (queued >= threshold && Interlocked.CompareExchange(
                ref _nextQueueWarningThreshold,
                threshold + QueueWarningRepeatInterval,
                threshold) == threshold)
        {
            _logger.LogWarning(
                "CLI-Ausgabe-Queue fuer Aufgabe {AufgabeId} enthaelt {QueuedLineCount} Zeilen. Persistenz faellt hinter die Terminal-Ausgabe zurueck.",
                _aufgabeId,
                queued);
        }
    }

    private async Task ProcessLinesAsync()
    {
        try
        {
            await foreach (var line in _lines.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    await PersistLineAsync(line).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _queuedLineCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler beim Verarbeiten der CLI-Ausgabe fuer Aufgabe {AufgabeId}.", _aufgabeId);
        }
    }

    private async Task PersistLineAsync(string line)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var protokollService = scope.ServiceProvider.GetRequiredService<ProtokollService>();
            await protokollService.AddCliOutputAsync(_aufgabeId, line).ConfigureAwait(false);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(
                ex,
                "CLI-Ausgabe fuer Aufgabe {AufgabeId} konnte nicht persistiert werden, da der ServiceProvider bereits disposed wurde.",
                _aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI-Ausgabe fuer Aufgabe {AufgabeId} konnte nicht persistiert werden.", _aufgabeId);
        }
    }
}
