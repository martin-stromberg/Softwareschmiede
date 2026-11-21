using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Patterns;
using FlaUI.Core.WindowsAPI;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für das Konsolentestfenster (CLI-Replay-Dialog, Titel "Konsolentest").</summary>
public sealed class ConsoleTestDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public ConsoleTestDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Konsolentest";

    /// <summary>Wartet, bis das Konsolentestfenster erscheint (wird von außen über den
    /// "Konsolentestfenster öffnen"-Button der Einstellungen geöffnet).</summary>
    /// <returns>Diese Instanz.</returns>
    public override ConsoleTestDialogView ForceShow()
    {
        GetDialogWindow();
        return this;
    }

    /// <summary>Klickt "Aufzeichnung öffnen…" und bedient den nativen Öffnen-Dialog:
    /// setzt den Dateinamen per ValuePattern und ruft die Öffnen-Schaltfläche auf (deterministisch,
    /// unabhängig vom Tastaturfokus und vom Autocomplete-Dropdown). Erwartet eine existierende
    /// .clireplay-Datei.</summary>
    /// <param name="path">Der Pfad der zu ladenden .clireplay-Datei.</param>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView OpenRecording(string path)
    {
        OpenClickRecording();

        var openDialog = WaitForOpenDialog();
        openDialog.Focus();
        Keyboard.Type(path);

        // Das Dateiname-Feld des nativen Dialogs zeigt beim Tippen eine Autocomplete-Liste
        // (UIA: sichtbares Listen-Element "Dateiname:"). Sie darf nicht per Enter übernommen
        // werden (sonst wird ggf. ein MRU-Vorschlag statt des getippten Pfads geöffnet) —
        // also erst per ESC schließen und danach den getippten Pfad mit Enter bestätigen.
        // Ein Klick auf "Öffnen" kann in der überlagernden Liste landen, daher Tastatur-only.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Thread.Sleep(300);
            var dropdownOpen = openDialog
                .FindFirstDescendant(cf => cf.ByControlType(ControlType.List).And(cf.ByName("Dateiname:")))
                ?.IsOffscreen == false;
            Keyboard.Press(dropdownOpen ? VirtualKeyShort.ESCAPE : VirtualKeyShort.RETURN);

            try
            {
                ElementWaitHelper.WaitUntilProcessWindowGone(Window, OpenDialogCondition, TimeSpan.FromSeconds(8));
                return this;
            }
            catch (TimeoutException)
            {
            }
        }

        throw new TimeoutException("Der Öffnen-Dialog der .clireplay-Auswahl wurde nach dem Bestätigen nicht geschlossen.");
    }

    /// <summary>Klickt "Aufzeichnung öffnen…" und bricht den nativen Öffnen-Dialog per ESC ab
    /// (ViewModel-Zustand darf sich dadurch nicht ändern).</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView OpenRecordingCancel()
    {
        OpenClickRecording();

        var openDialog = WaitForOpenDialog();
        openDialog.Focus();
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        ElementWaitHelper.WaitUntilProcessWindowGone(Window, OpenDialogCondition, Medium);

        return this;
    }

    /// <returns><c>true</c>, wenn das Fehlerbanner (<c>ErrorMessage</c>) im Dialog sichtbar
    /// (nicht <c>IsOffscreen</c>) ist — das Banner-Element bleibt bei <c>Visibility=Collapsed</c>
    /// im Automation-Baum erhalten.</returns>
    public bool IsErrorVisible()
    {
        var element = GetDialogWindow().FindFirstDescendant(cf => cf.ByName("ErrorMessage"));
        try
        {
            return element is not null && !element.IsOffscreen;
        }
        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
        {
            return element is not null;
        }
    }

    /// <returns>Den aktuell im Fehlerbanner angezeigten Text (HelpText führt den Inhalt
    /// datengebunden mit, damit der volle Fehlertext auch bei gekürztem Name abrufbar ist),
    /// oder <c>null</c>, wenn kein Fehler sichtbar ist.</returns>
    public string? GetErrorMessage()
    {
        var element = GetDialogWindow().FindFirstDescendant(cf => cf.ByName("ErrorMessage"));
        if (element is null || element.IsOffscreen)
            return null;

        return GetHelpTextOrName(element);
    }

    /// <summary>Wartet, bis das Fehlerbanner sichtbar wird (das Laden der Datei läuft async —
    /// ein direkter Sichtbarkeits-Check unmittelbar nach dem Öffnen-Dialog wäre racy).</summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Fehlerbanner wurde nicht rechtzeitig sichtbar.</exception>
    public ConsoleTestDialogView WaitOnErrorVisible(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        while (DateTime.UtcNow < deadline)
        {
            if (IsErrorVisible())
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException("Das Fehlerbanner des Konsolentestfensters wurde nicht rechtzeitig sichtbar.");
    }

    /// <summary>Setzt die Zeitraffer-Schwelle (Sekunden als Text, z. B. "0" für maximale Geschwindigkeit).</summary>
    /// <param name="text">Der einzugebende Schwellenwert-Text.</param>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView SetTimelapseThreshold(string text)
    {
        var field = WaitForElement(GetDialogWindow(), cf => cf.ByName("TimeLapseThreshold"), Short);
        field.AsTextBox().Text = text;
        return this;
    }

    /// <summary>Klickt "Abspielen" (wartet zuvor, bis die Schaltfläche aktiv ist — WPF wertet
    /// CanExecute verzögert neu aus; ein Klick auf den noch deaktivierten Button ginge verloren,
    /// z. B. direkt nach „Wiedergabe beendet.").</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView StartReplay()
    {
        WaitForEnabledElement(GetDialogWindow(), "StartPlayback", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Neu starten" (wartet auf Aktivierung — die Schaltfläche ist nur
    /// während laufender bzw. pausierter Wiedergabe aktiv) und spielt die geladene
    /// Aufzeichnung sofort wieder ab Position 0 ab.</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView RestartReplay()
    {
        WaitForEnabledElement(GetDialogWindow(), "RestartPlayback", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Pausieren/Fortsetzen" (wartet auf Aktivierung — die Schaltfläche ist nur
    /// während laufender Wiedergabe aktiv).</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView PauseToggle()
    {
        WaitForEnabledElement(GetDialogWindow(), "PausePlayback", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Schritt vor" (wartet auf Aktivierung — die Schaltfläche ist nur bei
    /// geladener Aufzeichnung, nicht unpausiert laufender Wiedergabe und Position &lt; Ende aktiv)
    /// und wendet den nächsten aufgezeichneten Chunk als Einzelschritt an.</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView StepForward()
    {
        WaitForEnabledElement(GetDialogWindow(), "StepForward", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Schritt zurück" (wartet auf Aktivierung — die Schaltfläche ist nur bei
    /// geladener Aufzeichnung, nicht unpausiert laufender Wiedergabe und Position &gt; 0 aktiv)
    /// und stellt den Zustand vor dem zuletzt angewendeten Chunk wieder her.</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView StepBackward()
    {
        WaitForEnabledElement(GetDialogWindow(), "StepBack", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <returns><c>true</c>, wenn die Schaltfläche mit dem angegebenen Automation-Namen existiert
    /// und aktiviert ist — Nachweis der Rand-Deaktivierung (z. B. <c>StepBack</c> an
    /// Position 0 oder beide Schritt-Buttons während unpausierter Wiedergabe).</returns>
    /// <param name="automationName">Der Automation-Name der Schaltfläche.</param>
    public bool IsButtonEnabled(string automationName)
    {
        var element = GetDialogWindow().FindFirstDescendant(cf => cf.ByName(automationName));
        return element is not null && element.IsEnabled;
    }

    /// <returns>Den 0-basierten Index der selektierten Zeile der Quell-Chunk-Liste
    /// (Selektion = zuletzt angewendeter Chunk), oder <c>-1</c> bei leerer Selektion.</returns>
    public int GetSelectsSourceEntryIndex()
    {
        var list = WaitForElement(GetDialogWindow(), cf => cf.ByName("SourceChunkList"), Short);
        var lines = list.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)
            .Or(cf.ByControlType(ControlType.ListItem)));
        for (var i = 0; i < lines.Length; i++)
        {
            var item = lines[i].Patterns.SelectionItem.PatternOrDefault;
            if (item is not null && item.IsSelected.TryGetValue(out var selected) && selected)
                return i;
        }

        return -1;
    }

    /// <returns>Der aktuelle Wiedergabe-Statustext (z. B. "Wiedergabe beendet.") — gelesen aus dem
    /// HelpText, da der Automation-Name das statische Element-Kennzeichen "PlaybackStatus" ist.</returns>
    public string GetStatusText()
        => GetHelpTextOrName(WaitForElement(GetDialogWindow(), cf => cf.ByName("PlaybackStatus"), Short));

    /// <returns>Der aktuelle Positionstext (z. B. "Chunk 3/3") — gelesen aus dem HelpText,
    /// da der Automation-Name das statische Element-Kennzeichen "PlaybackPosition" ist.</returns>
    public string GetPositionText()
        => GetHelpTextOrName(WaitForElement(GetDialogWindow(), cf => cf.ByName("PlaybackPosition"), Short));

    /// <returns>Der aktuelle Geometrie-Text der geladenen Aufzeichnung (z. B. "Aufzeichnung: 220×50")
    /// — gelesen aus dem HelpText, da der Automation-Name das statische Element-Kennzeichen
    /// "RecordingGeometry" ist.</returns>
    public string GetGeometryText()
        => GetHelpTextOrName(WaitForElement(GetDialogWindow(), cf => cf.ByName("RecordingGeometry"), Short));

    /// <returns>Das <see cref="IScrollPattern"/> des "ReplayTerminalScrollViewer" (der ScrollViewer
    /// um das Replay-Terminal), oder <c>null</c>, wenn das Element das Pattern nicht unterstützt.</returns>
    public IScrollPattern? GetReplayScrollPattern()
        => WaitForElement(GetDialogWindow(), cf => cf.ByName("ReplayTerminalScrollViewer"), Short)
            .Patterns.Scroll.PatternOrDefault;

    /// <returns><c>true</c>, wenn der ReplayTerminal-ScrollViewer aktuell horizontal scrollbar ist
    /// (<c>HorizontallyScrollable</c> — die Buffer-Breite der Aufzeichnung übersteigt den sichtbaren
    /// Bereich), sonst <c>false</c>.</returns>
    public bool IsHorizontalScrollbar()
    {
        var pattern = GetReplayScrollPattern();
        return pattern is not null
            && pattern.HorizontallyScrollable.TryGetValue(out var scrollbar)
            && scrollbar;
    }

    /// <returns>Die horizontale View-Size des ReplayTerminal-ScrollViewers in Prozent des Extents
    /// (&lt; 100 = der Inhalt ist breiter als der sichtbare Bereich), oder <c>-1</c> ohne Pattern.</returns>
    public double GetHorizontalViewSize()
    {
        var pattern = GetReplayScrollPattern();
        return pattern is not null && pattern.HorizontalViewSize.TryGetValue(out var viewSize) ? viewSize : -1;
    }

    /// <returns>Der aktuelle horizontale Scroll-Stand des ReplayTerminal-ScrollViewers in Prozent,
    /// oder <c>-1</c> ohne Pattern.</returns>
    public double GetHorizontalScrollPercent()
    {
        var pattern = GetReplayScrollPattern();
        return pattern is not null && pattern.HorizontalScrollPercent.TryGetValue(out var percent) ? percent : -1;
    }

    /// <summary>Setzt die horizontale Scroll-Position des ReplayTerminal-ScrollViewers per
    /// ScrollPattern; die vertikale Achse bleibt unverändert (<see cref="ScrollPatternConstants.NoScroll"/>).</summary>
    /// <param name="percent">Der horizontale Ziel-Scrollstand in Prozent (0–100).</param>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView SetHorizontalScrollPercent(double percent)
    {
        GetReplayScrollPattern()?.SetScrollPercent(percent, ScrollPatternConstants.NoScroll);
        return this;
    }

    /// <summary>Klickt in das Replay-Terminal (ein echter Mausklick setzt über
    /// <c>TerminalControl.OnMouseDown</c> den Tastaturfokus auf das Control) und sendet
    /// anschließend die angegebene Taste über den realen Eingabepfad — so lässt sich das
    /// Tastatur-Scrollen des umschließenden ScrollViewers in der Wiedergabe nachweisen:
    /// Die Replay-Session besitzt keinen Eingabekanal, daher müssen Navigationstasten zum
    /// ScrollViewer durchbubbeln statt vom VT100-Encoder verschluckt zu werden.</summary>
    /// <param name="key">Die zu sendende Taste (z. B. <see cref="VirtualKeyShort.END"/>).</param>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView PressKeyInReplayTerminal(VirtualKeyShort key)
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("ReplayTerminal"), Short).ClickInForeground();
        Keyboard.Press(key);
        return this;
    }

    /// <summary>Markiert den angegebenen Zellbereich des echten Replay-Terminals über einen
    /// Mausdrag. Die Replay-Geometrie ist Teil der Aufzeichnung und macht die Zellabbildung
    /// unabhängig von der aktuellen Fenstergröße reproduzierbar.</summary>
    public ConsoleTestDialogView MarkReplayCells(int startRow, int startCol, int endRow, int endCol, int cols, int rows)
    {
        var terminal = WaitForElement(GetDialogWindow(), cf => cf.ByName("ReplayTerminal"), Short);
        var rect = terminal.BoundingRectangle;
        var cellWidth = rect.Width / cols;
        // Die Replay-Geometrie fixiert die Spalten, die verfügbare Dialoghöhe kann
        // aber mehr als die aufgezeichneten Zeilen zeigen. Höhe/rows würde daher
        // in eine zu tiefe Zeile klicken. Consolas 13 pt hat im TerminalControl
        // ein stabiles 3:2-Höhe/Breite-Verhältnis.
        var cellHeight = cellWidth * 1.5;
        var start = new System.Drawing.Point(
            (int)(rect.Left + (startCol + 0.5) * cellWidth),
            (int)(rect.Top + (startRow + 0.5) * cellHeight));
        var end = new System.Drawing.Point(
            (int)(rect.Left + (endCol + 0.5) * cellWidth),
            (int)(rect.Top + (endRow + 0.5) * cellHeight));

        Mouse.MoveTo(start);
        Mouse.Down(MouseButton.Left);
        Mouse.MoveTo(end);
        Mouse.Up(MouseButton.Left);
        return this;
    }

    /// <summary>Erfasst die Pixel des angegebenen Zellrechtecks im echten Replay-Terminal.
    /// Der Helfer dient dem E2E-Nachweis der gezeichneten Auswahl: UIA kann den Inhalt des
    /// Custom-Renderers nicht als TextPattern lesen, ein vorher/nachher Pixelvergleich aber
    /// sehr wohl die sichtbare Hervorhebung belegen.</summary>
    public byte[] ErfasseReplayZellPixel(int startRow, int startCol, int endRow, int endCol, int cols, int rows)
    {
        var terminal = WaitForElement(GetDialogWindow(), cf => cf.ByName("ReplayTerminal"), Short);
        using var screenshot = terminal.Capture();
        var cellWidth = screenshot.Width / (double)cols;
        var cellHeight = cellWidth * 1.5;
        var left = Math.Clamp((int)Math.Floor(startCol * cellWidth), 0, screenshot.Width - 1);
        var top = Math.Clamp((int)Math.Floor(startRow * cellHeight), 0, screenshot.Height - 1);
        var right = Math.Clamp((int)Math.Ceiling((endCol + 1) * cellWidth), left + 1, screenshot.Width);
        var bottom = Math.Clamp((int)Math.Ceiling((endRow + 1) * cellHeight), top + 1, screenshot.Height);
        var pixels = new byte[(right - left) * (bottom - top) * 3];
        var index = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var color = screenshot.GetPixel(x, y);
                pixels[index++] = color.R;
                pixels[index++] = color.G;
                pixels[index++] = color.B;
            }
        }

        return pixels;
    }

    /// <summary>Ermittelt die Zahl unterschiedlicher RGB-Kanäle zweier gleich großer
    /// Zell-Screenshots. Ein Schwellenwert statt eines exakten Bildvergleichs bleibt bei
    /// Cursor-Blinken robust, verlangt aber eine tatsächlich gezeichnete Fläche.</summary>
    public static int CountDeviatingReplayPixel(byte[] before, byte[] nachher)
    {
        var deviation = 0;
        // Das Dialoglayout kann sich zwischen zwei Aufnahmen um wenige Pixel
        // nachjustieren (z. B. nach der ersten Fokus-/Scrollbar-Aktualisierung).
        // Verglichen wird deshalb der gemeinsame Zell-Ausschnitt; die Aufnahmen
        // stammen beide aus identischen logischen Zellen.
        var commonLength = Math.Min(before.Length, nachher.Length) / 3 * 3;
        for (var index = 0; index < commonLength; index += 3)
            if (before[index] != nachher[index] || before[index + 1] != nachher[index + 1] || before[index + 2] != nachher[index + 2])
                deviation++;
        return deviation;
    }

    /// <summary>Setzt den UIA-Fokus auf das Replay-Terminal, ohne dessen Auswahl zu verändern,
    /// und sendet anschließend Strg+Umschalt+C. Screenshots und Clipboard-Zugriffe können den
    /// nativen Vordergrundsfokus vom Custom-Control wegnehmen.</summary>
    public ConsoleTestDialogView CopyReplaySelection()
    {
        // Capture() und die eigenständigen STA-Zugriffe auf die Zwischenablage können den
        // nativen Vordergrund-/Tastaturfokus vom WPF-Control wegnehmen. UIA.Focus stellt
        // den WPF-Fokus wieder her; die kurze Wartezeit lässt den Fokuswechsel im Dispatcher
        // ankommen, bevor SendInput die Tastenkombination an das Vordergrundfenster sendet.
        var terminal = WaitForElement(GetDialogWindow(), cf => cf.ByName("ReplayTerminal"), Short);
        terminal.Focus();
        Thread.Sleep(100);

        // Die vorangehenden Shift+Rechts-Inputs werden per SendInput ausgelöst. Ein explizites
        // Release verhindert, dass ein hängen gebliebener Modifier die exakt erwartete
        // Ctrl+Shift+C-Geste verfälscht. LCONTROL/LSHIFT bilden dabei denselben WPF-
        // Modifierzustand (Control | Shift) wie der reale linke Tastaturpfad.
        Keyboard.Release(VirtualKeyShort.LCONTROL);
        Keyboard.Release(VirtualKeyShort.RCONTROL);
        Keyboard.Release(VirtualKeyShort.LSHIFT);
        Keyboard.Release(VirtualKeyShort.RSHIFT);
        Keyboard.TypeSimultaneously(VirtualKeyShort.LCONTROL, VirtualKeyShort.LSHIFT, VirtualKeyShort.KEY_C);
        Thread.Sleep(100);
        return this;
    }

    /// <summary>Wartet, bis der ReplayTerminal-ScrollViewer horizontal scrollbar wird — das
    /// Extent-/Scrollbar-Layout entsteht asynchron nach dem Binden der Session.</summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Der ScrollViewer wurde nicht rechtzeitig horizontal scrollbar.</exception>
    public ConsoleTestDialogView WaitOnHorizontalScrollDue(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        while (DateTime.UtcNow < deadline)
        {
            if (IsHorizontalScrollbar())
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException("Der ReplayTerminalScrollViewer wurde nicht rechtzeitig horizontal scrollbar.");
    }

    /// <summary>Wartet, bis der Wiedergabe-Statustext den erwarteten Wert annimmt.</summary>
    /// <param name="expectedStatus">Der erwartete Statustext.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der Statustext innerhalb des Timeouts nicht erreicht wird.</exception>
    public ConsoleTestDialogView WaitOnStatus(string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        var last = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            last = GetStatusText();
            if (last == expectedStatus)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Konsolentest-Status wurde innerhalb des Timeouts nicht \"{expectedStatus}\" (zuletzt: \"{last}\").");
    }

    /// <summary>Wartet, bis der Wiedergabe-Positionstext den erwarteten Wert annimmt.</summary>
    /// <param name="expectedPosition">Der erwartete Positionstext (z. B. "Chunk 1/2").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der Positionstext innerhalb des Timeouts nicht erreicht wird.</exception>
    public ConsoleTestDialogView WaitOnPosition(string expectedPosition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        var last = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            last = GetPositionText();
            if (last == expectedPosition)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Konsolentest-Position wurde innerhalb des Timeouts nicht \"{expectedPosition}\" (zuletzt: \"{last}\").");
    }

    /// <summary>Wartet, bis die Quell-Chunk-Liste mindestens die angegebene Anzahl Einträge
    /// enthält (das Laden der Datei läuft async — ein direkter Count-Check wäre racy).</summary>
    /// <param name="minimumCount">Die mindestens erwartete Eintragsanzahl.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Mindestanzahl wurde nicht rechtzeitig erreicht.</exception>
    public ConsoleTestDialogView WaitOnSourceEntries(int minimumCount, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        var last = 0;
        while (DateTime.UtcNow < deadline)
        {
            last = GetSourceEntriesCount();
            if (last >= minimumCount)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Die Quell-Chunk-Liste enthielt innerhalb des Timeouts nicht mindestens {minimumCount} Einträge (zuletzt: {last}).");
    }

    /// <returns>Die Anzahl der in der Quell-Chunk-Liste dargestellten Einträge (GridView-Zeilen).</returns>
    public int GetSourceEntriesCount()
    {
        var list = WaitForElement(GetDialogWindow(), cf => cf.ByName("SourceChunkList"), Short);
        return list.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)
            .Or(cf.ByControlType(ControlType.ListItem))).Length;
    }

    /// <returns>Den Text eines Quell-Eintrags der Liste (Quelltext-Spalte = letzte Zelle der Zeile).</returns>
    /// <param name="index">Der 0-basierte Zeilenindex.</param>
    /// <returns>Der gerenderte Quelltext des Eintrags.</returns>
    public string GetSourceEntryText(int index)
    {
        var list = WaitForElement(GetDialogWindow(), cf => cf.ByName("SourceChunkList"), Short);
        var lines = list.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)
            .Or(cf.ByControlType(ControlType.ListItem)));
        if (index < 0 || index >= lines.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"Quell-Eintrag {index} existiert nicht ({lines.Length} Einträge vorhanden).");

        var cells = lines[index].FindAllChildren();
        return cells.Length == 0 ? string.Empty : cells[^1].Name;
    }

    /// <summary>Löst "Schließen" per Invoke-Pattern aus (koordinatenunabhängig — ein Mausklick
    /// kann verdeckt landen, wenn das nicht-modale Fenster hinter anderen Fenstern liegt) und
    /// wartet, bis das Fenster verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public ConsoleTestDialogView Close()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("CloseConsoleTest"), Short).AsButton().Invoke();
        ElementWaitHelper.WaitUntilProcessWindowGone(Window, DialogWindowCondition, Short);
        return this;
    }

    private void OpenClickRecording()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("OpenRecording"), Short).AsButton().ClickInForeground();
    }

    private AutomationElement WaitForOpenDialog()
    {
        var deadline = DateTime.UtcNow + Long;
        while (DateTime.UtcNow < deadline)
        {
            var dialog = ElementWaitHelper.FindInProcessWindows(Window, OpenDialogCondition);
            if (dialog is not null)
                return dialog;

            Thread.Sleep(200);
        }

        throw new TimeoutException("Der Öffnen-Dialog der .clireplay-Auswahl wurde nicht geöffnet.");
    }

    /// <summary>Wartet, bis ein Element existiert UND aktiviert ist (nötig für Buttons, deren
    /// CanExecute-Wert WPF verzögert neu auswertet — ein vorzeitiger Klick ginge verloren).</summary>
    /// <param name="parent">Das Elternelement.</param>
    /// <param name="automationName">Der Automation-Name des gesuchten Elements.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Das gefundene aktivierte Element.</returns>
    private static AutomationElement WaitForEnabledElement(AutomationElement parent, string automationName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var element = parent.FindFirstDescendant(cf => cf.ByName(automationName));
            if (element is not null && element.IsEnabled)
                return element;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Element '{automationName}' wurde nicht innerhalb von {timeout.TotalSeconds}s aktiviert gefunden.");
    }

    private static Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> OpenDialogCondition
        => cf => cf.ByControlType(ControlType.Window)
            .And(cf.ByName("Öffnen")
                .Or(cf.ByName("Open"))
                .Or(cf.ByName("CLI-Aufzeichnung öffnen")));
}
