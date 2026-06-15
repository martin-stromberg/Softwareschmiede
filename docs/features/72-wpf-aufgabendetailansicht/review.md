# Plan-Review: Aufgabendetailansicht mit Ribbon-Menü und Status-abhängigem Content-Switching

## Ergebnis

**Status:** Vollständig umgesetzt

---

## Umgesetzte Planelemente

### Converter und Infrastruktur

- [x] `AufgabeStatusToVisibilityConverter` — Implementiert in `Converters/AppConverters.cs` (Zeilen 69–93)
  - Konvertiert `AufgabeStatus` zu `Visibility` basierend auf kommagetrennte Parameter
  - Registriert in `App.xaml.Resources` (Zeile 18)

### TaskDetailViewModel — Eigenschaften

- [x] Property `IsInfoViewVisible` (bool) — Vorhanden (Zeilen 154–158)
  - Steuert Sichtbarkeit zwischen Info-Panel und CLI-Fenster
  - Initial = false (CLI sichtbar)
  - Two-Way-Binding unterstützt

- [x] Property `ShowEditPanel` (bool, computed) — Vorhanden (Zeile 179)
  - True wenn `Aufgabe?.Status == AufgabeStatus.Neu`
  - PropertyChanged triggert in `LadenAsync()` (Zeile 276)

- [x] Property `ShowCliPanel` (bool, computed) — Vorhanden (Zeilen 182–184)
  - True wenn Status ∈ {Gestartet, InArbeit, Wartend}
  - PropertyChanged triggert in `LadenAsync()` (Zeile 277)

- [x] Property `ShowDiffPanel` (bool, computed) — Vorhanden (Zeile 187)
  - True wenn `Aufgabe?.Status == AufgabeStatus.Beendet`
  - PropertyChanged triggert in `LadenAsync()` (Zeile 278)

- [x] Property `EditTitel` (string?) — Vorhanden (Zeilen 161–169)
  - Editable Kopie von `Aufgabe.Titel`
  - Two-Way-Binding mit `UpdateSourceTrigger=PropertyChanged`
  - Triggert `KannSpeichern`-Neuberechnung (Zeile 167)
  - Initialisiert in `LadenAsync()` (Zeile 273)

- [x] Property `EditAnforderungsBeschreibung` (string?) — Vorhanden (Zeilen 172–176)
  - Editable Kopie von `Aufgabe.AnforderungsBeschreibung`
  - Initialisiert in `LadenAsync()` (Zeile 274)

- [x] Property `KannSpeichern` (bool, computed) — Vorhanden (Zeilen 190–192)
  - CanExecute für SpeichernCommand
  - Logik: Status ∈ {Neu, Gestartet} && !IsCliRunning && Titel.Length > 0
  - PropertyChanged triggert in `LadenAsync()` (Zeile 279) und beim Ändern von EditTitel (Zeile 167)

- [x] Property `KannLoeschen` (bool, computed) — Vorhanden (Zeilen 195–197)
  - CanExecute für LoeschenCommand
  - Logik: Status ∉ {Beendet, Archiviert} && !IsCliRunning && Aufgabe != null
  - PropertyChanged triggert in `LadenAsync()` (Zeile 280)

### TaskDetailViewModel — Commands

- [x] Command `SpeichernCommand` (ICommand) — Vorhanden (Zeile 215)
  - Initialisiert als `AsyncRelayCommand` (Zeile 254)
  - CanExecute: `KannSpeichern`
  - Implementierung: `SpeichernAsync()` (Zeilen 429–455)
    - Ruft `AufgabeService.UpdateAsync()` auf (Zeile 439)
    - Setzt `IsLoading = true` (Zeile 434)
    - Error-Handling mit FehlerMeldung (Zeilen 446–449)
    - Neu laden nach Update (Zeile 440)

- [x] Command `LoeschenCommand` (ICommand) — Vorhanden (Zeile 218)
  - Initialisiert als `AsyncRelayCommand` (Zeile 255)
  - CanExecute: `KannLoeschen`
  - Implementierung: `LoeschenAsync()` (Zeilen 457–497)
    - Zeigt Bestätigungsdialog mit `IDialogService.BestaetigenDialog()` (Zeile 463)
    - Ruft `AufgabeService.DeleteAsync()` auf (Zeile 471)
    - Ruft `AufgabeListeAktualisierenCallback` auf (Zeile 475)
    - Navigiert zurück mit `ZurueckAction` (Zeile 482)
    - Error-Handling mit FehlerMeldung (Zeilen 490–491)

