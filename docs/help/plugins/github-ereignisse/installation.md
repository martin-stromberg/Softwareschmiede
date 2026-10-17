← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Einrichtung

## Voraussetzungen

- Das GitHub-SCM-Plugin ist aktiviert und seine GitHub CLI (`gh`) ist verfügbar.
- Ein gültiger **Personal Access Token** ist unter **Einstellungen → Plugins → GitHub → Authentifizierung** gespeichert.
- Der Token muss auf die betroffenen Repositorys zugreifen können. Für Ereignisantworten benötigt er außerdem Schreibrechte für Issues und Pull Requests.
- Aufgaben müssen mit einem GitHub-Repository und der passenden Issue- oder Pull-Request-Referenz verknüpft sein.
- Für die CLI-Verarbeitung ist aktuell Codex CLI oder Claude CLI mit einem unterstützten Schreibmodus und einem tatsächlich erreichbaren privaten Antwortkanal erforderlich. Die Anwendung prüft die Berechtigung, statt sie selbst zu erweitern.

## Einrichtung

1. Öffne **Einstellungen** und wähle **Plugins**.
2. Wähle **GitHub** und trage unter **Authentifizierung** den **Personal Access Token** ein.
3. Klicke **Speichern**.
4. Prüfe in einer Aufgabe, dass Repository und Issue- oder Pull-Request-Referenz korrekt verknüpft sind.
5. Schreibe als Repository-Owner einen Text mit `$Softwareschmiede` in die Beschreibung oder Diskussion und prüfe **SCM-Ereignisse**.

## Hinweise

- Die Ereignisüberwachung beginnt automatisch im Hintergrund; das Abfrageintervall beträgt standardmäßig 60 Sekunden.
- Weitere Optionen für Ereignisabfrage und Verarbeitungsfristen sind derzeit nicht in der Benutzeroberfläche konfigurierbar.
- Standard-SCM-Plugins wie Bitbucket und Local Directory werden nicht automatisch ereignisfähig, nur weil sie in der Anwendung verfügbar sind. Derzeit implementiert ausschließlich GitHub diese Fähigkeit.
- Bei Codex und Claude CLI genügt die Auswahl des Plugins nicht: unbekannte Versionen, nicht unterstützte Berechtigungen oder ein fehlender tatsächlicher Kanalzugriff lassen das Ereignis wartend. GitHub Copilot CLI und Devin unterstützen den Ereignis-Antwortkanal derzeit nicht. Der KI-Simulator dient nur der lokalen Entwicklung und wird für diesen Zweck im SCM-Modus gestartet.
