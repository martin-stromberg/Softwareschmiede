# Ablauf – `start.ps1` für Visual-Studio-Debug mit freiem HTTP-Port

## Kontext

Dieser Ablauf beschreibt den lokalen Skriptpfad, mit dem `start.ps1` Webprojekte autonom erkennt und deren `http`-Debugprofil für Visual Studio auf freie Ports setzt.

## Diagramm A – Sequenz: Aufruf bis Debug-Start

```mermaid
sequenceDiagram
    actor Dev as Entwickler
    participant PS as start.ps1
    participant FS as Dateisystem
    participant LS as launchSettings.json
    participant VS as Visual Studio

    Dev->>PS: .\start.ps1
    PS->>FS: Relevante launchSettings.json finden
    FS-->>PS: Zielprojekte
    loop je Zielprojekt
        PS->>PS: Freien HTTP-Port ermitteln
        PS->>LS: profiles.http.applicationUrl aktualisieren
        PS->>PS: Ergebnis protokollieren
    end
    PS-->>Dev: Exit-Code + Diagnose
    Dev->>VS: F5 starten
    VS->>LS: Lese aktualisierte applicationUrl
```

## Diagramm B – Entscheidungslogik inkl. Exit-Codes

```mermaid
flowchart TD
    A([start.ps1 gestartet]) --> B{Relevante Projekte gefunden?}
    B -- Nein --> X10[Exit 10]
    B -- Ja --> C{launchSettings / profiles.http gültig?}
    C -- Nein --> X11[Exit 11]
    C -- Ja --> D{Freier Port ermittelbar?}
    D -- Nein --> X12[Exit 12]
    D -- Ja --> E{Write erfolgreich?}
    E -- Nein --> X13[Exit 13]
    E -- Ja --> OK([Exit 0])
```

## Schrittbeschreibung

1. **Eingang auswerten**
   - Keine fachlichen Parameter
   - Repository-Root aus Skriptpfad ableiten
2. **Portquelle festlegen**
   - Port wird je Zielprojekt intern ermittelt
3. **Port prüfen**
   - Bereich `1..65535`
   - Verfügbarkeit per Loopback-Listener
4. **Zieldatei aktualisieren**
   - alle relevanten `**/Properties/launchSettings.json`
   - Nur `profiles.http.applicationUrl` wird verändert
5. **Diagnose und Rückgabe**
   - Einheitliches Diagnoseformat mit Code
   - Exit-Code gemäß Fehlerklasse, aggregiert über mehrere Projekte

## Verknüpfte Dokumentation

- [API-Contract: start.ps1 für Visual-Studio-Debug](../api/start-ps1-visual-studio-freier-http-port.md)
- [API-Contract: Repository-Startskript mit freier Portzuweisung](../api/repository-startskript-freier-port.md)
- [Feature F020 – Repository-Startskript mit freier Portzuweisung](../business/features/F020-repository-startskript-freier-port.md)
