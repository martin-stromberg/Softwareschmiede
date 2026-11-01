← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Einrichtung

## Voraussetzungen

- Das GitHub-SCM-Plugin ist aktiviert. Die GitHub CLI (`gh`) wird für die Überwachung und alle lesenden Zugriffe **nicht** benötigt — sie läuft über direkte HTTP-Aufrufe. `gh` muss nur vorhanden sein, wenn die seltenen Komfortaktionen Issue-Anlage, PR-Anlage oder PR-Abschluss (Merge/Review) aus der Anwendung heraus genutzt werden oder Agenten in ihrer Sitzung selbst `gh` aufrufen sollen.
- Ein gültiger **Personal Access Token** ist unter **Einstellungen → Plugins → GitHub → Authentifizierung** gespeichert.
- Der Token muss auf die betroffenen Repositorys zugreifen können. Für Ereignisantworten benötigt er außerdem Schreibrechte für Issues und Pull Requests.
- Aufgaben müssen mit einem GitHub-Repository und der passenden Issue- oder Pull-Request-Referenz verknüpft sein.
- Für die CLI-Verarbeitung ist aktuell Codex CLI, Claude CLI oder Devin CLI mit einem unterstützten Schreibmodus und einem tatsächlich erreichbaren privaten Antwortkanal erforderlich. Die Anwendung prüft die Berechtigung, statt sie selbst zu erweitern. Bei Devin CLI wird `Softwareschmiede.Devin.CommandLineParameters` auf einen unterstützten Startvertrag gesetzt, z. B. `--permission-mode accept-edits --respect-workspace-trust false`.

## Einrichtung

1. Öffne **Einstellungen** und wähle **Plugins**.
2. Wähle **GitHub** und trage unter **Authentifizierung** den **Personal Access Token** ein.
3. Klicke **Speichern**.
4. Prüfe in einer Aufgabe, dass Repository und Issue- oder Pull-Request-Referenz korrekt verknüpft sind.
5. Schreibe als Repository-Owner einen Text mit dem Trigger-Keyword (Standard `$Softwareschmiede`) in die Beschreibung oder Diskussion und prüfe **SCM-Ereignisse**.

## Separates Konto (Bot-Konto)

Alternativ kann die Überwachung unter **Einstellungen → Allgemein → SCM-Ereignisse → Konto-Modus** auf **„Separates Konto“** umgeschaltet werden. Dann wird nicht mehr repository-weit enumeriert, sondern ausschließlich die GitHub-Notifications-Inbox eines eigenen Bot-Kontos gelesen; aus `reason = mention`-Einträgen entstehen Ereignisse.

1. Lege ein separates GitHub-Konto an und lade es als **Collaborator mit Schreibrecht** in die zu überwachenden Repositories ein.
2. Erstelle für dieses Konto einen Personal Access Token mit den Scopes `notifications` und `repo` und trage ihn unter **Einstellungen → Plugins → GitHub → Authentifizierung** im Feld **„Personal Access Token des separaten Kontos“** ein.
3. Wähle unter **Allgemein → SCM-Ereignisse** den **Konto-Modus „Separates Konto“** und speichere. Ohne eingetragenes Token des separaten Kontos bricht das Speichern mit einer Fehlermeldung ab.
4. Eine Aufgabe wird ausgelöst, wenn der Repository-Owner das separate Konto frei stehend als `@bot-name` in einer Beschreibung oder einem Kommentar erwähnt.

## Hinweise

- Die Ereignisüberwachung beginnt automatisch im Hintergrund; das Abfrageintervall beträgt standardmäßig 60 Sekunden.
- Unter **Einstellungen → Allgemein → SCM-Ereignisse** lässt sich die Überwachung abschalten („SCM-Ereignisüberwachung aktivieren") und das Trigger-Keyword anpassen. Beide Änderungen wirken ohne Neustart. Ein deaktivierter Schalter stoppt alle GitHub-Abfragen und die automatische Zustellung; bereits eingegangene Ereignisse bleiben bestehen.
- Weitere Optionen für Ereignisabfrage und Verarbeitungsfristen sind derzeit nicht in der Benutzeroberfläche konfigurierbar.
- Standard-SCM-Plugins wie Bitbucket und Local Directory werden nicht automatisch ereignisfähig, nur weil sie in der Anwendung verfügbar sind. Derzeit implementiert ausschließlich GitHub diese Fähigkeit.
- Bei Codex, Claude und Devin CLI genügt die Auswahl des Plugins nicht: Versionen außerhalb der unterstützten Bereiche (Codex `v0.114.x`, Claude Code `v2.1.x`, Devin CLI `v3000.11.x`), unbekannte Promptformate, nicht unterstützte Berechtigungen oder ein fehlender tatsächlicher Kanalzugriff lassen das Ereignis wartend. Eine erkannte, aber außerhalb des Bereichs liegende Version kann in der Ereignisübersicht ausdrücklich bestätigt werden. GitHub Copilot CLI unterstützt den Ereignis-Antwortkanal derzeit nicht. Der KI-Simulator dient nur der lokalen Entwicklung und wird für diesen Zweck im SCM-Modus gestartet.
