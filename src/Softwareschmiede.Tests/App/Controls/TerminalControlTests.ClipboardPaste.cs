using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Infrastructure.Terminal;
using static Softwareschmiede.Tests.Helpers.WpfUnitTestHelpers;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für die Clipboard-Paste-Funktionalität (<c>Ctrl+V</c> und <c>Ctrl+Shift+V</c>) von <see cref="TerminalControl"/>:
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

                // Ein erfolgreiches SetText bedeutet noch nicht, dass die systemweite
                // Zwischenablage für den unmittelbar folgenden Produktaufruf wieder
                // lesbar ist. Erst der Read-back bestätigt eine stabile Testvorbedingung.
                if (System.Windows.Clipboard.ContainsText()
                    && System.Windows.Clipboard.GetText() == text)
                    return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
            }

            if (attempt < maxAttempts)
                Thread.Sleep(100 * attempt);
        }

        throw new InvalidOperationException("Die Windows-Zwischenablage konnte nicht stabil mit dem erwarteten Text vorbereitet werden.");
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

    /// <summary>Ctrl+Shift+V ist neben Ctrl+V ein Paste-Shortcut und darf deshalb nicht zum
    /// Tastatur-Encoder weitergereicht werden.</summary>
    [OsInterfaceFact]
    public void OnPreviewKeyDown_CtrlShiftV_SetsHandledTrue()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;

            var args = InvokeCtrlShiftV(control);

            args.Handled.Should().BeTrue("Ctrl+Shift+V muss das Tastaturereignis als Paste behandeln");
        });
    }

    /// <summary>Ctrl+Shift+V muss denselben Clipboard-Pastepfad wie Ctrl+V auslösen.</summary>
    [OsInterfaceFact]
    public void OnPreviewKeyDown_CtrlShiftV_CallsReadClipboardAndInsertAsync()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStream = new MemoryStream();
            using var session = CreateSession(inputStream, new ImmediateEofStream());
            control.Session = session;
            SetClipboardTextWithRetry("shift-pasted");

            InvokeCtrlShiftV(control);

            var expected = KeyToVt100Encoder.EncodeClipboardText("shift-pasted");
            WaitForBytes(inputStream, expected.Length, TimeSpan.FromSeconds(5));
            inputStream.ToArray().Should().Equal(
                expected,
                "Ctrl+Shift+V muss ReadClipboardAndInsertAsync auslösen und den Zwischenablage-Text in den Input-Stream schreiben");
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

            var text = "Hi\nThere";
            var expected = KeyToVt100Encoder.EncodeClipboardText("Hi\nThere");

            SetClipboardTextWithRetry(text);
            InvokeReadClipboardAndInsertAsync(control);

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
            GetLastInputActivity(session).Should().BeNull("eine leere Zwischenablage darf keine Eingabeaktivität vortäuschen");
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
            GetLastInputActivity(session).Should().BeNull("ein fehlgeschlagenes Schreiben darf keine Eingabeaktivität melden");
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

            GetLastInputActivity(session).Should().NotBeNull("ReadClipboardAndInsertAsync muss nach erfolgreichem Schreiben MarkInputActivity() aufrufen");
        });
    }

    /// <summary>Ein langer mehrzeiliger Clipboard-Inhalt muss vollständig und mit normalisierten Zeilenumbrüchen
    /// in der Session ankommen.</summary>
    [OsInterfaceFact]
    public void ReadClipboardAndInsertAsync_LangerMehrzeiligerText_WritesCompleteEncodedBytes()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStream = new MemoryStream();
            using var session = CreateSession(inputStream, new ImmediateEofStream());
            control.Session = session;
            var text = CreateStacktraceLikeText(120);

            var expected = KeyToVt100Encoder.EncodeClipboardText(text);

            SetClipboardTextWithRetry(text);
            InvokeReadClipboardAndInsertAsync(control);

            inputStream.ToArray().Should().Equal(expected);
        });
    }

    /// <summary>Ein laufender Paste schreibt in die beim Start übergebene Session, auch wenn das Control später
    /// eine andere Session anzeigt.</summary>
    [OsInterfaceFact]
    public void ReadClipboardAndInsertAsync_SessionWechseltWaerendPaste_SchreibtInSnapshotSession()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var inputStreamA = new MemoryStream();
            var inputStreamB = new MemoryStream();
            using var sessionA = CreateSession(inputStreamA, new ImmediateEofStream());
            using var sessionB = CreateSession(inputStreamB, new ImmediateEofStream());
            control.Session = sessionA;
            SetClipboardTextWithRetry("snapshot paste");

            control.Session = sessionB;
            InvokeReadClipboardAndInsertAsync(control, sessionA);

            inputStreamA.ToArray().Should().Equal(KeyToVt100Encoder.EncodeClipboardText("snapshot paste"));
            inputStreamB.ToArray().Should().BeEmpty();
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
        => InvokePasteShortcut(control, Key.LeftCtrl);

    private static KeyEventArgs InvokeCtrlShiftV(TerminalControl control)
        => InvokePasteShortcut(control, Key.LeftCtrl, Key.LeftShift);

    private static KeyEventArgs InvokePasteShortcut(TerminalControl control, params Key[] pressedKeys)
    {
        var method = typeof(TerminalControl).GetMethod("OnPreviewKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // KeyEventArgs erfordert eine nicht-null PresentationSource; ein reales (unsichtbares) HwndSource-Fenster
        // dient hier nur zur Erfüllung dieser Konstruktor-Anforderung, wird vom Control-Code nicht angesprochen.
        using var hwndSource = new HwndSource(new HwndSourceParameters("TerminalControlTests_ClipboardPaste"));
        var keyboard = new TestKeyboardDevice(pressedKeys);
        var args = new KeyEventArgs(keyboard, hwndSource, 0, Key.V)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };

        method.Invoke(control, [args]);

        return args;
    }

    private static void InvokeReadClipboardAndInsertAsync(TerminalControl control)
        => InvokeReadClipboardAndInsertAsync(control, control.Session!);

    private static void InvokeReadClipboardAndInsertAsync(TerminalControl control, ITerminalSession session)
    {
        var method = typeof(TerminalControl).GetMethod(
            "ReadClipboardAndInsertAsync",
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(ITerminalSession)],
            null)!;
        var task = (Task)method.Invoke(control, [session])!;
        task.GetAwaiter().GetResult();
    }

    private static DateTimeOffset? GetLastInputActivity(PseudoConsoleSession session)
    {
        var field = typeof(PseudoConsoleSession).GetField("_lastInputUtc", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (DateTimeOffset?)field.GetValue(session);
    }

    private static string InvokeGetClipboardText(TerminalControl control)
    {
        var method = typeof(TerminalControl).GetMethod("GetClipboardText", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (string)method.Invoke(control, null)!;
    }

    private static string CreateStacktraceLikeText(int lineCount)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < lineCount; i++)
        {
            builder.Append("   at Softwareschmiede.App.Controls.TerminalControl.Generic`1[[System.String]].RenderIntoBatch(RenderBatchBuilder batchBuilder, RenderFragment renderFragment, Exception& renderFragmentException) in C:\\Repos\\Projekt\\Controls\\TerminalControl.cs:line ");
            builder.Append(200 + i);
            builder.Append('\n');
        }

        builder.Append("ÄÖÜ äöü [] <> () {} ` end");
        return builder.ToString();
    }
}
