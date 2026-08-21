using System.Windows;
using Softwareschmiede.App.ViewModels;

namespace Softwareschmiede.App.Views;

/// <summary>Dialog zur Initialisierung einer Autonomen Aufgabe.</summary>
public partial class AutonomAufgabeInitialisierungsDialog : Window
{
    private const string HilfeText = """
        Autonome Aufgabe – Ablauf

        Eine Autonome Aufgabe wird nicht direkt von dir per CLI gesteuert, sondern von einem
        Projektleiter-Agenten, der die Anforderung in Teilaufgaben zerlegt und eigenständig
        umsetzt:

        1. Initialisierung (dieser Dialog)
           Es wird ein eigenes Arbeitsverzeichnis mit Repository-Klon, Konfigurationsdateien
           (state.json, permissions.json) sowie plan.md/progress.md/governance.md angelegt.

        2. Start des Projektleiter-Agenten
           Der Projektleiter erhält deinen Initialprompt sowie die konfigurierten Limits
           (Token-Budget, Laufzeitbegrenzung) und beginnt mit der Umsetzung.

        3. Unteragenten
           Für Teilaufgaben erzeugt der Projektleiter bei Bedarf Unteragenten mit eigenem
           Feature-Branch und eigenem Arbeitsverzeichnis. Deren Berechtigungen werden
           streng auf ihren jeweiligen Zuständigkeitsbereich beschränkt.

        4. Fortschritt und Integration
           Ergebnisse der Unteragenten werden laufend in plan.md/progress.md integriert;
           Pull Requests werden vorbereitet, aber nicht automatisch gemergt.

        5. Session-Pause und Fortsetzung
           Bei Erreichen des Token- oder Zeitbudgets pausiert die Session automatisch. Du
           kannst die Aufgabe anschließend über "Resume" fortsetzen.

        Felder in diesem Dialog:
        - Projektbranch: Der Branch, auf dem der Projektleiter arbeitet. Kann aus den
          Remote-Branches des Repositories gewählt oder über "+" neu angelegt werden.
        - Promptvorlage: Optionale Vorlage, die den Initialprompt vorbefüllt.
        - Permissions-Quelle: Steuert, wie permissions.json erzeugt wird.
        - Token-Budget / Laufzeitbegrenzung: Obergrenzen für die Session.
        - Persistenz-Modus: Verhalten beim Fortsetzen einer pausierten Session.
        - Skill-Autogenerierung: Erlaubt dem Projektleiter, eigene Skills zu erzeugen.
        """;

    private AutonomAufgabeInitialisierungsDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    public AutonomAufgabeInitialisierungsDialog(AutonomAufgabeInitialisierungsDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    private void OnHilfeClick(object sender, RoutedEventArgs e)
    {
        var dialog = new HelpTextDialog(HilfeText) { Owner = this };
        dialog.ShowDialog();
    }
}
