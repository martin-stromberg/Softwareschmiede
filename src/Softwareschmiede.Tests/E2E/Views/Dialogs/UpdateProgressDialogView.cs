using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Update-Fortschritts-Dialog ("Update vorbereiten").</summary>
public sealed class UpdateProgressDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public UpdateProgressDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Update vorbereiten";

    /// <summary>Liest den aktuellen Phasentext ("UpdatePhase"), z. B. "Download", "Entpacken", "Update-Vorbereitung".</summary>
    /// <returns>Der angezeigte Phasentext.</returns>
    public string GetPhase()
        => WaitForElement(GetDialogWindow(), cf => cf.ByAutomationId("UpdatePhase"), Short).Name;

    /// <summary>Liest die aktuelle Fortschritts-/Fehlermeldung ("UpdateFortschrittMeldung").</summary>
    /// <returns>Der angezeigte Meldungstext.</returns>
    public string GetMessage()
        => WaitForElement(GetDialogWindow(), cf => cf.ByAutomationId("UpdateFortschrittMeldung"), Short).Name;

    /// <summary>
    /// Liest den Prozentwert der Fortschrittsanzeige ("UpdateFortschritt"). Bei nicht verfügbarem
    /// Wert (z. B. unbestimmter Fortschritt ohne Range-Value-Pattern) wird <c>null</c> geliefert.
    /// </summary>
    /// <returns>Der Prozentwert von 0 bis 100 oder <c>null</c>.</returns>
    public double? GetProgress()
    {
        var bar = WaitForElement(GetDialogWindow(), cf => cf.ByName("UpdateFortschritt"), Short).AsProgressBar();
        try
        {
            return bar.Value;
        }
        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
        {
            return null;
        }
        catch (FlaUI.Core.Exceptions.PatternNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Klickt den "Abbrechen"-Button ("UpdateAbbrechen"), sofern er aktiviert ist.</summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="InvalidOperationException">Der Abbrechen-Button ist aktuell deaktiviert.</exception>
    public UpdateProgressDialogView Cancel()
    {
        var button = WaitForElement(GetDialogWindow(), cf => cf.ByName("UpdateAbbrechen"), Short).AsButton();
        if (!button.IsEnabled)
            throw new InvalidOperationException("Der Abbrechen-Button des Update-Dialogs ist deaktiviert.");

        button.Click();
        return this;
    }

    /// <summary>Gibt an, ob der Abbrechen-Button aktuell aktiviert ist.</summary>
    /// <returns><c>true</c>, wenn abgebrochen werden kann.</returns>
    public bool CanCancel()
        => WaitForElement(GetDialogWindow(), cf => cf.ByName("UpdateAbbrechen"), Short).AsButton().IsEnabled;

    /// <summary>
    /// Wartet, bis der Dialog den Fehlerzustand erreicht hat (Abbrechen deaktiviert, Schließen
    /// erlaubt, Fehlermeldung sichtbar) und liefert die angezeigte Meldung.
    /// </summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Die angezeigte Fehlermeldung.</returns>
    /// <exception cref="TimeoutException">Der Fehlerzustand wurde nicht rechtzeitig erreicht.</exception>
    public string WaitForErrorState(TimeSpan timeout)
    {
        var dialog = GetDialogWindow();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var abbrechen = dialog.FindFirstDescendant(cf => cf.ByName("UpdateAbbrechen"));
            var meldung = dialog.FindFirstDescendant(cf => cf.ByAutomationId("UpdateFortschrittMeldung"))?.Name ?? string.Empty;
            if (abbrechen is not null && !abbrechen.IsEnabled && meldung.Length > 0)
                return meldung;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Der Update-Dialog erreichte innerhalb von {timeout.TotalSeconds}s keinen Fehlerzustand.");
    }

    /// <summary>Schließt den Dialog über das Window-Pattern (nur im Fehler-/Abschlusszustand erlaubt).</summary>
    /// <returns>Diese Instanz.</returns>
    public UpdateProgressDialogView Close()
    {
        var dialog = GetDialogWindow();
        dialog.AsWindow().Close();
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);
        return this;
    }
}
