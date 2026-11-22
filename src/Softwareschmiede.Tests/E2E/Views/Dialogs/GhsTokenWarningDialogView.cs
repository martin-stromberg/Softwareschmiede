using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>Native Win32-MessageBox "GitHub-Installationstoken" — der Hinweisdialog von
/// <c>SettingsViewModel.ConfirmGitHubInstallationTokens</c> bei ghs_-Secret-Werten.
/// Automation-IDs entsprechen den stabilen, sprachunabhängigen IDYES/IDNO-Konstanten.</summary>
public sealed class GhsTokenWarningDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public GhsTokenWarningDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "GitHub-Installationstoken";

    /// <summary>Wartet auf das Erscheinen des Dialogs und gibt das gefundene Fenster-Element
    /// zurück (für BoundingBox-/Screenshot-Prüfungen).</summary>
    /// <returns>Das Dialogfenster.</returns>
    public AutomationElement WaitForDialog() => GetDialogWindow();

    /// <summary>Bestätigt das Speichern des Installationstokens über den "Ja"-Button (IDYES).</summary>
    /// <returns>Diese Instanz.</returns>
    public GhsTokenWarningDialogView Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByAutomationId("6"), Short).AsButton().ClickInForeground();

        return this;
    }

    /// <summary>Bricht das Speichern über den "Nein"-Button (IDNO) ab.</summary>
    /// <returns>Diese Instanz.</returns>
    public GhsTokenWarningDialogView Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByAutomationId("7"), Short).AsButton().ClickInForeground();

        return this;
    }
}
