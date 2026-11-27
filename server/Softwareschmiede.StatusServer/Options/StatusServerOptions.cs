namespace Softwareschmiede.StatusServer.Options;

/// <summary>Gesamtkonfiguration des Status-Servers (Sektionen <c>App</c>, <c>GitHub</c>, <c>Page</c>).</summary>
public sealed class StatusServerOptions
{
    /// <summary>Optionaler URL-Unterpfad-Praefix fuer den Reverse-Proxy-Betrieb
    /// (z.B. <c>/softwareschmiede</c>), top-level Konfigurationsschluessel <c>PathBase</c>.
    /// Default leer = Betrieb auf dem Domain-Root (unveraendertes Verhalten).</summary>
    public string? PathBase { get; set; }

    /// <summary>Veraltungsschwellen der App-Sektionen (Sektion <c>App</c>).</summary>
    public AppSectionOptions App { get; set; } = new();

    /// <summary>Serverseitige GitHub-Aggregation (Sektion <c>GitHub</c>).</summary>
    public GitHubOptions GitHub { get; set; } = new();

    /// <summary>Aktualisierung der Status-Seite (Sektion <c>Page</c>).</summary>
    public PageOptions Page { get; set; } = new();
}

/// <summary>Veraltungsschwelle der App-Sektionen (Sektion <c>App</c>).</summary>
public sealed class AppSectionOptions
{
    /// <summary>Zeit in Sekunden, nach der ein fehlender Snapshot bei bestehender Verbindung
    /// als veraltet markiert wird (Standard: 300).</summary>
    public int StaleAfterSeconds { get; set; } = 300;
}

/// <summary>Serverseitige GitHub-Aggregation (Sektion <c>GitHub</c>).</summary>
public sealed class GitHubOptions
{
    /// <summary>Zu aggregierende Repositories im Format <c>owner/repo</c>.</summary>
    public List<string> Repositories { get; set; } = [];

    /// <summary>Optionales Read-Token für die GitHub-API (erhöht das Rate-Limit).</summary>
    public string? Token { get; set; }

    /// <summary>Mindestabstand zwischen zwei Abrufen desselben Endpunkts in Sekunden (Standard: 60).</summary>
    public int CacheTtlSeconds { get; set; } = 60;
}

/// <summary>Aktualisierung der Status-Seite (Sektion <c>Page</c>).</summary>
public sealed class PageOptions
{
    /// <summary>Meta-Refresh-Intervall der Status-Seite in Sekunden (Standard: 15).</summary>
    public int RefreshSeconds { get; set; } = 15;
}
