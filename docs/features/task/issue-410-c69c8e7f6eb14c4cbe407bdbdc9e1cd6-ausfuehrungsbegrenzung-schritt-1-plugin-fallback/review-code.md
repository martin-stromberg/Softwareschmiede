# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine offenen Befunde.

Zwei im Review identifizierte Punkte wurden vor Abschluss dieses Reviews direkt behoben und anschließend per Build (0 Warnungen/0 Fehler) sowie vollem Testlauf (2591/2591 bestanden) verifiziert:

1. **Fehlende Deduplizierung der Ignoriert-Liste** (`PluginSelectionService.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync`, `ki.plugin.default`-Stufe): `ignored.Add(trimmed)` wurde ohne Deduplizierungs-Guard aufgerufen; identischer Wert auf zwei Stufen (z. B. `plugins.default.*` und `ki.plugin.default`) hätte den Eintrag in `IgnoredPluginPrefixes` verdoppelt. Behoben: gleicher `OrdinalIgnoreCase`-Dedup-Guard wie in der lokalen `ResolveEnabledPrefix`-Funktion.
2. **Duplizierte Matching-Logik** (`PluginSelectionService`): Der PluginName-/PluginPrefix-Abgleich war in der neuen Methode und in `ResolveConfiguredDefaultKiPluginPrefixAsync` identisch ausformuliert. Behoben: private statische Hilfsmethode `FindKiPlugin(IReadOnlyList<IKiPlugin>, string)` (Z. 242-247), beide Aufrufer nutzen sie; die fachlich unterschiedlichen Log-Meldungen („nicht installiert" vs. „nicht installiert oder nicht aktiviert") bleiben an den Aufrufstellen.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`
- `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs`

## Einordnung der geprüften Punkte

- **Struktur:** Neue Auflösungsmethode (~75 Zeilen) hat eine einzelne Verantwortlichkeit (konfigurierte Kette gegen die aktivierte Plugin-Liste); wiederkehrende Stufenlogik ist in die lokale Funktion `ResolveEnabledPrefix` gekapselt. Stil deckt sich mit den vorhandenen Methoden der Datei.
- **Kopplung:** Aktivierungsprüfung läuft ausschließlich über den vorhandenen `PluginActivationService`-Einstiegspunkt; der Orchestrator ruft weiterhin `ResolveDevelopmentAutomationPluginStrictAsync` für den eigentlichen Start (Health-Check/Ladbarkeit inklusive).
- **Fehlerbehandlung:** Kein neuer catch-Block; der bisherige `InvalidOperationException`/`IOException`/`UnauthorizedAccessException`-Pfad greift unverändert für explizite Auswahl, Health-Check- und Startfehler.
- **Testqualität:** Jeder neue Test prüft genau einen fachlichen Fall in Arrange-Act-Assert-Struktur; Dispatch-Tests prüfen beobachtbares Verhalten (Status, Reason, `NextAttemptUtc`, Protokolleintrag, Zustellung) statt Implementierungsdetails.
- **Ablaufverantwortung:** Dispatch bleibt der einzige Einstiegspunkt (`DispatchCoreAsync`); kein zweiter Ausführungspfad wird nachgebaut — Tests nutzen `CreateIncomingEvent`/`RetryAsync`/`DispatchAsync` in produktiver Reihenfolge.
- **Toter Code / Namenskonventionen:** Keine toten Mitglieder; Benennung folgt dem vorhandenen Schema (`ResolveDevelopmentAutomationPlugin...`, deutsche Domänenbegriffe in Meldungen).
