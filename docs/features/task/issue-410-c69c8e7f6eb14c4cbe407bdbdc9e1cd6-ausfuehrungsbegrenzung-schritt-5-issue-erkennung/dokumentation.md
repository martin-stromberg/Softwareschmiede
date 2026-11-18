# Dokumentation: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5)

**Status: Aktualisiert**

## Angepasste Stellen

- `docs/help/plugins/github-ereignisse/beschreibung.md` — neuer Absatz „neu angelegte Issues
  erkennen" in der Funktionsweise + Einschränkungs-Bullet (nur Enumerationskanal, geparkter Auftrag).
- `docs/help/plugins/github-ereignisse/business-rules.md` — neuer Abschnitt
  „Issue erstellt"-Erkennung (Issue-Aufbereitung): Schalter, Baseline, Einmaligkeit, Trigger-Bypass,
  Parkzustand, Schleifenschutz, Isolation, Konfigurationsschlüssel.
- `docs/help/plugins/github-ereignisse/datenmodell.md` — `ScmIncomingEvent.Zweck`,
  `Aufgabe.Zweck`, `GitRepository.IssueAufbereitungAktiv`, Checkpoint-Namensraum
  `issueaufbereitung/…`, SourceKey-Namensraum `…/IssueAufbereitung/{n}`.
- `docs/help/plugins/github-ereignisse/ablauf-technisch.md` — Absatz zum Andockpunkt der
  Erkennung im Enumerationspfad (Reihenfolge vor `AcceptBatchAsync`, Baseline, Einreihung,
  Backoff/Idempotenz).
- `docs/help/plugins/github-ereignisse/architektur.md` — Komponentenzeile
  `ScmIssueAufbereitungsService`.
- `docs/RELEASE_NOTES.md` — je ein Bullet unter „What's New" und „Neuerungen".
- `README.md` — Satz im SCM-Ereignisabsatz zur optionalen Erkennung.

## Hinweise

- `index.md` der Hilfe-Sektion unverändert (keine neue Seite — die Funktion ist Teil des
  bestehenden Ereignis-Features).
- Troubleshooting unverändert: keine neue Fehlerklasse — Erkennungsfehler laufen über den
  bestehenden Checkpoint-Backoff; der geparkte Auftrag trägt einen ehrlichen Wartegrund.
