# Projektplan-Prüfung – GitHub-Projekte vollständig überwachen und Ereignisse ohne bestehende Aufgabe ausführen

## Ergebnis

**Status:** Projektplan vollständig

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung | Abdeckende(r) Schritt(e) | Abdeckung |
|---|---|---|
| FR-1 (Ist-Zustand: nur aufgabenverknüpfte Referenzen) | Schritt 2 (Fachliche Beschreibung benennt den Ist-Zustand korrekt und liefert die Erweiterung) | vollständig |
| FR-2 (Überwachung aller erfassten + aktivierten GitHub-Repos) | Schritt 2 | vollständig |
| FR-3 (Ereignisverarbeitung ohne Aufgabe → Anlage) | Schritt 3 (baut auf der Erkennung aus Schritt 2 auf) | vollständig |
| FR-4 (Wiederverwendung, keine Duplikate) | Schritt 3 (Dedup-Schlüssel, `DbUpdateException`-Muster, Nebenläufigkeit) | vollständig |
| FR-5 (Auto-Start + Zustellung an Standard-CLI nach Sicherheits-/Owner-Prüfungen) | Schritt 4 (Wiederverwendung des Orchestrator-Pfads: `SetStatusAsync` → `Gestartet`, `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync`, `CanWrite`-Reprüfung) | vollständig |
| FR-6 (fehlende Standard-CLI → wartend + bedienbare Aufforderung) | Schritt 4 (vorhandene Wartelogik „Keine Standard-CLI konfiguriert…" + Wirksamkeitsnachweis für neu angelegte Aufgaben) | vollständig |
| FR-7 (Fortsetzung nach Vertrauenswürdigkeitsabfrage) | Schritt 1 (Probe-Reset/-Wiederholung, Trust-Dialog als eigener Wartezustand, wirksame Wiederholungswege) | vollständig |
| FR-8 (Prüfauftrag CLI-Startparameter zur Trust-Vermeidung) | Schritt 1 (dokumentierte, begründete Empfehlung; Umsetzung nur bei Vereinbarkeit mit `ScmCliAccessEvaluator`-Whitelist) | vollständig |
| FR-9 (Bestandsschutz: Trigger, Owner, Token, Dedup, Ereignisübersicht) | Schritt 4 (explizite Bestandsschutz-Verifikation) plus Bestandsschutz-Akzeptanzkriterien in den Schritten 1–3 | vollständig |
| AK: Neue Issues/PRs → Erkennung + Aufgabenanlage | Schritt 2 + Schritt 3 | vollständig |
| AK: Kommentare/Reviews zu bisher unbekannten Referenzen | Schritt 2 + Schritt 3 | vollständig |
| AK: Wiederverwendung ohne Duplikate | Schritt 3 | vollständig |
| AK: Auto-Start + Zustellung an Standard-CLI | Schritt 4 | vollständig |
| AK: Fehlende Standard-CLI → wartend + Aufforderung | Schritt 4 | vollständig |
| AK: Fortsetzung nach Trust-Bestätigung | Schritt 1 | vollständig |
| AK: Prüfauftrag Startparameter | Schritt 1 | vollständig |
| AK: Bestandsschutz | Schritt 4 (+ Schritte 1–3) | vollständig |
| Nicht-Ziel: keine Webhooks | Vorgehensentscheidung 1 (explizit ausgeschlossen) | eingehalten |
| Nicht-Ziel: keine neuen Trigger-Arten | Übersicht „Nicht betroffen" + Schritt 3 (unveränderte `$Softwareschmiede`-Regeln) | eingehalten |
| Nicht-Ziel: keine Überwachung nicht erfasster/deaktivierter Repos | Schritt 2-Akzeptanzkriterien (`Aktiv == true`, nur GitHub-Plugin) | eingehalten |
| Nicht-Ziel: keine Duplikatanlage / keine nicht-qualifizierten Ereignisse | Schritt 3 (Fachliche Regeln + Akzeptanzkriterien) | eingehalten |

## Abhängigkeitsprüfung

- **Zyklenfreiheit:** Schritt 1: keine Abhängigkeiten; Schritt 2: keine; Schritt 3: → 2; Schritt 4: → 3. Nur Rückwärtsverweise, zyklenfrei.
- **Mergebarkeit:**
  - Schritt 1 ist ein fachlich und technisch unabhängiger Bugfix inkl. Prüfauftrag — eigenständig mergebar.
  - Schritt 2 ist als **„bewusste Zwischenstufe"** ehrlich benannt: Ereignisse ohne zuordenbare Aufgabe laufen weiter in den vorhandenen Diagnosepfad (`aufgabeId == null` → Snapshot-Diagnose „Mehrdeutige Zuordnung zu Aufgaben", kein Inbox-Eintrag, `ScmEventStore.cs:46,61-62,76-77`). Kein Funktionsverlust, eigenständig testbar.
  - Schritt 3 macht die Zwischenstufe wirksam; da der Orchestrator-Dispatch Inbox-Einträge mit gesetzter `AufgabeId` bereits automatisch startet und zustellt, entsteht mit Schritt 3 allein bereits das Zielverhalten — Schritt 4 härtet/verifiziert es Ende-zu-Ende. Kein Schritt verschlechtert Bestandsverhalten.
