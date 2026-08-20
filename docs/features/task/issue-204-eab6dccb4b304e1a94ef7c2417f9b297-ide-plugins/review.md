# Plan-Review: Multi-Plugin-Aggregation für IDE-Dropdown (Schritte 15–25)

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync(string repositoryPath, CancellationToken ct = default)` existiert exakt mit der spezifizierten Signatur und Rückgabetyp `Task<IReadOnlyList<IIdePlugin>>` (`src/Softwareschmiede/Application/Services/PluginSelectionService.cs`, neue Methode nach `ResolveIdePluginAsync`). Verhalten deckungsgleich mit Plan: `ArgumentException.ThrowIfNullOrWhiteSpace`, No-Enabled-Plugin-Fallback auf `[GetDefaultIdePlugin()]`, `ApplyIdePluginOrder`-Wiederverwendung, Aufteilung in `explicitPlugins`/`fallbackPlugins` über `CheckCompatibilityAsync`, Default-Plugin-Fallback wenn beide Listen leer, sonst `explicitPlugins.Concat(fallbackPlugins).ToList()`.
- [x] `TaskDetailViewModel.ErmittleAggregierteIdeEinstiegspunkteAsync(string lokalerKlonPfad, CancellationToken ct)` existiert mit exakt dem spezifizierten Rückgabetyp `Task<(string EffectiveWorkdir, IReadOnlyList<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)> Eintraege)>` und identischem Ablauf (effektives Arbeitsverzeichnis → `ResolveAlleKompatiblenIdePluginsAsync` → je Plugin `FindEntryPointsAsync` → Tupel-Aggregation unter Beibehaltung der Plugin-/Entry-Point-Reihenfolge).
- [x] `TaskDetailViewModel.FormatiereAnzeigeWert(IIdePlugin plugin, IdeEntryPoint entryPoint)` implementiert exakt die spezifizierte Logik (Bezeichnung aus `DisplayName ?? Path.GetFileName(Path)`, Präfix nur wenn Bezeichnung ungleich `PluginName`).
- [x] `BerechneKannIdeAuswaehlen` auf `private static bool BerechneKannIdeAuswaehlen(int entryPointCount) => entryPointCount >= 2;` umgestellt; beide Aufrufer (`AktualisiereKannIdeAuswaehlenAsync` mit `eintraege.Count`, `OeffneIdeInternAsync` mit `aggregierteEintraege.Count` bzw. `eintraege.Count`) übergeben passend `.Count`.
- [x] `AktualisiereKannIdeAuswaehlenAsync` ruft jetzt `ErmittleAggregierteIdeEinstiegspunkteAsync` statt `ErmittleIdeEntryPointsAsync` auf; Fehlerbehandlung (Catch setzt `KannIdeAuswaehlen = false`, kein `FehlerMeldung`) unverändert.
- [x] `WaehleEntryPointAsync` auf Tupel-Signatur `Task<(IIdePlugin Plugin, IdeEntryPoint EntryPoint)?>` umgestellt; Anzeige-Strings über `FormatiereAnzeigeWert`, Rückführung über `anzeigeWerte.IndexOf(gewaehlterWert)` statt Stringgleichheit auf `IdeEntryPoint`.
- [x] `OeffneIdeInternAsync` korrekt angepasst:
  - Callback-Parametertyp exakt wie spezifiziert geändert.
  - Haupt-Button-Zweig (`waehleEntryPointAsync == null`) bleibt beim Single-Plugin-Pfad (`ErmittleIdeEntryPointsAsync`, öffnet `entryPoints[0]` über das dort aufgelöste `plugin`); berechnet zusätzlich `KannIdeAuswaehlen` über einen separaten Aufruf von `ErmittleAggregierteIdeEinstiegspunkteAsync`.
  - Dropdown-Zweig nutzt ausschließlich `ErmittleAggregierteIdeEinstiegspunkteAsync`; öffnet bei 1 Eintrag direkt über `eintraege[0].Plugin`, bei ≥2 über das von `waehleEntryPointAsync` zurückgegebene `gewaehlt.Value.Plugin` (nicht über das ursprünglich für den Haupt-Button aufgelöste Plugin — im Dropdown-Zweig existiert dieses ohnehin nicht mehr).
- [x] Alle 6 Tests unter „Neue Tests — Erweiterung Multi-Plugin-Aggregation" für `TaskDetailViewModelTests_IdeAuswahl` mit exakt den im Plan genannten Namen angelegt und prüfen inhaltlich das beschriebene Verhalten:
  `WaehleEntryPointAsync_WithEntryPointsFromTwoPlugins_ShowsBothInDialog`,
  `WaehleEntryPointAsync_SelectingEntryFromFallbackPlugin_OpensViaThatPlugin_NotViaResolvedPlugin`,
  `WaehleEntryPointAsync_OrdersExplicitPluginEntriesBeforeFallbackPluginEntries`,
  `FormatiereAnzeigeWert_ForVisualStudioEntryPoint_UsesPluginNamePrefixAndFileName`,
  `FormatiereAnzeigeWert_ForVisualStudioCodeEntryPoint_UsesPluginNameOnly`,
  `KannIdeAuswaehlen_WhenEachCompatiblePluginHasExactlyOneEntryPoint_ButMultiplePluginsCompatible_ReturnsTrue`.
- [x] Alle 6 Tests für `PluginSelectionServiceTests_IdePlugin` mit exakt den im Plan genannten Namen angelegt:
  `ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnExplicitAndFallbackPlugins_WhenBothCompatible`,
  `ResolveAlleKompatiblenIdePluginsAsync_ShouldOrderExplicitPluginsBeforeFallbackPlugins`,
  `ResolveAlleKompatiblenIdePluginsAsync_ShouldRespectPluginOrder_FromSetting_WithinEachGroup`,
  `ResolveAlleKompatiblenIdePluginsAsync_ShouldExcludeIncompatiblePlugins`,
  `ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnDefaultPlugin_WhenNoPluginActive`,
  `ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnDefaultPlugin_WhenNoPluginCompatible`.
- [x] Alle 5 in „Betroffene bestehende Tests" gelisteten Testanpassungen korrekt vorgenommen:
  - `KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse` und `KannIdeAuswaehlen_NachLadenAsync_WhenOneEntryPoint_ReturnsFalse` nutzen jetzt `CreateSut(idePlugins: [visualStudioPlugin])` (Single-Plugin-Isolation).
  - `OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf`, `WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` und `WaehleEntryPointAsync_UsesDisplayNameInDialog` nutzen jetzt das neue `"{PluginName}: {…}"`-Anzeigeformat in Dialog-Mock-Rückgabe und Verify-Prädikaten.
- [x] `ResolveIdePluginAsync` selbst unverändert (Diff zeigt nur eine neue Methode danach eingefügt, keine Änderung am bestehenden Methodenkörper).
- [x] `ErmittleIdeEntryPointsAsync` (Single-Plugin, Haupt-Button) unverändert (nicht im Diff enthalten, weiterhin die einzige Fundstelle im Code).

## Offene Aufgaben

Keine.

## Hinweise

- `KannIdeAuswaehlen_WhenOpenEntryPointFailsWithMultipleEntryPoints_BleibtTrue` (bereits mit isoliertem Single-Plugin-Setup) wurde laut Plan nur zur Verifikation erneut auszuführen, nicht anzupassen — im Diff entsprechend unverändert; das ist plankonform.
- Die im Plan unter Schritt 25 als „ggf." markierte neue E2E-Testklasse `E2E_TaskDetailView_IdeAuswahl.cs` für das Szenario „mehrere kompatible IDE-Plugins gleichzeitig aktiv" wurde nicht geprüft, da dieser Review-Auftrag laut Vorgabe ausschließlich Schritte 15–25 der Multi-Plugin-Aggregation abdeckt und Schritt 25 selbst als optional/bedingt formuliert ist. Aussage dazu bewusst ausgeklammert, da kein Vergleichsziel für Unit-Test-fokussierte Prüfung; falls gewünscht, gesonderte Prüfung von E2E-Testdateien nachreichen.
- Für Test `KannIdeAuswaehlen_WhenEachCompatiblePluginHasExactlyOneEntryPoint_ButMultiplePluginsCompatible_ReturnsTrue` sowie mehrere neue Tests wird implizit auf den Standard-Plugin-Satz (VS + VS Code) über `CreateSut()` ohne Override zurückgegriffen — konsistent mit der im Plan beschriebenen zentralen Regressionsanforderung.
- Kein Code wurde im Rahmen dieses Reviews verändert; alle Prüfungen basieren auf `git diff` gegen den unveränderten Arbeitsstand.
