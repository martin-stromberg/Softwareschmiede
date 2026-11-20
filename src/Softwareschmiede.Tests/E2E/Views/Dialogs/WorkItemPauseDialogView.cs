using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System.Globalization;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den modalen „Pause einstellen"-Dialog der Aufgabendetailansicht (Issue 151).</summary>
public sealed class WorkItemPauseDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung (nicht das Dialogfenster selbst).</param>
    public WorkItemPauseDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Pause einstellen";

    /// <summary>Öffnet den Dialog über den Ribbon-Button „Pause einstellen" der geöffneten Aufgabendetailansicht.</summary>
    /// <returns>Diese Instanz, sobald das Dialogfenster sichtbar ist.</returns>
    public override WorkItemPauseDialogView ForceShow()
    {
        WaitForElement(Window, cf => cf.ByName("SetPause"), Medium).AsButton().Click();
        GetDialogWindow(Medium);
        return this;
    }

    /// <returns>Die im Dialog vorbelegte Uhrzeit (Stunde, Minute).</returns>
    public (int Hour, int Minute) GetPrefilledTime()
    {
        var dialog = GetDialogWindow();
        var hour = WaitForElement(dialog, cf => cf.ByName("PauseHour"), Short).AsTextBox().Text;
        var minute = WaitForElement(dialog, cf => cf.ByName("PauseMinute"), Short).AsTextBox().Text;
        return (int.Parse(hour, CultureInfo.InvariantCulture), int.Parse(minute, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Trägt Datum, Stunde und Minute des angegebenen lokalen Zielzeitpunkts ein. Das Datum wird in das
    /// DatePicker-Textfeld getippt (lokales Kurzdatumsformat) und per Tab-Taste committed.
    /// </summary>
    /// <param name="target">Der lokale Zielzeitpunkt (Datum + Uhrzeit).</param>
    /// <returns>Diese Instanz.</returns>
    public WorkItemPauseDialogView SetTimestamp(DateTime target)
    {
        var dialog = GetDialogWindow();

        var datePicker = WaitForElement(dialog, cf => cf.ByName("PauseDate"), Short);
        datePicker.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(target.ToString("d", CultureInfo.CurrentCulture));
        Keyboard.Press(VirtualKeyShort.TAB);

        SetTextBoxValue(dialog, "PauseHour", target.Hour.ToString("00", CultureInfo.InvariantCulture));
        SetTextBoxValue(dialog, "PauseMinute", target.Minute.ToString("00", CultureInfo.InvariantCulture));

        return this;
    }

    /// <returns><c>true</c>, wenn der „Pause aufheben"-Button aktuell aktiviert ist (nur bei bestehender Pause).</returns>
    public bool IsLiftEnabled()
    {
        var button = WaitForElement(GetDialogWindow(), cf => cf.ByName("LiftPause"), Short);
        return button.Properties.IsEnabled.Value;
    }

    /// <summary>Wartet, bis die Anzeige der aktuell gesetzten Pause („CurrentPauseDisplay") sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public WorkItemPauseDialogView WaitForCurrentPauseDisplay()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("CurrentPauseDisplay"), Short);
        return this;
    }

    /// <summary>Bestätigt den Dialog über „Übernehmen" und wartet, bis das Dialogfenster geschlossen ist.</summary>
    /// <returns>Die danach sichtbare Aufgabendetailansicht.</returns>
    public TaskDetailView Confirm()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("ConfirmPause"), Short).AsButton().Click();
        ElementWaitHelper.WaitUntilProcessWindowGone(Window, DialogWindowCondition, Short);
        return new TaskDetailView(Window);
    }

    /// <summary>Hebt eine bestehende Pause über „Pause aufheben" auf und wartet, bis das Dialogfenster geschlossen ist.</summary>
    /// <returns>Die danach sichtbare Aufgabendetailansicht.</returns>
    public TaskDetailView Lift()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("LiftPause"), Short).AsButton().Click();
        ElementWaitHelper.WaitUntilProcessWindowGone(Window, DialogWindowCondition, Short);
        return new TaskDetailView(Window);
    }

    /// <summary>Bricht den Dialog über „Cancel" ab und wartet, bis das Dialogfenster geschlossen ist.</summary>
    /// <returns>Die danach sichtbare Aufgabendetailansicht.</returns>
    public TaskDetailView Cancel()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("CancelPause"), Short).AsButton().Click();
        ElementWaitHelper.WaitUntilProcessWindowGone(Window, DialogWindowCondition, Short);
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
