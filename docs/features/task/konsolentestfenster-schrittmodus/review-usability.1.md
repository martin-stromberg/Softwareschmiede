# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### KonsolenTestDialog.xaml (Werkzeugleiste)

- **Erreichbarkeit** — Die Werkzeugleiste ist eine horizontale `StackPanel` ohne Umbruch oder Scrollen (`KonsolenTestDialog.xaml:24`). Durch die zwei neuen Schaltflächen „Schritt zurück"/„Schritt vor" (`KonsolenTestDialog.xaml:54-65`, zusammen ca. 190 px inkl. Abständen) übersteigt die benötigte Breite die Standard-Fensterbreite von 1200 px deutlich: rechnerisch ca. 1450 px Bedarf (Buttons ~FontSize 12–14 mit Padding 10,4, Pfad-TextBlock bis `MaxWidth="320"`, Zeitraffer-Label/TextBox, Status-/Positionstext, „Schließen") gegenüber ca. 1184 px nutzbarer Breite. Bereits vor der Änderung war die Leiste knapp zu breit (~1260 px); die neuen Buttons schieben nun den von der Anforderung geforderten `PositionsText` („Chunk x/y") sowie die Schaltfläche „Schließen" vollständig aus den sichtbaren Bereich — beide liegen am rechten Rand der Leiste (`KonsolenTestDialog.xaml:82-95`). Eine technisch affine Anwenderin sieht nach dem Laden und Schreiten bei Standardbreite weder die Schrittposition noch den Schließen-Button, ohne das Fenster manuell zu verbreitern. Das betrifft direkt die geforderte Interaktion „PositionsText spiegelt die Schrittposition". Hinweis: E2E-Tests bleiben davon unberührt, weil UI-Automation abgeschnittene Elemente weiterhin im Baum liest.

  Empfehlung: Werkzeugleiste umbrechen lassen (z. B. `WrapPanel` oder zweite Werkzeugzeile bzw. Status-/Positionsanzeige in eine eigene Zeile unter der Leiste), alternativ `MaxWidth` des Pfad-TextBlocks reduzieren und/oder `MinWidth` des Fensters auf die tatsächlich benötigte Breite setzen, damit alle geforderten Bedienelemente bei Standardbreite sichtbar bleiben.

### KonsolenTestViewModel.cs (Schrittmodus ohne gestartete Wiedergabe)

- **Erreichbarkeit** — Nach dem typischen Diagnose-Ablauf „Aufzeichnung laden → einige Male „Schritt vor"" steht die Position mitten in der Aufzeichnung, ohne dass je „Abspielen" gedrückt wurde. In diesem Zustand ist „Neu starten" deaktiviert (`KonsolenTestViewModel.cs:58`: `CanExecute = _replaySession is not null && IstWiedergabeAktiv`), und „Abspielen" setzt bewusst an der Schrittposition fort statt neu zu starten (`WiedergabeStarten`, `KonsolenTestViewModel.cs:300-315`, `_wiedergabeBeendet` ist false). Es gibt damit keinen direkten UI-Pfad, um zur Position 0 zurückzukehren bzw. die zeitgesteuerte Wiedergabe von vorn zu starten: Die Anwenderin muss entweder „Schritt zurück" so oft klicken, wie sie zuvor vorgeschritten ist (bei langen Mitschnitten dutzende Klicks), oder die Datei über „Aufzeichnung öffnen…" erneut laden. Das ist für das geforderte Vergleichen von Zuständen vor/nach einem Chunk unzumutbar umständlich.

  Empfehlung: „Neu starten"-CanExecute um den Schrittmodus-Zustand erweitern, z. B. `IstWiedergabeAktiv || AktuellerChunkIndex > 0` (die vorhandene Implementierung `ErsetzeReplaySessionDurchFrische` deckt den Reset aus jeder Position bereits ab), oder einen eigenen „Zum Anfang"-Reset anbieten.

### KonsolenTestViewModel.cs / KonsolenTestDialog.xaml (Status- und Nummerierungsbeschriftung, geringfügig)

- **Erreichbarkeit** — Drei kleine Beschriftungs-Unschärfen rund um die geforderte Erkennbarkeit „welcher Chunk zuletzt angewendet wurde": (a) Nach einem *Rückwärtsschritt* zeigt `StatusText` denselben Wortlaut „Einzelschritt — Chunk n/y angewendet." wie nach einem Vorwärtsschritt (`KonsolenTestViewModel.cs:370-379`) — der Text liest sich wie eine Vorwärts-Anwendung, obwohl gerade ein Chunk zurückgenommen wurde; an Position 0 steht „Chunk 0/y angewendet", obwohl kein Chunk angewendet ist. (b) Die Nummerierung ist inkonsistent: `PositionsText` zählt angewendete Chunks 1-basiert („Chunk 2/3"), die Quell-Chunk-Liste nummeriert ihre Zeilen dagegen 0-basiert in der Spalte „#" (`KonsolenTestDialog.xaml:157-159`, `Index = i` in `ErzeugeQuellEintraege`, `KonsolenTestViewModel.cs:260`); der nach „Chunk 2/3" markierte Eintrag trägt die Nummer „1". Für den Diagnose-Zweck (Zustand vor/nach Chunk X vergleichen) muss die Anwenderin den Versatz jeweils mental umrechnen.

  Empfehlung: Statustext des Rückwärtsschritts klar als Rücknahme formulieren (z. B. „Schritt zurück — Zustand vor Chunk n+1 wiederhergestellt, Position n/y") und die Nummerierungsbasis angleichen (entweder `#`-Spalte 1-basiert oder PositionsText explizit als „angewendete Chunks" kennzeichnen), damit Status, Position und markierter Listeneintrag ohne Umrechnung zusammenpassen.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- `.clireplay`-Aufzeichnung über Datei-Dialog laden („Aufzeichnung öffnen…") → unauffällig (Standard-Dialog, keine interne Kennung nötig)
- Einzelschritt vorwärts (zeitstempel-unabhängig, Chunk anwenden) → unauffällig (beschrifteter Button + ToolTip, korrekte Aktivierung/Deaktivierung an Positionsgrenzen und bei laufender Wiedergabe)
- Einzelschritt rückwärts (Zustand vor zuletzt angewendetem Chunk wiederherstellen) → unauffällig (Button bei Position 0 deaktiviert, Selektion wird korrekt geleert)
- Fortsetzen nach pausiert → Schritte an der geänderten Position → unauffällig (Schritt-Buttons im Pausiert-Zustand aktiv; während unpausierter Wiedergabe deaktiviert — Zustand ist über „Pausieren/Fortsetzen" steuerbar)
- Erkennbarkeit Position/zuletzt angewendeter Chunk (`PositionsText`, Quell-Selektion + ScrollIntoView, `StatusText`) → Befund vorhanden (Toolbar-Überlauf verbirgt PositionsText bei Standardbreite; Statustext/Nummerierung siehe dritter Befund)
- Rückkehr zum Anfang / Neustart der zeitgesteuerten Wiedergabe aus dem Schrittmodus → Befund vorhanden (kein direkter Pfad; „Neu starten" gesperrt, „Abspielen" setzt an Position fort)
- Eingabe der Zeitraffer-Schwelle / sonstige Bestandsbedienung → unauffällig (unverändert)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` (nur Bedienverhalten: Guards, Rückwärts-Rebuild, Ende-/Re-Arm-Semantik)
