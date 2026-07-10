# Anforderung: Arbeitsstatus in Aufgabenliste

## Fachliche Zusammenfassung

Die in der Navigationsseitenleiste und im Dashboard angezeigten aktiven Aufgaben sollen ihren KI-Ausführungsstatus (mit Symbol und Textbeschreibung) stets aktuell darstellen. Der Status zeigt an, ob die laufende CLI arbeitet, auf Eingaben wartet oder bereit ist. Die Anzeige muss sich automatisch aktualisieren, wenn sich der Ausführungsstatus einer Aufgabe ändert, ohne dass der Benutzer die Ansicht manuell neu laden muss. Dies verbessert die Sichtbarkeit des Fortschritts laufender KI-Entwicklungsaufgaben.

## Betroffene Klassen und Komponenten

### Bestehende Komponenten (zu prüfen/erweitern)
- **`KiAusfuehrungsStatusConverter`** (Converter) — Konvertiert `Aufgabe`-Objekt zu Status-String mit Symbol
  - Aktuell implementiert: `"▶ Läuft"`, `"⏸ Wartet"`, `"✓ Bereit"`
- **`MainWindowViewModel.AktiveAufgabenListe`** (ObservableCollection) — Binding-Quelle für Seitenleisten-Kacheln
- **`DashboardViewModel.AktiveAufgabenListe`** (ObservableCollection) — Binding-Quelle für Dashboard-Aufgabenliste
- **`AktiveAufgabeCardTemplate`** (DataTemplate in App.xaml) — Rendering-Vorlage für aktive Aufgabenkacheln
- **`AufgabeService.GetAktiveAufgabenAsync()`** (Service-Methode) — Filtert und sortiert aktive Aufgaben
- **`MainWindowViewModel.AktiveAufgabenAktualisierenAsync()`** (Service-Integration) — Ruft `AufgabeService` auf und befüllt `AktiveAufgabenListe`
- **`KiAusfuehrungsService`** (Singleton) — Verwaltet CLI-Prozess-Lifecycle und gibt `CliProcessStatusChanged`-Event aus
- **`AufgabeRecoveryService`** (Service) — Erkennt verwaiste Aufgaben mit abgelaufenem Heartbeat

### Neue oder erweiterte Komponenten
- **`AufgabeService.GetAktiveAufgabenAsync()`** (optional) — Eventuell Erweiterung zur effizienteren Abfrage bei hoher Refresh-Rate
- **Heartbeat-Update-Mechanismus** — Möglicherweise Timer oder Event-basierte Aktualisierung zur periodischen Status-Abfrage
- **Unit/Integration Tests** — Test-Abdeckung für Status-Updates bei verschiedenen Szenarien

## Implementierungsansatz

### Kernlogik
1. **Status-Berechnung** (bereits implementiert):
   - Der `KiAusfuehrungsStatusConverter` berechnet den Status basierend auf:
     - `AktiveRunId != null` ∧ `LastHeartbeatUtc` nicht älter als 5 Min → `"▶ Läuft"`
     - `Status == AufgabeStatus.Wartend` → `"⏸ Wartet"`
     - Fallback → `"✓ Bereit"`

2. **Automatische Aktualisierung**:
   - `MainWindowViewModel` muss periodisch (z.B. alle 2-5 Sekunden oder bei Heartbeat-Event) `AktiveAufgabenAktualisierenAsync()` aufrufen
   - Oder: `KiAusfuehrungsService.CliProcessStatusChanged`-Event abonnieren und nur betroffene Aufgabe aktualisieren (effizienter)
   - Dasselbe gilt für `DashboardViewModel`

3. **Aktualisierungsstrategie** (offene Entscheidung):
   - **Option A (Timer-basiert):** Timer alle 2-5 Sekunden triggert Refresh der gesamten Liste
   - **Option B (Event-basiert):** Abonnieren von `KiAusfuehrungsService.CliProcessStatusChanged` und einzelne Aufgabe in `AktiveAufgabenListe` updaten
   - **Option C (Hybrid):** Event + Fallback-Timer für Heartbeat-Ablauf-Erkennung
   - Option B/C sind effizienter; Option A ist einfacher implementierbar

