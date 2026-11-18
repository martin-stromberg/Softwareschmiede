# Usability-Review – Schritt 6: Issue-Aufbereitung (Einstellungen-UI)

Geprüft: Expander „Issue-Aufbereitung" im Einstellungen-Tab „Automatisierung"
plus Benutzerführung der Fehler-/Wartezustände (real via FlaUI-E2E abgenommen).

## Positiv

- **Ein Ort, vier Schalter:** Funktion, Funktions-Plugin, Repository-Auswahl
  und Kommentar-Dokumentation liegen in einem Expander — konsistent zu
  „Aufgaben ausführen" / „Aufgaben beenden" / „Prozessabläufe".
- **Status im Header** („aktiviert" / „deaktiviert") ist ohne Öffnen sichtbar —
  gleiches Muster wie bei den übrigen Automatisierungs-Expandern.
- **Sperrung statt Verstecken:** Bei deaktivierter Funktion bleiben die Felder
  sichtbar, aber gesperrt — die Konfiguration ist lesbar, bevor man aktiviert.
- **Verständliche Optionen:** „Globale Standard-CLI verwenden" benennt die
  Fallback-Semantik, ohne Interna (Prefix, Kette) zu verlangen; Hilfetexte
  erklären die Auflösungskette und die Reichweite der Repository-Markierung
  (nur von der Ereignisüberwachung erfasste, registrierte Repositories).
- **Keine internen Kennungen:** Repository-Auswahl per Anzeigename;
  UIA-Namen sprechend (`AutomationProperties.Name` = Repository-Name).
- **Ehrliche Wartegründe** am Ereignis (Funktion deaktiviert, Plugin nicht
  verfügbar + ignorierte Prefixes mit Namen, Limit erreicht, geschlossenes
  Issue) — jeder Zustand hat einen benennbaren Grund, keine stillen Hänger.
- **Keine Remote-Überraschung:** Bei Fehlern oder deaktiviertem Kommentar-
  Schalter erscheint nichts am Issue; die Dokumentation landet ausschließlich
  im Prozessprotokoll des Vorgangs.

## Beobachtungen / bewusste Grenzen

- Die Aufbereitung ist opt-in und zusätzlich pro Repository freizuschalten —
  doppelte Hürde ist gewollt (Schutz vor ungewollten Remote-Änderungen), wird
  im Hilfetext erklärt.
- Der Kommentar-Schalter wirkt hostseitig auch dann, wenn eine CLI trotzdem
  eine Kommentaraktion liefert — sie wird verworfen und protokolliert; für
  Benutzer nachvollziehbar.
- Wartegründe nennen teils den Lösungsweg („Plugin in den Einstellungen
  aktivieren oder Ereignis öffnen und CLI auswählen") — konsistent zu den
  übrigen SCM-Wartehinweisen.

## Ergebnis

**Freigegeben.** Die Oberfläche folgt den etablierten Mustern, erklärt
Reichweite und Voraussetzungen und bleibt in allen Zuständen ehrlich.