- **Reihenfolge:** Bugfix (1) → Leseseite (2) → Schreibseite (3) → E2E-Härtung (4) ist plausibel begründet.

## Fehlende oder unvollständige Punkte

Keine.

## Hinweise

- **Stichproben-Verifikation (alle bestätigt):**
  - `ScmEventMonitoringService.cs:25-36` lädt nur Aufgaben mit `GitRepository.Aktiv` und sammelt `IssueReferenz.IssueNummer`/`PullRequests`; Klassenkommentar Z. 12 bestätigt „ausschließlich lokal verknüpfte SCM-Referenzen". Kein Repo-Enumerationspfad — `IScmEventSource.ReadEventsAsync` ist referenzbasiert (`IScmEventSource.cs:12`).
  - `ScmIncomingEvent.AufgabeId` ist nicht-nullable `Guid` (`ScmIncomingEvent.cs:16`); `AcceptBatchAsync` akzeptiert `null` nur als Mehrdeutigkeitsmarker.
  - Trust-Deadlock-Hypothese konsistent zum Code: `_accessProbe` wird genau einmal erzeugt/gesendet (`ScmPromptSessionAdapter.cs:159-167`), danach nur noch `ReadAccessProbeAsync` (Z. 155) — kein Reset/Retry. `ConfirmReady` (Z. 181-192) erzwingt Ready ohne Prüfung auf Blockierdialog; `CanConfirmReady` hängt am Reason-Substring „Eingabebereitschaft unbekannt" (`ScmEventQueueViewModel.cs:295`), der beim Probe-Wartegrund nicht mehr greift — der beschriebene „tote" Wartend-Zustand ist plausibel.
  - Orchestrator: „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." (`ScmEventOrchestrator.cs:143`), `NextAttemptUtc = long.MaxValue` bei `InvalidOperationException` (Z. 209-214), `CanWrite`-/Owner-Reprüfung pro Dispatch (Z. 126-131), CLI-Auflösungskette Aufgabe→Projekt→global (Z. 141) — alle Plan-Beobachtungen korrekt.
  - `ScmCliAccessEvaluator` (Z. 167-196) akzeptiert nur `--model`/`-m`, Codex `--sandbox`/`-s` und `--ask-for-approval`/`-a`, Claude `--permission-mode` — die im Plan genannte Whitelist-Engstelle existiert.
  - `AufgabeService.CreateFromIssueAsync` (Z. 210), `CreateFromPullRequestAsync` (Z. 252, mit PR-Dedup + `DbUpdateException`-Absicherung), `IsPullRequestLinkedAsync` (Z. 120), `ProjektService.AddRepositoryAsync` setzt `Aktiv = true` (Z. 189), `GitRepository.ProjektId`/`Aktiv` (Z. 10/22), `PullRequestReferenz`-Schlüssel Provider+RepositoryId+PullRequestNumber — alle im Plan genannten Bausteine existieren.
  - CLI-Versionserkennung gepinnt auf „OpenAI Codex (v0.114.0)"/„Claude Code v2.1.0" (`ScmPromptSessionAdapter.cs:230-233`); `PollInterval` = 60 s (`ScmEventOptions.cs:7`).
- **Schwaches Restrisiko (keine Lücke):** Schritt 2 überlässt die fachliche Grenze für geschlossene/alte Referenzen dem Umsetzungsschritt („fachlich begründete Grenze", dokumentiert im Zusatz der offenen Punkte). Das Akzeptanzkriterium der Anforderung formuliert „Kommentare und Reviews zu Issues bzw. PRs" ohne Offen/Geschlossen-Einschränkung; bei einer „nur offene Referenzen"-Entscheidung sollte die Umsetzung die Begründung sorgfältig dokumentieren, damit die Abdeckung nicht faktisch verwässert wird.
- **Schwaches Restrisiko (keine Lücke):** In der Zwischenstufe nach Schritt 2 erzeugen repo-weit erkannte, aufgabenlose Referenzen Snapshot-Diagnosen mit dem (für diesen Fall semantisch ungenauen) Text „Mehrdeutige Zuordnung zu Aufgaben" — technisch korrekt abgebildet, aber in der SCM-Ereignisübersicht/Diagnostik potenziell irritierend bis Schritt 3 nachzieht. Optional kann Schritt 2 die Diagnose unterscheidbar formulieren.
- **Offene Fragen Q1–Q7:** Alle im Plan mit begründeten Vorschlägen versehen und den betroffenen Schritten zugeordnet; sie blockieren den Plan nicht (entspricht der Anforderungslage, die die Punkte offen lässt).
- **Testbarkeit:** Die im Plan genannten Fixtures (`ScmDispatchFixture` mit umschaltbarem Prompt-Buffer, `ScmE2EFixture`, KiSimulator `--scm-exchange`) existieren; die geforderte Konsolidierung neuer FlaUI-Szenarien in die bestehende E2E-Struktur ist projektkonform berücksichtigt.
