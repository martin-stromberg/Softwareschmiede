# UI — Bestandsaufnahme

## `SettingsView.xaml`
Datei: `src/Softwareschmiede.App/Views/SettingsView.xaml` (741 Zeilen)

### Registerstruktur (Stand heute)
Das `TabControl` (Zeile 206) enthält **drei** `TabItem` — es gibt noch **kein** Register „Automatisierung" und kein separates Register „IDE"/„Benachrichtigungen" (IDE-Plugins sind Teil des „Plugins"-Registers):

1. **„Allgemein"** (Zeilen 237–498), `ScrollViewer` + `StackPanel`, Abschnitte:
   - „Design" (ComboBox `DesignMode`, Z. 247–262)
   - „Arbeitsverzeichnis" (TextBox `Arbeitsverzeichnis`, Z. 264–277)
   - **„SCM-Ereignisse" (Z. 279–429 — der zu verschiebende Block)**
   - „Updates" inkl. „Update-Pfad" und Kanal-Anzeige (Z. 431–478)
   - „Diagnose" (Button `KonsolenTestOeffnen`, Z. 480–495)
2. **„Plugins"** (Z. 499–668): zweispaltig — links Standard-Plugin-Auswahl (SCM `DefaultScmPlugin`, KI `DefaultKiPlugin`) und Aktivierungslisten (SCM, KI, IDE `IdePluginListe` mit ↑/↓-Buttons), rechts `PluginDetailPanel` (`IsScmKiPluginContentVisible`/`IsIdePluginContentVisible` via `BoolToVisibilityConverter`).
3. **„Promptvorlagen"** (Z. 669–738): `ItemsControl` über `PromptVorlagen` mit Hinzufügen-/Löschen-Buttons.

### Abschnitt „SCM-Ereignisse" (Z. 279–429) — Inhalte und Bindungen

| Zeile | Control | `AutomationProperties.Name` | Binding | Heutiges Label |
|-------|---------|------------------------------|---------|----------------|
| 285–288 | `CheckBox` | `IsScmMonitoringEnabled` | `IsChecked={Binding IsScmMonitoringEnabled, TwoWay}` | „SCM-Ereignisüberwachung aktivieren" |
| 289–292 | `TextBlock` | — | — | Beschreibung zum Schalter |
| 293–297 | `CheckBox` | `IsScmTaskCompletionEnabled` | `IsChecked={Binding IsScmTaskCompletionEnabled, TwoWay}` | „Abschlussüberwachung ereignisgesteuerter Aufgaben aktivieren" |
| 302–311 | `TextBlock` + `TextBox` | `ScmTaskCompletionWaitMinutes` | `Text={Binding ScmTaskCompletionWaitMinutes}` | „Wartezeit bis zum Beenden (Minuten)" — **immer sichtbar und aktiv** (keine `Visibility`/`IsEnabled`-Bindung) |
| 316–323 | `TextBlock` + `TextBox` | `ScmTriggerKeyword` | `Text={Binding ScmTriggerKeyword}` | „Trigger-Keyword" — **immer sichtbar** (keine modusabhängige Regel) |
| 328–346 | `TextBlock` + `ComboBox` | `Backmerge-Modus` | `SelectedItem={Binding SelectedBackmergeModus}`, `ItemsSource={Binding BackmergeModusOptionen}`, `ItemTemplate` → `Label` | „Backmerge-Pull-Requests" |
| 347–365 | `TextBlock` + `ComboBox` | `Konto-Modus` | `SelectedItem={Binding SelectedKontoModus}`, `ItemsSource={Binding KontoModusOptionen}` | „Konto-Modus" — Beschreibungstext enthält den Hinweis „Im separaten Modus … werden Backmerge-Pull-Requests dann nicht automatisch entdeckt" (Z. 362), der laut Anforderung entfällt |
| 366–428 | 5× `TextBlock` + `TextBox` | `ScmPromptAppendixGeneral`, `ScmPromptAppendixIssue`, `ScmPromptAppendixIssueComment`, `ScmPromptAppendixPullRequest`, `ScmPromptAppendixPullRequestComment` | `Text={Binding ScmPromptAppendix*}` | „Prompt-Anhänge" — **immer sichtbar** (keine Aktivierungsregel) |

Es existieren **keine** `Visibility`-/`IsEnabled`-Bindungen in diesem Abschnitt — alle Felder sind unabhängig von Schalter-/Modus-Zustand sichtbar und aktiv (Anforderung: Aufräumarbeit — neue Sichtbarkeits-/Aktivierungsregeln). Vorhandene Converter-Infrastruktur: `BoolToVisibilityConverter` und `NullOrEmptyToVisibilityConverter` werden an anderer Stelle derselben View genutzt (z. B. Z. 191, 198, 651, 659).

### Ribbon/Meldungen
- Ribbon-Gruppe „Projekt" (Z. 167–180): `RibbonLargeButton` „Speichern" (`SpeichernCommand`), „Verwerfen" (`VerwerfenCommand`) — gelten seitenweit, also auch für das neue Register.
- Erfolgs-/Fehlerbanner (Z. 186–203): `ErfolgsMeldung`, `FehlerMeldung` (`AutomationProperties.Name="FehlerMeldung"`).

## `Expander`-Muster (für die drei Panels)

| Stelle | Verwendung |
|--------|-----------|
| `src/Softwareschmiede.App/Views/ProjectDetailView.xaml` Z. 392–431 | `<Expander Header="Beendete Aufgaben" IsExpanded="False" AutomationProperties.Name="BeendeteAufgabenExpander">` mit `Foreground="{DynamicResource PrimaryTextBrush}"` — auf-/zuklappbares Panel, zugeklappt initial. |
| `src/Softwareschmiede.App/Views/ScmEventQueueView.xaml` Z. 137–141 | `<Expander Header="Integrationshinweise" Visibility="{Binding Diagnostics, Converter={StaticResource NullOrEmptyToVisibilityConverter}}">` — Expander mit statischem Header und sichtbarkeitsgebundenem Inhalt. |

Keine der bestehenden Expander bindet einen Status-Text in die Titelleiste — die geforderte Statusanzeige („aktiviert"/„deaktiviert") in der Panel-Titelleiste wäre neu.

## `ScmEventQueueView.xaml` (Querverweis)
Texte in `ScmEventQueueViewModel` verweisen auf „Einstellungen → Allgemein → SCM-Ereignisse" (`RetrySourcesCommand`-Hinweis und `MonitoringNotice`, siehe [logic.md](logic.md)) — nach dem Umzug des Abschnitts sind diese Pfadangaben fachlich veraltet.

## E2E-Page-Object
Datei: `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`

- `SwitchTab(tabName)` klickt `ByName(tabName)` — Tab-Header werden über sichtbaren Text gefunden („Allgemein", „Plugins", "Promptvorlagen"); die `TabItem`-Header sind `StackPanel`s aus `Ellipse`/`TextBlock` ohne eigenen `AutomationProperties.Name`.
- `GetKontoModus`/`SetKontoModus`/`WaitForKontoModus` nutzen `ByName("Konto-Modus")` + `ControlType.ComboBox` — Selektor wird bei Umbenennung des Labels/AutomationNames anzupassen sein.
- `E2E_SettingsFeatureFlags.cs` → `Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E` (in `MainTest.General_BasisUndEinstellungen_E2E` konsolidiert): `WaitForElement(ByName("IsScmMonitoringEnabled"))` + `Assert.Null(ByName("Automatisierung"))` + `Assert.Null(ByName("IsAutonomAufgabenEnabled"))` — prüft heute explizit das **Fehlen** eines „Automatisierung"-Bereichs.
