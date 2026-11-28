# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Zusammenfassung

- Gesamt: 3035
- Bestanden: 3032
- Fehlgeschlagen: 0
- Übersprungen: 3

Ausgeführte Lanes (jeweils nach vollständigem `dotnet build Softwareschmiede.slnx` — 0 Fehler, 0 Warnungen — ohne `--no-build`, synchron, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):

| Lane | Ergebnis |
|------|----------|
| `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | 2881 gesamt: 2878 bestanden, 3 übersprungen, 0 fehlgeschlagen (3,9 min) |
| `dotnet test server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj` | 154 gesamt: 154 bestanden, 0 fehlgeschlagen (8 s) |

Die 3 Übersprungenen sind die erwarteten ConPTY-Skips (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`); OsInterface-Tests wurden laut Vorgabe nicht ausgeführt.

Delta zum vorherigen Lauf (151 → 154 Server-Tests, Haupt-Lane unverändert): die drei neuen Tests zu den Review-Fixes bestanden alle — `Auth/AdminUpdateSettingsTests.UpdateInstall_CanceledInstall_RedirectsNotStarted` (Outcome-Differenzierung `PostUpdateInstall`), `Pages/AdminPagesComposerTests.RenderUpdateInstallStarted_ExplainsRestart` (`updateLogPath`-Parameter) und `Update/StatusServerUpdateServiceTests.ApplySettings_DuringManualRun_WaitsAndKeepsPersistedValue` (`ApplySettingsAsync` unter `s_manualOverrideLock`).

## Testabdeckung

**Abdeckung:** 94,9 % (Zeilenabdeckung, gemessen am Projekt `Softwareschmiede.StatusServer` via `--collect:"XPlat Code Coverage"`; die Desktop-Testsuite wurde nicht separat instrumentiert)

| Datei | Abdeckung |
|-------|-----------|
| `server/Softwareschmiede.StatusServer/Data/StatusServerDatabaseInitializer.cs` | 76,5 % |
| `server/Softwareschmiede.StatusServer/Data/StatusServerDbContextDesignTimeFactory.cs` | 0 % |

Alle `Update/`-Dateien liegen bei ≥ 90 % (`StatusServerUpdateService` 97,6 %, `AutoUpdateApplicationExtensions` 91,5 %, `StatusServerAutoUpdateOptionsMapper` 90,6 %, `StatusServerUpdateSettingsCodec` 92,9 %; `UpdateStatusMapper`, `StatusServerUpdateSourceFactory`, `StatusServerUpdateOptions`, `StatusServerUpdateSettings`, `StatusServerUpdateStatus` 100 %).

## Fehlende Tests

Quelle: `Coverage-Daten`

- `server/Softwareschmiede.StatusServer/Data/StatusServerDbContextDesignTimeFactory.cs` — 0 % Abdeckung (reine Design-Time-Factory für `dotnet ef`, kein Laufzeitcode; nicht Teil dieses Features)

## Format-Status

`dotnet format Softwareschmiede.slnx --verify-no-changes` — **sauber** (Exit 0, keine Verstöße; Spiegel des Pre-Push-Hooks).

## Reale End-to-End-Abnahme

*Übernommen aus dem vorherigen Testlauf. Seitdem kamen nur drei Review-Fixes hinzu; Bewertung der Auswirkung auf die dokumentierte Abnahme:*

