using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views.Dialogs;

/// <summary>View für den Repository-Zuweisungs-Dialog.</summary>
public sealed class RepositoryAssignDialogView : DialogView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public RepositoryAssignDialogView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    protected override string DialogTitle => "Repository zuweisen";

    /// <summary>Klickt den "Zuweisen"-Button im Hauptfenster (ProjectDetailView), falls der Dialog noch nicht sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public override RepositoryAssignDialogView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("Zuweisen"), Short).AsButton().Click();
        GetDialogWindow();

        return this;
    }

    /// <summary>
    /// Wählt das erste Element aus der Repository-Liste des Dialogs aus. Wartet dabei, bis die (asynchron
    /// per Verzeichnis-Scan befüllte) Liste mindestens ein Element enthält.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn die Liste innerhalb des Timeouts kein Element enthält.</exception>
    public RepositoryAssignDialogView SelectFirstRepository()
    {
        WaitForFirstRepositoryItem().Click();
        return this;
    }

    /// <summary>
    /// Wartet, bis die Repository-Liste des Dialogs mindestens ein Element enthält, und gibt das erste
    /// Listenelement zurück, ohne es zu klicken. Für Aufrufer, die zwischen dem Erscheinen des
    /// Listeneintrags und dem eigentlichen Klick noch eine eigene Aktion ausführen müssen (z. B. das
    /// zugrunde liegende Verzeichnis gezielt löschen, um den Fallback-Pfad bei fehlgeschlagenem
    /// Strukturabruf zu testen).
    /// </summary>
    /// <returns>Das erste Listenelement der Repository-Liste.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn die Liste innerhalb des Timeouts kein Element enthält.</exception>
    public AutomationElement WaitForFirstRepositoryItem()
    {
        var dialog = GetDialogWindow();

        AutomationElement[] items = [];
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var listBox = dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listBox is not null)
            {
                items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                if (items.Length > 0)
                    return items[0];
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Repository-Liste im Zuweisungsdialog enthielt kein Element innerhalb des Timeouts.");
    }

    /// <summary>Wählt einen Eintrag in der "ArbeitsverzeichnisComboBox" aus (erfolgreicher Strukturabruf).</summary>
    /// <param name="name">Der Name des auszuwählenden Unterverzeichnisses.</param>
    /// <returns>Diese Instanz.</returns>
    public RepositoryAssignDialogView SelectWorkingDirectory(string name)
    {
        var dialog = GetDialogWindow();
        var comboBox = WaitForElement(dialog, cf => cf.ByName("ArbeitsverzeichnisComboBox"), Short);
        SelectComboBoxItemByClick(comboBox, name, Short);
        return this;
    }

    /// <summary>Trägt einen manuellen Pfad in das "ArbeitsverzeichnisEingabe"-Feld ein (fehlgeschlagener Strukturabruf).</summary>
    /// <param name="path">Der einzutragende relative Pfad.</param>
    /// <returns>Diese Instanz.</returns>
    public RepositoryAssignDialogView SetManualWorkingDirectory(string path)
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("ArbeitsverzeichnisEingabe"), Short).AsTextBox().Text = path;
        return this;
    }

    /// <summary>Bestätigt die Repository-Zuweisung über den "Zuweisen"-Button im Dialog.</summary>
    /// <returns>Die Projektdetailansicht, zu der nach der Zuweisung zurückgekehrt wird.</returns>
    public ProjectDetailView Confirm()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Zuweisen"), Short).AsButton().Click();

        return new ProjectDetailView(Window);
    }

    /// <summary>
    /// Trägt den Basis-Branch in das "BasisBranchEingabe"-Feld ein, sobald es aktiviert ist (nicht mehr
    /// durch <c>IsLoadingSourceBranches</c> deaktiviert - das Feld startet deaktiviert, während remote
    /// verfügbare Branches geladen werden).
    /// </summary>
    /// <param name="baseBranch">Der einzutragende Basis-Branch.</param>
    /// <returns>Diese Instanz.</returns>
    public RepositoryAssignDialogView SetBaseBranch(string baseBranch)
    {
        var dialog = GetDialogWindow();
        WaitForEnabledElement(dialog, "BasisBranchEingabe", Short).AsTextBox().Text = baseBranch;
        return this;
    }

    /// <returns><c>true</c>, wenn das Label "Arbeitsverzeichnis im Repository" sichtbar ist.</returns>
    public bool HasWorkingDirectoryLabel() => ElementExists(GetDialogWindow(), cf => cf.ByName("Arbeitsverzeichnis im Repository"));

    /// <returns><c>true</c>, wenn die "ArbeitsverzeichnisComboBox" sichtbar ist.</returns>
    public bool HasWorkingDirectoryComboBox() => ElementExists(GetDialogWindow(), cf => cf.ByName("ArbeitsverzeichnisComboBox"));

    /// <summary>Bricht den Dialog über den "Abbrechen"-Button ab, ohne eine Zuweisung vorzunehmen.</summary>
    public void Cancel()
    {
        var dialog = GetDialogWindow();
        WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short).AsButton().Click();
    }

    private static AutomationElement WaitForEnabledElement(AutomationElement parent, string automationName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var element = parent.FindFirstDescendant(cf => cf.ByName(automationName));
            if (element is not null && element.IsEnabled)
                return element;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Element '{automationName}' wurde nicht innerhalb von {timeout.TotalSeconds}s aktiviert gefunden.");
    }
}
