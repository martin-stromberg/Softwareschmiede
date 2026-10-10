# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Die geforderte Auswahl und das Kopieren sind aus Sicht einer Endanwenderin ohne technische Kennungen oder zusätzliche Einrichtung erreichbar. Die Markierung ist farblich sichtbar. Das Kontextmenü bezeichnet die Aktion verständlich als „Kopieren“ und zeigt das alternative Tastenkürzel „Strg+Umschalt+C“ an. Ohne gültige Auswahl ist die Aktion deaktiviert.

## Befunde

Keine.

## Geprüfte Interaktionen

- Angezeigten CLI-Text mit der Maus markieren → unauffällig: Ziehen mit gedrückter linker Maustaste erzeugt eine sichtbare Auswahl, auch über mehrere sichtbare Zeilen und in umgekehrter Richtung.
- Text mit der Tastatur markieren → unauffällig: Umschalt mit Pfeiltasten beziehungsweise Pos1/Ende erweitert den Bereich von der ursprünglichen Cursorposition aus. Beim Erweitern außerhalb des Ausschnitts wird das Auswahlende sichtbar gemacht.
- Markierten Text in die Zwischenablage kopieren → unauffällig: Die Aktion ist über das beschriftete Kontextmenü sowie das dort angegebene Tastenkürzel erreichbar. Ein Rechtsklick ersetzt die bestehende Auswahl nicht.
- Mehrzeiligen Text außerhalb des Ausgabefensters weiterverwenden → unauffällig in der statischen Prüfung: Kopiert werden die ausgewählten Zeichen als Klartext mit Zeilenumbrüchen; ungenutzter Leerraum am Zeilenende wird entfernt.
- Während laufender CLI-Ausgabe auswählen und kopieren → unauffällig in der statischen Prüfung: Kopieren erfordert kein Anhalten der Sitzung. Gültige Auswahlbereiche bleiben erhalten; veränderte oder nicht mehr verfügbare Inhalte werden nicht unbemerkt als die ursprüngliche Auswahl kopiert.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Controls/TerminalControl.cs`

## Prüfumfang

Basisbranch: `origin/staging`, Merge-Base `1283e2300dabbb8b877f8d7a3a2fb0485394bd08`. Der UI-Diff ist auf das Terminal-Control beschränkt und passt zur Anforderung.

Fachliche Grundlage war ausschließlich `requirement.md`. `plan.md`, `review.md`, `review-code.md` und ältere Review-Ergebnisse wurden nicht gelesen. Prüfung anhand der vollständig gelesenen Oberflächenimplementierung; keine interaktive WPF-Sitzung und kein E2E-Testlauf in diesem Review. Der tatsächliche Maus-/Zwischenablagefluss sowie die Darstellung unter laufender Ausgabe müssen deshalb weiterhin durch den separaten Testschritt nachgewiesen werden.
