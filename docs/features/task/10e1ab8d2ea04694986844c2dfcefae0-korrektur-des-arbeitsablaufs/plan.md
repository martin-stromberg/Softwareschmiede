# Umsetzungsplan: Korrektur des Arbeitsablaufs (CLI-Panel-Sichtbarkeit)

## Übersicht

Die Anforderung behebt einen Fehler in der Sichtbarkeitsbedingung des CLI-Panels nach Beendigung der Ausführung. Das CLI-Panel wird fälschlich ausgeblendet, wenn `AusfuehrungsStatus == Beendet` gesetzt wird, obwohl die Aufgabe noch im Status `Gestartet` oder `Wartend` ist. Die Lösung besteht darin, die Bedingung in `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen` zu erweitern, um auch `Beendet`-Status zu akzeptieren. Dies ermöglicht dem Benutzer, die letzte CLI-Ausgabe anzuschauen und die CLI manuell neu zu starten, ohne dass das Panel verschwindet.

## Designentscheidungen

Keine — die Lösung folgt bestehenden Mustern und Konventionen. Die Erweiterung der Bedingung ist eine minimale, logisch notwendige Korrektur ohne Alternativen.

## Programmabläufe

### Ablauf 1: CLI-Panel bleibt nach Beendigung sichtbar

1. Benutzer startet die CLI über den "Starten"-Button
2. `StartCliAsync` wird aufgerufen → `AusfuehrungsStatus` wird auf `Aktiv` gesetzt
3. `ShowCliPanel` wird auf `true` gesetzt (Bedingung erfüllt: `Aktiv`)
4. Benutzer oder Prozess beendet die Ausführung
5. `HandleProcessExitedAsync` wird aufgerufen → `PersistAusfuehrungBeendetAsync` → `AktivenLaufBeendenAsync`
6. `AusfuehrungsStatus` wird auf `Beendet` gesetzt
7. `ShowCliPanel` wird invalidiert und neu ausgewertet
8. Nach der Korrektur: `ShowCliPanel` bleibt `true` (Bedingung erfüllt: `Beendet`)
9. Benutzer kann die letzte CLI-Ausgabe anschauen
10. Benutzer kann den "Starten"-Button drücken → CLI neu starten

Beteiligte Klassen/Komponenten: `AufgabeAusfuehrungsStatusExtensions`, `TaskDetailViewModel`, `KiAusfuehrungsService`, `AufgabeService`

### Ablauf 2: Plugin-Wechsel mit korrektem CLI-Panel-Verhalten

1. Benutzer wechselt das KI-Plugin (Dialog öffnen, Plugin auswählen)
2. `PluginWechselAsync` wird aufgerufen
3. Aktuelle CLI wird mit `StopCliAsync` gestoppt
4. `IsCliRunning` wird auf `false` gesetzt (lokal)
5. `AusfuehrungsStatus` wird auf `Beendet` gesetzt (via `PersistAusfuehrungBeendetAsync`)
6. `ShowCliPanel` wird invalidiert → nach Korrektur `true` (Status ist `Beendet`, Aufgabenstatus noch `Gestartet`)
7. Neuer CLI-Prozess wird mit `StartCliAndUpdateStateAsync` gestartet
8. `AusfuehrungsStatus` wird auf `Aktiv` gesetzt
9. `ShowCliPanel` bleibt `true` (Bedingung weiterhin erfüllt)
10. CLI-Panel zeigt neue Session an ohne Unterbrechung

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `KiAusfuehrungsService`, `AufgabeService`, `EntwicklungsprozessService`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `AufgabeAusfuehrungsStatusExtensions` (Static Class)

