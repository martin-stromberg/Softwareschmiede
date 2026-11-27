using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Endpoints;
using Softwareschmiede.StatusServer.GitHub;
using Softwareschmiede.StatusServer.Options;
using Softwareschmiede.StatusServer.Pages;
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

var databasePath = builder.Configuration["Data:DatabasePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "statusserver.db");
var databaseDirectory = Path.GetDirectoryName(databasePath);
if (!string.IsNullOrEmpty(databaseDirectory))
    Directory.CreateDirectory(databaseDirectory);
builder.Services.AddDbContext<StatusServerDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = PasswordPolicy.MinLength;
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromHours(1);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<StatusServerDbContext>();

builder.Services.AddAntiforgery();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(StatusRoles.PageAccessPolicy,
        policy => policy.RequireRole(StatusRoles.Administration, StatusRoles.Ueberwacher));
    options.AddPolicy(StatusRoles.AdminOnlyPolicy,
        policy => policy.RequireRole(StatusRoles.Administration));
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(ServerSettingsService.DefaultSessionDurationMinutes);
    // Erneuerung erst nach halber Ticket-Laufzeit; die eigentliche Erneuerungs-
    // entscheidung trifft StatusServerCookieEvents.ValidatePrincipal mit der
    // admin-konfigurierbaren Dauer.
    options.SlidingExpiration = true;
    options.EventsType = typeof(StatusServerCookieEvents);
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Reverse-Proxy-Betrieb: Scheme/Host des eingehenden Requests übernehmen
    // (Trusted-Proxy-Liste für Self-Hosting geleert).
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddScoped<RelayTokenValidator>();
builder.Services.AddScoped<RelayTokenService>();
builder.Services.AddSingleton<RelayTokenPlaintextStore>();
builder.Services.AddScoped<ServerSettingsService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<StatusServerCookieEvents>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await StatusServerDatabaseInitializer.InitializeAsync(scope.ServiceProvider).ConfigureAwait(false);

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHub<StatusRelayHub>("/relay");
app.MapAuthEndpoints();
app.MapAdminEndpoints();

app.MapGet("/", async (
    HttpContext httpContext,
    InstanceSnapshotCache cache,
    GitHubStatusService gitHub,
    StatusPageComposer composer,
    IOptions<StatusServerOptions> options,
    UserManager<ApplicationUser> userManager,
    IAntiforgery antiforgery,
    CancellationToken cancellationToken) =>
{
    var repositories = await gitHub.GetRepositoryStatusesAsync(cancellationToken).ConfigureAwait(false);
    var user = await EndpointSecurityExtensions
        .BuildUserContextAsync(httpContext, userManager, antiforgery)
        .ConfigureAwait(false);
    var html = composer.Render(cache.GetAll(), repositories, options.Value.Page.RefreshSeconds, user);
    return Results.Content(html, PageShell.HtmlContentType);
}).RequireAuthorization(StatusRoles.PageAccessPolicy);

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
}).RequireAuthorization(StatusRoles.PageAccessPolicy);

app.Run();

/// <summary>Programmeinstieg des Status-Servers (partial für <c>WebApplicationFactory</c>-Tests).</summary>
public partial class Program;
