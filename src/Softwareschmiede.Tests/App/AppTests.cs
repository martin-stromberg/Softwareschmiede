using System.Reflection;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Softwareschmiede.Tests.App;

/// <summary>Unit-Tests für die globalen Exception-Handler von Softwareschmiede.App.App (F1-F3).</summary>
public sealed class AppTests
{
    /// <summary>DispatcherUnhandledException-Handler loggt die Exception und setzt e.Handled = true.</summary>
    [Fact]
    public void DispatcherUnhandledException_Handler_LogsAndHandlesException()
    {
        var sink = new CollectingSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        try
        {
            var exception = new InvalidOperationException("Test-UI-Thread-Fehler");
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            var argsType = typeof(System.Windows.Threading.DispatcherUnhandledExceptionEventArgs);
            var ctor = argsType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(System.Windows.Threading.Dispatcher) },
                null)!;
            var args = (System.Windows.Threading.DispatcherUnhandledExceptionEventArgs)ctor.Invoke(new object[] { dispatcher });
            argsType.GetField("_exception", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(args, exception);

            var method = typeof(Softwareschmiede.App.App).GetMethod("OnDispatcherUnhandledException", BindingFlags.NonPublic | BindingFlags.Static)!;
            method.Invoke(null, new object[] { this, args });

            args.Handled.Should().BeTrue("der Handler muss e.Handled = true setzen, damit der Shutdown ausbleibt");
            sink.Events.Should().Contain(e => e.Level == LogEventLevel.Error && e.Exception == exception);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    /// <summary>AppDomain.UnhandledException-Handler loggt die Exception.</summary>
    [Fact]
    public void UnhandledException_Handler_Logs()
    {
        var sink = new CollectingSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        try
        {
            var exception = new InvalidOperationException("Test-AppDomain-Fehler");
            var args = new UnhandledExceptionEventArgs(exception, isTerminating: true);

            var method = typeof(Softwareschmiede.App.App).GetMethod("OnAppDomainUnhandledException", BindingFlags.NonPublic | BindingFlags.Static)!;
            method.Invoke(null, new object[] { this, args });

            sink.Events.Should().Contain(e => e.Level == LogEventLevel.Error && e.Exception == exception);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    /// <summary>TaskScheduler.UnobservedTaskException-Handler loggt die Exception und ruft e.SetObserved() auf.</summary>
    [Fact]
    public void UnobservedTaskException_Handler_LogsAndSetsObserved()
    {
        var sink = new CollectingSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        try
        {
            var exception = new AggregateException(new InvalidOperationException("Test-Unobserved-Fehler"));
            var args = new UnobservedTaskExceptionEventArgs(exception);

            var method = typeof(Softwareschmiede.App.App).GetMethod("OnUnobservedTaskException", BindingFlags.NonPublic | BindingFlags.Static)!;
            method.Invoke(null, new object?[] { null, args });

            args.Observed.Should().BeTrue("der Handler muss e.SetObserved() aufrufen, damit der Prozess nicht beendet wird");
            sink.Events.Should().Contain(e => e.Level == LogEventLevel.Error && e.Exception == exception);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
