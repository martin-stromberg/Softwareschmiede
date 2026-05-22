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
            builder.Services.AddControllers();

            // DbContext
            builder.Services.AddDbContext<SoftwareschmiededDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "softwareschmiede.db")}"));

            // Caching
            builder.Services.AddMemoryCache();

            // Infrastructure Services

            builder.Services.AddSingleton<ICliRunner, CliRunner>();
            builder.Services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
            builder.Services.AddScoped<IAgentPackageService, AgentPackageReader>();
            builder.Services.AddScoped<IAgentPackageFileService, AgentPackageFileService>();
            builder.Services.AddScoped<IArbeitsverzeichnisResolver, ArbeitsverzeichnisResolver>();

            // Plugins
            builder.Services.AddSingleton<IPluginManager, PluginManager>();
            builder.Services.AddScoped<IGitPlugin>(sp => sp.GetRequiredService<IPluginManager>().GetDefaultSourceCodeManagementPlugin());
            builder.Services.AddScoped<IKiPlugin>(sp => sp.GetRequiredService<IPluginManager>().GetDefaultDevelopmentAutomationPlugin());

            // Application Services
            builder.Services.AddScoped<ProjektService>();
            builder.Services.AddScoped<AufgabeService>();
            builder.Services.AddScoped<ProtokollService>();
            builder.Services.AddScoped<EntwicklungsprozessService>();
            builder.Services.AddScoped<GitOrchestrationService>();
            builder.Services.AddScoped<IGitWorkspaceBrowserService, GitWorkspaceBrowserService>();
            builder.Services.AddScoped<RepositoryStartskriptService>();
            builder.Services.AddScoped<PluginSettingsService>();
            builder.Services.AddScoped<PluginDefaultSettingsService>();
            builder.Services.AddScoped<PluginSelectionService>();
            builder.Services.AddScoped<ArbeitsverzeichnisSettingsService>();
            builder.Services.AddSingleton<KiAusfuehrungsService>();
            builder.Services.AddSingleton<IRunningAutomationStatusSource>(sp => sp.GetRequiredService<KiAusfuehrungsService>());
            builder.Services.AddSingleton<IAutoShutdownOrchestrator, AutoShutdownOrchestrator>();
            builder.Services.AddSingleton<ISystemShutdownService, SystemShutdownService>();

            // Diff Services
            builder.Services.AddTransient<DiffAlgorithmService>();
            builder.Services.AddTransient<DiffCachingService>();
            builder.Services.AddTransient<DiffService>();

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
            app.MapControllers(); // API Endpoints aktivieren
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

            await app.RunAsync();
        }
    }
}
