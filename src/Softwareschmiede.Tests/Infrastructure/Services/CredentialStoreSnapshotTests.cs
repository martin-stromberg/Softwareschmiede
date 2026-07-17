using FluentAssertions;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für <see cref="CredentialStoreSnapshot"/> gegen einen In-Memory-<see cref="ICredentialStore"/>.</summary>
public sealed class CredentialStoreSnapshotTests
{
    /// <summary>Ein vor dem Snapshot vorhandener Wert wird nach Überschreiben durch Restore() auf den Originalwert zurückgesetzt.</summary>
    [Fact]
    public void Restore_StelltVorhandenenWertWiederHer()
    {
        var credentialStore = new InMemoryCredentialStoreForSnapshotTests();
        credentialStore.SetCredential("Softwareschmiede.Codex.CommandLineParameters", "--produktiv-flag");

        var snapshot = new CredentialStoreSnapshot(credentialStore, ["Softwareschmiede.Codex.CommandLineParameters"]);

        credentialStore.SetCredential("Softwareschmiede.Codex.CommandLineParameters", "--test-flag");
        snapshot.Restore();

        credentialStore.GetCredential("Softwareschmiede.Codex.CommandLineParameters").Should().Be("--produktiv-flag");
    }

    /// <summary>Ein beim Snapshot nicht vorhandener Schlüssel, der während des Tests gesetzt wurde, wird durch Restore() wieder entfernt.</summary>
    [Fact]
    public void Restore_LoeschtUrspruenglichFehlendenWert()
    {
        var credentialStore = new InMemoryCredentialStoreForSnapshotTests();

        var snapshot = new CredentialStoreSnapshot(credentialStore, ["LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory"]);

        credentialStore.SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        snapshot.Restore();

        credentialStore.GetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory").Should().BeNull();
    }
}

/// <summary>In-Memory-<see cref="ICredentialStore"/>-Fake für <see cref="CredentialStoreSnapshotTests"/> ohne Zugriff auf den realen OS-Store.</summary>
internal sealed class InMemoryCredentialStoreForSnapshotTests : ICredentialStore
{
    private readonly Dictionary<string, string> _store = new();

    /// <inheritdoc/>
    public string? GetCredential(string target) => _store.TryGetValue(target, out var value) ? value : null;

    /// <inheritdoc/>
    public void SetCredential(string target, string value) => _store[target] = value;

    /// <inheritdoc/>
    public void DeleteCredential(string target) => _store.Remove(target);
}
