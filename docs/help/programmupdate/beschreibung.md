← [Zurück zur Übersicht](index.md)

# Programmupdate — Beschreibung

## Zweck

Das Programmupdate-Feature ermöglicht dem Anwender, die Softwareschmiede-Anwendung aus dem Menü heraus zu aktualisieren, ohne die Anwendung manuell zu schließen und neue Dateien herunterzuladen. Der Update-Prozess lädt die neueste Version herunter, entpackt die Dateien und bereitet das Update vor. Ein Fortschrittsdialog zeigt den Fortschritt der Vorbereitung an.

## Funktionsweise

Der Update-Prozess läuft in mehreren Phasen ab:

1. **Download**: Die neueste Version wird vom Update-Server heruntergeladen.
2. **Entpacken**: Die heruntergeladenen Dateien werden in ein temporäres Verzeichnis entpackt.
3. **Update-Vorbereitung**: Die neue Version wird konfiguriert und validiert.

Während des gesamten Prozesses wird der Benutzer durch einen modalen Fortschrittsdialog informiert, der folgende Informationen anzeigt:

- **Aktuelle Phase**: Der Name der gerade ausführenden Phase (z. B. "Download", "Entpacken").
- **Fortschrittstext**: Eine aussagekräftige Meldung über den aktuellen Zustand (z. B. "Datei 47 von 128 wird heruntergeladen").
- **Fortschrittsbalken**: Ein visueller Indikator des prozentualen Fortschritts (0–100 %) oder unbestimmter Fortschrittsanzeige.
- **Fehleranzeige**: Falls ein Fehler auftritt, wird die Fehlermeldung angezeigt und der Dialog kann vom Benutzer geschlossen werden.

Der Benutzer kann den Update-Prozess während der Vorbereitung durch einen „Abbrechen"-Button unterbrechen. Nach erfolgreicher Vorbereitung startet ein externes Update-Skript und die Anwendung wird beendet.

## Benutzer-sichtbare Komponenten

### Update-Fortschrittsdialog

Der Dialog ist modal und wird über dem Hauptfenster angezeigt. Die folgenden Elemente sind enthalten:

- **Titel**: "Update-Vorbereitung"
- **Phase-Anzeige**: Zeigt die aktuelle Phase als Label an
- **Fortschritts-ProgressBar**: Eine horizontale Fortschrittsleiste mit Prozentwert (z. B. "47 %" oder unbestimmte Animation)
- **Meldungs-TextBlock**: Zeigt die aktuelle Fortschrittsmeldung an
- **Abbrechen-Button**: Erlaubt dem Benutzer, die Vorbereitung zu unterbrechen (deaktiviert nach erfolgreicher Vorbereitung)
- **Schließen-Button**: Erlaubt dem Benutzer, den Dialog zu schließen (nur bei Fehler oder nach Abschluss aktiviert)

### Fehlerzustand

Bei einem Fehler wird die Fehlermeldung prominent angezeigt, der Abbrechen-Button wird deaktiviert und der Schließen-Button wird aktiviert, damit der Benutzer den Dialog schließen kann.

## Einschränkungen

- Das Update wird nur vorbereitet — die tatsächliche Installation erfolgt durch ein externes Skript nach Beendigung der Anwendung.
- Der Update-Prozess kann nicht pausiert werden, nur unterbrochen.
- Während der Update-Vorbereitung können keine anderen Operationen in der Anwendung ausgeführt werden (modaler Dialog).
- Falls das externe Update-Skript fehlschlägt, wird die Anwendung möglicherweise in einem korrupierten Zustand hinterlassen. Dies ist außerhalb des Scope des Features.
