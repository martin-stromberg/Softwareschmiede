# Dokumentationsstruktur — relevante Bestandsdokumente

## `docs/CI_CD.md` (95 Zeilen) — Konventionsziel der Anforderung

Dokumentiert primär `release.yml` (Trigger, Jobs, Schritte, Secrets, Troubleshooting, Recovery). **Veraltete Stellen, verifiziert:**

- Zeilen 18–24 beschreiben `.github/workflows/test.yml` — **existiert nicht** im Repo.
- Zeilen 26–35 beschreiben `.github/workflows/pr.yml` — **existiert nicht**; tatsächliche Dateien: `pr-staging-ci.yml` (`PR CI for Staging`) und `staging-ci.yml` (`Pre-Release`). Die beschriebenen Inhalte (Format-Check, Security-Scan-Gate, `Category!=OsInterface`-Trennung, `back-merge-skip`-Erkennung) passen sinngemäß zur aktuellen CI, aber Dateinamen und die Bezeichnung `back-merge-skip` sind überholt (tatsächlich heißt der Job `detect-backmerge`).
- `security-scan.yml`, `sync-staging-with-main.yml`, `staging-to-main-promotion.yml`, `verify-pr-source.yml` werden in `CI_CD.md` **nicht** erwähnt.
- Abschnitt „Secrets und Umgebungsvariablen" (Zeilen 63–69): „Es sind keine weiteren, manuell zu hinterlegenden Secrets erforderlich." — müsste bei Einführung eines Owner-PAT-Secrets angepasst werden.
- `README.md:330` verlinkt `docs/CI_CD.md` als „CI/CD-Dokumentation".

## `docs/help/plugins/github-ereignisse/` — fachlicher Vertrag des Auslösers (9 Dateien)

| Datei | Anforderungsrelevanter Inhalt |
|-------|-------------------------------|
| `index.md` | Übersicht + Verlinkung der 8 Unterseiten |
| `beschreibung.md` | „Im separaten Modus … Eine Aufgabe wird ausgelöst, sobald jemand das separate Konto frei stehend als `@Kontoname` in einer Issue- oder Pull-Request-Beschreibung oder einem Kommentar erwähnt — … der Autor muss aber weiterhin der Repository-Owner sein." |
| `installation.md` (Z. 25–28) | Setup-Vertrag: separates Konto als **Collaborator mit Schreibrecht** einladen; PAT mit Scopes `notifications` + `repo` unter „Personal Access Token des separaten Kontos"; Überwachungsmodus „Separates Konto" wählen |
| `ablauf-technisch.md` (Z. 19, 38, 40) | Technischer Pfad: `GET /notifications`, `reason = mention`, `repository.node_id`-Zuordnung, Inbox-Checkpoint/Watermark, `TextFields`-Fragment mit `reactionGroups`, `GetRejection`-Regelkette |
| `business-rules.md` (Z. 31–33) | Verbindlicher Vertrag: `SeparatesKonto` liest ausschließlich die Inbox, nur `reason = mention`; Trigger = frei stehende `@`-Erwähnung; **Owner-Prüfung gegen Autor/Bearbeiter bleibt**; Viewer=Owner-Regel entfällt; eigener PAT erforderlich |
| `troubleshooting.md` (Z. 55–57) | Fehlerbild „Benachrichtigung erzeugt kein Ereignis": Erwähnung nicht frei stehend, Autor ≠ Repository-Owner, Bot ohne Repo-Zugriff, Repo nicht registriert, `reason ≠ mention`. Lösung: Bot als Collaborator mit Schreibrecht einladen |
| `architektur.md`, `datenmodell.md`, `ablauf-anwender.md` | Kontext (Komponenten, Persistenz, Anwendersicht) — keine neue Anforderungsaussage |

## Sonstige dokumentierte Berührungspunkte

- `README.md:135` verlinkt die GitHub-Ereignisüberwachung; `:330` verlinkt `CI_CD.md`.
- `CLAUDE.md` enthält Test-Regeln (stabile Lane, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`, OsInterface-Trennung) und den Pre-Push-Format-Hook (`dotnet format Softwareschmiede.slnx --verify-no-changes`, gespiegelt aus `pr-staging-ci.yml`) — YAML-Dateien sind vom `dotnet format`-Check nicht betroffen.
- `CONTRIBUTING.md` dokumentiert die Testbefehle; `.github/copilot-instructions.md` existiert (nicht näher geprüft, für Anforderung ohne Befund).
- `.github/skills/` (`razor-viewmodel`, `scenarios`) und `.github/upgrades/` sind Verzeichnisse ohne Bezug zur Anforderung.
- Referenzen in Workflow-Kommentaren auf `ci-target-schema.md`/`ci-inventory.md` sind unauflösbar — diese Dateien existieren im Repo nicht (Überreste früherer Feature-Verzeichnisse).

## Fazit für die Dokumentationspflicht der Anforderung

„Vollständig innerhalb des Repositories dokumentiert" bedeutet faktisch: Erweiterung von `docs/CI_CD.md` (gern gesehener Ort, trotz vorhandener Veraltung) plus ggf. Betriebshinweise (Secret für Owner-PAT, Collaborator-Einladung des `@softwarewerkstatt`-Kontos, Aktivierung erst nach Promotion nach `main`).
