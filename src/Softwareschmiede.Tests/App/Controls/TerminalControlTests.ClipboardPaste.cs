using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für die Clipboard-Paste-Funktionalität (<c>Ctrl+V</c>) von <see cref="TerminalControl"/>:
/// Tastatur-Handling, Zwischenablage-Zugriff und Schreiben in den Input-Stream der Session.</summary>
public sealed partial class TerminalControlTests
{
    /// <summary>
    /// Setzt Text in die Windows-Zwischenablage mit Retry: Die Zwischenablage ist eine
    /// prozessübergreifende, systemweite Ressource ohne Locking-Schutz. Läuft dieses Testprojekt
    /// parallel zu vielen anderen Tests (voller Suite-Lauf), kann ein anderer Prozess/Thread sie
    /// transient belegen (<c>CLIPBRD_E_CANT_OPEN</c>). Der Produktivcode (<c>TerminalControl.
    /// GetClipboardText</c>) fängt einen solchen Fehler bewusst ab und liefert dann einen leeren
    /// String, was von einem echten "Zwischenablage ist leer" nicht unterscheidbar ist - ein
    /// isoliert laufender Test sieht dieses Timing-Fenster nie, ein Lauf der vollen Suite gelegentlich
    /// schon. Ein einzelner fehlgeschlagener Versuch ist daher kein echtes Testergebnis, sondern
    /// Rauschen; erst wenn auch mehrere Versuche mit Backoff fehlschlagen, wird die Exception
    /// weitergereicht.
    /// </summary>
    /// <param name="text">Der in die Zwischenablage zu schreibende Text.</param>
    /// <param name="maxAttempts">Maximale Anzahl an Versuchen, bevor eine Exception weitergereicht wird.</param>
    private static void SetClipboardTextWithRetry(string text, int maxAttempts = 10)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }

    /// <summary>Leert die Windows-Zwischenablage mit Retry - Begründung siehe <see cref="SetClipboardTextWithRetry"/>.</summary>
    /// <param name="maxAttempts">Maximale Anzahl an Versuchen, bevor eine Exception weitergereicht wird.</param>
    private static void ClearClipboardWithRetry(int maxAttempts = 10)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.Clear();
                return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }

    /// <summary>Drückt der Anwender <c>Ctrl+V</c>, muss das Tastaturereignis als behandelt markiert werden,
    /// damit es nicht an den bestehenden Tastatur-Encoder weitergereicht wird.</summary>
    [OsInterfaceFact]
    public void OnPreviewKeyDown_CtrlV_SetsHandledTrue()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;

            SetClipboardTextWithRetry("x");

            var args = InvokeCtrlV(control);

            args.Handled.Should().BeTrue("Ctrl+V muss das Tastaturereignis als behandelt markieren");
        });
    }

    /// <summary>Drückt der Anwender <c>Ctrl+V</c> bei vorhandenem Zwischenablage-Text, muss der Text kodiert
    /// und in den Input-Stream der Session geschrieben werden (Nachweis, dass <c>ReadClipboardAndInsertAsync</c>
    /// angestoßen wurde).</summary>
    [OsInterfaceFact]
    public void OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStream = new MemoryStream();
            using var session = CreateSession(inputStream, new ImmediateEofStream());
            control.Session = session;

            SetClipboardTextWithRetry("pasted");

            InvokeCtrlV(control);

            var expected = KeyToVt100Encoder.EncodeClipboardText("pasted");
            WaitForBytes(inputStream, expected.Length, TimeSpan.FromSeconds(5));

            inputStream.ToArray().Should().Equal(
                expected,
                "Ctrl+V muss ReadClipboardAndInsertAsync anstoßen, das den Zwischenablage-Text kodiert in den Input-Stream schreibt");
        });
    }

    /// <summary>Ein erfolgreicher Zwischenablage-Read schreibt die newline-normalisierten UTF-8-Bytes des
    /// Textes in den Input-Stream der Session.</summary>
    [OsInterfaceFact]
    public void ReadClipboardAndInsertAsync_Success_WritesEncodedBytesToInputStream()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStream = new MemoryStream();
            using var session = CreateSession(inputStream, new ImmediateEofStream());
            control.Session = session;

            SetClipboardTextWithRetry("Hi\nThere");

            InvokeReadClipboardAndInsertAsync(control);

            var expected = KeyToVt100Encoder.EncodeClipboardText("Hi\nThere");
            inputStream.ToArray().Should().Equal(expected);
        });
    }

    /// <summary>Ist die Zwischenablage leer, darf nichts in den Input-Stream geschrieben werden.</summary>
    [OsInterfaceFact]
    public void ReadClipboardAndInsertAsync_ClipboardEmpty_DoesNothing()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStream = new MemoryStream();
            using var session = CreateSession(inputStream, new ImmediateEofStream());
            control.Session = session;

            ClearClipboardWithRetry();

            InvokeReadClipboardAndInsertAsync(control);

            inputStream.ToArray().Should().BeEmpty("bei leerer Zwischenablage darf ReadClipboardAndInsertAsync keine Bytes schreiben");
        });
    }

    /// <summary>Schlägt das Schreiben in den Input-Stream während des Zwischenablage-Einfügens fehl, muss der
    /// Fehler über den Logger protokolliert werden, statt das Control zu beeinträchtigen.</summary>
    [OsInterfaceFact]
    public void ReadClipboardAndInsertAsync_ClipboardAccessThrows_LogsWarningAndContinues()
    {
        var loggerMock = new Mock<ILogger<TerminalControl>>();

        RunOnSta(() =>
        {
            var control = new TerminalControl();
            SetLogger(control, loggerMock.Object);
            using var session = CreateSession(new WriteThrowingStream(), new ImmediateEofStream());
            control.Session = session;

            SetClipboardTextWithRetry("paste-me");

            var act = () => InvokeReadClipboardAndInsertAsync(control);

            act.Should().NotThrow("ein Fehler beim Einfügen aus der Zwischenablage darf nicht propagieren");
        });

        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce(),
            "ein Fehler beim Zwischenablage-Einfügen muss geloggt werden statt still verworfen zu werden");
    }

    /// <summary>Nach erfolgreichem Schreiben in den Input-Stream muss <c>Session.MarkInputActivity()</c>
    /// aufgerufen werden, damit der Laufzeitstatus der CLI korrekt aktualisiert wird.</summary>
    [OsInterfaceFact]
    public void ReadClipboardAndInsertAsync_CallsMarkInputActivity()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var session = CreateSession(new MemoryStream(), new ImmediateEofStream());
            control.Session = session;

            SetClipboardTextWithRetry("x");

            InvokeReadClipboardAndInsertAsync(control);

            var field = typeof(PseudoConsoleSession).GetField("_lastInputUtc", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var lastInputUtc = (DateTimeOffset?)field.GetValue(session);

            lastInputUtc.Should().NotBeNull("ReadClipboardAndInsertAsync muss nach erfolgreichem Schreiben MarkInputActivity() aufrufen");
        });
    }

    /// <summary>Enthält die Zwischenablage Text, muss <c>GetClipboardText()</c> diesen unverändert zurückgeben.</summary>
    [OsInterfaceFact]
    public void GetClipboardText_ClipboardContainsText_ReturnsText()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            SetClipboardTextWithRetry("Zwischenablage-Inhalt");

            var result = InvokeGetClipboardText(control);

            result.Should().Be("Zwischenablage-Inhalt");
        });
    }

    /// <summary>Schlägt der Zwischenablage-Zugriff fehl (hier simuliert durch Aufruf von einem Nicht-STA-Thread,
    /// was einen echten Zugriffsfehler der WPF-Zwischenablage-API auslöst), muss <c>GetClipboardText()</c> einen
    /// Leerstring zurückgeben statt die Exception zu propagieren.</summary>
    [OsInterfaceFact]
    public void GetClipboardText_ClipboardAccessThrows_ReturnsEmptyString()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();

            string? result = null;
            var mtaThread = new Thread(() => result = InvokeGetClipboardText(control));
            mtaThread.SetApartmentState(ApartmentState.MTA);
            mtaThread.Start();
            mtaThread.Join();

            result.Should().Be(
                string.Empty,
                "ein Zwischenablage-Zugriffsfehler muss abgefangen werden und einen Leerstring liefern");
        });
    }

    private static void WaitForBytes(MemoryStream stream, int expectedLength, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (stream.Length < expectedLength && DateTime.UtcNow < deadline)
            Thread.Sleep(20);
    }

    private static KeyEventArgs InvokeCtrlV(TerminalControl control)
    {
        var method = typeof(TerminalControl).GetMethod("OnPreviewKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // KeyEventArgs erfordert eine nicht-null PresentationSource; ein reales (unsichtbares) HwndSource-Fenster
        // dient hier nur zur Erfüllung dieser Konstruktor-Anforderung, wird vom Control-Code nicht angesprochen.
        using var hwndSource = new HwndSource(new HwndSourceParameters("TerminalControlTests_ClipboardPaste"));
        var keyboard = new TestKeyboardDevice(Key.LeftCtrl);
        var args = new KeyEventArgs(keyboard, hwndSource, 0, Key.V)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };

        method.Invoke(control, [args]);

        return args;
    }

    private sealed class TestKeyboardDevice : KeyboardDevice
    {
        private readonly HashSet<Key> _downKeys;

        public TestKeyboardDevice(params Key[] downKeys)
            : base(InputManager.Current)
        {
            _downKeys = [.. downKeys];
        }

        protected override KeyStates GetKeyStatesFromSystem(Key key)
            => _downKeys.Contains(key) ? KeyStates.Down : KeyStates.None;
    }

    private static void InvokeReadClipboardAndInsertAsync(TerminalControl control)
    {
        var method = typeof(TerminalControl).GetMethod("ReadClipboardAndInsertAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(control, null)!;
        task.GetAwaiter().GetResult();
    }

    private static string InvokeGetClipboardText(TerminalControl control)
    {
        var method = typeof(TerminalControl).GetMethod("GetClipboardText", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (string)method.Invoke(control, null)!;
    }
}
