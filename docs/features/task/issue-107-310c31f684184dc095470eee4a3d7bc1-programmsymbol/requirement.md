# Anforderungsübersetzung: Programmsymbol

## Fachliche Zusammenfassung

Das Softwareschmiede-Programm erhält ein Executable-Icon, das im Windows-Explorer und in der Taskleiste angezeigt wird. Als Symbol wird das Hammersymbol (🔨) verwendet, das bereits als SVG-Grafik in der Codebasis vorhanden ist und die visuelle Identität des Projekts unterstreicht. Das Icon wird durch eine Property in der Projekt-Konfiguration (`Softwareschmiede.App.csproj`) eingebunden und automatisch beim Build-Prozess in die ausführbare Datei integriert.

## Betroffene Klassen und Komponenten

**Konfiguration & Build:**
- `src/Softwareschmiede.App/Softwareschmiede.App.csproj`
  - Neue Property: `<ApplicationIcon>` mit Verweis auf die Icon-Datei

**Assets:**
- `src/Softwareschmiede.App/images/favicon-hammer-pick.svg` (bereits vorhanden)
  - Quelle für die Icon-Grafik; ggf. Konvertierung oder Anpassung erforderlich
- `src/Softwareschmiede.App/images/Softwareschmiede.ico` (zu erstellen)
  - Windows-Standard-Icon-Datei im `.ico`-Format, mehrfache Auflösungen (16x16, 32x32, 64x64, 256x256)

**Sonstige:**
- Keine Code-Änderungen erforderlich (reine Konfiguration)
- Keine neuen Klassen, Interfaces oder Service-Registrierungen

## Implementierungsansatz

1. **Icon-Erstellung:**
   - Konvertiere das bestehende SVG-Icon (`favicon-hammer-pick.svg`) in das Windows `.ico`-Format
   - Stelle sicher, dass mehrere Auflösungen in die `.ico`-Datei eingebettet werden (z. B. 16x16, 32x32, 64x64, 256x256), um optimale Darstellung in verschiedenen Kontexten zu gewährleisten
   - Speichere die Icon-Datei unter `src/Softwareschmiede.App/images/Softwareschmiede.ico`

2. **Projekt-Konfiguration:**
   - Füge die Property `<ApplicationIcon>images/Softwareschmiede.ico</ApplicationIcon>` in der `<PropertyGroup>` der Datei `Softwareschmiede.App.csproj` ein
   - Dies bindet das Icon automatisch beim MSBuild-Prozess in die `Softwareschmiede.App.exe` ein

3. **Verifikation:**
   - Baue das Projekt neu (`dotnet build`)
   - Überprüfe, dass die `Softwareschmiede.App.exe` im Ausgabeverzeichnis das korrekte Icon anzeigt
   - Teste die Darstellung im Windows-Explorer und in der Taskleiste

## Konfiguration

Das Feature ist nicht konfigurierbar — die Icon-Datei und die MSBuild-Property sind fest in das Projekt eingebettet. Das verwendete Symbol (Hammersymbol) ist durch diese Anforderung explizit festgelegt.

## Offene Fragen

1. **Icon-Konvertierungstool:** Welches Tool/Verfahren wird für die SVG-zu-ICO-Konvertierung bevorzugt?
   - Automatisierung im Build-Prozess oder manuelle Erstellung möglich?

2. **Icon-Feinschliff:** Soll das bestehende SVG-Icon 1:1 in das `.ico`-Format konvertiert werden, oder sind Anpassungen für bessere Lesbarkeit in kleinen Größen (z. B. 16x16) gewünscht?

3. **Nächste Schritte:** Soll nach Icon-Implementierung zusätzlich das Anwendungsfenster (MainWindow) einen Icon-Verweis erhalten (`Icon=` Property in XAML)?
   - Derzeit ist kein Window-Icon in `MainWindow.xaml` gesetzt.
