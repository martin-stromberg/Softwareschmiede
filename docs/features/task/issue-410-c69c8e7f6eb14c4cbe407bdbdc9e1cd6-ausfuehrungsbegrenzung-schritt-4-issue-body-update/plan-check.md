# Plan-Prüfung: SCM-Antwort-Art „Issue-Beschreibung aktualisieren" (Schritt 4)

**Status: Plan vollständig**

Geprüft: `requirement.md`, `inventory.md`, `plan.md` gegen die verbindliche Anforderung (Projektplan Schritt 4, Entscheidung 5) und den Testbedarf.

## Abdeckung der Akzeptanzkriterien

| Kriterium | Planabdeckung | Bewertung |
|-----------|---------------|-----------|
| `ScmResponseRequest`/`ScmResponseKind` erlauben Issue-Beschreibungs-Update | §1: neuer Enum-Wert `IssueBodyUpdate` am Enum-Ende + Vertragsdoku | ✅ |
| `IScmEventResponder` unterstützt die Art abgleichsicher (kein Doppelschreiben, Erkennung bereits angewendeter Aktionen) | §2: Reconcile-vor-Schreiben (Marker im Remote-Body), `AlreadyExisted`; Unit- + Integrationstests | ✅ |
| GitHub-Plugin setzt Beschreibung remote; typisierte Providerfehler mit korrekter Wiederhol-/Sperrsemantik | §2: `PATCH issues/{n}` über `EventApiAsync`/`EvaluateResponse` (Mutation → `OutcomeUncertain`; 401/403 → Sperre, 429/5xx → Wiederholung, 404/422 → Permanent); Fehler-Matrix als Theory-Tests | ✅ |
| Ausführung ausschließlich über Outbox mit deterministischer Aktions-ID (konsistent zu receipt/waiting) | §3: Zweck `"issuebody"` + `ScmEventStore.ActionId`; Nachhol-Schleife auf „alle Nicht-`ai:`-Zwecke" umgestellt | ✅ |
| Unit- und Integrationstests (ScmDispatchFixture/ScmEventDispatchTests-Muster) für Ausführung, Reconcile und Fehlerfälle | §4: 5 Unit- + 5 Integrationsfälle benannt | ✅ |

## Verbindliche Design-Hinweise

| Hinweis | Erfüllt |
|---------|---------|
| Execute UND Reconcile | ✅ beide Pfade geplant |
| Deterministische Aktions-ID | ✅ `ActionId(eventId, "issuebody")` |
| Plugins ohne Fähigkeit → ehrlicher Fehler | ✅ Vertragsdoku + `Permanent`-Fehler + Integrationstest |
| Titel nicht betroffen | ✅ `title` wird nie gesendet; Unit-Assert auf fehlendes Feld |
| Kein echter GitHub-Call; Mock-/Integrationsebene reicht | ✅ `FakeGitHubEventApi`-Transport, Fixture-Responder-Mock |

## Geprüfte Risiken

- **Enum-Persistenz:** numerische JSON-Serialisierung → Anhängen am Ende festgeschrieben. ✅
- **Nachhol-Schleife:** Whitelist-Filter als Lücke erkannt und durch `!StartsWith("ai:")` geschlossen; `ai:`-Zwecktrennung und `receipt`-Nebeneffekt geprüft — `ExecuteAsync` ist early-out-idempotent, EF-Übersetzung von `StartsWith` nach `LIKE` für SQLite gesichert. ✅
- **Marker im Issue-Body:** bewusste, zum Kommentar-Muster konsistente Entscheidung; Vorteil für Schleifenschutz (Schritt 5/6) dokumentiert. ✅
- **Kein produktiver Aufrufer:** bewusster Scope (Schritt 6 liefert ihn); Fähigkeit ist über `EnsureAsync`+Dispatch trotzdem vollständig nachweisbar. ✅

## Testvollständigkeit

Akzeptanzszenarien 1–5 sind jeweils mindestens einem konkreten Test zugeordnet; keine UI-/E2E-Pflicht (keine UI-Änderung). Die dokumentierte Nicht-Durchführbarkeit der realen End-to-End-Abnahme folgt der Aufgabenstellung („kein echter GitHub-Call").

**Ergebnis: Plan vollständig — keine Lücken festgestellt.**
