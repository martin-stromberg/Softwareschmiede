using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Dashboard-Startansicht.</summary>
public sealed class DashboardView : BaseWindowView
{
    /// <summary>AutomationId der Aufgabenliste auf dem Dashboard.</summary>
    private const string AufgabenListeAutomationId = "AktiveAufgabenListeDashboard";

    /// <summary>Namenspräfix der Modus-Umschalter der Dashboard-Aufgabenliste.</summary>
    private const string AufgabenModusPraefix = "DashboardAufgabenModus:";

    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public DashboardView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("Dashboard").And(cf.ByControlType(ControlType.Text)));

    /// <inheritdoc/>
    public override DashboardView ForceShow()
    {
        if (IsVisible)
            return this;

        Menu.NavigateToDashboard();
        return this;
    }

    /// <inheritdoc/>
    public override DashboardView ForceClose(bool recurseToDashboard) => this;

    /// <returns><c>true</c>, wenn das Recovery-Banner ("Aufgabe(n) benötigen Wiederherstellung.") sichtbar ist.</returns>
    public bool HasRecoveryBanner() => ElementExists(Window, cf => cf.ByName("Aufgabe(n) benötigen Wiederherstellung."));

    /// <summary>
    /// Schaltet den Anzeigemodus der Dashboard-Aufgabenliste um (Klick auf den
    /// 'DashboardAufgabenModus:{modus}'-Umschalter — ansichtsspezifischer Anker, da die
    /// Seitenleisten-Umschalter 'AufgabenModus:*' heißen).
    /// </summary>
    /// <param name="modus">Der anzuzeigende Modus ("Datum", "Repository" oder "KI").</param>
    /// <returns>Diese Instanz.</returns>
    public DashboardView SetzeAufgabenListenModus(string modus)
    {
        KlickeAufgabenModusUmschalter(AufgabenModusPraefix, modus);
        return this;
    }

    /// <summary>
    /// Liest die Gruppennamen der Dashboard-Aufgabenliste im gruppierten Modus
    /// (UIA-Namen 'AufgabenGruppe:{Name}' der Gruppenheader, gescoped unter 'AktiveAufgabenListeDashboard').
    /// </summary>
    /// <returns>Die Gruppennamen ohne das 'AufgabenGruppe:'-Präfix; leer im flachen Modus.</returns>
    public string[] GetAufgabenGruppenNamen() => GetAufgabenGruppenNamen(AufgabenListeAutomationId);

    /// <summary>
    /// Wartet, bis der Gruppenheader 'AufgabenGruppe:{name}' in der Dashboard-Aufgabenliste
    /// erscheint (gescoped unter 'AktiveAufgabenListeDashboard').
    /// </summary>
    /// <param name="name">Der erwartete Gruppenname (z. B. ein Repository-Name).</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Der Gruppenheader erschien nicht rechtzeitig.</exception>
    public DashboardView WaitForAufgabenGruppe(string name, TimeSpan timeout)
    {
        WaitForAufgabenGruppe(AufgabenListeAutomationId, name, timeout);
        return this;
    }
}
