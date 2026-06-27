# Anforderung

## Ziel
Fuer die Anwendung **Softwareschmiede** soll ein neues **Codex-KI-Plugin** entstehen.

Das Plugin soll es ermoeglichen, die **Codex-CLI innerhalb der Anwendung** aufzurufen und als KI-gestuetzte Funktion in die bestehende Plugin-Landschaft einzubetten.

## Fachlicher Kontext
In der Anwendung existieren bereits Plugins fuer andere KI-Integrationen, insbesondere:
- Claude CLI
- GitHub Copilot

Das neue Codex-Plugin soll nach demselben Grundprinzip umgesetzt werden: als anwendungsinternes Plugin fuer Softwareschmiede, nicht als Plugin fuer Codex selbst.

## Fachliche Anforderungen
1. Es soll ein neues Plugin fuer Softwareschmiede erstellt werden, das die Codex-CLI integriert.
2. Das Plugin soll innerhalb der Anwendung installier- oder aktivierbar sein.
3. Das Plugin soll Codex als KI-Funktion aus der Anwendung heraus aufrufbar machen.
4. Das Plugin soll sich an der vorhandenen Plugin-Struktur fuer Claude CLI und GitHub Copilot orientieren.
5. Das Plugin soll sich als eigenstaendige Integration in die bestehende Erweiterungslandschaft einordnen.
6. Die Nutzung von Codex soll innerhalb der Anwendung moeglich sein, ohne dass dafuer ein separates Codex-Plugin ausserhalb von Softwareschmiede geschrieben werden muss.

## Abgrenzung
- Es wird **kein** Plugin fuer die Codex-Plattform oder Codex selbst spezifiziert.
- Es wird **kein** vollstaendiger Ersatz fuer Claude CLI oder GitHub Copilot gefordert.
- Es wird **keine** konkrete UI, API oder technische Implementierung in dieser Anforderung festgelegt.
- Es wird **nicht** angenommen, dass weitere KI-Anbieter unterstuetzt werden muessen.

## Akzeptanzkriterien
- Die Anforderung beschreibt eindeutig ein Plugin fuer **Softwareschmiede**.
- Die Codex-CLI ist als aufrufbare KI-Funktion innerhalb der Anwendung fachlich beschrieben.
- Die Parallele zu den bestehenden Plugins fuer Claude CLI und GitHub Copilot ist nachvollziehbar.
- Die Abgrenzung zu einem Plugin fuer Codex selbst ist eindeutig dokumentiert.
- Die Anforderung ist strukturiert genug, um darauf aufbauend Bestandsaufnahme und Planung zu erstellen.

## Offene Punkte
- Wie genau wird die Codex-CLI in die bestehende Plugin-Architektur von Softwareschmiede eingehangen?
- Welche Bedienung erwartet die Anwendung fuer den Aufruf der Codex-CLI?
- Welche vorhandenen Plugin-Punkte aus den Claude-CLI- und GitHub-Copilot-Integrationen muessen wiederverwendet werden?
- Welche Berechtigungen, Konfigurationen oder Laufzeitvoraussetzungen benoetigt das Plugin?
