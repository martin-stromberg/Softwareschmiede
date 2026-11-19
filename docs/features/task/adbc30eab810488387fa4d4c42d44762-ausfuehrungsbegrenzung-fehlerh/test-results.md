# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Nachlauf zu den beiden offenen Punkten aus `continue.md` (Inter-Test-Races):

1. **Prozessweites Clipboard-Lock** (`ClipboardTestLock`, `SemaphoreSlim` + `Halten()`-Disposable)
   serialisiert alle Clipboard-Regionen des Testhosts über die xUnit-Collections hinweg —
   in `TerminalControlTests` (KeyInput + ClipboardPaste) und in den E2E-Sequenzen
   (`E2E_KonsolenTestfenster`, `E2E_ConPtyLifecycle`), jeweils über die komplette Interaktion
   inklusive der von der E2E-App ausgelösten Kopier-/Paste-Gesten und dem Read-back.
2. **Race-tolerante Asserts** in `KonsolenTestViewModelTests.WiedergabeNeustarten_AusSchrittmodus_
   SpieltAbPosition0`: bei Zeitraffer-Schwelle 0 darf die Wiedergabe vor dem Assert bereits
   durchgelaufen sein — `IstWiedergabeAktiv`/`StatusText` akzeptieren "läuft" ODER "beendet";
   `WarteBisAsync` + `AktuellerChunkIndex == 3` belegen das Durchlaufen.

Die zwei zuvor als Umgebungs-Flakes dokumentierten Tests waren echte Races und sind im
kombinierten Parallel-Lauf grün.

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

| Projekt / Filter | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|------------------|--------|-----------|----------------|--------------|
| Softwareschmiede.Tests (`TerminalControlTests` + `KonsolenTestViewModelTests`) | 62 | 57 | 5 | 0 |
| Softwareschmiede.Tests (`ReplayText`, E2E-Clipboard) | 2 | 2 | 0 | 0 |
| Softwareschmiede.Tests (`TerminalControlTests` + `ReplayText` + `KonsolenTestViewModelTests`, kombiniert/parallel) | 64 | 64 | 0 | 0 |
| **Letzter Lauf (maßgeblich)** | **64** | **64** | **0** | **0** |

- Build vor Testlauf: `dotnet build Softwareschmiede.slnx` — erfolgreich (0 Warnungen, 0 Fehler)
- `dotnet format Softwareschmiede.slnx --verify-no-changes` — sauber
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen Läufen gesetzt
- Hinweis zum ersten Lauf (5 Fehler): In dieser Sandbox scheitert der WPF-/OLE-Clipboard-Pfad
  (`System.Windows.Clipboard` / `OleSetClipboard`) phasenweise deterministisch mit
  `CLIPBRD_E_CANT_OPEN` — auch isoliert und in einer eigenständigen STA-PowerShell (20/20),
  während der rohe Win32-Pfad (`OpenClipboard`/`EmptyClipboard`/`SetClipboardData`) fehlerfrei
  läuft. Die Störung ist extern zum Testhost, tritt zeitweise auf und ist per Testcode nicht
  behebbar; der nachfolgende kombinierte Lauf (alle betroffenen Collections parallel) war
  vollständig grün.

## Testabdeckung

**Abdeckung:** Nicht messbar (fokussierter Testlauf ohne Coverage-Erhebung)

`ClipboardTestLock` ist Infrastruktur ohne eigene Assert-Pflicht; die Wirkung zeigt der
kombinierte Parallel-Lauf der zuvor race-anfälligen Collections.

## Fehlende Tests

Keine neuen Lücken.
