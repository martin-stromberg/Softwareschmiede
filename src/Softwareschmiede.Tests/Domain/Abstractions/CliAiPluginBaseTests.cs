using System.Diagnostics;
using FluentAssertions;
using Moq;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Abstractions;

/// <summary>Tests für die Basisklasse CliKiPluginBase.</summary>
public sealed class CliAiPluginBaseTests
{
    /// <summary>StartCliAsync gibt ProcessStartInfo aus BuildProcessStartInfo zurück.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfo_FromBuildProcessStartInfo()
    {
        var sut = new TestCliAiPlugin(supportsSession: false);

        var result = await sut.StartCliAsync("/repo/path", ct: TestContext.Current.CancellationToken);

        result.FileName.Should().Be("test-cli");
        result.WorkingDirectory.Should().Be("/repo/path");
    }

    /// <summary>SupportsSessionContinuation gibt den konfigurierten Wert zurück.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SupportsSessionContinuation_ShouldReturnConfiguredValue(bool expected)
    {
        var sut = new TestCliAiPlugin(supportsSession: expected);

        sut.SupportsSessionContinuation().Should().Be(expected);
    }

    /// <summary>StartCliAsync gibt den übergebenen Parameter an BuildProcessStartInfo weiter.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldPassParameter_ToBuildProcessStartInfo()
    {
        var sut = new TestCliAiPlugin(supportsSession: true);

        var result = await sut.StartCliAsync("/repo", "session-123", TestContext.Current.CancellationToken);

        result.Arguments.Should().Be("session-123");
    }