- [x] Command `InfoCliToggleCommand` (ICommand) — Vorhanden (Zeile 221)
  - Initialisiert als `RelayCommand` (Zeile 256)
  - Keine CanExecute-Prüfung
  - Implementierung: `InfoCliToggle()` (Zeilen 499–502)
    - Toggled `IsInfoViewVisible` einfach mit `!IsInfoViewVisible`

### TaskDetailViewModel — Weitere Anpassungen

- [x] Dependency Injection: `IDialogService` — Vorhanden in Constructor (Zeile 236)
  - Wird in ViewModel als Feld gespeichert (Zeile 23)
  - Wird für Bestätigungsdialog in `LoeschenAsync()` genutzt (Zeile 463)

- [x] LadenAsync() — Angepasst (Zeilen 260–302)
  - Initialisiert `EditTitel = Aufgabe?.Titel` (Zeile 273)
  - Initialisiert `EditAnforderungsBeschreibung = Aufgabe?.AnforderungsBeschreibung` (Zeile 274)
  - Triggert PropertyChanged für alle Compute-Properties (Zeilen 276–280)
  - Verarbeitet Protokolleinträge (Zeilen 282–285)

### TaskDetailView (XAML)

- [x] Ribbon-Menü (Grid.Row="0") — Implementiert (Zeilen 14–81)
  - Border mit SurfaceBrush Background und BorderBrush (Zeilen 15–18)
  - StackPanel Horizontal (Zeile 20)
  - **RibbonGroup "Navigation"** (Zeilen 23–30)
    - Button "Zurück" mit Icon "←" (Zeilen 25–28)
    - Command: `ZurueckCommand`
  - **RibbonGroup "Aufgabe"** (Zeilen 33–54)
    - Button "Speichern" (💾) → `SpeichernCommand` (Zeilen 36–39)
    - Button "Löschen" (🗑) → `LoeschenCommand` (Zeilen 40–43)
    - Button "Starten" (▶) → `StatusGestartetSetzenCommand` (Zeilen 44–47)
    - Button "Beenden" (✓) → `AufgabeAbschliessenCommand` (Zeilen 48–51)
  - **RibbonGroup "CLI"** (Zeilen 57–78)
    - ComboBox für KI-Plugin-Auswahl (Zeilen 61–65)
    - Button "CLI Starten" (Zeilen 66–69)
    - Button "Stoppen" (Zeilen 71–75) mit Visibility auf `IsCliRunning`

- [x] Edit-Panel (Status=Neu) — Implementiert (Zeilen 95–149)
  - Visibility: `{Binding ShowEditPanel, Converter={StaticResource BoolToVisibilityConverter}}` (Zeile 97)
  - Kachel mit Border (Zeilen 99–147)
  - Label "Aufgabe bearbeiten" (Zeilen 105–109)
  - TextBlock "Titel" (Zeilen 111–114)
  - TextBox: `{Binding EditTitel, UpdateSourceTrigger=PropertyChanged}` (Zeilen 115–123)
  - TextBlock "Anforderungsbeschreibung" (Zeilen 125–128)
  - TextBox (MultiLine): `{Binding EditAnforderungsBeschreibung, UpdateSourceTrigger=PropertyChanged}` (Zeilen 129–140)
  - Hinweistext "Speichern-Button im Ribbon verwenden" (Zeilen 142–145)

- [x] CLI-Panel (Status ∈ {Gestartet, InArbeit, Wartend}) — Implementiert (Zeilen 152–270)
  - Visibility: `{Binding ShowCliPanel, Converter={StaticResource BoolToVisibilityConverter}}` (Zeile 152)
  - Grid mit 2 Rows (Zeilen 153–156)

  - **Toggle-Leiste (Row 0)** (Zeilen 159–184)
    - Label "Ansicht:" (Zeilen 163–167)
    - ToggleButton: `IsChecked="{Binding IsInfoViewVisible}"` mit Command `InfoCliToggleCommand` (Zeilen 168–170)
    - Style mit Trigger: Content "CLI" oder "Info" je nach IsChecked (Zeilen 173–182)

  - **Inhalts-Grid (Row 1)** (Zeilen 187–269)
    - ProcessWindowHost: `Visibility="{Binding IsInfoViewVisible, Converter={StaticResource InverseBoolToVisibilityConverter}}"` (Zeilen 189–191)
    - Info-Panel (ScrollViewer) (Zeilen 194–268)
      - Visibility: `{Binding IsInfoViewVisible, Converter={StaticResource BoolToVisibilityConverter}}` (Zeile 196)
      - Kachel mit Aufgabeeigenschaften (Zeilen 198–219)
        - TextBlock `{Binding AufgabeTitel}` (Zeilen 205–209)
        - TextBlock `{Binding AufgabeStatus}` (Zeilen 210–213)
        - TextBlock `{Binding Aufgabe.AnforderungsBeschreibung}` (Zeilen 214–217)
      - Kachel mit Protokoll-ListBox (Zeilen 221–266)
        - ListBox `ItemsSource="{Binding Protokolleintraege}"` (Zeilen 232–264)
        - DataTemplate mit Zeitstempel, Typ, Inhalt (Zeilen 238–263)

