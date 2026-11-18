# Umsetzungsplan – Schritt 1: TFM .NET 11 + CI-Toolchain

## Scope-Klärung

- Verarbeitungseinheit: alle 13 csproj + 5 CI-Dateien — eine atomare TFM-Umstellung.
- Auslöser: Projektschritt 1 (Issue #364, Projektplan).
- Wirkung: Solution baut/testet unter .NET 11 SDK; CI provisioniert .NET 11.
- Scope-Ausschlüsse: Keine (Paketversionen bleiben bewusst unverändert — gehört zu Schritt 2).

## Änderungen

1. **TFM in 13 csproj:**
   - `net10.0` → `net11.0` in: `src/Softwareschmiede`, `src/Softwareschmiede.Plugin.Contracts`,
     `src/Softwareschmiede.IntegrationTests`, 8× `plugins/*`.
   - `net10.0-windows10.0.17763.0` → `net11.0-windows10.0.17763.0` in:
     `src/Softwareschmiede.App`, `src/Softwareschmiede.Tests`.
   - Ersetzung ausschließlich innerhalb `<TargetFramework>…</TargetFramework>`.
2. **App.csproj `CopyPluginsToOutput`:** 8× `bin\$(Configuration)\net10.0\` → `net11.0\`
   (nur die Plugin-Artefakt-Glob-Pfade; `TargetPlatformMinVersion` unverändert lassen).
3. **CI:** in `.github/workflows/pr-staging-ci.yml`, `staging-ci.yml`, `release.yml`,
   `security-scan.yml`, `.github/actions/build-and-package/action.yml`:
   - `dotnet-version: '10.0.x'` → `'11.0.x'` (9×)
   - `name: Setup .NET 10` → `Setup .NET 11` (9×)
   - Kommentare `net10.0-windows` → `net11.0-windows` (release.yml Z.21,
     build-and-package Z.3/Z.33; jeweils nur den TFM-Teil).
4. **Prüfschritt:** repo-weites `net10`-Grep auf Resttreffer; nur erlaubte Reste
   (`.github/upgrades/` Altartefakt, Paketversionen, `TargetPlatformMinVersion`,
   Migrations-/Kommentar-Kontext ohne TFM-Bezug).

## Verifikation (Akzeptanzszenarien)

- **Szenario A (Happy Path):** Mit `DOTNET_ROOT`+PATH auf `C:\Users\Martin\.dotnet11`
  `dotnet build Softwareschmiede.slnx` → erfolgreich, 0 Fehler; im Ausgabeverzeichnis
  liegen `net11.0`-Ordner; `bin\...\plugins\*` enthalten die kopierten Plugin-DLLs.
- **Szenario B:** `dotnet test` beider Testprojekte reguläre Lane
  (`--filter "Category!=OsInterface"`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) → bestanden.
  (Build vorab ohne `--no-build`.)
- **Szenario C:** `dotnet format Softwareschmiede.slnx --verify-no-changes` mit
  .NET-11-dotnet → ohne Befund.
- **Szenario D (Reststellen):** `grep -rn "net10" src plugins .github` zeigt nur erlaubte
  Resttreffer (Paket-Versionszahlen, `TargetPlatformMinVersion`, `.github/upgrades/`).
- **Reale End-to-End-Abnahme:** Die Anwendung wird in Schritt 1 nicht fachlich verändert;
  reale Abnahme = erfolgreicher App-Build inkl. Plugin-Copy (Szenario A) plus E2E-Test-
  Durchlauf soweit in dieser Sandbox möglich (nicht-interaktive Session → E2E-Timeout ist
  dokumentierte Umweltbeschränkung, kein Befund).

## Offene Punkte

Keine.
