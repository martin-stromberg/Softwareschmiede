using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using Softwareschmiede.Tests.E2E;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>
/// Basisklasse für alle UI-View-Helper des View-Patterns. Kapselt wiederholte
/// FlaUI-Interaktionsmuster (Sichtbarkeitsprüfung, Navigation, Menüzugriff) für E2E-Tests.
/// Erbt bewusst nicht von <see cref="Softwareschmiede.Tests.E2E.WpfTestBase"/>, um die
/// View-Pattern-Schicht von der Test-Basisklassen-Schicht getrennt zu halten - die gemeinsame
/// Polling-/Timeout-Logik (Timeout-Konstanten, <see cref="WaitForElement"/>, <see cref="WaitUntilGone"/>,
/// <see cref="GetHelpTextOrName"/>) ist deshalb in <see cref="ElementWaitHelper"/> ausgelagert und wird
/// von beiden Schichten referenziert, statt unabhängig dupliziert zu werden.
/// </summary>
public abstract class BaseWindowView
{
    /// <summary>Kurzes Timeout (20s) für schnell erscheinende UI-Elemente.</summary>
    protected static readonly TimeSpan Short = ElementWaitHelper.Short;

    /// <summary>Mittleres Timeout (15s) für UI-Elemente nach asynchronen Operationen.</summary>
    protected static readonly TimeSpan Medium = ElementWaitHelper.Medium;

    /// <summary>Langes Timeout (30s), z. B. für aufwendige Hintergrundoperationen wie Arbeitsverzeichnis-/Repository-Vorbereitung.</summary>
    protected static readonly TimeSpan Long = ElementWaitHelper.Long;

    /// <param name="window">Das FlaUI-Hauptfenster, auf das sich diese View bezieht.</param>
    protected BaseWindowView(Window window)
    {
        Window = window;
    }

    /// <summary>Das FlaUI-Hauptfenster, auf das sich diese View bezieht.</summary>
    public Window Window { get; }

    /// <summary>Gibt an, ob diese Ansicht gerade aktiv/fokussiert (sichtbar) ist.</summary>
    public abstract bool IsVisible { get; }

    /// <summary>Navigiert zu dieser Ansicht. No-Op, wenn sie bereits sichtbar ist.</summary>
    /// <returns>Diese Instanz (Fluent-API).</returns>
    public abstract BaseWindowView ForceShow();

    /// <summary>Schließt diese Ansicht, optional inklusive aller übergeordneten Ansichten bis zum Dashboard.</summary>
    /// <param name="recurseToDashboard">Wenn <c>true</c>, werden auch alle übergeordneten Ansichten bis zum Dashboard geschlossen.</param>
    /// <returns>Diese Instanz (Fluent-API).</returns>
    public abstract BaseWindowView ForceClose(bool recurseToDashboard);
    /// <summary>
    /// Sorgt dafür, dass die aktuelle Ansicht, sowie die darüber liegenden Ansichten geschlossen werden, bis das Dashboard sichtbar ist.
    /// </summary>
    /// <returns>Diese Instanz (Fluent-API).</returns>
    public BaseWindowView ForceReset()
    {
        return ForceClose(true);
    }

    /// <summary>Zugriff auf das Navigationsmenü der Anwendung.</summary>
    public virtual MenuView Menu => new(Window);

    /// <param name="parent">Das Element, dessen Teilbaum durchsucht wird.</param>
    /// <param name="conditionFunc">Die Suchbedingung.</param>
    /// <returns><c>true</c>, wenn ein passendes, sichtbares Element existiert.</returns>
    protected static bool ElementExists(AutomationElement parent, Func<ConditionFactory, ConditionBase> conditionFunc)
    {
        var element = parent.FindFirstDescendant(conditionFunc);
        return element is not null && IsOnScreen(element);
    }