- **Geänderte Methoden:** `SollCliAnzeigen` — Die Bedingung für `ausfuehrungsStatus` wird von `== AufgabeAusfuehrungsStatus.Aktiv` zu `is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet)` erweitert. Dies ermöglicht die Anzeige des CLI-Panels auch nach Beendigung der Ausführung.
- **Aktualisierte XML-Dokumentation:** Der Kommentar der Methode sollte explizit erwähnen, dass die CLI-Ansicht in beiden Zuständen (`Aktiv` und `Beendet`) angezeigt wird, wenn die Aufgabe aktiv oder wartend ist.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **TaskDetailViewModel Properties:** `ShowCliPanel` und `KannCliNeuStarten` verwenden beide `SollCliAnzeigen`. Nach der Korrektur wird `ShowCliPanel` länger `true` sein, was zu längerer Sichtbarkeit des CLI-Panels führt. Dies ist gewünscht.
- **UI-Blinkeffekte:** Der Plugin-Wechsel könnte einen weniger ausgeprägten Blinkeffekt des CLI-Panels aufweisen, da das Panel während des Wechsels nicht mehr verschwindet (nur wenn es vorher nicht vorhanden war).
- **Bestehende Tests:** Tests, die explizit mit `AusfuehrungsStatus == Beendet` und `ShowCliPanel == false` arbeiten, müssen überprüft werden. Es ist wahrscheinlich, dass solche Tests nicht existieren, da `ShowCliPanel` primär über Properties invalidiert und nicht explizit in Tests überprüft wird.
- **App-Neustart-Szenario:** Wenn die App neu gestartet wird, während `AusfuehrungsStatus == Beendet` ist, wird das CLI-Panel angezeigt (gewünscht). Der Benutzer kann dann eine neue Ausführung starten.

## Umsetzungsreihenfolge

1. **Korrektur von `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen`**
   - Voraussetzungen: Keine
   - Beschreibung: Änderung der Bedingung in `SollCliAnzeigen` von `ausfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv` zu `ausfuehrungsStatus is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet)`. XML-Dokumentation aktualisieren, um die neue Logik zu erklären.
   - Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatusExtensions.cs`

2. **Unit-Tests erweitern für `SollCliAnzeigen` mit `Beendet`-Status**
   - Voraussetzungen: Änderung aus Schritt 1 durchgeführt
   - Beschreibung: Neue Testmethode erstellen, um zu überprüfen, dass `SollCliAnzeigen` `true` zurückgibt, wenn `AusfuehrungsStatus == Beendet` und `AufgabeStatus.IstAktivOderWartend() == true`. Test sollte auch überprüfen, dass bei `AufgabeStatus == Beendet` oder `Archiviert` weiterhin `false` zurückgegeben wird.
   - Testdatei: `src/Softwareschmiede.Tests/Domain/Enums/AufgabeAusfuehrungsStatusExtensionsTests.cs` (oder ähnlich, abhängig von vorhandener Teststruktur)

3. **Überprüfung und ggf. Anpassung bestehender Unit-Tests**
   - Voraussetzungen: Änderungen aus Schritt 1 und 2 durchgeführt
   - Beschreibung: Durchsuche den Code nach Tests, die `SollCliAnzeigen` mit `Beendet`-Status verwenden oder Tests, die `ShowCliPanel` überprüfen. Überprüfe, ob die Tests mit der neuen Bedingung noch sinnvoll sind. Passe Tests an oder ergänze sie, wenn nötig (z. B. Tests in `TaskDetailViewModelTests`).

4. **E2E-Test für CLI-Panel-Sichtbarkeit nach Beendigung**
   - Voraussetzungen: Änderungen aus Schritt 1–3 durchgeführt, Code gebaut
   - Beschreibung: Neuer E2E-Test, der überprüft, dass das CLI-Panel nach Beendigung der Ausführung sichtbar bleibt. Szenario: Aufgabe starten → CLI-Ausgabe produzieren → Ausführung beenden → Überprüfe, dass CLI-Panel noch vorhanden und "Starten"-Button noch klickbar ist.
   - Testdatei: `src/Softwareschmiede.Tests/E2E/E2E_CliPanelVisibility.cs` (neue Datei) oder Erweiterung bestehender E2E-Test-Klasse

5. **E2E-Test für Plugin-Wechsel mit CLI-Panel-Kontinuität**
   - Voraussetzungen: Änderungen aus Schritt 1–4 durchgeführt, Code gebaut
   - Beschreibung: E2E-Test überprüft, dass beim Plugin-Wechsel das CLI-Panel nicht verschwindet (oder nur kurzzeitig). Szenario: Aufgabe starten → CLI aktiv → Plugin-Dialog öffnen, neues Plugin auswählen → Überprüfe, dass CLI-Panel weiterhin sichtbar ist und neue Session anzeigt.
   - Testdatei: `src/Softwareschmiede.Tests/E2E/E2E_CliPanelVisibility.cs` (neue Datei) oder bestehende `E2E_Plugin*.cs`-Datei

6. **Vollständiger Build und Testlauf**
   - Voraussetzungen: Alle Code-Änderungen durchgeführt
   - Beschreibung: `dotnet build` und `dotnet test` ausführen (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Überprüfe, dass alle Tests bestehen und keine Regressionen eingeführt wurden.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsGestartet_ReturnsTrue` | `AufgabeAusfuehrungsStatusExtensionsTests` oder ähnlich | Überprüft, dass `SollCliAnzeigen` `true` zurückgibt, wenn `AusfuehrungsStatus == Beendet` und `AufgabeStatus == Gestartet` |
