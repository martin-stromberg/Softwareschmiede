# Anforderung: CLI Konsole optimieren

**Aufgaben-ID:** 2225d20a-55b8-400b-84f7-2fa448eec08c  
**Feature-Branch:** task/issue-85-2225d20a55b8400b84f72fa448eec08c-cli-konsole-optimieren  
**Erstellt:** 2026-07-07

---

## Fachliche Zusammenfassung

Die Konsolenausgabe in der Pseudo-Console-Integration zeigt Stabilitätsprobleme: Bei der Anzeige von CLI-Ausgaben werden neue Textausgaben unzuverlässig dargestellt und überschneiden sich mit bisheriger Ausgabe. Dies tritt auf, weil die `TerminalBuffer`-Verwaltung und die Rendering-Synchronisierung zwischen `PseudoConsoleSession` (Leseschleife), `TerminalBuffer.Apply()` und `TerminalControl.OnRender()` möglicherweise Race Conditions oder unvollständige Buffer-Synchronisierung aufweisen. Zusätzlich fehlt die Unterstützung für Zwischenablage-Einfügungen (Paste): Der VT100-Encoder (`KeyToVt100Encoder`) und `TerminalControl` unterstützen derzeit keine `Ctrl+V`-Eingaben. Beide Probleme müssen behoben werden: Buffer-Rendering stabilisieren und Clipboard-Paste-Funktionalität hinzufügen.

---

## Betroffene Klassen und Komponenten

### Domain Layer (Softwareschmiede.Domain.Terminal)

- **`TerminalBuffer`** (bestehend)
  - Verwaltet 2D-Grid aus `TerminalCell`-Objekten mit Größe, Cursor-Position, Farbattributen, Scrollback
  - Methode `Apply(TerminalEvent evt)` ist intern synchronisiert via `lock`, aber Synchronisierung zwischen Leseschleife und Rendering-Cycle muss überprüft werden
  - Neue Methode oder Überprüfung: **`ApplySynchronized(TerminalEvent evt)` / Async-sichere Variante?** (Klärungsbedarf: Sind Lock-Granularität und Timeout ausreichend?)

- **`TerminalCell`** (bestehend)
  - Record Struct (Zeichen, Farben, Bold/Dim/Underline-Attribute)
  - Keine Änderungen erforderlich

- **`TerminalEvent`** + Subklassen (bestehend)
  - `TextWrittenEvent`, `CursorMovedEvent`, `ColorChangedEvent`, `ScreenClearedEvent`, `LineErasedEvent`, `CursorVisibilityChangedEvent`
  - Neue Subklasse möglich: `ClipboardPasteEvent` (zur Vereinheitlichung des Daten-Flusses), falls Clipboard-Text zentral über Parser/Events verarbeitet wird

### Infrastructure Layer (Softwareschmiede.Infrastructure.Terminal)

- **`PseudoConsoleSession`** (bestehend, ggfs. erweitern)
  - Startet `ReadLoopAsync` bei Konstruktion; läuft unabhängig vom `TerminalControl`-Lebenszyklus (Issue-86)
  - `BufferChanged`-Event wird nach jedem erfolgreichen Parser-Chunk gefeuert
  - Überprüfung: Wird `BufferChanged` vor oder nach `Apply(event)` gefeuert? (Synchronisierungs-Critical)
  - Ggfs. neue Thread-sichere Methode: `async WriteClipboardBytesAsync(byte[] bytes)` zum Schreiben in `InputStream` mit Rate-Limiting

- **`AnsiSequenceParser`** (bestehend)
  - Zustandsbehaftete VT100-Zustandsmaschine
  - Parst Eingabe-Bytes zu `TerminalEvent`-Instanzen
  - Keine direkte Änderung erforderlich für Clipboard-Paste (wird über `KeyToVt100Encoder` und `InputStream` bewältigt)

- **`KeyToVt100Encoder`** (erweitern oder neue Methode)
  - Statische Klasse; konvertiert WPF `KeyEventArgs` zu VT100-Sequenzen
  - Neue statische Methode: `EncodeClipboardText(string text)` → `byte[]`
    - Konvertiert Zwischenablage-Text zu VT100-Sequenzen oder Rohtext (Klärungsbedarf)
    - Behandelt Multi-line-Text (Newlines → `\r` oder `\r\n`?)
    - Zeichensatz-Encoding (UTF-8 vs. ASCII)

### Presentation Layer (Softwareschmiede.App.Controls)

- **`TerminalControl`** (erweitern)
  - WPF `FrameworkElement` mit `PreviewKeyDown` und `TextInput` Event-Handling
  - Neue Event-Handler oder Erweiterung bestehender:
    - `PreviewKeyDown`: Zusätzlich auf `Ctrl+V` prüfen → `ReadClipboardAndInsert()`
    - Neue Methode: `ReadClipboardAndInsert()` private
      - Liest `System.Windows.Forms.Clipboard.GetText()` (oder WPF-Äquivalent `System.Windows.IDataObject`)
      - Konvertiert via `KeyToVt100Encoder.EncodeClipboardText(text)` oder direkt zu Bytes
      - Schreibt asynchron in `session.InputStream` via `await session.InputStream.WriteAsync(bytes)`
      - Aufrufen von `session.MarkInputActivity()` zum Aktualisieren des Runtime-Status
  - Synchronisierungs-Überprüfung: Timing zwischen `InvalidateVisual()` und Buffer-Updates