    /// <summary>
    /// Wartet, bis ein sichtbares Element im Teilbaum von <paramref name="parent"/> gefunden wird. Lokales
    /// Gegenstück zu <see cref="Softwareschmiede.Tests.E2E.WpfTestBase.WaitForElement"/>, da View-Klassen
    /// nicht von <see cref="Softwareschmiede.Tests.E2E.WpfTestBase"/> erben. Ignoriert Treffer, die zwar im
    /// Automation-Baum vorhanden, aber <see cref="AutomationElement.IsOffscreen"/> sind - z. B. Elemente
    /// einer zuvor geöffneten, fensterumfassenden <c>TaskDetailView</c>, die nach dem Navigieren weg davon
    /// weiterhin (verdeckt) im Baum verbleiben können.
    /// </summary>
    /// <remarks>
    /// Übernimmt das Fail-Fast-Verhalten von <see cref="Softwareschmiede.Tests.E2E.WpfTestBase.WaitForElement"/>:
    /// bei jeder Polling-Iteration wird zusätzlich nach einem sichtbaren Fehlerbanner ("FehlerMeldung")
    /// gesucht. Erscheint es, während auf ein *anderes* Element gewartet wird, bricht die Wartezeit sofort
    /// mit einer aussagekräftigen <see cref="InvalidOperationException"/> (inkl. Fehlertext) ab, statt bis
    /// zum vollen Timeout stur weiterzuwarten. Zielt <paramref name="conditionFunc"/> selbst auf
    /// "FehlerMeldung" (z. B. Fehlerfall-Tests, die genau dieses Element als Erfolgskriterium erwarten), wird
    /// die Zielsuche unmittelbar vor dem Werfen der Exception erneut versucht und liefert in diesem Fall das
    /// gefundene Element als regulären Treffer zurück, statt fälschlich in den Fehlerpfad zu laufen.
    /// </remarks>
    /// <param name="parent">Das Element, dessen Teilbaum durchsucht wird.</param>
    /// <param name="conditionFunc">Die Suchbedingung.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Das gefundene Element.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn das Element nicht rechtzeitig gefunden wird.</exception>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn während des Wartens ein Fehlerbanner erscheint, das nicht selbst das gesuchte Zielelement ist.</exception>
    protected static AutomationElement WaitForElement(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout)
        => ElementWaitHelper.WaitForElement(parent, conditionFunc, timeout, IsOnScreen);

    /// <summary>
    /// Wartet, bis ein Top-Level-Fenster mit dem angegebenen Titel auf dem Desktop erscheint. Nutzt die
    /// zum Hauptfenster gehörende <c>Window.Automation</c>-Instanz, statt eine neue
    /// <c>UIA3Automation</c> zu erzeugen (vermeidet ein zusätzliches, unverwaltetes COM-Objekt pro Aufruf).
    /// </summary>
    protected AutomationElement WaitForWindow(string title, TimeSpan timeout)
        => ElementWaitHelper.WaitForElement(Window.Automation.GetDesktop(), cf => cf.ByName(title), timeout, IsOnScreen);

    /// <summary>
    /// Wählt einen Eintrag in einer ComboBox per Klick auf das ComboBoxItem aus (robuster als FlaUI's
    /// <c>Select(string)</c>, das bei manchen TwoWay-Bindings das Binding nicht zuverlässig aktualisiert).
    /// </summary>
    protected static void SelectComboBoxItemByClick(AutomationElement comboBoxElement, string itemText, TimeSpan timeout)
        => ElementWaitHelper.SelectComboBoxItemByClick(comboBoxElement, itemText, timeout, IsOnScreen);

    /// <param name="parent">Das Element, dessen Teilbaum durchsucht wird.</param>
    /// <param name="conditionFunc">Die Suchbedingung.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <exception cref="TimeoutException">Wird geworfen, wenn das Element nicht rechtzeitig verschwindet.</exception>
    protected static void WaitUntilGone(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout)
    {
        var element = ElementWaitHelper.PollUntilGone(parent, conditionFunc, timeout, IsOnScreen);
        if (element is not null)
            throw new TimeoutException($"Element verschwand nicht innerhalb von {timeout.TotalSeconds}s.");
    }

    /// <param name="element">Das zu prüfende Element.</param>
    /// <returns><c>true</c>, wenn das Element sichtbar (nicht <see cref="AutomationElement.IsOffscreen"/>) ist, oder die Eigenschaft nicht unterstützt wird.</returns>
    private static bool IsOnScreen(AutomationElement element)
    {
        try
        {
            return !element.IsOffscreen;
        }
        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
        {
            return true;
        }
    }

    /// <summary>Liest den <c>HelpText</c> eines Elements aus; fällt auf <c>Name</c> zurück, falls leer oder nicht unterstützt.</summary>
    /// <param name="element">Das auszulesende Element.</param>
    /// <returns>Der HelpText, oder falls leer, der Name des Elements.</returns>
    protected static string GetHelpTextOrName(AutomationElement element)
        => ElementWaitHelper.GetHelpTextOrName(element);

    /// <summary>
    /// Navigiert über <see cref="WindowExtensions.CurrentView"/> so lange zu übergeordneten Ansichten,
    /// bis das Dashboard sichtbar ist. Wird von <c>ForceClose(recurseToDashboard: true)</c>-Implementierungen genutzt.
    /// </summary>
    protected void RecurseToDashboard()
    {
        BaseWindowView current;
        try
        {
            current = Window.CurrentView();
        }
        catch (InvalidOperationException)
        {
            new DashboardView(Window).ForceShow();
            return;
        }

        if (current is DashboardView)
            return;

        current.ForceClose(true);
    }
}
