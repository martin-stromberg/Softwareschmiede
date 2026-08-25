# Offene Aufgaben

Erstellt am: 2026-08-25
Aktualisiert am: 2026-08-25 (Fortsetzungslauf 2)
Abbruchgrund (ursprünglich): Maximale Iterationsanzahl erreicht (3 von 3)

## Offene Planelemente

Keine (Plan-Review-Status: „Vollständig umgesetzt").

## Code-Review-Befunde

- [x] **Namenskonventionen** (`E2E_ViewPattern.cs`): Alle 9 `_E2E`-Szenariomethoden auf die deutsche
  `Handlung_ErwartetesErgebnis_E2E`-Konvention umbenannt (`RunViewPatternHappyPath_E2E` →
  `ViewPatternHappyPath_NavigiertUndErstelltKorrekt_E2E`, `RecognizeViewsCorrectly_E2E` →
  `AnsichtenErkennung_LiefertKorrekteViewTypen_E2E`, `MenuNavigationWorks_E2E` →
  `MenueNavigation_WechseltZwischenAnsichten_E2E`, `ForceShowNavigatesCorrectly_E2E` →
  `ForceShow_NavigiertKorrektZuAnsicht_E2E`, `ForceCloseWithoutRecursion_E2E` →
  `ForceClose_OhneRekursion_SchliesstNurEineEbene_E2E`, `ForceCloseWithRecursion_E2E` →
  `ForceClose_MitRekursion_SchliesstBisDashboard_E2E`, `RecognizeDialogsCorrectly_E2E` →
  `DialogErkennung_LiefertKorrekteDialogViewTypen_E2E`,
  `UnrecognizedViewThrowsDetailedException_E2E` → `UnbekannteAnsicht_WirftAussagekraeftigeException_E2E`,
  `RecognizeErrorViewCorrectly_E2E` → `FehlerAnsichtErkennung_ZeigtFehlermeldung_E2E`), konsistent in
  `E2E_ViewPattern.cs` und `MainTest.cs` angepasst. Build sauber verifiziert.
- [x] **Doppelter Code** (`ProjectListView`/`ProjectDetailView`/`TaskDetailView` vs. `WpfTestBase`):
  `WpfTestBase.CreateProject`, `OpenProject`, `DeleteCurrentProject`, `DeleteCurrentTask`,
  `AufgabeDetailZurueck` und `OffeneAufgabenItems` delegieren jetzt intern an die entsprechenden
  `Views.ProjectListView`/`Views.ProjectDetailView`/`Views.TaskDetailView`-Methoden (Komposition), statt
  die Klick-/Warte-Sequenz ein zweites Mal zu implementieren. Da `Softwareschmiede.App.Views` (Produktivcode)
  und `Softwareschmiede.Tests.E2E.Views` (Testschicht) gleichnamige Klassen (`ProjectListView` etc.)
  enthalten, wurden die Aufrufe und betroffenen `<see cref=...>`-Doku-Verweise vollqualifiziert, um
  CS0104-Mehrdeutigkeiten zu vermeiden. Build sauber (0 Warnungen/Fehler).
- [x] **Dokumentationskonvention** (`ElementWaitHelper.cs`): `<returns>`-Tags an den statischen Feldern
  `Short`/`Medium` entfernt (nur `<summary>`, analog zu `WpfTestBase.Short`/`Medium`/`Long`).

## Fehlgeschlagene Tests

- [x] `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` — **Root Cause gefunden und behoben (echter
  Produktivcode-Bug, kein Testfehler).**

  **Reproduktion:** Über einen temporären, isolierten Diagnose-Test (nicht Teil der finalen Suite, nach
  Gebrauch wieder entfernt) wurde der FlaUI-Automation-Baum vor und nach `Menu.NavigateToProjects()`
  gedumpt (Name/ControlType/IsOffscreen/BoundingRectangle jedes sichtbaren Elements). Ergebnis: Der Dump
  vor und nach dem Klick auf "Projekte" war **byte-identisch** — inklusive aller `ProjectListView`-Marker
  ("Neu"-Projekt-Button, Projekt-Kacheln, "Unzugeordnete Repositories"-Panel) UND aller
  `TaskDetailView`-Marker ("EditTitel", "Zurück", "Speichern" etc.) gleichzeitig. `CurrentView()` erkannte
  entsprechend reproduzierbar `TaskDetailView` statt `ProjectListView`.

  **Tatsächliche Root Cause** (verifiziert durch Code-Analyse, nicht die ursprüngliche Offscreen-Hypothese):
  `ProjectListView.xaml` (Produktivcode) rendert die Projekt-/Aufgabendetailansicht als
  **Vollbild-Overlay** innerhalb der Projektliste selbst (`<ContentControl Content="{Binding
  DetailViewModel}" ... />`), nicht über einen Wechsel des Top-Level-`MainWindowViewModel.CurrentView`.
  `MainWindowViewModel.NavigateToProjectList()` (aufgerufen ausschließlich vom Seitenleisten-Button
  "Projekte") setzt beim Wiederverwenden des gecachten `_projectListViewModel` lediglich
  `CurrentView = _projectListViewModel`, ohne dessen `DetailViewModel` zurückzusetzen. Wurde zuvor eine
  Aufgabe "fensterumfassend" geöffnet und nie über "Zurück" geschlossen (z. B. am Ende von
  `TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E`), bleibt das zugehörige
  `TaskDetailViewModel` in `_projectListViewModel.DetailViewModel` bestehen. Ein Klick auf "Projekte"
  zeigt dadurch weiterhin die alte Aufgabenansicht statt der Projektliste — ein echter, vorbestehender
  Navigations-Bug, der durch die neue View-Pattern-Testsuite lediglich erstmals sichtbar/erkannt wurde.

  **Fix** (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`, `NavigateToProjectList()`): Beim
  Wiederverwenden von `_projectListViewModel` wird jetzt, falls `DetailViewModel` noch gesetzt ist, das
  bereits vorhandene, für genau diesen Zweck vorgesehene `_projectListViewModel.SchliesseDetailCommand`
  ausgeführt (setzt `DetailViewModel = null` und disposed dabei korrekt sowohl das aktuelle als auch ein
  ggf. zurückgehaltenes `ProjectDetailViewModel` — siehe vorhandene Kommentare im `DetailViewModel`-Setter
  in `ProjectListViewModel.cs`, die genau diesen Bypass-Pfad bereits antizipieren).

  **Verifikation:** Der isolierte Diagnose-Test wurde nach dem Fix erneut ausgeführt: Der "Neu"-Dump nach
  `NavigateToProjects()` enthält keine `TaskDetailView`-Marker mehr, `CurrentView()` erkennt korrekt
  `ProjectListView` (sofort und nach 2s Pause, keine Race Condition). Voller Build sauber (0
  Warnungen/Fehler). Reguläre Testlane (`--filter "Category!=OsInterface"`) erneut grün: 1398 bestanden,
  1 übersprungen, 0 fehlgeschlagen.

  **Nicht abschließend verifiziert in dieser Sandbox:** Der volle `RunGeneralTests`-Testlauf (inkl. der
  View-Pattern-Szenarien) scheitert weiterhin — aber an einer *anderen*, unveränderten Stelle *vor* den
  View-Pattern-Szenarien: `AutonomAufgabeInitialisierung_DialogErstelltArbeitsverzeichnisUndZeigtDetailAnsicht_E2E`
  läuft nach 30s in ein `TimeoutException`, weil `%APPDATA%\AutonomAufgaben` inzwischen 348 verwaiste
  Verzeichnisse aus wiederholten Sandbox-Testläufen enthält (siehe Hinweis unten — dasselbe, bereits in
  der vorherigen Iteration dokumentierte Umgebungsproblem, jetzt weiter angewachsen). Dadurch werden die
  View-Pattern-Szenarien im vollen Testlauf in dieser Sandbox nicht erneut erreicht. Die Behebung des
  `CurrentView()`-Bugs selbst ist davon unabhängig durch den isolierten Diagnose-Test **vor und nach dem
  Fix** direkt am echten Automation-Baum belegt (siehe oben) — nicht spekulativ.

## Hinweis zur Testumgebung (kein Code-Befund, aber blockiert die volle End-zu-Ende-Verifikation)

`%APPDATA%\AutonomAufgaben` enthält aktuell 348 verwaiste Verzeichnisse aus wiederholten
Sandbox-Testläufen (Altlast, nicht durch diesen Branch verursacht, wächst mit jedem weiteren Lauf, der
`AutonomAufgabeInitialisierung_..._E2E` erreicht). Dies verlangsamt/blockiert diesen Test (Timeout nach
30s) und verhindert dadurch in `RunGeneralTests`, dass die nachfolgenden View-Pattern-Szenarien im vollen
Testlauf erneut erreicht werden.

**Wurde in diesem Lauf NICHT gelöscht** (weisungsgemäß laut CLAUDE.md — könnte potenziell Daten der
Self-Hosting-Instanz enthalten). Der Anwender müsste entweder:
- `%APPDATA%\AutonomAufgaben` selbst bereinigen (oder Bereinigung explizit freigeben), oder
- den vollen `RunGeneralTests`-Lauf inkl. View-Pattern-Szenarien selbst/außerhalb dieser Sandbox einmal
  bestätigen.

Der eigentliche, im Titel dieses Dokuments referenzierte Funktionsfehler (`CurrentView()`-Fehlerkennung)
gilt nach Code-Analyse und isolierter Reproduktion/Verifikation als behoben.
