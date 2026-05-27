# API-Contract – Startvalidierung beim Aufgabenstart (UI/Service)

## Überblick

Dieser Contract beschreibt die Startvalidierung in `AufgabeDetail` für Prozessstart und Folgeanweisungen.

- **KI-Plugin ist Pflicht**
- **Agentenpaket ist optional**
- **Agent ist optional**

Die Validierung wird über bestehende UI-/Service-Logik umgesetzt (kein eigener öffentlicher REST-Endpunkt).

## Geltungsbereich

- UI: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor(.cs)`
- Services: `PluginSelectionService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `AufgabeService`
- Domäne: `Aufgabe.KiPluginPrefix`, `Aufgabe.AgentenpaketName`, `Aufgabe.AgentenName`

## Pflicht-/Optional-Regeln

| Feld | Status | Wirkung |
|---|---|---|
| `KI-Plugin` | *(required)* | Ohne verfügbares/auflösbares KI-Plugin bleiben Start/Senden deaktiviert. |
| `Agentenpaket` | optional | Leerer Wert ist zulässig; es erfolgt kein Paket-Deploy. |
| `Agent` | optional | Leerer Wert ist zulässig; der Lauf startet ohne expliziten Agentenparameter. |

## Validierungslogik (technischer Ablauf)

1. UI lädt verfügbare KI-Plugins.
2. Effektives KI-Plugin wird aufgelöst (`explizit -> Aufgabe -> Default -> Fallback`).
3. Kompatible Agentenpakete/Agenten werden plugin-spezifisch geladen.
4. Start/Senden wird nur blockiert, wenn kein gültiges KI-Plugin verfügbar ist.
5. Paket-/Agent-Auswahl wird bei Bedarf gespeichert, darf aber `null` bleiben.

## Zustands- und Fehlermeldungen

- **Blockierend**
  - „Kein KI-Plugin verfügbar.“
- **Nicht blockierend (Hinweise)**
  - „Kein Agentenpaket gewählt – die KI verwendet ihre Standardeinstellungen.“
  - „Für das gewählte Agentenpaket sind keine kompatiblen Agenten verfügbar. Die KI verwendet ihre Standardeinstellungen.“
  - „Kein Agent gewählt – die KI verwendet ihre Standardeinstellungen.“

## Nicht-Ziele / API-Abgrenzung

- Kein neuer öffentlicher Controller-Endpunkt.
- Kein zusätzlicher HTTP-Contract außerhalb bestehender UI- und Servicepfade.

## Verknüpfte Dokumentation

- [KI-Plugin-spezifische Agenten-Discovery/Auswahl](./ki-plugin-spezifische-agenten-discovery-auswahl.md)
- [HTTP-Endpunkte](./http-endpoints.md)
- [Flow: KI-Plugin-spezifische Agenten-Discovery/Auswahl](../flows/ki-plugin-spezifische-agenten-discovery-auswahl-flow.md)
- [Business: F028 – Startvalidierung beim Aufgabenstart](../business/features/F028-startvalidierung-aufgabenstart.md)
