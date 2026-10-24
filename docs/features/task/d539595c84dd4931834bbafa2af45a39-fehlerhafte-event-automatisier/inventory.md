# Bestandsaufnahme: Versionsbereiche und ehrliche Fehlermeldung in der SCM-CLI-Erkennung

Anforderung: [`requirement.md`](requirement.md) — Erkennung unterstützter CLI-Versionen über Versionsbereiche statt exakt gepinnter Banner-Strings und ursachenehrliche Warte-/Fehlertexte bei unbekannter bzw. nicht unterstützter CLI-Version.

## Ausgangszustand

| Merkmal | Wert |
|---|---|
| Datum/Uhrzeit | 2026-10-24 10:33 (MESZ, `W. Europe Standard Time`, UTC+02:00) |
| Branch | `task/d539595c84dd4931834bbafa2af45a39-fehlerhafte-event-automatisier` |
| Commit | `f18f506e2dba0c4892df65c6f641d833b060b046` — „test: Desktopweite UIA-Enumerationen durch prozess-scoped Win32-Fensterliste ersetzen" |
| Uncommittete Änderungen | nur untracked: `docs/features/task/d539595c84dd4931834bbafa2af45a39-fehlerhafte-event-automatisier/` (Anforderungs-/Inventory-Verzeichnis selbst) |
| .NET SDK | 10.0.402 |
| Laufzeiten | Microsoft.NETCore.App 8.0.32 / 9.0.21 / 10.0.13; Ziel-Framework `net10.0` (App: `net10.0-windows10.0.17763.0`) |
| Laufende `Softwareschmiede.App.exe` | keine zum Build-Zeitpunkt (kein Dateilock-Risiko) |

## Ergebnis der Bestandsaufnahme (Kurzfassung)

- **Zentraler Mechanismus existiert bereits** und ist genau an den in der Anforderung benannten Stellen verdrahtet: `ScmPromptSessionAdapter.RecognizeStartup` prüft aktuell exakte `Contains`-Strings (`OpenAI Codex (v0.114.0)`, `Claude Code v2.1.0`, `Devin CLI` + `v3000.11.92`, `SCM_SIMULATOR_PROTOCOL_V1`), `_recognizedCli` ist ein `bool` mit drei effektiv verlorenen Unterscheidungen („unbekannte CLI" vs. „bekannte CLI in anderer Version" vs. „unterstützt").
- **Der Versionsstring wird nirgends extrahiert oder gespeichert** — es gibt kein Feld/Property, das die tatsächlich erkannte Version für Fehlermeldungen bereitstellt. `IScmPromptSessionAdapter` besitzt keine Erkennungs- oder Versionseigenschaft.
- **Die drei betroffenen Grundtexte existieren wörtlich:** „Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen." (`ScmEventOrchestrator.cs:178`), „CLI beschäftigt; wartet auf Eingabebereitschaft." (ebd. Zeilen 178/182), „CLI-Version oder Initialisierung ist nicht durch ein unterstütztes Promptformat bestätigt." (`ScmPromptSessionAdapter.cs:163`).
- **UI-Kopplung per Textabgleich:** `ScmEventQueueItem.CanConfirmReady` (`ScmEventQueueViewModel.cs:333`) blendet den Button `ScmEventConfirmReady` (`ScmEventQueueView.xaml:117-118`) nur ein, wenn `Reason` „Eingabebereitschaft unbekannt" enthält.
- **Testabdeckung ist vorhanden und grün** auf den betroffenen Pfaden: Banner-Fixtures mit gepinnter Version in `ScmKiProfile.Devin`, den `InlineData` von `ScmEventCliExchangeAccessTests*` und `Banner(provider)` in `.RestrictedProcess.cs`; Warte-Grund-Assertions in `ScmEventOrchestratorTests` und `E2E_ScmEvents` (Zeilen 191, 394, 406, 421).
- **Ausgangslauf:** stabile Lanes vollständig grün (2160/2161 + 219/219 + 9/9); OsInterface-Lane von `Softwareschmiede.Tests` 64/71 mit **2 preexisting Fehlern außerhalb des Anforderungsbereichs** (E2E-Texteingabe-Race, Clipboard-Test) und 5 bewusst übersprungenen ConPTY-Tests (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Der für die Anforderung maßgebliche E2E-Test `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` **bestand** (1 m 55 s).
- **Nicht vorhanden:** eine Profil-/Deskriptorklasse für Promptformate mit Versionsbereichen (in der Anforderung als `ScmCliPromptProfile` o. ä. vorgeschlagen), ein dreiwertiger Erkennungszustand, eine Benennung der erkannten Version in irgendeinem Grundtext, Tests für abweichende/außerhalb liegende Versionen.

## Detaildokumente

| Dokument | Inhalt |
|---|---|
| [`inventory/models.md`](inventory/models.md) | Datenmodellklassen/Records: `ScmIncomingEvent`, `ScmExchangeAccess`, `ScmCliConfigurationFile`, `ScmExchangeAccessProbe`, `ScmEventQueueItem`, `ScmKiProfile` |
| [`inventory/logic.md`](inventory/logic.md) | Logikklassen und Mechanismen: `ScmPromptSessionAdapter`, `ScmEventOrchestrator`, `ScmCliAccessEvaluator`, `ScmEventExchangeProtocol` (Kanalprobe), `ScmCliWorkspaceTrust`, `ScmEventQueueViewModel`, `KiAusfuehrungsService`-Verdrahtung |
| [`inventory/enums.md`](inventory/enums.md) | `ScmPromptReadyStatus`, `ScmExchangeAccessStatus`, Status-Strings des `ScmIncomingEvent` |
| [`inventory/interfaces.md`](inventory/interfaces.md) | `IScmPromptSessionAdapter`, `ITerminalSession` (Adapter-Events), `IScmEventSource`/`IScmEventResponder` (Kontext) |
| [`inventory/tests.md`](inventory/tests.md) | Test-Ausgangszustand: Befehle, Ergebnisse, preexisting Fehler, Skips, betroffene Testartefakte; verlinkte Logs unter [`inventory/test-results/`](inventory/test-results/) |
