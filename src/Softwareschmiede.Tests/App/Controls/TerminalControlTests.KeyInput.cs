using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FluentAssertions;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Infrastructure.Terminal;
using static Softwareschmiede.Tests.Helpers.WpfUnitTestHelpers;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für das Tastatur-Handling von <see cref="TerminalControl"/> jenseits der
/// Zwischenablage: Sessions ohne realen Eingabekanal (<see cref="TerminalReplaySession"/> mit
/// <see cref="Stream.Null"/>-Input) dürfen Navigationstasten nicht verschlucken — sie sollen zum
/// umschließenden ScrollViewer bubbeln (Tastatur-Scrolling in der Wiedergabe).</summary>
public sealed partial class TerminalControlTests
{
    /// <summary>Bei einer Session ohne Eingabekanal (Replay: <c>InputStream == Stream.Null</c>)
    /// müssen die Navigationstasten unbehandelt bleiben — nur so erreichen sie den umschließenden
    /// ScrollViewer, dessen Standard-Handling Line-/Page-/Home-/End-Scrollbefehle auslöst.</summary>
    [Fact]
    public void OnPreviewKeyDown_ReplaySession_NavigationstastenBleibenUnbehandelt()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var session = CreateReplaySession(220, 50, "X");
            control.Session = session;

            foreach (var taste in new[]
            {
                Key.Left, Key.Right, Key.Up, Key.Down,
                Key.PageUp, Key.PageDown, Key.Home, Key.End,
            })
            {
                var args = InvokePreviewKeyDown(control, taste);

                args.Handled.Should().BeFalse(
                    "die Navigationstaste {0} darf bei einer Session ohne Eingabekanal nicht verschluckt werden — sie muss zum ScrollViewer bubbeln",
                    taste);
            }
        });
    }

    /// <summary>Gegenprobe: bei einer Live-Session mit realem Eingabekanal werden
    /// Navigationstasten weiterhin VT100-kodiert an den Prozess weitergeleitet und das
    /// Ereignis als behandelt markiert.</summary>
    [Fact]
    public void OnPreviewKeyDown_LiveSession_NavigationstasteWirdAnSessionWeitergeleitet()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStream = new MemoryStream();
            using var session = CreateSession(inputStream, new ImmediateEofStream());
            control.Session = session;

            var args = InvokePreviewKeyDown(control, Key.Right);

            args.Handled.Should().BeTrue(
                "bei Live-Sessions werden Pfeiltasten VT100-kodiert an die Session weitergeleitet");
            WaitForBytes(inputStream, 3, TimeSpan.FromSeconds(5));
            inputStream.ToArray().Should().Equal(
                KeyToVt100Encoder.Encode(args),
                "die Pfeiltaste muss als VT100-Sequenz im Input-Stream der Session landen");
        });
    }

    /// <summary>Auch bei einer Session ohne Eingabekanal bleiben Nicht-Navigations-Tasten
    /// unverändert behandelt (das Ereignis geht weiterhin nicht an Vorgänger-Elemente).</summary>
    [Fact]
    public void OnPreviewKeyDown_ReplaySession_NichtNavigationsTastenBleibenBehandelt()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var session = CreateReplaySession(220, 50, "X");
            control.Session = session;

            var args = InvokePreviewKeyDown(control, Key.Enter);

            args.Handled.Should().BeTrue(
                "Nicht-Navigations-Tasten werden weiterhin verschluckt — nur Navigationstasten bubbeln zum ScrollViewer");
        });
    }

    private static KeyEventArgs InvokePreviewKeyDown(TerminalControl control, Key key)
    {
        var method = typeof(TerminalControl).GetMethod("OnPreviewKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // KeyEventArgs erfordert eine nicht-null PresentationSource; ein reales (unsichtbares) HwndSource-Fenster
        // dient hier nur zur Erfüllung dieser Konstruktor-Anforderung, wird vom Control-Code nicht angesprochen.
        using var hwndSource = new HwndSource(new HwndSourceParameters("TerminalControlTests_KeyInput"));
        var keyboard = new TestKeyboardDevice();
        var args = new KeyEventArgs(keyboard, hwndSource, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };

        method.Invoke(control, [args]);

        return args;
    }
}
