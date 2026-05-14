using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

public sealed class KiSimulatorPluginTests
{
    private readonly Mock<ICredentialStore> _credentialStoreMock = new();
    private readonly KiSimulatorPlugin _sut;

    public KiSimulatorPluginTests()
    {
        _sut = new KiSimulatorPlugin(
            _credentialStoreMock.Object,
            new Mock<ILogger<KiSimulatorPlugin>>().Object);
    }

    [Fact]
    public void PluginMetadata_ShouldExposeExpectedValues()
    {
        _sut.PluginName.Should().Be("KI Simulator");
        _sut.PluginPrefix.Should().Be("Softwareschmiede.KiSimulator");
        _sut.ProviderDateiPraefix.Should().Be("simulator");
        _sut.GetSettingGroups().Should().ContainSingle();
        _sut.GetSettingGroups().Single().Fields.Select(f => f.Key)
            .Should().Equal("Delay12Ms", "Delay23Ms", "Delay34Ms");
    }

    [Fact]
    public async Task StartDevelopmentAsync_ShouldReturnDeterministicFourSteps_IrrespectiveOfPrompt()
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns("0");
        var agent = new AgentInfo("simulator", null, "builtin://simulator");

        var outputs = new List<string>();
        await foreach (var output in _sut.StartDevelopmentAsync(string.Empty, agent, "repo"))
        {
            outputs.Add(output);
        }

        outputs.Should().HaveCount(4);
        outputs[0].Should().Be("Ich kümmere mich darum, die Anforderung zu verstehen.");
        outputs[1].Should().Be("Ich habe die Anforderung verstanden. ich mache mir nun einen Plan.");
        outputs[2].Should().Be("Der Plan ist fertig. Ich begebe mich nun an die Umsetzung.");
        outputs[3].Should().Contain("Fertig. Hier ist das Ergebnis:");
        outputs[3].Should().Contain("Lorem ipsum dolor sit amet");
        outputs[3].Should().Contain("Duis autem vel eum iriure dolor in hendrerit");
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("10001")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task StartDevelopmentAsync_ShouldFallbackTo2000Ms_WhenDelaySettingInvalid(string invalidValue)
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns(invalidValue);
        var agent = new AgentInfo("simulator", null, "builtin://simulator");
        var watch = Stopwatch.StartNew();

        var outputs = new List<string>();
        await foreach (var output in _sut.StartDevelopmentAsync("Prompt", agent, "repo"))
        {
            outputs.Add(output);
            if (outputs.Count == 2)
            {
                break;
            }
        }
        watch.Stop();

        outputs.Should().Equal(
            "Ich kümmere mich darum, die Anforderung zu verstehen.",
            "Ich habe die Anforderung verstanden. ich mache mir nun einen Plan.");
        watch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(1700));
    }

    [Theory]
    [InlineData("0", 500, 0)]
    [InlineData("10000", 12000, 9500)]
    public async Task StartDevelopmentAsync_ShouldRespectDelayBoundaries(string configuredDelay, int maximumElapsedMilliseconds, int minimumElapsedMilliseconds)
    {
        _credentialStoreMock.Setup(c => c.GetCredential(It.IsAny<string>())).Returns(configuredDelay);
        var agent = new AgentInfo("simulator", null, "builtin://simulator");
        var watch = Stopwatch.StartNew();

        var outputs = new List<string>();
        await foreach (var output in _sut.StartDevelopmentAsync("Prompt", agent, "repo"))
        {
            outputs.Add(output);
            if (outputs.Count == 2)
            {
                break;
            }
        }
        watch.Stop();

        outputs.Should().HaveCount(2);
        watch.ElapsedMilliseconds.Should().BeLessThan(maximumElapsedMilliseconds);
        watch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(minimumElapsedMilliseconds);
    }

    [Fact]
    public async Task RunTestsAndHealth_ShouldBeDeterministicWithoutExternalDependencies()
    {
        var result = await _sut.RunTestsAsync("repo");
        var health = await _sut.CheckHealthAsync();

        result.Bestanden.Should().BeTrue();
        result.Ergebnisse.Should().ContainSingle();
        result.Ergebnisse[0].TestName.Should().Be("KI-Simulator Selbsttest");
        health.Should().BeTrue();
    }
}
