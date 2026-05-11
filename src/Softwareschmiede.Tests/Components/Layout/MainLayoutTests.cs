using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Layout;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Tests.Components.Layout;

public sealed class MainLayoutTests
{
    [Fact]
    public void MainLayoutMarkup_ShouldContainRunningCountAndAutoShutdownToggle()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Layout", "MainLayout.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("Laufende Automatisierungen");
        markup.Should().Contain("@_runningAutomationCount");
        markup.Should().Contain("@if (_runningAutomationCount > 0)");
        markup.Should().Contain("@onchange=\"AutoShutdownChanged\"");
    }

    [Fact]
    public void OnInitialized_ShouldLoadRunningCount_SubscribeAndDisableAutoShutdown()
    {
        var runningSource = new FakeRunningAutomationStatusSource { RunningCount = 3 };
        var orchestrator = new FakeAutoShutdownOrchestrator();
        var sut = CreateSut(runningSource, orchestrator);

        sut.InvokeOnInitialized();

        GetPrivateField<int>(sut, "_runningAutomationCount").Should().Be(3);
        runningSource.SubscriberCount.Should().Be(1);
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeFalse();
    }

    [Fact]
    public void AutoShutdownChanged_ShouldParseBoolValue()
    {
        var sut = CreateSut(new FakeRunningAutomationStatusSource(), new FakeAutoShutdownOrchestrator());

        InvokePrivate(sut, "AutoShutdownChanged", new ChangeEventArgs { Value = true });

        GetPrivateField<bool>(sut, "_autoShutdownEnabled").Should().BeTrue();
        var orchestrator = GetInjected<FakeAutoShutdownOrchestrator>(sut, "AutoShutdownOrchestrator");
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public void AutoShutdownChanged_ShouldParseStringBoolValue()
    {
        var sut = CreateSut(new FakeRunningAutomationStatusSource(), new FakeAutoShutdownOrchestrator());

        InvokePrivate(sut, "AutoShutdownChanged", new ChangeEventArgs { Value = "true" });

        GetPrivateField<bool>(sut, "_autoShutdownEnabled").Should().BeTrue();
        var orchestrator = GetInjected<FakeAutoShutdownOrchestrator>(sut, "AutoShutdownOrchestrator");
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public void AutoShutdownChanged_ShouldFallbackToFalse_OnInvalidValue()
    {
        var sut = CreateSut(new FakeRunningAutomationStatusSource(), new FakeAutoShutdownOrchestrator());

        InvokePrivate(sut, "AutoShutdownChanged", new ChangeEventArgs { Value = "ungueltig" });

        GetPrivateField<bool>(sut, "_autoShutdownEnabled").Should().BeFalse();
        var orchestrator = GetInjected<FakeAutoShutdownOrchestrator>(sut, "AutoShutdownOrchestrator");
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromRunningCountChanged()
    {
        var runningSource = new FakeRunningAutomationStatusSource { RunningCount = 1 };
        var sut = CreateSut(runningSource, new FakeAutoShutdownOrchestrator());
        sut.InvokeOnInitialized();

        sut.Dispose();

        runningSource.SubscriberCount.Should().Be(0);
    }

    private static TestMainLayout CreateSut(
        IRunningAutomationStatusSource runningSource,
        IAutoShutdownOrchestrator autoShutdownOrchestrator)
    {
        var sut = new TestMainLayout();
        SetInjectedProperty(sut, "RunningAutomationStatusSource", runningSource);
        SetInjectedProperty(sut, "AutoShutdownOrchestrator", autoShutdownOrchestrator);
        return sut;
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var property = typeof(MainLayout).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist for test setup.");
        property!.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = typeof(MainLayout).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Field {fieldName} should exist.");
        return (T)field!.GetValue(target)!;
    }

    private static T GetInjected<T>(object target, string propertyName)
        where T : class
    {
        var property = typeof(MainLayout).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist.");
        return (property!.GetValue(target) as T)!;
    }

    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var method = typeof(MainLayout)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name.Equals(methodName, StringComparison.Ordinal)
                && m.GetParameters().Length == args.Length);
        method.Should().NotBeNull($"Method {methodName} should exist.");
        return method!.Invoke(target, args);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Softwareschmiede.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with Softwareschmiede.slnx not found.");
    }

    private sealed class FakeRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        public int RunningCount { get; set; }

        public event Action<int, int>? RunningCountChanged;

        public int SubscriberCount => RunningCountChanged?.GetInvocationList().Length ?? 0;

        public int GetRunningCount() => RunningCount;
    }

    private sealed class FakeAutoShutdownOrchestrator : IAutoShutdownOrchestrator
    {
        public List<bool> SetEnabledCalls { get; } = [];

        public void SetEnabled(bool enabled) => SetEnabledCalls.Add(enabled);
    }

    private sealed class TestMainLayout : MainLayout
    {
        public void InvokeOnInitialized() => OnInitialized();
    }
}
