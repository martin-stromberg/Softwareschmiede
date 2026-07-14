# Umsetzungsplan - Programmupdate

## Zielbild

Die WPF-Anwendung prueft beim Programmstart GitHub-Releases von `martin-stromberg/Softwareschmiede`, vergleicht die neueste stabile Release-Version mit der lokal installierten Version aus `version.json` und zeigt nur bei verfuegbarem Update einen Button im unteren Bereich der linken Sidebar. Zusaetzlich gibt es dort einen Refresh-Button als Symbol, mit dem der Benutzer die Update-Pruefung manuell erneut starten kann.

Der Update-Button startet einen gefuehrten Ablauf: CLI-Risiko pruefen, optional Sicherheitsabfrage anzeigen, `release.zip` herunterladen, in ein Update-Verzeichnis innerhalb des Programmverzeichnisses entpacken, die Update-Vorbereitung mit Fortschrittsdialog anzeigen, ein externes PowerShell-Update-Skript starten und die laufende App beenden. Das Skript ersetzt danach die Programmdateien und startet `Softwareschmiede.exe` neu. Falls fuer den finalen Austausch erhoehte Rechte erforderlich sind, wird der externe Prozess mit Elevation gestartet.

## Leitentscheidungen fuer die Umsetzung

- Lokale Version wird verbindlich aus einer im Publish enthaltenen Datei `version.json` gelesen.
- `version.json` ist die einzige fachlich verbindliche Quelle fuer die installierte Programmversion; Assembly-, File- oder Informational-Versionen duerfen hoechstens als Diagnoseinformation dienen.
- GitHub Pre-Releases werden ignoriert. Es werden nur stabile Releases als Update angeboten.
- Das erwartete Release-Asset heisst exakt `release.zip`.
- Der Update-Check laeuft automatisch nur beim Programmstart. Eine manuelle erneute Pruefung erfolgt ueber einen Refresh-Button als Symbol in der Sidebar.
- Der Update-Prozess muss mit fehlenden Schreibrechten umgehen. Falls notwendig, wird der externe Updater mit erhoehten Rechten gestartet.
- Der externe Austausch erfolgt per PowerShell-Skript, weil Zielplattform Windows/WPF ist und PowerShell im Repository bereits als Skriptumgebung genutzt wird.
- Riskante CLI-Ausfuehrungen sind aktive Aufgaben mit `AktiveRunId != null` und `LaufStatus != AufgabeLaufStatus.WartetAufEingabe`; `LaufStatus == null` gilt konservativ als riskant.
- Es wird kein Rollback implementiert. Der Plan verlangt aber atomare Download-Vorbereitung, frisches Entpack-Verzeichnis, Basisvalidierung, Skript-Logging und kontrollierte Fehlerzustaende, damit kein unkontrolliert teilweise vorbereitetes Update entsteht.
- Download, Entpacken und Update-Vorbereitung werden fuer den Benutzer in einem Fortschrittsdialog sichtbar gemacht.

## Umsetzungsschritte

### 1. `version.json` im Release-Build herstellen

1. Format fuer `version.json` definieren, z. B.:

   ```json
   {
     "version": "1.2.3",
     "tagName": "v1.2.3",
     "commit": "<sha>",
     "createdAtUtc": "2026-07-14T00:00:00Z"
   }
   ```

2. In `.github/actions/build-and-package/action.yml` vor dem ZIP-Erstellen eine `version.json` in den Publish-Ordner schreiben.
3. Die Version aus dem Release-Tag bzw. Semantic-Release-Kontext ableiten; bei Tags `vX.Y.Z` das fuehrende `v` fuer das Feld `version` entfernen.
4. Sicherstellen, dass `version.json` im Root von `release.zip` liegt, neben `Softwareschmiede.exe`.
5. Die Publish-Guard-Pruefung erweitern: `Softwareschmiede.exe` und `version.json` muessen vorhanden sein.
6. Optional MSBuild-Versionen weiterhin setzen, aber nicht als Quelle fuer den Update-Vergleich verwenden.

