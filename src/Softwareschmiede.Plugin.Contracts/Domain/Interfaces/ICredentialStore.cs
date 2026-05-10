namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Sicherer Credential-Speicher.</summary>
public interface ICredentialStore
{
    /// <summary>Gibt den gespeicherten Wert zurück oder null wenn nicht vorhanden.</summary>
    /// <param name="target">Schlüssel/Zielname des Credentials.</param>
    string? GetCredential(string target);

    /// <summary>Speichert einen Credential-Wert.</summary>
    /// <param name="target">Schlüssel/Zielname des Credentials.</param>
    /// <param name="value">Zu speichernder Wert (z.B. API-Token).</param>
    void SetCredential(string target, string value);

    /// <summary>Löscht einen Credential-Eintrag.</summary>
    /// <param name="target">Schlüssel/Zielname des zu löschenden Credentials.</param>
    void DeleteCredential(string target);
}
