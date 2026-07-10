# Umsetzungsplan: Programmsymbol

## Übersicht

Die Anwendung `Softwareschmiede.App.exe` erhält ein Executable-Icon (Hammer-/Spitzhacken-Symbol), das im Windows-Explorer, in der Taskleiste und in der Fenster-Titelleiste erscheint. Dazu wird aus der bereits vorhandenen SVG-Grafik `favicon-hammer-pick.svg` eine Multi-Resolution-`.ico`-Datei erzeugt, ins Repo eingecheckt, über die MSBuild-Property `<ApplicationIcon>` in `Softwareschmiede.App.csproj` eingebunden und zusätzlich per `Window.Icon` explizit in `MainWindow.xaml` referenziert. Es sind keine C#-, Datenmodell- oder Datenbankänderungen erforderlich — lediglich Asset-, Projekt- und XAML-Markup-Anpassungen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| SVG→ICO-Konvertierung | Einmalige Offline-Konvertierung, fertige `.ico` als Asset ins Repo einchecken (kein Build-Schritt) | Der CI-/Build-Prozess bleibt frei von zusätzlichen Tooling-Abhängigkeiten (kein SVG-Renderer im Build). Das `.ico` ist ein deterministisches, versioniertes Asset. |
| Icon-Inhalt | 1:1-Übernahme der bestehenden SVG-Geometrie (dunkler Kreis + gelb/oranges gekreuztes Hammer-/Spitzhacken-Symbol), Rasterung in mehreren Auflösungen | Wahrt die Markenidentität; keine gestalterische Neuinterpretation gefordert. Mehrere Auflösungen sichern saubere Darstellung von 16×16 bis 256×256. |
| Eingebettete Auflösungen | 16×16, 32×32, 64×64, 256×256 (jeweils 32-bit RGBA mit Alpha) | Deckt Explorer-Kleinsymbole, Taskleiste, große Kachel- und Detailansichten ab. Entspricht der Vorgabe aus Anforderung und Bestandsaufnahme. |
| Taskleisten-Symbol zur Laufzeit | Über `<ApplicationIcon>` eingebettetes Exe-Icon | WPF verwendet für Fenster ohne gesetztes `Icon`-Attribut automatisch das Exe-Icon als Fallback — die Taskleisten-Anforderung ist damit ohne zusätzliche Resource-Einbettung erfüllt. |
| Titelleisten-Symbol (`Window.Icon`) | Zusätzlich explizites `Icon="images/Softwareschmiede.ico"` auf `MainWindow` setzen; dazu `Softwareschmiede.ico` als `<Resource>`-Item in die `.csproj` aufnehmen | Anwenderentscheidung (siehe Offene Punkte der Vorversion): Das Titelleisten-Symbol soll garantiert und nicht nur über den impliziten Exe-Fallback gesetzt sein. Ein `<Resource>`-Item macht die `.ico` zusätzlich zur `<ApplicationIcon>`-Einbettung als WPF-Pack-Resource verfügbar, damit die relative XAML-URI auflösbar ist. |

## Programmabläufe

### Icon-Einbettung beim Build (MSBuild)

1. MSBuild liest die Property `<ApplicationIcon>` aus `Softwareschmiede.App.csproj`.
2. Beim Kompilieren des `WinExe`-Targets bettet MSBuild die referenzierte `.ico`-Datei als Win32-Ressource in `Softwareschmiede.App.exe` ein.
3. Windows-Explorer und Taskleiste lesen diese eingebettete Ressource und zeigen das Symbol an.

Beteiligte Klassen/Komponenten: `Softwareschmiede.App.csproj` (Build-Konfiguration), `images/Softwareschmiede.ico` (Asset). Keine Laufzeit-C#-Klassen beteiligt.

## Neue Klassen

Keine. Es werden keine Klassen, Enums oder Interfaces angelegt (reine Asset-/Konfigurationsaufgabe).

## Änderungen an bestehenden Klassen

Keine C#-Klassenänderungen. Die einzige Konfigurationsänderung betrifft die Projektdatei:

### `Softwareschmiede.App.csproj` (MSBuild-Projektdatei)

- **Neue Property:** `<ApplicationIcon>images/Softwareschmiede.ico</ApplicationIcon>` in der bestehenden `<PropertyGroup>` — bindet das Icon in die Exe ein.
- **Neues Item:** `<Resource Include="images\Softwareschmiede.ico" />` in einer `<ItemGroup>` — macht die `.ico` zusätzlich als WPF-Pack-Resource verfügbar, damit `Window.Icon` sie per relativer URI laden kann.

### `Views/MainWindow.xaml`

- **Neues Attribut:** `Icon="images/Softwareschmiede.ico"` auf dem `<Window>`-Root-Element — setzt das Titelleisten-Symbol explizit, unabhängig vom impliziten Exe-Icon-Fallback.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `<ApplicationIcon>` | MSBuild-Property in `Softwareschmiede.App.csproj` | `images/Softwareschmiede.ico` | Bindet das Executable-Icon beim Build in `Softwareschmiede.App.exe` ein |

