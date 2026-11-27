using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Softwareschmiede.StatusServer.Tests;

/// <summary><see cref="WebApplicationFactory{TEntryPoint}"/> für den Status-Server mit
/// isolierter SQLite-Datenbank je Factory-Instanz (Temp-Pfad, wird beim Dispose aufgeräumt).</summary>
public class StatusWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _ownedDirectory =
        Path.Combine(Path.GetTempPath(), $"statusserver-tests-{Guid.NewGuid():N}");

    /// <summary>Erstellt eine Factory mit eigener, isolierter Datenbank-Datei.</summary>
    public StatusWebApplicationFactory()
    {
    }

    /// <summary>Optionaler, fester SQLite-Pfad (für Neustart-Persistenz-Tests, die
    /// denselben Pfad mit zwei Factory-Instanzen nutzen — Aufräumen obliegt dann
    /// dem Test, die Factory löscht nur ihr eigenes Temp-Verzeichnis).</summary>
    public string? DatabasePathOverride { get; init; }

    /// <summary>Der SQLite-Pfad, den diese Factory-Instanz dem Server per
    /// <c>Data:DatabasePath</c> vorgibt.</summary>
    public string DatabasePath
        => DatabasePathOverride ?? Path.Combine(_ownedDirectory, "statusserver.db");

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("Data:DatabasePath", DatabasePath);

        // Update-Subsystem (msTools.Updater) in Tests hermetisch halten: keine echten
        // GitHub-Checks durch die Bibliotheks-Hintergrunddienste und kein Workspace
        // im produktiven Anwendungspfad.
        builder.UseSetting("Update:Enabled", "false");
        builder.UseSetting("Update:HostedServicesEnabled", "false");
        builder.UseSetting("Update:WorkingDirectory", Path.Combine(_ownedDirectory, "updates"));
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Gepoolte SQLite-Verbindungen physisch schließen, damit das Temp-Verzeichnis lösbar ist.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            // Auch bei gesetztem DatabasePathOverride aufräumen: Die Override-DB
            // liegt dann zwar außerhalb und obliegt dem Test, das eigene
            // Temp-Verzeichnis (Update-Workspace unter Update:WorkingDirectory)
            // gehört weiterhin der Factory.
            Directory.Delete(_ownedDirectory, recursive: true);
        }
        catch (IOException)
        {
            // SQLite-Handles können kurz nach dem Dispose noch offen sein — Temp-Rest
            // ist unkritisch und wird vom OS aufgeräumt.
        }
    }
}
