# Enums

## `UpdatePreparationPhase`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 125-135)

| Wert | Bedeutung |
|------|-----------|
| `Download` | Das Release-Asset wird heruntergeladen — die Phase, die ein Fallback betrifft |
| `Entpacken` | Das Release-ZIP wird entpackt |
| `UpdateVorbereiten` | Das externe Update-Skript wird vorbereitet |

## `UpdateCheckStatus`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 96-106)

| Wert | Bedeutung |
|------|-----------|
| `KeinUpdate` | Kein neueres Update verfügbar |
| `UpdateVerfuegbar` | Ein neueres Update ist verfügbar |
| `NichtPruefbar` | Die Update-Prüfung konnte nicht zuverlässig durchgeführt werden |

## `UpdateMode`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 30-40)

| Wert | Bedeutung |
|------|-----------|
| `Aus = 0` | Update-Prüfung und -Installation deaktiviert |
| `NurPruefen = 1` | Nur prüfen; Fund wird angeboten, nie automatisch installiert |
| `BeiProgrammstartPruefenUndAusfuehren = 2` | Beim Start prüfen und Fund automatisch installieren |

## `UpdateKanal`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 43-50)

| Wert | Bedeutung |
|------|-----------|
| `Produktiv = 0` | Nur stabile Produktivversionen sind Kandidaten |
| `ReleaseCandidate = 1` | Nur Prereleases (RCs) sind Kandidaten |
