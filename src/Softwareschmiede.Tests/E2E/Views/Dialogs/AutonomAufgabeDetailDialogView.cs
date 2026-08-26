using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Detail-Dialog einer autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeDetailDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public AutonomAufgabeDetailDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Autonome Aufgabe";

    /// <summary>
    /// Wartet bis zu <see cref="BaseWindowView.Long"/>, bis der Dialog erscheint - die Arbeitsverzeichnis-/
    /// Repository-Vorbereitung, die dieser Ansicht vorausgeht, kann mehrere Sekunden dauern.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public override AutonomAufgabeDetailDialogView ForceShow()
    {
        GetDialogWindow(Long);
        WaitForElement(GetDialogWindow(), cf => cf.ByName("AutonomAufgabeStart"), Long);
        return this;
    }

    /// <summary>Schließt das Detailfenster (eigenständiges Top-Level-Fenster, kein modaler Dialog).</summary>
    public void Close() => GetDialogWindow().AsWindow().Close();

    /// <summary>Klickt den "AutonomAufgabeStart"-Button, um den Projektleiter-Agenten zu starten.</summary>
    /// <returns>Diese Instanz.</returns>
    public AutonomAufgabeDetailDialogView Start()
    {
        WaitForElement(GetDialogWindow(), cf => cf.ByName("AutonomAufgabeStart"), Long).AsButton().Click();
        return this;
    }

    /// <returns><c>true</c>, wenn der "AutonomAufgabeResume"-Button sichtbar ist.</returns>
    public bool HasResumeButton() => ElementExists(GetDialogWindow(), cf => cf.ByName("AutonomAufgabeResume"));
}
