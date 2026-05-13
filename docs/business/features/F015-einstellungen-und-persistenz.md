# F015 – Einstellungen & Persistenz

## Einleitung

Diese Querschnittsfunktion beschreibt, welche Einstellungen die Softwareschmiede speichert
und wie diese Werte Ihre tägliche Arbeit steuern.
Sie schafft Klarheit, welche Angaben nur einmal gepflegt werden müssen und wann eine Anpassung sinnvoll ist.

---

## Wer nutzt es?

Alle Fachanwender, die die Softwareschmiede regelmäßig einsetzen.
Stakeholder nutzen diese Sicht, um Betriebssicherheit und einheitliche Team-Standards zu gewährleisten.

---

## Welche Einstellungen werden typischerweise gespeichert?

- Arbeitsverzeichnis für lokale Repositories
- Standardplugin je Pluginart (z. B. KI und SCM)
- Plugin-spezifische Betriebsmodi (z. B. `WorkspaceMode` im Local Directory Plugin mit verständlichen UI-Labels)
- Plugin-spezifische Quellpfade (z. B. `SourceDirectory` im Local Directory Plugin)
- Zugangsdaten/Schlüssel für angebundene Dienste (sicher abgelegt)
- Projektbezogene Repository-Angaben je SCM-Plugin (z. B. `RepositoryUrl` und `RepositoryName` beim GitHub-Plugin)
- Weitere nutzerbezogene Vorgaben für den täglichen Ablauf

---

## Was bedeutet Persistenz im Alltag?

- Einmal gespeicherte Einstellungen bleiben zwischen Sitzungen erhalten.
- Neue Aufgaben und Prompts nutzen diese Werte automatisch als Vorgabe.
- Änderungen wirken für zukünftige Abläufe, ohne bestehende Historien zu verändern.

---

## Beispiel

Sie legen einmal ein Standard-KI-Plugin fest und speichern ein Arbeitsverzeichnis.
Ab dann startet jede neue Aufgabe mit diesen Vorgaben.
Wenn Sie später das Standardplugin ändern, gilt die neue Auswahl für kommende Prompts.
Bereits abgeschlossene Aufgaben bleiben unverändert dokumentiert.

---

## Häufige Fragen (FAQ)

**Muss ich Einstellungen bei jedem Start neu eingeben?**  
Nein. Gespeicherte Werte bleiben erhalten.

**Gelten Änderungen sofort?**  
Ja, für neue Aktionen nach dem Speichern.

**Kann ich auf Standardwerte zurücksetzen?**  
Ja. In den jeweiligen Einstellungsbereichen stehen Rücksetz- oder Default-Optionen zur Verfügung.

**Werden frühere Aufgaben durch neue Einstellungen verändert?**  
Nein. Historische Protokolle und abgeschlossene Abläufe bleiben nachvollziehbar.

---

## Verwandte Funktionen

- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – Speicherort für Arbeitskopien
- [F013 – Claude-CLI-Integration](./F013-claude-cli-integration.md) – KI-Zugangsdaten in Einstellungen
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Standardwerte und Auswahl im Prompt
- [F017 – Lokales Verzeichnis Plugin](./F017-lokales-verzeichnis-plugin.md) – lokale Plugin-Einstellungen wie WorkspaceMode
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md) – Vorgehen bei ungültigen oder nicht nutzbaren Einstellungen
- [Zurück zur Übersicht](../features.md)
