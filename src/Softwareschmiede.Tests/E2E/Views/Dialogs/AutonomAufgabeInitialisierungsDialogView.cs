using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Initialisierungs-Dialog einer autonomen Aufgabe.</summary>
public sealed class AutonomAufgabeInitialisierungsDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public AutonomAufgabeInitialisierungsDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Autonome Aufgabe initialisieren";

    /// <summary>Klickt den "AutonomAufgabeInitialisieren"-Button im Hauptfenster (TaskDetailView), falls der Dialog noch nicht sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public override AutonomAufgabeInitialisierungsDialogView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("AutonomAufgabeInitialisieren"), Short).AsButton().Click();
        GetDialogWindow();

        return this;
    }

    /// <returns><c>true</c>, wenn alle Formularfelder des Dialogs sichtbar sind.</returns>
    public bool HasFormFields()
    {
        var dialog = GetDialogWindow();
        return ElementExists(dialog, cf => cf.ByName("AutonomAufgabeProjektbranchEingabe"))
            && ElementExists(dialog, cf => cf.ByName("AutonomAufgabePermissionsAuswahl"))
            && ElementExists(dialog, cf => cf.ByName("AutonomAufgabeTokenBudget"))
            && ElementExists(dialog, cf => cf.ByName("AutonomAufgabeLaufzeitLimit"));
    }

    /// <summary>Trägt den initialen Prompt in das "AutonomAufgabeInitialPrompt"-Feld ein.</summary>
    /// <param name="prompt">Der initiale Prompt-Text.</param>
    /// <returns>Diese Instanz.</returns>
    public AutonomAufgabeInitialisierungsDialogView SetInitialPrompt(string prompt)
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeInitialPrompt"), Short).AsTextBox().Text = prompt;
        return this;
    }

    /// <summary>
    /// Bestätigt den Dialog über "AutonomAufgabeBestaetigen" und wartet, bis die eingebettete
    /// "Automatisierung"-Registerkarte der Aufgabendetailansicht sichtbar wird (Arbeitsverzeichnis-/
    /// Repository-Vorbereitung kann einige Sekunden dauern). Kein eigenes Detail-Fenster mehr (Folge-
    /// Integration zu Issue 205): Steuerung erfolgt danach ausschließlich über die Ribbon-Buttons der
    /// Aufgabendetailansicht.
    /// </summary>
    /// <returns>Die Detailansicht der autonomen Aufgabe (eingebettete Registerkarte).</returns>
    public AutonomAufgabeDetailView Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("AutonomAufgabeBestaetigen"), Short).AsButton().Click();

        WaitForElement(Window, cf => cf.ByName("AutonomAufgabeDetailTabs"), Long);
        return new AutonomAufgabeDetailView(Window);
    }
}
