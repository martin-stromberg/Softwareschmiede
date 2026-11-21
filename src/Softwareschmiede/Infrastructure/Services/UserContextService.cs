using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Einfacher Benutzerkontext basierend auf dem lokalen Benutzerkonto.</summary>
public sealed class UserContextService : IUserContextService
{
    /// <inheritdoc/>
    public string GetUserId()
    {
        var user = Environment.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(user))
        {
            return "default-user";
        }

        return user;
    }
}
