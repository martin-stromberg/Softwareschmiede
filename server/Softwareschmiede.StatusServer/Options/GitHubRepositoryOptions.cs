namespace Softwareschmiede.StatusServer.Options;

/// <summary>Ein zu aggregierendes GitHub-Repository im Format <c>owner/name</c>.</summary>
/// <param name="Owner">Der Besitzer (Organisation oder Benutzer) des Repositories.</param>
/// <param name="Name">Der Repository-Name.</param>
public sealed record GitHubRepositoryOptions(string Owner, string Name)
{
    /// <summary>Anzeigename im Format <c>owner/name</c>.</summary>
    public string DisplayName => $"{Owner}/{Name}";

    /// <summary>Parst einen <c>owner/name</c>-Eintrag der Konfiguration.</summary>
    /// <param name="value">Der zu parsende Wert.</param>
    /// <param name="repository">Das geparste Repository oder <see langword="null"/> bei ungültigem Format.</param>
    /// <returns><see langword="true"/>, wenn der Wert das Format <c>owner/name</c> besitzt.</returns>
    public static bool TryParse(string? value, out GitHubRepositoryOptions? repository)
    {
        repository = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        repository = new GitHubRepositoryOptions(parts[0], parts[1]);
        return true;
    }
}
