# Anforderungsübersetzung: Korrektur des Arbeitsablaufs

**Aufgaben-ID:** 10e1ab8d-2ea0-4694-9868-44c2dfcefae0  
**Branch:** task/10e1ab8d2ea04694986844c2dfcefae0-korrektur-des-arbeitsablaufs  
**Erstellt:** 2026-08-20

---

## Fachliche Zusammenfassung

Nach der letzten Änderung (Migration `AufgabeAusfuehrungsStatus`) ist die CLI-Ansicht in der Aufgabendetailansicht nicht mehr verfügbar, nachdem die Ausführung beendet oder der CLI-Anbieter gewechselt wurde — obwohl die Aufgabe selbst noch im Status `Gestartet` oder `Wartend` ist. Das Problem liegt in der unzureichenden Sichtbarkeitsbedingung für `ShowCliPanel`, die nur dann `true` wird, wenn `AusfuehrungsStatus == Aktiv`. Die Anforderung zielt darauf ab, die CLI-Ansicht auch dann verfügbar zu halten, wenn die Ausführung beendet ist, sodass der Benutzer die letzte Ausgabe anschauen und die CLI manuell neu starten kann. Das Verhalten bei Plugin-Wechsel muss korrigiert werden, um den CLI-Panel nicht unerwünscht auszublenden.

---

## Betroffene Klassen und Komponenten

### Datenmodell und Enums
- **`AufgabeAusfuehrungsStatus`** (Enum) — Status mit Werten `NichtGestartet`, `Aktiv`, `Beendet`
- **`AufgabeAusfuehrungsStatusExtensions`** — Erweiterungsmethode `SollCliAnzeigen` mit fehlerhafter Bedingung

### ViewModels
- **`TaskDetailViewModel`** — Property `ShowCliPanel`, das auf `SollCliAnzeigen` aufruft
- **Abhängige Properties:** `KannCliNeuStarten`, UI-Commands `CliViewCommand`

### Services
- **`KiAusfuehrungsService`** — Verwaltet CLI-Prozesse, beendet und startet Sessions
- **`AufgabeService`** — Persistiert `AusfuehrungsStatus`
- **`EntwicklungsprozessService`** — Orchestriert CLI-Start und Plugin-Wechsel

---

## Implementierungsansatz

### 1. Korrektur der Sichtbarkeitsbedingung

**Problem:** `AufgabeAusfuehrungsStatusExtensions.SollCliAnzeigen()` erlaubt die CLI-Anzeige nur, wenn `ausfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv`.

**Lösung:** Die Bedingung erweitern, um auch `Beendet`-Status zu akzeptieren:

```csharp
public static bool SollCliAnzeigen(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
    => aufgabeStatus.IstAktivOderWartend()
        && ausfuehrungsStatus is (AufgabeAusfuehrungsStatus.Aktiv or AufgabeAusfuehrungsStatus.Beendet);
```

**Effekt:**
- CLI-Ansicht ist sichtbar, solange die Aufgabe im Status `Gestartet` oder `Wartend` ist, unabhängig davon, ob `AusfuehrungsStatus == Aktiv` oder `Beendet`
- Benutzer kann die letzte CLI-Ausgabe anschauen
- Der Button „Starten" bleibt verfügbar (da `DarfAusfuehrungStarten` bereits `Beendet` erlaubt), sodass der Benutzer die CLI manuell neu starten kann
- Bei Status `NichtGestartet` wird die CLI-Ansicht nicht angezeigt (gewünschtes Verhalten bleibt erhalten)

### 2. Verhalten beim Plugin-Wechsel prüfen

**Kontext:** In `TaskDetailViewModel.PluginWechselAsync`:
1. Aktueller Prozess wird mit `KiAusfuehrungsService.StopCliAsync()` beendet
2. `IsCliRunning` wird lokal auf `false` gesetzt
3. Neuer Prozess wird mit `StartCliAndUpdateStateAsync()` gestartet

**Annahme:** Nach dieser Änderung sollte der Plugin-Wechsel nicht mehr die CLI-Ansicht ausblendet, da:
- Während des Wechsels wird `ShowCliPanel` kurzzeitig auf `false` gestellt (da `IsCliRunning = false` und `AusfuehrungsStatus == Aktiv` noch nicht aktualisiert ist)
- Nach dem erfolgreichen Start wird `AusfuehrungAktivSetzenAsync` aufgerufen, wodurch `AusfuehrungsStatus` wieder auf `Aktiv` gesetzt wird
- `ShowCliPanel` wird wieder `true`

**Validierungspunkt:** Falls der Plugin-Wechsel nach dieser Änderung immer noch eine leere CLI-Ansicht zeigt (statt eine neue Session einzubetten), ist die Ursache im Prozess-Handle-Management, nicht in `ShowCliPanel` selbst.

---

## Betroffene UI-Komponenten und Events

### TaskDetailView (XAML-Bindings)
- `CliViewCommand` — Binding beeinträchtigt, wenn `ShowCliPanel` sich ändert
- CLI-Panel-Sichtbarkeit — Sollte nicht mehr verschwinden bei beendeter Ausführung

### Events und Callbacks
- `TaskDetailViewModel.OnPropertyChanged(nameof(ShowCliPanel))` — Wird von mehreren Properties beobachtet:
  - `Aufgabe` (Property-Setter)
  - CLI-Prozess-Stopp (`OnCliProcessStatusChanged` bei `IsCliRunning = false`)
  - Plugin-Wechsel (indirekt durch Status-Updates)

---

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Die Verhaltensänderung ist rein logisch und hat keine Benutzer-sichtbaren Einstellungen.

---

## Offene Fragen und Validierungspunkte

1. **Ist das Verhalten beim Plugin-Wechsel nach dieser Änderung akzeptabel?**
   - Wird die neue CLI-Session korrekt eingebettet, oder gibt es Timing-Probleme zwischen `StopCliAsync` und `StartCliAsync`?

2. **Sollte die CLI-Ansicht auch dann verfügbar sein, wenn die Aufgabe im Status `Neu` ist und nie gestartet wurde?**
   - Aktuell: Nein (da `IstAktivOderWartend()` nur `Gestartet` und `Wartend` zulässt)
   - Dies ist wahrscheinlich erwünscht, um Verwirrung zu vermeiden

3. **Automatische Recovery bei App-Neustart:**
   - Nach dieser Änderung wird die CLI auch angezeigt, wenn `AusfuehrungsStatus == Beendet` und die App neu gestartet wird
   - Ist dies gewünschtes Verhalten? (Benutzer sieht CLI-Ansicht, kann aber nicht neu starten, da `AusfuehrungsStatus == Beendet` erlaubt einen Start, aber es keine Recovery-Session gibt)
   - **Annahme:** Dies ist akzeptabel, da der Benutzer bewusst die Ausführung beendet hat und die letzte Ausgabe anschauen kann

4. **E2E-Tests für CLI-Status-Übergänge:**
   - Gibt es Tests für den Übergang `Aktiv` → `Beendet` und die CLI-Ansicht-Sichtbarkeit?
   - Tests für Plugin-Wechsel-Szenarios sollten überprüft werden