- [x] Diff-Panel (Status=Beendet) — Implementiert (Zeilen 273–290)
  - Visibility: `{Binding ShowDiffPanel, Converter={StaticResource BoolToVisibilityConverter}}` (Zeile 275)
  - Kachel mit Border (Zeilen 277–288)
  - TextBlock (Platzhalter): "Diff-Ansicht wird hier angezeigt... (Implementierung folgt später)" (Zeilen 282–287)

- [x] Fehler-Border (Grid.Row="1") — Vorhanden (Zeilen 84–89)
  - Zeigt FehlerMeldung mit NullOrEmptyToVisibilityConverter

- [x] Statusleiste (Grid.Row="3") — Vorhanden (Zeilen 295–304)
  - Zeigt Status mit `{Binding AufgabeStatus, Mode=OneWay}`

### Datenbankmigrationen

- [x] Keine neuen Migrationen erforderlich
  - `Aufgabe.Titel` existiert bereits
  - `Aufgabe.AnforderungsBeschreibung` existiert bereits
  - `AufgabeService.UpdateAsync()` existiert bereits
  - `AufgabeService.DeleteAsync()` existiert bereits
  - Status-Übergänge in `ValidateStatusTransition()` existieren bereits

### Unit-Tests

- [x] TaskDetailViewModelTests — Implementiert in `Tests/App/ViewModels/TaskDetailViewModelTests.cs`

  **ShowEditPanel, ShowCliPanel, ShowDiffPanel Tests:**
  - `ShowEditPanel_IsTrue_WhenStatusNeu()` (Zeilen 95–106)
  - `ShowCliPanel_IsTrue_WhenStatusGestartet()` (Zeilen 109–120)
  - `ShowCliPanel_IsTrue_WhenStatusInArbeit()` (Zeilen 123–132)
  - `ShowCliPanel_IsTrue_WhenStatusWartend()` (Zeilen 135–144)
  - `ShowDiffPanel_IsTrue_WhenStatusBeendet()` (Zeilen 147–158)

  **KannSpeichern Tests:**
  - `KannSpeichern_IsTrue_WhenStatusNeuUndTitelGesetzt()` (Zeilen 163–174)
  - `KannSpeichern_IsFalse_WhenTitelLeer()` (Zeilen 177–188)
  - `KannSpeichern_IsFalse_WhenStatusBeendet()` (Zeilen 191–200)
  - `KannSpeichern_IsTrue_WhenStatusGestartet()` (Zeilen 203–214)

  **KannLoeschen Tests:**
  - `KannLoeschen_IsTrue_WhenStatusNeu()` (Zeilen 219–228)
  - `KannLoeschen_IsFalse_WhenStatusBeendet()` (Zeilen 231–240)
  - `KannLoeschen_IsFalse_WhenStatusArchiviert()` (Zeilen 243–252)
  - `KannLoeschen_IsTrue_WhenStatusGestartet()` (Zeilen 255–264)

  **SpeichernCommand Tests:**
  - `SpeichernCommand_RuftUpdateAsyncAuf_UndAktualisiertDaten()` (Zeilen 269–285)
  - `SpeichernCommand_SetsIsLoading_DuringExecution()` (Zeilen 288–301)
  - `SpeichernCommand_CanExecuteFalse_WennTitelLeer()` (Zeilen 304–314)
  - `SpeichernCommand_SetzFehlerMeldung_BeiException()` (Zeilen 317–329)

  **LoeschenCommand Tests:**
  - `LoeschenCommand_LoeschtAufgabe_WennBenutzerBestaetigt()` (Zeilen 334–350)
  - `LoeschenCommand_NavigiertNichtZurueck_WennBenutzerAbbricht()` (Zeilen 353–369)
  - `LoeschenCommand_RuftBestaetigenDialogAuf()` (Zeilen 372–385)
  - `LoeschenCommand_SetzFehlerMeldung_WennDeleteScheitert()` (Zeilen 388–402)
  - `LoeschenCommand_CanExecuteFalse_WennStatusBeendet()` (Zeilen 405–414)
  - `LoeschenCommand_RuftCallbackAuf_NachErfolgreichemLoeschen()` (Zeilen 417–432)

  **InfoCliToggleCommand Tests:**
  - `InfoCliToggleCommand_SetzIsInfoViewVisible_AufTrue_BeiInitialFalse()` (Zeilen 437–445)
  - `InfoCliToggleCommand_SetzIsInfoViewVisible_AufFalse_BeiTrue()` (Zeilen 448–457)
  - `InfoCliToggleCommand_TogglesMehrfach_Korrekt()` (Zeilen 460–470)

  **EditTitel und EditAnforderungsBeschreibung Tests:**
  - `EditTitel_WirdNachLaden_MitAufgabeTitelInitialisiert()` (Zeilen 475–484)
  - `EditAnforderungsBeschreibung_WirdNachLaden_Initialisiert()` (Zeilen 487–496)
  - `EditTitel_AendertKannSpeichern_BeiAenderung()` (Zeilen 499–512)

  **ZurueckCommand Test:**
  - `ZurueckCommand_RuftZurueckActionAuf()` (Zeilen 515–524)

