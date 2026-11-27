# Release-Pipeline und Update-Manifest

Die Softwareschmiede-Pipeline erzeugt bereits alle Release-Assets, die msTools.Updater als Quelle nutzt — mit zwei entscheidenden Lücken für den StatusServer.

## Workflows

### `.github/workflows/release.yml` (main-Push + `v*.*.*`-Tags)

- `concurrency: release` (Serialisierung, kein Cancel), Windows-Runner, .NET 11.
- „Resolve release version" via `scripts/resolve-release-version.mjs`: Klassifizierung `release_kind` (`automatic` = semantic-release auf main, `manual` = manueller Tag-Push) und `release_action` (`create` vs. `upload-existing` Asset-Reparatur); Outputs `version`/`tag`.
- Release-Gates: `dotnet test` auf `Softwareschmiede.Tests`, `Softwareschmiede.IntegrationTests`, **`Softwareschmiede.StatusServer.Tests`** (Z. 111–114) — Server-Tests sind bereits Teil des Gates.
- `build-and-package`-Action mit `release-version`/`release-tag`.
- Asset-Upload (drei Pfade):
  - automatic: `npm run release` (semantic-release/@semantic-release/github) mit `RELEASE_ASSET_PATH=release.zip`, `RELEASE_MANIFEST_PATH=update.json`, `RELEASE_EXTRA_ASSET_PATHS=statusserver-win-x64.zip,statusserver-linux-x64.zip` (Z. 157–163)
  - manual: `gh release create $tag release.zip update.json statusserver-win-x64.zip statusserver-linux-x64.zip --generate-notes` (Z. 176–178)
  - repair: `gh release upload $tag release.zip update.json statusserver-win-x64.zip statusserver-linux-x64.zip --clobber` (Z. 187)

### `.github/workflows/staging-ci.yml` (staging-Push → Pre-Release)

- `version`-Job: semantic-release `--dry-run --branches staging` → nächste Version, RC-Nummer via `git tag --list "v$version-rc.*"`, Outputs `rc_tag` (`vX.Y.Z-rc.N`) und `rc_version` (`X.Y.Z-rc.N`).
- `prerelease`-Job (Z. 330–355): dieselbe `build-and-package`-Action, dann `gh release create $rc_tag release.zip update.json statusserver-win-x64.zip statusserver-linux-x64.zip --prerelease --generate-notes`. **Pre-Releases existieren also bereits als GitHub-Releases mit `--prerelease`-Flag** — genau das, was `AllowPrereleaseUpdates` auf msTools-Seite selektiert.

## `build-and-package`-Action (`.github/actions/build-and-package/action.yml`)

| Schritt | Ergebnis |
|---------|----------|
| Publish Desktop | `dotnet publish src/Softwareschmiede.App -r win-x64 --self-contained` → `publish/`; Exe umbenannt zu `Softwareschmiede.exe` |
| „Write version manifest" (Z. 57–78) | **`publish/version.json`** mit `{version, tagName, commit, createdAtUtc}` — nur im Desktop-Publish, Schema ≠ `release-metadata.json` |
| Publish StatusServer (Z. 94–105) | `dotnet publish server/Softwareschmiede.StatusServer -r win-x64` → `publish-statusserver-win` und `-r linux-x64` → `publish-statusserver-linux`, self-contained; Kommentar erklärt explizit, warum separate ZIPs statt Einbuchung in `release.zip` (Desktop-Updater fixiert auf den Namen) |
| „Create StatusServer ZIPs" (Z. 107–122) | `statusserver-win-x64.zip`, `statusserver-linux-x64.zip`; Verify-Schritt prüft nur Exe-Präsenz |
| „Create Release ZIP" | `release.zip` aus `publish/` |
| „Create update manifest" (Z. 128–171) | `update.json` (Rezepte-Format), **enthält genau ein Asset**: `release.zip` (`platform: windows`, `runtimeIdentifier: win-x64`, `assetUrl: https://github.com/{repo}/releases/download/{tag}/release.zip`, sha256 + sizeBytes) |

## Release-Assets heute

`release.zip` · `update.json` · `statusserver-win-x64.zip` · `statusserver-linux-x64.zip` — für stabile Releases und RC-Pre-Releases identisch.

## Gaps für Issue #488

1. **StatusServer-Pakete enthalten kein `release-metadata.json`** (und auch kein `version.json`): Der Default-`IInstalledVersionProvider` von msTools.Updater liest `release-metadata.json` aus dem App-Root (`version`, `publishedAt`, `commitSha`, `repository`, `runtimeIdentifier`) — ohne die Datei ist die installierte Version unbekannt und jede Versionsprüfung wirkungslos. FinanceManager schreibt sie im Publish-Schritt je RID (Vorlage: `FinanceManager/.github/actions/build-and-package/action.yml` Z. 58–65). Ergänzung nötig für `publish-statusserver-win` und `publish-statusserver-linux` (inkl. `runtimeIdentifier` je RID), idealerweise mit Verify-Zusatz.
2. **`update.json` enthält nur das Desktop-Asset** (`release.zip`, `windows`/`win-x64`): msTools' `SelectPackage` wählt ausschließlich über `platform` + `runtimeIdentifier` — böte man `statusserver-win-x64.zip` zusätzlich im selben Manifest an, kollidierte es mit `release.zip` um dieselbe Selektion. Konsequenz: **separates Server-Manifest** (z. B. `statusserver-update.json` mit den beiden Server-Assets), per `UseGithubSource(owner, name, "statusserver-update.json")` adressierbar. Alternativ-Optionen: Desktop-Updater auf ein eigenes Manifest umstellen (bricht `AssetName`/`ManifestAssetName`-Fixierung) oder Desktop-Release aus dem Server-Manifest herauszuhalten — beide berühren den funktionierenden Desktop-Updater nicht notwendigerweise. Die drei Upload-Stellen in `release.yml` und die `gh release create`-Zeile in `staging-ci.yml` müssten das neue Asset dann jeweils mit hochladen (`gh release upload` + semantic-release `RELEASE_EXTRA_ASSET_PATHS`-Liste/Create-Kommando).
3. **`releaseNotes` ist generisch** („Softwareschmiede release {tag}") — für ein separates Server-Manifest vermutlich akzeptabel, aber beim eigenen Generierungsschritt frei gestaltbar (FinanceManager hasht schreibt das Manifest per `scripts/generate-update-manifest.mjs`; Softwareschmiede generiert inline in PowerShell, es gibt **kein** entsprechendes Node-Skript unter `scripts/` — dort liegt nur `resolve-release-version.mjs` + `lib/`).
4. **RC-Versionsfeld:** `rc_version` (`X.Y.Z-rc.N`) fließt bereits als `version` ins Manifest und als SemVer-Prerelease ist sie mit `AllowPrereleaseUpdates` korrekt als Vorabversion klassifizierbar.

## Lokale Paketquelle

Das `msTools.Updater`-nupkg muss ins Repo (FinanceManager-Muster: `NuGet.config`-Feed `external/msTools.Updater/` + eingechecktes `.nupkg`, Version 0.11.0-rc.8 aus dem NuGet-Cache `C:\Users\Martin\.nuget\packages\mstools.updater\0.11.0-rc.8\`). Softwareschmiede hat derzeit weder `NuGet.config` noch `external/`. Zusätzlich betrifft das die CI: `dotnet restore` in Release/Staging-CI muss die Quelle sehen — mit eingechecktem `NuGet.config` im Repo-Root ist das automatisch abgedeckt.