### Tests & Validierung

- **Neue Unit-Tests** oder Erweiterung bestehender Tests:
  - `TerminalBufferTests`: Thread-Sicherheit von `Apply()` mit parallelen Schreibzugriffen
  - `KeyToVt100EncoderTests`: Clipboard-Text-Encoding (Single-line, Multi-line, Sonderzeichen, Unicode)
  - `TerminalControlTests`: `Ctrl+V`-Eingabe-Handling; Clipboard-Lesefehler (z.B. keine Berechtigung)
- Integrations-Tests: Parallele CLI-Ausgabe + Clipboard-Paste sollten nicht blockieren oder überschneiden

---

## Implementierungsansatz

### 1. Buffer-Synchronisierung verbessern (Stabilitäts-Fix)

**Ziel:** Sicherstellen, dass `TerminalBuffer.Apply()` und `TerminalControl.OnRender()` keine Race Conditions oder inkonsistente Buffer-States erzeugen.

**Strategie:**

1. **Analysieren der aktuellen Synchronisierung:**
   - Überprüfung: Ist `lock`-Granularität in `TerminalBuffer.Apply()` ausreichend? (Derzeit: Lock auf gesamte `Apply`-Operation)
   - Timing von `BufferChanged`-Event: Wird es vor oder nach `lock` freigegeben gefeuert?
   - Überprüfung: Schützt der Lock auch die Grid-Zugriffe in `TerminalControl.OnRender()`? (Müssen Zellen-Lesezugriffe auch gelockt sein)

2. **Potenzielle Verbesserungen (je nach Befund):**
   - `BufferChanged` als **Atomic Operation:** Event sollte nach vollständigem Update und Lock-Release gefeuert werden (Double-Check-Locking oder separate Event-Signalisierung)
   - Render-Cycle Synchronisierung: `OnRender()` sollte über Grid-Snapshot arbeiten, nicht direkt Grid-Zugriffe unterm Lock durchführen
   - Alternative: Copy-on-Read für kritische Zugriffe (z.B. `GetSnapshot()` Methode in `TerminalBuffer`)

3. **Code-Änderungen:**
   ```csharp
   // Beispiel: Bessere Synchronisierung
   public void Apply(TerminalEvent evt)
   {
       lock (_gridLock)
       {
           // Apply changes
           ApplyEventUnsafe(evt);
           // Neue Variablen-Markierung für konsistenten Zustand
           _bufferVersion++;
       }
       // Event außerhalb Lock, aber mit Versionsmarkierung
       BufferChanged?.Invoke(this, EventArgs.Empty);
   }
   ```

### 2. Clipboard-Paste-Funktionalität implementieren

**Ziel:** `Ctrl+V`-Eingabe in `TerminalControl` abfangen und Zwischenablage-Text in den Prozess-Input-Stream schreiben.

**Strategie:**

1. **Keyboard-Handler erweitern (`TerminalControl.PreviewKeyDown`):**
   ```csharp
   protected override void OnPreviewKeyDown(KeyEventArgs e)
   {
       if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
       {
           e.Handled = true;
           _ = ReadClipboardAndInsertAsync();
           return;
       }
       // Existing handler...
   }
   ```

2. **Clipboard-Lesemethode (`TerminalControl.ReadClipboardAndInsertAsync()`):**
   ```csharp
   private async Task ReadClipboardAndInsertAsync()
   {
       try
       {
           string text = GetClipboardText();
           if (string.IsNullOrEmpty(text))
               return;

           byte[] bytes = KeyToVt100Encoder.EncodeClipboardText(text);
           if (_session != null)
           {
               await _session.InputStream.WriteAsync(bytes, 0, bytes.Length);
               _session.MarkInputActivity();
           }
       }
       catch (Exception ex)
       {
           LogWarning($"Fehler beim Einfügen aus Zwischenablage: {ex.Message}");
       }
   }

   private string GetClipboardText()
   {
       try
       {
           // WPF: Verwende System.Windows.IDataObject
           if (System.Windows.Clipboard.ContainsText())
               return System.Windows.Clipboard.GetText();
       }
       catch
       {
           // Fallback oder Fehlerbehandlung
       }
       return string.Empty;
   }
   ```

3. **Text-zu-Bytes-Konvertierung (`KeyToVt100Encoder.EncodeClipboardText()`):**
   ```csharp
   public static byte[] EncodeClipboardText(string text)
   {
       // Konvertiere Text zu UTF-8 Bytes
       // Behandle Newlines: \n → \r oder \r\n (je nach CLI-Konfiguration)
       // Option 1: Rohtext (ASCII/UTF-8)
       // Option 2: Escape-Sequenzen (falls Sonderzeichen wie Tabs vorhanden sind)
       
       var bytes = new List<byte>();
       foreach (char c in text)
       {
           if (c == '\n')
               bytes.Add((byte)'\r');  // LF → CR
           else if (c == '\r')
               continue;  // Skip CR, nutze nur LF
           else
               bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
       }
       return bytes.ToArray();
   }
   ```

