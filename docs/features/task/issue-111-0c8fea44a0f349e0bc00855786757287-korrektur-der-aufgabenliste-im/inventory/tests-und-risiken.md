# Tests und Risiken

## Vorhandene Testabdeckung

Aktive Aufgabenliste und Navigation:

- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- Deckt Befuellung, gemeinsame Dashboard-Liste, Fire-and-Forget-Refresh und Navigation zu `TaskDetailViewModel` ab.

CLI-Laufpersistenz:

- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_AktiverLauf.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_AktiverLauf.cs`

Statusanzeige der Kachel:

- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`

E2E-Seitenleistenwechsel:

- `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs`
- Deckt den Wechsel zwischen Aufgaben ueber die Seitenleiste und die korrekte Terminal-Session ab.

## Fehlende Testfaelle fuer diese Anforderung

- `AufgabeService.GetAktiveAufgabenAsync` sortiert nach neuem "Letzter Start"-Zeitstempel absteigend.
- Aufgaben ohne "Letzter Start" bleiben sichtbar und werden deterministisch sortiert.
- `AktivenLaufSetzenAsync` setzt den neuen Startzeitstempel bei echtem CLI-Start.
- `UpdateHeartbeatAsync` veraendert den neuen Startzeitstempel nicht.
- Oeffnen/Navigieren zu bereits laufender Hintergrundaufgabe veraendert den neuen Startzeitstempel nicht.
- Seitenleisten-ViewModel/DTO loest SCM- und KI-Plugin-Anzeigenamen pro Aufgabe korrekt auf.
- `ActiveTasksListControl` markiert genau die aktuell angezeigte Aufgabe aktiv.
- Wenn keine `TaskDetailViewModel`-Aufgabe im Inhaltsbereich aktiv ist, ist keine Kachel aktiv.

## Regressionrisiken

- Wenn fuer die Sortierung weiter `LastHeartbeatUtc` genutzt wird, bleibt die Hauptursache der instabilen Liste bestehen.
- Wenn die Plugin-Anzeige aus Defaults statt gespeicherten Aufgaben-/Repository-Werten aufgebaut wird, koennen alle Panels denselben Plugin-Namen anzeigen.
- Wenn aktive Markierung ueber Objektidentitaet statt `Aufgabe.Id` erfolgt, kann sie durch `ReplaceAll`/Refresh verloren gehen oder falsch sein.
- Wenn Startzeit direkt beim Anzeigen einer `TaskDetailViewModel` gesetzt wird, verletzt das die Anforderung fuer Hintergrundaufgaben.
- Eine neue Migration muss mit bestehenden SQLite-Konvertierungen konsistent sein; `DateTimeOffset` sollte wie bestehende Datumsfelder als Unix-Millisekunden persistiert werden.

## Empfohlene Verifikationsstrategie

- Unit-Tests fuer `AufgabeService` erweitern: Startzeit setzen, Heartbeat laesst sie unveraendert, Sortierung inklusive Fallback.
- Unit-Tests fuer `MainWindowViewModel` oder neues Sidebar-Item-ViewModel: aktive Aufgabe aus `CurrentView` ableiten und bei Navigation/Refresh stabil halten.
- UI-/Control-Test oder WPF-E2E fuer aktive Kachel: Aufgabe A und B aktiv, A im Inhalt sichtbar, nur A markiert; nach Wechsel nur B markiert.
- E2E-Test erweitern oder neu anlegen: zwei Aufgaben mit unterschiedlichen CLI-Startzeiten, Hintergrundwechsel darf Reihenfolge nicht aendern.

