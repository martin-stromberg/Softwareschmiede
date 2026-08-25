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
}