Testbar durch:
- Test oder Skriptpruefung, dass `release.zip` eine Root-Datei `version.json` enthaelt.
- Unit-Tests fuer das Parsen gueltiger und ungueltiger `version.json`-Dateien.

### 2. Update-Domaenenmodelle und Optionen anlegen

1. Neue Modelle im Application-Layer anlegen, z. B. unter `src/Softwareschmiede/Application/Services/Updates/`:
   - `UpdateOptions` mit Repository Owner, Repository Name, Asset-Name `release.zip`, optionalem Check-Timeout und Update-Unterordnername `updates`.
   - `InstalledVersionInfo` mit `Version`, `TagName`, `Commit`, `CreatedAtUtc`.
   - `UpdateInfo` mit `Version`, `TagName`, `AssetName`, `DownloadUrl`, `PublishedAt`.
   - `UpdateCheckResult` mit Statuswerten wie `KeinUpdate`, `UpdateVerfuegbar`, `NichtPruefbar`.
   - `UpdatePreparationProgress` mit Phase, Prozentwert optional und Meldung fuer den Fortschrittsdialog.
   - `UpdatePreparationResult` mit Pfaden fuer ZIP, Entpack-Verzeichnis, Skript und Log.
2. Einen robusten SemVer-Vergleich kapseln, der Tags `v1.2.3` und `1.2.3` akzeptiert und ungueltige Tags ignoriert.
3. Fehler- und Statusmeldungen so halten, dass das ViewModel nur UI-Zustand ableitet und keine HTTP-/Dateisystemdetails kennen muss.

Testbar durch:
- Versionsvergleich-Tests fuer gleich, neuer, aelter, `v`-Prefix und ungueltige Tags.
- Tests fuer Options-Defaults und Asset-Namen `release.zip`.

### 3. Lokalen `version.json`-Provider implementieren

1. Interface `IApplicationVersionProvider` einfuehren.
2. Implementierung liest `Path.Combine(AppContext.BaseDirectory, "version.json")`.
3. JSON strukturiert deserialisieren und `version` als Pflichtfeld behandeln.
4. Ungueltige, fehlende oder nicht parsebare Dateien als `NichtPruefbar` behandeln und loggen, nicht als Update erzwingen.
5. Fuer Tests den Basispfad injizierbar machen, damit keine echte App-Installation benoetigt wird.

Testbar durch:
- Unit-Tests fuer gueltige Datei, fehlende Datei, leeres JSON, fehlendes `version`, ungueltige SemVer und Pfade mit Leerzeichen.

### 4. GitHub-Release-Abfrage implementieren

1. Interface `IGitHubReleaseClient` oder neutraler `IUpdateReleaseClient` anlegen.
2. Infrastruktur-Implementierung unter `src/Softwareschmiede/Infrastructure/Services/` bauen:
   - `HttpClient` direkt oder ueber `IHttpClientFactory` verwenden.
   - GitHub REST API `https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/latest` abfragen.
   - Header `User-Agent` setzen.
   - `tag_name`, `prerelease`, `assets[].name`, `assets[].browser_download_url` deserialisieren.
   - `prerelease == true`, fehlendes `release.zip`, HTTP-Fehler, Rate-Limits und JSON-Fehler tolerant als `NichtPruefbar` behandeln.
3. Download-URL nie aus Asset-Namen selbst zusammensetzen, sondern aus `browser_download_url` nehmen.
4. Da Pre-Releases ignoriert werden, ist `releases/latest` passend. Optional defensiv `prerelease` trotzdem pruefen.

Testbar durch:
- Fake-HTTP-Handler fuer neues Release mit exakt `release.zip`.
- Tests fuer falschen Asset-Namen, 404/500, Rate-Limit, ungueltiges JSON und Pre-Release.

### 5. Update-Service als Orchestrator bauen

1. `IUpdateService` im Application-Layer einfuehren mit Methoden:
   - `Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct)`
   - `Task<UpdatePreparationResult> PrepareUpdateAsync(UpdateInfo update, IProgress<UpdatePreparationProgress> progress, CancellationToken ct)`
   - `Task StartPreparedUpdateAsync(UpdatePreparationResult preparation, CancellationToken ct)`
