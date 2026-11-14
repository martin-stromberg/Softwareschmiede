# Enums

## `UpdatePreparationPhase`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 127–137)

Phasen der Paketvorbereitung; Verbraucher: `UpdatePackageService.PreparePackageAsync` (Report), `UpdateProgressViewModel.Apply` (Anzeigetext), `ProtokollierenderUpdateService` (`PreparationPhase`-Ereignis in E2E).

| Wert | Bedeutung |
|------|-----------|
| `Download` | Das Release-Asset wird heruntergeladen (mit Prozentfortschritt) |
| `Entpacken` | Das Release-ZIP wird entpackt |
| `UpdateVorbereiten` | Das externe Update-Skript wird vorbereitet |

*Kein Wert für eine Verifikationsphase vorhanden.*

## `UpdateMode`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 32–42)

| Wert | Bedeutung |
|------|-----------|
| `Aus = 0` | Update-Prüfung und -Installation deaktiviert |
| `NurPruefen = 1` | Nur prüfen; Update wird angeboten, nie automatisch installiert |
| `BeiProgrammstartPruefenUndAusfuehren = 2` | Beim Programmstart prüfen und gefundenes Update automatisch installieren |

## `UpdateKanal`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 45–52)

| Wert | Bedeutung |
|------|-----------|
| `Produktiv = 0` | Nur stabile Produktivversionen als Update-Kandidaten |
| `ReleaseCandidate = 1` | Nur Prerelease-Versionen (Release-Kandidaten) |

## `UpdateCheckStatus`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 98–108)

| Wert | Bedeutung |
|------|-----------|
| `KeinUpdate` | Kein neueres Update verfügbar |
| `UpdateVerfuegbar` | Ein neueres Update ist verfügbar |
| `NichtPruefbar` | Die Update-Prüfung konnte nicht zuverlässig durchgeführt werden |
