using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>Integrationstests für die CommandLineParameters-Übergabe vom CredentialStore an ProcessStartInfo.</summary>
public sealed class CliKiPluginCommandLineParametersIntegrationTests
{
    private readonly InMemoryCredentialStore _credentialStore = new();

    /// <summary>CodexPlugin übergibt CommandLineParameters aus dem Store an ProcessStartInfo.</summary>
    [Fact]
    public async Task CodexPlugin_ShouldAppendCommandLineParameters_FromCredentialStore()
    {
        _credentialStore.SetCredential("Softwareschmiede.Codex.CommandLineParameters", "--verbose --model gpt-4");
        var sut = new CodexPlugin(_credentialStore, NullLogger<CodexPlugin>.Instance);

        var psi = await sut.StartCliAsync("/repo/path");

        psi.Arguments.Should().Contain("--verbose --model gpt-4");
    }

    /// <summary>CodexPlugin kombiniert Session-Parameter und CommandLineParameters korrekt.</summary>
    [Fact]
    public async Task CodexPlugin_ShouldCombineSessionParametersAndCommandLineParameters()
    {
        _credentialStore.SetCredential("Softwareschmiede.Codex.CommandLineParameters", "--extra");
        var sut = new CodexPlugin(_credentialStore, NullLogger<CodexPlugin>.Instance);

        var psi = await sut.StartCliAsync("/repo/path", "--session abc");

        psi.Arguments.Should().Be("--session abc --extra");
    }

    /// <summary>ClaudeCliPlugin übergibt CommandLineParameters aus dem Store an ProcessStartInfo.</summary>
    [Fact]
    public async Task ClaudeCliPlugin_ShouldAppendCommandLineParameters_FromCredentialStore()
    {
        _credentialStore.SetCredential("Softwareschmiede.ClaudeCli.CommandLineParameters", "--dangerously-skip-permissions");
        var sut = new ClaudeCliPlugin(_credentialStore, NullLogger<ClaudeCliPlugin>.Instance);

        var psi = await sut.StartCliAsync("/repo/path");

        psi.Arguments.Should().Contain("--dangerously-skip-permissions");
    }

    /// <summary>GitHubCopilotPlugin übergibt CommandLineParameters aus dem Store an ProcessStartInfo.</summary>
    [Fact]
    public async Task GitHubCopilotPlugin_ShouldAppendCommandLineParameters_FromCredentialStore()
    {
        _credentialStore.SetCredential("Softwareschmiede.GitHubCopilot.CommandLineParameters", "--model gpt-4o");
        var sut = new GitHubCopilotPlugin(_credentialStore, NullLogger<GitHubCopilotPlugin>.Instance);

        var psi = await sut.StartCliAsync("/repo/path");

        psi.Arguments.Should().Contain("--model gpt-4o");
    }

    /// <summary>Leere CommandLineParameters ändern die Arguments nicht.</summary>
    [Fact]
    public async Task CodexPlugin_ShouldNotModifyArguments_WhenCommandLineParametersAreEmpty()
    {
        _credentialStore.SetCredential("Softwareschmiede.Codex.CommandLineParameters", "   ");
        var sut = new CodexPlugin(_credentialStore, NullLogger<CodexPlugin>.Instance);

        var psi = await sut.StartCliAsync("/repo/path", "--initial");

        psi.Arguments.Should().Be("--initial");
    }
}
