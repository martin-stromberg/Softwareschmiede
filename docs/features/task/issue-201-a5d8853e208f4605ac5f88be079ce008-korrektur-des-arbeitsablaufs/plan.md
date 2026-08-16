# Umsetzungsplan: Korrektur des Arbeitsablaufs

## Ziel und Leitlinien

Die Aufgabenseite bleibt nach dem Speichern einer neuen Aufgabe geöffnet. Der Lebenszyklus der KI-Ausführung wird dauerhaft und unabhängig vom Gesamtstatus der Aufgabe gespeichert. Ein Stoppen oder natürliches Ende der KI-Ausführung beendet nur die Ausführung; erst die Aktion „Beenden“ setzt den Gesamtstatus auf `Beendet` und löscht das lokale Repository.

Die bestehende Trennung zwischen Gesamtstatus (`Aufgabe.Status`) und dem kurzfristigen Laufzeitwert (`LaufStatus`) bleibt erhalten. `LaufStatus` beschreibt weiterhin nur den beobachteten Zustand einer aktiven CLI, während ein neuer persistenter Ausführungsstatus den Lebenszyklus abbildet.

## Annahmen

- Der neue persistente Status heißt fachlich `AufgabeAusfuehrungsStatus` und hat mindestens `NichtGestartet`, `Aktiv` und `Beendet`. Die konkrete lokale Benennung darf an die vorhandenen Enum-Konventionen angepasst werden.
- „Stoppen“ und ein erfolgreich erkanntes natürliches CLI-Prozessende setzen den Ausführungsstatus auf `Beendet`. Ein zusätzlicher Status wie „Pausiert“ oder „Abgebrochen“ ist für diese Anforderung nicht erforderlich.
- Beim expliziten Start einer bereits vorbereiteten Aufgabe wird nur die CLI neu gestartet. Repository-Klon, Branch und Startskript werden nur beim Erststart vorbereitet.
- Für bestehende Datensätze ohne neue Spalte gilt als konservatives Backfill: `Neu` wird `NichtGestartet`; `Gestartet`/`Wartend` mit nicht leerer `AktiveRunId` wird `Aktiv`; `Gestartet`/`Wartend` ohne `AktiveRunId` wird `Beendet`; `Beendet`/`Archiviert` wird `Beendet`. Dadurch wird eine historisch bereits beendete CLI nicht unbeabsichtigt automatisch erneut gestartet.
- Eine aktive Ausführung wird beim erneuten Laden nur wieder verbunden, wenn der persistierte Status `Aktiv` ist und eine laufende bzw. wiederherstellbare CLI-Session oder ein gültiger Recovery-Fall vorliegt. Kann die Session nicht wiederhergestellt werden, bleibt die Aufgabe im Status `Aktiv`, erhält aber keinen impliziten neuen Erststart; ein erneuter CLI-Start erfolgt ausschließlich über „Starten“.
- Die Beschriftung „Starten“ bleibt auch beim erneuten Start einer beendeten Ausführung bestehen. Die UI zeigt für eine beendete Ausführung den vorhandenen Nicht-CLI-/Aufgabenbereich und einen aktivierbaren Startbefehl; eine neue erklärende Ansicht ist nicht nötig.

## Änderungen nach Datei

### 1. Persistentes Statusmodell und EF-Persistenz

- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
  - Eine nullable-freie Property für den persistenten KI-Ausführungsstatus ergänzen und bei neu erzeugten Aufgaben auf `NichtGestartet` setzen.
  - Die bestehende Laufmetadatenlogik so abgrenzen, dass `AktiveRunId`, Heartbeat, Startzeit und `LaufStatus` beim Ende/Stoppen bereinigt werden, ohne den neuen Ausführungsstatus mit `Aufgabe.Status` zu vermischen.
  - Bei Gesamtstatus `Beendet` keine erneute Aktivierung der KI-Ausführung zulassen.
- `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs` (neu) sowie gegebenenfalls eine passende Extension-Datei
  - Werte und Hilfsfunktionen für `NichtGestartet`, `Aktiv` und `Beendet` definieren.
  - Abfragen wie „darf gestartet werden“ und „soll CLI angezeigt/wiederverbunden werden“ auf den Ausführungsstatus und den Gesamtstatus stützen.
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
  - Die neue Enum-Property analog zu `Status` und `LaufStatus` als String mappen.
- `src/Softwareschmiede/Migrations/<timestamp>_AddAufgabeAusfuehrungsStatus.cs` (neu) und der zugehörige Model-Snapshot
  - Eine nicht-nullbare Textspalte mit dem EF-kompatiblen Defaultwert anlegen.
  - Bestehende Daten entsprechend der Backfill-Annahme initialisieren, insbesondere ohne `AktiveRunId` nicht als aktiv markieren.
  - Down-Migration nur für die neue Spalte vorsehen.

