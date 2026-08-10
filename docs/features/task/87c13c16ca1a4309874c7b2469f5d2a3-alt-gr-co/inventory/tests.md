# Tests-Übersicht

## Testklassen

### `KeyToVt100EncoderTests`

Datei: `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.cs`

**Hinweis:** Diese Testklasse behandelt ausschließlich `EncodeClipboardText()`. Es existieren **keine Tests** für die `Encode(KeyEventArgs e)`-Methode.

#### Testmethoden

| Methode | Was wird getestet? |
|---------|-------------------|
| `EncodeClipboardText_SingleLineText_ReturnsUtf8Bytes` | Einzeiliger Text wird unverändert als UTF-8-Bytes kodiert |
| `EncodeClipboardText_MultiLineTextWithLF_ConvertsToCarriageReturn` | Text mit LF (\n) wird zu CR (\r) normalisiert |
| `EncodeClipboardText_MultiLineTextWithCRLF_ConvertsToCarriageReturn` | Text mit CRLF (\r\n) wird zu einzelnem CR (\r) normalisiert |
| `EncodeClipboardText_UnicodeCharacters_ReturnsValidUtf8` | Unicode-Zeichen (Umlaute, Emojis) werden korrekt kodiert |
| `EncodeClipboardText_EmptyString_ReturnsEmptyArray` | Leerer String ergibt leeres Byte-Array |
| `EncodeClipboardText_Null_ReturnsEmptyArray` | null ergibt leeres Byte-Array |
| `EncodeClipboardText_SpecialCharactersAndTabs_PreservedInUtf8` | Tabs und Sonderzeichen bleiben erhalten |
| `EncodeClipboardText_LoneCarriageReturn_StaysSingleCarriageReturn` | Einzelnes CR (\r) ohne LF bleibt unverändert |

---

## Fehlende Tests

Die folgenden Aspekte sind **nicht getestet** und müssen bei der Implementierung der neuen Features berücksichtigt werden:

### Für `KeyToVt100Encoder.Encode(KeyEventArgs e)`

- **Alt Gr-Unterstützung:**
  - Alt Gr + alphanumerische Tasten
  - Alt Gr + Sonderzeichen (pro Tastaturlayout)
  - Unterscheidung zwischen Alt und Alt Gr
  
- **Ctrl+Pfeiltasten:**
  - Ctrl+Left (wortweise Navigation links)
  - Ctrl+Right (wortweise Navigation rechts)
  - Korrekte VT100-Sequenzen (\x1b[1;5D für Ctrl+Left, \x1b[1;5C für Ctrl+Right)

- **Grenzfälle:**
  - Ctrl+Alt+Zeichen
  - Shift+Ctrl+Zeichen
  - Mehrfach-Modifier-Kombinationen
  - Tasten ohne definierte Sequenzen

### Für `TerminalControl`

- Integration von Alt Gr-Eingaben über `OnPreviewKeyDown()` und `OnTextInput()`
- Korrekte Weiterleitung von Ctrl+Pfeiltasten an den Input-Stream

### Für `PseudoConsoleSession`

- Integrationstests: VT100-Sequenzen in echten CLI-Prozessen testen
- Beispiel: Alt Gr + "@" und Ctrl+Links sollten vom Terminal korrekt interpretiert werden

---

## Struktur der bestehenden Tests

- **Test-Framework:** xUnit (Fact-Attribute)
- **Assertion-Bibliothek:** FluentAssertions (.Should() API)
- **Kodierung:** UTF-8 mit `Encoding.UTF8.GetBytes()`