---

## Offene Aufgaben

Keine. Alle Planelemente sind vollständig umgesetzt.

---

## Hinweise

### Implementierungsdetails

1. **ViewModel-Initialisierung:** Das ViewModel wird mit Dependency Injection für alle Services (AufgabeService, ProtokollService, KiAusfuehrungsService, EntwicklungsprozessService, PluginSelectionService, IDialogService) versorgt. Dies ermöglicht vollständige Testbarkeit und Austauschbarkeit.

2. **Converter-Registrierung:** Der `AufgabeStatusToVisibilityConverter` ist korrekt in `App.xaml.Resources` registriert und wird nicht in TaskDetailView.xaml verwendet (nicht notwendig, da die Sichtbarkeit über computed Properties erfolgt).

3. **MVVM-Konformität:** Die Implementierung folgt strikt dem MVVM-Pattern:
   - ViewModel steuert Sichtbarkeit über Computed Properties (`ShowEditPanel`, `ShowCliPanel`, `ShowDiffPanel`)
   - Commands sind in ViewModel implementiert
   - Two-Way-Binding für Edit-Felder ist konfiguriert
   - PropertyChanged wird korrekt für abhängige Properties aufgerufen

4. **Error-Handling:** Beide Commands (`SpeichernCommand`, `LoeschenCommand`) haben umfassendes Error-Handling:
   - `SpeichernCommand` setzt `FehlerMeldung` und zeigt Fehler in der UI
   - `LoeschenCommand` zeigt zuerst einen Bestätigungsdialog, dann Error-Dialog bei Fehler
   - Service-Fehler (z.B. ungültiger Status) werden korrekt abgefangen

5. **Bestätigungsdialog:** Der `LoeschenCommand` nutzt `IDialogService.BestaetigenDialog()`, um den Benutzer zur Bestätigung aufzufordern, bevor die Aufgabe gelöscht wird. Dies ist sicher implementiert und wird in Tests mit Mocking überprüft.

6. **Test-Abdeckung:** Die Unit-Tests decken alle neuen Funktionen ab:
   - Compute-Properties für Content-Switching (ShowEditPanel, ShowCliPanel, ShowDiffPanel)
   - CanExecute-Logik für Commands (KannSpeichern, KannLoeschen)
   - Command-Implementierung (Speichern, Löschen, Toggle)
   - Edit-Feld-Initialisierung
   - Callback-Aufruf nach Löschen

7. **E2E-Tests:** Keine E2E-Tests sind implementiert. Diese wären optional und könnten später hinzugefügt werden, um die View-Interaktionen zu testen (z.B. Button-Klicks, Panel-Wechsel, Dialog-Bestätigung).

### Architektur-Konsistenz

- **Ribbon-Menü:** Folgt dem gleichen Design wie ProjectDetailView (RibbonGroup, RibbonLargeButton)
- **Status-abhängiges Content-Switching:** Nutzt bewährte WPF-Patterns (Visibility-Binding mit Converter)
- **Edit-Modus:** Nur in Status=Neu sichtbar (wie spezifiziert)
- **Info/CLI-Toggle:** Einfache Toggle-Logik ohne Datenverlust (nur UI-Zustand)

### Erfüllte Anforderungen

1. ✓ Ribbon-Menü mit Buttons für Speichern, Löschen, Starten, Beenden
2. ✓ Status-abhängiges Content-Switching (Edit-, CLI-, Diff-Panel)
3. ✓ Edit-Modus nur für Status=Neu
4. ✓ CLI-Panel mit Toggle zwischen CLI-Fenster und Info-Panel
5. ✓ Diff-Panel als Platzhalter für Status=Beendet
6. ✓ Commands mit validierter CanExecute-Logik
7. ✓ Bestätigungsdialog beim Löschen
8. ✓ Unit-Tests für alle neuen Properties und Commands

