namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Interface für den CLI-Session-Service.</summary>
public interface ICliSessionService
{
    /// <summary>Gibt an, ob der CLI-Prozess läuft.</summary>
    bool IsRunning { get; }

    /// <summary>Startet den CLI-Prozess.</summary>
    Task StartAsync(string cliName, string workingDir, Func<string, Task> onOutput);

    /// <summary>Sendet Eingabe an den CLI-Prozess.</summary>
    Task SendAsync(string input);
}
