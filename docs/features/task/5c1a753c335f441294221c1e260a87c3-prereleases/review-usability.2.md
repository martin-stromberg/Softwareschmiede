# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Einstellungen öffnen (Sidebar-Schaltfläche „Einstellungen" mit Zahnrad-Symbol) → unauffällig
- Update-Modus über Auswahlbox festlegen → unauffällig. Im Register „Allgemein" steht unter der klarschriftlich beschrifteten Gruppe „Updates" eine ComboBox mit exakt den drei in der Anforderung genannten Optionen `Aus`, `Nur Pruefen` und `Bei Programmstart pruefen und ausfuehren` (Anzeige-Labels, keine technischen Enum-Werte oder IDs).
- Prerelease-Versionen per Checkbox aktivieren → unauffällig. Checkbox „Prerelease-Versionen laden" steht direkt unter der Auswahlbox in derselben „Updates"-Gruppe, verständlich beschriftet.
- Auswahl wirksam machen → unauffällig. Über die Ribbon-Schaltfläche „Speichern"; der Erfolg wird mit der Meldung „Einstellungen gespeichert." sichtbar bestätigt, Fehler erscheinen als rote Meldungsleiste.
- Manuelle Update-Prüfung auslösen (Sidebar-Schaltfläche „Prüfen", Tooltip „Auf Programmupdate prüfen") → unauffällig. Befund aus Iteration 1 behoben: Bei „kein Update gefunden" erscheint nun direkt unter der Schaltfläche der Hinweis „Kein Update verfügbar. Die installierte Version ist aktuell." (`UpdateHinweis`-TextBlock in `MainWindow.xaml`, befüllt in `ApplyUpdateCheckResult` bei `isManualRefresh: true`). Auch Fehlerfälle melden sich sichtbar („Update-Prüfung ist fehlgeschlagen.", „Lokale Version ist nicht prüfbar." o. ä.). Ist der Modus auf `Aus` gestellt, ist die Schaltfläche deaktiviert — konsistent zur Anforderung, dass „Aus" die Prüfung deaktiviert.
- Gefundenes Update installieren → unauffällig. Sobald ein Update gefunden wurde, erscheint eine zusätzliche Schaltfläche „Update" mit Tooltip „Update auf Version {x} vorbereiten". Die Installation läuft in einem beschrifteten Fortschrittsdialog „Update vorbereiten" mit Phasentext, Fortschrittsbalken und „Abbrechen"-Schaltfläche; Fehler und Abbruch werden als Klartext im Dialog angezeigt.
- Automatische Prüfung/Installation beim Programmstart → unauffällig. Läuft ohne erforderliches Zutun; bei gefundenem Update erscheint je nach Modus die „Update"-Schaltfläche bzw. der Fortschrittsdialog. Keine internen Kennungen nötig.

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `src/Softwareschmiede.App/Views/SettingsView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/UpdateProgressDialog.xaml`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`
- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (nur Prüfung der angezeigten Meldungstexte)
- `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs` (nur Prüfung der angezeigten Meldungstexte)
