using System.Diagnostics;
using FluentAssertions;
using Moq;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Abstractions;

/// <summary>Tests für die Basisklasse CliKiPluginBase.</summary>
public sealed class CliKiPluginBaseTests
{
    /// <summary>StartCliAsync gibt ProcessStartInfo aus BuildProcessStartInfo zurück.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldReturnProcessStartInfo_FromBuildProcessStartInfo()
    {
        var sut = new TestCliKiPlugin(supportsSession: false);

        var result = await sut.StartCliAsync("/repo/path");

        result.FileName.Should().Be("test-cli");
        result.WorkingDirectory.Should().Be("/repo/path");
    }

    /// <summary>SupportsSessionContinuation gibt den konfigurierten Wert zurück.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SupportsSessionContinuation_ShouldReturnConfiguredValue(bool expected)
    {
        var sut = new TestCliKiPlugin(supportsSession: expected);

        sut.SupportsSessionContinuation().Should().Be(expected);
    }

    /// <summary>StartCliAsync gibt den übergebenen Parameter an BuildProcessStartInfo weiter.</summary>
    [Fact]
    public async Task StartCliAsync_ShouldPassParameters_ToBuildProcessStartInfo()
    {
        var sut = new TestCliKiPlugin(supportsSession: true);

        var result = await sut.StartCliAsync("/repo", "session-123");

        result.Arguments.Should().Be("session-123");
    }

    /// <summary>GetCliHelpTextAsync gibt null zurück, wenn die CLI nicht gefunden wird.</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnNull_WhenCliNotFound()
    {
        var sut = new TestCliKiPluginWithHelpOverride();

        var result = await sut.GetCliHelpTextAsync();

        result.Should().BeNull();
    }

    /// <summary>GetCliHelpTextAsync gibt Ausgabe zurück, wenn die CLI vorhanden ist (dotnet --help).</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnOutput_WhenCliIsPresent()
    {
        var sut = new TestCliKiPluginWithDotnetHelp();

        var result = await sut.GetCliHelpTextAsync();

        result.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>GetCliHelpTextAsync gibt null zurück bei Timeout.</summary>
    [Fact]
    public async Task GetCliHelpTextAsync_ShouldReturnNull_OnTimeout()
    {
        var sut = new TestCliKiPluginWithShortTimeout("dotnet", TimeSpan.FromMilliseconds(1));

        var result = await sut.GetCliHelpTextAsync();

        result.Should().BeNull();
    }

    /// <summary>AppendCommandLineParameters hängt den Credential-Wert an leere Arguments an.</summary>
    [Fact]
    public async Task AppendCommandLineParameters_ShouldSetArguments_WhenArgumentsAreEmpty()
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Test.CommandLineParameters"))
            .Returns("--verbose");
        var sut = new TestCliKiPluginWithCredentialStore(credentialStoreMock.Object);

        var result = await sut.StartCliAsync("/repo");

        result.Arguments.Should().Be("--verbose");
    }

    /// <summary>AppendCommandLineParameters hängt den Credential-Wert an bestehende Arguments an.</summary>
    [Fact]
    public async Task AppendCommandLineParameters_ShouldAppendArguments_WhenArgumentsExist()
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential("Softwareschmiede.Test.CommandLineParameters"))
            .Returns("--extra-flag");
        var sut = new TestCliKiPluginWithCredentialStore(credentialStoreMock.Object);

        var result = await sut.StartCliAsync("/repo", "--initial-flag");

        result.Arguments.Should().Be("--initial-flag --extra-flag");
    }

    /// <summary>AppendCommandLineParameters hat keinen Effekt, wenn kein Credential hinterlegt ist.</summary>
    [Fact]
    public async Task AppendCommandLineParameters_ShouldNotModifyArguments_WhenCredentialIsNull()
    {
        var credentialStoreMock = new Mock<ICredentialStore>();
        credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns((string?)null);
        var sut = new TestCliKiPluginWithCredentialStore(credentialStoreMock.Object);

        var result = await sut.StartCliAsync("/repo", "--initial");

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

        var result = await TestCliKiPlugin.RunOneShotAsync(
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

    private abstract class BaseTestPlugin(string providerPraefix = "test") : CliKiPluginBase
    {
        public override string ProviderDateiPraefix => providerPraefix;
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

    private sealed class TestCliKiPlugin(bool supportsSession) : BaseTestPlugin("test-cli")
    {
        public override bool SupportsSessionContinuation() => supportsSession;

        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
            => new() { FileName = "test-cli", Arguments = parameters ?? string.Empty, WorkingDirectory = localRepoPath };

        public static Task<string> RunOneShotAsync(ProcessStartInfo psi, string standardInput)
            => RunOneShotTextGenerationAsync(psi, standardInput, CancellationToken.None);
    }

    private sealed class TestCliKiPluginWithHelpOverride() : BaseTestPlugin("nonexistent-cli-xyz")
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
            => new() { FileName = ProviderDateiPraefix, WorkingDirectory = localRepoPath };
    }

    private sealed class TestCliKiPluginWithCredentialStore(ICredentialStore cs) : BaseTestPlugin
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
        {
            var psi = new ProcessStartInfo { FileName = "test-cli", Arguments = parameters ?? string.Empty, WorkingDirectory = localRepoPath };
            AppendCommandLineParameters(psi, cs, PluginPrefix);
            return psi;
        }
    }

    private sealed class TestCliKiPluginWithDotnetHelp() : BaseTestPlugin("dotnet")
    {
        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
            => new() { FileName = "dotnet", WorkingDirectory = localRepoPath };
    }

    private sealed class TestCliKiPluginWithShortTimeout(string exe, TimeSpan timeout) : BaseTestPlugin(exe)
    {
        public override Task<string?> GetCliHelpTextAsync(CancellationToken ct = default)
            => RunHelpCommandAsync(ProviderDateiPraefix, ct, timeout);

        protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
            => new() { FileName = ProviderDateiPraefix, WorkingDirectory = localRepoPath };
    }
}
