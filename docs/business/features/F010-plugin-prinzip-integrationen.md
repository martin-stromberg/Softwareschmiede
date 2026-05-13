# F010 – Plugin-Prinzip für Integrationen

## Einleitung

Diese Funktion regelt, wie externe Anbindungen in der Softwareschmiede bereitgestellt werden.  
SCM- und KI-Anbindungen laufen als **ausgelagerte Plugins** und nicht fest im Kern der Anwendung.  
Die Anwendung erkennt diese Plugins beim Start bzw. beim ersten Zugriff automatisch.  
Sie müssen dafür keine Einrichtung von Hand durchführen.  
Vor allem ist **keine manuelle DLL-Kopie** nötig.

---

## Wer nutzt es?

**Fachanwender**, die Projekte und Aufgaben mit verschiedenen SCM- und KI-Plugins bearbeiten (z. B. GitHub, Local Directory, Copilot, Claude CLI).  
**Stakeholder**, die nachvollziehen möchten, wie stabil und wartbar die Anbindungen betrieben werden.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen die Softwareschmiede wie gewohnt.
2. Sie arbeiten in **Projektverwaltung** und **Aufgabenverwaltung** weiter wie bisher.
3. Sie starten eine Aufgabe über **KI starten**.
4. Die Anwendung nutzt je nach Auswahl die verfügbaren SCM- und KI-Plugins im Hintergrund.
5. Sie müssen keine Plugin-Dateien selbst kopieren oder verschieben.

---

## Beispiel

Sie starten morgens eine neue Aufgabe in einem bestehenden Projekt.  
Beim Klick auf **KI starten** nutzt die Anwendung z. B. **GitHub + Copilot** oder **Local Directory + Claude CLI**.  
Das geschieht über ausgelagerte Plugins, die bereits automatisch erkannt wurden.  
Sie mussten vorher keine DLL-Datei in einen Ordner kopieren.

---

## Was passiert im Hintergrund?

Beim Start bzw. beim ersten Plugin-Zugriff prüft die Softwareschmiede ihren Plugin-Ordner automatisch.  
Gefundene SCM- und KI-Plugins werden direkt verwendet.  
Die Bereitstellung der nötigen Dateien erfolgt automatisch.  
Darum ist keine manuelle DLL-Kopie notwendig.

---

## Häufige Fragen (FAQ)

**Muss ich Plugins nach jedem Update neu einrichten?**  
Nein. Die Softwareschmiede erkennt ausgelagerte Plugins automatisch.

**Muss ich DLL-Dateien selbst in den Plugin-Ordner kopieren?**  
Nein. Es ist keine manuelle DLL-Kopie nötig.

**Merke ich im Alltag einen Unterschied durch das Plugin-Prinzip?**  
Ja, meist nur positiv. Die Nutzung bleibt gleich, die Struktur ist klarer.

**Was ist der Vorteil für Stakeholder?**  
Änderungen an GitHub oder Copilot bleiben besser getrennt und planbar.

**Kann ich trotzdem normal mit Projekten und Aufgaben arbeiten?**  
Ja. Ihr gewohnter Ablauf bleibt unverändert.

**Unterstützen alle SCM-Plugins dieselben Funktionen?**  
Nein. Zum Beispiel ist das Local Directory Plugin auf lokale Git-Arbeit ausgelegt und bietet keine Remote-Funktionen wie Pull Requests.

---

## Verwandte Funktionen

- [F001 – Projektverwaltung](./F001-projektverwaltung.md) – Projekte mit GitHub-Ablage verwalten
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Aufgaben mit Copilot bearbeiten
- [F017 – Lokales Verzeichnis Plugin](./F017-lokales-verzeichnis-plugin.md) – lokale SCM-Arbeit ohne Remote-Provider
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Standard- und Ad-hoc-Auswahl von KI-Plugins
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md) – Ergebnisse in GitHub übergeben
- [Technische Schnittstellen](../../api/plugin-interfaces.md) – Verträge und PluginManager-Verhalten
- [Flow Plugin-Discovery](../../flows/plugin-discovery-load-flow.md) – Ablauf der dynamischen Plugin-Erkennung
- [Testplan Plugin-Klassenbibliotheken](../../tests/testplan-plugin-klassenbibliotheken-github-und-copilot.md) – Testabdeckung für das Feature
- [Zurück zur Übersicht](../features.md)