4. **Collection-Updates**:
   - `AktiveAufgabenListe` ist `ObservableCollection<Aufgabe>` — Property-Change-Notifications der einzelnen Aufgaben werden nicht automatisch an UI weitergeleitet
   - Lösung: Aufgabe in Collection neu setzen (`collection[index] = updatedAufgabe`) oder Collection-Item `PropertyChanged`-Events triggern
   - Alternative: `AktiveAufgabeViewModel` mit eigenem Status-Property und Binding zum Converter

5. **Abhängigkeiten**:
   - `KiAusfuehrungsService.CliProcessStatusChanged` Event
   - `AufgabeRecoveryService` für Heartbeat-Timeout-Erkennung
   - Datenbankabfrage bzw. In-Memory-Daten-Aktualisierung

### Relevante Events und Hooks
- `KiAusfuehrungsService.CliProcessStatusChanged` — Ausgelöst wenn CLI startet/stoppt
- `Process.Exited` (intern in `KiAusfuehrungsService`) — Ausgelöst wenn Prozess beendet
- Heartbeat-Updates (vom `CliProcessManager` oder `AufgabeService`)

## Konfiguration

Die Status-Anzeige ist nicht konfigurierbar; die Update-Frequenz könnte optional konfigurierbar sein:
- Ist eine Konfiguration der Refresh-Rate wünschenswert (Einstellungen > Terminal > "Status-Aktualisierungsintervall")? **Offene Frage**
- Standardwert: 2-5 Sekunden bei Timer-basiert; Event-basiert hat keine feste Frequenz

## Offene Fragen

1. **Aktualisierungsstrategie:**
   - Soll Timer-basiert (alle N Sekunden), Event-basiert (bei `CliProcessStatusChanged`) oder Hybrid (Event + Timer-Fallback) implementiert werden?
   - Wie oft soll der Status abgerufen/aktualisiert werden? (2s, 5s, 10s?)

2. **Herzschlag-Mechanismus:**
   - Woher kommt der Heartbeat (`LastHeartbeatUtc`)? Von der CLI-Ausgabe, von `CliProcessManager`, oder vom Datenbank-Polling?
   - Wie wird die Heartbeat-Zeit aktualisiert, wenn kein neuer Prozess-Status kommt?

3. **Performance bei vielen Aufgaben:**
   - Falls 20 Aufgaben gleichzeitig angezeigt werden, kann eine 2-Sekunden-Refresh-Rate die DB überlasten? Muss Caching implementiert werden?

4. **Collection-Update-Mechanismus:**
   - Sollte nur die betroffene Aufgabe in der Collection aktualisiert werden (einzelnes Item), oder die ganze Liste neu geladen werden?
   - Bindungsmodell: Wird das `PropertyChanged`-Event der `Aufgabe`-Entity abonniert, oder wird ein separates View-Model (`AktiveAufgabeViewModel`) verwendet?

5. **Timeout-Erkennung:**
   - Wenn `LastHeartbeatUtc` älter als 5 Minuten wird, wechselt Status zu `"✓ Bereit"` — triggert dies auch eine UI-Aktualisierung?

6. **Dashboard vs. Seitenleiste:**
   - Sollen beide separat aktualisiert werden, oder teilen sie eine gemeinsame Datenquelle?
   - Ist Synchronisierung zwischen beiden Ansichten garantiert?

7. **Fehlerbehandlung:**
   - Was soll angezeigt werden, wenn die Statusabfrage fehlschlägt (Netzwerkfehler, DB-Fehler)? Zeitstempel des letzten erfolgreichen Status beibehalten?

8. **Visuelle Rückmeldung:**
   - Sollte der Status mit Animationen oder Übergängen angezeigt werden, oder einfacher Text + Symbol-Wechsel?
   - Wünsch sich der Kunde auch akustische Benachrichtigungen bei Statuswechsel?
