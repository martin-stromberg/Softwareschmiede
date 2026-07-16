# Interner Prüfbericht: Secrets-, Pfad- und PII-Scan (Schritt 1)

Dieser Bericht dokumentiert den in `plan.md` unter „Secrets- und Interna-Prüfung“ geforderten
Abschluss-Scan vor der öffentlichen Veröffentlichung des Repositories. Er ist ein internes
Arbeitsdokument dieses Vorhabens (Issue 130) und kein nutzerorientiertes Dokument.

## Ergebnis

**Kein Blocker gefunden.** Es gibt nichts, das vor der Veröffentlichung zwingend entfernt oder
maskiert werden musste.

## 1. Secrets-Scan über den Arbeitsbaum

Muster `key=`, `token=`, `password=`, `secret=`, `apikey`, `ConnectionString`, sowie Dateiendungen
`.pem`, `.pfx`, `.key` wurden repository-weit durchsucht.

- Keine hartcodierten Secrets gefunden. Alle Treffer sind entweder:
  - C#-Property-/Konstanten-Namen ohne Wert (`ApiKey` als Bezeichner, XAML `x:Key=...`)
  - Test-Fixtures mit offensichtlich synthetischen Werten (`"my-very-secret-token"` in
    `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`, `"sk-ant-test-key"`
    in `ClaudeCliPluginTests.cs`)
  - Dokureferenzen auf Umgebungsvariablen-Namen (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`) in
    README.md/`docs/help` — Anleitungstext, kein Wert
  - Ein Kommentar in `.gitignore` ("Connection Strings mit Passwörtern"), kein echter Wert
- Keine Dateien mit Endung `.pem`/`.pfx`/`.key` im Repository.
- Nebenfund (unproblematisch): `.github/upgrades/scenarios/new-dotnet-version_5399b1/` enthält
  Fixture-Code eines fremden Beispielprojekts ("FinanceManager.Web") mit einem Feldnamen
  `_apiKey` — reiner Property-Name ohne Wert, Teil eines .NET-Upgrade-Workflow-Testszenarios,
  fachlich losgelöst vom Softwareschmiede-Produktcode.

## 2. Secrets-Scan über die Git-Historie

`git log -p --all -S"password="`, `-S"secret"`, `-S"api_key"`, `-S"ConnectionString"` ergaben keine
echten Secret-Werte. Die einzigen Treffer sind die eigenen Planungsdokumente dieses Vorhabens sowie
zwei harmlose Commits, die den String `api_key` nur als Test-Feldnamen
(`PluginSettingField("api_key", ...)`) einführen. Kein Hinweis auf jemals committete und wieder
entfernte echte Zugangsdaten.

## 3. Interne Pfade und Kommentarmarker

Durchsucht: `*.ps1`, `*.py`, `*.json`, `*.md`, `*.xaml` (für `src/*.cs` bereits in der Bestandsaufnahme
als trefferfrei verifiziert) nach `C:\Users\`, `C:\Entwicklung\`, UNC-Pfaden (`\\...`) sowie
`INTERNAL:`, `CONFIDENTIAL:`, `VERTRAULICH:`.

- Keine echten Treffer für hartcodierte persönliche/interne Pfade oder Markerkommentare.
- `docs/help/plugins/bitbucket-plugin/troubleshooting.md`: `C:\Users\{username}\_netrc` ist ein
  generischer Platzhalter, kein echter Nutzername.
- `docs/help/plugins/bitbucket-plugin/*.md`, `docs/help/programmupdate/index.md`: enthalten
  `martin-stromberg` (bekannter, öffentlicher Maintainer-Name laut Designentscheidung) sowie
  offensichtliche Platzhalter-E-Mails (`martin@example.com`, `developer@company.com`) und einen
  Platzhalter-Servernamen (`bitbucket.company.com`) — unproblematisch.

## 4. PII in Testdaten

`src/Softwareschmiede.Tests/` wurde auf private E-Mail-Domains (`@gmail.com`, `@web.de`, `@gmx.*`,
`@yahoo.*` etc.) und Telefonnummern-Muster durchsucht. Keine Treffer. Vorhandene Testdaten nutzen
durchgehend offensichtlich synthetische Werte (`test@example.com`-artige Muster).

## 5. `docs/token-usage-log.csv`, `docs/CI_CD.md`, `docs/help/`

- `docs/token-usage-log.csv` (183 Zeilen): enthält ausschließlich Timestamps, Branch-Namen,
  Agent-Typen/-IDs und Token-Zahlen — reine Prozessmetriken, keine Secrets/PII. Branch-Namen
  enthalten interne Issue-Beschreibungen, aber keine Personendaten über den bekannten
  Projektkontext hinaus. Deckungsgleich mit der Designentscheidung „Umgang mit
  `docs/token-usage-log.csv`“ in `plan.md` (repo-externe Persistenz statt Historienbereinigung).
- `docs/CI_CD.md`: unauffällig, referenziert nur `secrets.GITHUB_TOKEN` (Standard) und öffentliche
  Action-Namen.
- `docs/help/`: unauffällig bis auf die oben genannten, unbedenklichen Platzhalter/Maintainer-Nennungen.

## Fazit und Konsequenz für die weiteren Schritte

Es wurden keine echten Secrets gefunden, die eine Bereinigung der Git-Historie erfordern würden
(Designentscheidung „Git-Historie: Keine Bereinigung, kein Force-Push“ bleibt damit unverändert
gültig). Es mussten keine Dateien im Arbeitsbaum bereinigt oder maskiert werden. Die Lizenzierung
(Schritt 2 ff.) kann ohne Vorbehalt fortgesetzt werden.

## Abschluss-Verifikation (Schritt 11)

- **Build:** `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug`
  (zieht alle Projektreferenzen inkl. `Softwareschmiede.App` und aller sieben Plugin-Projekte
  transitiv mit) — **0 Fehler** nach der FlaUI-`PrivateAssets`-Änderung. Die zunächst analog zu
  `coverlet.collector`/`xunit.runner.visualstudio` übernommene `IncludeAssets`-Liste (ohne
  `compile`) brach die Kompilierung von `WpfTestBase.cs`/E2E-Testdateien, da FlaUI dort direkt per
  `using FlaUI...` referenziert wird; behoben durch Ergänzung von `compile` in `IncludeAssets`
  für `FlaUI.Core`/`FlaUI.UIA3`.
- **Publish-Verifikation:** `dotnet publish src/Softwareschmiede.App/Softwareschmiede.App.csproj -c
  Release` in ein Scratch-Verzeichnis — **keine `FlaUI.*.dll`** im Publish-Output. Die
  `PrivateAssets=all`-Einstellung verhindert die transitive Weitergabe wie vorgesehen.
- **Testlauf (CI-äquivalent):** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test
  src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter
  "Category!=E2E&Category!=ConPTY"` — **942/942 Tests bestanden**, keine durch die
  FlaUI-Änderung verursachten Regressionen.
- **E2E-Suite (`Category=E2E`):** In diesem Sandbox-/Agenten-Kontext **nicht** ausgeführt. Die
  laufende Session ist laut `CLAUDE.md` („Self-Hosting-Risiko“) potenziell selbst innerhalb einer
  laufenden `Softwareschmiede.App.exe`-Instanz gehostet; FlaUI-E2E-Tests starten/steuern
  WPF-Fensterinstanzen und benötigen eine verlässliche interaktive Desktop-Session, die in diesem
  Kontext nicht sicher garantiert werden kann. Da die FlaUI-Änderung ausschließlich
  `PrivateAssets`/`IncludeAssets` betrifft (keine Verhaltensänderung des Testcodes) und die
  Kompilierung sowie alle Nicht-E2E-Tests bereits grün sind, ist das Restrisiko gering. Der
  vollständige lokale E2E-Lauf (`dotnet test ... --filter "Category=E2E"`) verbleibt als
  **menschliche Nachfolgeaufgabe** vor dem tatsächlichen Public-Umschalten des Repositories.
- **Token-Usage-Hook:** Bestehender Inhalt von `docs/token-usage-log.csv` nach
  `~/.softwareschmiede/token-usage-log.csv` migriert, `LOG_FILE`-Konstante sowie
  `commit_log_file()`-Aufruf in `.claude/hooks/log_token_usage.py` entsprechend umgestellt;
  `docs/token-usage-log.csv` per `git rm --cached` entfernt und in `.gitignore` aufgenommen.

## Menschliche Nachfolgeaufgaben (Repository-Settings)

Folgende Punkte sind reine GitHub-Repository-Einstellungen und können nicht durch einen
Code-/Dokumentations-Commit umgesetzt werden:

1. **Repository auf „Public“ umschalten** (`Settings` → `General` → `Danger Zone` → `Change
   visibility`).
2. **Branch-Protection für `main`** aktivieren (mindestens 1 Approval-Pflicht, Status-Checks
   `test` und `security-scan` als required Checks).
3. **GitHub Private Security Advisories aktivieren** (`Settings` → `Security` → `Enable private
   vulnerability reporting`), damit der in `SECURITY.md` beschriebene Meldeweg tatsächlich
   verfügbar ist.
4. **Vollständigen lokalen E2E-Testlauf** (`Category=E2E`) auf einer Maschine mit garantiert
   interaktiver Desktop-Session durchführen (siehe Abschnitt „Abschluss-Verifikation" oben).
