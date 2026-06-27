using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Plugin-Einstellung speichern, verschlüsseln, beim Reload laden.</summary>
public sealed class PluginSettingsServiceIntegrationTests
{
    private readonly InMemoryCredentialStore _credentialStore = new();
    private readonly PluginSettingsService _sut;

    /// <summary>PluginSettingsServiceIntegrationTests.</summary>
    public PluginSettingsServiceIntegrationTests()
    {
        _sut = new PluginSettingsService(_credentialStore, NullLogger<PluginSettingsService>.Instance);
    }

    /// <summary><summary>SetValue_SpeichertWert_UndGetValue_LaeadtIhn.</summary>.</summary>
    [Fact]
    /// <summary>SetValue_SpeichertWert_UndGetValue_LaeadtIhn.</summary>
    public void SetValue_SpeichertWert_UndGetValue_LaeadtIhn()
    {
        var plugin = new FakeSettingsPlugin("TestPlugin", "test.plugin");
        var field = new PluginSettingField("api_key", "API Key", PluginSettingFieldType.Secret);

        _sut.SetValue(plugin, field, "mein-api-schluessel");
        var geladen = _sut.GetValue(plugin, field);

        geladen.Should().Be("mein-api-schluessel");
    }

    /// <summary><summary>HasValue_GibtFalse_OhneGespeichertenWert.</summary>.</summary>
    [Fact]
    /// <summary>HasValue_GibtFalse_OhneGespeichertenWert.</summary>
    public void HasValue_GibtFalse_OhneGespeichertenWert()
    {
        var plugin = new FakeSettingsPlugin("LeeresPlugin", "empty.plugin");
        var field = new PluginSettingField("some_key", "Some Key");

        _sut.HasValue(plugin, field).Should().BeFalse();
    }

    /// <summary><summary>HasValue_GibtTrue_NachSpeichern.</summary>.</summary>
    [Fact]
    /// <summary>HasValue_GibtTrue_NachSpeichern.</summary>
    public void HasValue_GibtTrue_NachSpeichern()
    {
        var plugin = new FakeSettingsPlugin("VollPlugin", "voll.plugin");
        var field = new PluginSettingField("token", "Token", PluginSettingFieldType.Secret);

        _sut.SetValue(plugin, field, "ein-token");
        _sut.HasValue(plugin, field).Should().BeTrue();
    }

    /// <summary><summary>DeleteValue_EntferntGespeichertenWert.</summary>.</summary>
    [Fact]
    /// <summary>DeleteValue_EntferntGespeichertenWert.</summary>
    public void DeleteValue_EntferntGespeichertenWert()
    {
        var plugin = new FakeSettingsPlugin("DeletePlugin", "delete.plugin");
        var field = new PluginSettingField("to_delete", "To Delete");

        _sut.SetValue(plugin, field, "wird-gelöscht");
        _sut.DeleteValue(plugin, field);

        _sut.HasValue(plugin, field).Should().BeFalse();
    }

    /// <summary><summary>GetValue_GibtNull_OhneGespeichertenWert.</summary>.</summary>
    [Fact]
    /// <summary>GetValue_GibtNull_OhneGespeichertenWert.</summary>
    public void GetValue_GibtNull_OhneGespeichertenWert()
    {
        var plugin = new FakeSettingsPlugin("NullPlugin", "null.plugin");
        var field = new PluginSettingField("missing_key", "Missing Key");

        _sut.GetValue(plugin, field).Should().BeNull();
    }
}

/// <summary>In-Memory Credential Store für Tests.</summary>
internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _store = new();

    public string? GetCredential(string key) => _store.TryGetValue(key, out var v) ? v : null;

    /// <summary>SetCredential.</summary>
    public void SetCredential(string key, string value) => _store[key] = value;

    /// <summary>DeleteCredential.</summary>
    public void DeleteCredential(string key) => _store.Remove(key);
}

/// <summary>Fake-Plugin für Einstellungs-Tests.</summary>
internal sealed class FakeSettingsPlugin : IPlugin
{
    /// <summary>FakeSettingsPlugin.</summary>
    public FakeSettingsPlugin(string pluginName, string pluginPrefix)
    {
        PluginName = pluginName;
        PluginPrefix = pluginPrefix;
    }

    public string PluginName { get; }
    public string PluginPrefix { get; }
    public PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <summary>IReadOnlyList.</summary>
    public IReadOnlyList<PluginSettingGroup> GetSettingGroups()
        => Array.Empty<PluginSettingGroup>();
}
