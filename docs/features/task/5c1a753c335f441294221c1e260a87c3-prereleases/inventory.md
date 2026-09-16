# Bestandsaufnahme: Prereleases bei Updates

## Ergebnis

Die Anforderung betrifft eine bestehende WPF/.NET-Anwendung mit einer Key-Value-Persistenz für Anwendungseinstellungen. Die Updatefunktion ist bereits in Application-, Infrastructure- und App-Schichten getrennt, unterstützt aber aktuell nur stabile Releases und wird ohne benutzerbezogene Update-Einstellungen ausgelöst.

Der `/inventory`-Schritt wurde lokal ausgeführt, weil in der aktuellen Codex-Umgebung kein delegierbarer Unteragent verfügbar ist.

## Betroffene Bereiche

| Bereich | Bestand | Relevanz |
|---|---|---|
| Einstellungen | `AppEinstellungService`, `SettingsViewModel`, `SettingsView.xaml` | Neue Update-Auswahl und Prerelease-Checkbox, Laden/Speichern und Persistenz |
| Update-Anwendung | `UpdateService`, `IUpdateService`, `UpdateModels` | Updateprüfung muss Optionen konsistent weiterreichen |
| Release-Abfrage | `GitHubReleaseClient`, `IUpdateReleaseClient` | Stable-/Prerelease-Auswahl und Release-Auflösung |
| Programmstart/Hintergrund | `MainWindowViewModel` | Automatische Prüfung und Installation abhängig vom gespeicherten Modus |
| Registrierung | `App.xaml.cs` | DI für neue Abhängigkeiten oder Optionen |
| Tests | Update-Unit-Tests, Settings-ViewModel-Tests, FlaUI-E2E | Abdeckung der Modi, Persistenz, Releasefilter und Startfluss |

## Detaildokumente

- [Einstellungsmodell und UI](inventory/settings.md)
- [Updateprüfung und Releasequelle](inventory/update-pipeline.md)
- [Programmstart und Anwendung](inventory/startup-flow.md)
- [Tests und Abdeckung](inventory/tests.md)

## Festgestellte Lücken

1. Es gibt keinen persistenten Schlüssel oder ViewModel-Zustand für den Update-Prüfmodus.
2. Es gibt keinen persistenten Prerelease-Schalter.
3. `IUpdateReleaseClient` bietet nur `GetLatestStableReleaseAsync` ohne Optionen.
4. `GitHubReleaseClient` ruft `/releases/latest` auf und verwirft `release.Prerelease` grundsätzlich.
5. `MainWindowViewModel` prüft im Hintergrund und beim manuellen Update ohne Kenntnis eines Update-Modus; ein automatisches Vorbereiten und Starten beim Programmstart ist dort nicht vorhanden.
6. Für die neuen Einstellungen und den exakten Update-Benutzerfluss existieren noch keine passenden E2E-Szenarien.

## Randbedingungen

- Die bestehende Datenbankmigration wird für neue Key-Value-Einstellungen voraussichtlich nicht benötigt; `AppEinstellungService` legt fehlende Schlüssel beim Speichern an.
- Der vorhandene Versionsvergleich akzeptiert nur `X.Y.Z` sowie Build-Metadaten, aber keine SemVer-Prerelease-Suffixe. Für die Anforderung muss deshalb geklärt und umgesetzt werden, wie Prerelease-Versionen verglichen werden.
- Die automatische Installation verwendet bereits `PrepareUpdateAsync` und `StartPreparedUpdateAsync`; Sicherheitsprüfung, Fortschrittsdialog und geordnetes Beenden müssen bei einer Startautomatik berücksichtigt werden.
