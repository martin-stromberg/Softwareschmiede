# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Die `requirement.md` enthält keinen explizit nummerierten Abschnitt „Akzeptanzkriterien"; als Prüfbasis dienen daher der Fachliche Scope (Verarbeitungseinheit, auslösende Ereignisse, erwartete Wirkung, Umgang mit fehlenden Objekten, Scope-Ausschlüsse) sowie die bestätigten Anwender-Entscheidungen vom 2026-11-07.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Geplanter Prompt wird dauerhaft persistiert (Entity + DbSet + Migration, ein Plan pro Aufgabe) | Neue Entity `ZeitgesteuerterPrompt`, `DbSet`, `OnModelCreating` mit Unique-Index auf `AufgabeId` + `DeleteBehavior.Cascade`, Migration `AddZeitgesteuerterPrompt`; `UpsertAsync` vor Timer-Armierung in `SchedulePromptAsync` | `SchedulePromptAsync_SchreibtDatensatz`, `SchedulePromptAsync_ErneutesPlanen_ErsetztDatensatz`, E2E-DB-Check via `OpenTestDbContext` | Abgedeckt |
| Plan überlebt Verlassen der Detailansicht | `TaskDetailViewModel.Dispose` ruft `CancelScheduledPrompt` nicht mehr auf; `LadenAsync` befüllt Anzeige aus `GetScheduledPromptStatus`; `ScheduledPromptChanged`-Event als Ersatz-Synchronisation | Umgekehrter Test `Dispose_StorniertGeplantePrompts`, `LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus`, E2E Verlassen/Wiederöffnen | Abgedeckt |
| Plan überlebt CLI-Stopp / wird bei nächster Session zugestellt | `OnCliProcessStatusChanged`-Storno entfällt; Eintrag geht in „wartet auf Session"; Nachzustellung bei `CliProcessStatusChanged(Gestartet)` über neues Service-Abo | Umgekehrter Test `OnCliProcessStatusChanged_CliGestoppt_…`, `CliGestartet_MitWartendemPlan_ZustelltNach` | Abgedeckt |
| Plan überlebt App-Neustart (Re-Arm beim Start) | `RestoreScheduledPromptsAsync()` in `App.OnStartup` nach `MigrateAsync`; begründete Entscheidung gegen `IHostedService` (Hosted Services starten vor `MigrateAsync`) | `Restore_ZukuenftigerPlan_ArmiertTimer`, `Restore_UeberfaelligerPlan_WartetAufSession`, `Restore_VerwaisterDatensatz_WirdGeloescht`, `Restore_NachNeustart_LiefertBeiSessionStartZu` | Abgedeckt |
| Versand an laufende Session auch ohne geöffnete Detailansicht | Zustellpfad unverändert über `KiAusfuehrungsService.GetTerminalSession`/`_handles`; Session überdauert View-Lebenszyklus bereits heute | Akzeptanzszenario 1 (Happy Path) + `Versand_LoeschtDatensatz` | Abgedeckt |
| Manueller Storno-Button (Anwenderentscheid, IM Scope) | Button „Wartestellung aufheben" mit `AutomationProperties.Name="WartestellungAufheben"` im sichtbarkeitsgesteuerten Status-`StackPanel` (TaskDetailView.xaml ~154–163); `CancelScheduledPromptCommand` (`AsyncRelayCommand`) → `CancelScheduledPromptAsync`; Anzeigeleerung über `ScheduledPromptChanged` | Unit `CancelScheduledPromptCommand_EntferntPlanUndAnzeige`; E2E-View-Hilfsmethode `CancelScheduledPrompt()`; E2E-Szenario in erweiterter `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` inkl. DB-Check auf gelöschten Datensatz | Abgedeckt |
| Protokolleintrag (`SystemMeldung`) bei Hintergrund-/Nachzustellung (Anwenderentscheid) | `SchreibeVersandProtokollAsync(aufgabeId, nachzustellung)` via scoped `ProtokollService.AddEintragAsync` über vorhandene `_scopeFactory`; Reihenfolge: `WritePromptAsync` → Datensatz löschen → `PromptSent`/`ScheduledPromptChanged` → Protokolleintrag; Nachzustellung immer, Timer-/Sofortversand nur ohne `PromptSent`-Abonnent; Fehler nur Warn-Log | `Nachzustellung_SchreibtSystemMeldung`, `Versand_OhneAbonnent_SchreibtSystemMeldung`, `Versand_MitAbonnent_KeinProtokollEintrag` (Negativtest), `ProtokollEintrag_BeiGeloeschterAufgabe_BrichtVersandNicht` | Abgedeckt |
| Sendefehler → Plan behalten + Retry bei nächstem Session-Start, kein TTL | Alle Fehlschlag-Fälle (keine Session, `ObjectDisposedException`, sonstiger `WritePromptAsync`-Fehler) → „wartet auf Session", Datensatz bleibt; einziger Wiederaufnahmeweg `CliProcessStatusChanged(Gestartet)`; explizit kein TTL/Verfall (Festlegung 2026-11-07) | `Sendefehler_BehaeltPlanUndWartetAufSession`, `Timer_OhneSession_BehaeltPlanUndWartetAufSession`, Akzeptanzszenarien 9+10 | Abgedeckt |
| Aufgabenabschluss storniert Plan inkl. Datensatz; Löschung per Cascade | `EntwicklungsprozessService.AbschliessenCoordinatedAsync` → `await CancelScheduledPromptAsync` (löscht Datensatz + Eintrag); `DeleteBehavior.Cascade`; verwaiste In-Memory-Einträge als harmlos bewertet | Angepasster `AbschliessenAsync_ShouldCancelScheduledPrompt_WhenPromptGeplant`, `Restore_VerwaisterDatensatz_WirdGeloescht` | Abgedeckt |
| Anzeige spiegelt persistierten Zustand (`ScheduledPromptStatus`, `⏳`/`HasScheduledPrompt`) | `GetScheduledPromptStatus` bleibt synchrone Quelle; `ScheduledPromptChanged` für Detailansicht; `MainWindowViewModel` unverändert (periodischer `_aktualisierungsTimer`); Restore befüllt In-Memory-Warteschlange | `LadenAsync_MitGeplantemPrompt_ZeigtWartestellungStatus`, `ScheduledPromptChanged_AktualisiertAnzeige`, `Restore_*`-Tests; `MainWindowViewModelTests` fachlich unverändert gültig | Abgedeckt |
| Pausenverschiebung aktualisiert persistierten Zielzeitpunkt | `HandleTimerElapsedAsync` Pause-Zweig ruft `UpdateZielZeitpunktAsync`; Re-Arm lädt Zielzeit auf Pausenende vor | `Pause_Verschiebung_AktualisiertDatensatz` + angepasste Pause-Tests | Abgedeckt |
| Scope-Ausschlüsse 1+2 bestätigt, Ausschluss 3 reduziert (keine Datumsauswahl; Storno im Scope) | Scope-Ausschluss-Tabelle: alle drei Ausschlüsse mit Begründung und Status `bestätigt (2026-11-07)`; Aufnahme des Stornos in den Scope explizit dokumentiert | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (entfällt bei Status „Plan vollständig")

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Plan anlegen → Wartestellung sichtbar → Detailansicht verlassen → Datensatz bleibt in DB → Wiederöffnen → Storno-Button klicken → Anzeige weg + Datensatz gelöscht | Erweiterung der bestehenden Methode `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (`E2E_ZeitgesteuerterPrompt.cs`, aufgerufen in `ConPty_WeitereLebenszyklusSzenarien_E2E`) — Konsolidierungsregel eingehalten; Hilfsmethode `CancelScheduledPrompt()` in E2E-View `TaskDetailView.cs`; DB-Nachweis via `OpenTestDbContext` | Abgedeckt |
| Versand im Hintergrund bei geschlossener Detailansicht (Happy-Path-Kern) | UI-Anteil (Verlassen/Wiederöffnen/Status) im erweiterten E2E-Szenario; der eigentliche Hintergrundversand an die laufende Session ist ohne ConPTY-Session nicht E2E-treibbar | Abgedeckt (Kombination E2E + Service-Ebene, begründet) |
| App-Neustart → Re-Arm → Zustellung | Kein echter FlaUI-Neustart geplant — begründet (zweiter App-Launch + ConPTY in dieser Sandbox nicht lauffähig, `OsInterface`-Lane); Nachweis stattdessen auf Service-Ebene (`Restore_NachNeustart_LiefertBeiSessionStartZu`) | Nicht erforderlich mit Begründung |
| Sofortversand-Regression | `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` als unveränderte Regression mitlaufen lassen | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

— (entfällt bei Status „Plan vollständig")

## Hinweise

- **Pause-Test-Provider:** Die bestehenden Pause-Tests (`SchedulePromptAsync_AktivePause_…`, `Timer_BeiNachPlanungGesetzterPause_…`) nutzen `CreatePausedTaskProviderAsync`, das bisher nur `AufgabeService` im Scope registriert. Sobald `SchedulePromptAsync` den `ZeitgesteuerterPromptStore` per Scope auflöst, muss dieser Provider den Store ebenfalls registrieren (bzw. durch `CreateStoreProviderAsync` ersetzt werden) oder der Store muss tolerant per `GetService` (statt `GetRequiredService`) aufgelöst werden — Implementierungsdetail, das im Plan nur implizit über „Muster `CreatePausedTaskProviderAsync`" abgedeckt ist.
- **Sofortversand ohne Datensatz:** Beim Sofortversand (Zielzeit ≤ jetzt) wird bewusst nichts persistiert; schlägt dieser Versand fehl, entsteht ein „wartet auf Session"-Eintrag ohne Datensatz, der einen App-Neustart nicht überlebt. Der Plan begründet das explizit („nichts zu wiederherstellen") — fachlich vertretbar, da Sofortversand keine „Wartestellung" ist, aber eine bewusst akzeptierte Rest-Einschränkung gegenüber „kein Plan geht verloren".
- **`CliProcessStatusChanged(Gestartet)` in Unit-Tests auslösen:** Für `CliGestartet_MitWartendemPlan_ZustelltNach`/`Nachzustellung_SchreibtSystemMeldung` muss das Event testseitig ausgelöst werden können (bisherige Tests registrieren Sessions nur per Reflection in `_handles`). Wie das Event im Test ausgelöst wird (Reflection auf den Event-Handler oder simulierter Prozessstart), sollte bei der Umsetzung geklärt werden.