2. Der Service nutzt Versionsprovider, Release-Client, Package-Service und Script-Service.
3. Check-Ergebnisse im Speicher cachen, damit Sidebar-Bindings nicht unbeabsichtigt mehrfach GitHub abfragen.
4. Beim Programmstart genau einen nicht-blockierenden Check anstossen.
5. Der Refresh-Button ruft bewusst erneut `CheckForUpdateAsync` auf und aktualisiert den UI-State.
6. Beim Klick auf den Update-Button vor Download erneut `CheckForUpdateAsync` ausfuehren, damit veraltete UI-Informationen kein falsches Update starten.
7. Netzwerk-/Versions-/Assetfehler loggen und als "kein Button sichtbar" oder dezente Fehlermeldung fuer manuelles Refresh behandeln.

Testbar durch:
- Unit-Tests mit Mocks fuer lokale Version und Remote-Releases.
- Tests, dass gleiche/aeltere Version keinen Update-Button-Status liefert.
- Test, dass automatischer Start-Check nur einmal angestossen wird.
- Test, dass manueller Refresh eine erneute Pruefung ausloest.

### 6. CLI-Sicherheitspruefung kapseln

1. `ICliUpdateSafetyService` anlegen.
2. Implementierung nutzt `AufgabeService.GetAktiveAufgabenAsync()`.
3. Riskante Aufgaben filtern:
   - `AktiveRunId != null`
   - `LaufStatus == AufgabeLaufStatus.Laeuft` oder `LaufStatus == null`
   - allgemein `LaufStatus != AufgabeLaufStatus.WartetAufEingabe`
4. Rueckgabe enthaelt Anzahl und optional Titel/IDs fuer Dialogtext und Logs.
5. `IRunningAutomationStatusSource.GetRunningCount()` nicht als alleinige Quelle verwenden, weil damit wartende und laufende Prozesse nicht unterscheidbar sind.

Testbar durch:
- Tests fuer keine aktive Aufgabe, wartende Aufgabe, laufende Aufgabe und null-Status.
- Test, dass nur riskante Aufgaben die Sicherheitsabfrage erzwingen.

### 7. Download, Entpacken und Validierung implementieren

1. `IUpdatePackageService` anlegen.
2. Arbeitsverzeichnis unter `Path.Combine(AppContext.BaseDirectory, "updates")` verwalten:
   - `download/release.zip.download`
   - `download/release.zip`
   - `extracted/<version>/`
   - `update.ps1`
   - `update.log`
3. Vorbereitungsablauf mit Fortschrittsmeldungen:
   - alte unvollstaendige `.download`-Dateien loeschen.
   - versionsspezifisches Entpack-Verzeichnis frisch anlegen.
   - Phase `Download` melden und Asset per Streaming herunterladen.
   - nach Erfolg `.download` atomar nach `.zip` verschieben.
   - Dateigroesse > 0 pruefen.
   - Phase `Entpacken` melden und ZIP nach `extracted/<version>/` entpacken.
   - Basisvalidierung: `Softwareschmiede.exe` und `version.json` muessen im Entpack-Root vorhanden sein.
   - Phase `UpdateVorbereiten` melden und Skriptdatei erzeugen.
4. Schreib-/ZIP-/I/O-Fehler abfangen, loggen und vorbereitete Teilverzeichnisse kontrolliert bereinigen, soweit sie nur Update-Artefakte enthalten.
5. Keine Dateien im eigentlichen Programmverzeichnis ersetzen; dieser Schritt bleibt dem externen Skript vorbehalten.

Testbar durch:
- Temp-Verzeichnis-Tests fuer gueltiges ZIP, korruptes ZIP, leeres ZIP, fehlende Exe, fehlende `version.json` und Pfade mit Leerzeichen.
- Test, dass `.download` nur bei erfolgreichem Download in `.zip` umbenannt wird.

### 8. Externes Update-Skript erzeugen und starten

