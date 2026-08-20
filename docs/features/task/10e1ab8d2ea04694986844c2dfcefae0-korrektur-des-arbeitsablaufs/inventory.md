# Bestandsaufnahme: Korrektur des Arbeitsablaufs

Diese Bestandsaufnahme analysiert die bestehende Implementierung der Aufgabendetail-Ansicht und der CLI-Verwaltung im Kontext der Anforderung zur Korrektur des Arbeitsablaufs (Aufgaben-ID: 10e1ab8d-2ea0-4694-9868-44c2dfcefae0).

## Zusammenfassung

Nach der Anforderungs-Analyse wurde folgendes festgestellt:

### Betroffene Komponenten

- **Enum `AufgabeAusfuehrungsStatus`:** Definiert mit Werten `NichtGestartet`, `Aktiv`, `Beendet` ✓
- **Extension `AufgabeAusfuehrungsStatusExtensions`:** Enthält zwei kritische Methoden:
  - `DarfAusfuehrungStarten`: Prüft, ob Start erlaubt ist (akzeptiert bereits `Beendet`) ✓
  - `SollCliAnzeigen`: **FEHLERHAFT** – prüft nur `Aktiv`, nicht `Beendet` ✗
- **ViewModel `TaskDetailViewModel`:** 
  - Property `ShowCliPanel` nutzt `SollCliAnzeigen` direkt (erbt somit den Fehler) ✗
  - Property `KannCliNeuStarten` nutzt auch `SollCliAnzeigen` ✗
  - Weitere abhängige Properties: `IsCliRunning`, `Aufgabe`
- **Service `KiAusfuehrungsService`:** 
  - Startet und stoppt CLI-Prozesse ✓
  - Publiziert `CliProcessStatusChanged` Event ✓
  - Ruft `PersistAusfuehrungBeendetAsync` bei Prozessbeendigung auf ✓
- **Service `AufgabeService`:**
  - Methode `AktivenLaufBeendenAsync` setzt `AusfuehrungsStatus = Beendet` ✓
  - Wird von `KiAusfuehrungsService` aufgerufen ✓
- **Service `EntwicklungsprozessService`:**
  - Koordiniert Repository-Setup und CLI-Start ✓
  - Nicht direkt betroffen von dieser Anforderung

### Kritische Erkenntnisse

1. **Die Sichtbarkeitsbedingung ist zu restriktiv:** 
   - Aktuell: CLI-Panel ist nur sichtbar, wenn `AusfuehrungsStatus == Aktiv`
   - Gewünscht: CLI-Panel sollte auch sichtbar sein, wenn `AusfuehrungsStatus == Beendet` und `Status ∈ {Gestartet, Wartend}`
   - Effekt: Nach dem Stoppen der CLI kann der Benutzer die letzte Ausgabe nicht mehr anschauen und die CLI nicht neu starten

2. **Die Bedingung `DarfAusfuehrungStarten` ist bereits korrekt:**
   - Akzeptiert bereits `Beendet` als validen Status für einen erneuten Start
   - Der "Starten"-Button wird also korrekterweise angezeigt, wenn `AusfuehrungsStatus == Beendet`

3. **Property-Invalidierung ist vorhanden:**
   - `CliStoppenAsync` setzt `_aufgabe.AusfuehrungsStatus = Beendet` lokal und invalidiert `ShowCliPanel` ✓
   - `LadenAsync` prüft `SollCliAnzeigen` beim Wiederaufbinden einer Session ✓

4. **Plugin-Wechsel-Logik:**
   - In `TaskDetailViewModel.PluginWechselAsync`: CLI wird gestoppt, dann neu gestartet
   - Während des Wechsels wird `ShowCliPanel` kurzzeitig `false` (Timing-Abhängigkeit)
   - Nach erfolgreichen Restart wird Status wieder `Aktiv` und `ShowCliPanel` wird `true`
   - Diese Logik sollte nach der Korrektur von `SollCliAnzeigen` korrekt funktionieren

### Tests

- Umfangreiche Unit-Tests für TaskDetailViewModel vorhanden (~2534 Zeilen)
- Tests für KiAusfuehrungsService und AufgabeService vorhanden
- E2E-Tests existieren, enthalten auch Hinweise auf `ShowCliPanel`-Verhalten
- **Lücke:** Keine direkten Tests für `SollCliAnzeigen` Extension mit `Beendet`-Status identifiziert

---

## Details

Detaillierte Analysen der einzelnen Komponenten:

- [Enums](inventory/enums.md) – `AufgabeAusfuehrungsStatus`, `CliProcessStatus`
- [Erweiterungsmethoden](inventory/extensions.md) – `AufgabeAusfuehrungsStatusExtensions.DarfAusfuehrungStarten` und `SollCliAnzeigen` (mit Fehleranalyse)
- [Datenmodelle](inventory/models.md) – `Aufgabe`, `CliProcessHandle` Entities
- [Logikklassen](inventory/logic.md) – `TaskDetailViewModel`, `KiAusfuehrungsService`, `AufgabeService`, `EntwicklungsprozessService`
- [Tests](inventory/tests.md) – Übersicht aller existierenden Tests und Hilfsfaktoren

---

## Handlungsfelder

Basierend auf dieser Bestandsaufnahme:

1. **Korrektur der Sichtbarkeitsbedingung in `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen`:**
   - Bedingung von `== AufgabeAusfuehrungsStatus.Aktiv` zu `is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet)` ändern
   - Dies ist die minimale Änderung, um die Anforderung zu erfüllen

2. **Validierung des Plugin-Wechsel-Verhaltens:**
   - Nach der Korrektur sollten E2E-Tests den Plugin-Wechsel überprüfen
   - Sicherstellen, dass keine unerwünschten CLI-Panel-Blinkeffekte auftreten

3. **Erweitern der Test-Abdeckung:**
   - Tests für `SollCliAnzeigen` mit `Beendet`-Status hinzufügen
   - Tests für den Übergang `Aktiv` → `Beendet` und die Sichtbarkeit des CLI-Panels

4. **Dokumentation aktualisieren:**
   - XML-Kommentare in `SollCliAnzeigen` aktualisieren, um die neue Logik zu erklären
