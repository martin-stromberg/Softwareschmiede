namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistierte Promptvorlage fuer wiederkehrende CLI-Eingaben.</summary>
public sealed class PromptTemplate
{
    /// <summary>Eindeutige ID der Vorlage.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Anzeigename der Vorlage.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Prompttext, der nach Platzhalteraufloesung an die CLI gesendet wird.</summary>
    public string PromptText { get; set; } = string.Empty;

    /// <summary>Sortierposition fuer die Anzeige.</summary>
    public int SortOrder { get; set; }

    /// <summary>Erstellungszeitpunkt.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Letzter Aktualisierungszeitpunkt.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
