using FluentAssertions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Domain.Interfaces;

public sealed class IKiPluginContractTests
{
    [Fact]
    public void IKiPlugin_ShouldExposeSingleStartDevelopmentAsyncOverload_WithExecutionIdParameter()
    {
        var methods = typeof(IKiPlugin).GetMethods()
            .Where(m => m.Name == nameof(IKiPlugin.StartDevelopmentAsync))
            .ToList();

        methods.Should().ContainSingle();
        var method = methods.Single();
        method.ReturnType.Should().Be(typeof(IAsyncEnumerable<string>));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(6);
        parameters[0].ParameterType.Should().Be(typeof(string));
        parameters[1].ParameterType.Should().Be(typeof(AgentInfo));
        parameters[2].ParameterType.Should().Be(typeof(string));
        parameters[3].ParameterType.Should().Be(typeof(string));
        parameters[4].ParameterType.Should().Be(typeof(string));
        parameters[5].ParameterType.Should().Be(typeof(CancellationToken));
        parameters[5].HasDefaultValue.Should().BeTrue();
    }
}
