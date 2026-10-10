# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

Basisbranch: `origin/staging` (Diff gegen den gemeinsamen Vorfahren einschließlich Arbeitsstand).

Prüfmethode: Statischer Bedienablauf-Review der vollständigen geänderten UI-Datei anhand von `requirement.md`. Keine interaktive WPF-Ausführung; die Befunde ergeben sich unmittelbar aus den vorhandenen Bedienmöglichkeiten und Auswahlabläufen. `plan.md`, `review.md` und `review-code.md` wurden nicht gelesen.

## Befunde

### TerminalControl.cs (CLI-Ausgabe): Kopieraktion nicht auffindbar

- **Erreichbarkeit** — Nach dem Markieren eines Texts gibt es weder einen beschrifteten Kopierbefehl noch einen Hinweis auf das einzige funktionierende Kürzel `Ctrl+Shift+C` (`OnPreviewKeyDown`, Zeile 297). Eine Anwenderin muss dieses Kürzel bereits kennen. Rechtsklick bietet keinen Kopierbefehl, und das aus anderen Anwendungen bekannte `Ctrl+C` wird an die CLI weitergereicht. Damit ist der zentrale zweite Schritt der geforderten Aufgabe aus der Oberfläche nicht erkennbar.

  Empfehlung: Ein Kontextmenü mit „Kopieren“ und angezeigtem Kürzel „Strg+Umschalt+C“ anbieten, dessen Verfügbarkeit der gültigen Auswahl entspricht. Alternativ eine gleichwertige sichtbar beschriftete Kopieraktion am Terminal anbieten. Die bestehende CLI-Bedeutung von `Ctrl+C` beibehalten.

### TerminalControl.cs (CLI-Ausgabe): Erste Tastaturauswahl markiert nicht den erwarteten Bereich

- **Abweichendes Muster** — Bei einer noch nicht vorhandenen Auswahl wird der Auswahlbeginn erst nach der Navigation gesetzt (`ExtendSelection`, Zeilen 662–678). Steht der Terminalcursor hinter `Fehler`, markiert das erste `Shift+Home` deshalb nur das `F`, statt die Ausgabe vom Cursor bis zum Zeilenanfang auszuwählen. Entsprechend markiert das erste `Shift+Up` nur eine Zelle der vorherigen Zeile. Eine Anwenderin, die die angebotene Tastaturauswahl wie die übliche Textauswahl bedient, kopiert dadurch einen unvollständigen Text.

  Empfehlung: Beim Beginn einer Tastaturauswahl die Ausgangsposition vor dem Navigationsschritt als Anker festhalten. Der erste Tastendruck muss bereits den Bereich zwischen Ausgangs- und Zielposition markieren. `Shift+Home`, `Shift+End` und `Shift+Up` ohne vorherigen Mausklick gezielt prüfen.

### TerminalControl.cs (CLI-Ausgabe): Tastaturauswahl läuft aus dem sichtbaren Ausschnitt

- **Erreichbarkeit** — Wird eine Auswahl mit `Shift+Up` über die obere sichtbare Zeile hinaus erweitert, wandert das Auswahlende zwar in den Verlauf, die Anzeige scrollt jedoch nicht mit (`ExtendSelection`, Zeilen 657–679). Die Anwenderin kann nicht mehr sehen, wo ihre Markierung endet, und muss den zu kopierenden Bereich blind abschätzen. Dasselbe Problem betrifft die horizontale Erweiterung in einer breiten Wiedergabeausgabe.

  Empfehlung: Nach jeder Tastaturerweiterung den Ausschnitt so verschieben, dass das aktive Auswahlende sichtbar bleibt. Beim Erreichen des Verlaufs muss die Endverfolgung entsprechend deaktiviert werden; Auswahl und Scrollposition sollen gemeinsam ein nachvollziehbares Ergebnis anzeigen.

## Geprüfte Interaktionen

- Sichtbaren CLI-Text mit der Maus markieren → im sichtbaren Ausschnitt statisch unauffällig; blaue Bereichsmarkierung vorhanden.
- CLI-Text mit der Tastatur markieren → Befunde vorhanden: falscher Startanker und fehlendes Mitscrollen.
- Markierten Text in die Zwischenablage kopieren → Befund vorhanden: Aktion ohne sichtbaren Einstieg oder Kürzelhinweis.
- Mehrzeilig ausgewählten Text außerhalb der CLI weiterverwenden → statisch unauffällig: Ausgabe erfolgt als Text mit Zeilenumbrüchen; keine Aussage über einen interaktiv geprüften Zwischenablageinhalt.
- Während laufender CLI-Ausgabe markieren und kopieren → statisch unauffällig hinsichtlich Zuordnung: Auswahl verwendet stabile Zeilenidentitäten und wird bei veränderten markierten Zellen verworfen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Controls/TerminalControl.cs` (vollständig; einzige geänderte UI-Datei).

Zur Einordnung der vorhandenen Bedienzugänge wurden zusätzlich die Terminal-Einbindungen in `TaskDetailView.xaml`, `TaskDetailView.xaml.cs` und `KonsolenTestDialog.xaml` durchsucht. Daraus wurden keine unabhängigen Befunde zu unveränderten UI-Bereichen abgeleitet.
