using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Gemeinsame Polling-/Timeout-Hilfslogik für FlaUI-basierte E2E-Tests. Wird sowohl von
/// <see cref="WpfTestBase"/> als auch von den View-Pattern-Klassen unter
/// <c>Softwareschmiede.Tests.E2E.Views</c> (siehe <c>BaseWindowView</c>) referenziert, damit die
/// Timeout-Konstanten und die Warteschleifen für UI-Elemente nicht unabhängig doppelt gepflegt
/// werden müssen. Beide Schichten behalten dabei ihr eigenes Verhalten an den Stellen, an denen es
/// bewusst unterschiedlich ist (z. B. Offscreen-Filterung, Reaktion auf ein nicht verschwundenes
/// Element) - dafür nehmen die Methoden hier optionale Sichtbarkeits-Prädikate entgegen, statt ein
/// Verhalten vorzugeben.
/// </summary>
internal static class ElementWaitHelper
{
    /// <summary>
    /// Kurzes Timeout (20s) für schnell erscheinende UI-Elemente. War ursprünglich 10s; auf
    /// windows-latest-CI-Runnern (siehe .github/workflows/test.yml) zeigte sich ein einmaliger
    /// JIT-/Rendering-Warmup-Effekt bei den ersten Popup-/Dialog-Interaktionen eines Testlaufs
    /// (ComboBox-Dropdown, MessageBox) - belegt dadurch, dass spätere Tests mit identischen
    /// UI-Mustern im selben Lauf durchgehend in 6-10s durchliefen, während die ersten 2-3 solcher
    /// Interaktionen knapp über 10s lagen. 20s deckt diesen einmaligen Warmup-Puffer ab, ohne echte
    /// künftige Regressionen (die deutlich länger bräuchten) zu maskieren.
    /// </summary>
    internal static readonly TimeSpan Short = TimeSpan.FromSeconds(20);

    /// <summary>Mittleres Timeout (15s) für UI-Elemente nach asynchronen Operationen.</summary>
    internal static readonly TimeSpan Medium = TimeSpan.FromSeconds(15);