1. `IUpdateScriptService` anlegen.
2. PowerShell-Skript `updates/update.ps1` generieren. Parameter:
   - laufende App-PID
   - Zielverzeichnis `AppContext.BaseDirectory`
   - Entpack-Verzeichnis
   - Exe-Pfad `Softwareschmiede.exe`
   - Logpfad
3. Skriptverhalten:
   - Logdatei oeffnen und jeden Hauptschritt protokollieren.
   - laufenden Prozess per PID freundlich beenden, falls noch aktiv, und begrenzt warten.
   - nach Timeout Prozess beenden, weil der Benutzer den Update-Ablauf bereits bestaetigt hat.
   - Dateien aus Entpack-Verzeichnis rekursiv in das Zielverzeichnis kopieren/ersetzen.
   - `version.json` mit der neuen Version ersetzen.
   - Update-Artefakte und Logdatei nicht waehrend des Kopierens loeschen.
   - bei Erfolg `Softwareschmiede.exe` aus dem Zielverzeichnis starten.
   - bei Fehler mit Exitcode abbrechen und Log erhalten.
4. Skriptstart ueber `ProcessStartInfo` mit `powershell.exe` oder `pwsh`-Fallback pruefen; `ExecutionPolicy Bypass`, `NoProfile` und korrekt gequotete Argumente verwenden.
5. Falls Schreibrechte fuer das Zielverzeichnis fehlen oder beim Vorabcheck als unsicher gelten, `ProcessStartInfo.Verb = "runas"` verwenden, damit Windows eine UAC-Abfrage fuer erhoehte Rechte anzeigt.
6. Nach erfolgreichem Skriptstart beendet die App sich selbst geordnet ueber WPF `Application.Current.Shutdown()`.

Testbar durch:
- Skriptgenerator-Tests fuer Pfade mit Leerzeichen, Sonderzeichen, PID, Logpfad und Exe-Pfad.
- Unit-Test, dass `ProcessStartInfo` keine Shell-String-Verkettung fuer Dateipfade nutzt.
- Test, dass bei Elevation-Bedarf `Verb = "runas"` gesetzt wird.
- Optional ein isolierter Integrationstest mit Dummy-Zielverzeichnis und Dummy-Exe-Ersatz, ohne die echte App zu beenden.

### 9. Fortschrittsdialog fuer Update-Vorbereitung

1. Eine kleine Dialog-/ViewModel-Struktur fuer den Update-Fortschritt ergaenzen, z. B. `UpdateProgressDialog` und `UpdateProgressViewModel`.
2. Phasen anzeigen:
   - Download
   - Entpacken
   - Update-Vorbereitung
3. Prozentwert anzeigen, wenn beim Download `Content-Length` bekannt ist; andernfalls indeterminaten Fortschritt verwenden.
4. Dialog waehrend Download/Entpacken/Vorbereitung modal oder klar fokussiert halten, damit der Benutzer den laufenden Update-Prozess erkennt.
5. Abbrechen nur anbieten, solange das externe Skript noch nicht gestartet wurde. Nach Skriptstart wird die App beendet.
6. Fehler aus dem Orchestrator im Dialog oder ueber vorhandenen Dialogservice anzeigen; die App bleibt dann laufen.

Testbar durch:
- ViewModel-Tests fuer Phasenwechsel, Prozentwerte, indeterminaten Zustand und Fehlerstatus.
- Update-Service-Test, dass Progress-Events in erwarteter Reihenfolge gemeldet werden.

### 10. Sidebar-Button, Refresh-Button und ViewModel-Integration

1. `MainWindowViewModel` erweitern:
   - `bool UpdateVerfuegbar`
   - `UpdateInfo? VerfuegbaresUpdate`
   - `bool UpdateCheckLaeuft`
   - `bool UpdateWirdVorbereitet`
   - `ICommand UpdateStartenCommand`
   - `ICommand UpdatePruefenCommand`
2. Beim Start des ViewModels einen nicht-blockierenden Update-Check anstossen. Ergebnis per Dispatcher in Properties uebernehmen.
3. Kein periodisches GitHub-Polling an den vorhandenen 5-Sekunden-Timer haengen.
4. `UpdatePruefenCommand` als Refresh-Symbol ausfuehren:
   - waehrend laufendem Check deaktivieren.
   - bei verfuegbarem Update den Update-Button sichtbar machen.
   - bei keinem Update den Update-Button ausblenden.
   - bei Fehlern dezent informieren oder loggen, ohne die App-Nutzung zu blockieren.
