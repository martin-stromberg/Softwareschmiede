# Anforderung

## Fachliche Zusammenfassung

GitHub-Code-Scanning und verwandte GitHub-Sicherheits- und Qualitätsprüfungen melden Alerts, diese erscheinen derzeit aber nicht als bearbeitbare Anforderungen im Programm. Dadurch können erkannte Sicherheits- und Qualitätsprobleme nicht direkt wie normale Entwicklungsaufgaben aus dem offenen Anforderungsbestand heraus bearbeitet werden.

Das Programm soll GitHub-Alerts lesen und gemeinsam mit offenen Anforderungen anzeigen. Wird ein solcher Alert ausgewählt, soll daraus eine neue Aufgabe erzeugt werden, analog zur bisherigen Erstellung von Aufgaben aus SCM-Issues. Zusätzlich soll für diese Aufgabe automatisch ein neues GitHub-Issue angelegt werden, damit die Bearbeitung auch in GitHub als reguläres Issue nachvollziehbar ist.

Die SCM-Plugin-Schnittstelle muss dafür fachlich zwischen normalen Issues und Alerts unterscheiden können.

## Betroffene Funktionen

- Anzeige offener Anforderungen: GitHub-Alerts sollen in der Liste der offenen Anforderungen erscheinen.
- Aufgabenerstellung aus Anforderungen: Ein Klick auf einen Alert soll eine Aufgabe erstellen, analog zur bestehenden Issue-Verarbeitung.
- GitHub-Issue-Erstellung: Beim Erstellen einer Aufgabe aus einem Alert soll automatisch ein neues GitHub-Issue für diese Aufgabe angelegt werden.
- SCM-Plugin-Abstraktion: Die Schnittstelle muss Issue-Quellen und Alert-Quellen unterscheiden, damit GitHub-spezifische Alerts korrekt verarbeitet werden können.

## Fachliche Regeln

- GitHub-Issues bleiben weiterhin normale SCM-Issues.
- GitHub-Alerts werden als eigene Art von SCM-Anforderung behandelt und dürfen nicht stillschweigend wie bestehende Issues modelliert werden.
- Aus einem Alert entsteht erst beim Anklicken beziehungsweise Auswählen eine lokale Aufgabe.
- Beim Erstellen dieser Aufgabe muss automatisch ein zugehöriges GitHub-Issue erzeugt werden.
- Die neue Funktion gilt ausschließlich für GitHub.
- Jira für Bitbucket liefert keine vergleichbaren Security- oder Quality-Alert-Aufgaben und muss diese Funktion daher nicht unterstützen.

## Erwartetes Ergebnis

- Benutzer sehen GitHub-Sicherheits- und Qualitätsalerts in der Liste der offenen Anforderungen.
- Benutzer können einen Alert auswählen und daraus eine Entwicklungsaufgabe erzeugen.
- Zu der erzeugten Aufgabe existiert automatisch ein neues GitHub-Issue.
- Die SCM-Plugin-Schnittstelle ist so erweitert, dass Implementierungen zwischen Issues und Alerts unterscheiden können.
- Nicht-GitHub-SCM-Integrationen werden durch die GitHub-only-Funktion nicht zu einer Alert-Unterstützung verpflichtet.

## Abgrenzung

- Keine Alert-Unterstützung für Jira für Bitbucket.
- Keine allgemeine Pflicht für alle SCM-Plugins, Security- oder Quality-Alerts bereitzustellen.
- Keine Aussage dazu, welche konkreten GitHub-Alert-Typen zuerst unterstützt werden müssen; relevant sind die von GitHub-Code-Scanning-Bots erkannten Sicherheits- und Qualitätsprobleme.

## Offene Fragen

- Welche konkreten GitHub-Alert-Quellen sollen initial gelesen werden, zum Beispiel Code Scanning, Dependabot, Secret Scanning oder nur Code Scanning?
- Nach welchem Schema soll das automatisch erzeugte GitHub-Issue betitelt und beschrieben werden?
- Soll verhindert werden, dass aus demselben GitHub-Alert mehrfach Aufgaben oder GitHub-Issues erzeugt werden?
- Wie soll der Status eines Alerts behandelt werden, nachdem daraus eine Aufgabe und ein GitHub-Issue erstellt wurden?
