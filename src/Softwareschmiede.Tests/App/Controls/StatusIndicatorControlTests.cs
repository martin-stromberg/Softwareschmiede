using System.Threading;
using FluentAssertions;
using Softwareschmiede.App.Controls;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für StatusIndicatorControl.</summary>
public sealed class StatusIndicatorControlTests
{
    /// <summary>Dependency Property BranchName kann gesetzt und gelesen werden.</summary>
    [Fact]
    public void BranchName_DependencyProperty_GetSet()
    {
        string? result = null;
        var thread = new Thread(() =>
        {
            var control = new StatusIndicatorControl();
            control.BranchName = "feature/login-fix";
            result = control.BranchName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        result.Should().Be("feature/login-fix");
    }
}
