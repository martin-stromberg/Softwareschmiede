using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>
/// Sichert im Konstruktor die aktuellen Werte einer vorgegebenen Menge von Credential-Schlüsseln
/// über einem <see cref="ICredentialStore"/> und stellt sie via <see cref="Restore"/> exakt wieder her.
/// </summary>
public sealed class CredentialStoreSnapshot
{
    private readonly ICredentialStore _credentialStore;
    private readonly Dictionary<string, string?> _originalValues;

    /// <summary>Erzeugt den Snapshot und ruft für jeden Schlüssel den aktuellen Wert ab.</summary>
    /// <param name="credentialStore">Der zu sichernde Credential Store.</param>
    /// <param name="keys">Die zu sichernden Schlüssel/Zielnamen.</param>
    public CredentialStoreSnapshot(ICredentialStore credentialStore, IEnumerable<string> keys)
    {
        _credentialStore = credentialStore;
        _originalValues = keys.ToDictionary(key => key, credentialStore.GetCredential);
    }

    /// <summary>
    /// Stellt für jeden gemerkten Schlüssel den ursprünglichen Wert wieder her: schreibt ihn zurück,
    /// falls er beim Snapshot vorhanden war, oder löscht ihn, falls er beim Snapshot nicht existierte.
    /// </summary>
    public void Restore()
    {
        foreach (var (key, originalValue) in _originalValues)
        {
            if (originalValue is null)
                _credentialStore.DeleteCredential(key);
            else
                _credentialStore.SetCredential(key, originalValue);
        }
    }
}