    /// <summary>Langes Timeout (30s), z. B. für aufwendige Hintergrundoperationen wie Arbeitsverzeichnis-/Repository-Vorbereitung.</summary>
    internal static readonly TimeSpan Long = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Wartet, bis ein Element im Teilbaum von <paramref name="parent"/> gefunden wird, das zusätzlich
    /// <paramref name="isVisible"/> erfüllt (Standard: jedes gefundene Element gilt als sichtbar).
    /// Wirft <see cref="TimeoutException"/>, wenn das Element nicht innerhalb von <paramref name="timeout"/> erscheint.
    /// </summary>
    /// <remarks>
    /// Als Fail-Fast-Diagnose wird bei jeder Polling-Iteration zusätzlich nach einem Fehlerbanner
    /// ("FehlerMeldung") gesucht: Wer auf ein *anderes* Element wartet, soll sofort mit einer
    /// aussagekräftigen Meldung abbrechen, statt erst nach Ablauf des vollen Timeouts. Da die Suche nach
    /// <paramref name="conditionFunc"/> und die Suche nach "FehlerMeldung" zwei separate, nicht-atomare
    /// UI-Automation-Aufrufe sind, kann es vorkommen, dass der erste Aufruf ein gerade erst erscheinendes
    /// Element knapp verpasst, während der zweite (Millisekunden später) es bereits findet. Für Aufrufer,
    /// deren <paramref name="conditionFunc"/> selbst auf "FehlerMeldung" zielt (z. B. Fehlerfall-Tests, die
    /// genau dieses Element als Erfolgskriterium erwarten), würde das fälschlich als Abbruchgrund statt als
    /// gefundenes Zielelement gewertet. Deshalb wird die Zielsuche unmittelbar vor dem Werfen der Exception
    /// erneut versucht: Ist "FehlerMeldung" tatsächlich das gesuchte Zielelement, ist es zu diesem Zeitpunkt
    /// im Automation-Baum bereits vorhanden (der Fehlerbanner-Check hat es ja soeben gefunden) und die
    /// erneute Zielsuche liefert es als regulären Treffer zurück. Zielt <paramref name="conditionFunc"/> auf
    /// ein anderes Element, bleibt die erneute Suche erfolglos und die Fail-Fast-Diagnose greift wie bisher.
    /// </remarks>
    /// <param name="parent">Das Element, dessen Teilbaum durchsucht wird.</param>
    /// <param name="conditionFunc">Die Suchbedingung.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <param name="isVisible">Optionales zusätzliches Sichtbarkeits-Prädikat; Standard: jeder Treffer gilt als sichtbar.</param>
    /// <returns>Das gefundene Element.</returns>
    internal static AutomationElement WaitForElement(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout,
        Func<AutomationElement, bool>? isVisible = null)
    {
        isVisible ??= static _ => true;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var element = parent.FindFirstDescendant(conditionFunc);
            if (element is not null && isVisible(element))
                return element;

            var fehlerMeldung = parent.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
            if (fehlerMeldung is not null && isVisible(fehlerMeldung))
            {
                // Letzter Versuch: Falls conditionFunc selbst auf "FehlerMeldung" zielt, ist das Element
                // inzwischen sicher auffindbar (siehe Erklärung oben) und der Aufruf soll regulär mit
                // diesem Treffer zurückkehren statt in den Fehlerpfad zu laufen.
                element = parent.FindFirstDescendant(conditionFunc);
                if (element is not null && isVisible(element))
                    return element;

                throw new InvalidOperationException(
                    $"In der Anwendung wird eine Fehlermeldung angezeigt: {GetHelpTextOrName(fehlerMeldung)}");
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Element wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden.");
    }

    /// <summary>
    /// Sucht wiederholt nach einem Element im Teilbaum von <paramref name="parent"/>, bis es entweder
    /// verschwindet (bzw. <paramref name="isVisible"/> nicht mehr erfüllt) oder <paramref name="timeout"/>
    /// abgelaufen ist. Gibt <c>null</c> zurück, wenn das Element rechtzeitig verschwunden ist, sonst das
    /// zuletzt gefundene Element. Aufrufer entscheiden selbst, wie ein nicht rechtzeitig verschwundenes
    /// Element gemeldet wird - <see cref="WpfTestBase.WaitUntilGone"/> und <c>BaseWindowView.WaitUntilGone</c>
    /// nutzen dafür bewusst unterschiedliche Fehlerpfade.
    /// </summary>
    /// <param name="parent">Das Element, dessen Teilbaum durchsucht wird.</param>
    /// <param name="conditionFunc">Die Suchbedingung.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <param name="isVisible">Optionales zusätzliches Sichtbarkeits-Prädikat; Standard: jeder Treffer gilt als sichtbar.</param>
    /// <returns><c>null</c>, wenn das Element rechtzeitig verschwunden ist, sonst das zuletzt gefundene Element.</returns>
    internal static AutomationElement? PollUntilGone(
        AutomationElement parent,
        Func<ConditionFactory, ConditionBase> conditionFunc,
        TimeSpan timeout,
        Func<AutomationElement, bool>? isVisible = null)
    {
        isVisible ??= static _ => true;

        var deadline = DateTime.UtcNow + timeout;
        AutomationElement? element = null;
        while (DateTime.UtcNow < deadline)
        {
            element = parent.FindFirstDescendant(conditionFunc);
            if (element is null || !isVisible(element))
                return null;

            Thread.Sleep(200);
        }

        return element;
    }

    /// <summary>
    /// Wählt einen Eintrag in einer ComboBox per Klick auf das ComboBoxItem aus (robuster als FlaUI's
    /// <c>Select(string)</c>, das bei manchen TwoWay-Bindings das Binding nicht zuverlässig aktualisiert).
    /// </summary>
    /// <param name="comboBoxElement">Das ComboBox-Element.</param>
    /// <param name="itemText">Der Name des auszuwählenden ComboBoxItems.</param>
    /// <param name="timeout">Maximale Wartezeit, bis das ComboBoxItem nach dem Öffnen erscheint.</param>
    /// <param name="isVisible">Optionales zusätzliches Sichtbarkeits-Prädikat für die Item-Suche; Standard: jeder Treffer gilt als sichtbar.</param>
    internal static void SelectComboBoxItemByClick(
        AutomationElement comboBoxElement,
        string itemText,
        TimeSpan timeout,
        Func<AutomationElement, bool>? isVisible = null)
    {
        var comboBox = comboBoxElement.AsComboBox();
        comboBox.Click();
        Thread.Sleep(300);

        var item = WaitForElement(comboBoxElement, cf => cf.ByName(itemText), timeout, isVisible);
        item.Click();
        Thread.Sleep(200);
    }

    /// <summary>Wartet, bis eine ComboBox den erwarteten selektierten Eintrag anzeigt.</summary>
    /// <param name="comboBoxElement">Das ComboBox-Element.</param>
    /// <param name="expectedItemText">Der erwartete Anzeigetext des ausgewählten Eintrags.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der erwartete Eintrag nicht rechtzeitig ausgewählt ist.</exception>
    internal static void WaitForSelectedComboBoxItem(AutomationElement comboBoxElement, string expectedItemText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? selectedItemName = null;
        while (DateTime.UtcNow < deadline)
        {
            selectedItemName = comboBoxElement.AsComboBox().SelectedItem?.Name;
            if (string.Equals(selectedItemName, expectedItemText, StringComparison.Ordinal))
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"ComboBox zeigte nicht innerhalb von {timeout.TotalSeconds}s den erwarteten Eintrag '{expectedItemText}'. Aktuell: '{selectedItemName}'.");
    }

    /// <summary>
    /// Liest den <c>HelpText</c> eines Elements aus; fällt auf <c>Name</c> zurück, wenn <c>HelpText</c>
    /// leer ist oder von der zugrunde liegenden Automatisierung nicht unterstützt wird.
    /// </summary>
    /// <param name="element">Das auszulesende Element.</param>
    /// <returns>Der HelpText, oder falls leer, der Name des Elements.</returns>
    internal static string GetHelpTextOrName(AutomationElement element)
    {
        string? helpText = null;
        try
        {
            helpText = element.HelpText;
        }
        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
        {
        }

        return string.IsNullOrWhiteSpace(helpText) ? element.Name : helpText;
    }
}
