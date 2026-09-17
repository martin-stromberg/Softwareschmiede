# Offene Aufgaben

Erstellt am: 2025-07-08
Abbruchgrund: Maximale Iterationsanzahl erreicht (Iteration 3)
Nacharbeiten-Lauf abgeschlossen am: 2025-07-08 (alle Einträge erledigt und verifiziert)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und wurden im Nacharbeiten-Lauf bearbeitet.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt`.

## Code-Review-Befunde

- [x] `MainWindowViewModel.cs` — Lange Parameterliste: Konstruktor (Zeilen 160–172) umfasst weiterhin 12 Parameter, davon fünf optionale testgetriebene Seams (`dispatcherInvoke`, `dialogService`, `versionProvider`, `laufdatenChangedNotifier`, `updateDienste`). Empfehlung: verbleibende optionale Seams in ein Parameter-Objekt bündeln (z. B. `MainWindowTestSeams`) oder per Property-Injection/DI-Auflösung aus dem Konstruktor nehmen.
  - Erledigt: Optionale Seams in `MainWindowOptionaleDienste`-Record gebündelt (DispatcherInvoke, DialogService, VersionProvider, LaufdatenChangedNotifier, UpdateDienste); produktiv per DI registriert, Tests injizieren gezielt.
- [x] `MainWindowViewModel.cs` — Duplizierter Code: Versuch-Gerüst in drei nahezu identischen Varianten (`InitializeUpdatesAfterWindowReadyAsync` Zeilen 447–501, `UpdatePruefenAsync` Zeilen 503–547, `UpdateStartenAsync` Zeilen 549–599); zusätzlich wörtlich duplizierter Settings-Revalidierungsblock in `InstalliereUpdateAsync` (Zeilen 624–638 und 647–661). Empfehlung: gemeinsame Methode `FuehreUpdateVersuchAsync(...)` extrahieren; Aktualitätsnachweis als `PruefeSettingsAktualitaetAsync(...)` auslagern.
  - Erledigt: `FuehreUpdateVersuchAsync(versuchArt, vorbereitend, kern, ct)` trägt Gate-Akquise, Versuchsprotokoll, Busy-Flags, Generations-Snapshot und den Aus-Guard; `PruefeSettingsAktualitaetAsync` kapselt beide Aktualitätsnachweise.
- [x] `MainWindowViewModel.cs` — Struktur/Verantwortlichkeiten: Klasse umfasst 814 Zeilen, der Update-Fluss allein ~350 Zeilen. Empfehlung: Update-Ablauf in eigenen Koordinator (z. B. `MainWindowUpdateFlow`) auslagern, ViewModel delegiert nur noch Commands/Properties/Settings-Saved-Einstieg.
  - Erledigt: `MainWindowUpdateFlow` (438 Zeilen) besitzt Gate, Generation, Startautomatik, Prüf-/Installationspfad und Settings-Saved-Reaktion; `MainWindowViewModel` delegiert und umfasst nur noch ~431 Zeilen.
- [x] `SettingsViewModel.cs` — Primitive Obsession: Update-Modus als Anzeige-Label-String modelliert (`SelectedUpdateMode` string, `TryParseUpdateModus` Zeilen 425–441); Label-Literale zusätzlich in `E2E_UpdateSettings.cs` (Zeilen 25–27), `MainWindowViewModelTests_UpdateSettingsReadFailure.cs` und `MainWindowViewModelTests_UpdateStartup.cs` dupliziert. Empfehlung: `UpdateModusOptionen` als `(UpdateMode, string)`-Paare bzw. Enum-Auswahl mit ValueConverter; Labels aus einer Quelle.
  - Erledigt: `UpdateModusOption`-Record (`Modus` + `Label`) mit `Alle`/`FuerModus`; `UpdateModusTexte` als einzige Label-Quelle für UI und Tests; `SettingsViewModel.SelectedUpdateMode` ist typisiert, ComboBox bindet per `SelectedItem`.
- [x] `MainWindowViewModelTests_UpdateSettingsReadFailure.cs` — Switch-/String-Dispatch auf Magic Strings ohne `default` (Zeilen 54–76): falsch geschriebener `[InlineData]`-String überspringt die Act-Phase kommentarlos. Empfehlung: `[MemberData]` mit `Func<MainWindowViewModel, Task>`-Delegaten oder drei separate Testmethoden.
  - Erledigt: String-Dispatch durch typsichere Enum-Auswahl ersetzt (ungültige Werte kompilieren nicht mehr / werden von der Theory abgedeckt).
- [x] `E2E_UpdateSettings.cs` — God-Methode `Startup_SafetyCancelAndErrorsRemainUsable` (Zeilen 605–892, ~287 Zeilen) deckt drei konzeptionell getrennte Aspekte ab. Empfehlung: Teilflüsse in eigene Hilfsmethoden extrahieren (analog `LesefehlerStartupVarianteStartenAsync`), Szenario-Methode als kurze Sequenz führen.
  - Erledigt: In benannte Phasen zerlegt (`SafetyDialogAbgelehntPhaseAsync`, `DownloadAbbruchPhaseAsync`, `AssetHttpFehlerPhaseAsync`, `DefektesZipPhaseAsync`, `LauncherFehlerPhaseAsync`, `T09StartupPhaseAsync`, `T09ManuellInitialPhaseAsync`, `T09ManuellStartenPhaseAsync`); die Szenario-Methode ist eine kurze Sequenz.
- [x] `E2E_UpdateSettings.cs` — Sechs leere `catch { }`-Blöcke ohne Begründung in `SetzeUpdateSteuerungZurueckAsync` (Zeilen 1226–1228, 1264–1266). Empfehlung: erwartete Typen gezielt fangen oder kurzen begründenden Kommentar wie in den Nachbarblöcken.
  - Erledigt: Catch-Blöcke mit begründenden Kommentaren versehen (Cleanup ist best effort; Testwurzel/Prozesse können bereits weg sein).
- [x] `E2E_UpdateSettings.cs` — Irreführender Alias `WarteAufSpeichernAbgeschlossen` (Zeile 1217) delegiert 1:1 an `WarteAufSpeichernAktiviert`. Empfehlung: Alias entfernen oder auf echte Speicher-Bestätigung (`SettingsView.WaitForSettingsSaved`) warten lassen.
  - Erledigt: Alias entfernt, Aufrufstellen verwenden direkt `WarteAufSpeichernAktiviert`.
- [x] `UpdateFixtureHttpMessageHandlerTests.cs` — `Dispose` (Zeile 42) fängt bei `Directory.Delete` nur `IOException`; `UnauthorizedAccessException` möglich (Vergleichscode `UpdateE2EFixture.Dispose` behandelt beide). Empfehlung: `UnauthorizedAccessException` zusätzlich fangen oder Catch erweitern.
  - Erledigt: `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)`.

## Usability-Befunde

- [x] Auswahlbox „Updates" ohne sichtbare Beschriftung/Erläuterung (SettingsView.xaml:289–311): „Update-Modus" nur als unsichtbarer `AutomationProperties.Name`; andere Bereiche haben erklärende Zeile. Empfehlung: sichtbare Feldbezeichnung + kurze Erläuterung.
  - Erledigt: Sichtbare Feldbezeichnung und Modus-Erläuterungstexte ergänzt.
- [x] Inkonsistente Schreibweise ohne Umlaute: „Nur Pruefen" / „Bei Programmstart pruefen und ausfuehren" (SettingsViewModel.cs:20–23) vs. „Prüfen"/„prüfen" im Rest der UI. Empfehlung: „Nur prüfen" / „Bei Programmstart prüfen und ausführen".
  - Erledigt: `UpdateModusTexte` verwendet „Nur prüfen" / „Bei Programmstart prüfen und ausführen"; Dokumentation und README nachgezogen.
- [x] Checkbox „Prerelease-Versionen laden" ohne Erklärung (SettingsView.xaml:307–310): Fachbegriff ohne Hinweis auf Vorabversionen. Empfehlung: Beschreibungstext ergänzen (z. B. „Auch Vorabversionen (z. B. Beta-Versionen) berücksichtigen. Diese können noch Fehler enthalten.").
  - Erledigt: Erklärender Hinweistext unter der Checkbox ergänzt.
- [x] Deaktivierter „Prüfen"-Button bei Modus „Aus" ohne Begründung (`KannUpdatePruefen`, MainWindowViewModel.cs:721–725). Empfehlung: Tooltip/Hinweis „Update-Prüfung ist in den Einstellungen deaktiviert."
  - Erledigt: `MainWindowUpdateFlow.UpdatePruefenTooltip` liefert die Begründung; `ToolTipService.ShowOnDisabled` zeigt sie auch am deaktivierten Button.
- [x] Fortschrittsdialog im Fehlerzustand nur über Fenster-X schließbar; einzige Schaltfläche „Abbrechen" deaktiviert; Fehlertext kann rohe `ex.Message`-Details enthalten (UpdateProgressViewModel.cs:100–107, UpdateProgressDialog.xaml:47–51, MainWindowViewModel.cs:675). Empfehlung: aktive „Schließen"-Schaltfläche im Fehlerzustand + verständliche Kernbotschaft.
  - Erledigt: „Schließen"-Schaltfläche im Endzustand (an `CanClose` gebunden); `BoolToVisibilityConverter` lokal im Dialog deklariert, damit isolierte Dialog-Tests ohne App-Ressourcen laufen.

## Fehlgeschlagene Tests

- [x] `Softwareschmiede.Tests.App.Controls.TerminalControlTests.ReadClipboardAndInsertAsync_LangerMehrzeiligerText_WritesCompleteEncodedBytes` — `COMException CLIPBRD_E_CANT_OPEN (0x800401D0)`: intermittierende Clipboard-Ressourcenkonkurrenz dieser Sandbox, feature-unabhängig (Terminal-Steuerung, kein Update-Code). Als dokumentiertes Umgebungsproblem behandelt; im finalen OsInterface-Lauf des Nacharbeiten-Laufs grün durchgelaufen (48/50 bestanden, 0 fehlgeschlagen, 2 übersprungen).
