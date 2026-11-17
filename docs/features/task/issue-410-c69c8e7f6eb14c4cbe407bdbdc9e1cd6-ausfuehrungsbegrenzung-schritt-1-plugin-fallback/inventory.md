# Bestandsaufnahme: Plugin-Fallback für SCM-Ereignisausführungen (Schritt 1, #411)

Ist-Stand der Plugin-Auflösung im SCM-Dispatch-Pfad sowie der Test-Infrastruktur, bezogen auf `requirement.md` im selben Verzeichnis. Projektweite Vorgeschichte: `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/inventory.md` (dort § 1.4, § 3, § 6).

## Zusammenfassung

- `PluginSelectionService.ResolveDevelopmentAutomationPluginWithProjectScopeAsync` (Z. 103-126) löst die Kette „Aufgaben-Prefix → Projekt-Default → Global-Default (`plugins.default.*`) → `ki.plugin.default`-Einstellung" auf, **prüft aber weder Installation noch Aktivierung** — sie gibt den gespeicherten Prefix ungeprüft zurück.
- `PluginSelectionService.ResolveDevelopmentAutomationPluginStrictAsync` (Z. 88-94) prüft Aktivierung über `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync` und wirft `InvalidOperationException("Die ausgewählte CLI ist nicht mehr installiert oder aktiviert.")`.
- `ScmEventOrchestrator.DispatchCoreAsync` (Z. 164-167) ruft im SCM-Pfad zuerst die projektbezogene Auflösung (Prefix, aktivitätsblind) und danach die strikte Auflösung (wirft bei deaktiviertem/nicht installiertem Plugin) auf. Der `catch` in Z. 250-255 setzt bei `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` `NextAttemptUtc = long.MaxValue` — harte Sperre, kein Fallback, kein automatischer Wiedereinstieg.
- `EntwicklungsprozessService` startet im SCM-Pfad mit `strictPluginSelection: true` (Orchestrator Z. 172/174) und ruft intern erneut `ResolveDevelopmentAutomationPluginStrictAsync` auf (Z. 162-164, 285-287). Der erfolgreiche Start schreibt den tatsächlich verwendeten Prefix per `AufgabeService.UpdateAsync` in `Aufgabe.KiPluginPrefix` zurück (Z. 183-188, 288-293).
- Wartezustand existiert: `WaitAsync`/`WaitWithCommentAsync` setzen `Status = "Wartend"` + `Reason` (Z. 535-549); `WaitWithCommentAsync` legt zusätzlich eine deterministische „waiting"-Outbox-Kommentaraktion an. Der Dispatch-Poll wählt alle Ereignisse mit `NextAttemptUtc <= jetzt` — ein Wartezustand **ohne** `NextAttemptUtc` bleibt startbar und wird beim nächsten Poll erneut versucht.
- `ProtokollService.AddEintragAsync` schreibt Protokolleinträge je Aufgabe; im Orchestrator bereits für PR-Verknüpfung und Screenshot-Übersprünge genutzt (Z. 473-476, 519-523).
- **Lücke:** Es existiert keine aktivierungsbewusste Variante der Auflösungskette und kein „ehrlich wartender" Pfad für „kein ausführbares Plugin konfiguriert" — nur `long.MaxValue`-Sperre oder die generische „Keine Standard-CLI konfiguriert"-Wartemeldung.

Test-Ausgangszustand: reguläre Lane (`Category!=OsInterface`) vollständig grün — 2580 erfolgreich, 0 fehlgeschlagen, 3 übersprungen (Details und Nachweis in [Tests](inventory/tests.md)).

## Details

- [Datenmodell und Einstellungsschlüssel](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
