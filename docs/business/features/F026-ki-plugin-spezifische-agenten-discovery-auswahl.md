# F026 – KI-Plugin-spezifische Agenten-Discovery und -Auswahl

## Einleitung

Mit dieser Funktion wählen Sie Agenten gezielt passend zum gewählten KI-Plugin aus.
Die Anwendung erzwingt dabei die Reihenfolge:

1. **KI-Plugin wählen**
2. **kompatibles Agentenpaket wählen**
3. **Agent wählen**

So vermeiden Sie inkonsistente Kombinationen und fehlgeschlagene Läufe.

---

## Nutzen für Anwender:innen

- Nur passende Pakete/Agenten werden angezeigt.
- Start und Folgeanweisungen nutzen dieselbe Plugin-Logik.
- Ihre Auswahl bleibt pro Aufgabe gespeichert und wird beim Wiederöffnen berücksichtigt.
- Bei fehlender Kompatibilität sehen Sie klare Hinweise statt unklarer Fehlverhalten.

---

## Schritt-für-Schritt

1. Aufgabe öffnen.
2. KI-Plugin auswählen.
3. Ein kompatibles Agentenpaket wählen.
4. Einen Agenten aus dem Paket wählen.
5. Entwicklungsprozess starten oder Folgeanweisung senden.

Wichtig:
- Bei **Pluginwechsel** werden Paket und Agent zurückgesetzt.
- Bei **Paketwechsel** wird der Agent zurückgesetzt.

---

## Was passiert im Hintergrund?

- Das System ermittelt kompatible Pakete und Agenten plugin-spezifisch.
- Das gewählte KI-Plugin wird als `KiPluginPrefix` in der Aufgabe gespeichert.
- Wenn keine direkte Auswahl vorliegt, wird über die Kette
  **explizit → Aufgabe → Standardplugin → Fallback**
  aufgelöst.
- Start- und Folgeprompt verwenden dieselbe Auflösung.

---

## Akzeptanzkriterien

1. Die Reihenfolge **KI-Plugin → Agentenpaket → Agent** ist in der UI verbindlich.
2. Es werden nur kompatible Pakete/Agenten angezeigt.
3. Ohne gültige Auswahl sind Start/Senden deaktiviert und ein verständlicher Hinweis wird angezeigt.
4. `KiPluginPrefix` wird pro Aufgabe gespeichert und wiederverwendet.
5. Start- und Folgeprompt nutzen dieselbe Plugin-Auflösung.

---

## Verwandte Funktionen

- [F004 – Agentenpakete](./F004-agentenpakete.md)
- [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md)
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md)
- [Zurück zur Übersicht](../features.md)
