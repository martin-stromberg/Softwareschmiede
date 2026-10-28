# Dokumentation: Verweise auf Autonome Aufgaben

Stand: 2026-10-28, Commit `8c98efa`. Zählung über Schlüsselwort „autonom“ (case-insensitive), ohne `docs/features/task/`.

## Komplett zu entfernen

- `docs/help/aufgaben/autonome-aufgaben/` (9 Dateien: `ablauf-anwender.md` 17, `ablauf-technisch.md` 52, `architektur.md` 18, `beschreibung.md` 10, `business-rules.md` 25, `datenmodell.md` 42, `index.md` 2, `installation.md` 34, `troubleshooting.md` 12 Treffer)

## Abschnitte bzw. Absätze zu bereinigen

| Datei | Treffer | Art der Bereinigung |
|-------|---------|---------------------|
| `docs/help/aufgaben/index.md` | 2 | Abschnitt „Autonome Aufgaben“ entfernen, Verweis entfernen |
| `docs/help/aufgaben/beschreibung.md` | 2 | Button-Beschreibung und Pausen-Regel bereinigen (Pause bleibt für reguläre Aufgaben) |
| `docs/help/aufgaben/business-rules.md` | 3 | Regeln für autonome Aufgaben entfernen |
| `docs/help/aufgaben/ablauf-technisch.md` | 4 | Autonomer Ablauf entfernen |
| `docs/help/aufgaben/ablauf-anwender.md` | 1 | dito |
| `docs/help/aufgaben/api.md` | 2 | API-Hinweise entfernen |
| `docs/help/aufgaben/troubleshooting.md` | 1 | Eintrag entfernen |
| `docs/help/einstellungen/beschreibung.md` | 3 | Checkbox „Autonome Aufgaben aktivieren“ entfernen |
| `docs/help/einstellungen/ablauf-anwender.md` | 4 | dito |
| `docs/help/einstellungen/installation.md` | 5 | Abschnitt „Feature-Flag Autonome Aufgaben“ entfernen |
| `docs/help/einstellungen/datenmodell.md` | 1 | `autonomeaufgaben.enabled` entfernen |
| `docs/help/e2e-test-infrastruktur/beschreibung.md` | 1 | Verweis auf autonome E2E-Tests entfernen |
| `docs/help/e2e-test-infrastruktur/ablauf-technisch.md` | 1 | dito |
| `docs/help/e2e-test-infrastruktur/datenmodell.md` | 14 | Autonome Testdaten/Views entfernen |
| `docs/help/index.md` | 1 | Absatz „Neu: Autonome Aufgaben“ entfernen |
| `README.md` (Repo-Root) | 10 | Feature-Beschreibung, Konfiguration und Verweise bereinigen |

Nicht zu ändern: `CHANGELOG.md` (0 Treffer), `CLAUDE.md` (0 Treffer), `changes.log` und `docs/features/task/...` (historische Protokolle, laut Anforderung).

Hinweis: Die Anforderung nennt die Pflicht, dass die Dokumentation so aussehen soll, „als hätte das Feature nie existiert“. Die 14 Treffer in `docs/help/e2e-test-infrastruktur/datenmodell.md` und die Treffer in `README.md` sind dabei inhaltlich zu prüfen, nicht nur zu löschen.
