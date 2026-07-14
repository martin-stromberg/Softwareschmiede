← [Zurück zur Übersicht](index.md)

# Dateiexplorer — Beschreibung

## Zweck

Nachdem ein Task-Repository geklont wurde, zeigt der Dateiexplorer die lokale Dateistruktur in einer übersichtlichen Split-View an. Dies ermöglicht es, schnell Dateien zu lokalisieren und deren Inhalt bzw. Änderungen zu inspizieren — ohne ein externes Tool öffnen zu müssen.

## Funktionsweise

Der Explorer präsentiert sich in zwei Grundmodi:

### Standardmodus
Der **Standardmodus** ist beim Öffnen des Registers die Standardeinstellung. Er zeigt den **kompletten Arbeitsbaum** des geklonten Repositories:
- Links eine hierarchische Verzeichnis-/Dateistruktur (TreeView)
- Das `.git`-Verzeichnis wird aus der Anzeige ausgeschlossen
- Rechts wird der Inhalt der ausgewählten Datei als Klartext (Read-Only) angezeigt
- Bei sehr großen Dateien (> 1 MB) oder Binärdateien erscheint statt des Inhalts ein Hinweis

### Vergleichsmodus
Der **Vergleichsmodus** blendet aus, was sich nicht verändert hat. Er zeigt:
- Nur Commits aus dem aktuellen Branch (seit der Basis-Referenz, z. B. `origin/main`)
- Jeder Commit wird als aufklappbarer Knoten dargestellt; die Kinder-Dateien laden sich erst beim Aufklappen
- Pro geänderter Datei wird ein farblich hervorgehobenes **Diff** angezeigt:
  - **Grün** = neue Zeile
  - **Rot** = gelöschte Zeile
  - **Orange** = geänderte Zeile mit Inline-Highlighting der Wortabschnitte, die sich unterscheiden

## Beispiele

**Standardmodus — Datei inspizieren:**
1. Klick auf „Dateien"-Register in der Aufgabendetailansicht
2. Im Baum links navigieren: z. B. `src` → `Softwareschmiede.App` → `Views` → `TaskDetailView.xaml`
3. Rechts wird der XAML-Code angezeigt

**Vergleichsmodus — Änderungen überprüfen:**
1. Klick auf Button „≍ Vergleich" oberhalb des Baums
2. Commits werden als Wurzelknoten aufgelistet, z. B. `feat: Dateiexplorer hinzufügen (a1b2c3d)`
3. Klick auf ▶, um die Dateien dieses Commits einzusehen
4. Auswahl einer Datei zeigt rechts das Diff mit grünen Hinzufügungen und roten Löschungen

## Einschränkungen

- Das `.git`-Verzeichnis wird nicht angezeigt, um die Baumgröße und Ladezeit überschaubar zu halten
- Sehr große Repositories (mit tausenden Dateien) können mehrere Sekunden zum Laden benötigen
- Binärdateien und Dateien über 1 MB werden nicht vollständig angezeigt, sondern mit Hinweis gekennzeichnet
- Das Inline-Highlighting bei Modified-Zeilen im Diff arbeitet auf Basis des gemeinsamen Präfix/Suffix (Wortabschnitte), nicht auf Token-Ebene
- Die Benutzerauswahl zwischen Standard- und Vergleichsmodus wird nicht persistent gespeichert — bei erneutem Öffnen der Aufgabe startet der Explorer immer im Standardmodus
