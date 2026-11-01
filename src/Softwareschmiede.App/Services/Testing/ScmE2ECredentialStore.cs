using System.Collections.Concurrent;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>Prozessgebundener In-Memory-Credential-Store des SCM-E2E-Modus: PluginSettingsService
/// und Fixture-Plugin teilen ihn, ohne den echten WindowsCredentialStore der Testmaschine zu
/// lesen oder zu beschreiben. Einträge überleben keinen Prozessneustart.</summary>
internal sealed class ScmE2ECredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _credentials = new(StringComparer.Ordinal);

    public string? GetCredential(string target)
        => _credentials.TryGetValue(target, out var value) ? value : null;

    public void SetCredential(string target, string value) => _credentials[target] = value;

    public void DeleteCredential(string target) => _credentials.TryRemove(target, out _);
}
