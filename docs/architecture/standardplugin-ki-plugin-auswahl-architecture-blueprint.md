# Architektur-Blueprint – Standardplugin je Pluginart & KI-Plugin-Auswahl

## Zielarchitektur
Die Lösung trennt:
1. **Konfiguration von Defaults** (Einstellungen + Persistenz),
2. **Auflösung von Plugin-Auswahl** (explizit gewählt -> Default -> Fallback),
3. **Ausführung** (Prompt wird exakt an die aufgelöste KI-Plugin-Instanz übergeben).

## Komponenten
- **PluginManager (bestehend):** Liefert verfügbare Plugins je Pluginart.
- **PluginDefaultSettingsService (neu):** Lesen/Schreiben von Defaults je Pluginart.
- **PluginSelectionService (neu):** Auflösen der effektiven Plugininstanz.
- **Einstellungen-UI (erweitert):** Auswahl und Speichern der Standardplugins.
- **Prompt-UI/AufgabeDetail (erweitert):** KI-Plugin-Auswahl beim Senden.
- **Ausführungsservices (erweitert):** Transportieren `selectedKiPluginId` bis zum tatsächlichen Pluginaufruf.

## Datenfluss (Prompt)
1. UI lädt verfügbare KI-Plugins + gespeichertes Standardplugin.
2. Nutzer lässt Vorauswahl bestehen oder ändert sie.
3. UI sendet Prompt inkl. `selectedKiPluginId`.
4. PluginSelectionService löst auf:
   - explizit gewähltes Plugin, sonst
   - gespeichertes Default, sonst
   - deterministischer Fallback.
5. Prompt wird an die aufgelöste KI-Plugin-Instanz übergeben.
6. Protokoll enthält verwendete Plugin-ID/-Name.

## Persistenzentscheidung
- Nutzung bestehender App-Einstellungen mit Schlüsselkonvention:
  - `plugins.default.SourceCodeManagement`
  - `plugins.default.DevelopmentAutomation`
- Wert ist **stabile technische Plugin-ID** (nicht Anzeigename).

## Fehlertoleranz
- Gespeicherte, aber nicht mehr verfügbare Plugin-ID -> Warnung + Fallback.
- Kein verfügbares KI-Plugin -> verständlicher Fehler im UI, kein stilles Scheitern.

## Qualitätsziele
- **Korrektheit:** Gewähltes Plugin wird tatsächlich verwendet.
- **Wartbarkeit:** Auswahl-/Fallback-Logik zentral in einem Service.
- **Rückwärtskompatibilität:** Ohne gesetzte Defaults bleibt bestehendes Verhalten erhalten.

## Umsetzungsreihenfolge
1. Settings-/Selection-Services implementieren.
2. Signaturen in Ausführungsservices um `selectedKiPluginId` erweitern.
3. Einstellungen-UI erweitern.
4. Prompt-UI erweitern.
5. Logging/Protokoll erweitern.
6. Unit-/Integrationstests ergänzen.
