namespace Softwareschmiede.Application.Services;

/// <summary>Konfiguration für den Abruf und das Caching von Verzeichnisstrukturen externer Repositories.</summary>
public sealed class DirectoryStructureOptions
{
    /// <summary>Name des Konfigurationsabschnitts in appsettings.json.</summary>
    public const string SectionName = "DirectoryStructure";

    /// <summary>Caching-Dauer für abgerufene Verzeichnisstrukturen (in Sekunden).</summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>Maximale Verzeichnis-Tiefe beim Abruf der Repository-Struktur.</summary>
    public int MaxDepth { get; set; } = 2;

    /// <summary>Schalter zur Aktivierung/Deaktivierung der Verzeichnisstruktur-Voraus-Ladung.</summary>
    public bool Enabled { get; set; } = true;
}
