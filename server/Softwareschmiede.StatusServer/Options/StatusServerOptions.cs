namespace Softwareschmiede.StatusServer.Options;

/// <summary>Gesamtkonfiguration des Status-Servers (Sektionen <c>Relay</c>, <c>App</c>, <c>GitHub</c>, <c>Page</c>).</summary>
public sealed class StatusServerOptions
{
    /// <summary>Konfiguration der Relay-Zugangstokens (Sektion <c>Relay</c>).</summary>
    public RelayOptions Relay { get; set; } = new();

    /// <summary>Veraltungsschwellen der App-Sektionen (Sektion <c>App</c>).</summary>
    public AppSectionOptions App { get; set; } = new();

    /// <summary>Serverseitige GitHub-Aggregation (Sektion <c>GitHub</c>).</summary>
    public GitHubOptions GitHub { get; set; } = new();

    /// <summary>Zugriffsschutz und Aktualisierung der Status-Seite (Sektion <c>Page</c>).</summary>
    public PageOptions Page { get; set; } = new();
}

/// <summary>Konfiguration der Relay-Zugangstokens (Sektion <c>Relay</c>).</summary>
public sealed class RelayOptions
{
    /// <summary>Zulässige Relay-Tokens; leer bedeutet: alle Verbindungen werden abgelehnt
    /// (außer im Development-Modus mit gesetztem <see cref="DevToken"/>).</summary>
    public List<string> Tokens { get; set; } = [];

    /// <summary>Entwicklungs-Token, das nur gilt, wenn die Umgebung <c>Development</c> ist.</summary>
    public string? DevToken { get; set; }
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

/// <summary>Zugriffsschutz und Aktualisierung der Status-Seite (Sektion <c>Page</c>).</summary>
public sealed class PageOptions
{
    /// <summary>Benutzername des Basic-Auth-Schutzes; leer = Schutz deaktiviert (lokaler Dev-Modus).</summary>
    public string? Username { get; set; }

    /// <summary>Passwort des Basic-Auth-Schutzes; leer = Schutz deaktiviert (lokaler Dev-Modus).</summary>
    public string? Password { get; set; }

    /// <summary>Meta-Refresh-Intervall der Status-Seite in Sekunden (Standard: 15).</summary>
    public int RefreshSeconds { get; set; } = 15;
}
