# Anforderung

## Fachliche Zusammenfassung

Pull Requests, die aus einer Aufgabe heraus über die Ribbon-Action ausgelöst werden, sollen dauerhaft zur Aufgabe gespeichert und anschließend überwacht werden. Für Aufgaben soll zusätzlich zu den bestehenden Inhaltsbereichen `Info`, `CLI` und `Dateien` ein neuer Inhaltsbereich `PR` angeboten werden.

Im neuen Inhaltsbereich sollen alle Pull Requests der Aufgabe angezeigt werden. Zu jedem Pull Request sollen der Pull-Request-Status und die Status der zugehörigen Actions sichtbar sein. Vorerst muss nur GitHub unterstützt werden.

Das GitHub-Plugin soll eine Einstellung erhalten, mit der Pull Requests nach erfolgreichem Abschluss der zugehörigen Actions automatisch bestätigt werden können. Dabei muss berücksichtigt werden, dass GitHub unter Umständen eine normale Genehmigung durch den Pull-Request-Ersteller nicht akzeptiert und daher ein Bypass erforderlich sein kann.

Nachdem ein Pull Request bestätigt beziehungsweise gemergt wurde, sollen auch die Actions überwacht werden, die durch den Merge ausgelöst werden.

## Betroffene Klassen und Komponenten

### Aufgabenoberfläche

- Aufgaben-Detailansicht mit den bestehenden Inhaltsbereichen `Info`, `CLI` und `Dateien`
- Ribbon-Action zum Auslösen eines Pull Requests
- Neuer Inhaltsbereich `PR` für Pull Requests und Action-Status einer Aufgabe

### Persistenz und Domänenmodell

- Persistierung der Pull-Request-Referenzen je Aufgabe
- Modellierung von Pull-Request-Metadaten wie Provider, Repository, Nummer, URL, Status und Merge-Status
- Modellierung der zugehörigen Action- beziehungsweise Workflow-Status
- Zuordnung von Pull Requests und Workflow-Runs zu Aufgaben

### GitHub-Plugin

- Erstellung oder Auslösung von Pull Requests über die bestehende Ribbon-Action
- Abfrage und Aktualisierung von Pull-Request-Status
- Abfrage und Aktualisierung von GitHub-Actions-/Workflow-Run-Status
- Neue Plugin-Einstellung für automatisches Bestätigen beziehungsweise Mergen nach erfolgreichen Actions
- Unterstützung eines notwendigen Bypass-Verfahrens, wenn GitHub eine Genehmigung durch den Ersteller nicht als reguläre Approval akzeptiert

### Hintergrundverarbeitung

- Überwachung gespeicherter Pull Requests
- Überwachung der Actions vor dem Bestätigen/Mergen
- Überwachung der Actions, die nach dem Merge ausgelöst werden
- Statusaktualisierung und Fehlerbehandlung bei GitHub-API-Fehlern oder fehlenden Berechtigungen

## Funktionale Anforderungen

1. Wird ein Pull Request über die Ribbon-Action ausgelöst, muss die Anwendung den Pull Request persistent mit der zugehörigen Aufgabe verknüpfen.
2. Die Anwendung muss gespeicherte Pull Requests einer Aufgabe überwachen und ihren aktuellen Status aktualisieren.
3. Die Aufgaben-Detailansicht muss einen neuen Inhaltsbereich `PR` anbieten.
4. Der Inhaltsbereich `PR` muss die Pull Requests der Aufgabe anzeigen.
5. Der Inhaltsbereich `PR` muss zu jedem Pull Request die Status der zugehörigen GitHub Actions anzeigen.
6. Das GitHub-Plugin muss eine Einstellung für automatisches Bestätigen beziehungsweise Mergen eines Pull Requests bereitstellen.
7. Ist die automatische Bestätigung aktiviert, darf ein Pull Request erst nach erfolgreichem Abschluss der relevanten Actions bestätigt beziehungsweise gemergt werden.
8. Die Implementierung muss berücksichtigen, dass für das Bestätigen beziehungsweise Mergen unter Umständen ein GitHub-Bypass notwendig ist.
9. Nachdem ein Pull Request bestätigt beziehungsweise gemergt wurde, muss die Anwendung auch die durch den Merge ausgelösten Actions überwachen.
10. Der initiale Provider-Scope ist GitHub; andere Pull-Request-Provider müssen nicht umgesetzt werden.

## Implementierungsansatz

### Pull Request erfassen und persistieren

Die bestehende Ribbon-Action soll nach erfolgreichem Auslösen eines Pull Requests die relevanten Pull-Request-Daten an die Anwendung zurückgeben oder über einen Service persistieren. Die Persistenz sollte providerneutral genug modelliert werden, um GitHub jetzt abzubilden und weitere Provider später ergänzen zu können, ohne die Aufgabenoberfläche erneut grundlegend umzubauen.

