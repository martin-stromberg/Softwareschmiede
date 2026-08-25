# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### E2E_ViewPattern.cs (End2EndTest)

- **Namenskonventionen und Einheitlichkeit** — Alle neu eingeführten `_E2E`-Szenariomethoden (`RunViewPatternHappyPath_E2E`, `RecognizeViewsCorrectly_E2E`, `MenuNavigationWorks_E2E`, `ForceShowNavigatesCorrectly_E2E`, `ForceCloseWithoutRecursion_E2E`, `ForceCloseWithRecursion_E2E`, `RecognizeDialogsCorrectly_E2E`, `UnrecognizedViewThrowsDetailedException_E2E`, `RecognizeErrorViewCorrectly_E2E`) sind vollständig englisch benannt. Alle bereits vorhandenen `_E2E`-Szenariomethoden, die in derselben `RunGeneralTests`/`RunConPtyTests`-Sequenz aufgerufen werden (siehe `MainTest.cs`, z. B. `AppStarten_ZeigtVersionsTextInFusszeile_E2E`, `Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E`, `IdePluginSettings_AktivierungValidierungUndReihenfolge_E2E`, `Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E`, `TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E`, `CommandLineParameters_TextBoxSpeichertWertUndHilfeDialogFunktioniert_E2E`), folgen durchgängig dem Muster `Handlung_ErwartetesErgebnis_E2E` auf Deutsch. Die neuen Methodennamen brechen mit dieser im gesamten Testfile etablierten Konvention.

  Empfehlung: Die neun neuen `_E2E`-Methodennamen in `E2E_ViewPattern.cs` an die deutsche `Handlung_ErwartetesErgebnis_E2E`-Konvention der übrigen Datei angleichen, z. B. `RunViewPatternHappyPath_E2E` → `ViewPatternHappyPath_NavigiertUndErstelltKorrekt_E2E`, `RecognizeViewsCorrectly_E2E` → `AnsichtenErkennung_LiefertKorrekteViewTypen_E2E`, `MenuNavigationWorks_E2E` → `MenueNavigation_WechseltZwischenAnsichten_E2E`, `ForceShowNavigatesCorrectly_E2E` → `ForceShow_NavigiertKorrektZuAnsicht_E2E`, `ForceCloseWithoutRecursion_E2E` → `ForceClose_OhneRekursion_SchliesstNurEineEbene_E2E`, `ForceCloseWithRecursion_E2E` → `ForceClose_MitRekursion_SchliesstBisDashboard_E2E`, `RecognizeDialogsCorrectly_E2E` → `DialogErkennung_LiefertKorrekteDialogViewTypen_E2E`, `UnrecognizedViewThrowsDetailedException_E2E` → `UnbekannteAnsicht_WirftAussagekraeftigeException_E2E`, `RecognizeErrorViewCorrectly_E2E` → `FehlerAnsichtErkennung_ZeigtFehlermeldung_E2E` (oder vergleichbare deutsche Bezeichner, die dem bestehenden Muster folgen).

### Views/ProjectListView.cs, Views/ProjectDetailView.cs, Views/TaskDetailView.cs, Views/MenuView.cs (vs. WpfTestBase.cs)

