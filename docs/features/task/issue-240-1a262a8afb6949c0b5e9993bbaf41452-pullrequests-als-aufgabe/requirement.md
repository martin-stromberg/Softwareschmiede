# Anforderung: Pullrequests als Aufgabe

## Metadaten

- Aufgaben-ID: `1a262a8a-fb69-49c0-b5e9-993bbaf41452`
- Branch: `task/issue-240-1a262a8afb6949c0b5e9993bbaf41452-pullrequests-als-aufgabe`
- Erstellt: `2026-08-30`
- Betroffene Bereiche: Projektdetailansicht, SCM-Plugin fuer GitHub, SCM-Plugin fuer BitBucket/Jira

## Problem

In der Projektdetailansicht werden derzeit nur bereits in der Anwendung angelegte Aufgaben und aus SCM-Plugins abgerufene Issues als moegliche neue Aufgaben angeboten. Offene Pullrequests werden nicht angezeigt. Dadurch koennen Pullrequests nicht direkt als Aufgabe angelegt und einem Code-Review zugefuehrt werden.

## Ziel

Offene Pullrequests sollen aus den unterstuetzten SCM-Plugins als Vorschlaege in der Projektdetailansicht erscheinen und analog zu Issues als Aufgaben angelegt werden koennen. Die angelegte Aufgabe muss mit dem Pullrequest verknuepft sein und beim Start den Quell-Branch des Pullrequests fuer ein Code-Review verwenden.

## Umfang

### Im Umfang

- Abruf offener Pullrequests im GitHub-SCM-Plugin.
- Abruf offener Pullrequests im BitBucket/Jira-SCM-Plugin.
- Anzeige offener Pullrequests in der Vorschlagsliste der Projektdetailansicht zusaetzlich zu Issues.
- Eindeutige Kennzeichnung von Pullrequests gegenueber Issues, beispielsweise durch ein eigenes Icon oder Label.
- Ausschluss von Pullrequests, die bereits einer bestehenden Aufgabe zugeordnet sind.
- Anlegen einer neuen Aufgabe aus einem Pullrequest-Vorschlag.
- Verknuepfung der neuen Aufgabe mit dem zugrunde liegenden Pullrequest.
- Auschecken des Pullrequest-Quell-Branches beim Start einer solchen Aufgabe.

### Nicht im Umfang

- Erweiterung um weitere SCM-Anbieter.
- Aenderung des bestehenden Issue-Workflows ausserhalb der fuer die gemeinsame Vorschlags- und Verknuepfungslogik erforderlichen Anpassungen.
- Erstellung, Aktualisierung oder Zusammenfuehrung von Pullrequests.

## Funktionale Anforderungen

### FR-1: Offene Pullrequests abrufen

Das GitHub-SCM-Plugin und das BitBucket/Jira-SCM-Plugin muessen offene Pullrequests des konfigurierten Projekts bzw. Repositorys abrufen und fuer die weitere Verarbeitung bereitstellen.

### FR-2: Pullrequests als Vorschlaege anzeigen

Die Projektdetailansicht muss offene Pullrequests gemeinsam mit den vorhandenen Issue-Vorschlaegen in der Liste moeglicher neuer Aufgaben anzeigen.

### FR-3: Pullrequest-Vorschlaege kennzeichnen

Jeder Pullrequest-Vorschlag muss visuell und inhaltlich eindeutig als Pullrequest erkennbar sein, zum Beispiel durch ein eigenes Icon, ein Label oder eine vergleichbare Kennzeichnung.

### FR-4: Bereits verknuepfte Pullrequests ausblenden

Pullrequests, die bereits einer bestehenden Aufgabe zugeordnet sind, duerfen nicht erneut als Vorschlag angezeigt werden.

### FR-5: Aufgabe aus Pullrequest anlegen

Ein Benutzer muss einen Pullrequest-Vorschlag analog zu einem Issue als neue Aufgabe anlegen koennen. Die neue Aufgabe muss dabei die Verknuepfung zum Pullrequest uebernehmen.

### FR-6: Pullrequest-Quell-Branch verwenden

Beim Start einer Aufgabe, die mit einem Pullrequest verknuepft ist, darf im geklonten Repository kein neuer Branch erzeugt werden. Stattdessen muss der Quell-Branch des Pullrequests ausgecheckt werden.

### FR-7: Bestehenden Aufgabenstart erhalten

Aufgaben ohne Pullrequest-Verknuepfung muessen weiterhin nach dem bestehenden Verhalten gestartet werden, insbesondere hinsichtlich der Erzeugung bzw. Auswahl eines Arbeits-Branches.

## Geschaeftsregeln

- Nur offene Pullrequests werden als neue Aufgaben vorgeschlagen.
- Ein Pullrequest darf hoechstens einmal als Vorschlag fuer eine neue Aufgabe erscheinen, sobald er einer bestehenden Aufgabe zugeordnet ist.
- Die Pullrequest-Kennung und die fuer den Checkout erforderlichen Branch-Informationen muessen in der Aufgabenverknuepfung erhalten bleiben.
- Der Quell-Branch des Pullrequests ist die Arbeitsgrundlage fuer eine daraus gestartete Review-Aufgabe.

## Akzeptanzkriterien

- [ ] In der Projektdetailansicht werden offene GitHub-Pullrequests zusaetzlich zu Issues als Vorschlaege angezeigt.
- [ ] In der Projektdetailansicht werden offene BitBucket/Jira-Pullrequests zusaetzlich zu Issues als Vorschlaege angezeigt.
- [ ] Pullrequest-Vorschlaege sind eindeutig von Issue-Vorschlaegen unterscheidbar.
- [ ] Ein bereits einer Aufgabe zugeordneter Pullrequest erscheint nicht erneut in der Vorschlagsliste.
- [ ] Aus einem Pullrequest-Vorschlag kann eine neue Aufgabe angelegt werden.
- [ ] Die neu angelegte Aufgabe ist mit dem Pullrequest verknuepft.
- [ ] Beim Start der verknuepften Aufgabe wird der Quell-Branch des Pullrequests ausgecheckt und kein neuer Branch erzeugt.
- [ ] Der Start von Aufgaben ohne Pullrequest-Verknuepfung bleibt unveraendert.

## Nichtfunktionale Anforderungen

- Pullrequests muessen in der Vorschlagsliste konsistent und ohne Verwechslung mit Issues dargestellt werden.
- Die bestehende Funktionalitaet fuer Issues und fuer Aufgaben ohne Pullrequest-Verknuepfung darf nicht regressieren.
- Der Benutzerfluss vom Vorschlag bis zum Start der Review-Aufgabe muss in der Projektdetailansicht nachvollziehbar und bedienbar sein.

## Offene Punkte

- Keine fachlichen offenen Punkte identifiziert.
