# Detailinventar: Tests und Abdeckung

## Vorhandene Unit-Tests

- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`: neues Update, fehlende lokale Version und Fehler beim Skriptstart.
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateVersionComparerTests.cs`: Parsing und stabile Versionsvergleiche.
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs` und `UpdateScriptServiceTests.cs`: Paket- und Skriptpfad.
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`: HTTP-/Releaseverhalten der GitHub-Quelle.
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`: Laden und Speichern bestehender allgemeiner und Plugin-Einstellungen.
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`: Updateprüfung, Vorbereitung und Start aus dem Hauptfenster.

## Vorhandene E2E-Infrastruktur

`src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs` unterstützt das Öffnen, Speichern und erneute Öffnen der Einstellungen. `E2E_SettingsFeatureFlags.cs` zeigt das bestehende Muster für Persistenztests einer Checkbox. Es gibt derzeit kein E2E-Szenario für Updateoptionen, Prereleases oder automatische Installation beim Programmstart.

## Erforderliche spätere Abdeckung

Die Planung sollte mindestens Tests für diese Fälle vorsehen:

1. Alle drei Update-Modi werden geladen, gespeichert und nach erneutem Öffnen wiederhergestellt.
2. `Aus` verhindert Prüfungen; `Nur Prüfen` verhindert automatische Installation; der Startmodus prüft und installiert.
3. Stable-only ignoriert Prereleases; aktivierte Prerelease-Auswahl berücksichtigt sie und vergleicht sie korrekt.
4. Die gewählten Einstellungen erreichen sowohl manuelle Prüfung als auch automatische Installation konsistent.
5. Der relevante Settings-Benutzerfluss wird über FlaUI-E2E nachgewiesen, einschließlich Persistenz.
