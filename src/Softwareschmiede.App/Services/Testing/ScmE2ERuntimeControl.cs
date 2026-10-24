using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.App.Services.Testing;

// Isolated E2E input only. Commands exercise real session services, never synthesize inbox state.
internal sealed class ScmE2ERuntimeControl(ScmE2ETestConfiguration configuration, IServiceScopeFactory scopes,
    KiAusfuehrungsService cli, IPluginManager plugins) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var path in Directory.EnumerateFiles(configuration.TestRoot, "runtime-*.command"))
            {
                var resultPath = path + ".result";
                if (File.Exists(resultPath)) continue;
                try
                {
                    var command = JsonSerializer.Deserialize<ScmE2ERuntimeCommand>(await File.ReadAllTextAsync(path, stoppingToken))!;
                    await File.WriteAllTextAsync(path + ".received", "received", stoppingToken);
                    Guid runId = default;
                    await ScmTaskCoordinator.Shared.RunAsync(command.TaskId, async () =>
                    {
                        if (command.Replace)
                        {
                            await File.WriteAllTextAsync(path + ".locked", "locked", stoppingToken);
                            while (!File.Exists(path + ".release")) await Task.Delay(25, stoppingToken);
                            await cli.StopCliAsync(command.TaskId, stoppingToken);
                            while (cli.GetScmPromptSession(command.TaskId)?.IsAlive == true) await Task.Delay(25, stoppingToken);
                        }
                        using var scope = scopes.CreateScope();
                        var task = await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Aufgaben
                            .AsNoTracking().SingleAsync(x => x.Id == command.TaskId, stoppingToken);
                        var plugin = plugins.GetDevelopmentAutomationPlugins().Single();
                        var handle = await cli.StartTerminalSessionAsync(task.Id, plugin, task.LokalerKlonPfad!, "--scm-exchange", stoppingToken);
                        runId = handle.CliRunId;
                    }, stoppingToken);
                    await PublishResultAsync(resultPath, new { runId }, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    await PublishResultAsync(resultPath, new { error = ex.ToString() }, stoppingToken);
                }
            }
            await Task.Delay(50, stoppingToken);
        }
    }

    private static async Task PublishResultAsync(string path, object result, CancellationToken ct)
    {
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(result), ct);
        File.Move(path + ".tmp", path);
    }
}

internal sealed record ScmE2ERuntimeCommand(Guid TaskId, bool Replace);
