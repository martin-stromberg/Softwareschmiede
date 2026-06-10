# Projekte — Beschreibung

## Zweck

Ein Projekt bündelt einen thematischen Entwicklungsbereich. Es hält mindestens ein Git-Repository und beliebig viele Aufgaben. Aus der Projektdetailansicht heraus werden neue Aufgaben angelegt und bestehende verwaltet.

## Funktionsweise

Ein Projekt hat einen Namen, eine optionale Beschreibung und einen Status (`Aktiv` oder `Archiviert`). Nur aktive Projekte erscheinen im Dashboard und können Aufgaben erhalten.

Einem Projekt lassen sich ein oder mehrere Git-Repositories zuordnen. Jedes Repository verweist auf einen Plugin-Typ (z.B. `GitHub` oder `LocalDirectoryPlugin`) sowie die Repository-URL. Beim Starten einer Aufgabe wird das passende Repository automatisch ermittelt.

Repositories können eine `RepositoryStartKonfiguration` enthalten, die beim Starten einer Aufgabe ein Startskript ausführt (z.B. `npm install`). Das Skript kann auch nachträglich manuell ausgelöst werden.

## Beispiele

- Projekt „Backend-API" mit einem GitHub-Repository und mehreren Aufgaben für Features und Bugfixes.
- Projekt „Lokale Tools" mit einem `LocalDirectoryPlugin`-Repository, das direkt auf ein Verzeichnis zeigt.

## Einschränkungen

- Archivierte Projekte erscheinen nicht im Dashboard, sind aber weiterhin in der Projektliste sichtbar.
- Hat ein Projekt mehrere aktive Repositories und eine Aufgabe referenziert keines davon eindeutig, wird der Entwicklungsprozessstart verweigert.
