# UI- und Paste-Pfad

## Relevante Dateien

- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

## Aktueller Ablauf

`TerminalControl` ist der direkte Renderer und Eingabe-Adapter fuer eine `PseudoConsoleSession`. Die aktive Session kommt ueber die Dependency Property `Session`; beim Wechsel registriert das Control `BufferChanged`, uebernimmt den Session-Buffer und rendert neue Ausgabe.

Fuer `Ctrl+V` wird `OnPreviewKeyDown` speziell behandelt:

- nur wenn `Key.V` mit `ModifierKeys.Control` gedrueckt wird;
- nur wenn `Session?.InputStream` vorhanden ist;
- `e.Handled` wird gesetzt;
- `ReadClipboardAndInsertAsync` wird fire-and-forget gestartet.

`ReadClipboardAndInsertAsync` liest den Clipboard-Text ueber `System.Windows.Clipboard`, bricht bei leerem Text ab, ruft `KeyToVt100Encoder.EncodeClipboardText(text)` auf und schreibt die resultierenden Bytes ueber `WriteToInputStreamAsync` auf den Input-Stream der aktuellen Session.

`WriteToInputStreamAsync` ruft `await Session!.InputStream!.WriteAsync(bytes).ConfigureAwait(false)` auf und danach `Session.MarkInputActivity()`. Ein explizites `FlushAsync` gibt es hier nicht. Fehler werden geloggt, aber nicht an UI oder Aufrufer propagiert.

## Kodierung und Zeilenumbrueche

`KeyToVt100Encoder.EncodeClipboardText` behandelt `null`/leer als leeres Bytearray. Fuer alle anderen Texte wird `PseudoConsoleSession.NormalizeToCarriageReturn(text)` verwendet und danach UTF-8 kodiert.

Die Normalisierung wandelt alle Zeilenende-Varianten zu einem einzelnen `\r`:

- `\r\n` -> `\r`
- `\n` -> `\r`
- alleinstehendes `\r` bleibt `\r`

Normale Texteingabe (`OnTextInput`) nutzt `EncodeText` und damit keine Newline-Normalisierung. Enter-Tasten werden als einzelnes `0x0D` kodiert. Damit ist Clipboard-Paste absichtlich an die Tastatur-Enter-Konvention angeglichen.

## Auffaellige Punkte

- Der Clipboard-Text wird als ein Bytearray erzeugt. Im UI-Pfad gibt es aktuell kein internes Chunking.
- Die Methode greift nach dem Clipboard-Lesen erneut ueber `Session` auf die aktuelle Session zu. Falls die Session waehrend des asynchronen Paste-Vorgangs wechselt oder beendet wird, ist der urspruengliche Zielkontext nicht stabil gebunden.
- Mehrere `Ctrl+V`-Operationen oder parallele UI-/Prompt-Schreibvorgaenge koennen ohne gemeinsame Serialisierung auf denselben Input-Stream schreiben.
- Clipboard-Paste hat keinen expliziten Flush, anders als `PseudoConsoleSession.WritePromptAsync`.
- Der fire-and-forget-Aufruf macht den Abschluss des Paste-Vorgangs fuer den UI-Handler nicht beobachtbar. Fehler werden geloggt, aber ein partieller Write waere fuer den Nutzer nicht direkt sichtbar.

## Relevanz fuer die Anforderung

Im UI-Handler selbst wurde keine Logik gefunden, die bewusst nur den letzten Abschnitt eines langen Textes uebernimmt. Die groessten Risiken liegen in fehlender Zielsession-Stabilisierung, fehlender Write-Serialisierung und fehlender Flusskontrolle/Flush fuer laengere Eingaben.
