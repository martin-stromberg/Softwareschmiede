# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Lauf nach der Pool-Auslagerung (`ScmStartPlatzReservierungsPool` als DI-Singleton statt prozessweiter statischer Reservierungsbestand). Fokus auf die vom Fix betroffenen Suites; der ehemals verwundbare Race-Test `Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts` ist im vollständig parallelen Integrationslauf grün.

Die zwei in `continue.md` dokumentierten Umgebungs-Flakes (`KonsolenTestViewModelTests.WiedergabeNeustarten_*`, `TerminalControlTests.OnPreviewKeyDown_*` / CLIPBRD_E_CANT_OPEN) wurden in diesem Lauf nicht erneut ausgeführt — sie stehen außerhalb des Fix-Umfangs und bleiben dokumentiert offen.

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

| Projekt / Filter | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|------------------|--------|-----------|----------------|--------------|
| Softwareschmiede.IntegrationTests (`Category!=OsInterface`, komplett) | 352 | 352 | 0 | 0 |
| Softwareschmiede.Tests (`Category!=OsInterface`, Filter: `ScmEventExecutionLimitServiceTests` + `ScmEventOrchestratorTests` + `ScmEventCancelTests`) | 47 | 47 | 0 | 0 |
| **Summe** | **399** | **399** | **0** | **0** |

- Build vor Testlauf: `dotnet build Softwareschmiede.slnx` — erfolgreich (0 Warnungen, 0 Fehler)
- `dotnet format Softwareschmiede.slnx --verify-no-changes` — sauber
- `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen Läufen gesetzt
- OS-Interface-Tests (`Category=OsInterface`) sind nicht Teil dieses Laufs

## Testabdeckung

**Abdeckung:** Nicht messbar (fokussierter Testlauf ohne Coverage-Erhebung; vorheriger Vollauf in Iteration 3: 90,50 %)

Der neue Typ `ScmStartPlatzReservierungsPool` ist über die bestehenden Reservierungs-Tests
(`ScmEventExecutionLimitServiceTests.PruefeUndReserviereStartPlatzAsync_*`,
`ScmEventDispatchTests_Limits.*`) indirekt vollständig abgedeckt — sämtliche
Reservierungs- und Zulassungspfade laufen über ihn.

## Fehlende Tests

Keine neuen Lücken — siehe Anmerkung zur Abdeckung oben.
