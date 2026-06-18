using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Einfacher Benutzerkontext basierend auf dem lokalen Benutzerkonto.</summary>
public sealed class BenutzerkontextService : IBenutzerkontextService
{
    /// <inheritdoc/>
    public string GetBenutzerId()
    {
        var benutzer = Environment.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(benutzer))
        {
            return "default-user";
        }

        return benutzer;
    }
}
