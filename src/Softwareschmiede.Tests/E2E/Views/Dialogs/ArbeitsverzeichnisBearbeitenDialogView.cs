using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Arbeitsverzeichnis-Bearbeitungs-Dialog.</summary>
public sealed class ArbeitsverzeichnisBearbeitenDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public ArbeitsverzeichnisBearbeitenDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Arbeitsverzeichnis bearbeiten";

    /// <summary>Klickt den "ArbeitsverzeichnisBearbeiten"-Button im Hauptfenster (ProjectDetailView), falls der Dialog noch nicht sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public override ArbeitsverzeichnisBearbeitenDialogView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("ArbeitsverzeichnisBearbeiten"), Short).AsButton().Click();
        GetDialogWindow();

        return this;
    }

    /// <returns>Der aktuelle Wert des "ArbeitsverzeichnisEingabe"-Feldes.</returns>
    public string GetManualPath() => WaitForElement(GetDialogWindow(), cf => cf.ByName("ArbeitsverzeichnisEingabe"), Short).AsTextBox().Text;

    /// <summary>Bestätigt die Bearbeitung über den "Speichern"-Button im Dialog.</summary>
    /// <returns>Die Projektdetailansicht, zu der nach dem Speichern zurückgekehrt wird.</returns>
    public ProjectDetailView Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Speichern"), Short).AsButton().Click();

        return new ProjectDetailView(Window);
    }
}
