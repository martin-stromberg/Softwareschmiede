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

/// <summary>Tests fuer <see cref="CliOutputLogWriter"/>.</summary>
public sealed class CliOutputProtokollWriterTests
{
    /// <summary>CompleteAsync wartet auf die Persistenz bereits angenommener Zeilen.</summary>
    [Fact]
    public async Task CompleteAsync_DraintAngenommeneZeilen_BevorProviderDisposedWird()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var workItemId = Guid.NewGuid();
        var sut = new CliOutputLogWriter(
            workItemId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CliOutputLogWriter>.Instance);

        sut.OnOutputChunk(System.Text.Encoding.UTF8.GetBytes("erste\nzweite ohne ende"));
        await sut.CompleteAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        var entries = db.LogEntries
            .AsNoTracking()
            .Where(e => e.WorkItemId == workItemId && e.Type == LogEntryType.CliOutput)
            .OrderBy(e => e.Timestamp)
            .Select(e => e.Content)
            .ToList();

        entries.Should().Equal("erste", "zweite ohne ende");
    }

    /// <summary><see cref="CliOutputLogWriter"/> implementiert <see cref="Softwareschmiede.Infrastructure.Terminal.ITerminalDiagnoseSink"/>:
    /// Diagnose-Marker laufen denselben Accumulator-Pfad wie normale CLI-Ausgabe und landen als
    /// Protokollzeilen im Aufgabenprotokoll.</summary>
    [Fact]
    public async Task OnDiagnoseChunk_SchreibtMarkerAlsProtokollzeilen()
    {
        await using var provider = CreateCliOutputServiceProvider();
        var workItemId = Guid.NewGuid();
        var sut = new CliOutputLogWriter(
            workItemId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CliOutputLogWriter>.Instance);

        sut.Should().BeAssignableTo<Softwareschmiede.Infrastructure.Terminal.ITerminalDiagnoseSink>();
        sut.OnDiagnoseChunk(System.Text.Encoding.UTF8.GetBytes("[Terminal-Diagnose] Markerzeile\r\n"));
        await sut.CompleteAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext>();
        var entries = db.LogEntries
            .AsNoTracking()
            .Where(e => e.WorkItemId == workItemId && e.Type == LogEntryType.CliOutput)
            .Select(e => e.Content)
            .ToList();

        entries.Should().Equal("[Terminal-Diagnose] Markerzeile");
    }

    /// <summary>Persistenzfehler werden geloggt und schlagen nicht in den Terminal-Lesepfad zurueck.</summary>
    [Fact]
    public async Task CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new InvalidOperationException("Simulierter Scope-Fehler"));
        var loggerMock = new Mock<ILogger<CliOutputLogWriter>>();
        var sut = new CliOutputLogWriter(Guid.NewGuid(), scopeFactoryMock.Object, loggerMock.Object);

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

        var loggerMock = new Mock<ILogger<CliOutputLogWriter>>();
        loggerMock
            .Setup(l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => warningLogged.TrySetResult());

        var sut = new CliOutputLogWriter(Guid.NewGuid(), scopeFactoryMock.Object, loggerMock.Object);
        var output = string.Concat(Enumerable.Range(0, CliOutputLogWriter.QueueCapacity + 100).Select(i => $"zeile {i}\n"));

        var writeTask = Task.Run(() => sut.OnOutputChunk(Encoding.UTF8.GetBytes(output)), TestContext.Current.CancellationToken);

        var warned = await Task.WhenAny(warningLogged.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        warned.Should().Be(warningLogged.Task, "die bounded Queue muss bei Rueckstau Backpressure sichtbar machen");
        writeTask.IsCompleted.Should().BeFalse("die Queue darf bei blockierter Persistenz nicht unbegrenzt alle Zeilen aufnehmen");

        persistenzBlockiert.Set();

        await writeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>CompleteAsync schliesst den Channel nicht, solange ein Chunk noch Zeilen in die volle Queue schreibt.</summary>
    [Fact]
    public async Task CompleteAsync_BackpressureWaerendAktivemChunk_VerliertKeineDekodiertenZeilen()
    {
        var saveBlocker = new BlockingSaveChangesInterceptor();
        await using var provider = CreateCliOutputServiceProvider(saveBlocker);
        var workItemId = Guid.NewGuid();
        var queueFullLogged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loggerMock = new Mock<ILogger<CliOutputLogWriter>>();
        loggerMock
            .Setup(l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => IstQueueVollLogeintrag(state)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => queueFullLogged.TrySetResult());

        var sut = new CliOutputLogWriter(
            workItemId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object);
        var expectedLines = Enumerable.Range(0, CliOutputLogWriter.QueueCapacity + 20)
            .Select(i => $"zeile {i}")
            .ToArray();
        var output = string.Concat(expectedLines.Select(line => line + "\n"));

        var writeTask = Task.Run(() => sut.OnOutputChunk(Encoding.UTF8.GetBytes(output)), TestContext.Current.CancellationToken);

        await saveBlocker.FirstSaveStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var queueFull = await Task.WhenAny(queueFullLogged.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        queueFull.Should().Be(queueFullLogged.Task, "der Test muss einen vollen Channel und einen blockierten Producer erreichen");
        writeTask.IsCompleted.Should().BeFalse("OnOutputChunk muss noch Zeilen aus dem bereits dekodierten Chunk queuen");

        var cleanupTask = Task.Run(() => sut.CompleteAsync(TimeSpan.FromMilliseconds(100)), TestContext.Current.CancellationToken);
        await cleanupTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        writeTask.IsCompleted.Should().BeFalse("der Timeout-Cleanup darf den Channel nicht schliessen, waehrend der Producer noch schreibt");

        saveBlocker.Release();

        await writeTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var persistedLines = db.LogEntries
            .AsNoTracking()
            .Where(e => e.WorkItemId == workItemId && e.Type == LogEntryType.CliOutput)
            .OrderBy(e => e.Timestamp)
            .Select(e => e.Content)
            .ToList();

        persistedLines.Should().Equal(expectedLines);
    }

    /// <summary>
    /// Ein gültiger Rate-Limit-Marker in der CLI-Ausgabe persistiert das Plugin-Session-Limit
    /// unter <c>plugins.sessionlimit.&lt;Prefix&gt;</c> und pausiert die auslösende Aufgabe (Issue 151).
    /// </summary>
    [Fact]
    public async Task RateLimitMarker_PersistiertSessionLimit_UndPausiertAufgabe()
    {
        await using var provider = CreateCliOutputServiceProvider(mitLimitService: true);
        var workItemId = await SeedLaufendeAufgabeAsync(provider);
        var sut = new CliOutputLogWriter(
            workItemId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CliOutputLogWriter>.Instance);

        var resetUtc = DateTimeOffset.UtcNow.AddHours(2);
        sut.OnOutputChunk(Encoding.UTF8.GetBytes($"[[SOFTWARESCHMIEDE_RATE_LIMIT:{resetUtc:O}]]\n"));
        await sut.CompleteAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();

        var setting = db.AppSettings
            .AsNoTracking()
            .SingleOrDefault(e => e.Key == AiPluginLimitService.SessionLimitKeyPrefix + "Softwareschmiede.TestKi");
        setting.Should().NotBeNull("der Marker muss das Session-Limit des Plugin-Prefix persistieren");

        var workItem = db.WorkItems.AsNoTracking().Single(a => a.Id == workItemId);
        workItem.PausedUntilUtc.Should().NotBeNull("die Aufgabe muss bei zukünftigem Limit pausiert werden");

        db.LogEntries
            .AsNoTracking()
            .Any(e => e.WorkItemId == workItemId && e.Type == LogEntryType.RateLimit)
            .Should().BeTrue("der Marker erzeugt weiterhin den RateLimit-Protokolleintrag");
    }

    /// <summary>Ausgabe ohne Marker verändert weder Session-Limits noch den Pausenzustand der Aufgabe.</summary>
    [Fact]
    public async Task MarkerfreieZeile_LoestKeineLimitVerarbeitungAus()
    {
        await using var provider = CreateCliOutputServiceProvider(mitLimitService: true);
        var workItemId = await SeedLaufendeAufgabeAsync(provider);
        var sut = new CliOutputLogWriter(
            workItemId,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CliOutputLogWriter>.Instance);

        sut.OnOutputChunk(Encoding.UTF8.GetBytes("normale ausgabe\n"));
        await sut.CompleteAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();

        db.AppSettings.AsNoTracking().Any(e => e.Key.StartsWith(AiPluginLimitService.SessionLimitKeyPrefix))
            .Should().BeFalse("ohne Marker darf kein Session-Limit persistiert werden");
        db.WorkItems.AsNoTracking().Single(a => a.Id == workItemId).PausedUntilUtc.Should().BeNull();
    }

    private static bool IstQueueVollLogeintrag(object? state)
        => state?.ToString()?.Contains("ist voll", StringComparison.Ordinal) == true;

    private static async Task<Guid> SeedLaufendeAufgabeAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var projectId = Guid.NewGuid();
        db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = projectId,
            Name = "Writer-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        var workItem = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Laufende Aufgabe",
            Status = WorkItemStatus.Started,
            ExecutionStatus = WorkItemExecutionStatus.Active,
            ActiveRunId = Guid.NewGuid().ToString("N"),
            AiPluginPrefix = "Softwareschmiede.TestKi",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync();
        return workItem.Id;
    }

    private static ServiceProvider CreateCliOutputServiceProvider(params IInterceptor[] interceptors)
        => CreateCliOutputServiceProvider(mitLimitService: false, interceptors);

    private static ServiceProvider CreateCliOutputServiceProvider(bool mitLimitService, params IInterceptor[] interceptors)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddDbContext<SoftwareschmiedeDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
                if (interceptors.Length > 0)
                    options.AddInterceptors(interceptors);
            })
            .AddScoped<LogService>()
            .AddSingleton<ILogger<LogService>>(NullLogger<LogService>.Instance);

        if (mitLimitService)
        {
            services
                .AddScoped<AppSettingService>()
                .AddScoped<AiPluginLimitService>()
                .AddSingleton<WorkItemRunDataChangedNotifier>()
                .AddSingleton<ILogger<AppSettingService>>(NullLogger<AppSettingService>.Instance)
                .AddSingleton<ILogger<AiPluginLimitService>>(NullLogger<AiPluginLimitService>.Instance);
        }

        return services.BuildServiceProvider();
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
