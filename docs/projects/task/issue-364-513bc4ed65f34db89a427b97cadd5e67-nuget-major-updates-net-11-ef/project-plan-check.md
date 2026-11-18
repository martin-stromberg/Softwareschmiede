# Projektplan-Prüfung – NuGet-Major-Updates (#364)

Prüfung von `project-plan.md` gegen `requirement.md` und `inventory.md`.
(Hinweis: In dieser Umgebung ohne Unteragenten selbst geprüft, als eigenständige Phase
nach Erstellung des Plans — dokumentierte Abweichung in todo.md.)

## Ergebnis

**Status:** Projektplan vollständig

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung (requirement.md) | Abgedeckt durch | Status |
|------------------------------|-----------------|--------|
| 1. TFM-Umstellung aller Projekte auf net11.0( -windows) | Schritt 1 | ✓ |
| 1. Microsoft.*/EFCore.* → 11.0.x, kein Mischstand | Schritt 2 | ✓ |
| 1. CI-/Release-Toolchain auf .NET 11 (implizit nötig für CI-grün) | Schritt 1 | ✓ |
| 2. xunit → xunit.v3 (beide Testprojekte, Metapaket) | Schritt 3 | ✓ |
| 2. Xunit.SkippableFact-Kompatibilität prüfen | Schritt 3 (explizit) | ✓ |
| 2. xunit.runner.visualstudio 4.x als Ausführungspfad | Schritt 3 (explizit) | ✓ |
| 3. SQLitePCLRaw.lib.e_sqlite3 → 3.x, gebündelt mit EF-Sprung | Schritt 2 | ✓ |
| SCM-Vorsequenz (Release abwarten, Admin-Merge #419, Rebase) | todo.md-Vorsequenz (Precondition, kein Entwicklungsschritt — korrekt, da kein lieferbarer Code) | ✓ |
| PR gegen staging mit Closes #364 | Abschlussphase (todo.md + SCM-Instruktion) | ✓ |
| AK: Build/Tests/Format grün, keine Legacy-Versionen | je Schritt eigene AK + Abschlusskontrolle | ✓ |
| AK: EF-Migrations-Upgrade-Sicherheit | Schritt 2 (expliziter Nachweis) | ✓ |
| Nicht-Ziel CPM | Vorgehensentscheidung 4 + Offener Punkt 3 | ✓ |
| Nicht-Ziel sonstige Paketupdates | Schritte beschränken sich auf genannte Blöcke | ✓ |

## Abhängigkeitsprüfung

- Schritt 1: keine Abhängigkeiten — zulässig als Start.
- Schritt 2 → 1: EF Core 11 verlangt net11.0-TFM (Issue-Feststellung) — korrekt, azyklisch.
- Schritt 3 → 2: fachlich wäre 1 ausreichend; 2 gewählt, da beide Schritte dieselben
  Testprojekt-csproj anfassen — sequentiell konfliktärmer, azyklisch, verzögert keinen
  parallelen Pfad (alles läuft ohnehin sequentiell in einem Workflow).

## Fehlende oder unvollständige Punkte

- Keine. Alle Muss-Anforderungen sind testbaren Schritten mit Akzeptanzkriterien
  zugeordnet; Nicht-Ziele dokumentiert; offene Punkte enthalten übernommene Vorschläge
  und keine Nutzerentscheidung (kein Scope-/Priorisierungskonflikt — Auftrag ist
  durch Issue + Kommentar eindeutig).

## Hinweise

- Jede Schrittbeschreibung ist als vollständige Lifecycle-Eingabe verwendbar
  (Ziel, Konkretisierung, Akzeptanzkriterien, betroffene Bereiche benannt).
- Der Testanzahl-/Discovery-Check in Schritt 3 beugt „still verschwindenden" Tests vor.
- Schritt 2 verweist auf den EF-Migrationsbestand; der `entityframework-database`-Skill
  wird dort zusätzlich herangezogen.
