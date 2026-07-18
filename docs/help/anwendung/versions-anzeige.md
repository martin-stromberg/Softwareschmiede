← [Zurück zur Übersicht](index.md)

# Versionsanzeige — Beschreibung

## Zweck

Die Versionsanzeige präsentiert die aktuell installierte Programmversion dauerhaft in der Fußzeile der Navigations-Seitenleiste. Benutzer können die aktuelle Programmversion auf einen Blick erkennen, ohne auf Einstellungen oder Dialoge zugreifen zu müssen. Dies ist besonders nützlich bei Multi-Fenster-Szenarien oder bei der Kommunikation mit dem Support.

## Funktionsweise

Die Versionsanzeige wird beim Start der Anwendung automatisch geladen:

1. Das Hauptfenster der Anwendung wird angezeigt.
2. Im Hintergrund wird die installierte Programmversion aus der lokalen `version.json`-Datei abgerufen.
3. Die Version wird im Format `Version X.Y.Z` (z. B. „Version 1.2.3") in der Fußzeile der Seitenleiste angezeigt.
4. Ist die Seitenleiste eingeklappt (nur Symbole sichtbar), wird die Versionsanzeige ausgeblendet, um Platz zu sparen.

### Fallback bei Fehler

Wenn die `version.json` nicht vorhanden oder nicht lesbar ist, wird der Fallback-Text `"Version unbekannt"` angezeigt. Dies verhindert, dass ein Versionslade-Fehler die Anwendung blockiert oder zum Absturz führt. Der Fehler wird im Anwendungslog protokolliert.

### Platzierung und Styling

Die Versionsanzeige befindet sich:

- **Vertikal:** In der Fußzeile der Seitenleiste (`Grid.Row="2"`), oberhalb der Update-Buttons („Update" und „Prüfen").
- **Horizontal:** Zentriert im verfügbaren Platz, mit Ausrichtung auf den linken Rand.
- **Optik:** Kleine Schrift (`FontSize="11"`), dezente Farbe (`SecondaryTextBrush`), um sich von den Navigations-Schaltflächen zu unterscheiden.

## Beispiele

### Beispiel 1: Neue Installation
Nach Installation von Softwareschmiede v2.1.0:

1. Starten Sie die Anwendung.
2. Die Seitenleiste zeigt die Navigations-Schaltflächen (Dashboard, Projekte, Einstellungen).
3. In der Fußzeile, über den Update-Buttons, sehen Sie: `Version 2.1.0`

### Beispiel 2: Eingeklappte Navigation
Wenn die Seitenleiste eingeklappt ist (nur Symbole sichtbar):

1. Klicken Sie auf den Hamburger-Button (☰) in der oberen linken Ecke der Seitenleiste.
2. Die Seitenleiste klappt auf nur 48 Pixel Breite zusammen.
3. Die Versionsanzeige wird automatisch ausgeblendet, um Platz zu sparen.
4. Klicken Sie erneut auf (☰), um die Seitenleiste wieder aufzuklappen.
5. Die Versionsanzeige ist wieder sichtbar.

### Beispiel 3: Support-Kommunikation
Wenn Sie den Support kontaktieren:

1. Öffnen Sie Softwareschmiede.
2. Werfen Sie einen Blick auf die Fußzeile der Seitenleiste.
3. Notieren Sie die angezeigte Versionsnummer (z. B. „Version 1.2.3").
4. Geben Sie diese Information im Support-Ticket an, um Kompatibilitätsprobleme schneller zu klären.

## Einschränkungen

- **Nur für aufgeklappte Navigation sichtbar:** Die Versionsanzeige wird ausgeblendet, wenn die Seitenleiste eingeklappt ist. Dies ist beabsichtigt, um die Seitenleiste nicht zu überlasten.
- **Statische Anzeige:** Die Versionsnummer ändert sich erst nach dem Neustart der Anwendung — Live-Updates von laufenden Installationen werden nicht angezeigt.
- **Keine Interaktion:** Ein Klick auf die Versionsanzeige löst keine Aktion aus (z. B. Öffnen von Release-Notes). Die Anzeige ist rein informativ.
- **Abhängigkeit von `version.json`:** Die Versionsanzeige funktioniert nur, wenn die Anwendung eine gültige `version.json`-Datei enthält. Diese wird automatisch beim Build erzeugt.