    /// <summary>GetCliHelpTextAsync gibt null zurück, wenn die CLI nicht gefunden wird.</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnNull_WhenCliNotFound()
    {
        var sut = new TestCliAiPluginWithHelpOverride();

        var result = await sut.GetCliHelpTextAsync(TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    /// <summary>GetCliHelpTextAsync gibt Ausgabe zurück, wenn die CLI vorhanden ist (dotnet --help).</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnOutput_WhenCliIsPresent()
    {
        var sut = new TestCliAiPluginWithDotnetHelp();

        var result = await sut.GetCliHelpTextAsync(TestContext.Current.CancellationToken);

        result.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>GetCliHelpTextAsync gibt null zurück bei Timeout.</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnNull_OnTimeout()
    {
        var sut = new TestCliAiPluginWithShortTimeout("dotnet", TimeSpan.FromMilliseconds(1));

        var result = await sut.GetCliHelpTextAsync(TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    /// <summary>AppendCommandLineParameters hängt den Credential-Wert an leere Arguments an.</summary>
    [Fact]
    public async Task AppendCommandLineParameter_ShouldSetArguments_WhenArgumentsAreEmpty()
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Test.CommandLineParameters"))
            .Returns("--verbose");
        var sut = new TestCliAiPluginWithCredentialStore(credentialStoreMock.Object);

        var result = await sut.StartCliAsync("/repo", ct: TestContext.Current.CancellationToken);

        result.Arguments.Should().Be("--verbose");
    }

    /// <summary>AppendCommandLineParameters hängt den Credential-Wert an bestehende Arguments an.</summary>
    [Fact]
    public async Task AppendCommandLineParameter_ShouldAppendArguments_WhenArgumentsExist()
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Test.CommandLineParameters"))
            .Returns("--extra-flag");
        var sut = new TestCliAiPluginWithCredentialStore(credentialStoreMock.Object);

        var result = await sut.StartCliAsync("/repo", "--initial-flag", TestContext.Current.CancellationToken);

        result.Arguments.Should().Be("--initial-flag --extra-flag");
    }

    /// <summary>AppendCommandLineParameters hat keinen Effekt, wenn kein Credential hinterlegt ist.</summary>
    [Fact]
    public async Task AppendCommandLineParameter_ShouldNotModifyArguments_WhenCredentialIsNull()
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
        var sut = new TestCliAiPluginWithCredentialStore(credentialStoreMock.Object);

        var result = await sut.StartCliAsync("/repo", "--initial", TestContext.Current.CancellationToken);

        result.Arguments.Should().Be("--initial");
    }

    /// <summary>RunOneShotTextGenerationAsync verarbeitet stdin und stdout explizit als UTF-8.</summary>
    [Fact]
    public async Task RunOneShotTextGenerationAsync_ShouldRoundtripGermanUtf8Characters()
    {
        var psi = new ProcessStartInfo { FileName = "powershell" };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("""
            $inputStream = [Console]::OpenStandardInput()
            $memory = [System.IO.MemoryStream]::new()
            $inputStream.CopyTo($memory)
            $inputText = [System.Text.Encoding]::UTF8.GetString($memory.ToArray())
            $outputText = "$inputText`nKI-Ausgabe: plötzlich für Selbstwertgefühl äöüß €"
            $outputBytes = [System.Text.Encoding]::UTF8.GetBytes($outputText)
            [Console]::OpenStandardOutput().Write($outputBytes, 0, $outputBytes.Length)
            """);

        var result = await TestCliAiPlugin.RunOneShotAsync(
            psi,
            "Originalanforderung: Größe ändern, Straße prüfen & fürs Team dokumentieren.");

        result.Should().Contain("Größe ändern, Straße prüfen & fürs Team dokumentieren.");
        result.Should().Contain("plötzlich für Selbstwertgefühl äöüß €");
        result.Should().NotContain("Ã");
        result.Should().NotContain("â‚¬");
        psi.StandardInputEncoding.Should().BeSameAs(System.Text.Encoding.UTF8);
        psi.StandardOutputEncoding.Should().BeSameAs(System.Text.Encoding.UTF8);
        psi.StandardErrorEncoding.Should().BeSameAs(System.Text.Encoding.UTF8);
    }

    /// <summary>GetTerminalStartSpecAsync mappt BuildProcessStartInfo auf eine TerminalSessionStartSpec inkl. Capabilities.</summary>
    [Fact]
    public async Task GetTerminalStartSpecAsync_MapsProcessStartInfoAndCapabilities()
    {
        var sut = new TestCliAiPlugin(supportsSession: true);

        var spec = await sut.GetTerminalStartSpecAsync("/repo", "session-42", TestContext.Current.CancellationToken);

        spec.FileName.Should().Be("test-cli");
        spec.Arguments.Should().Be("session-42");
        spec.WorkingDirectory.Should().Be("/repo");
        spec.PluginName.Should().Be("Test");
        spec.OptionalParameters.Should().Be("session-42");
        spec.Capabilities.Should().Be(TerminalProviderCapabilities.SupportsPty);
    }

    /// <summary>BuildTerminalStartSpec wirft InvalidOperationException bei leerem FileName
    /// (Vertragsverletzung der Plugin-Implementierung, kein Aufrufer-Fehler).</summary>
    [Fact]
    public void BuildTerminalStartSpec_FileNameEmpty_ThrowsInvalidOperationException()
    {
        var sut = new TestCliAiPluginWithEmptyFileName();

        var act = () => sut.BuildTerminalStartSpec("/repo", null);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Test");
    }

    /// <summary>Ein Plugin mit RequiresPty-Capability liefert diese in der Spec.</summary>
    [Fact]
    public async Task GetTerminalStartSpecAsync_RequiresPtyPlugin_ReturnsRequiresPtyCapability()
    {
        var sut = new TestCliAiPluginRequiresPty();

        var spec = await sut.GetTerminalStartSpecAsync("/repo", ct: TestContext.Current.CancellationToken);

        spec.Capabilities.Should().HaveFlag(TerminalProviderCapabilities.RequiresPty);
        spec.Capabilities.Should().HaveFlag(TerminalProviderCapabilities.SupportsPty);
    }

    private sealed class TestCliAiPluginWithEmptyFileName() : BaseTestPlugin
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
            => new() { FileName = "", WorkingDirectory = localRepoPath };
    }

    private sealed class TestCliAiPluginRequiresPty() : BaseTestPlugin
    {
        public override TerminalProviderCapabilities TerminalCapabilities =>
            TerminalProviderCapabilities.RequiresPty | TerminalProviderCapabilities.SupportsPty;

        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
            => new() { FileName = "test-cli", WorkingDirectory = localRepoPath };
    }

    private abstract class BaseTestPlugin(string providerPrefix = "test") : CliAiPluginBase
    {
        public override string ProviderFilePrefix => providerPrefix;
        public override string PluginName => "Test";
        public override string PluginPrefix => "Softwareschmiede.Test";
        public override PluginType PluginType => PluginType.DevelopmentAutomation;
        /// <summary>IReadOnlyList.</summary>
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        /// <summary>SupportsSessionContinuation.</summary>
        public override bool SupportsSessionContinuation() => false;
        /// <summary>Task.</summary>
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class TestCliAiPlugin(bool supportsSession) : BaseTestPlugin("test-cli")
    {
        public override bool SupportsSessionContinuation() => supportsSession;

        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
            => new() { FileName = "test-cli", Arguments = parameter ?? string.Empty, WorkingDirectory = localRepoPath };

        public static Task<string> RunOneShotAsync(ProcessStartInfo psi, string defaultInput)
            => RunOneShotTextGenerationAsync(psi, defaultInput, CancellationToken.None);
    }

    private sealed class TestCliAiPluginWithHelpOverride() : BaseTestPlugin("nonexistent-cli-xyz")
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
            => new() { FileName = ProviderFilePrefix, WorkingDirectory = localRepoPath };
    }

    private sealed class TestCliAiPluginWithCredentialStore(ICredentialStore cs) : BaseTestPlugin
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
        {
            var psi = new ProcessStartInfo { FileName = "test-cli", Arguments = parameter ?? string.Empty, WorkingDirectory = localRepoPath };
            AppendCommandLineParameters(psi, cs, PluginPrefix);
            return psi;
        }
    }

    private sealed class TestCliAiPluginWithDotnetHelp() : BaseTestPlugin("dotnet")
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
            => new() { FileName = "dotnet", WorkingDirectory = localRepoPath };
    }

    private sealed class TestCliAiPluginWithShortTimeout(string exe, TimeSpan timeout) : BaseTestPlugin(exe)
    {
        public override Task<string?> GetCliHelpTextAsync(CancellationToken ct = default)
            => RunHelpCommandAsync(ProviderFilePrefix, ct, timeout);

        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameter)
            => new() { FileName = ProviderFilePrefix, WorkingDirectory = localRepoPath };
    }
}
