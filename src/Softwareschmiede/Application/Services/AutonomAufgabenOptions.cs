namespace Softwareschmiede.Application.Services;

/// <summary>Konfiguration für das Feature "Autonome Aufgaben" (Projektleiter-Agent).</summary>
public sealed class AutonomAufgabenOptions
{
    /// <summary>Name des Konfigurationsabschnitts in appsettings.json.</summary>
    public const string SectionName = "AutonomAufgaben";

    /// <summary>Feature-Flag zum Aktivieren/Deaktivieren von Autonomen Aufgaben.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Standardbudget für neue Autonome Aufgaben.</summary>
    public int DefaultTokenBudget { get; set; } = 500000;

    /// <summary>Standard-Laufzeitlimit in Minuten (8 Stunden).</summary>
    public int DefaultRuntimeLimitMinutes { get; set; } = 480;

    /// <summary>Basis-Verzeichnis für Arbeitsverzeichnisse der Autonomen Aufgaben.</summary>
    public string WorkingDirectoryBase { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutonomAufgaben");

    /// <summary>Timeout in Sekunden für Heartbeat-Unterbrechungserkennung.</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 300;

    /// <summary>Maximale Anzahl gleichzeitig laufender Unteragenten pro Autonome Aufgabe.</summary>
    public int MaxConcurrentSubagents { get; set; } = 5;

    /// <summary>Standard für automatische Skill-Generierung.</summary>
    public bool SkillAutoGenerationEnabled { get; set; }
}
