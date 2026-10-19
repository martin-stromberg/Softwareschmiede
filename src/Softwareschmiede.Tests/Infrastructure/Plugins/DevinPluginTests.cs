using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Tests fuer das Devin-CLI-Plugin.</summary>
public sealed class DevinPluginTests
{
    private readonly Mock<ICredentialStore> _credentialStoreMock;
    private readonly DevinPlugin _sut;

    /// <summary>Initialisiert eine neue Instanz der <see cref="DevinPluginTests"/>.</summary>
    public DevinPluginTests()
    {
        _credentialStoreMock = new Mock<ICredentialStore>();
        _sut = new DevinPlugin(
            _credentialStoreMock.Object,
            new Mock<ILogger<DevinPlugin>>().Object);
    }

    /// <summary>Exposes expected plugin metadata.</summary>
    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("Devin CLI");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.Devin");
        _sut.ProviderDateiPraefix.Should().Be("devin");
        _sut.PluginType.Should().Be(PluginType.DevelopmentAutomation);

        var settings = _sut.GetSettingGroups();
        settings.Should().HaveCount(2);
        settings.Should().Contain(group => group.GroupName == "Ausfuehrung");
        settings.Should().Contain(group => group.GroupName == "CLI-Konfiguration");
        settings.SelectMany(g => g.Fields).Should().Contain(f => f.Key == "ExecutablePath");
        settings.SelectMany(g => g.Fields).Should().Contain(f => f.Key == "CommandLineParameters");
        settings.SelectMany(g => g.Fields).Should().NotContain(f =>
            f.Key.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || f.Key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
            || f.FieldType == PluginSettingFieldType.Secret);
    }

    /// <summary>SupportsSessionContinuation returns true because Devin supports CLI session resume flags.</summary>
    [Fact]
    public void SupportsSessionContinuation_ShouldReturnTrue()
    {
        _sut.SupportsSessionContinuation().Should().BeTrue();
    }

    /// <summary>Devin benötigt den verzögerten, wiederholten Submit: Ein \r im selben Eingabe-Read wie der
    /// Prompttext wird von der Devin CLI als Zeilenumbruch gewertet, nicht als Enter-Submit — und das für
    /// einen Submit nötige Abstands-Fenster wächst mit der Prompt-Größe, sodass ein einzelner verzögerter
    /// Submit bei großen SCM-Prompts verloren gehen kann (empirisch gegen devin.exe v3000.11.92 unter
    /// ConPTY verifiziert: ~10 KB → ~0,4 s, ~20–30 KB → ~0,7–1,1 s nötiger Abstand).</summary>
    [Fact]
    public void PromptSubmitDelay_ShouldRequireSeparateDelayedSubmit()
    {
        _sut.PromptSubmitDelay.Should().BeGreaterThan(TimeSpan.Zero);
        _sut.PromptSubmitRepeatCount.Should().BeGreaterThan(0,
            "der Submit muss wiederholt werden, weil das nötige Abstands-Fenster größenabhängig streut");
        _sut.PromptSubmitRepeatInterval.Should().BeGreaterThan(TimeSpan.Zero);
        (_sut.PromptSubmitDelay + _sut.PromptSubmitRepeatCount * _sut.PromptSubmitRepeatInterval)
            .Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(2.5),
                "die Wiederholungen müssen große Prompts (~30 KB ≈ 1,1 s) mit deutlichem Sicherheitspuffer abdecken");
    }

    /// <summary>Die Startbeschreibung trägt die Submit-Verzögerung und -Wiederholung in die Terminal-Session.</summary>
    [Fact]
    public async Task GetTerminalStartSpecAsync_MapsPromptSubmitDelayIntoSpec()
    {
        var spec = await _sut.GetTerminalStartSpecAsync("/repo");

        spec.PromptSubmitDelay.Should().Be(_sut.PromptSubmitDelay);
        spec.PromptSubmitRepeatCount.Should().Be(_sut.PromptSubmitRepeatCount);
        spec.PromptSubmitRepeatInterval.Should().Be(_sut.PromptSubmitRepeatInterval);
        spec.Capabilities.Should().HaveFlag(TerminalProviderCapabilities.RequiresPty);
    }

    /// <summary>StartCliAsync uses default devin command when no path is configured.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseDevinCommand_WhenNoPathConfigured()
    {
        _credentialStoreMock.Setup(store => store.GetCredential(It.IsAny<string>())).Returns((string?)null);

        var psi = await _sut.StartCliAsync("/repo/path");

        Path.GetFileNameWithoutExtension(psi.FileName).Should().Be("devin");
        psi.WorkingDirectory.Should().Be("/repo/path");
        psi.Arguments.Should().BeEmpty();
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeFalse();
    }

    /// <summary>StartCliAsync uses configured executable path when provided.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldUseConfiguredPath_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ExecutablePath"))
            .Returns(@"C:\tools\devin.exe");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\devin.exe");
    }

    /// <summary>GetCliHelpTextAsync calls the configured executable with --help.</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnHelpText_WhenConfiguredExecutableSupportsHelp()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ExecutablePath"))
            .Returns("dotnet");

        var helpText = await _sut.GetCliHelpTextAsync();

        helpText.Should().NotBeNullOrWhiteSpace();
        helpText.Should().Contain("--help");
    }

    /// <summary>StartCliAsync trims quotes from configured executable path.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldTrimQuotes_WhenExecutablePathIsSet()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ExecutablePath"))
            .Returns("\"C:\\tools\\devin.exe\"");

        var psi = await _sut.StartCliAsync("/repo");

        psi.FileName.Should().Be(@"C:\tools\devin.exe");
    }

    /// <summary>StartCliAsync passes optional Devin CLI parameters as arguments.</summary>
    [Theory]
    [InlineData("Build the project")]
    [InlineData("--continue")]
    [InlineData("-c")]
    [InlineData("--resume session-123")]
    [InlineData("-r session-123")]
    [InlineData("--print")]
    [InlineData("--prompt-file prompt.md")]
    [InlineData("--model sonnet")]
    [InlineData("--permission-mode plan")]
    public async Task StartCliAsync_ShouldPassOptionalDevinParameters_AsArguments(string parameters)
    {
        var psi = await _sut.StartCliAsync("/repo", parameters);

        psi.Arguments.Should().Be(parameters);
    }

    /// <summary>StartCliAsync appends CommandLineParameters from credential store to arguments.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldIncludeCommandLineParameters_InProcessStartInfo()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.CommandLineParameters"))
            .Returns("--permission-mode plan --model sonnet");

        var psi = await _sut.StartCliAsync("/repo", "--continue");

        psi.Arguments.Should().Be("--continue --permission-mode plan --model sonnet");
    }

    /// <summary>StartCliAsync does not create Devin credential arguments or environment variables.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldNotSetAuthEnvironmentVariablesOrCredentialArguments()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.Token"))
            .Returns("token-must-not-be-used");
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ApiKey"))
            .Returns("key-must-not-be-used");

        var psi = await _sut.StartCliAsync("/repo");

        psi.Arguments.Should().NotContain("token-must-not-be-used");
        psi.Arguments.Should().NotContain("key-must-not-be-used");
        psi.EnvironmentVariables.ContainsKey("DEVIN_API_KEY").Should().BeFalse();
        psi.EnvironmentVariables.ContainsKey("DEVIN_TOKEN").Should().BeFalse();
    }

    /// <summary>Devin bietet die rueckgabefaehige Issue-Template-Ausfuellhilfe an.</summary>
    [Fact]
    public void Plugin_ShouldExposeIssueTemplateTextGeneratorCapability()
    {
        _sut.Should().BeAssignableTo<IIssueTemplateTextGenerator>();
    }

    /// <summary>Issue-Ausfuellhilfe uebergibt den Prompt als Argument an Devin -p.</summary>
    [Fact]
    public void BuildIssueTemplateFillInvocation_ShouldPassPromptAsArgument()
    {
        const string templateBody = "## Fehlerbeschreibung\nBitte ausfuellen.";
        const string originalRequirement = "Als Nutzer moechte ich ein Issue aus einer Aufgabe anlegen.";
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.ExecutablePath"))
            .Returns(@"C:\tools\devin.exe");

        var invocation = _sut.BuildIssueTemplateFillInvocation(templateBody, originalRequirement);

        invocation.ProcessStartInfo.FileName.Should().Be(@"C:\tools\devin.exe");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("-p");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("--respect-workspace-trust");
        invocation.ProcessStartInfo.ArgumentList.Should().Contain("false");
        invocation.StandardInput.Should().BeNull();
        invocation.ProcessStartInfo.ArgumentList.Should().ContainSingle(arg =>
            arg.Contains("Template:", StringComparison.Ordinal)
            && arg.Contains(templateBody)
            && arg.Contains("Originalanforderung:")
            && arg.Contains(originalRequirement));
    }

    /// <summary>BuildIssueTemplateFillInvocation haengt gespeicherte CommandLineParameters als einzelne Argumente an.</summary>
    [Fact]
    public void BuildIssueTemplateFillInvocation_ShouldAppendCommandLineParameters()
    {
        _credentialStoreMock.Setup(store => store.GetCredential("Softwareschmiede.Devin.CommandLineParameters"))
            .Returns("--model sonnet --permission-mode plan");

        var invocation = _sut.BuildIssueTemplateFillInvocation("template", null);

        invocation.ProcessStartInfo.ArgumentList.Should().ContainInOrder("--model", "sonnet", "--permission-mode", "plan");
    }
}
