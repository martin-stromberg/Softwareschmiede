# Asset-Analyse

## Vorhandene Assets

### `favicon-hammer-pick.svg`
Datei: `src/Softwareschmiede.App/images/favicon-hammer-pick.svg`

| Eigenschaft | Wert |
|-------------|------|
| Format | SVG (Scalable Vector Graphics) |
| ViewBox | 64x64 |
| Geometrie | Hammer- und Spitzhackensymbol (gekreuzt) |
| Hintergrund | Dunkler Kreis (#111827) |
| Icon-Farbe | Gelb/Orange (#f59e0b) |
| Status | Existiert und ist verwendbar |

**Beschreibung:** Die SVG-Datei enthält das Hammer- und Spitzhackensymbol der Softwareschmiede, passend zur Markenidentität. Das Design ist skalierbar und für verschiedene Größen geeignet.

**Verwendungszweck:** Quelle für die ICO-Konvertierung

---

## Fehlende Assets

### `Softwareschmiede.ico` (zu erstellen)
Ziel-Pfad: `src/Softwareschmiede.App/images/Softwareschmiede.ico`

| Eigenschaft | Geplanter Wert |
|-------------|----------------|
| Format | Windows Icon (.ico) |
| Erforderliche Größen | 16x16, 32x32, 64x64, 256x256 Pixel |
| Quelle | Konvertierung von `favicon-hammer-pick.svg` |
| Status | **Nicht vorhanden** — muss erstellt werden |

**Zweck:** Verwendung als ApplicationIcon in der .csproj-Datei für die Windows-Anwendung. Wird vom MSBuild-Prozess automatisch in die `Softwareschmiede.App.exe` eingebettet.

---

## UI-Referenzen

### `MainWindow.xaml`
Datei: `src/Softwareschmiede.App/Views/MainWindow.xaml`

| Element | Aktueller Zustand | Notizen |
|---------|------------------|--------|
| Window-Icon-Attribut | **nicht vorhanden** | Könnte optional hinzugefügt werden (Anforderung Punkt 3) |
| Title | Dynamisch gebunden (`{Binding Title}`) | Wird von ViewModel gesetzt |

**Erkenntnisse:** Das MainWindow hat derzeit kein `Icon`-Attribut. Dies wäre optional, um das Icon auch im Fenster-Titelbereich anzuzeigen. Die Anforderung erwähnt dies als "nächste Schritte", ist aber nicht primär erforderlich.

---

## Verzeichnisstruktur

```
src/Softwareschmiede.App/
├── images/
│   ├── favicon-hammer-pick.svg       [VORHANDEN]
│   └── Softwareschmiede.ico          [FEHLT - zu erstellen]
├── Views/
│   └── MainWindow.xaml               [VORHANDEN - kein Icon-Attribut]
├── app.manifest                       [VORHANDEN]
└── Softwareschmiede.App.csproj        [VORHANDEN - keine ApplicationIcon-Property]
```
