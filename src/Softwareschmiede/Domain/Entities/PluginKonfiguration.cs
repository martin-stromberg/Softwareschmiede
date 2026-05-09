using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Konfiguration eines registrierten Plugins (Git oder KI).</summary>
public sealed class PluginKonfiguration
{
    /// <summary>Eindeutige ID der Plugin-Konfiguration.</summary>
    public Guid Id { get; set; }

    /// <summary>Typ des Plugins, z.B. "GitHub" oder "GitHubCopilot".</summary>
    public string PluginTyp { get; set; } = string.Empty;

    /// <summary>Kategorie des Plugins (Git oder KI).</summary>
    public PluginKategorie PluginKategorie { get; set; }

    /// <summary>Anzeigename des Plugins.</summary>
    public string AnzeigeName { get; set; } = string.Empty;

    /// <summary>Schlüssel im Credential Store für den API-Token/Passwort.</summary>
    public string CredentialStoreKey { get; set; } = string.Empty;

    /// <summary>Optionale Basis-URL (z.B. für Self-Hosted Instanzen).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gibt an, ob das Plugin aktiviert ist.</summary>
    public bool Aktiviert { get; set; } = true;
}