### 3. Fehlerbehandlung & Validierung

- **Clipboard-Lesefehler:** Abfangen von `System.Windows.Forms.IDataObject`-Zugriff-Fehlern (z.B. keine Berechtigung)
- **Große Texte:** Ggfs. Rate-Limiting oder Chunk-weise Eingabe, um Buffer-Überlauf zu vermeiden
- **Zeichensatz-Probleme:** UTF-8-Validierung; Fallback auf ASCII für CLI-Kompatibilität

### 4. Testing-Strategie

- **Unit-Tests für `KeyToVt100Encoder.EncodeClipboardText()`:**
  - Single-line Text
  - Multi-line Text (LF, CRLF, CR)
  - Unicode/Special Characters
  - Empty String / Null
  - Tab-Zeichen

- **Unit-Tests für `TerminalBuffer` Thread-Sicherheit:**
  - Parallele `Apply()` + `GetCells()` Zugriffe
  - Timing zwischen Event-Firing und Lock-Release

- **Integrations-Tests:**
  - `Ctrl+V` drücken → Text aus Zwischenablage wird eingefügt
  - Parallele CLI-Ausführung + Paste sollte nicht blockieren
  - Große Paste-Operationen sollten nicht UI einfrieren

---

## Konfiguration

### Optionale Einstellungen (appsettings.json)

- **`TerminalClipboardPasteEnabled`** (bool, Default: true)
  - Aktiviert/Deaktiviert Clipboard-Paste-Funktionalität

- **`TerminalPasteLineSeparator`** (string, Default: "\r")
  - Zeichen-Sequenz für Zeilenumbrüche in Clipboard-Paste
  - Optionen: `\r` (CR), `\n` (LF), `\r\n` (CRLF)

- **`TerminalPasteMaxBytesPerSecond`** (int, Default: 0 = unlimited)
  - Rate-Limiting für Clipboard-Paste (Bytes pro Sekunde)
  - Verhindert Buffer-Überlauf bei sehr großen Clipboard-Inhalten

- **`TerminalBufferSynchronizationMode`** (enum, Default: "Locked")
  - `Locked`: Aktueller Lock-basierter Ansatz
  - `CopyOnRead`: Copy-Snapshot für Rendering (falls implementiert)

---

## Offene Fragen

1. **Ursache der Ausgabe-Vermischung:**
   - Sind es wirklich Race Conditions in `TerminalBuffer.Apply()` / Rendering, oder liegt das Problem in ANSI-Parser-Zustandsverwaltung?
   - Tritt das Problem nur bei bestimmten CLI-Tools oder CLI-Ausgabe-Mustern auf (z.B. schnelle sequenzielle Ausgaben)?
   - Gibt es bestehende Fehler-Logs oder Reproduktions-Schritte im Ticket?

2. **Buffer-Synchronisierung:**
   - Ist `lock` auf `TerminalBuffer` ausreichend, oder sollte auch `TerminalControl.OnRender()` Grid-Zugriffe locken?
   - Wie lange hält der Lock typischerweise? (Performance-Concern bei häufigen Updates)
   - Sollte ein Copy-on-Read-Ansatz (Snapshot) untersucht werden, um Lock-Contention zu reduzieren?

3. **Clipboard-Paste-Mechanik:**
   - Soll Raw-Text eingefügt werden (UTF-8) oder über VT100-Escape-Sequenzen?
   - Wie sollten Multi-Line-Clipboard-Inhalte behandelt werden? (LF → CR, CRLF → ?, etc.)
   - Sollte es ein Rate-Limiting für sehr große Paste-Operationen geben (um CLI-Input-Buffer nicht zu überladen)?
   - Sollte Paste nur möglich sein, wenn die CLI läuft (`RuntimeStatus == CliRuntimeStatus.WartetAufEingabe`)?

4. **UI-Feedback:**
   - Soll ein visueller Indikator anzeigen, dass Clipboard-Text eingefügt wird? (z.B. kurzes Highlight)
   - Soll eine Fehlermeldung angezeigt werden, wenn Paste fehlschlägt?

5. **Kompatibilität:**
   - Welche Windows/.NET-Versionen müssen Clipboard-Zugriff unterstützen? (WPF `Clipboard` vs. `System.Windows.Forms.Clipboard`)
   - Gibt es Sicherheits-Anforderungen für Clipboard-Zugriff (z.B. Benutzer-Bestätigung bei großen Mengen)?

6. **Testing & Reproduktion:**
   - Welche CLI-Tools/Ausgabe-Szenarien führen zu sichtbarer Vermischung?
   - Kann ein Minimal-Reproduktions-Fall (Test-CLI mit schneller Ausgabe) erstellt werden?

7. **Priorität:**
   - Ist Buffer-Stabilitäts-Fix kritischer als Clipboard-Paste-Feature?
   - Können diese separat oder als atomare Änderung implementiert werden?
