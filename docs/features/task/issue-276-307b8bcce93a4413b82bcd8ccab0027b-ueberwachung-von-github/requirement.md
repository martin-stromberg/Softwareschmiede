# Übersetzte Anforderung: Überwachung von GitHub

**Quelle:** Issue #276 / Aufgaben-ID `307b8bcc-e93a-4413-b82b-cd8ccab0027b`  
**Branch:** `task/issue-276-307b8bcce93a4413b82bcd8ccab0027b-ueberwachung-von-github`

## Fachliche Zusammenfassung

Die bestehenden SCM-Plugins sollen Ereignisse aus Issues und Pull Requests überwachen können. GitHub ist die erste unterstützte Plattform; weitere SCM-Systeme sollen später nach demselben Prozessmodell ergänzt werden können. Ein Ereignis löst die Verarbeitung aus, wenn ein Kommentar oder Beschreibungstext den Trigger `$Softwareschmiede` enthält und vom Repository-Owner stammt. Das Programm bestätigt den Eingang eines Kommentars mit einer 👀-Reaktion und leitet den Inhalt abhängig vom Aufgaben- und CLI-Status an eine laufende oder neu gestartete CLI weiter. Ist keine Standard-CLI verfügbar, wird das Ereignis in eine Queue aufgenommen und als wartend kenntlich gemacht. Die KI entscheidet selbst über die passende Antwort im ursprünglichen Issue oder Pull Request. Die Aufgabe bleibt aktiv, bis sie durch die KI oder den Nutzer abgeschlossen wird.

## Funktionale Anforderungen

### SCM-Ereignisse

- Bestehende SCM-Plugins erhalten die Fähigkeit, Ereignisse aus Issues und Pull Requests zu überwachen.
- GitHub wird als erste Plattform unterstützt.
- Das Prozessmodell muss eine spätere Ergänzung weiterer SCM-Systeme ermöglichen.
- Als Ereignisinhalt wird ein Kommentar oder Beschreibungstext erkannt.
- Ein Ereignis ist nur gültig, wenn sein Text `$Softwareschmiede` enthält und sein Autor dem Owner des Repositorys entspricht.

### Bestätigung und Aufgabenstatus

- Bei einem Kommentar setzt das Programm eine 👀-Reaktion, um den Empfang zu bestätigen.
- Ist für das zugehörige Issue oder den Pull Request bereits eine Aufgabe aktiv, bleibt sie aktiv.
- Andernfalls wird die Aufgabe aktiviert.

### CLI-Verarbeitung

- Läuft bereits eine CLI für die Aufgabe, wird der Kommentar an diese CLI weitergereicht.
- Läuft keine CLI, prüft das Programm, ob eine Standard-CLI verfügbar ist.
- Ist eine Standard-CLI vorhanden, startet das Programm sie und sendet einen Prompt mit dem Kontext und dem Kommentartext.
- Der Prompt weist die CLI darauf hin, ihre Antwort im ursprünglichen Issue oder Pull Request einzutragen.
- Ist keine Standard-CLI vorhanden, trägt das Programm das Ereignis in eine Queue ein und gibt zusätzlich einen Wartehinweis aus: über ein Warte-Icon, beispielsweise ⏳, oder einen kurzen Kommentar.

### Queue-Verarbeitung

- Der Nutzer kann einen Queue-Eintrag öffnen.
- Beim Öffnen wird die zugehörige Aufgabe aktiviert, falls sie noch nicht aktiv ist.
- Die CLI wird mit einem Auswahl-Dialog gestartet.
- Danach sendet das Programm den regulären Prompt für das Ereignis.

### KI-Antwort und Abschluss

- Die KI entscheidet selbstständig, wie sie den Ursprungskommentar beantwortet. Mögliche Formen sind ein Kommentar, ein Pull Request, eine Reaktion oder eine andere passende Form.
- Das Programm setzt nach der anfänglichen 👀-Reaktion keine weiteren Reaktionen, außer gegebenenfalls dem Warte-Icon. Als Alternative zum Warte-Icon kann es einen kurzen Warte-Kommentar verfassen.
- Die Verarbeitung endet, sobald die KI den Prompt verarbeitet hat.
- Die Aufgabe bleibt aktiv, bis sie durch die KI oder den Nutzer abgeschlossen wird.

## Akzeptanzkriterien

- GitHub-Issue- und Pull-Request-Ereignisse mit `$Softwareschmiede` werden nur verarbeitet, wenn der Autor der Repository-Owner ist.
- Ein gültiger Kommentar wird durch eine 👀-Reaktion bestätigt.
- Eine bereits aktive Aufgabe bleibt aktiv; eine noch nicht aktive Aufgabe wird aktiviert.
- Bei laufender CLI wird der Kommentar an diese CLI weitergereicht.
- Ohne laufende CLI startet eine verfügbare Standard-CLI mit Kontext, Kommentartext und dem Hinweis, im Ursprung zu antworten.
- Ohne Standard-CLI wird ein Queue-Eintrag angelegt und ein Wartezustand kenntlich gemacht.
- Das Öffnen eines Queue-Eintrags aktiviert nötigenfalls die Aufgabe, startet eine CLI mit Auswahl-Dialog und sendet anschließend den regulären Prompt.
- Die KI kann die passende Antwortform selbst wählen; das Programm erzeugt keine zusätzliche Reaktion außer der Empfangsbestätigung und dem gegebenenfalls verwendeten Wartehinweis.
- Nach Verarbeitung des Prompts endet der Prozess, während die Aufgabe bis zu ihrem expliziten Abschluss aktiv bleibt.
- Das Prozessmodell erlaubt die spätere Ergänzung weiterer SCM-Plattformen.

## Offene Punkte

1. Die Anforderung nennt Kommentare und Beschreibungstexte als Ereignisquellen, legt aber die Empfangsbestätigung als Reaktion auf einen Kommentar fest. Wie Beschreibungstext-Ereignisse bestätigt werden, ist nicht spezifiziert.
2. Es ist nicht festgelegt, ob der Trigger `$Softwareschmiede` exakt unter Beachtung der Groß-/Kleinschreibung gesucht wird oder ob Varianten ebenfalls gelten.
3. Für Ereignisse, die aus Beschreibungstexten stammen, ist nicht präzisiert, welcher Text als „Kommentar“ an eine laufende CLI beziehungsweise in den Prompt übergeben wird.
4. Die Queue-Einträge, ihre Darstellung und die konkrete Auswahloberfläche beim Start der CLI sind fachlich nicht weiter beschrieben.
5. Der Wartezustand kann laut Anforderung über ein Warte-Icon oder einen kurzen Kommentar angezeigt werden; eine verbindliche Variante ist nicht festgelegt.
