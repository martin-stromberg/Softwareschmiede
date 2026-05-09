using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            // DbContext
            builder.Services.AddDbContext<SoftwareschmiededDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "softwareschmiede.db")}"));

            // Infrastructure Services

            builder.Services.AddSingleton<ICliRunner, CliRunner>();
            builder.Services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
            builder.Services.AddScoped<IAgentPackageService, AgentPackageReader>();
            builder.Services.AddScoped<IAgentPackageFileService, AgentPackageFileService>();

            // Plugins
            builder.Services.AddScoped<IGitPlugin, GitHubPlugin>();
            builder.Services.AddScoped<IKiPlugin, GitHubCopilotPlugin>();

            // Application Services
            builder.Services.AddScoped<ProjektService>();
            builder.Services.AddScoped<AufgabeService>();
            builder.Services.AddScoped<ProtokollService>();
            builder.Services.AddScoped<EntwicklungsprozessService>();
            builder.Services.AddScoped<GitOrchestrationService>();
            builder.Services.AddScoped<PluginSettingsService>();
            builder.Services.AddSingleton<KiAusfuehrungsService>();

            var app = builder.Build();

            // Auto-Migration beim Start
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                await db.Database.MigrateAsync();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

            await app.RunAsync();
        }
    }
}
