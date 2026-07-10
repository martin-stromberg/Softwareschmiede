# Tasks: Programmsymbol

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Assets | Multi-Resolution-`Softwareschmiede.ico` (16/32/64/256, 32-bit RGBA) aus `favicon-hammer-pick.svg` erzeugen und unter `src/Softwareschmiede.App/images/` einchecken | Erledigt | Kein direkter Test (Asset im Repo vorhanden, enthaelt 16/32/48/64/256 @ 32-bit) |
| 2 | Konfiguration | `<ApplicationIcon>images/Softwareschmiede.ico</ApplicationIcon>` in `Softwareschmiede.App.csproj` ergänzen | Erledigt | Kein direkter Test (Property in csproj Zeile 22) |
| 3 | Konfiguration | `<Resource Include="images\Softwareschmiede.ico" />`-Item in `Softwareschmiede.App.csproj` ergänzen (WPF-Pack-Resource für `Window.Icon`) | Erledigt | Kein direkter Test (Resource-Item in csproj Zeile 26) |
| 4 | UI | `Icon="images/Softwareschmiede.ico"` auf `<Window>`-Root in `Views/MainWindow.xaml` setzen | Erledigt | Kein direkter Test (Attribut in MainWindow.xaml Zeile 8) |
| 5 | Verifikation | Build von `Softwareschmiede.App` ausführen und Exe-Icon in Explorer/Taskleiste/Titelleiste visuell prüfen | Offen | Kein direkter Test (manuelle Sichtprüfung, nicht automatisierbar) |
