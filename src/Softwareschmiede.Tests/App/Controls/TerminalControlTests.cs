using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Tests.Helpers;
using Softwareschmiede.Infrastructure.Terminal;
using static Softwareschmiede.Tests.Helpers.WpfUnitTestHelpers;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für TerminalControl: Tastatureingabe-Fehlerbehandlung und BufferChanged-Bindung an
/// die Session (Issue-86, parallele CLI-Ausführungen — die Leseschleife läuft in <see cref="PseudoConsoleSession"/>,
/// TerminalControl ist reiner Renderer).</summary>
public sealed partial class TerminalControlTests
{
    /// <summary>
    /// Schreibt der Anwender Text, während die Pipe zum CLI-Prozess bereits geschlossen ist (z. B. weil der
    /// Prozess gerade beendet wurde), darf OnTextInput die dabei auftretende Exception nicht stillschweigend
    /// verwerfen, sondern muss sie über den injizierten Logger protokollieren.
    /// </summary>
    [Fact]
    public void OnTextInput_WriteThrows_LogsWarning()
    {
        var loggerMock = new Mock<ILogger<TerminalControl>>();

        RunOnSta(() =>
        {
            var control = new TerminalControl();
            SetLogger(control, loggerMock.Object);

            using var session = CreateSession(new WriteThrowingStream(), new ImmediateEofStream());
            control.Session = session;

            var textComposition = new TextComposition(InputManager.Current, control, "a");
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, textComposition)
            {
                RoutedEvent = TextCompositionManager.TextInputEvent,
            };

            var method = typeof(TerminalControl).GetMethod("OnTextInput", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var act = () => method.Invoke(control, new object[] { args });

            act.Should().NotThrow("ein Schreibfehler auf dem Terminal-Input-Stream darf nicht propagieren");
        });

        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce(),
            "ein Schreibfehler beim Terminal-Input muss geloggt werden statt still verworfen zu werden");
    }

    /// <summary>Setzt man <see cref="TerminalControl.Session"/>, muss ein Handler für
    /// <see cref="PseudoConsoleSession.BufferChanged"/> auf der neuen Session registriert werden, damit das
    /// Control auf neu eintreffende Ausgabe mit einer Neuzeichnung reagiert (beobachtet über die dabei am
    /// UI-Dispatcher angestoßene Operation).</summary>
    [Fact]
    public void OnSessionChanged_RegistersBufferChangedHandler()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var stream = new ControllableStream();
            using var session = CreateSession(stream);

            control.Session = session;

            var operationsPosted = RaiseOutputAndCountDispatcherOperations(session, stream, "X");

            operationsPosted.Should().BeGreaterThan(
                0,
                "OnSessionChanged muss einen BufferChanged-Handler auf der neuen Session registrieren, der bei neuer Ausgabe eine Neuzeichnung des Controls anstößt");
        });
    }

    /// <summary>Wechselt das Control von Session A zu Session B, muss der Handler auf A deregistriert und auf
    /// B registriert werden — sonst würde A das Control dauerhaft referenzieren (Memory-Leak) und weiterhin
    /// Neuzeichnungen anstoßen, obwohl B angezeigt wird.</summary>
    [Fact]
    public void OnSessionChanged_ToNewSession_DeregistersOldHandler()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var streamA = new ControllableStream();
            using var streamB = new ControllableStream();
            using var sessionA = CreateSession(streamA);
            using var sessionB = CreateSession(streamB);

            control.Session = sessionA;
            control.Session = sessionB;

            var operationsOnOldSession = RaiseOutputAndCountDispatcherOperations(sessionA, streamA, "A");
            operationsOnOldSession.Should().Be(
                0,
                "beim Wechsel zu einer neuen Session darf neue Ausgabe der alten Session keine Neuzeichnung des Controls mehr anstoßen");

            var operationsOnNewSession = RaiseOutputAndCountDispatcherOperations(sessionB, streamB, "B");
            operationsOnNewSession.Should().BeGreaterThan(
                0,
                "die neue Session muss weiterhin Neuzeichnungen des Controls anstoßen");
        });
    }

    /// <summary>Wird die Session auf <c>null</c> gesetzt (z. B. weil der CLI-Prozess gestoppt wurde), darf
    /// spätere Ausgabe der zuvor gebundenen Session das Control nicht mehr zu einer Neuzeichnung veranlassen.</summary>
    [Fact]
    public void OnSessionChanged_ToNull_DeregistersAllHandlers()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var stream = new ControllableStream();
            using var session = CreateSession(stream);

            control.Session = session;
            control.Session = null;

            var operationsPosted = RaiseOutputAndCountDispatcherOperations(session, stream, "X");

            operationsPosted.Should().Be(
                0,
                "wird die Session auf null gesetzt, darf spätere Ausgabe der alten Session keine Neuzeichnung des Controls mehr anstoßen");
        });
    }

    /// <summary>Zwei parallele Sessions mit unterschiedlicher Ausgabe dürfen sich nicht gegenseitig beeinflussen:
    /// Jede Sitzung besitzt ihren eigenen Buffer, der ausschließlich durch die eigene Leseschleife befüllt wird.</summary>
    [Fact]
    public void ParallelSessions_NoBufferInterference()
    {
        RunOnSta(() =>
        {
            var controlA = new TerminalControl();
            var controlB = new TerminalControl();
            using var sessionA = CreateSession(new FixedContentStream("AAA"));
            using var sessionB = CreateSession(new FixedContentStream("BBB"));

            var doneA = new TaskCompletionSource();
            var doneB = new TaskCompletionSource();
            sessionA.BufferChanged += (_, _) => doneA.TrySetResult();
            sessionB.BufferChanged += (_, _) => doneB.TrySetResult();

            controlA.Session = sessionA;
            controlB.Session = sessionB;

            Task.WhenAny(doneA.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            Task.WhenAny(doneB.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();

            sessionA.Buffer.GetRow(0)[0].Character.Should().Be('A', "Session A darf nur ihre eigene Ausgabe im Buffer haben");
            sessionB.Buffer.GetRow(0)[0].Character.Should().Be('B', "Session B darf nur ihre eigene Ausgabe im Buffer haben");
        });
    }

    /// <summary>Wechselt das Control von Session A zu Session B und zurück zu A, muss der Bufferinhalt von A
    /// unverändert erhalten geblieben sein — die Leseschleife von A lief währenddessen unabhängig weiter im
    /// Hintergrund und puffert Ausgabe, statt sie zu verlieren.</summary>
    [Fact]
    public void SessionSwitch_BackToPreviousSession_PreservesBuffer()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var sessionA = CreateSession(new FixedContentStream("HELLO"));
            using var sessionB = CreateSession(new ImmediateEofStream());

            var doneA = new TaskCompletionSource();
            sessionA.BufferChanged += (_, _) => doneA.TrySetResult();

            control.Session = sessionA;
            Task.WhenAny(doneA.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();

            var zeichenNachErstemRender = sessionA.Buffer.GetRow(0)[0].Character;

            control.Session = sessionB;
            control.Session = sessionA;

            sessionA.Buffer.GetRow(0)[0].Character.Should().Be(
                zeichenNachErstemRender,
                "der Bufferinhalt von Session A muss nach dem Wechsel zu B und zurück erhalten bleiben");
        });
    }

    /// <summary>Bei der Neuanbindung einer Session (Weg-/Zurücknavigation) wird der Buffer aus dem
    /// Replay-Puffer neu aufgebaut — Ausgabe, die während der Trennung eintraf, ist genau einmal
    /// vorhanden, Live-Chunks erscheinen nicht doppelt.</summary>
    [Fact]
    public void OnSessionChanged_ReattachedSession_KeineDoppelteAusgabe()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var stream = new ControllableStream();
            using var session = CreateSession(stream);

            control.Session = session;

            // "A" schieben, solange das Control gebunden ist.
            var aVerarbeitet = new TaskCompletionSource();
            session.BufferChanged += (_, _) => { if (BufferText(session).Contains('A')) aVerarbeitet.TrySetResult(); };
            stream.Push("A");
            Task.WhenAny(aVerarbeitet.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            aVerarbeitet.Task.IsCompletedSuccessfully.Should().BeTrue("die gebundene Ausgabe muss verarbeitet werden");

            // Trennen; währenddessen trifft "B" ein (nur im Buffer/Replay der Session, nicht am Control).
            control.Session = null;
            var bVerarbeitet = new TaskCompletionSource();
            session.BufferChanged += (_, _) => { if (BufferText(session).Contains('B')) bVerarbeitet.TrySetResult(); };
            stream.Push("B");
            Task.WhenAny(bVerarbeitet.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            bVerarbeitet.Task.IsCompletedSuccessfully.Should().BeTrue("die Ausgabe während der Trennung muss sessionseitig weiterlaufen");

            // Reattach: RebuildBufferFromReplay stellt "AB" wieder her — ohne Duplikat.
            control.Session = session;

            var text = BufferText(session);
            text.IndexOf('A').Should().Be(text.LastIndexOf('A'), "'A' darf nach dem Rebuild nicht doppelt erscheinen");
            text.IndexOf('B').Should().Be(text.LastIndexOf('B'), "'B' darf nach dem Rebuild nicht doppelt erscheinen");
            text.IndexOf('A').Should().BeLessThan(text.IndexOf('B'));
        });
    }

    /// <summary>Bei aktivem Alternate Screen meldet das Control kein Scrollback: Extent entspricht dem
    /// sichtbaren Grid, der Offset bleibt auf 0 geklemmt und Line-/Page-Scrollen sind No-Ops.</summary>
    [Fact]
    public void ScrollInfo_AlternateScreen_KeinScrollbackKeinOffset()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            WriteLines(session, 20);

            // Alternate Screen aktivieren (Vollbild-TUI): sichtbarer Bereich ohne Scrollback.
            session.Buffer.Apply(new Softwareschmiede.Domain.Terminal.AlternateScreenChangedEvent(true));
            WriteLines(session, 3);
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            scrollInfo.ExtentHeight.Should().Be(session.Buffer.Rows, "bei Alt-Screen entspricht die Extent dem sichtbaren Grid");
            scrollInfo.VerticalOffset.Should().Be(0);

            scrollInfo.SetVerticalOffset(5);
            scrollInfo.VerticalOffset.Should().Be(0, "SetVerticalOffset ist bei aktivem Alt-Screen ein No-Op");
            scrollInfo.LineUp();
            scrollInfo.LineDown();
            scrollInfo.PageUp();
            scrollInfo.PageDown();
            scrollInfo.VerticalOffset.Should().Be(0, "Scroll-Navigation ist bei aktivem Alt-Screen ein No-Op");
        });
    }

    private static string BufferText(PseudoConsoleSession session)
    {
        var sb = new System.Text.StringBuilder();
        for (var row = 0; row < session.Buffer.Rows; row++)
            foreach (var cell in session.Buffer.GetRow(row))
                sb.Append(cell.Character);
        return sb.ToString();
    }

    /// <summary>TerminalControl stellt dem umgebenden ScrollViewer zeilenbasierte Scrollinformationen bereit.</summary>
    [Fact]
    public void ScrollInfo_LangerVerlauf_MeldetExtentGroesserAlsViewport()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            WriteLines(session, 20);

            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            scrollInfo.ExtentHeight.Should().BeGreaterThan(scrollInfo.ViewportHeight);
            scrollInfo.VerticalOffset.Should().Be(scrollInfo.ScrollOwner?.ScrollableHeight ?? scrollInfo.ExtentHeight - scrollInfo.ViewportHeight);
        });
    }

    /// <summary>SetVerticalOffset sowie Line-/Page-Scrollen klemmen den Offset auf den gültigen Bereich.</summary>
    [Fact]
    public void ScrollInfo_SetVerticalOffsetUndNavigation_KlemmenOffset()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            WriteLines(session, 20);
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            var maxOffset = scrollInfo.ExtentHeight - scrollInfo.ViewportHeight;

            scrollInfo.SetVerticalOffset(-10);
            scrollInfo.VerticalOffset.Should().Be(0);

            scrollInfo.LineDown();
            scrollInfo.VerticalOffset.Should().Be(1);

            scrollInfo.PageDown();
            scrollInfo.VerticalOffset.Should().BeGreaterThan(1);

            scrollInfo.SetVerticalOffset(9999);
            scrollInfo.VerticalOffset.Should().Be(maxOffset);

            scrollInfo.LineUp();
            scrollInfo.VerticalOffset.Should().Be(maxOffset - 1);
        });
    }

    /// <summary>Wenn der Anwender am Ende steht, folgt der Scroll-Offset neuem Output automatisch ans neue Ende.</summary>
    [Fact]
    public void ScrollInfo_NeueAusgabeAmEnde_FolgtNeuemEnde()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            WriteLines(session, 8);
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            var oldOffset = scrollInfo.VerticalOffset;

            WriteLines(session, 8);
            InvokeUpdateScrollInfo(control);

            scrollInfo.VerticalOffset.Should().BeGreaterThan(oldOffset);
            scrollInfo.VerticalOffset.Should().Be(scrollInfo.ExtentHeight - scrollInfo.ViewportHeight);
        });
    }

    /// <summary>Scrollt der Anwender manuell nach oben, bleibt der Offset bei neuer Ausgabe erhalten.</summary>
    [Fact]
    public void ScrollInfo_ManuellNachOben_NeueAusgabeErhaeltOffset()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            WriteLines(session, 20);
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            scrollInfo.SetVerticalOffset(2);

            WriteLines(session, 5);
            InvokeUpdateScrollInfo(control);

            scrollInfo.VerticalOffset.Should().Be(2);
        });
    }

    /// <summary>Ein Sessionwechsel setzt den Scrollzustand wieder auf Follow-End.</summary>
    [Fact]
    public void OnSessionChanged_SetztScrollzustandAufEnde()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var sessionA = CreateSession(new ImmediateEofStream());
            using var sessionB = CreateSession(new ImmediateEofStream());

            control.Session = sessionA;
            WriteLines(sessionA, 20);
            InvokeUpdateScrollInfo(control);
            ((IScrollInfo)control).SetVerticalOffset(0);

            WriteLines(sessionB, 20);
            control.Session = sessionB;

            var scrollInfo = (IScrollInfo)control;
            scrollInfo.VerticalOffset.Should().Be(scrollInfo.ExtentHeight - scrollInfo.ViewportHeight);
        });
    }

    /// <summary>Im echten ScrollViewer-Layout mit CanContentScroll erhält das Terminal einen realen ScrollOwner
    /// und berechnet den Viewport aus der begrenzten sichtbaren Höhe statt aus dem 50-Zeilen-Fallback.</summary>
    [Fact]
    public void ScrollViewerLayout_CanContentScroll_BegrenztViewportAufSichtbareHoehe()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            var scrollViewer = new ScrollViewer
            {
                Content = control,
                CanContentScroll = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };

            var size = new Size(160, 64);
            scrollViewer.Measure(size);
            scrollViewer.Arrange(new Rect(size));
            scrollViewer.UpdateLayout();

            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            WriteLines(session, 20);
            scrollViewer.UpdateLayout();
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            scrollInfo.ScrollOwner.Should().BeSameAs(scrollViewer);
            control.ActualHeight.Should().BeApproximately(size.Height, 0.5);
            session.Buffer.Rows.Should().BeInRange(3, 5);
            scrollInfo.ViewportHeight.Should().Be(session.Buffer.Rows);
            scrollInfo.ExtentHeight.Should().BeGreaterThan(scrollInfo.ViewportHeight);
        });
    }

    /// <summary>Bei einer Session mit fixierter Geometrie (<see cref="ITerminalSession.SupportsResize"/>
    /// == <c>false</c>, z. B. <see cref="TerminalReplaySession"/>) darf das Control den Buffer beim
    /// Binden nicht auf die Control-Größe verkleinern — die Aufzeichnungs-Geometrie bleibt erhalten.</summary>
    [Fact]
    public void OnSessionChanged_FixierteGeometrie_BehaeltAufzeichnungsGroesse()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl(); // 160×48 px — deutlich kleiner als 220×50 Zellen
            using var session = CreateReplaySession(220, 50, "X");

            control.Session = session;

            session.Buffer.Cols.Should().Be(220,
                "ein fixierter Buffer darf beim Binden nicht auf die Control-Breite verkleinert werden");
            session.Buffer.Rows.Should().Be(50,
                "ein fixierter Buffer darf beim Binden nicht auf die Control-Höhe verkleinert werden");
        });
    }

    /// <summary>Bei fixierter Geometrie darf auch eine Größenänderung des Controls den Buffer nicht
    /// verkleinern; der horizontale Extent bildet weiterhin die volle Aufzeichnungsbreite ab.</summary>
    [Fact]
    public void OnRenderSizeChanged_FixierteGeometrie_ResizedNicht()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateReplaySession(220, 50, "X");
            control.Session = session;
            control.CanHorizontallyScroll = true;

            var scrollInfo = (IScrollInfo)control;
            var cellWidth = GetCellWidth(control);
            scrollInfo.ExtentWidth.Should().BeApproximately(220 * cellWidth, 0.01);

            // Re-Arrange auf eine andere Control-Größe — die Buffer-Geometrie bleibt fixiert.
            var neueGroesse = new Size(320, 96);
            control.Measure(neueGroesse);
            control.Arrange(new Rect(neueGroesse));

            session.Buffer.Cols.Should().Be(220);
            session.Buffer.Rows.Should().Be(50);
            scrollInfo.ExtentWidth.Should().BeApproximately(220 * cellWidth, 0.01,
                "der Extent bildet weiterhin die volle Aufzeichnungsbreite ab");
        });
    }

    /// <summary>Bei fixierter Geometrie bildet die horizontale <see cref="IScrollInfo"/>-Achse
    /// (Pixel-Einheiten) den Buffer-Extent ab: <see cref="IScrollInfo.SetHorizontalOffset"/> klemmt
    /// auf den ScrollableWidth-Bereich und die Line-/Page-/MouseWheel-Methoden verschieben den
    /// Offset erwartbar — ohne Einfluss auf die vertikale Achse.</summary>
    [Fact]
    public void ScrollInfo_FixierteGeometrie_HorizontalesScrollen()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateReplaySession(220, 50, "X");
            control.Session = session;
            control.CanHorizontallyScroll = true;
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            var cellWidth = GetCellWidth(control);
            var extentWidth = 220 * cellWidth;

            scrollInfo.ExtentWidth.Should().BeApproximately(extentWidth, 0.01);
            scrollInfo.ExtentWidth.Should().BeGreaterThan(scrollInfo.ViewportWidth,
                "ein 220-spaltiger Buffer ist breiter als der 160-px-Viewport");
            var maxOffset = scrollInfo.ExtentWidth - scrollInfo.ViewportWidth;
            var verticalOffsetVorher = scrollInfo.VerticalOffset;

            scrollInfo.SetHorizontalOffset(-10);
            scrollInfo.HorizontalOffset.Should().Be(0);

            scrollInfo.LineRight();
            scrollInfo.HorizontalOffset.Should().BeApproximately(cellWidth, 0.01,
                "LineRight verschiebt um eine Zelle");

            scrollInfo.PageRight();
            scrollInfo.HorizontalOffset.Should().BeApproximately(
                scrollInfo.ViewportWidth, 0.01, "PageRight verschiebt um Viewport minus eine Zelle");

            scrollInfo.MouseWheelRight();
            scrollInfo.HorizontalOffset.Should().BeApproximately(
                scrollInfo.ViewportWidth + 3 * cellWidth, 0.01,
                "MouseWheelRight verschiebt um drei Zellen");

            scrollInfo.SetHorizontalOffset(99999);
            scrollInfo.HorizontalOffset.Should().BeApproximately(maxOffset, 0.01,
                "der Offset wird auf den ScrollableWidth-Bereich geklemmt");

            scrollInfo.LineLeft();
            scrollInfo.HorizontalOffset.Should().BeApproximately(maxOffset - cellWidth, 0.01);

            scrollInfo.VerticalOffset.Should().Be(verticalOffsetVorher,
                "horizontales Scrollen darf die vertikale Achse nicht beeinflussen");

            // Ohne CanHorizontallyScroll des Hosts fällt der Extent auf den Viewport zurück —
            // und ein gesetzter Offset wird sofort (nicht erst beim nächsten UpdateScrollInfo)
            // auf den leeren Scrollbereich zurückgeklemmt.
            control.CanHorizontallyScroll = false;
            scrollInfo.ExtentWidth.Should().Be(scrollInfo.ViewportWidth);
            scrollInfo.HorizontalOffset.Should().Be(0,
                "bei deaktiviertem horizontalem Scrollen existiert kein Scrollbereich — der Offset muss sofort auf 0 fallen");

            // Zurückgeschaltet meldet der Extent wieder die volle Aufzeichnungsbreite.
            control.CanHorizontallyScroll = true;
            scrollInfo.ExtentWidth.Should().BeApproximately(extentWidth, 0.01);
        });
    }

    /// <summary>Ist der Viewport schmaler als eine Zelle (<see cref="IScrollInfo.ViewportWidth"/>
    /// &lt; Zellbreite, z. B. vor dem ersten Arrange mit <c>ActualWidth == 0</c>), muss die
    /// Seiten-Scrollweite auf eine Zelle geklemmt bleiben — ohne die Klemmung würde die Distanz
    /// negativ und PageLeft/PageRight scrollten in die falsche Richtung.</summary>
    [Fact]
    public void ScrollInfo_PageScrollen_SchmalerViewport_ScrollrichtungBleibtErhalten()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            // Viewport enger als eine Zelle: die ungeschützte Formel ViewportWidth - _cellWidth
            // würde hier ein negatives Seiten-Delta erzeugen (invertierte Scrollrichtung).
            var size = new Size(1, 48);
            control.Measure(size);
            control.Arrange(new Rect(size));

            using var session = CreateReplaySession(220, 50, "X");
            control.Session = session;
            control.CanHorizontallyScroll = true;
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            var cellWidth = GetCellWidth(control);
            scrollInfo.ViewportWidth.Should().BeLessThan(cellWidth,
                "der Test arrangiert absichtlich einen Viewport schmaler als eine Zelle");

            scrollInfo.SetHorizontalOffset(100);

            scrollInfo.PageRight();
            scrollInfo.HorizontalOffset.Should().BeGreaterThan(100,
                "PageRight muss auch bei winzigem Viewport nach rechts scrollen (Minimum: eine Zelle)");

            scrollInfo.PageLeft();
            scrollInfo.HorizontalOffset.Should().Be(100,
                "PageLeft muss den vorherigen PageRight exakt zurücknehmen (symmetrische Seitenweite)");
        });
    }

    /// <summary>Regressions-Nachweis für Live-Sessions (<see cref="ITerminalSession.SupportsResize"/>
    /// == <c>true</c>): die Buffer-Breite folgt per Floor-Arithmetik der Control-Breite — der
    /// Extent kann den Viewport dadurch nie übersteigen und es entsteht kein horizontaler
    /// Scrollbereich.</summary>
    [Fact]
    public void ScrollInfo_LiveSession_KeinHorizontalerScrollbereich()
    {
        RunOnSta(() =>
        {
            var control = CreateArrangedControl();
            using var session = CreateSession(new ImmediateEofStream());
            control.Session = session;
            control.CanHorizontallyScroll = true;
            InvokeUpdateScrollInfo(control);

            var scrollInfo = (IScrollInfo)control;
            scrollInfo.ExtentWidth.Should().BeLessThanOrEqualTo(scrollInfo.ViewportWidth,
                "bei Live-Sessions ist die Buffer-Breite konstruktionsbedingt <= Viewport");

            scrollInfo.SetHorizontalOffset(100);
            scrollInfo.HorizontalOffset.Should().Be(0,
                "ohne scrollbaren Extent bleibt der horizontale Offset auf 0 geklemmt");
        });
    }

    /// <summary>Schiebt <paramref name="content"/> in <paramref name="stream"/>, wartet auf die vollständige
    /// Verarbeitung durch die Leseschleife von <paramref name="session"/> und zählt dabei, wie viele Operationen
    /// währenddessen am aktuellen UI-Dispatcher angestoßen wurden. Ein gebundenes <c>TerminalControl</c> stößt bei
    /// jeder Verarbeitung eine Neuzeichnung über <c>Dispatcher.InvokeAsync</c> an; ohne registrierten Handler
    /// bleibt die Operationszahl bei 0.</summary>
    /// <param name="session">Die Sitzung, deren Ausgabeverarbeitung abgewartet wird.</param>
    /// <param name="stream">Der steuerbare Output-Stream der Sitzung, in den <paramref name="content"/> geschoben wird.</param>
    /// <param name="content">Der zu schiebende Ausgabeinhalt.</param>
    /// <returns>Die Anzahl der während der Verarbeitung am UI-Dispatcher angestoßenen Operationen.</returns>
    private static int RaiseOutputAndCountDispatcherOperations(PseudoConsoleSession session, ControllableStream stream, string content)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var operationsPosted = 0;
        void OnOperationPosted(object? sender, DispatcherHookEventArgs e) => operationsPosted++;

        var processed = new TaskCompletionSource();
        void OnBufferChanged(object? sender, EventArgs e) => processed.TrySetResult();
        session.BufferChanged += OnBufferChanged;

        dispatcher.Hooks.OperationPosted += OnOperationPosted;
        try
        {
            stream.Push(content);
            Task.WhenAny(processed.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        }
        finally
        {
            dispatcher.Hooks.OperationPosted -= OnOperationPosted;
            session.BufferChanged -= OnBufferChanged;
        }

        return operationsPosted;
    }

    private static void SetLogger(TerminalControl control, ILogger<TerminalControl> logger)
    {
        var field = typeof(TerminalControl).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(control, logger);
    }

    private static TerminalControl CreateArrangedControl()
    {
        var control = new TerminalControl();
        var size = new Size(160, 48);
        control.Measure(size);
        control.Arrange(new Rect(size));
        return control;
    }

    private static void InvokeUpdateScrollInfo(TerminalControl control)
    {
        var method = typeof(TerminalControl).GetMethod("UpdateScrollInfo", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(control, [true]);
    }

    private static void WriteLines(PseudoConsoleSession session, int count)
    {
        for (var i = 0; i < count; i++)
            session.Buffer.Apply(new Softwareschmiede.Domain.Terminal.TextWrittenEvent($"{i}\n"));
    }

    private static PseudoConsoleSession CreateSession(Stream outputStream)
        => CreateSession(new MemoryStream(), outputStream);

    private static PseudoConsoleSession CreateSession(Stream inputStream, Stream outputStream)
    {
        return TestPseudoConsoleSessionFactory.Create(inputStream, outputStream);
    }

    /// <summary>Erstellt eine <see cref="TerminalReplaySession"/> (fixierte Geometrie,
    /// <see cref="ITerminalSession.SupportsResize"/> == <c>false</c>) in der angegebenen
    /// Aufzeichnungs-Geometrie — das Gegenstück zu <see cref="CreateSession(Stream)"/> für
    /// Live-Sessions.</summary>
    private static TerminalReplaySession CreateReplaySession(int cols, int rows, params string[] chunkInhalte)
    {
        var chunks = chunkInhalte
            .Select((text, i) => new CliOutputChunkRecord(
                TimeSpan.FromMilliseconds(i * 10),
                System.Text.Encoding.UTF8.GetBytes(text)))
            .ToArray();
        return new TerminalReplaySession(
            new CliOutputAufzeichnung
            {
                AufgabeId = Guid.NewGuid(),
                PluginName = "TestPlugin",
                StartUtc = DateTimeOffset.UtcNow,
                Cols = cols,
                Rows = rows,
                IstVollstaendig = true,
                Chunks = chunks,
            },
            new FakeTimeProvider());
    }

    private static double GetCellWidth(TerminalControl control)
    {
        var field = typeof(TerminalControl).GetField("_cellWidth", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (double)field.GetValue(control)!;
    }

    /// <summary>Stream, der beim Lesevorgang sofort 0 Bytes liefert (simuliertes Stream-Ende), ohne die Dispatcher-Pumpe zu benötigen.</summary>
    private sealed class ImmediateEofStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(0);

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream, der beim ersten Lesevorgang einen festen Inhalt liefert und danach das Stream-Ende (0 Bytes) meldet.</summary>
    private sealed class FixedContentStream : Stream
    {
        private readonly byte[] _content;
        private bool _served;

        public FixedContentStream(string content)
        {
            _content = System.Text.Encoding.ASCII.GetBytes(content);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_served)
                return new ValueTask<int>(0);

            _served = true;
            _content.CopyTo(buffer);
            return new ValueTask<int>(_content.Length);
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream, der beim Schreibvorgang eine Exception wirft (z. B. bereits geschlossene Pipe zum CLI-Prozess).</summary>
    private sealed class WriteThrowingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
            => throw new IOException("Simulierter Schreibfehler des Terminal-Input-Streams");

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>Stream, dessen Lesevorgang blockiert, bis über <see cref="Push"/> Inhalt bereitgestellt wird.
    /// Simuliert einen CLI-Prozess, der zu einem beliebigen späteren Zeitpunkt neue Ausgabe produziert — auch
    /// nachdem ein Control die gebundene Session bereits gewechselt hat.</summary>
    private sealed class ControllableStream : Stream
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        /// <summary>Stellt <paramref name="content"/> für den nächsten Lesevorgang bereit.</summary>
        /// <param name="content">Der bereitzustellende Ausgabeinhalt.</param>
        public void Push(string content)
        {
            _queue.Enqueue(System.Text.Encoding.ASCII.GetBytes(content));
            _signal.Release();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _signal.WaitAsync(cancellationToken);
            if (_queue.TryDequeue(out var data))
            {
                data.CopyTo(buffer);
                return data.Length;
            }

            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _signal.Dispose();
            base.Dispose(disposing);
        }
    }
}
