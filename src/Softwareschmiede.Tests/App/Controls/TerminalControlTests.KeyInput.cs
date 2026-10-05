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
    /// <summary>Ctrl+Shift+C kopiert die aktive Auswahl, ohne ein ETX oder andere Bytes an
    /// den Prozess zu senden.</summary>
    [OsInterfaceFact]
    public void OnPreviewKeyDown_CtrlShiftC_CopiesSelectionWithoutCliInput()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var input = new MemoryStream();
            using var session = CreateSession(input, new ImmediateEofStream());
            control.Session = session;
            session.Buffer.Apply(new Softwareschmiede.Domain.Terminal.TextWrittenEvent("copy"));
            session.Buffer.Apply(new Softwareschmiede.Domain.Terminal.CursorMovedEvent(0, 4, true));
            InvokeExtendSelection(control, Key.Home);

            InvokePreviewKeyDown(control, Key.C, Key.LeftCtrl, Key.LeftShift);

            System.Windows.Clipboard.GetText().Should().Be("copy");
            input.ToArray().Should().BeEmpty();
        });
    }

    /// <summary>Ohne Auswahl bleibt die Zwischenablage unverändert und Ctrl+Shift+C erreicht
    /// den CLI-Input nicht.</summary>
    [OsInterfaceFact]
    public void OnPreviewKeyDown_CtrlShiftCOhneAuswahl_LaesstClipboardUndCliInputUnveraendert()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var input = new MemoryStream();
            using var session = CreateSession(input, new ImmediateEofStream());
            control.Session = session;
            SetClipboardTextWithRetry("unverändert");

            InvokePreviewKeyDown(control, Key.C, Key.LeftCtrl, Key.LeftShift);

            System.Windows.Clipboard.GetText().Should().Be("unverändert");
            input.ToArray().Should().BeEmpty();
        });
    }

    /// <summary>Steht der Cursor nach einem Zeichen in der letzten Spalte hinter dem Grid,
    /// muss die erste Shift-Vertikalnavigation die Ausgangsspalte klemmen statt eine
    /// ungültige Zellversionsadresse zu lesen.</summary>
    [Fact]
    public void ExtendSelection_ShiftUpMitCursorHinterLetzterSpalte_WirftNicht()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var buffer = new Softwareschmiede.Domain.Terminal.TerminalBuffer(3, 2);
            buffer.Apply(new Softwareschmiede.Domain.Terminal.TextWrittenEvent("ABC"));
            SetBufferForSelectionTest(control, buffer);

            var action = () => InvokeExtendSelection(control, Key.Up);

            action.Should().NotThrow("Shift+Up muss den Cursor hinter der letzten Spalte auf eine echte Zelle klemmen");
        });
    }

    /// <summary>Die erste Shift-Navigation verwendet die Ausgangs-Cursorzelle als Anker,
    /// damit Shift+Home den gesamten Bereich bis zum Zeilenanfang markiert.</summary>
    [Fact]
    public void ExtendSelection_ErsteShiftHomeNavigation_BehaeltCursorAlsAnker()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var buffer = new Softwareschmiede.Domain.Terminal.TerminalBuffer(10, 1);
            buffer.Apply(new Softwareschmiede.Domain.Terminal.CursorMovedEvent(0, 6, true));
            SetBufferForSelectionTest(control, buffer);

            InvokeExtendSelection(control, Key.Home);

            var selection = typeof(TerminalControl).GetField("_selection", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
            var anchor = selection.GetType().GetProperty("Anchor")!.GetValue(selection)!;
            var end = selection.GetType().GetProperty("End")!.GetValue(selection)!;
            ((int)anchor.GetType().GetProperty("Column")!.GetValue(anchor)!).Should().Be(6);
            ((int)end.GetType().GetProperty("Column")!.GetValue(end)!).Should().Be(0);
        });
    }

    /// <summary>Normale Ausgabe darf eine Auswahl nicht verlieren, wenn die markierte
    /// logische Zeile beim Scrollen nur ihre sichtbare Position wechselt.</summary>
    [Fact]
    public void Selection_NormalScroll_KeepsLogicalRowAndCopyText()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var buffer = new Softwareschmiede.Domain.Terminal.TerminalBuffer(12, 2);
            // Tatsächliche Terminal-Zeilenumbrüche sind nötig: Ein literales "\\n"
            // bleibt in derselben Zellezeile und kann keinen Scrollback-Fall erzeugen.
            buffer.Apply(new Softwareschmiede.Domain.Terminal.TextWrittenEvent("erste\nzweite"));
            SetBufferForSelectionTest(control, buffer);

            // Cursor steht hinter "zweite"; Shift+Home markiert genau diese Zeile.
            InvokeExtendSelection(control, Key.Home);
            buffer.Apply(new Softwareschmiede.Domain.Terminal.TextWrittenEvent("\n"));

            typeof(TerminalControl).GetMethod("ValidateSelection", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(control, null);
            typeof(TerminalControl).GetField("_selection", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(control).Should().NotBeNull("die vollständige logische Zeile wurde nur verschoben");

            SetClipboardTextWithRetry("vorher");
            typeof(TerminalControl).GetMethod("CopySelectionToClipboard", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(control, null);
            System.Windows.Clipboard.GetText().Should().Be("zweite");
        });
    }
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

    private static KeyEventArgs InvokePreviewKeyDown(TerminalControl control, Key key, params Key[] pressedKeys)
    {
        var method = typeof(TerminalControl).GetMethod("OnPreviewKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // KeyEventArgs erfordert eine nicht-null PresentationSource; ein reales (unsichtbares) HwndSource-Fenster
        // dient hier nur zur Erfüllung dieser Konstruktor-Anforderung, wird vom Control-Code nicht angesprochen.
        using var hwndSource = new HwndSource(new HwndSourceParameters("TerminalControlTests_KeyInput"));
        var keyboard = new TestKeyboardDevice(pressedKeys);
        var args = new KeyEventArgs(keyboard, hwndSource, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };

        method.Invoke(control, [args]);

        return args;
    }

    private static void SetBufferForSelectionTest(TerminalControl control, Softwareschmiede.Domain.Terminal.TerminalBuffer buffer)
        => typeof(TerminalControl).GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(control, buffer);

    private static void InvokeExtendSelection(TerminalControl control, Key key)
        => typeof(TerminalControl).GetMethod("ExtendSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [key]);
}