- **Doppelter Code** — Die neue View-Pattern-Schicht dupliziert mehrere UI-Interaktionssequenzen, die in `WpfTestBase.cs` bereits als Hilfsmethoden existieren und im selben Testlauf (`RunGeneralTests`) weiterhin aktiv von den bestehenden, nicht auf das View-Pattern migrierten `_E2E`-Methoden genutzt werden. Es existieren somit zwei parallele, nahezu identische Implementierungen derselben fachlichen Abläufe:
  - `WpfTestBase.CreateProject` (Zeile 392–409) vs. `ProjectListView.CreateProject` (Zeile 63–76): identische Klick-/Warte-Sequenz auf "Neu" → "ProjektName" → "Speichern".
  - `WpfTestBase.OpenProject` (Zeile 412–417) vs. `ProjectListView.OpenProject` (Zeile 85–91): identische Sequenz Klick auf Projektname → Warten auf "Speichern".
  - `WpfTestBase.DeleteCurrentProject` (Zeile 873–895) vs. `ProjectDetailView.DeleteProject` (Zeile 62–72): identischer Ablauf Löschen-Klick → Bestätigungsdialog → Warten bis "Speichern" verschwunden.
  - `WpfTestBase.DeleteCurrentTask` (Zeile 903–916) vs. `TaskDetailView.DeleteTask` (Zeile 69–79): identischer Ablauf Löschen-Klick → Bestätigungsdialog → Warten bis "Starten" verschwunden.
  - `WpfTestBase.AufgabeDetailZurueck` (Zeile 859–865) vs. `TaskDetailView.GoBack` (Zeile 83–89): identischer Ablauf "Zurück"-Klick → Warten auf "ProjektName".
  - `WpfTestBase.OffeneAufgabenItems` (Zeile 919–923) vs. `ProjectDetailView.GetTaskElements` (Zeile 75–79): identische Abfrage der "OffeneAufgabenListe"-Kinder.

  Die Klassendoku von `BaseWindowView` und die Methodendoku von `ProjectListView.CreateProject`/`OpenProject` begründen diese Duplikation bewusst mit der Trennung von View-Pattern-Schicht und Test-Basisklassen-Schicht. Das rechtfertigt jedoch nicht, dass beide Implementierungen dauerhaft parallel gepflegt werden müssen, obwohl beide im selben Testlauf aktiv verwendet werden - eine Änderung an einem UI-Element-Namen (z. B. "ProjektName") muss aktuell an mindestens zwei Stellen synchron nachgezogen werden.

  Empfehlung: Eine Richtung wählen und die Duplikation auflösen, z. B. `WpfTestBase.CreateProject`, `OpenProject`, `DeleteCurrentProject`, `DeleteCurrentTask`, `AufgabeDetailZurueck` und `OffeneAufgabenItems` so umbauen, dass sie intern auf die entsprechenden `ProjectListView`-/`ProjectDetailView`-/`TaskDetailView`-Methoden delegieren (Komposition statt eigener Klick-/Warte-Logik), statt die Klick-Sequenz ein zweites Mal zu implementieren. Die genannte Begründung "`BaseWindowView` erbt bewusst nicht von `WpfTestBase`" verhindert das nicht - die Delegation kann in die umgekehrte Richtung erfolgen (`WpfTestBase`-Methode instanziiert die passende `View`-Klasse und ruft deren Methode auf).

### Views/ElementWaitHelper.cs

- **Namenskonventionen und Einheitlichkeit** — Die XML-Dokumentation der statischen Felder `Short` (Zeile 18–28) und `Medium` (Zeile 30–32) enthält jeweils einen `<returns>`-Tag, obwohl `<returns>` laut C#-XML-Doc-Konvention nur für Methoden/Properties vorgesehen ist, nicht für Felder. Die im selben Refactoring berührten, inhaltlich identischen Felder `WpfTestBase.Short`/`Medium`/`Long` (Zeile 26–35 in `WpfTestBase.cs`) verwenden dagegen ausschließlich `<summary>` ohne `<returns>` - die neue Datei weicht damit vom eigenen, im selben Pull Request etablierten Stil ab.

  Empfehlung: Die `<returns>`-Tags aus der Dokumentation von `ElementWaitHelper.Short` und `ElementWaitHelper.Medium` entfernen, sodass sie wie `WpfTestBase.Short`/`Medium`/`Long` nur `<summary>` verwenden.

## Geprüfte Dateien

- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ViewPattern.cs`
- `src/Softwareschmiede.Tests/E2E/ElementWaitHelper.cs`
- `src/Softwareschmiede.Tests/E2E/Views/BaseWindowView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/DialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs`
- `src/Softwareschmiede.Tests/E2E/Views/DashboardView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectListView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ProjectDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/ErrorView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/FileExplorerView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TodoListView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/AutonomAufgabeDetailView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/RepositoryAssignDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/PluginSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/DeleteConfirmationDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/IssueSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/IssueCreateDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/AutonomAufgabeInitialisierungsDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/AutonomAufgabeDetailDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/ArbeitsverzeichnisBearbeitenDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/OpenTodosDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/HelpTextDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/SolutionSelectionDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/UpdateProgressDialogView.cs`
- `.githooks/install-hooks.cmd`
- `.githooks/install-hooks.sh`
- `.githooks/pre-commit`
- `.githooks/translation-check.py`

Hinweis: Die vier `.githooks/*`-Dateien wurden ebenfalls geprüft (im Diff gegenüber `main` enthalten), gehören inhaltlich aber zu einer anderen, nicht mit Issue #231 zusammenhängenden Änderung (Lokalisierungs-Pre-Commit-Hook). Es wurden dort keine Befunde gemäß den oben genannten Kriterien festgestellt.
