# Plan-Check – Schritt 6

Prüfung des Umsetzungsplans gegen `requirement.md` und den Testbedarf.

## Vollständigkeit gegen Anforderung

- **Entparken/Automatik:** `NextAttemptUtc = null` macht den internen Auftrag
  fällig → Poll/`DispatchDueEvents`/ereignisgesteuerte Startprüfung und
  manuelle Aktionen erreichen ihn. ✅
- **Reguläre, limitierte CLI-Ausführung:** eigener Pfad mit
  `PruefeUndReserviereStartPlatzAsync` (global + pro Plugin), Task-Lease/
  BlockReason, echter `CliStart`/`Neustart`. ✅ #410
- **Prompt:** fester Analyse-Prompt, alle 9 Qualitätskriterien, Titel+Body,
  deterministisch aus persistierten Daten. ✅
- **Plugin-Fallback:** #411-Kette (Aufbereitungs-Plugin → Projekt → Global →
  `ki.plugin.default`), activation-aware; kein Plugin → ehrlicher Wartegrund,
  kein Start, dedupliziertes Protokoll. ✅
- **Ergebnisrücklauf:** `actions/*.json` `issueBodyUpdate`+`comment` →
  `IssueBodyUpdate`/`Comment`-Outbox-Aktionen, idempotent, reconcile-sicher. ✅
- **Ergebnisanwendung:** `IssueBodyUpdate` ersetzt Beschreibung remote, Titel
  unverändert (API-Feld existiert nicht in `ScmResponseRequest`); Kommentar
  nur bei aktivem Schalter, hostseitig erzwungen. ✅
- **Fehler:** Logger + `SystemMeldung`-Protokolleintrag, kein Remote-
  Fehlerkommentar, Issue unverändert (Writes nur nach erfolgreicher Analyse
  über Outbox mit Fehlertypisierung). ✅
- **Einstellungen-UI:** Funktion/Plugin/Repositories/Kommentar persistiert
  und bedienbar. ✅
- **Ehrlichkeit:** Warte- und Unklar-Zustände mit Grund, keine Erfolgsbehaup-
  tung, deduplizierte Protokolleinträge, `VerarbeitungUngeklärt` statt
  stillem Verwerfen. ✅

## Testbedarf-Abgleich

Alle geforderten Testaspekte sind eingeplant (Prompt, Ausführungspfad,
Ergebnisanwendung, Kommentar-Option, Fehlerfälle, SettingsViewModel,
Integration, konsolidierter FlaUI-E2E). ✅

## Risiken

- `request.json`-Bitgleichheit erfordert deterministischen Prompt-Aufbau —
  gelöst (IssueReferenz.Titel + observation.Body; Kommentar-Schalter hostseitig).
- Monitor-Tests aus Schritt 5 ändern ihre Park-Erwartung — bewusst
  angepasst, nicht regressiv.
- `ReconcileAttemptAsync` des generischen Pfads erhält zusätzliches
  `issueBodyUpdate`-Mapping — additive Erweiterung, kein Verhaltensbruch für
  bisherige Kinds.

## Ergebnis

**Plan vollständig** — keine offenen Punkte; Umsetzung kann beginnen.
