namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit einer <see cref="IScmEventSource"/>, lokale Repository-Aliase in eine
/// kanonische Vergleichsform zu überführen (z. B. <c>https://{host}/{owner}/{name}</c>).</summary>
/// <remarks>Der Host nutzt die kanonische Form als Gruppierungs- und Checkpoint-Schlüssel, damit
/// Alias-Mischformen (HTTPS-/SSH-URL, <c>owner/name</c>, <c>.git</c>-Suffix) auf dieselbe
/// Auflösung zusammenlaufen. Quellen, die das Interface nicht implementieren, behalten die
/// bisherige Roh-Alias-Gruppierung. Die kanonische Form muss selbst ein parsebarer Alias sein
/// und wird als Auflösungseingabe (<see cref="Softwareschmiede.Domain.ValueObjects.ScmRepositoryContext.RepositoryId"/>)
/// wiederverwendet.</remarks>
public interface IScmRepositoryAliasNormalizer
{
    /// <summary>Liefert die kanonische Form eines lokalen Repository-Alias.</summary>
    /// <param name="alias">Lokaler Alias (z. B. Repository-URL oder <c>owner/name</c>).</param>
    /// <returns>Kanonische Form des Alias.</returns>
    /// <exception cref="Softwareschmiede.Domain.ValueObjects.ScmProviderException">
    /// <see cref="Softwareschmiede.Domain.Enums.ScmProviderFailureKind.Permanent"/> bei einem
    /// für diese Quelle nicht parsebaren Alias.</exception>
    string NormalizeRepositoryAlias(string alias);
}