5. `UpdateStartenCommand`:
   - erneuten Update-Check ausfuehren.
   - wenn kein Update mehr verfuegbar ist, Button ausblenden und abbrechen.
   - `ICliUpdateSafetyService` abfragen.
   - bei riskanten CLI-Aufgaben `IDialogService.BestaetigenDialog` anzeigen; bei Nein abbrechen.
   - Fortschrittsdialog oeffnen.
   - Download/Entpacken/Skriptvorbereitung ausfuehren.
   - waehrend Vorbereitung Command deaktivieren.
   - Skriptstart ausfuehren und danach App beenden.
   - Fehler loggen und im Fortschrittsdialog oder ueber Dialogservice anzeigen.
6. `MainWindow.xaml` Sidebar-Grid auf drei Zeilen erweitern:
   - Header/Navigation
   - aktive Aufgaben-Scrollbereich
   - Footer mit Update-Button und Refresh-Icon-Button
7. Update-Button im Footer an `UpdateStartenCommand` und `UpdateVerfuegbar` binden.
8. Refresh-Button als Symbolbutton an `UpdatePruefenCommand` binden. Eingeklappte Navigation zeigt nur Icon; aufgeklappte Navigation kann einen Tooltip oder kurzen Text verwenden, passend zu bestehenden Sidebar-Patterns.

Testbar durch:
- `MainWindowViewModelTests` erweitern: Button-State bei Update/kein Update, Refresh-Command, automatischer Start-Check, Command ruft Services auf, Sicherheitsdialog erscheint bei riskanten Aufgaben, Abbrechen stoppt Ablauf.
- UI-/FlaUI-Test optional: Sidebar-Footer erscheint bei `UpdateVerfuegbar`.

### 11. DI und Konfiguration registrieren

1. In `src/Softwareschmiede.App/App.xaml.cs` registrieren:
   - `UpdateOptions`
   - `IApplicationVersionProvider`
   - `IUpdateReleaseClient` / `IGitHubReleaseClient`
   - `IUpdateService`
   - `ICliUpdateSafetyService`
   - `IUpdatePackageService`
   - `IUpdateScriptService`
   - Fortschrittsdialog/ViewModel oder passende Dialogfactory
2. Falls `IHttpClientFactory` genutzt wird, `Microsoft.Extensions.Http` referenzieren und `services.AddHttpClient(...)` verwenden. Andernfalls `HttpClient` kontrolliert singleton-/service-lifetime-konform bereitstellen.
3. `MainWindowViewModel`-Konstruktor um die neuen Services und `IDialogService` erweitern. Bestehende Tests entsprechend mit Mocks anpassen.

Testbar durch:
- Host-/DI-Test oder bestehender App-Test, der `MainWindowViewModel` aus einem ServiceProvider aufloest.
- Build mit `WarningsAsErrors`, weil neue public/internal Typen XML-Dokumentation brauchen.

### 12. Fehlerbehandlung und Logging

1. Update-Check-Fehler beim Start nur warnend loggen; die Anwendung startet und laeuft normal weiter.
2. Fehler beim manuellen Refresh knapp melden oder in der UI kenntlich machen, ohne einen Update-Button anzuzeigen.
3. Download-/Entpackfehler dem Benutzer im Fortschrittsdialog anzeigen und keine Skriptdatei starten.
4. Skriptstartfehler dem Benutzer anzeigen und die App nicht beenden.
5. Skriptfehler in `updates/update.log` protokollieren; App kann in diesem Fall nicht immer selbst informieren, weil sie schon beendet sein kann.
6. Alte Update-Artefakte nur innerhalb des eigenen `updates`-Verzeichnisses bereinigen und Pfade vor Loeschoperationen gegen `AppContext.BaseDirectory` validieren.
7. Kein Rollback: Bei Fehlern bleiben Logs und der bisherige Zielzustand nachvollziehbar erhalten. Der Plan versucht keine automatische Wiederherstellung alter Programmdateien.

