using System.Text.Json;
using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Scoped, preserving workspace trust preparation for supported CLIs.</summary>
public sealed class ScmCliWorkspaceTrustTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scm-trust-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates an isolated configuration workspace.</summary>
    public ScmCliWorkspaceTrustTests() => Directory.CreateDirectory(_root);

    /// <summary>The Claude project entry gains trust without touching unrelated user fields.</summary>
    [Fact]
    public void EnsureClaudeTrusted_WritesScopedKey_AndPreservesOtherFields()
    {
        var config = Path.Combine(_root, ".claude.json");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir")).FullName;
        File.WriteAllText(config, "{\"numProjects\":7,\"projects\":{\"other\":{\"hasTrustDialogAccepted\":false}}}");
        ScmCliWorkspaceTrust.EnsureClaudeTrusted(config, directory);
        using var document = JsonDocument.Parse(File.ReadAllText(config));
        document.RootElement.GetProperty("numProjects").GetInt32().Should().Be(7);
        document.RootElement.GetProperty("projects").GetProperty("other").GetProperty("hasTrustDialogAccepted").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("projects").GetProperty(directory).GetProperty("hasTrustDialogAccepted").GetBoolean().Should().BeTrue();
    }

    /// <summary>An explicit stored decision — including a refusal — is never overridden.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnsureClaudeTrusted_RespectsExistingDecision(bool accepted)
    {
        var config = Path.Combine(_root, ".claude.json");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir")).FullName;
        File.WriteAllText(config, "{\"projects\":{\"" + directory.Replace("\\", "\\\\") + "\":{\"hasTrustDialogAccepted\":" + (accepted ? "true" : "false") + ",\"otherField\":1}}}");
        ScmCliWorkspaceTrust.EnsureClaudeTrusted(config, directory);
        using var document = JsonDocument.Parse(File.ReadAllText(config));
        var project = document.RootElement.GetProperty("projects").GetProperty(directory);
        project.GetProperty("hasTrustDialogAccepted").GetBoolean().Should().Be(accepted);
        project.GetProperty("otherField").GetInt32().Should().Be(1);
    }

    /// <summary>A directory with its own .claude configuration keeps the manual dialog.</summary>
    [Fact]
    public void EnsureClaudeTrusted_SkipsDirectoriesWithOwnClaudeConfiguration()
    {
        var config = Path.Combine(_root, ".claude.json");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir", ".claude")).Parent!.FullName;
        ScmCliWorkspaceTrust.EnsureClaudeTrusted(config, directory);
        File.Exists(config).Should().BeFalse();
    }

    /// <summary>An unreadable user configuration is left byte-for-byte intact.</summary>
    [Fact]
    public void EnsureClaudeTrusted_LeavesInvalidJsonUntouched()
    {
        var config = Path.Combine(_root, ".claude.json");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir")).FullName;
        File.WriteAllText(config, "{ not json");
        ScmCliWorkspaceTrust.EnsureClaudeTrusted(config, directory);
        File.ReadAllText(config).Should().Be("{ not json");
    }

    /// <summary>The Codex projects entry is appended once and existing files gain it without duplication.</summary>
    [Fact]
    public void EnsureCodexTrusted_AppendsScopedEntry_Idempotently()
    {
        var config = Path.Combine(_root, ".codex", "config.toml");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir")).FullName;
        ScmCliWorkspaceTrust.EnsureCodexTrusted(config, directory);
        var escaped = directory.Replace("\\", "\\\\");
        var content = File.ReadAllText(config);
        content.Should().Contain($"[projects.\"{escaped}\"]");
        content.Should().Contain("trust_level = \"trusted\"");
        ScmCliWorkspaceTrust.EnsureCodexTrusted(config, directory);
        File.ReadAllText(config).Should().Be(content);
    }

    /// <summary>A stored project entry — including a deliberate untrusted decision — is respected.</summary>
    [Theory]
    [InlineData("untrusted")]
    [InlineData("trusted")]
    public void EnsureCodexTrusted_RespectsExistingProjectEntry(string level)
    {
        var config = Path.Combine(_root, ".codex", "config.toml");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir")).FullName;
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        var escaped = directory.Replace("\\", "\\\\");
        var original = $"sandbox_mode = \"workspace-write\"\n\n[projects.\"{escaped}\"]\ntrust_level = \"{level}\"\n";
        File.WriteAllText(config, original);
        ScmCliWorkspaceTrust.EnsureCodexTrusted(config, directory);
        File.ReadAllText(config).Should().Be(original);
    }

    /// <summary>A directory with its own .codex configuration keeps the manual dialog.</summary>
    [Fact]
    public void EnsureCodexTrusted_SkipsDirectoriesWithOwnCodexConfiguration()
    {
        var config = Path.Combine(_root, ".codex", "config.toml");
        var directory = Directory.CreateDirectory(Path.Combine(_root, "task-workdir", ".codex")).Parent!.FullName;
        ScmCliWorkspaceTrust.EnsureCodexTrusted(config, directory);
        File.Exists(config).Should().BeFalse();
    }

    /// <summary>Removes only the isolated fixture workspace.</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