### 2. Domain- und Anwendungsservices für Start, Stoppen und Abschluss

- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
  - Erzeugung und Statusänderungen um den Ausführungsstatus erweitern.
  - Methoden für „Ausführung aktiv setzen“ und „Ausführung beenden“ ergänzen oder die bestehenden `AktivenLauf...`-Methoden entsprechend erweitern.
  - Sicherstellen, dass das Beenden der KI-Ausführung weder `Aufgabe.Status` auf `Beendet` setzt noch `LokalerKlonPfad` löscht.
  - Startübergänge für `Neu` und `Ausfuehrungsstatus.Beendet` erlauben, solange `Aufgabe.Status != Beendet`/`Archiviert`; den Gesamtstatus bei einem Start nur wie bisher fachlich erforderlich auf `Gestartet` setzen.
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - Erststart und erneuten CLI-Start trennen: Erststart bereitet Repository und Branch vor, Neustart verwendet den bestehenden Klon.
  - Vor einem erneuten Start den Gesamtstatus auf terminale Werte prüfen.
  - `AbschliessenAsync` unverändert als einzigen Pfad für Klonlöschung und Gesamtstatus `Beendet` behandeln; laufende CLI-Prozesse vorher über den bestehenden kontrollierten Ablauf beenden, ohne den Abschluss mit einem normalen Stoppen gleichzusetzen.
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
  - Beim erfolgreichen manuellen Stoppen und beim beobachteten Prozessende ein Ereignis/Callback bereitstellen, das den persistenten Ausführungsstatus auf `Beendet` setzt und die flüchtigen Laufdaten bereinigt.
  - Fehler beim Prozessende dürfen den Gesamtstatus nicht verändern; die bestehende Protokollierung bleibt erhalten.
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
  - Recovery-Kandidaten auf Aufgaben mit persistentem Ausführungsstatus `Aktiv` begrenzen.
  - `Beendet` und `NichtGestartet` aus Autostart-/Recovery-Kandidaten ausschließen.
  - Recovery darf eine aktive, wiederherstellbare CLI verbinden bzw. den bestehenden Recovery-Pfad verwenden, aber niemals eine beendete Ausführung implizit neu starten.

### 3. Aufgabenansicht, Commands und Autostart

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
  - `LadenAsync` so ändern, dass der persistierte Ausführungsstatus zuerst ausgewertet wird: aktive Session verbinden, aktiven Recovery-Fall behandeln, bei `Beendet`/`NichtGestartet` keine CLI automatisch starten.
  - `StartenCommand` für neue und beendete KI-Ausführungen freigeben, solange der Gesamtstatus nicht terminal ist; beim Status `Beendet` der Aufgabe deaktivieren/verbergen.
  - Start für `NichtGestartet` über den bestehenden Erststart und für `Beendet` über den vorbereiteten Klon/`CliNeustartenAsync` führen. Nach erfolgreichem Start den Ausführungsstatus auf `Aktiv` setzen und das CLI-Panel anzeigen.
  - `CliStoppenAsync` auf den persistenten Übergang `Aktiv -> Beendet` umstellen. Gesamtstatus, Repository und Abschlusslogik dürfen dabei unverändert bleiben.
  - Anzeigezustände (`ShowCliPanel`, Starten/Stoppen/Beenden) nicht mehr allein aus `Aufgabe.Status` ableiten. Für eine beendete Ausführung muss „Starten“ möglich bleiben; für eine insgesamt beendete Aufgabe darf kein Start möglich sein.
  - Bei fehlender wiederherstellbarer Session keine automatische Neustart-Aufrufkette aus `LadenAsync` auslösen.
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
  - Beim Erstellen die Liste weiterhin aktualisieren, aber den bestehenden geöffneten `TaskDetailViewModel` nicht durch Rücknavigation ersetzen.
  - Prüfen, dass die neue Aufgabe mit `NichtGestartet` gespeichert und direkt in der Detailansicht bearbeitbar bleibt.
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
  - Beim Wechsel zwischen Aufgaben und beim erneuten Aufruf dieselbe Statusauswertung wie in `TaskDetailViewModel` ermöglichen.
  - Aufgaben mit beendetem KI-Lauf weiterhin über die Projektliste öffnen können; eine aktive Seitenleistenfilterung darf nicht verhindern, dass die Aufgabe über ihren Projektkontext erreichbar ist.

### 4. Speichern und Navigation

- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`, Methode `SpeichernAsync`
  - Nach erfolgreichem Speichern und Neuladen die Listen-Callback-Aktualisierung ausführen.
  - `ZurueckAction` im erfolgreichen Speichervorgang einer neu angelegten Aufgabe nicht mehr ausführen. Die aktuelle Detailansicht bleibt geöffnet und erhält die persistierte Aufgaben-ID bzw. den aktuellen Status.
  - Fehlerpfade und explizite Zurücknavigation unverändert lassen.
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs` und zugehörige Navigation
  - Sicherstellen, dass der direkte Aufruf „Neue Aufgabe“ weiterhin die Detailansicht öffnet und der Callback lediglich die Projektliste aktualisiert.

## Tests

Die bestehenden Tests werden gezielt erweitert; UI-Automation wird nach Möglichkeit in wenigen Szenarien gebündelt, damit derselbe App-Start mehrere zusammengehörige Zustandsübergänge abdeckt.

- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests*` und `AufgabeServiceTests_AktiverLauf*`
  - Default `NichtGestartet` bei neuen Aufgaben prüfen.
  - Übergänge `NichtGestartet -> Aktiv`, `Aktiv -> Beendet` und erneutes `Beendet -> Aktiv` prüfen.
  - Beenden der Ausführung darf weder Gesamtstatus `Beendet` setzen noch den Klonpfad löschen.
  - Gesamtstatus `Beendet` muss Starten unabhängig vom Ausführungsstatus verhindern.
- `src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests*`
  - Nur `Aktiv` als Recovery-Kandidat zulassen.
  - `NichtGestartet` und `Beendet` dürfen keinen Autostart auslösen.
  - Wiederherstellung einer aktiven Aufgabe nach fehlendem Prozess prüfen, ohne eine beendete Ausführung zu simulieren.
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests*`
  - Manueller Stop und normales Prozessende setzen den persistenten Ausführungsstatus auf `Beendet`.
  - Fehlerpfad verändert den Gesamtstatus nicht.
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests*`
  - Laden mit aktivem Status verbindet/zeigt die CLI.
  - Laden mit beendetem oder nicht gestarteten Status startet keine CLI automatisch.
  - Explizites Starten einer beendeten Ausführung setzt den Status auf aktiv und zeigt die CLI.
  - Stoppen lässt den Gesamtstatus `Gestartet`/`Wartend` bestehen und erlaubt späteres explizites Starten.
  - Gesamtstatus `Beendet` sperrt Starten.
  - Speichern einer neuen Aufgabe führt nicht `ZurueckAction` aus und hält die Detailansicht offen.
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests*`
  - Aufgabenerstellung öffnet weiterhin die Detailansicht; Listenaktualisierung erfolgt ohne Rücknavigation.
- `src/Softwareschmiede.IntegrationTests/Services/*` (bestehende Persistenz-/DbContext-Testdatei verwenden oder bei Bedarf neu anlegen)
  - Migration/Mapping des neuen String-Enums prüfen.
  - Backfill für `Neu`, laufende Aufgaben mit `AktiveRunId`, gestoppte Aufgaben ohne `AktiveRunId` sowie bereits insgesamt beendete Aufgaben prüfen.
  - Laden und erneutes Speichern darf den Ausführungsstatus nicht verlieren.
- `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation*`
  - Erwartung von Rückkehr zur Projektansicht auf geöffnete TaskDetailView ändern und prüfen, dass die gespeicherten Eingaben sichtbar bleiben.
- `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle*` beziehungsweise die bestehende gebündelte CLI-Lifecycle-Suite
  - Start, Wechsel zu einer anderen Aufgabe, Rückkehr bei aktiver Ausführung und erneutes Laden abdecken.
  - Stoppen, Rückkehr ohne Autostart und explizites erneutes Starten abdecken.
  - Abschluss separat prüfen: nur „Beenden“ setzt den Gesamtstatus und löst die vorhandene Repository-Löschung aus.
  - Bestehende E2E-Szenarien mit minimaler Zahl an Testmethoden erweitern, ohne die unterschiedlichen Zustandsübergänge zu entfernen.

## Reihenfolge der Umsetzung

1. Enum, Entity, DbContext und Migration inklusive Backfill anlegen.
2. `AufgabeService`, CLI-/Entwicklungsprozess-Service und Recovery auf den getrennten Status umstellen.
3. `TaskDetailViewModel` und Navigation für Autostart, Start/Stop/Abschluss und Speichern anpassen.
4. Unit- und Integrationstests für Persistenz und Übergänge ergänzen.
5. Bestehende E2E-Szenarien für Speichern, Wechsel, Wiederaufruf, Neustart, Stoppen und erneutes Starten aktualisieren bzw. gebündelt erweitern.

## Verifikation

Für den Implementierungsschritt gilt die Vorgabe aus `CLAUDE.md`: Vor Tests muss ein vollständiger Build erfolgreich sein. In diesem Plan-Schritt werden ausdrücklich keine Tests ausgeführt.

## Offene Punkte

Keine. Die oben genannten Annahmen legen die für die Umsetzung notwendigen Details konservativ fest.
