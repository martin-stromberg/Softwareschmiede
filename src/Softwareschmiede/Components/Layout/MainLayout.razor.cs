using Microsoft.AspNetCore.Components;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private IRunningAutomationStatusSource RunningAutomationStatusSource { get; set; } = null!;
    [Inject] private IAutoShutdownOrchestrator AutoShutdownOrchestrator { get; set; } = null!;

    private int _runningAutomationCount;
    private bool _autoShutdownEnabled;

    protected override void OnInitialized()
    {
        _runningAutomationCount = RunningAutomationStatusSource.GetRunningCount();
        RunningAutomationStatusSource.RunningCountChanged += RunningCountChanged;
        AutoShutdownOrchestrator.SetEnabled(_autoShutdownEnabled);
    }

    private void RunningCountChanged(int previousCount, int currentCount)
    {
        _runningAutomationCount = currentCount;
        _ = InvokeAsync(StateHasChanged);
    }

    private void AutoShutdownChanged(ChangeEventArgs changeEventArgs)
    {
        _autoShutdownEnabled = changeEventArgs.Value switch
        {
            bool checkedValue => checkedValue,
            string stringValue when bool.TryParse(stringValue, out var parsedValue) => parsedValue,
            _ => false
        };
        AutoShutdownOrchestrator.SetEnabled(_autoShutdownEnabled);
    }

    public void Dispose()
    {
        RunningAutomationStatusSource.RunningCountChanged -= RunningCountChanged;
    }
}