Zu speichern sind mindestens:

- Aufgaben-ID
- Provider (`GitHub`)
- Repository-Identifikation
- Pull-Request-Nummer oder eindeutige Provider-ID
- Pull-Request-URL
- Pull-Request-Status
- Merge-/Abschlussstatus
- Zeitpunkt der letzten Statusprüfung

### PR-Inhaltsbereich

Die Aufgaben-Detailansicht soll einen neuen Tab oder Inhaltsbereich `PR` erhalten. Dieser Bereich zeigt eine Liste der mit der Aufgabe verknüpften Pull Requests und pro Pull Request den aktuellen Status sowie die zugehörigen Action- beziehungsweise Workflow-Status.

Der Bereich sollte auch Zustände für "keine Pull Requests vorhanden", "Status wird geladen", "Fehler beim Abrufen" und "keine Actions gefunden" abbilden.

### GitHub-Statusüberwachung

Für GitHub sollen Pull-Request-Status und Actions regelmäßig oder ereignisgetrieben aktualisiert werden. Die Überwachung soll sowohl Pull Requests beobachten, die noch auf erfolgreiche Actions warten, als auch Pull Requests, die bereits bestätigt beziehungsweise gemergt wurden und dadurch neue Actions ausgelöst haben.

Die Statuslogik sollte klar zwischen diesen Phasen unterscheiden:

- Pull Request erstellt und gespeichert
- Pre-Merge-Actions laufen
- Pre-Merge-Actions erfolgreich
- Pull Request automatisch oder manuell bestätigt beziehungsweise gemergt
- Post-Merge-Actions laufen
- Post-Merge-Actions erfolgreich oder fehlgeschlagen

### Automatischer Abschluss

Das GitHub-Plugin soll eine Einstellung erhalten, mit der der automatische Abschluss aktiviert oder deaktiviert werden kann. Bei aktivierter Einstellung prüft die Anwendung nach erfolgreichem Abschluss der relevanten Actions, ob der Pull Request bestätigt beziehungsweise gemergt werden kann.

Wenn GitHub die Genehmigung durch den Pull-Request-Ersteller nicht als reguläres Approval akzeptiert oder Branch-Protection-Regeln einen Bypass erfordern, muss die Implementierung eine dafür geeignete GitHub-API-Funktion oder einen konfigurierten Bypass-Pfad verwenden. Fehlende Berechtigungen müssen sichtbar protokolliert und im PR-Inhaltsbereich nachvollziehbar angezeigt werden.

## Konfiguration

Im GitHub-Plugin ist mindestens eine neue Einstellung erforderlich:

- Automatischer PR-Abschluss nach erfolgreichen Actions: aktiviert/deaktiviert

Je nach bestehender Plugin-Architektur können zusätzliche Einstellungen erforderlich werden:

- Strategie für den Abschluss: Approval, Merge, Auto-Merge oder Bypass
- Merge-Methode: Merge-Commit, Squash oder Rebase
- Optionaler Bypass-Modus, falls Branch-Protection-Regeln dies benötigen

## Nicht-Ziele

- Unterstützung anderer Provider als GitHub
- Vollständiges Provider-abstraktes Pull-Request-Management über GitHub hinaus
- Manuelle Review-Kommentare oder Code-Review-Funktionen im neuen PR-Inhaltsbereich
- Änderung der fachlichen Regeln von GitHub Actions selbst

## Offene Fragen

1. Bedeutet "bestätigt" fachlich ein GitHub-Approval, das Aktivieren von Auto-Merge oder das tatsächliche Mergen des Pull Requests?
2. Welche Pull-Request-Actions gelten als relevant für den automatischen Abschluss: alle Checks, nur required checks aus Branch Protection oder eine konfigurierbare Auswahl?
3. Soll der automatische Abschluss direkt mergen oder nur die Voraussetzungen schaffen, damit GitHub Auto-Merge übernimmt?
4. Welche GitHub-Berechtigungen stehen dem Plugin zur Verfügung, insbesondere für Approval, Merge, Auto-Merge und Bypass?
5. Wie soll die Anwendung reagieren, wenn ein Bypass erforderlich ist, aber die Berechtigung fehlt?
6. In welchem Intervall oder über welchen Mechanismus sollen Pull Requests und Actions überwacht werden?
7. Sollen mehrere Pull Requests pro Aufgabe unterstützt werden, und falls ja, sollen sie alle gleichwertig angezeigt und überwacht werden?
8. Welche Post-Merge-Actions sollen beobachtet werden: alle Workflow-Runs auf dem Zielbranch, nur durch den Merge-Commit ausgelöste Runs oder nur Workflows, die dem Pull Request zuordenbar sind?
