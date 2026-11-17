← [Zurück zur Übersicht](index.md)

# Projektseite (GitHub Pages) — Beschreibung

## Zweck

Die Projektseite ist der öffentliche Auftritt der Softwareschmiede im Web. Sie richtet sich an Interessenten, die sich einen schnellen Überblick über die Anwendung verschaffen oder direkt zum Download gelangen möchten — ohne vorher das Repository durchsuchen zu müssen.

## Funktionsweise

Die Seite ist eine einzelne, deutschsprachige Landing-Page mit klarer Typografie und responsivem Layout. Sie folgt automatisch der Hell-/Dunkel-Einstellung des Betriebssystems des Besuchers; die Farbwerte sind an das Erscheinungsbild der Anwendung angelehnt. Ein manueller Umschalter ist nicht vorgesehen.

Aufbau der Seite von oben nach unten:

- **Kopfbereich:** Produktname mit Logo sowie eine Navigationsleiste mit Sprüngen zu den Abschnitten „Features", „Screenshots", „Download" und „Dokumentation" sowie einem Verweis auf das GitHub-Repository. Der Kopfbereich bleibt beim Scrollen sichtbar.
- **Einstiegsbereich:** Produkttitel, Tagline und Kurzbeschreibung, dazu zwei Schaltflächen — „Neueste Version herunterladen" (führt zu den GitHub Releases) und „Repository auf GitHub".
- **Features:** Acht Karten mit den zentralen Fähigkeiten — Projekt- und Aufgabenverwaltung, Anbindung von GitHub, BitBucket und lokalen Ordnern per Plugin, KI-Agenten im eingebetteten Terminal (Claude CLI, GitHub Copilot, Codex, Devin), Pull-Request-Workflow, GitHub-Ereignisüberwachung, Programmupdate aus der Anwendung, IDE- und Dateisystem-Integration sowie zeitgesteuerte Prompts und Automatisierung.
- **Screenshots:** Bildergalerie mit sechs Ansichten der Anwendung im dunklen Design — Dashboard mit aktiven Aufgaben, Projektübersicht, Projektansicht, Aufgabendetail mit eingebettetem Terminal, SCM-Ereignisse und Einstellungen.
- **Download:** Verweise auf das aktuelle Release (inklusive `release.zip` für Windows) und auf die vollständige Versionshistorie mit Release Notes und Vorabversionen.
- **Dokumentation & Repository:** Verweise auf das GitHub-Repository, die README und die Anwenderdokumentation.
- **Fußzeile:** Verweise auf MIT-Lizenz, Sicherheitshinweise (`SECURITY.md`), Drittanbieter-Lizenzen und das Repository.

## Beispiele

- Ein Interessent ruft `https://martin-stromberg.github.io/Softwareschmiede/` auf, liest die Feature-Übersicht und gelangt über „Neueste Version herunterladen" direkt zum aktuellen Release.
- Ein Besucher mit dunkler Systemeinstellung sieht die Seite automatisch in der dunklen Variante — ohne eine Einstellung vornehmen zu müssen.
- Auf dem Smartphone werden die Karten untereinander statt nebeneinander angeordnet; alle Inhalte bleiben lesbar und erreichbar.

## Einschränkungen

- Die Seite erscheint ausschließlich auf Deutsch; es gibt keine Mehrsprachigkeit.
- Die Hell-/Dunkel-Darstellung folgt ausschließlich der Systemeinstellung — ein manueller Umschalter auf der Seite existiert nicht.
- Die Screenshots werden manuell gepflegt und im Repository unter `site/assets/screenshots/` abgelegt; sie zeigen die Anwendung ausschließlich im dunklen Design.
- Die Seite enthält keine dynamischen Inhalte: kein Kontaktformular, keine News-Sektion, kein eigenes Release-Notes-Listing und kein Tracking.
- Die Seite spiegelt ausschließlich den jeweils veröffentlichten Projektstand; inhaltliche Änderungen erscheinen erst nach dem regulären Release-Prozess des Projekts.