- **Outcome-Differenzierung `PostUpdateInstall`:** betrifft das Redirect-Mapping nach `InstallAsync`. In der realen Abnahme wurde davon nur der Negativpfad ohne `confirmDowntime` geprüft (Fehlerseite vor dem eigentlichen Installationsanlauf — vom Mapping unberührt). Der neu differenzierte `Canceled`-Pfad ist durch `UpdateInstall_CanceledInstall_RedirectsNotStarted` testseitig abgedeckt; der Success-Pfad bleibt an die nicht durchführbare reale Installation gebunden. **Keine Beeinträchtigung der dokumentierten Abnahme.**
- **`updateLogPath`-Parameter:** reine Anzeige des Log-Pfads auf der „gestartet"-Seite (konfigurierbares `Update:WorkingDirectory` statt Literal). Belegt durch `RenderUpdateInstallStarted_ExplainsRestart`. **Keine Beeinträchtigung.**
- **`ApplySettingsAsync` unter `s_manualOverrideLock`:** Serialisierungs-Detail beim Übernehmen persistierter Einstellungen während eines manuellen Laufs. Belegt durch `ApplySettings_DuringManualRun_WaitsAndKeepsPersistedValue`. **Keine Beeinträchtigung.**

Durchgeführte Abnahme gegen eine **real gestartete** StatusServer-Instanz (Kestrel auf `http://127.0.0.1:5099`, Content-Root `%TEMP%\statusserver-e2e\app` mit `release-metadata.json` Version 1.0.0, eigene SQLite-DB), konfiguriert mit `Update:SourceType=LocalFolder` + `Update:LocalFolderPath` auf ein präpariertes Quellverzeichnis (echtes ZIP-Paket `statusserver-win-x64.zip` + `statusserver-update.json`-Manifest mit realem SHA-256, Version 9.9.9 — analog `TestSupport/TempUpdateSource.cs`). Bedienung real per HTTP: Registrierung → Admin-Login-Cookie → Antiforgery-Tokens → Formular-POSTs. Der Server wurde nach der Abnahme sauber beendet (eigene Instanz, PID verifiziert; kein `Softwareschmiede.App.exe` angefasst).

**Belege** (gerenderte HTML-Artefakte, da keine Screenshot-Möglichkeit in dieser Umgebung — kein Browser-Automatisierungs-Tool verfügbar): `e2e-artifacts/` dieses Ordners.

### Happy Path (plan.md, Akzeptanzszenarien)

| # | Szenario | Ergebnis |
|---|----------|----------|
| 1 | Admin anmelden, `/admin/settings`: Updates-Sektion mit installierter Version, `Idle`, Stable, Off | **Verifiziert** — Seite zeigt „Installierte Version 1.0.0" (aus `release-metadata.json`), Zustand „Bereit", Kanal Stable, Modus Aus (`e2e-artifacts/admin-settings-initial.html`) |
| 2 | Kanal Pre-Release + Modus „Nur prüfen" speichern → `?msg=update-settings-saved`, Persistenz in `ServerSettings` | **Verifiziert** — Redirect `?msg=update-settings-saved`; nach **echtem Prozess-Neustart** stehen `prerelease`/`check` weiterhin selektiert (aus der SQLite-`ServerSettings`-Tabelle geladen; `e2e-artifacts/admin-settings-after-restart.html`) |
| 3 | „Jetzt nach Update suchen" → Check + Download + SHA-256 → `ReadyToInstall` + verfügbare Version | **Verifiziert** — Redirect `?msg=update-check-done`; Seite zeigt „Bereit zur Installation" + `9.9.9`; `updates/status.json` `state=4`, `checksumValid=true`, Paket in `updates/pending/` (`e2e-artifacts/admin-settings-ready-to-install.html`) |
| 4 | „Update jetzt installieren" mit Downtime → detached Skript → Prozess-Neustart | **Nicht durchführbar** — `InstallAsync` startet ein detached OS-Skript, das den Host-Prozess stoppt und Binärdateien im Installationsverzeichnis austauscht. Gegen einen `dotnet <dll>`-Dev-Host existiert weder Service-/IIS- noch Exe-Lifecycle zum Wiederanbinden; ein echter Neustart eines produktiven Servers ist in der Sandbox nicht sinnvoll darstellbar. Verifiziert bis zur dokumentierten Testgrenze `ReadyToInstall`; die Negativprüfung ohne `confirmDowntime` wurde real abgenommen (Fehlerseite, nichts gestartet — `e2e-artifacts/admin-settings-install-no-confirm.html`). Orchestrierung zusätzlich durch `StatusServerUpdateServiceTests`/`AdminUpdateSettingsTests` abgedeckt |
| 5 | Nach Neustart: neue Version installiert, `data/` + `appsettings*.json` unverändert | **Nicht durchführbar** — hängt an Szenario 4 (kein realer Dateiaustausch möglich). Teilaspekt Datenerhalt über Neustart verifiziert: `ServerSettings` (Kanal/Modus) und `updates/status.json` überlebten den echten Prozess-Neustart; `PreserveFile("appsettings*.json")`/`PreserveFile("data/*")` ist konfiguriert und review-belegt |
| 6 | Nachvollziehbarkeit: Status-Sektion, `updates/status.json`, `updates/update.log`, ILogger | **Verifiziert** — Status-Sektion zeigt Zustand/Version/letzte Prüfung/Fehler; `updates/status.json` mit vollständigem Snapshot vorhanden; `update.log` wird erwartungsgemäß erst vom Installationsskript geschrieben (nicht ausgelöst); ILogger-Ausgaben im Server-Konsolenlog sichtbar |
| 7 | Automatik-Pfad `CheckAndInstall` + Uhrzeit | **Teilweise verifiziert** — `AutoUpdateCheckerService` lief real mit den persistierten Einstellungen an (nach Neustart mit Modus `check`/`Enabled=true` erfolgte eigenständig eine neue Prüfung: „Letzte Prüfung" aktualisiert auf 20:02, Zustand „Update verfügbar"). Die tageszeitgesteuerte Installation wurde nicht abgewartet; Mapping `CheckAndInstall` → Download+Install-Flags+`ScheduledInstallTime` ist durch `StatusServerAutoUpdateOptionsMapperTests` und den Neustart-E2E-Test belegt |

