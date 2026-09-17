# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MainWindow.xaml / MainWindowViewModel.cs (Seitenleiste „Programmupdate prüfen")

- **Erreichbarkeit** — Die Anforderung führt mit dem Modus „Nur Pruefen" eine Update-Prüfung ein, die ein gefundenes Update nur anbietet. Die einzige Möglichkeit für eine Anwenderin, diese Prüfung selbst aktiv auszulösen, ist der Seitenleisten-Button „⟳ Prüfen" (`UpdatePruefenCommand`). Klickt sie ihn und es ist kein Update verfügbar — der Normalfall —, passiert sichtbar gar nichts: `UpdateService.CheckForUpdateAsync` liefert `UpdateCheckResult.KeinUpdate()` ohne Message-Text (UpdateService.cs, Zeile 51), `ApplyUpdateCheckResult` setzt `UpdateHinweis = result.Message` (MainWindowViewModel.cs, Zeile 750), also `null`, und der in diesem Branch neu angebundene `UpdateHinweis`-TextBlock (MainWindow.xaml, Zeile 176-182) bleibt ausgeblendet. Für eine nicht-technische Anwenderin ist nicht unterscheidbar, ob die Prüfung gelaufen ist und nichts gefunden hat oder ob der Klick wirkungslos war — die Schaltfläche wirkt defekt. Nur im Fehlerfall (`NichtPruefbar`) oder bei gefundenem Update gibt es eine Rückmeldung.

  Empfehlung: Bei manueller Prüfung (`isManualRefresh: true`) immer einen Hinweistext setzen — z. B. `UpdateCheckResult.KeinUpdate("Kein Update verfügbar. Die installierte Version ist aktuell.")` im UpdateService oder einen entsprechenden Fallback-Text in `ApplyUpdateCheckResult` — damit der vorhandene `UpdateHinweis`-Anzeigebereich das Prüfergebnis sichtbar bestätigt.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Einstellungen öffnen (Seitenleiste „⚙ Einstellungen") → unauffällig
- Update-Modus über Auswahlbox mit den Optionen `Aus`, `Nur Pruefen`, `Bei Programmstart pruefen und ausfuehren` wählen (Einstellungen → Register „Allgemein" → Abschnitt „Updates", ComboBox mit Klartext-Optionen) → unauffällig
- Auswahl `Aus` deaktiviert die Updateprüfung → unauffällig (Startprüfung wird übersprungen, „Prüfen"-Button deaktiviert, evtl. angebotenes Update wird entfernt)
- Auswahl `Nur Pruefen` führt Prüfung aus, installiert nicht automatisch; gefundenes Update erscheint als „Update"-Button mit Versions-Tooltip → unauffällig; eingeschränkt durch den Befund oben: manuell ausgelöste Prüfung ohne Fund gibt keine Rückmeldung
- Auswahl `Bei Programmstart pruefen und ausfuehren` prüft beim Start und installiert automatisch; Fortschrittsdialog „Update vorbereiten" mit Phasentext, Fortschrittsbalken und „Abbrechen"-Button, bei laufenden CLI-Aufgaben verständliche Sicherheitsabfrage „Update starten?" → unauffällig
- Checkbox „Prerelease-Versionen laden" aktivieren/deaktivieren → unauffällig (klare Beschriftung, wird nur beim Speichern wirksam — konsistent mit dem etablierten Speichern/Verwerfen-Muster der Seite)
- Einstellungen speichern über Ribbon-Button „Speichern" → unauffällig

Keine Stelle verlangt interne Kennungen (IDs, GUIDs, technische Schlüssel); alle Auswahlen erfolgen über Klartext. Die Umsetzung nutzt die auf der Seite etablierten Muster (ComboBox wie beim Design-Modus, Checkbox wie bei „Autonome Aufgaben aktivieren").

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/UpdateProgressDialog.xaml`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`
- `src/Softwareschmiede.App/App.xaml.cs` (Startverdrahtung der Update-Prüfung nach Fensteranzeige)