| `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsWartend_ReturnsTrue` | `AufgabeAusfuehrungsStatusExtensionsTests` oder ähnlich | Überprüft, dass `SollCliAnzeigen` `true` zurückgibt, wenn `AusfuehrungsStatus == Beendet` und `AufgabeStatus == Wartend` |
| `SollCliAnzeigen_WhenAusfuehrungsStatusIsBeendet_AndAufgabeStatusIsBeendet_ReturnsFalse` | `AufgabeAusfuehrungsStatusExtensionsTests` oder ähnlich | Überprüft, dass `SollCliAnzeigen` `false` zurückgibt, wenn `AufgabeStatus == Beendet` (obwohl `AusfuehrungsStatus == Beendet`) |
| `CliPanelVisibility_AfterExecution_RemainsVisible` | E2E-Testklasse (z. B. `E2E_CliPanelVisibility`) | E2E-Test: Überprüft, dass das CLI-Panel nach Beendigung der Ausführung sichtbar bleibt und der "Starten"-Button klickbar ist |
| `CliPanelVisibility_DuringPluginSwitch_RemainsVisible` | E2E-Testklasse (z. B. `E2E_CliPanelVisibility`) | E2E-Test: Überprüft, dass das CLI-Panel während des Plugin-Wechsels nicht verschwindet |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTests` (allgemein) | Möglicherweise Tests, die `ShowCliPanel` mit `AusfuehrungsStatus == Beendet` überprüfen und `false` erwarten. Diese müssen angepasst oder überprüft werden. Wahrscheinlich sind keine direkten Tests betroffen, da die Logik meist nur indirekt über Property-Invalidierung getestet wird. |
| `E2E_FileExplorer.cs` | Hinweis in Inventory: Test enthält Hinweise auf `ShowCliPanel`-Verhalten. Überprüfen, ob Test noch valide ist (wahrscheinlich ja, da Test ohnehin erwartet, dass Panel während aktiver Aufgabe sichtbar ist). |

Falls bestehende Tests scheitern, müssen sie überprüft werden. Es ist wahrscheinlich, dass keine Tests scheitern, da die Änderung eine Erweiterung der True-Bedingung ist, nicht eine Einschränkung.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| CLI-Panel bleibt nach Beendigung sichtbar | `E2E_CliPanelVisibility.cs` (neue Datei) oder Erweiterung bestehender Datei | Akzeptanzkriterium 1: Benutzer kann die letzte CLI-Ausgabe anschauen, wenn Ausführung beendet ist. Benutzer kann den "Starten"-Button drücken. |
| Plugin-Wechsel mit CLI-Panel-Kontinuität | `E2E_CliPanelVisibility.cs` oder `E2E_PluginWechsel.cs` | Akzeptanzkriterium 2: CLI-Panel wird nicht fälschlich ausgeblendet beim Plugin-Wechsel. Neue Session wird angezeigt. |

**Betroffene bestehende E2E-Tests:**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine identifizierten Anpassungen erforderlich | Die Änderung ist eine logische Erweiterung. Bestehende E2E-Tests sollten weiterhin bestehen, es sei denn, sie prüfen explizit, dass `ShowCliPanel == false` bei `AusfuehrungsStatus == Beendet`, was unwahrscheinlich ist. |

## Offene Punkte

Keine. Alle Aspekte der Anforderung sind durch die Bestandsaufnahme klar dokumentiert und die Lösung ist eindeutig vorgegeben.
