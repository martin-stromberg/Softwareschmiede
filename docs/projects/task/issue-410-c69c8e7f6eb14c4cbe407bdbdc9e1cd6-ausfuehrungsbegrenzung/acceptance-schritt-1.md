# Abnahmeprüfung – Entwicklungsschritt 1

Geprüft: Plugin-Fallback für SCM-Ereignisausführungen (#411), Branch
`task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-1-plugin-fallback`
(Commits `414f3a06` + `bf310040`), Diff-Basis `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung` (Merge-Base `369053a2`).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Prüfnachweis gegen die Akzeptanzkriterien

**AK 1 – Fallback auf globales Standard-Plugin:** `PluginSelectionService.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` (`src/Softwareschmiede/Application/Services/PluginSelectionService.cs:137-211`) löst die Kette Aufgaben-Prefix → Projekt-Default (`GetProjectDefaultPluginPrefixAsync`) → Global-Default (`plugins.default.*`) → konfigurierte Standard-CLI (`ki.plugin.default`) in korrekter Reihenfolge auf — identisch zur bisherigen, nicht aktivierungsbewussten Kette (`:103-126`). Jede Stufe wird nur akzeptiert, wenn der konfigurierte Wert in der Liste `GetEnabledDevelopmentAutomationPluginsAsync` (installiert + ladbar + aktiviert, `PluginActivationService.cs:59-60`) enthalten ist. Dispatch-Nachweis: `ScmEventOrchestratorTests.DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers` (deaktiviertes Aufgaben-Prefix → Zustellung über den globalen Standard, `task.KiPluginPrefix` persistiert auf den tatsächlich verwendeten Prefix).

**AK 2 – Verweigerter Start ohne harte Sperre:** `ScmEventOrchestrator.DispatchCoreAsync` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs:164-181`): Liefert die Auflösung `null` bei ignorierten Zuordnungen, wird ein verständlicher Wartegrund gesetzt (benennt die ignorierten Zuordnungen samt Handlungshinweis), ein `ProtokollTyp.SystemMeldung`-Eintrag geschrieben (dedupliziert auf Grundwechsel, `:176-177`) und `WaitWithCommentAsync` → Status `Wartend`. `WaitAsync` (`:552-557`) setzt `NextAttemptUtc` nicht — das Ereignis bleibt poll-fähig (`ScmEventMonitoringService.cs:235-240` selektiert `NextAttemptUtc == null || <= now`) und startet nach Konfigurationskorrektur automatisch im nächsten Zyklus bzw. über „Erneut prüfen" (`RetryAsync`, `:293-303`). Testnachweis: `UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix` (assertiert `NextAttemptUtc == null`, Protokolleintrag mit Prefix, erfolgreiche Zustellung nach Reaktivierung + `RetryAsync`). Kein willkürliches Ersatz-Plugin: Die Auflösung gleicht ausschließlich konfigurierte Werte gegen die aktivierte Liste ab; der „erstbeste aktivierte Plugin"-Fallback (`ResolvePluginAsync`, `:392-395`) wird auf diesem Pfad nicht verwendet.

**AK 3 – Alle nicht-ausführbaren Zustände / Strenge der expliziten Wahl:** Deaktiviert, nicht installiert und nicht ladbar kollabieren an der API-Grenze einheitlich zu „nicht in `GetEnabledDevelopmentAutomationPlugins()`" bzw. „nicht in der aktivierten Liste" — alle drei Zustände werden identisch als ignorierte Zuordnung behandelt (Unit-Tests für deaktiviert und nicht-installiert; nicht-ladbar ist an dieser Stelle von nicht-installiert nicht unterscheidbar). Ein ausführbarer explizit gewählter Prefix (`explicitPluginPrefix`) umgeht die Auflösungskette vollständig (`:164`) und geht in `ResolveDevelopmentAutomationPluginStrictAsync` — keine Fallback-Logik. Ein deaktivierter expliziter Prefix erzeugt unverändert den `InvalidOperationException`-Pfad mit `NextAttemptUtc = long.MaxValue` (`:267-272`, aufhebbar via `RetryAsync`) — kein stiller Fallback. Testnachweis: `ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable`.

**AK 4 – Tests:** 8 neue Unit-Tests in `PluginSelectionServiceTests.cs` decken alle Zweige ab (aktivierte Aufgaben-Stufe gewinnt; deaktiviertes/nicht-installiertes Aufgaben-Prefix → Projekt-/Global-Default; deaktivierter Projekt-Default → Global; deaktivierter Global-Default → `ki.plugin.default`; alle Stufen deaktiviert → `null` + benannte Ignoriert-Liste; nichts konfiguriert → `null` + leere Liste; deaktivierter Einstellungen-Default → `null`). 3 neue Dispatch-Tests über `ScmDispatchFixture` (echtes SQLite, echte DI-Verdrahtung, gemockt nur die Terminal-Grenze) + 1 angepasster Bestandstest (`UnresolvableSettingsDefaultCli_KeepsWaitingState` prüft jetzt den neuen, die ignorierte Zuordnung benennenden Wartegrund).

### Eigenverifikation

- Build + fokussierter Testlauf eigenständig ausgeführt:
  `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~PluginSelectionServiceTests|FullyQualifiedName~ScmEventOrchestratorTests"` → Build 0 Warnungen/0 Fehler, **52/52 Tests bestanden** (beinhaltet alle neuen Tests und den kompletten Bestand beider Testklassen).
- Der im Lifecycle-Report (`docs/features/task/.../test-results.md`) behauptete Voll-Lane (2591 bestanden / 3 übersprungen / 0 Fehler) wurde nicht komplett wiederholt; die fokussierte Lane deckt alle geänderten Testklassen ab.

### Randbeobachtungen (keine Abweichungen)

- **Fall „gar keine Zuordnung konfiguriert"** (kein Aufgaben-/Projekt-/Global-Default, kein `ki.plugin.default`): Es greift der unveränderte Bestandspfad „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." (`:182-183`) — Wartegrund sichtbar, Ereignis wiederaufnehmbar, SCM-Wartekommentar via `WaitWithCommentAsync`, aber **kein** `SystemMeldung`-Protokolleintrag. Nicht als Abweichung gewertet: Das AK bindet den Protokolleintrag an „nicht ausführbar/aktiv" (eine konfigurierte, gebrochene Zuordnung — dort wird er geschrieben); der Fall ganz ohne Zuordnung war nie Teil des ersetzten Defekts (InvalidOperationException → `long.MaxValue`) und ist unverändertes Bestandsverhalten.
- **Vor dem Fix bereits auf `long.MaxValue` gesperrte Alt-Ereignisse** bleiben gesperrt, bis „Erneut prüfen" gedrückt wird (`RetryAsync` setzt `NextAttemptUtc = null`, danach greift die neue Auflösung). Keine Migration erforderlich — die Anforderung fordert Wiederaufnehmbarkeit nach Konfigurationskorrektur, die über diesen Weg gegeben ist.
- **Health-Check-/Startfehler eines aufgelösten (aktivierten) Plugins** laufen weiter über den gezielten `long.MaxValue`-Pfad (`:185`, `:267-272`) — das ist bewusstes Bestandsdesign für Start-/Konfigurationsfehler (deliberate retry via „Erneut prüfen"), nicht der ersetzte „Zuordnung nicht ausführbar"-Fall.
- **Erfolgreicher Fallback** hinterlässt keinen Aufgabenprotokoll-Eintrag — die ignorierte Zuordnung wird per `ILogger.LogWarning` protokolliert (`:163-165`); die Anforderung fordert Sichtbarkeit nur für den verweigerten Start.
- Die nicht-aktivierungsbewusste Variante `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` bleibt für die UI-Pfade (`TaskDetailViewModel.cs:966/1393/1495/1599`) bestehen — dort steht der Plugin-Auswahldialog dahinter; der SCM-Dispatch nutzt ausschließlich die neue Variante.
- Dokumentation konsistent: `changes.log`, `docs/RELEASE_NOTES.md`, `README.md` und die Hilfe-Seite `troubleshooting.md` beschreiben das Verhalten zutreffend.