### Fehlschlag-Szenarien

| Szenario | Ergebnis |
|----------|----------|
| Hash-Mismatch | **Verifiziert** — Manifest-SHA-256 manipuliert → POST `/admin/settings/update/check` → Redirect `?err=update-check-failed`; UI: Zustand „Fehlgeschlagen", „Letzter Fehler: Update package hash does not match the release manifest."; `status.json` `state=7`/`errorCode=7` (`e2e-artifacts/admin-settings-hash-mismatch.html`, `e2e-artifacts/updates-status-failed.json`) |
| Netzfehler/Quelle nicht erreichbar | **Durch Tests belegt** (`UpdateCheck_Post_RedirectsPerOutcome`, `StatusServerUpdateSourceFactoryTests`); live nicht separat provoziert |
| Gesperrte Dateien (Windows) | **Nicht durchführbar** — Skript-Ebene, erfordert reale Installation (s. Szenario 4); Retry-Logik liegt im Bibliotheksskript |
| Installations-Lock | **Verifiziert (Negativpfad)** — Lock-Reset ohne vorhandenen Lock → Redirect `?err=update-lock-reset-failed`; verwaister-Lock-/Stale-Pfad durch `UpdateLockReset_NoLock_RedirectsWithError` und Service-Tests abgedeckt |
| Fehlende Downtime-Bestätigung | **Verifiziert** — POST ohne `confirmDowntime` → HTTP 200 Fehlerseite „Bitte bestätigen Sie die Downtime …", kein Update gestartet |
| Ungültige Werte | **Verifiziert** — `channel=bogus` → Fehlerseite „Unbekannter Release-Kanal." |

### Datenerhalt

Soweit in der Sandbox prüfbar verifiziert: `data/statusserver.db` (Admin-Benutzer, `ServerSettings` inkl. Update-Einstellungen) überlebte den echten Prozess-Neustart; der `updates/`-Workspace enthält `pending/`-Paket + `status.json`. Der vollständige Dateiaustausch (Paket-Entpackung, Backup/Restore von `appsettings*.json`/`data/*`, Workspace-Bereinigung) ist an die nicht durchführbare Installation gebunden (s. Szenario 4/5).
