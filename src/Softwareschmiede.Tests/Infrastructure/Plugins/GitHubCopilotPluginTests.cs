using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests für das GitHub Copilot Plugin.</summary>
public sealed class GitHubCopilotPluginTests
{
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly GitHubCopilotPlugin _sut;

    /// <summary>GitHubCopilotPluginTests.</summary>
    public GitHubCopilotPluginTests()
    {
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new GitHubCopilotPlugin(
            _credentialStoreMock.Object,
            new Mock<ILogger<GitHubCopilotPlugin>>().Object);
    }

    /// <summary>Exposes expected plugin metadata.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("GitHub Copilot");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.GitHubCopilot");
        _sut.ProviderDateiPraefix.Should().Be("copilot");
        _sut.GetSettingGroups().Should().HaveCount(3);
        _sut.GetSettingGroups().SelectMany(g => g.Fields).Should().Contain(f => f.Key == "Token");
        _sut.GetSettingGroups().Should().Contain(g => g.GroupName == "CLI-Konfiguration");
        _sut.GetSettingGroups().SelectMany(g => g.Fields).Should().Contain(f => f.Key == "CommandLineParameters");
    }

    /// <summary>SupportsSessionContinuation returns false.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnFalse()
    {
        _sut.SupportsSessionContinuation().Should().BeFalse();
    }

    /// <summary>StartCliAsync uses default copilot command when no path configured.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseCopilotCommand_WhenNoPathConfigured()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var psi = await _sut.StartCliAsync("/repo/path");

        psi.FileName.Should().Be("copilot");
        psi.WorkingDirectory.Should().Be("/repo/path");
    }

    /// <summary>StartCliAsync uses configured executable path when provided.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseConfiguredPath_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHubCopilot.ExecutablePath"))
            .Returns(@"C:\tools\copilot.exe");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\copilot.exe");
    }

    /// <summary>StartCliAsync passes optional parameters as arguments.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldPassParameters_AsArguments()
    {
        var psi = await _sut.StartCliAsync("/repo", "--continue");

        psi.Arguments.Should().Be("--continue");
    }

    /// <summary>StartCliAsync sets GH_TOKEN when credential is stored.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldSetGhTokenEnvironmentVariable_WhenTokenIsStored()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token"))
            .Returns("ghp_test-token");

        var psi = await _sut.StartCliAsync("/repo");

        psi.EnvironmentVariables["GH_TOKEN"].Should().Be("ghp_test-token");
    }

    /// <summary>StartCliAsync appends CommandLineParameters from credential store to arguments.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldIncludeCommandLineParameters_InProcessStartInfo()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHubCopilot.CommandLineParameters"))
            .Returns("--model gpt-4o");

        var psi = await _sut.StartCliAsync("/repo");

        psi.Arguments.Should().Contain("--model gpt-4o");
    }

    /// <summary>Copilot bietet die rueckgabefaehige Issue-Template-Ausfuellhilfe an.</summary>
    [Fact]
    public void Plugin_ShouldExposeIssueTemplateTextGeneratorCapability()
    {
        _sut.Should().BeAssignableTo<IIssueTemplateTextGenerator>();
    }

    /// <summary>Issue-Ausfuellhilfe uebergibt den Prompt als Argument an copilot -p.</summary>
    [Fact]
    public void BuildIssueTemplateFillInvocation_ShouldPassPromptAsArgument()
    {
        const string templateBody = "## Akzeptanzkriterien\n- offen";
        const string originalRequirement = "Die Issue-Anlage soll Templates per KI ausfuellen.";
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHubCopilot.ExecutablePath"))
            .Returns(@"C:\tools\copilot.exe");

        var invocation = _sut.BuildIssueTemplateFillInvocation(templateBody, originalRequirement);

        invocation.ProcessStartInfo.FileName.Should().Be(@"C:\tools\copilot.exe");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("-p");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("-s");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("--no-ask-user");
        invocation.StandardInput.Should().BeNull();
        invocation.ProcessStartInfo.ArgumentList.Should().ContainSingle(arg =>
            arg.Contains("Template:", StringComparison.Ordinal)
            && arg.Contains(templateBody)
            && arg.Contains("Originalanforderung:")
            && arg.Contains(originalRequirement));
    }

    /// <summary>BuildIssueTemplateFillInvocation setzt GH_TOKEN wenn ein GitHub-Token gespeichert ist.</summary>
    [Fact]
    public void BuildIssueTemplateFillInvocation_ShouldSetGhToken_WhenStored()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHub.Token"))
            .Returns("ghp_test-token");

        var invocation = _sut.BuildIssueTemplateFillInvocation("template", null);

        invocation.ProcessStartInfo.EnvironmentVariables["GH_TOKEN"].Should().Be("ghp_test-token");
    }

    /// <summary>BuildIssueTemplateFillInvocation haengt gespeicherte CommandLineParameters als einzelne Argumente an.</summary>
    [Fact]
    public void BuildIssueTemplateFillInvocation_ShouldAppendCommandLineParameters()
    {
        _credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.GitHubCopilot.CommandLineParameters"))
            .Returns("--model gpt-4o");

        var invocation = _sut.BuildIssueTemplateFillInvocation("template", null);

        invocation.ProcessStartInfo.ArgumentList.Should().Contain("--model");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("gpt-4o");
    }
}
