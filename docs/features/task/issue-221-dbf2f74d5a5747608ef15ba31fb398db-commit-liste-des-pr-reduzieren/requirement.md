# Übersetzte Anforderung

## Auslöser
Pull-Request-Erstellung über die Ribbon-Action in der Anwendung.

## Ist-Zustand
Wird ein Pull Request mit einem Zielbranch erstellt, der nicht der Hauptbranch ist (z. B. ein Staging-Branch), listet die generierte Pull-Request-Seite alle Commits auf, die noch nicht im **Hauptbranch** enthalten sind. Das führt dazu, dass bereits im Zielbranch gemergte Commits erneut in der Commit-Liste erscheinen.

## Soll-Zustand
Die Commit-Liste des Pull Requests soll ausschließlich die Commits enthalten, die im ausgewählten **Zielbranch** noch nicht vorhanden sind. Als Referenz für die Vergleichs-URL muss daher der Zielbranch herangezogen werden, nicht der Hauptbranch.

## Akzeptanzkriterien
1. Bei der Erzeugung der Pull-Request-URL wird der vom Benutzer gewählte Zielbranch als Basis für den Commit-Vergleich verwendet.
2. Wenn der Zielbranch vom Hauptbranch abweicht, erscheinen nur die Commits, die im Zielbranch fehlen.
3. Bestehende Tests werden angepasst oder erweitert, um das Verhalten zu validieren.
