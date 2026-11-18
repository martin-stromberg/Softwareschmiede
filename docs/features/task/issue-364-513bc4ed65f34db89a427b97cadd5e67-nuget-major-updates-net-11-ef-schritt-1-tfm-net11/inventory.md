# Bestandsaufnahme – Schritt 1 (TFM .NET 11 + CI-Toolchain)

## Betroffene Dateien

**csproj (13):**

- `net10.0`: `src/Softwareschmiede`, `src/Softwareschmiede.Plugin.Contracts`,
  `src/Softwareschmiede.IntegrationTests`, `plugins/*` (8)
- `net10.0-windows10.0.17763.0`: `src/Softwareschmiede.App`, `src/Softwareschmiede.Tests`

**Hartcodierte net10-Pfade:**

- `src/Softwareschmiede.App/Softwareschmiede.App.csproj` — `CopyPluginsToOutput`,
  8× `plugins\<Name>\bin\$(Configuration)\net10.0\*.dll` (BitBucket, LocalDirectory,
  GitHubCopilot, ClaudeCli, Codex, Devin, KiSimulator, GitHub).

**CI-Dateien mit `dotnet-version: '10.0.x'` (9) + Schrittnamen `Setup .NET 10` (9):**

- `.github/workflows/pr-staging-ci.yml` (3× / 3×)
- `.github/workflows/staging-ci.yml` (3× / 3×)
- `.github/workflows/release.yml` (1× / 1×) + Kommentar `net10.0-windows` (Z.21)
- `.github/workflows/security-scan.yml` (1× / 1×)
- `.github/actions/build-and-package/action.yml` (1× / 1×) + Beschreibungs-/Kommentar-
  Erwähnungen `net10.0-windows` (Z.3, Z.33)

**Nicht betroffen:** `scripts/Run-AllTestsIndividually.ps1` (kein net10-Bezug),
`TargetPlatformMinVersion`, `app.manifest`, `publish.ps1` (prüfen), `Directory.Build.*`
(existiert nicht), `global.json` (existiert nicht — kein SDK-Pinning im Repo).

## Build-Umgebung

- Lokal: .NET 11 SDK 11.0.100 unter `C:\Users\Martin\.dotnet11` (Aufruf per
  `DOTNET_ROOT=C:\Users\Martin\.dotnet11` + PATH-Präfix oder Vollqualifizierung;
  PATH-dotnet bleibt 10.0.403).
- CI: `actions/setup-dotnet@v6` mit `dotnet-version` — `11.0.x` löst 11.0.100+ auf.
- WPF/Windows-TFM erfordert Windows-Build-Host — gegeben (Sandbox + CI windows-latest).

## Risiken / Beobachtungen

- `net11.0-windows10.0.17763.0` ist valide (Windows-TFM-Suffix bleibt OS-Version,
  Framework-Teil wird 11.0).
- Pakete 10.0.15 (Microsoft.*) liefern net10-Assets → kompilieren/laufen auf net11;
  NU170x-Warnungen nicht zu erwarten (Abwärtskompatibilität gegeben).
- WPF-E2E-Tests starten reale App-Fenster — Sandbox-Lauf ohne interaktive Session kann
  Timeout liefern; dann App-Log prüfen (CLAUDE.md-Regel). ConPTY-Tests via
  `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` überspringen.
- `dotnet format` der SDK-10-Installation kann net11-Projekte ggf. nicht laden →
  Format-Check mit dem .NET-11-dotnet ausführen.
