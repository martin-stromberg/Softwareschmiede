using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System.Globalization;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den modalen „Pause einstellen"-Dialog der Aufgabendetailansicht (Issue 151).</summary>
public sealed class AufgabePausierenDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung (nicht das Dialogfenster selbst).</param>
    public AufgabePausierenDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Pause einstellen";

    /// <summary>Öffnet den Dialog über den Ribbon-Button „Pause einstellen" der geöffneten Aufgabendetailansicht.</summary>
    /// <returns>Diese Instanz, sobald das Dialogfenster sichtbar ist.</returns>
    public override AufgabePausierenDialogView ForceShow()
    {
        WaitForElement(Window, cf => cf.ByName("PauseEinstellen"), Medium).AsButton().Click();
        GetDialogWindow(Medium);
        return this;
    }

    /// <returns>Die im Dialog vorbelegte Uhrzeit (Stunde, Minute).</returns>
    public (int Stunde, int Minute) GetVorbelegteZeit()
    {
        var dialog = GetDialogWindow();
        var stunde = WaitForElement(dialog, cf => cf.ByName("PausierenStunde"), Short).AsTextBox().Text;
        var minute = WaitForElement(dialog, cf => cf.ByName("PausierenMinute"), Short).AsTextBox().Text;
        return (int.Parse(stunde, CultureInfo.InvariantCulture), int.Parse(minute, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Trägt Datum, Stunde und Minute des angegebenen lokalen Zielzeitpunkts ein. Das Datum wird in das
    /// DatePicker-Textfeld getippt (lokales Kurzdatumsformat) und per Tab-Taste committed.
    /// </summary>
    /// <param name="ziel">Der lokale Zielzeitpunkt (Datum + Uhrzeit).</param>
    /// <returns>Diese Instanz.</returns>
    public AufgabePausierenDialogView SetzeZeitpunkt(DateTime ziel)
    {
        var dialog = GetDialogWindow();

        var datumPicker = WaitForElement(dialog, cf => cf.ByName("PausierenDatum"), Short);
        datumPicker.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(ziel.ToString("d", CultureInfo.CurrentCulture));
        Keyboard.Press(VirtualKeyShort.TAB);

        SetTextBoxValue(dialog, "PausierenStunde", ziel.Hour.ToString("00", CultureInfo.InvariantCulture));
        SetTextBoxValue(dialog, "PausierenMinute", ziel.Minute.ToString("00", CultureInfo.InvariantCulture));

        return this;
    }

    /// <returns><c>true</c>, wenn der „Pause aufheben"-Button aktuell aktiviert ist (nur bei bestehender Pause).</returns>
    public bool IsAufhebenEnabled()
    {
        var button = WaitForElement(GetDialogWindow(), cf => cf.ByName("PausierenAufheben"), Short);
        return button.Properties.IsEnabled.Value;
    }

    /// <summary>Wartet, bis die Anzeige der aktuell gesetzten Pause („AktuellePauseAnzeige") sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public AufgabePausierenDialogView WaitForAktuellePauseAnzeige()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("AktuellePauseAnzeige"), Short);
        return this;
    }

    /// <summary>Bestätigt den Dialog über „Übernehmen" und wartet, bis das Dialogfenster geschlossen ist.</summary>
    /// <returns>Die danach sichtbare Aufgabendetailansicht.</returns>
    public TaskDetailView Bestaetigen()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("PausierenBestaetigen"), Short).AsButton().Click();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
        return new TaskDetailView(Window);
    }

    /// <summary>Hebt eine bestehende Pause über „Pause aufheben" auf und wartet, bis das Dialogfenster geschlossen ist.</summary>
    /// <returns>Die danach sichtbare Aufgabendetailansicht.</returns>
    public TaskDetailView Aufheben()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("PausierenAufheben"), Short).AsButton().Click();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
        return new TaskDetailView(Window);
    }

    /// <summary>Bricht den Dialog über „Abbrechen" ab und wartet, bis das Dialogfenster geschlossen ist.</summary>
    /// <returns>Die danach sichtbare Aufgabendetailansicht.</returns>
    public TaskDetailView Abbrechen()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("PausierenAbbrechen"), Short).AsButton().Click();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
        return new TaskDetailView(Window);
    }

    private static void SetTextBoxValue(AutomationElement dialog, string automationName, string value)
    {
        var box = WaitForElement(dialog, cf => cf.ByName(automationName), Short);
        box.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(value);
        Keyboard.Press(VirtualKeyShort.TAB);
    }
}
