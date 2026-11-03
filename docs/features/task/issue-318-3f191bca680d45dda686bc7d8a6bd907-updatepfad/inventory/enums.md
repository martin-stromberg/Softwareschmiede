# Enums

## `UpdateMode`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 30–40)

| Wert | Bedeutung |
|------|-----------|
| `Aus = 0` | Update-Prüfung und -Installation deaktiviert |
| `NurPruefen = 1` | Nur prüfen; Update wird angeboten, nie automatisch installiert (Default bei fehlendem/ungültigem Speicherwert) |
| `BeiProgrammstartPruefenUndAusfuehren = 2` | Beim Programmstart prüfen und bei Befund automatisch installieren |

Persistiert als Zahlenwert unter `updates.mode`; `Enum.IsDefined`-Validierung in `SetUpdateSettingsAsync`.

## `UpdateCheckStatus`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 82–92)

| Wert | Bedeutung |
|------|-----------|
| `KeinUpdate` | Kein neueres Update verfügbar |
| `UpdateVerfuegbar` | Neueres Update verfügbar |
| `NichtPruefbar` | Prüfung nicht zuverlässig möglich (u. a. Defensivprüfung bei prerelease-fremdem Ergebnis, Lesefehler, HTTP-Fehler) |

## `UpdatePreparationPhase`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 111–121)

| Wert | Bedeutung |
|------|-----------|
| `Download` | Release-Asset wird heruntergeladen |
| `Entpacken` | Release-ZIP wird entpackt |
| `UpdateVorbereiten` | Externes Update-Skript wird vorbereitet |

**Hinweis:** Ein Kanal-Enum (z. B. `UpdateKanal` mit `Produktiv`/`ReleaseCandidate`) existiert nicht; die Kanal-/Prerelease-Unterscheidung läuft heute ausschließlich über `bool IncludePrereleases` und `bool IsPrerelease`.
