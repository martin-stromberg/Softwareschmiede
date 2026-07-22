using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using System.Text;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer <see cref="CliOutputProtokollWriter"/>.</summary>
public sealed class CliOutputProtokollWriterTests
{
    /// <summary>CompleteAsync wartet auf die Persistenz bereits angenommener Zeilen.</summary>
    [Fact]
    public async Task CompleteAsync_DraintAngenommeneZeilen_BevorProviderDisposedWird()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var aufgabeId = Guid.NewGuid();
        var sut = new CliOutputProtokollWriter(
            aufgabeId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CliOutputProtokollWriter>.Instance);

        sut.OnOutputChunk(System.Text.Encoding.UTF8.GetBytes("erste\nzweite ohne ende"));
        await sut.CompleteAsync(TimeSpan.FromSeconds(2));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext>();
        var eintraege = db.Protokolleintraege
            .AsNoTracking()
            .Where(e => e.AufgabeId == aufgabeId && e.Typ == ProtokollTyp.CliOutput)
            .OrderBy(e => e.Zeitstempel)
            .Select(e => e.Inhalt)
            .ToList();

        eintraege.Should().Equal("erste", "zweite ohne ende");
    }

    /// <summary>Persistenzfehler werden geloggt und schlagen nicht in den Terminal-Lesepfad zurueck.</summary>
    [Fact]
    public async Task CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new InvalidOperationException("Simulierter Scope-Fehler"));
        var loggerMock = new Mock<ILogger<CliOutputProtokollWriter>>();
        var sut = new CliOutputProtokollWriter(Guid.NewGuid(), scopeFactoryMock.Object, loggerMock.Object);

        var act = async () =>
        {
            sut.OnOutputChunk(System.Text.Encoding.UTF8.GetBytes("zeile\n"));
            await sut.CompleteAsync(TimeSpan.FromSeconds(2));
        };

        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(ex => ex is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    /// <summary>Bei Rueckstau begrenzt der Writer die Queue und erzeugt Backpressure statt unbegrenzt Speicher zu belegen.</summary>
    [Fact]
    public async Task CliOutputProtokollWriter_HoheAusgabe_BegrenztQueueMitBackpressure()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        using var persistenzBlockiert = new ManualResetEventSlim(false);
        var warningLogged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Callback(() => persistenzBlockiert.Wait(TimeSpan.FromSeconds(5)))
            .Throws(new InvalidOperationException("Simulierte langsame Persistenz"));

        var loggerMock = new Mock<ILogger<CliOutputProtokollWriter>>();
        loggerMock
            .Setup(l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => warningLogged.TrySetResult());

        var sut = new CliOutputProtokollWriter(Guid.NewGuid(), scopeFactoryMock.Object, loggerMock.Object);
        var output = string.Concat(Enumerable.Range(0, CliOutputProtokollWriter.QueueCapacity + 100).Select(i => $"zeile {i}\n"));

        var writeTask = Task.Run(() => sut.OnOutputChunk(Encoding.UTF8.GetBytes(output)));

        var warned = await Task.WhenAny(warningLogged.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        warned.Should().Be(warningLogged.Task, "die bounded Queue muss bei Rueckstau Backpressure sichtbar machen");
        writeTask.IsCompleted.Should().BeFalse("die Queue darf bei blockierter Persistenz nicht unbegrenzt alle Zeilen aufnehmen");

        persistenzBlockiert.Set();

        await writeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.CompleteAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>CompleteAsync schliesst den Channel nicht, solange ein Chunk noch Zeilen in die volle Queue schreibt.</summary>
    [Fact]
    public async Task CompleteAsync_BackpressureWaerendAktivemChunk_VerliertKeineDekodiertenZeilen()
    {
        var saveBlocker = new BlockingSaveChangesInterceptor();
        await using var provider = CreateCliOutputServiceProvider(saveBlocker);
        var aufgabeId = Guid.NewGuid();
        var queueFullLogged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loggerMock = new Mock<ILogger<CliOutputProtokollWriter>>();
        loggerMock
            .Setup(l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("ist voll", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => queueFullLogged.TrySetResult());

        var sut = new CliOutputProtokollWriter(
            aufgabeId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object);
        var expectedLines = Enumerable.Range(0, CliOutputProtokollWriter.QueueCapacity + 20)
            .Select(i => $"zeile {i}")
            .ToArray();
        var output = string.Concat(expectedLines.Select(line => line + "\n"));

        var writeTask = Task.Run(() => sut.OnOutputChunk(Encoding.UTF8.GetBytes(output)));

        await saveBlocker.FirstSaveStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var queueFull = await Task.WhenAny(queueFullLogged.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        queueFull.Should().Be(queueFullLogged.Task, "der Test muss einen vollen Channel und einen blockierten Producer erreichen");
        writeTask.IsCompleted.Should().BeFalse("OnOutputChunk muss noch Zeilen aus dem bereits dekodierten Chunk queuen");

        var cleanupTask = Task.Run(() => sut.CompleteAsync(TimeSpan.FromMilliseconds(100)));
        await cleanupTask.WaitAsync(TimeSpan.FromSeconds(5));
        writeTask.IsCompleted.Should().BeFalse("der Timeout-Cleanup darf den Channel nicht schliessen, waehrend der Producer noch schreibt");

        saveBlocker.Release();

        await writeTask.WaitAsync(TimeSpan.FromSeconds(10));
        await sut.CompleteAsync(TimeSpan.FromSeconds(10));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var persistedLines = db.Protokolleintraege
            .AsNoTracking()
            .Where(e => e.AufgabeId == aufgabeId && e.Typ == ProtokollTyp.CliOutput)
            .OrderBy(e => e.Zeitstempel)
            .Select(e => e.Inhalt)
            .ToList();

        persistedLines.Should().Equal(expectedLines);
    }

    private static ServiceProvider CreateCliOutputServiceProvider(params IInterceptor[] interceptors)
    {
        var databaseName = Guid.NewGuid().ToString();
        return new ServiceCollection()
            .AddDbContext<SoftwareschmiededDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
                if (interceptors.Length > 0)
                    options.AddInterceptors(interceptors);
            })
            .AddScoped<ProtokollService>()
            .AddSingleton<ILogger<ProtokollService>>(NullLogger<ProtokollService>.Instance)
            .BuildServiceProvider();
    }

    private sealed class BlockingSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _firstSaveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new(false);
        private int _blockFirstSave = 1;

        public Task FirstSaveStarted => _firstSaveStarted.Task;

        public void Release() => _release.Set();

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            BlockFirstSave();
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            BlockFirstSave(cancellationToken);
            return new ValueTask<InterceptionResult<int>>(result);
        }

        private void BlockFirstSave(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _blockFirstSave, 0) != 1)
                return;

            _firstSaveStarted.SetResult();
            _release.Wait(cancellationToken);
        }
    }
}
