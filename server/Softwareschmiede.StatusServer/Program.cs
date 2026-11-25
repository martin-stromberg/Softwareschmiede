using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.GitHub;
using Softwareschmiede.StatusServer.Options;
using Softwareschmiede.StatusServer.Relay;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Status;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StatusServerOptions>(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<StatusServerOptions>>().Value;
    return new InstanceSnapshotCache(
        sp.GetRequiredService<TimeProvider>(),
        TimeSpan.FromSeconds(Math.Max(10, options.App.StaleAfterSeconds)));
});
builder.Services.AddSingleton<RelayTokenValidator>();
builder.Services.AddSingleton<StatusPageComposer>();
builder.Services.AddHttpClient<GitHubStatusService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<StatusServerOptions>>().Value;
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("softwareschmiede-status-server/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.Timeout = TimeSpan.FromSeconds(20);
    if (!string.IsNullOrEmpty(options.GitHub.Token))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.GitHub.Token);
});

var app = builder.Build();

app.UseMiddleware<PageAccessMiddleware>();

app.MapHub<StatusRelayHub>("/relay");

app.MapGet("/", async (
    InstanceSnapshotCache cache,
    GitHubStatusService gitHub,
    StatusPageComposer composer,
    IOptions<StatusServerOptions> options,
    CancellationToken cancellationToken) =>
{
    var repositories = await gitHub.GetRepositoryStatusesAsync(cancellationToken).ConfigureAwait(false);
    var html = composer.Render(cache.GetAll(), repositories, options.Value.Page.RefreshSeconds);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/status.json", async (
    InstanceSnapshotCache cache,
    GitHubStatusService gitHub,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var repositories = await gitHub.GetRepositoryStatusesAsync(cancellationToken).ConfigureAwait(false);
    return Results.Json(new
    {
        GeneratedUtc = timeProvider.GetUtcNow(),
        Instances = cache.GetAll(),
        GitHub = repositories,
    });
});

app.Run();

/// <summary>Programmeinstieg des Status-Servers (partial für <c>WebApplicationFactory</c>-Tests).</summary>
public partial class Program;