Testbar durch:
- Unit-Tests fuer Fehlerpfade im Orchestrator.
- Package-Service-Tests, dass Bereinigung nicht ausserhalb des Update-Verzeichnisses arbeitet.
- Tests fuer Skriptstartfehler und Elevation-Abbruch.

### 13. Tests ausbauen

1. In `src/Softwareschmiede.Tests` neue Testklassen anlegen:
   - `Application/Services/Updates/ApplicationVersionProviderTests.cs`
   - `Application/Services/Updates/UpdateVersionComparerTests.cs`
   - `Application/Services/Updates/UpdateServiceTests.cs`
   - `Application/Services/Updates/CliUpdateSafetyServiceTests.cs`
   - `Application/Services/Updates/UpdatePackageServiceTests.cs`
   - `Application/Services/Updates/UpdateScriptServiceTests.cs`
   - `Application/Services/Updates/UpdateProgressViewModelTests.cs`
   - `Infrastructure/Services/GitHubReleaseClientTests.cs`
2. `App/ViewModels/MainWindowViewModelTests.cs` um Update-Button-, Refresh-Button- und Command-Szenarien erweitern.
3. Tests duerfen keine echte GitHub-API, keine echte App-Beendigung, keine echte UAC-Elevation und keinen echten Austausch der Repo-Dateien ausloesen.
4. Fuer Dateisystemtests ausschliesslich isolierte Temp-Verzeichnisse verwenden.
5. Fuer potenziell instabile Volltests das vorhandene `scripts/Run-AllTestsIndividually.ps1` als Zusatzpruefung dokumentieren.

Auszufuehrende Tests:
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- Bei Bedarf: `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`
- Bei Flakiness: `powershell -ExecutionPolicy Bypass -File scripts/Run-AllTestsIndividually.ps1`

### 14. Dokumentation aktualisieren

1. Anwenderdokumentation unter `docs/help/` ergaenzen:
   - Wann der Update-Button sichtbar ist.
   - Was der Refresh-Button prueft.
   - Was die CLI-Sicherheitsabfrage bedeutet.
   - Was der Fortschrittsdialog waehrend Download/Entpacken/Vorbereitung zeigt.
   - Wo Update-Logs liegen.
   - Was bei UAC-Abfrage, fehlenden Schreibrechten oder fehlgeschlagenem Update zu tun ist.
2. README um Release-/Update-Hinweise ergaenzen:
   - GitHub-Release-Asset exakt `release.zip`.
   - lokale Versionsquelle `version.json`.
   - Pre-Releases werden nicht angeboten.
   - finaler Austausch kann erhoehte Rechte anfordern.
   - kein automatisches Rollback.
3. Entwicklerhinweise zum Skript, zu `version.json` und zu Tests dokumentieren.

## Akzeptanzkriterien-Abdeckung

| Akzeptanzkriterium | Planabdeckung |
|---|---|
| Automatische Release-Pruefung | Schritte 3 bis 5 und 10 |
| Kein Button ohne neue Version | Schritte 5 und 10 |
| Button unten in linker Sidebar bei Update | Schritt 10 |
| Pruefung aktiver CLI-Ausfuehrungen | Schritt 6 |
| Sicherheitsabfrage mit Abbruch | Schritte 6 und 10 |
| Release-ZIP herunterladen | Schritt 7 |
| ZIP im Programmverzeichnis entpacken | Schritt 7 |
| Skript beendet laufende Anwendung | Schritt 8 |
| Skript ersetzt Programmdateien | Schritt 8 |
| Skript startet Anwendung neu | Schritt 8 |
| Kein unkontrolliert teilweise aktualisierter Zustand | Schritte 7, 8 und 12 |
| Fortschrittsdialog fuer Download/Entpacken/Vorbereitung | Schritte 7, 9 und 10 |
| Manuelle erneute Pruefung per Refresh-Symbol | Schritte 5 und 10 |

## Offene Punkte

