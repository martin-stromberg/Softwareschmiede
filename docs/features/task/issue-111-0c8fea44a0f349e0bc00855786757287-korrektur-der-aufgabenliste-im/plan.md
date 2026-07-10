# Umsetzungsplan: Korrektur der Aufgabenliste im Programmmenue

## Zielbild

Die linke Aufgabenliste zeigt aktive oder wartende Aufgaben stabil sortiert nach dem letzten echten CLI-Start. Die aktuell im Inhaltsbereich angezeigte Aufgabe ist eindeutig visuell markiert. Jedes Aufgabenpanel zeigt neben Titel, Projekt und KI-Ausfuehrungsstatus auch das zugehoerige SCM-/SCI-Plugin und KI-Plugin der Aufgabe.

## Planungsentscheidungen

- "SCI-Plugin" wird als im Code vorhandenes SCM-/Source-Code-Management-Plugin interpretiert.
- Fuer den letzten echten CLI-Start wird ein neues persistiertes nullable Feld `LetzterCliStartUtc` an `Aufgabe` eingefuehrt. `LastHeartbeatUtc` bleibt ausschliesslich Heartbeat-/Laufaktivitaetsdatum.
- Bestehende Aufgaben ohne `LetzterCliStartUtc` bleiben sichtbar. Fuer die Sortierung wird deterministisch auf `ErstellungsDatum`, danach `Titel`, danach `Id` zurueckgefallen.
- In den Aufgabenpanels werden Plugin-Anzeigenamen (`IPlugin.PluginName`) angezeigt. Wenn ein gespeicherter Plugin-Prefix nicht auf ein geladenes Plugin aufgeloest werden kann, wird der gespeicherte Prefix angezeigt.
- Die aktive Kachel wird ueber die Aufgaben-ID der aktuell angezeigten `TaskDetailViewModel` bestimmt, nicht ueber Objektidentitaet.

## Umsetzungsschritte

### 1. Domainmodell und Persistenz erweitern

Betroffene Dateien:

- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/*`

Vorgehen:

1. In `Aufgabe` die Property `public DateTimeOffset? LetzterCliStartUtc { get; set; }` ergaenzen.
2. In `SoftwareschmiededDbContext` das neue Feld analog zu `ErstellungsDatum`, `AbschlussDatum` und `LastHeartbeatUtc` als Unix-Millisekunden konfigurieren, damit SQLite-Sortierung korrekt und stabil bleibt.
3. Eine EF-Core-Migration fuer die neue nullable Spalte `LetzterCliStartUtc` in `Aufgaben` erstellen und den Model-Snapshot aktualisieren.
4. Keine Datenmigration fuer Altbestand erzwingen; null ist bewusst erlaubt und wird im Query-Fallback behandelt.

### 2. Letzten CLI-Start beim echten Prozessstart setzen

Betroffene Dateien:

- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- vorhandene Tests unter `src/Softwareschmiede.Tests/Application/Services/`

Vorgehen:

1. In `AufgabeService.AktivenLaufSetzenAsync` einen gemeinsamen Zeitpunkt `now = DateTimeOffset.UtcNow` verwenden.
2. Bei diesem echten Start-Ereignis sowohl `LastHeartbeatUtc` als auch `LetzterCliStartUtc` auf `now` setzen.
3. `UpdateHeartbeatAsync`, `AktivenLaufBeendenAsync`, `AktualisiereLaufStatusAsync` und reine Navigations-/Ladepfade unveraendert lassen, damit Hintergrundaufgaben beim Anzeigen keinen neuen Startzeitpunkt erhalten.
4. XML-Kommentare in `AktivenLaufSetzenAsync` so anpassen, dass die unterschiedliche Bedeutung von Heartbeat und letztem CLI-Start klar ist.

### 3. Aktive Aufgaben stabil sortieren und Repositorydaten laden

Betroffene Datei:

- `src/Softwareschmiede/Application/Services/AufgabeService.cs`

Vorgehen:

1. `GetAktiveAufgabenAsync` weiterhin auf gestartete oder wartende Aufgaben beschraenken und auf maximal 20 Eintraege limitieren.
2. Zusaetzlich zu `Projekt` auch `GitRepository` laden, weil das SCM-/SCI-Plugin pro Repository in `GitRepository.PluginTyp` gespeichert ist.
3. Sortierung ersetzen durch:

```csharp
.OrderByDescending(a => a.LetzterCliStartUtc ?? a.ErstellungsDatum)
.ThenBy(a => a.Titel)
.ThenBy(a => a.Id)
```

4. Den Methodenkommentar von "letzte Aktivitaet" auf "letzter CLI-Start" korrigieren.

### 4. Sidebar-Item fuer UI-Daten einfuehren

Betroffene Dateien:

- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- neuer Typ unter `src/Softwareschmiede.App/ViewModels/` oder `src/Softwareschmiede.App/Models/`
- Dependency-Injection-Konfiguration, falls fuer Plugin-Aufloesung benoetigt

Vorgehen:

1. Einen schmalen UI-Typ fuer Aufgabenpanels einfuehren, z. B. `AktiveAufgabePanelItem`, mit mindestens:
   - `Guid Id`
   - `string Titel`
   - `string ProjektName`
   - `string? ScmPluginName`
   - `string? KiPluginName`
   - `bool IsAktiv`
   - `DateTimeOffset? LetzterCliStartUtc`
   - Statusdaten, die der bestehende `KiAusfuehrungsStatusConverter` benoetigt, oder alternativ ein bereits berechneter Status-Text
2. `MainWindowViewModel.AktiveAufgabenListe` auf diesen UI-Typ umstellen, sofern keine anderen Verbraucher zwingend rohe `Aufgabe`-Entities benoetigen.
3. Falls der bestehende `KiAusfuehrungsStatusConverter` beibehalten wird, entweder den UI-Typ mit den benoetigten Laufstatusdaten ausstatten oder einen neuen Converter/Status-Text gezielt fuer Sidebar-Items einfuehren.
4. Plugin-Anzeigenamen im ViewModel oder in einem kleinen Mapping-Service aufloesen:
   - SCM: `GitRepository.PluginTyp` gegen geladene SCM-Plugins mappen, Fallback auf Prefix.
   - KI: `Aufgabe.KiPluginPrefix` gegen geladene KI-Plugins mappen, Fallback auf Prefix.
   - Kein globaler Default als scheinbarer Aufgabenwert verwenden, wenn an der Aufgabe kein Prefix gespeichert ist.
5. Die aktive Aufgaben-ID aus `CurrentView` ableiten:
   - Wenn `CurrentView` ein `TaskDetailViewModel` mit gesetzter `AufgabeId` ist, diese ID verwenden.
   - Andernfalls keine Aufgabe als aktiv markieren.
6. Beim Refresh der Liste `IsAktiv` fuer jedes Item anhand der ID setzen, damit `ReplaceAll` keine aktive Markierung ueber Objektidentitaet verlieren kann.
7. Beim Wechsel von `CurrentView` die aktive Markierung aktualisieren. Wenn die Datenliste nicht neu geladen werden muss, reicht ein erneutes Berechnen der Items bzw. ein Property-Update auf den vorhandenen Items.

### 5. ActiveTasksListControl an neue Paneldaten anpassen

Betroffene Dateien:

- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs` nur bei Bedarf
- `src/Softwareschmiede.App/Views/MainWindow.xaml` nur bei geaendertem Typ/Bindingbedarf

Vorgehen:

1. Bindings von `Projekt.Name` auf `ProjektName` anpassen.
2. Zwei kompakte Textzeilen oder eine kompakte Metadatenzeile fuer `ScmPluginName` und `KiPluginName` ergaenzen. Die Texte muessen im bestehenden Seitenleistenlayout mit `TextTrimming="CharacterEllipsis"` stabil bleiben.
3. Die Kachel per Style/DataTrigger auf `IsAktiv` hervorheben:
   - anderer Hintergrund und/oder BorderBrush aus vorhandenen Ressourcen,
   - klare visuelle Abgrenzung ohne Layoutsprung,
   - optional `AutomationProperties.Name` oder `HelpText` um Aktivstatus erweitern.
4. Beide Templates (`AufgabenKachelMitNavigationButtonTemplate` und `AufgabenKachelVollflaechigKlickbarTemplate`) identisch hinsichtlich Aktivmarkierung und Metadaten halten.
5. Navigation weiter ueber `Id` ausloesen.

### 6. CanExecute-Praezedenz beim CLI-Neustart pruefen

Betroffene Datei:

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

Vorgehen:

1. Den Ausdruck `KannCliNeuStarten` fachlich pruefen.
2. Falls die aktuelle Operator-Praezedenz nicht der gewollten Logik entspricht, auf die explizite Form korrigieren:

```csharp
public bool KannCliNeuStarten =>
    (_aufgabe?.Status is Domain.Enums.AufgabeStatus.Gestartet
        or Domain.Enums.AufgabeStatus.Wartend)
    && !_isCliRunning;
```

3. Diese Korrektur nur vornehmen, wenn sie fuer die CLI-Neustartlogik notwendig ist; sie ist ein Risikofund aus der Bestandsaufnahme, aber nicht Kern der Sidebar-Darstellung.

## Tests

### Unit-Tests AufgabeService

Betroffene Dateien:

- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_AktiverLauf.cs`

Testfaelle:

1. `AktivenLaufSetzenAsync` setzt `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc` und `LaufStatus`.
2. `UpdateHeartbeatAsync` aktualisiert `LastHeartbeatUtc`, laesst `LetzterCliStartUtc` aber unveraendert.
3. `GetAktiveAufgabenAsync` sortiert absteigend nach `LetzterCliStartUtc`.
4. Aufgaben ohne `LetzterCliStartUtc` bleiben sichtbar und werden deterministisch ueber `ErstellungsDatum`, `Titel`, `Id` einsortiert.
5. `GetAktiveAufgabenAsync` laedt `Projekt` und `GitRepository`.
6. Bestehende Limitierung auf maximal 20 Eintraege bleibt erhalten.

### Unit-Tests MainWindowViewModel/Sidebar-Items

Betroffene Datei:

- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

Testfaelle:

1. Bei angezeigter `TaskDetailViewModel`-Aufgabe ist genau das passende Sidebar-Item `IsAktiv = true`.
2. Bei Dashboard, Projektliste oder Einstellungen ist kein Sidebar-Item aktiv.
3. Nach Navigation zu einer anderen Aufgabe wechselt die aktive Markierung auf die neue ID.
4. Refresh/ReplaceAll verliert die aktive Markierung nicht.
5. SCM- und KI-Plugin-Anzeigenamen werden pro Aufgabe aufgeloest; bei nicht geladenem Plugin wird der gespeicherte Prefix angezeigt.
6. Fehlender gespeicherter Plugin-Prefix fuehrt nicht zu einem irrefuehrenden globalen Default.

### UI-/E2E-Tests

Betroffene Dateien:

- `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs`
- ggf. ein neuer Control-/E2E-Test fuer `ActiveTasksListControl`

Testfaelle:

1. Zwei aktive Aufgaben werden in der Seitenleiste angezeigt; beim Oeffnen von Aufgabe A ist nur A visuell/automationstechnisch aktiv.
2. Nach Wechsel zu Aufgabe B ist nur B aktiv.
3. Plugintexte fuer SCM-/SCI- und KI-Plugin erscheinen in jeder Kachel.
4. Das Wiederanzeigen einer bereits laufenden Hintergrundaufgabe veraendert `LetzterCliStartUtc` und die Reihenfolge nicht.

## Manuelle Verifikation

1. Anwendung starten und mindestens zwei Aufgaben mit unterschiedlichen CLI-Starts erzeugen.
2. Pruefen, dass die zuletzt gestartete CLI oben steht.
3. Zwischen laufenden Hintergrundaufgaben wechseln und sicherstellen, dass die Reihenfolge stabil bleibt.
4. Pruefen, dass genau die im Inhaltsbereich sichtbare Aufgabe hervorgehoben ist.
5. Pruefen, dass SCM-/SCI- und KI-Plugin-Namen pro Kachel korrekt und nicht abgeschnitten ueberlappend angezeigt werden.

## Risiken und Gegenmassnahmen

- Risiko: `LastHeartbeatUtc` wird versehentlich weiter fuer Sortierung verwendet. Gegenmassnahme: gezielter Unit-Test fuer Heartbeat ohne Sortieraenderung.
- Risiko: Plugin-Aufloesung verwendet globale Defaults und zeigt dadurch gleiche Namen fuer alle Aufgaben. Gegenmassnahme: Tests mit unterschiedlichen gespeicherten Prefixen und einem nicht geladenen Prefix.
- Risiko: Aktive Markierung geht bei Listen-Refresh verloren. Gegenmassnahme: Aktivstatus ausschliesslich aus aktueller Aufgaben-ID ableiten.
- Risiko: Neue DateTimeOffset-Spalte sortiert in SQLite lexikografisch oder inkonsistent. Gegenmassnahme: gleiche Unix-Millisekunden-Konvertierung wie bestehende Datumsfelder verwenden.
- Risiko: Die neue Kachelhoehe verschlechtert die Seitenleistenuebersicht. Gegenmassnahme: kompakte Textzeilen, Ellipsis und keine layoutveraendernde Aktivmarkierung.

## Nicht-Ziele

- Keine neue manuelle Sortierfunktion.
- Keine fachliche Aenderung der Aufgabeninhalte.
- Keine Aenderung daran, wie bestehende Hintergrundsessions geoeffnet werden.
- Keine Rueckbefuellung historischer "Letzter Start"-Werte fuer Altbestand.

## Offene Punkte

Keine.
