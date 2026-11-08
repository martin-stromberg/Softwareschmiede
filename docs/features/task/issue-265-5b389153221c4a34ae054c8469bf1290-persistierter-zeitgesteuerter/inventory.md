# Bestandsaufnahme: Persistierter zeitgesteuerter Prompt (Issue #265)

Bestandsaufnahme des bestehenden Mechanismus „zeitgesteuerter Prompt-Versand" (`PromptZeitVersandService` und die angeschlossenen ViewModels, Services und Tests) bezogen auf die Anforderung, geplante Prompts dauerhaft zu persistieren und — soweit technisch möglich — über das Verlassen der Detailansicht bzw. App-Neustarts hinweg zuzustellen. Quelle: `requirement.md` (Issue #265, Aufgaben-ID `5b389153-221c-4a34-ae05-4c8469bf1290`).

## Zusammenfassung

- **Vorhanden:** `PromptZeitVersandService` (Singleton) verwaltet geplante Prompts pro Aufgabe in einer reinen In-Memory-Warteschlange (`Dictionary<Guid, ScheduledPromptEntry>` mit `ITimer` via `TimeProvider.CreateTimer`). Der Klassenkommentar sagt explizit: „Es gibt keine Persistierung — die Verzögerung ist rein sitzungsgebunden."
- **Storno-Punkte, die Persistenz brechen würden:** `TaskDetailViewModel.Dispose()` (Zeile 1559) und `OnCliProcessStatusChanged` bei CLI-Stopp (Zeile 1532) rufen `CancelScheduledPrompt` — ein geplanter Prompt überlebt heute weder das Verlassen der Detailansicht noch einen CLI-Stopp. `EntwicklungsprozessService.AbschliessenCoordinatedAsync` (Zeile 438) storniert beim Aufgabenabschluss.
- **Keine Persistenz-Artefakte vorhanden:** Es existiert keine Entity/Tabelle/DbSet/Migration für geplante Prompts. Auf `Aufgabe` existieren die flachen Spalten `VorschlagPrompt`/`VorschlagAusfuehrenAbUtc` (`AufgabeService.SavePromptVorschlagAsync`/`ClearPromptVorschlagAsync`), die fachlich dem „Prompt-Vorschlag" dienen — eine mögliche Anknüpfungsstelle, die die Anforderung als abzuwägende Option (offene Frage 8) nennt.
- **Vorhandene Erweiterungspunkte:** `PromptZeitVersandService` besitzt bereits eine optionale `IServiceScopeFactory` (Konstruktor-Parameter `scopeFactory`), über die der Singleton scoped Services nutzt (heute: `AufgabeService.GetByIdAsync` für `PausiertBisUtc`). Das `AddHostedService`-Muster und die Basisklasse `PeriodicBackgroundService` existieren für einen Start-/Hintergrunddienst. Migrationen laufen beim App-Start via `db.Database.MigrateAsync()` (`App.xaml.cs` Zeile 96); `DateTimeOffset`-Properties werden projektweit über `UnixMillisConverter`/`NullableUnixMillisConverter` als `long` gespeichert.
- **Zustellpfad:** `ITerminalSession.WritePromptAsync` über `KiAusfuehrungsService.GetTerminalSession(aufgabeId)`; Sessions liegen prozessweit in `_handles` (`ConcurrentDictionary<Guid, CliProcessHandle>`) und überdauern bereits heute das Verlassen der Detailansicht. Ohne aktive Session wird der Prompt bei Fälligkeit still verworfen (Log-Warnung in `SendPromptAsync`).
- **Anzeige:** `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` in `TaskDetailViewModel` (Text „Prompt in Wartestellung"), `AktiveAufgabePanelItem.HasScheduledPrompt` → `KiAusfuehrungsStatusKlassifikation` → `⏳ Prompt in Wartestellung` in der aktiven Aufgabenliste. Beide lesen ausschließlich die In-Memory-Warteschlange.
- **Tests vorhanden:** Unit-Tests für Service (8 Tests), TaskDetailViewModel (8 Tests), MainWindowViewModel (2 Tests), Status-Converter (3 Tests), EntwicklungsprozessService (1 Test) sowie FlaUI-E2E-Szenarien (`ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E`, Teil von `ConPty_WeitereLebenszyklusSzenarien_E2E`; Session-Limit-Szenario in `General_RepositoryUndAufgaben_E2E`).

**Test-Ausgangszustand:** Build `dotnet build Softwareschmiede.slnx` erfolgreich (0 Warnungen, 0 Fehler). Stabiler Lauf `dotnet test --filter "Category!=OsInterface"` mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`: **2166 erfolgreich, 0 fehlgeschlagen, 1 übersprungen** (Exit-Code 0). Nachgewiesene bestehende Testfehler: keine. Testlücke: Die komplette `Category=OsInterface`-Lane (inkl. aller FlaUI-E2E-Tests, darunter die zwei anforderungsrelevanten E2E-Szenarien) wurde nicht ausgeführt — Begründung und Nachweis in [tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
