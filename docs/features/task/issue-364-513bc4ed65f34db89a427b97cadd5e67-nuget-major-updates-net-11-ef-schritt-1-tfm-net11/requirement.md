# Anforderung – Schritt 1: TFM-Umstellung aller Projekte auf .NET 11 inkl. CI-Toolchain

(Projektschritt 1 aus `docs/projects/task/issue-364-.../project-plan.md`, Issue #364)

## Ziel

Alle Projekte der Lösung zielen auf .NET 11; die CI-/Release-Toolchain provisioniert das
.NET-11-SDK. Paketversionen bleiben in diesem Schritt unverändert (10.0.x läuft auf net11).

## Anforderungen

- Alle `*.csproj` (13 Dateien): `net10.0` → `net11.0`,
  `net10.0-windows10.0.17763.0` → `net11.0-windows10.0.17763.0`.
- `Softwareschmiede.App.csproj`: die 8 hartcodierten Plugin-Artefakt-Pfade
  `bin\$(Configuration)\net10.0\*.dll` in `CopyPluginsToOutput` → `net11.0`.
- `TargetPlatformMinVersion` (Windows-Version 10.0.17763.0) bleibt unverändert.
- Alle 9 `dotnet-version: '10.0.x'` in `.github/workflows/{pr-staging-ci,staging-ci,release,
  security-scan}.yml` und `.github/actions/build-and-package/action.yml` → `'11.0.x'`;
  dazugehörige Schrittnamen `Setup .NET 10` → `Setup .NET 11` und `net10.0`-Kommentare
  in `release.yml`/`action.yml` aktualisieren.

## Scope-Klärung

**Scope-Ausschlüsse:** Keine.

## Akzeptanzkriterien / Akzeptanzszenarien

- Happy Path: `dotnet build Softwareschmiede.slnx` mit .NET-11-SDK (11.0.100) fehlerfrei;
  danach `dotnet test` beider Testprojekte reguläre Lane grün
  (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`).
- `grep net10` findet keine Projekt-/CI-Referenzen mehr (erlaubt:
  `TargetPlatformMinVersion`-Wert `10.0.17763.0`, `net10.0` nur noch in Versionsangaben von
  Paketen bzw. unverwandten Inhalten wie `.github/upgrades/`).
- `dotnet format Softwareschmiede.slnx --verify-no-changes` sauber.
- Plugin-Copy: im App-Build landen Plugin-DLLs aus `net11.0`-Ordnern unter
  `bin\...\plugins\` (Nachweis über Vorhandensein der kopierten DLLs).
