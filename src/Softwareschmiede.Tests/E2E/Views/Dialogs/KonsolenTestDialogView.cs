using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für das Konsolentestfenster (CLI-Replay-Dialog, Titel "Konsolentest").</summary>
public sealed class KonsolenTestDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public KonsolenTestDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Konsolentest";

    /// <summary>Wartet, bis das Konsolentestfenster erscheint (wird von außen über den
    /// "Konsolentestfenster öffnen"-Button der Einstellungen geöffnet).</summary>
    /// <returns>Diese Instanz.</returns>
    public override KonsolenTestDialogView ForceShow()
    {
        GetDialogWindow();
        return this;
    }

    /// <summary>Klickt "Aufzeichnung öffnen…" und bedient den nativen Öffnen-Dialog:
    /// setzt den Dateinamen per ValuePattern und ruft die Öffnen-Schaltfläche auf (deterministisch,
    /// unabhängig vom Tastaturfokus und vom Autocomplete-Dropdown). Erwartet eine existierende
    /// .clireplay-Datei.</summary>
    /// <param name="pfad">Der Pfad der zu ladenden .clireplay-Datei.</param>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView OeffneAufzeichnung(string pfad)
    {
        KlickAufzeichnungOeffnen();

        var openDialog = WaitForOpenDialog();
        openDialog.Focus();
        Keyboard.Type(pfad);

        // Das Dateiname-Feld des nativen Dialogs zeigt beim Tippen eine Autocomplete-Liste
        // (UIA: sichtbares Listen-Element "Dateiname:"). Sie darf nicht per Enter übernommen
        // werden (sonst wird ggf. ein MRU-Vorschlag statt des getippten Pfads geöffnet) —
        // also erst per ESC schließen und danach den getippten Pfad mit Enter bestätigen.
        // Ein Klick auf "Öffnen" kann in der überlagernden Liste landen, daher Tastatur-only.
        for (var versuch = 0; versuch < 5; versuch++)
        {
            Thread.Sleep(300);
            var dropdownOffen = openDialog
                .FindFirstDescendant(cf => cf.ByControlType(ControlType.List).And(cf.ByName("Dateiname:")))
                ?.IsOffscreen == false;
            Keyboard.Press(dropdownOffen ? VirtualKeyShort.ESCAPE : VirtualKeyShort.RETURN);

            try
            {
                WaitUntilGone(Window.Automation.GetDesktop(), OpenDialogCondition, TimeSpan.FromSeconds(8));
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
    public KonsolenTestDialogView OeffneAufzeichnungAbbrechen()
    {
        KlickAufzeichnungOeffnen();

        var openDialog = WaitForOpenDialog();
        openDialog.Focus();
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        WaitUntilGone(Window.Automation.GetDesktop(), OpenDialogCondition, Medium);

        return this;
    }

    /// <returns><c>true</c>, wenn das Fehlerbanner (<c>FehlerMeldung</c>) im Dialog sichtbar
    /// (nicht <c>IsOffscreen</c>) ist — das Banner-Element bleibt bei <c>Visibility=Collapsed</c>
    /// im Automation-Baum erhalten.</returns>
    public bool IstFehlerSichtbar()
    {
        var element = GetDialogWindow().FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
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
    public string? GetFehlerMeldung()
    {
        var element = GetDialogWindow().FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        if (element is null || element.IsOffscreen)
            return null;

        return GetHelpTextOrName(element);
    }

    /// <summary>Wartet, bis das Fehlerbanner sichtbar wird (das Laden der Datei läuft async —
    /// ein direkter Sichtbarkeits-Check unmittelbar nach dem Öffnen-Dialog wäre racy).</summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Fehlerbanner wurde nicht rechtzeitig sichtbar.</exception>
    public KonsolenTestDialogView WarteAufFehlerSichtbar(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        while (DateTime.UtcNow < deadline)
        {
            if (IstFehlerSichtbar())
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException("Das Fehlerbanner des Konsolentestfensters wurde nicht rechtzeitig sichtbar.");
    }

    /// <summary>Setzt die Zeitraffer-Schwelle (Sekunden als Text, z. B. "0" für maximale Geschwindigkeit).</summary>
    /// <param name="text">Der einzugebende Schwellenwert-Text.</param>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView SetZeitrafferSchwelle(string text)
    {
        var feld = WaitForElement(GetDialogWindow(), cf => cf.ByName("ZeitrafferSchwelle"), Short);
        feld.AsTextBox().Text = text;
        return this;
    }

    /// <summary>Klickt "Abspielen" (wartet zuvor, bis die Schaltfläche aktiv ist — WPF wertet
    /// CanExecute verzögert neu aus; ein Klick auf den noch deaktivierten Button ginge verloren,
    /// z. B. direkt nach „Wiedergabe beendet.").</summary>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView StartWiedergabe()
    {
        WaitForEnabledElement(GetDialogWindow(), "WiedergabeStarten", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Neu starten" (wartet auf Aktivierung — die Schaltfläche ist nur
    /// während laufender bzw. pausierter Wiedergabe aktiv) und spielt die geladene
    /// Aufzeichnung sofort wieder ab Position 0 ab.</summary>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView NeustartWiedergabe()
    {
        WaitForEnabledElement(GetDialogWindow(), "WiedergabeNeustarten", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Pausieren/Fortsetzen" (wartet auf Aktivierung — die Schaltfläche ist nur
    /// während laufender Wiedergabe aktiv).</summary>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView PausierenToggle()
    {
        WaitForEnabledElement(GetDialogWindow(), "WiedergabePausieren", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Schritt vor" (wartet auf Aktivierung — die Schaltfläche ist nur bei
    /// geladener Aufzeichnung, nicht unpausiert laufender Wiedergabe und Position &lt; Ende aktiv)
    /// und wendet den nächsten aufgezeichneten Chunk als Einzelschritt an.</summary>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView SchrittVor()
    {
        WaitForEnabledElement(GetDialogWindow(), "SchrittVor", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Klickt "Schritt zurück" (wartet auf Aktivierung — die Schaltfläche ist nur bei
    /// geladener Aufzeichnung, nicht unpausiert laufender Wiedergabe und Position &gt; 0 aktiv)
    /// und stellt den Zustand vor dem zuletzt angewendeten Chunk wieder her.</summary>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView SchrittZurueck()
    {
        WaitForEnabledElement(GetDialogWindow(), "SchrittZurueck", Medium).AsButton().ClickInForeground();
        return this;
    }

    /// <returns><c>true</c>, wenn die Schaltfläche mit dem angegebenen Automation-Namen existiert
    /// und aktiviert ist — Nachweis der Rand-Deaktivierung (z. B. <c>SchrittZurueck</c> an
    /// Position 0 oder beide Schritt-Buttons während unpausierter Wiedergabe).</returns>
    /// <param name="automationName">Der Automation-Name der Schaltfläche.</param>
    public bool IstSchaltflaecheAktiviert(string automationName)
    {
        var element = GetDialogWindow().FindFirstDescendant(cf => cf.ByName(automationName));
        return element is not null && element.IsEnabled;
    }

    /// <returns>Den 0-basierten Index der selektierten Zeile der Quell-Chunk-Liste
    /// (Selektion = zuletzt angewendeter Chunk), oder <c>-1</c> bei leerer Selektion.</returns>
    public int GetSelektierterQuellEintragIndex()
    {
        var liste = WaitForElement(GetDialogWindow(), cf => cf.ByName("QuellChunkListe"), Short);
        var zeilen = liste.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)
            .Or(cf.ByControlType(ControlType.ListItem)));
        for (var i = 0; i < zeilen.Length; i++)
        {
            var item = zeilen[i].Patterns.SelectionItem.PatternOrDefault;
            if (item is not null && item.IsSelected.TryGetValue(out var selected) && selected)
                return i;
        }

        return -1;
    }

    /// <returns>Der aktuelle Wiedergabe-Statustext (z. B. "Wiedergabe beendet.") — gelesen aus dem
    /// HelpText, da der Automation-Name das statische Element-Kennzeichen "WiedergabeStatus" ist.</returns>
    public string GetStatusText()
        => GetHelpTextOrName(WaitForElement(GetDialogWindow(), cf => cf.ByName("WiedergabeStatus"), Short));

    /// <returns>Der aktuelle Positionstext (z. B. "Chunk 3/3") — gelesen aus dem HelpText,
    /// da der Automation-Name das statische Element-Kennzeichen "WiedergabePosition" ist.</returns>
    public string GetPositionsText()
        => GetHelpTextOrName(WaitForElement(GetDialogWindow(), cf => cf.ByName("WiedergabePosition"), Short));

    /// <summary>Wartet, bis der Wiedergabe-Statustext den erwarteten Wert annimmt.</summary>
    /// <param name="erwarteterStatus">Der erwartete Statustext.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der Statustext innerhalb des Timeouts nicht erreicht wird.</exception>
    public KonsolenTestDialogView WarteAufStatus(string erwarteterStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        var letzter = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            letzter = GetStatusText();
            if (letzter == erwarteterStatus)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Konsolentest-Status wurde innerhalb des Timeouts nicht \"{erwarteterStatus}\" (zuletzt: \"{letzter}\").");
    }

    /// <summary>Wartet, bis der Wiedergabe-Positionstext den erwarteten Wert annimmt.</summary>
    /// <param name="erwartetePosition">Der erwartete Positionstext (z. B. "Chunk 1/2").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der Positionstext innerhalb des Timeouts nicht erreicht wird.</exception>
    public KonsolenTestDialogView WarteAufPosition(string erwartetePosition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        var letzte = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            letzte = GetPositionsText();
            if (letzte == erwartetePosition)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Konsolentest-Position wurde innerhalb des Timeouts nicht \"{erwartetePosition}\" (zuletzt: \"{letzte}\").");
    }

    /// <summary>Wartet, bis die Quell-Chunk-Liste mindestens die angegebene Anzahl Einträge
    /// enthält (das Laden der Datei läuft async — ein direkter Count-Check wäre racy).</summary>
    /// <param name="mindestAnzahl">Die mindestens erwartete Eintragsanzahl.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Mindestanzahl wurde nicht rechtzeitig erreicht.</exception>
    public KonsolenTestDialogView WarteAufQuellEintraege(int mindestAnzahl, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Medium);
        var letzte = 0;
        while (DateTime.UtcNow < deadline)
        {
            letzte = GetQuellEintraegeCount();
            if (letzte >= mindestAnzahl)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Die Quell-Chunk-Liste enthielt innerhalb des Timeouts nicht mindestens {mindestAnzahl} Einträge (zuletzt: {letzte}).");
    }

    /// <returns>Die Anzahl der in der Quell-Chunk-Liste dargestellten Einträge (GridView-Zeilen).</returns>
    public int GetQuellEintraegeCount()
    {
        var liste = WaitForElement(GetDialogWindow(), cf => cf.ByName("QuellChunkListe"), Short);
        return liste.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)
            .Or(cf.ByControlType(ControlType.ListItem))).Length;
    }

    /// <returns>Den Text eines Quell-Eintrags der Liste (Quelltext-Spalte = letzte Zelle der Zeile).</returns>
    /// <param name="index">Der 0-basierte Zeilenindex.</param>
    /// <returns>Der gerenderte Quelltext des Eintrags.</returns>
    public string GetQuellEintragText(int index)
    {
        var liste = WaitForElement(GetDialogWindow(), cf => cf.ByName("QuellChunkListe"), Short);
        var zeilen = liste.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)
            .Or(cf.ByControlType(ControlType.ListItem)));
        if (index < 0 || index >= zeilen.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"Quell-Eintrag {index} existiert nicht ({zeilen.Length} Einträge vorhanden).");

        var zellen = zeilen[index].FindAllChildren();
        return zellen.Length == 0 ? string.Empty : zellen[^1].Name;
    }

    /// <summary>Löst "Schließen" per Invoke-Pattern aus (koordinatenunabhängig — ein Mausklick
    /// kann verdeckt landen, wenn das nicht-modale Fenster hinter anderen Fenstern liegt) und
    /// wartet, bis das Fenster verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public KonsolenTestDialogView Schliessen()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("KonsolenTestSchliessen"), Short).AsButton().Invoke();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
        return this;
    }

    private void KlickAufzeichnungOeffnen()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("AufzeichnungOeffnen"), Short).AsButton().ClickInForeground();
    }

    private AutomationElement WaitForOpenDialog()
    {
        var deadline = DateTime.UtcNow + Long;
        while (DateTime.UtcNow < deadline)
        {
            var dialog = Window.Automation.GetDesktop().FindFirstDescendant(OpenDialogCondition);
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