Hinweis: Keine `appsettings`-/Laufzeitkonfiguration betroffen. Das Feature ist bewusst nicht zur Laufzeit konfigurierbar.

## Seiteneffekte und Risiken

- **Build-Datei-Lock (Self-Hosting-Risiko):** Ein Rebuild von `Softwareschmiede.App` kann an einem Datei-Lock durch eine laufende `Softwareschmiede.App.exe` scheitern (MSB3027/MSB3026). Gemäß CLAUDE.md darf keine `Softwareschmiede.App.exe`-Instanz eigenständig beendet werden — bei Lock ist der Anwender zu bitten, die blockierende Instanz selbst zu schließen.
- **Icon-Caching in Windows:** Der Explorer-Icon-Cache kann ein altes/leeres Symbol weiter anzeigen, obwohl das Icon korrekt eingebettet ist. Verifikation ggf. an einer frisch kopierten Exe oder nach Cache-Refresh vornehmen — kein Codeproblem.
- **Asset im Projektglob:** Die `.ico` liegt unter `images/`. Sicherstellen, dass sie nicht versehentlich als WPF-`Resource`/`Content` mit doppeltem Build-Verhalten eingebunden wird; `<ApplicationIcon>` genügt für die Einbettung.
- Keine weiteren bekannten Seiteneffekte: keine bestehenden Klassen, Abläufe oder Tests hängen vom Fehlen eines Exe-Icons ab.

## Umsetzungsreihenfolge

1. **Multi-Resolution-`.ico` erzeugen und einchecken**
   - Voraussetzungen: Vorhandene Quelle `src/Softwareschmiede.App/images/favicon-hammer-pick.svg`.
   - Beschreibung: Die SVG offline nach `src/Softwareschmiede.App/images/Softwareschmiede.ico` konvertieren mit eingebetteten Auflösungen 16×16, 32×32, 64×64, 256×256 (32-bit RGBA). Datei ins Repo aufnehmen.

2. **`<ApplicationIcon>`-Property setzen**
   - Voraussetzungen: `Softwareschmiede.ico` aus Schritt 1 vorhanden.
   - Beschreibung: In `src/Softwareschmiede.App/Softwareschmiede.App.csproj` innerhalb der bestehenden `<PropertyGroup>` die Zeile `<ApplicationIcon>images/Softwareschmiede.ico</ApplicationIcon>` ergänzen.

3. **`Window.Icon` in `MainWindow.xaml` setzen**
   - Voraussetzungen: `Softwareschmiede.ico` aus Schritt 1 vorhanden.
   - Beschreibung: In `Softwareschmiede.App.csproj` ein `<Resource Include="images\Softwareschmiede.ico" />`-Item ergänzen; in `Views/MainWindow.xaml` auf dem `<Window>`-Root-Element `Icon="images/Softwareschmiede.ico"` setzen.

4. **Build und manuelle Verifikation**
   - Voraussetzungen: Schritte 1–3 abgeschlossen; keine blockierende `Softwareschmiede.App.exe`-Instanz (sonst Anwender um Schließen bitten).
   - Beschreibung: `dotnet build src/Softwareschmiede.App/Softwareschmiede.App.csproj` ausführen. Anschließend prüfen, dass die erzeugte `Softwareschmiede.App.exe` im Explorer das Hammer-Symbol trägt, die laufende App das Symbol in der Taskleiste zeigt und die Fenster-Titelleiste das Symbol anzeigt.

## Tests

### Neue Tests

Keine automatisierten Tests. Das eingebettete Exe-Icon ist eine Build-Artefakt-Eigenschaft ohne Laufzeitlogik und ohne fachlich testbares Verhalten; es existiert keine sinnvolle Unit-Test-Oberfläche. Die Verifikation erfolgt über einen erfolgreichen Build und visuelle Kontrolle (siehe Umsetzungsreihenfolge Schritt 3).

### Betroffene bestehende Tests

Keine. Es ändern sich keine Signaturen, Datenstrukturen oder Verhaltensweisen; bestehende Unit- und Integrationstests bleiben unberührt.

### E2E-Tests (Pflicht)

Es gibt keine neue oder geänderte Benutzerinteraktion innerhalb der Anwendung — das Feature betrifft ausschließlich die Symboldarstellung des Betriebssystems (Explorer/Taskleiste) und ist über WPF-/UI-Automation nicht sinnvoll prüfbar.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| — (kein automatisierter E2E-Test; manuelle visuelle Verifikation von Explorer- und Taskleistensymbol) | — | Exe zeigt Hammer-Symbol in Explorer und Taskleiste |

Bestehende E2E-Tests betroffen: Keine.

## Offene Punkte

Keine. Die Frage nach einem expliziten `Window.Icon` wurde vom Anwender entschieden: Zusätzlich zum eingebetteten Exe-Icon wird `MainWindow.xaml` ein explizites `Icon`-Attribut erhalten (siehe Designentscheidungen und Umsetzungsreihenfolge Schritt 3).
