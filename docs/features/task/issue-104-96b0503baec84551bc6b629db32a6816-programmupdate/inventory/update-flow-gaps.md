# Update-Ablauf: Anknuepfpunkte und fehlende Bausteine

## Vorhandene Anknuepfpunkte

Vorhanden:

- Programmverzeichnis zur Laufzeit ueber `AppContext.BaseDirectory` nutzbar; die App verwendet es bereits fuer Logs (`App.xaml.cs:33-40`).
- Zentrale DI und Logging mit Serilog.
- Dialogservice fuer Sicherheitsabfragen.
- Persistierte aktive Aufgaben inkl. CLI-Laufstatus.
- Release-ZIP `release.zip` in GitHub Releases.
- WPF-Shell, in die ein globaler Button integriert werden kann.

## Fehlende Bausteine

Nicht vorhanden:

- Update-Check-Service.
- GitHub-Release-HTTP-Client.
- Lokale Versionsquelle fuer die WPF-App.
- Download-Service fuer Release-Assets.
- ZIP-Validierung/Entpacken.
- Update-Arbeitsverzeichnis-Management im Programmverzeichnis.
- Externer Updater oder PowerShell-/Batch-Skript.
- Neustartlogik.
- Rollback-/Backup-Konzept.
- Update-spezifische Logs ausserhalb normaler App-Logs.

## Empfohlener technischer Zuschnitt

Ein moeglicher Zuschnitt fuer die spaetere Planung:

- `Application.Services.UpdateService`: Orchestriert Check, Sicherheitsabfrage-Vorbereitung, Download, Entpacken, Skriptstart.
- `Infrastructure.Services.GitHubReleaseClient`: Ruft GitHub Releases per HTTP ab.
- `Application.Services.ApplicationVersionProvider`: Liefert lokale Version.
- `Application.Services.UpdatePackageService`: Download, Temp-Verzeichnis, ZIP-Entpackung, Basisvalidierung.
- `Application.Services.UpdateScriptService`: erzeugt PowerShell-Skript und startet es.
- `Application.Services.CliUpdateSafetyService`: findet aktive nicht-wartende CLI-Aufgaben.

## Sicherheitspruefung CLI

Die Anforderung verlangt eine Abfrage bei aktiven CLI-Ausfuehrungen, die nicht auf Eingabe warten. Die vorhandenen Daten erlauben diese Bewertung:

- `AufgabeService.GetAktiveAufgabenAsync()` liefert aktive/wartende Aufgaben.
- `Aufgabe.AktiveRunId != null` zeigt einen aktiven Lauf an.
- `Aufgabe.LaufStatus == AufgabeLaufStatus.WartetAufEingabe` zeigt den weniger kritischen Wartestatus.
- `Aufgabe.LaufStatus == AufgabeLaufStatus.Laeuft` oder `null` sollte als riskant gelten.

Eine reine Nutzung von `IRunningAutomationStatusSource.GetRunningCount()` waere zu grob, weil sie wartende und laufende Prozesse nicht unterscheidet.

## Temp- und Skriptpfade

Die Anforderung will ein temporaeres Verzeichnis innerhalb des Programmverzeichnisses. Vorschlag fuer Pfadstruktur:

- `<AppBase>updates/download/release.zip`
- `<AppBase>updates/extracted/<version>/`
- `<AppBase>updates/update.ps1`
- `<AppBase>updates/update.log`

Risiken:

- Programmverzeichnis ist eventuell nicht beschreibbar.
- Das Update-Skript darf nicht aus einem Verzeichnis geloescht werden, bevor es fertig ist.
- Schreibende Dateioperationen muessen Pfade mit Leerzeichen robust quoten.
- Vorhandene alte Update-Verzeichnisse muessen kontrolliert bereinigt werden.

## Externer Austauschprozess

Der finale Austausch muss ausserhalb der laufenden App passieren. Ein PowerShell-Skript ist passend zur Windows/WPF-Zielplattform und vorhandenen PowerShell-Skripten im Repo.

Mindestparameter:

- PID der laufenden App.
- Zielverzeichnis `AppContext.BaseDirectory`.
- Entpack-Verzeichnis.
- Exe-Pfad oder Exe-Name `Softwareschmiede.exe`.
- Logpfad.

Mindestverhalten:

1. Warten, bis die App beendet ist; optional selbst beenden/anfordern.
2. Zielverzeichnis nicht komplett loeschen, sondern Dateien aus entpacktem Release kontrolliert kopieren/ersetzen.
3. Kritische Update-Artefakte und Logs nicht waehrend des Kopierens verlieren.
4. Bei Erfolg App neu starten.
5. Bei Fehler Log schreiben und Zielzustand nachvollziehbar lassen.

## Besondere Release-Artefakt-Implikation

Der CI-Publish benennt die Exe in Releases nach `Softwareschmiede.exe` um. Lokale Dev-Builds heissen dagegen `Softwareschmiede.App.exe`. Der Update-Prozess sollte deshalb fuer installierte Releases `Softwareschmiede.exe` erwarten, aber Tests duerfen nicht versehentlich die self-hosting Release-Instanz beenden. Diese Unterscheidung ist bereits in `.github/actions/build-and-package/action.yml:24-34` dokumentiert.
