using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für das persistente Navigationsmenü der Anwendung (Dashboard/Projekte/Einstellungen).</summary>
public sealed class MenuView : BaseWindowView
{
    private static readonly string[] DashboardButtonNamen = ["Dashboard"];
    private static readonly string[] ProjekteButtonNamen = [" Projekte", "Projekte"];
    private static readonly string[] EinstellungenButtonNamen = [" Einstellungen", "Einstellungen"];

    /// <summary>AutomationId der Aufgabenliste in der Seitenleiste.</summary>
    private const string AufgabenListeAutomationId = "AktiveAufgabenListeMenue";

    /// <summary>Namenspräfix der Modus-Umschalter der Seitenleisten-Aufgabenliste.</summary>
    private const string AufgabenModusPraefix = "AufgabenModus:";

    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public MenuView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("Dashboard"))
           && TryFindNavigationButton(ProjekteButtonNamen) is not null
           && TryFindNavigationButton(EinstellungenButtonNamen) is not null;

    /// <inheritdoc/>
    public override MenuView ForceShow() => this;

    /// <inheritdoc/>
    public override MenuView ForceClose(bool recurseToDashboard) => this;

    /// <summary>Klickt den "Dashboard"-Button und wartet auf den Dashboard-Seitentitel.</summary>
    /// <returns>Die Dashboard-Ansicht.</returns>
    public DashboardView NavigateToDashboard()
    {
        var dashboard = new DashboardView(Window);
        NavigiereBisSichtbar(
            DashboardButtonNamen,
            () => dashboard.IsVisible,
            "Dashboard sollte nach Klick auf 'Dashboard' sichtbar sein.");
        return dashboard;
    }

    /// <summary>Klickt den "Projekte"-Button und wartet auf die Projektliste.</summary>
    /// <returns>Die Projektlisten-Ansicht.</returns>
    public ProjectListView NavigateToProjects()
    {
        var projectList = new ProjectListView(Window);
        NavigiereBisSichtbar(
            ProjekteButtonNamen,
            () => projectList.IsVisible,
            "Projektliste sollte nach Klick auf 'Projekte' sichtbar sein.");
        return projectList;
    }

    /// <summary>Klickt den "Einstellungen"-Button und wartet auf die Einstellungs-Tabs.</summary>
    /// <returns>Die Einstellungen-Ansicht.</returns>
    public SettingsView NavigateToSettings()
    {
        var settings = new SettingsView(Window);
        NavigiereBisSichtbar(
            EinstellungenButtonNamen,
            () => settings.IsVisible,
            "Einstellungen sollten nach Klick auf 'Einstellungen' sichtbar sein.");
        return settings;
    }

    /// <returns>Der in der Fußzeile der Navigations-Seitenleiste angezeigte Versionstext ("AppVersionText").</returns>
    public string GetVersionText() => WaitForElement(Window, cf => cf.ByAutomationId("AppVersionText"), Short).Name;

    /// <summary>Liest den Anzahl-Text des Auto-Shutdown-Blocks ("AutoShutdownLaufendeAnzahl").</summary>
    /// <returns>Der Anzeigetext oder <c>null</c>, wenn das Element nicht sichtbar ist.</returns>
    public string? GetAutoShutdownLaufendeAnzahlText()
        => LiesElementText("AutoShutdownLaufendeAnzahl");

    /// <summary>Wartet, bis der Anzahl-Text des Auto-Shutdown-Blocks <paramref name="teilText"/> enthält.</summary>
    /// <param name="teilText">Der erwartete Teiltext (z. B. "1 laufende").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Der vollständige Anzeigetext.</returns>
    /// <exception cref="TimeoutException">Der Text erschien nicht rechtzeitig.</exception>
    public string WaitForAutoShutdownLaufendeAnzahlText(string teilText, TimeSpan timeout)
        => WarteAufElementText("AutoShutdownLaufendeAnzahl", teilText, timeout);

    /// <summary>Gibt an, ob die Aktivierungs-Checkbox des Auto-Shutdowns sichtbar ist.</summary>
    /// <returns><c>true</c>, wenn die Checkbox vorhanden und nicht ausgeblendet ist.</returns>
    public bool IsAutoShutdownAktivierenVisible()
        => IstElementSichtbar("AutoShutdownAktivieren");

    /// <summary>Wartet, bis die Aktivierungs-Checkbox des Auto-Shutdowns nicht mehr sichtbar ist.</summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Checkbox blieb sichtbar.</exception>
    public MenuView WaitForAutoShutdownAktivierenVerschwunden(TimeSpan timeout)
        => WarteAufElementUnsichtbar("AutoShutdownAktivieren", timeout);

    /// <summary>Liest den gesetzten Zustand der Aktivierungs-Checkbox (<c>null</c> bei nicht gefundener Checkbox).</summary>
    /// <returns>Der Checked-Zustand oder <c>null</c>.</returns>
    public bool? GetAutoShutdownAktiviertChecked()
        => GetCheckBoxChecked("AutoShutdownAktivieren");

    /// <summary>Setzt die Aktivierungs-Checkbox auf den gewünschten Zustand (Togglen nur bei Abweichung).</summary>
    /// <param name="aktiviert">Der Zielzustand.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Checkbox wurde nicht rechtzeitig sichtbar.</exception>
    public MenuView SetzeAutoShutdownAktiviert(bool aktiviert)
        => SetzeCheckBoxZustand(
            "AutoShutdownAktivieren",
            aktiviert,
            "Die Auto-Shutdown-Aktivierungs-Checkbox wurde nicht rechtzeitig sichtbar.");

    /// <summary>Gibt an, ob das Toleranz-Textfeld sichtbar ist (Optionen nur bei aktiviertem Schalter).</summary>
    /// <returns><c>true</c>, wenn das Feld vorhanden und nicht ausgeblendet ist.</returns>
    public bool IsAutoShutdownToleranzVisible()
        => IstElementSichtbar("AutoShutdownToleranzMinuten");

    /// <summary>
    /// Wartet, bis das Toleranz-Textfeld sichtbar ist. Das Options-Panel blendet sich über
    /// <c>AutoShutdownOptionenSichtbar</c> frühestens im selben Render-Pass wie die
    /// Aktivierungs-Checkbox ein — nach <see cref="SetzeAutoShutdownAktiviert"/> kann ein
    /// direkter Read das Feld daher noch nicht sehen.
    /// </summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Das Toleranz-Feld wurde nicht rechtzeitig sichtbar.</exception>
    public MenuView WaitForAutoShutdownToleranzSichtbar(TimeSpan timeout)
    {
        WaitForElement(Window, cf => cf.ByAutomationId("AutoShutdownToleranzMinuten"), timeout);
        return this;
    }

    /// <summary>Setzt den Toleranzwert (Minuten) im Auto-Shutdown-Optionsfeld.</summary>
    /// <param name="minuten">Der einzugebende Wert als Text.</param>
    /// <returns>Diese Instanz.</returns>
    public MenuView SetzeAutoShutdownToleranz(string minuten)
    {
        var feld = WaitForElement(Window, cf => cf.ByAutomationId("AutoShutdownToleranzMinuten"), Medium).AsTextBox();
        feld.Focus();
        feld.Text = minuten;
        return this;
    }

    /// <summary>Liest den aktuellen Text des Toleranz-Feldes.</summary>
    /// <returns>Der Feldtext oder <c>null</c>, wenn das Feld nicht gefunden wurde.</returns>
    public string? GetAutoShutdownToleranzText()
        => Window.FindFirstDescendant(cf => cf.ByAutomationId("AutoShutdownToleranzMinuten"))
            ?.AsTextBox().Text;

    /// <summary>Liest den gesetzten Zustand der Erzwingen-Checkbox (<c>null</c> bei nicht gefundener Checkbox).</summary>
    /// <returns>Der Checked-Zustand oder <c>null</c>.</returns>
    public bool? GetAutoShutdownErzwingenChecked()
        => GetCheckBoxChecked("AutoShutdownErzwingen");

    /// <summary>Setzt die Erzwingen-Checkbox auf den gewünschten Zustand (Togglen nur bei Abweichung).</summary>
    /// <param name="erzwingen">Der Zielzustand.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Die Checkbox wurde nicht rechtzeitig sichtbar.</exception>
    public MenuView SetzeAutoShutdownErzwingen(bool erzwingen)
        => SetzeCheckBoxZustand(
            "AutoShutdownErzwingen",
            erzwingen,
            "Die Auto-Shutdown-Erzwingen-Checkbox wurde nicht rechtzeitig sichtbar.");

    /// <summary>Liest den Countdown-Text des Auto-Shutdown-Blocks ("AutoShutdownCountdown").</summary>
    /// <returns>Der Anzeigetext oder <c>null</c>, wenn kein Countdown sichtbar ist.</returns>
    public string? GetAutoShutdownCountdownText()
        => LiesElementText("AutoShutdownCountdown");

    /// <summary>Wartet, bis der Countdown-Text sichtbar ist und <paramref name="teilText"/> enthält.</summary>
    /// <param name="teilText">Der erwartete Teiltext (z. B. "Herunterfahren in").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Der vollständige Anzeigetext.</returns>
    /// <exception cref="TimeoutException">Der Countdown-Text erschien nicht rechtzeitig.</exception>
    public string WaitForAutoShutdownCountdownText(string teilText, TimeSpan timeout)
        => WarteAufElementText("AutoShutdownCountdown", teilText, timeout);

    /// <summary>Wartet, bis kein Countdown-Text mehr sichtbar ist.</summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Der Countdown-Text blieb sichtbar.</exception>
    public MenuView WaitForAutoShutdownCountdownVerschwunden(TimeSpan timeout)
        => WarteAufElementVerschwunden("AutoShutdownCountdown", timeout);

    /// <summary>
    /// Klickt den "Programmupdate prüfen"-Button der Seitenleiste. Der Button ist erst nach dem
    /// Laden der Update-Einstellungen aktiviert - der Klick wartet daher begrenzt auf die
    /// Aktivierung, damit ein Klick nicht wirkungslos auf einen noch gesperrten Button trifft.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public MenuView ClickUpdatePruefen()
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            var button = Window.FindFirstDescendant(
                cf => cf.ByName("Programmupdate prüfen").And(cf.ByControlType(ControlType.Button)));
            if (button is not null && !button.IsOffscreen && button.IsEnabled)
            {
                try
                {
                    button.ClickInForeground();
                    return this;
                }
                catch (FlaUI.Core.Exceptions.FlaUIException)
                {
                    // Element kann zwischen Prüfung und Klick kurzzeitig keinen klickbaren
                    // Punkt haben (Layout-Umbau) - nächster Schleifendurchlauf versucht erneut.
                }
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Der 'Programmupdate prüfen'-Button wurde nicht rechtzeitig aktiviert.");
    }

    /// <summary>Klickt den "Programmupdate starten"-Button der Seitenleiste (nur bei Updateangebot sichtbar).</summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Der Button ist nicht sichtbar, weil kein Update angeboten wird.</exception>
    public MenuView ClickUpdateStarten()
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            var button = Window.FindFirstDescendant(
                cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
            if (button is not null && !button.IsOffscreen)
            {
                try
                {
                    button.ClickInForeground();
                    return this;
                }
                catch (FlaUI.Core.Exceptions.FlaUIException)
                {
                    // Element kann zwischen Prüfung und Klick kurzzeitig keinen klickbaren
                    // Punkt haben (Layout-Umbau) - nächster Schleifendurchlauf versucht erneut.
                }
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Der 'Programmupdate starten'-Button wurde nicht rechtzeitig sichtbar.");
    }

    /// <summary>Gibt an, ob der "Programmupdate starten"-Button aktuell sichtbar ist (Updateangebot liegt an).</summary>
    /// <returns><c>true</c>, wenn der Button im Automation-Baum vorhanden und nicht ausgeblendet ist.</returns>
    public bool IsUpdateStartButtonVisible()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        return button is not null && !button.IsOffscreen;
    }

    /// <summary>Gibt an, ob der "Programmupdate starten"-Button sichtbar und aktiviert ist.</summary>
    /// <returns><c>true</c>, wenn der Button sichtbar und klickbar ist.</returns>
    public bool IsUpdateStartButtonEnabled()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        return button is not null && !button.IsOffscreen && button.IsEnabled;
    }

    /// <summary>Gibt an, ob der "Programmupdate prüfen"-Button aktuell aktiviert ist.</summary>
    /// <returns><c>true</c>, wenn der Button klickbar ist.</returns>
    public bool IsUpdatePruefenButtonEnabled()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate prüfen").And(cf.ByControlType(ControlType.Button)));
        return button is not null && button.IsEnabled;
    }

    /// <summary>
    /// Liest die angebotene Update-Version aus dem ToolTip des "Programmupdate starten"-Buttons.
    /// WPF bildet <c>ToolTip</c> auf die UIA-Eigenschaft <c>HelpText</c> ab; diese enthält den
    /// gebundenen Rohwert (die Versionszeichenkette), nicht die StringFormat-Maske
    /// ("Update auf Version {0} vorbereiten"). Beide Formen werden akzeptiert.
    /// </summary>
    /// <returns>Die angebotene Versionszeichenkette oder <c>null</c>, wenn kein Update angeboten wird.</returns>
    public string? GetOfferedUpdateVersion()
    {
        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        if (button is null || button.IsOffscreen)
            return null;

        var helpText = button.HelpText;
        const string prefix = "Update auf Version ";
        const string suffix = " vorbereiten";
        if (helpText is not null
            && helpText.StartsWith(prefix, StringComparison.Ordinal)
            && helpText.EndsWith(suffix, StringComparison.Ordinal))
        {
            return helpText[prefix.Length..^suffix.Length];
        }

        // UIA liefert in HelpText den gebundenen Rohwert (die Version), nicht die
        // StringFormat-Maske - der nicht maskierte Text ist das Updateangebot.
        return string.IsNullOrEmpty(helpText) ? null : helpText;
    }

    /// <summary>
    /// Wartet, bis der "Programmupdate starten"-Button erscheint, und liefert die angebotene Version.
    /// </summary>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Die angebotene Versionszeichenkette.</returns>
    /// <exception cref="TimeoutException">Es wurde kein Update angeboten.</exception>
    public string WaitForOfferedUpdateVersion(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var version = GetOfferedUpdateVersion();
            if (version is not null)
                return version;

            Thread.Sleep(200);
        }

        var button = Window.FindFirstDescendant(
            cf => cf.ByName("Programmupdate starten").And(cf.ByControlType(ControlType.Button)));
        throw new TimeoutException(
            $"Innerhalb von {timeout.TotalSeconds}s wurde kein Updateangebot angezeigt. " +
            $"Diagnose: Button={(button is null ? "nicht gefunden" : $"gefunden, IsOffscreen={button.IsOffscreen}, HelpText='{button.HelpText}'")}, " +
            $"Hinweis='{GetUpdateHinweis()}'");
    }

    /// <summary>Liest den aktuellen "UpdateHinweis"-Text der Seitenleiste.</summary>
    /// <returns>Der Hinweistext oder <c>null</c>, wenn kein Hinweis angezeigt wird.</returns>
    /// <remarks>
    /// Das Element trägt kein <c>AutomationProperties.Name</c> - der gebundene Hinweistext ist
    /// daher als UIA-Name des TextBlocks lesbar.
    /// </remarks>
    public string? GetUpdateHinweis()
    {
        var element = Window.FindFirstDescendant(cf => cf.ByAutomationId("UpdateHinweis"));
        if (element is null || element.IsOffscreen)
            return null;

        return element.Name;
    }

    /// <summary>Wartet, bis der "UpdateHinweis" einen Text enthält, der <paramref name="teilText"/> umfasst.</summary>
    /// <param name="teilText">Der erwartete Teiltext.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Der vollständige Hinweistext.</returns>
    /// <exception cref="TimeoutException">Der Hinweis erschien nicht rechtzeitig.</exception>
    public string WaitForUpdateHinweis(string teilText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? zuletzt = null;
        while (DateTime.UtcNow < deadline)
        {
            zuletzt = GetUpdateHinweis();
            if (zuletzt is not null && zuletzt.Contains(teilText, StringComparison.Ordinal))
                return zuletzt;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"UpdateHinweis enthielt nicht innerhalb von {timeout.TotalSeconds}s '{teilText}'. Zuletzt gesehen: '{zuletzt}'.");
    }

    /// <summary>
    /// Wechselt über die Aufgabenliste in der Seitenleiste ("Aktive Aufgaben") direkt zur angegebenen
    /// Aufgabe, ohne über "Zurück" zu navigieren.
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Die Aufgabendetailansicht der Zielaufgabe.</returns>
    public TaskDetailView NavigateToTask(string taskTitle)
    {
        WaitForElement(AktiveAufgabenListeMenueElement(), cf => cf.ByName($"AufgabeNavigieren:{taskTitle}"), Medium).AsButton().ClickInForeground();
        return new TaskDetailView(Window);
    }

    /// <summary>
    /// Klappt die Navigation aus, falls sie eingeklappt ist (in der 48-px-Leiste sind die
    /// Modus-Umschalter der Aufgabenliste verborgen). Erkennt den Zustand an der Sichtbarkeit
    /// des 'AufgabenModus:Datum'-Umschalters.
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    public MenuView EnsureNavigationExpanded()
    {
        var deadline = DateTime.UtcNow + Medium;
        var toggleGeklickt = false;
        while (DateTime.UtcNow < deadline)
        {
            var modusButton = Window.FindFirstDescendant(cf => cf.ByName($"{AufgabenModusPraefix}Datum"));
            if (modusButton is not null && !modusButton.IsOffscreen)
                return this;

            if (!toggleGeklickt)
            {
                var toggle = Window.FindFirstDescendant(
                    cf => cf.ByName("☰").And(cf.ByControlType(ControlType.Button)));
                if (toggle is not null)
                {
                    toggle.ClickInForeground();
                    toggleGeklickt = true;
                }
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Die Navigation konnte nicht ausgeklappt werden (AufgabenModus:Datum nicht sichtbar).");
    }

    /// <summary>
    /// Schaltet den Anzeigemodus der Seitenleisten-Aufgabenliste um (Klick auf den
    /// 'AufgabenModus:{modus}'-Umschalter). Fensterweit eindeutig, da die Dashboard-Umschalter
    /// 'DashboardAufgabenModus:*' heißen.
    /// </summary>
    /// <param name="modus">Der anzuzeigende Modus ("Datum", "Repository" oder "KI").</param>
    /// <returns>Diese Instanz.</returns>
    public MenuView SetzeAufgabenListenModus(string modus)
    {
        KlickeAufgabenModusUmschalter(AufgabenModusPraefix, modus);
        return this;
    }

    /// <summary>
    /// Liest die Gruppennamen der Seitenleisten-Aufgabenliste im gruppierten Modus
    /// (UIA-Namen 'AufgabenGruppe:{Name}' der Gruppenheader, gescoped unter 'AktiveAufgabenListeMenue').
    /// </summary>
    /// <returns>Die Gruppennamen ohne das 'AufgabenGruppe:'-Präfix; leer im flachen Modus.</returns>
    public string[] GetAufgabenGruppenNamen() => GetAufgabenGruppenNamen(AufgabenListeAutomationId);

    /// <summary>
    /// Wartet, bis der Gruppenheader 'AufgabenGruppe:{name}' in der Seitenleisten-Aufgabenliste
    /// erscheint (gescoped unter 'AktiveAufgabenListeMenue').
    /// </summary>
    /// <param name="name">Der erwartete Gruppenname (z. B. ein Repository-Name).</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Der Gruppenheader erschien nicht rechtzeitig.</exception>
    public MenuView WaitForAufgabenGruppe(string name, TimeSpan timeout)
    {
        WaitForAufgabenGruppe(AufgabenListeAutomationId, name, timeout);
        return this;
    }

    /// <summary>
    /// Liest den in der Seitenleiste für die angegebene aktive Aufgabe angezeigten KI-Plugin-Namen
    /// (HelpText "KI: {Name}" - der UIA-Name des Elements ist seit der Symbolzeile der Glyph).
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Der reine Plugin-Name ohne das "KI: "-Präfix.</returns>
    public string GetActiveTaskKiPluginName(string taskTitle)
    {
        var kiPluginText = WaitForElement(
            AktiveAufgabenListeMenueElement(),
            cf => cf.ByAutomationId($"AktiveAufgabeKiPlugin_{taskTitle}"),
            Short);
        var helpText = kiPluginText.HelpText;
        if (helpText is not null && helpText.StartsWith("KI: ", StringComparison.Ordinal))
        {
            return helpText.Substring(4);
        }

        throw new InvalidOperationException($"Für Aufgabe '{taskTitle}' wurde kein 'KI: '-Eintrag in der Seitenleiste gefunden. Gesehen: '{helpText}'.");
    }

    /// <summary>Liest den KI-Plugin-Glyph (UIA-Name des 'AktiveAufgabeKiPlugin_{titel}'-Elements) der Kachel.</summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Der angezeigte Glyph.</returns>
    public string GetActiveTaskKiPluginSymbol(string taskTitle)
        => WaitForElement(
            AktiveAufgabenListeMenueElement(),
            cf => cf.ByAutomationId($"AktiveAufgabeKiPlugin_{taskTitle}"),
            Short).Name;

    /// <summary>Liest den SCM-Plugin-Glyph (UIA-Name des 'AktiveAufgabeScmPlugin_{titel}'-Elements) der Kachel.</summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Der angezeigte Glyph.</returns>
    public string GetActiveTaskScmPluginSymbol(string taskTitle)
        => WaitForElement(
            AktiveAufgabenListeMenueElement(),
            cf => cf.ByAutomationId($"AktiveAufgabeScmPlugin_{taskTitle}"),
            Short).Name;

    /// <summary>
    /// Liest den Status-Glyph der Kachel. Der UIA-Name des Elements 'AufgabeStatus:{titel}' ist der
    /// E2E-Anker selbst - der sichtbare Text (Glyph) wird daher über das TextPattern gelesen.
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Der angezeigte Status-Glyph (z. B. "▶", "⏸", "✓").</returns>
    public string GetActiveTaskStatusGlyph(string taskTitle)
    {
        var element = WaitForElement(
            AktiveAufgabenListeMenueElement(),
            cf => cf.ByName($"AufgabeStatus:{taskTitle}"),
            Short);
        if (element.Patterns.Text.IsSupported)
        {
            return element.Patterns.Text.Pattern.DocumentRange.GetText(-1);
        }

        // Fallback: AutomationId-Element ohne Name-Überschreibung lesen.
        var glyphElement = AktiveAufgabenListeMenueElement()
            .FindFirstDescendant(cf => cf.ByAutomationId($"AktiveAufgabeStatusGlyph_{taskTitle}"));
        return glyphElement?.Name ?? string.Empty;
    }

    /// <summary>Klickt den 'OffeneTodos:{titel}'-Button innerhalb der Kachel (verschachtelter Button, löst keine Kachel-Navigation aus).</summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <returns>Diese Instanz.</returns>
    public MenuView KlickeOffeneTodosButton(string taskTitle)
    {
        WaitForElement(
            AktiveAufgabenListeMenueElement(),
            cf => cf.ByName($"OffeneTodos:{taskTitle}"),
            Medium).ClickInForeground();
        return this;
    }

    private AutomationElement AktiveAufgabenListeMenueElement()
    {
        var menue = Window.FindFirstDescendant(cf => cf.ByAutomationId(AufgabenListeAutomationId));
        return menue ?? throw new InvalidOperationException(
            $"Das Element '{AufgabenListeAutomationId}' wurde im Hauptfenster nicht gefunden.");
    }

    /// <summary>
    /// Wartet, bis die Status-Kachel der Aufgabe in der Seitenleiste den erwarteten Status-Text als
    /// <c>AutomationProperties.HelpText</c> anzeigt (siehe ActiveTasksListControl.xaml).
    /// </summary>
    /// <param name="taskTitle">Der Titel der Aufgabe.</param>
    /// <param name="expectedStatus">Der erwartete Status-Text (z. B. "▶ Läuft").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn der erwartete Status nicht rechtzeitig erscheint.</exception>
    public MenuView WaitForTaskStatus(string taskTitle, string expectedStatus, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastStatus = null;
        while (DateTime.UtcNow < deadline)
        {
            var statusElement = AktiveAufgabenListeMenueElement().FindFirstDescendant(cf => cf.ByName($"AufgabeStatus:{taskTitle}"));
            lastStatus = statusElement?.HelpText;
            if (lastStatus == expectedStatus)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Statuskachel zeigte innerhalb von {timeout.TotalSeconds}s nicht den erwarteten Status '{expectedStatus}' an. Zuletzt gesehen: '{lastStatus}'.");
    }

    /// <summary>
    /// Klickt einen Navigations-Button und wartet, bis die Zielansicht sichtbar ist. Unter
    /// Systemlast kann ein einzelner Invoke-Klick ohne Reaktion bleiben (Element im UIA-Baum
    /// gefunden, die Command-Auslösung führte jedoch nicht zum Ansichtswechsel) - der Klick
    /// wird deshalb innerhalb des Gesamtzeitfensters in Intervallen wiederholt. Das ist für
    /// alle drei Navigationsziele idempotent (erneuter <c>CurrentView</c>-Wechsel auf dieselbe
    /// Ansicht; bei "Projekte" schließt ein Wiederholungsklick lediglich ein ggf. offenes
    /// Detail). Das Fehlerbanner-Fail-Fast ("FehlerMeldung") bleibt während des Wartens aktiv.
    /// </summary>
    /// <param name="buttonNamen">Akzeptierte UIA-Namen des Navigations-Buttons.</param>
    /// <param name="zielSichtbar">Prädikat für die sichtbare Zielansicht.</param>
    /// <param name="fehlertext">Assert-Meldung bei ausbleibender Zielansicht.</param>
    private void NavigiereBisSichtbar(
        IReadOnlyList<string> buttonNamen,
        Func<bool> zielSichtbar,
        string fehlertext)
    {
        // Erst auf den Button warten - fehlt er ganz, ist das eine andere Fehlerart als ein
        // verschluckter Klick und soll weiterhin mit eigenem Timeout diagnostiziert werden.
        WaitForNavigationButton(buttonNamen, Short);

        var deadline = DateTime.UtcNow + Medium;
        var naechsterKlick = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            if (zielSichtbar())
                return;

            var fehlerMeldung = Window.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
            if (fehlerMeldung is not null && !fehlerMeldung.IsOffscreen)
            {
                throw new InvalidOperationException(
                    $"In der Anwendung wird eine Fehlermeldung angezeigt: {GetHelpTextOrName(fehlerMeldung)}");
            }

            if (DateTime.UtcNow >= naechsterKlick)
            {
                try
                {
                    var button = TryFindNavigationButton(buttonNamen);
                    if (button is not null)
                        button.ClickInForeground();
                }
                catch (FlaUI.Core.Exceptions.FlaUIException)
                {
                    // Das Element kann zwischen Fund und Klick ersetzt werden - der nächste
                    // Schleifendurchlauf versucht erneut.
                }

                naechsterKlick = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            }

            Thread.Sleep(200);
        }

        Assert.True(zielSichtbar(), fehlertext + " " + TryFindNavigationButton(buttonNamen)?.DescribeClickTarget()
            + "; Window=" + Window.BoundingRectangle + "; Children=" + string.Join(" | ", Window.FindAllChildren().Select(e => e.ControlType + ":" + e.Name)));
    }

    private AutomationElement WaitForNavigationButton(IReadOnlyList<string> names, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var button = TryFindNavigationButton(names);
            if (button is not null)
                return button;

            Thread.Sleep(200);
        }

        var fensterTitel = string.Empty;
        var kinder = string.Empty;
        try
        {
            fensterTitel = Window.Title;
            kinder = string.Join(" | ", Window.FindAllChildren()
                .Select(k => $"{k.ControlType}:{k.Name}")
                .Take(20));
        }
        catch (Exception ex)
        {
            fensterTitel = $"<Fehler: {ex.GetType().Name}: {ex.Message}>";
        }

        throw new TimeoutException(
            $"Navigations-Button wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden. Gesucht: {string.Join(", ", names)}. " +
            $"Fenster: '{fensterTitel}'. Kinder: {kinder}");
    }

    private AutomationElement? TryFindNavigationButton(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            AutomationElement? button;
            try
            {
                button = Window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)));
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // UIA-Provider des Fensters transient nicht erreichbar - die
                // Warteschleife versucht es erneut statt hart zu scheitern.
                return null;
            }
            if (button is not null)
                return button;
        }

        return null;
    }

    /// <summary>Liest den UIA-Namen (Anzeigetext) eines Elements; <c>null</c>, wenn es fehlt oder ausgeblendet ist.</summary>
    private string? LiesElementText(string automationId)
    {
        var element = Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        return element is null || element.IsOffscreen ? null : element.Name;
    }

    /// <summary>Gibt an, ob ein Element vorhanden und nicht ausgeblendet ist.</summary>
    private bool IstElementSichtbar(string automationId)
    {
        var element = Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        return element is not null && !element.IsOffscreen;
    }

    /// <summary>Liest den Checked-Zustand einer Checkbox; <c>null</c>, wenn sie nicht gefunden wurde.</summary>
    private bool? GetCheckBoxChecked(string automationId)
        => Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsCheckBox().IsChecked;

    /// <summary>
    /// Setzt eine Checkbox auf den gewünschten Zustand: wartet auf Sichtbarkeit, klickt bei
    /// Abweichung (ggf. wiederholt) und kehrt erst zurück, wenn <c>IsChecked</c> den Zielzustand
    /// meldet — ein wirkungslos bleibender UIA-Klick wird so erkannt statt erst an späteren,
    /// nicht zusammenhängenden Asserts aufzuschlagen. Sonderfall <paramref name="ziel"/>
    /// <c>false</c>: Sichtbarkeitsregeln können die Checkbox synchron mit dem Ausschalten aus
    /// dem UIA-Baum kollabieren lassen (z. B. <c>AutoShutdownUiSichtbar</c> bei Anzahl 0 +
    /// Deaktivierung). War sie zuvor sichtbar und verschwindet nun, ist das Ausschalten damit
    /// bereits bewiesen — der Helper wartet nicht vergeblich auf einen lesbaren Zustand.
    /// </summary>
    /// <exception cref="TimeoutException">Die Checkbox wurde nicht rechtzeitig sichtbar oder erreichte den Zielzustand nicht.</exception>
    private MenuView SetzeCheckBoxZustand(string automationId, bool ziel, string fehlertext)
    {
        var deadline = DateTime.UtcNow + Medium;
        var naechsterKlick = DateTime.UtcNow;
        var gefunden = false;
        bool? zuletztIsChecked = null;
        while (DateTime.UtcNow < deadline)
        {
            var element = Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element is null || element.IsOffscreen)
            {
                // Verschwinden NACH dem ersten Sichtbarwerden beweist bei ziel == false
                // den ausgeschalteten Zustand (siehe Methoden-Dokumentation).
                if (!ziel && gefunden)
                {
                    return this;
                }
            }
            else
            {
                gefunden = true;
                var checkbox = element.AsCheckBox();
                try
                {
                    zuletztIsChecked = checkbox.IsChecked;
                }
                catch (FlaUI.Core.Exceptions.FlaUIException)
                {
                    // Das Element kollabierte zwischen Fund und Zustandsabfrage - der
                    // nächste Schleifendurchlauf bewertet den Baum erneut (siehe oben).
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // UIA-Provider des kollabierenden Elements transient nicht erreichbar.
                }

                if (zuletztIsChecked == ziel)
                {
                    return this;
                }

                // Erneuter Klick erst nach Abstand: UIA kann den Zustand kurz verzögert
                // melden; ein Sofort-Doppelklick würde den Zielzustand wieder zurückdrehen.
                if (DateTime.UtcNow >= naechsterKlick)
                {
                    try
                    {
                        checkbox.Click();
                        naechsterKlick = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                    }
                    catch (FlaUI.Core.Exceptions.FlaUIException)
                    {
                        // Kollabierendes Element ohne klickbaren Punkt - der nächste
                        // Schleifendurchlauf bewertet den Baum erneut.
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        // UIA-Provider transient nicht erreichbar - erneut versuchen.
                    }
                }
            }

            Thread.Sleep(200);
        }

        var diagnose = gefunden
            ? $" Zuletzt gesehen: IsChecked={zuletztIsChecked?.ToString() ?? "null"}, Ziel={ziel}."
            : string.Empty;
        throw new TimeoutException(fehlertext + diagnose);
    }

    /// <summary>Wartet, bis der Anzeigetext eines Elements <paramref name="teilText"/> enthält.</summary>
    /// <exception cref="TimeoutException">Der Text erschien nicht rechtzeitig.</exception>
    private string WarteAufElementText(string automationId, string teilText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? zuletzt = null;
        while (DateTime.UtcNow < deadline)
        {
            zuletzt = LiesElementText(automationId);
            if (zuletzt is not null && zuletzt.Contains(teilText, StringComparison.Ordinal))
                return zuletzt;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"{automationId} enthielt nicht innerhalb von {timeout.TotalSeconds}s '{teilText}'. Zuletzt gesehen: '{zuletzt}'.");
    }

    /// <summary>Wartet, bis ein Element keinen sichtbaren Anzeigetext mehr liefert.</summary>
    /// <exception cref="TimeoutException">Der Text blieb sichtbar.</exception>
    private MenuView WarteAufElementVerschwunden(string automationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (LiesElementText(automationId) is null)
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"{automationId} blieb länger als {timeout.TotalSeconds}s sichtbar. Zuletzt gesehen: '{LiesElementText(automationId)}'.");
    }

    /// <summary>Wartet, bis ein Element nicht mehr vorhanden oder ausgeblendet ist.</summary>
    /// <exception cref="TimeoutException">Das Element blieb sichtbar.</exception>
    private MenuView WarteAufElementUnsichtbar(string automationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!IstElementSichtbar(automationId))
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"{automationId} blieb länger als {timeout.TotalSeconds}s sichtbar.");
    }
}
