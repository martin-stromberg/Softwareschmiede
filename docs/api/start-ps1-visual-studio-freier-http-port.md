# API-Contract – `start.ps1` für Visual-Studio-Debug mit freiem HTTP-Port

## Zweck

`start.ps1` ist ein repository-internes Startskript zur lokalen Debug-Vorbereitung.
Es erkennt Web-Projekte autonom und setzt pro Zielprojekt einen freien HTTP-Port im `http`-Profil.

## Scope

- Dateipfad: `start.ps1` (Repository-Root)
- Ziel: `**/Properties/launchSettings.json`
- Änderungsbereich: ausschließlich `profiles.http.applicationUrl`
- Kein HTTP-Endpoint-Contract (lokaler Skriptvertrag)

## Aufruf (verbindlich)

```powershell
.\start.ps1
```

Das Skript arbeitet **parameterlos**.  
Portauflösung, Projekt-Erkennung und Datei-Update erfolgen vollständig intern.

## Discovery-Regeln

1. Rekursiver Scan ab Repository-Root nach `**/Properties/launchSettings.json`
2. Ausschlüsse: `.git`, `bin`, `obj`, `TestResults`, `node_modules`
3. Deterministische Reihenfolge: alphabetisch nach absolutem Pfad
4. Isolation je Treffer: Fehler in Projekt A blockiert Projekt B nicht

## Port- und Updateverhalten

- Freier Port je Projekt via Loopback (`TcpListener(..., 0)`)
- Host-Erhalt nur bei VS-kompatiblem Loopback-Host (`localhost`, `127.0.0.1`, `::1`), sonst erzwungener Fallback `localhost`
- Atomisches Schreiben: `launchSettings.json.tmp` + `Move-Item`
- Retry bei Write/Move-Fehlern, inkl. Temp-Cleanup

## Exit-Codes

| Exit-Code | Bedeutung |
|---|---|
| `0` | Alle Ziele erfolgreich verarbeitet |
| `10` | Keine passenden `launchSettings.json` gefunden / Datei nicht lesbar |
| `11` | Ungültige Konfiguration (JSON, `profiles`, `http`) |
| `12` | Port nicht ermittelbar/verfügbar |
| `13` | Schreibfehler bei atomischem Update |
| `99` | Unerwarteter Laufzeitfehler |

Bei Mehrprojektlauf gilt Aggregation: `13 > 12 > 11 > 10 > 0`.

## Diagnostikformat

Konsolenausgaben enthalten korrelierbare Felder:

`[timestamp] [LEVEL] [CODE:<ExitCode>] [RUN:<runId>] [PROJECT:<projectPath>] [FILE:<launchSettingsPath>] [PORT:<port|n/a>] <Nachricht>`

## Verknüpfte Dokumentation

- [Repository-Startskript mit freier Portzuweisung](./repository-startskript-freier-port.md)
- [Flow: start.ps1 für Visual-Studio-Debug](../flows/start-ps1-visual-studio-freier-http-port-flow.md)
- [Feature F020 – Repository-Startskript mit freier Portzuweisung](../business/features/F020-repository-startskript-freier-port.md)
