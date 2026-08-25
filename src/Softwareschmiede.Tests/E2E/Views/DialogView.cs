using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>
/// Basisklasse für modale Dialoge. Dialoge sind eigenständige Top-Level-Fenster (eigenes
/// Fenster-Handle, eigener Titel) statt Elemente im Hauptfenster, daher unterscheidet sich
/// die Sichtbarkeits- und Navigationslogik von <see cref="BaseWindowView"/>-Subklassen für Haupt-Views.
/// </summary>
public abstract class DialogView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung (nicht das Dialogfenster selbst).</param>
    protected DialogView(Window window) : base(window)
    {
    }

    /// <summary>Der native Fenstertitel des Dialogs, anhand dessen er auf dem Desktop gefunden wird.</summary>
    protected abstract string DialogTitle { get; }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window.Automation.GetDesktop(), DialogWindowCondition);

    /// <summary>Wartet bis zu <see cref="BaseWindowView.Short"/>, bis das Dialogfenster erscheint, und gibt es zurück.</summary>
    /// <returns>Das gefundene Dialogfenster.</returns>
    protected AutomationElement GetDialogWindow() => GetDialogWindow(Short);

    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Das gefundene Dialogfenster.</returns>
    protected AutomationElement GetDialogWindow(TimeSpan timeout)
        => WaitForElement(Window.Automation.GetDesktop(), DialogWindowCondition, timeout);

    /// <summary>
    /// Bedingung für die Dialogsuche auf dem Desktop: Name UND Control-Type <c>Window</c>. Nur nach
    /// dem Namen zu suchen würde auch beliebige gleichnamige Elemente irgendwo im Inhalt eines anderen
    /// bereits offenen Fensters treffen, da <c>FindFirstDescendant</c> auf dem Desktop-Element den
    /// kompletten Automation-Baum aller offenen Fenster rekursiv durchsucht.
    /// </summary>
    private Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> DialogWindowCondition
        => cf => cf.ByName(DialogTitle).And(cf.ByControlType(ControlType.Window));

    /// <summary>
    /// Wartet, bis der Dialog erscheint. Weicht bewusst vom in <see cref="BaseWindowView.ForceShow"/>
    /// dokumentierten Vertrag ("Navigiert zu dieser Ansicht") ab: Diese Basisimplementierung navigiert
    /// NICHT aktiv (kein Button-Klick im Hauptfenster), sondern wartet nur passiv, bis der Dialog von
    /// außen bereits geöffnet wurde. Grund: Viele Dialoge dieser Hierarchie werden nicht durch einen
    /// einzelnen, zustandslos erreichbaren Button ausgelöst, sondern erscheinen nur als Folge komplexer
    /// Fachlogik (z. B. eines laufenden CLI-Prozesses, eines Update-Checks beim App-Start, oder einer
    /// bereits gestarteten autonomen Aufgabe) - eine generische aktive Navigation ist für diese Fälle
    /// nicht sinnvoll möglich. Subklassen mit einem einfachen, direkt erreichbaren Auslöse-Button
    /// überschreiben <see cref="ForceShow"/> stattdessen mit echter Navigationslogik (siehe
    /// <see cref="Softwareschmiede.Tests.E2E.Views.Dialogs.RepositoryAssignDialogView"/> und
    /// <see cref="Softwareschmiede.Tests.E2E.Views.Dialogs.PluginSelectionDialogView"/>). Aufrufer von
    /// <c>ForceShow()</c> auf einer Subklasse ohne eigene Überschreibung müssen den Dialog daher selbst
    /// zuvor öffnen, sonst wirft dieser Aufruf nach <see cref="BaseWindowView.Short"/> eine
    /// <see cref="TimeoutException"/>.
    /// </summary>
    /// <returns>Diese Instanz (Fluent-API).</returns>
    public override DialogView ForceShow()
    {
        GetDialogWindow();
        return this;
    }

    /// <inheritdoc/>
    public override DialogView ForceClose(bool recurseToDashboard)
    {
        var dialog = GetDialogWindow();
        dialog.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        WaitUntilGone(Window.Automation.GetDesktop(), DialogWindowCondition, Short);

        if (recurseToDashboard)
            RecurseToDashboard();

        return this;
    }
}
