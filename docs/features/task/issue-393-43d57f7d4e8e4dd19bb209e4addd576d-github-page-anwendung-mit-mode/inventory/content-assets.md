# Bestandsaufnahme: Inhaltsquellen, Design-Referenzen und Assets

Analysiert wurden die Repository-Artefakte, aus denen sich Inhalt, Farbdesign und Bildmaterial der geplanten GitHub-Pages-Seite speisen bzw. die als Assets wiederverwendet werden können. Datenmodell-, Logikklassen, Interfaces und Enums der Anwendung sind von der Anforderung nicht betroffen — daher hier kein klassischer Code-Bestand, sondern die relevanten Repo-Artefakte.

## Inhaltsquellen

### `README.md`
Datei: `README.md` (~44 KB, Deutsch)

| Abschnitt | Für die Seite nutzbarer Inhalt |
|-----------|--------------------------------|
| Titel/Badges (Zeilen 1–7) | `# 🔨 Softwareschmiede`, Tagline „KI-gestützter Softwareentwicklungs-Workflow als lokale Windows-Desktopanwendung", Badges für `staging-ci.yml`, `release.yml`, MIT-License |
| `## Überblick` | Technologie-Steckbrief: WPF auf .NET 10, SQLite via EF Core, Serilog, Plugin-Modell (GitHub, BitBucket, Local Directory, GitHub Copilot, Claude CLI, Codex, Devin), xUnit/FlaUI |
| `## Kernfunktionen` | Aufzählung der zentralen Features: Projekt-/Aufgabenverwaltung, kompakte Aufgabenliste mit Anzeigemodi, Plugin-basierte SCM-Integration (HTTPS-only, SSH→HTTPS-Umwandlung), Plugin-basierte KI-Ausführung über ConPTY-Terminal, IDE-Integration (VS/VS Code), Dateiexplorer/Diff, PR-Workflow, GitHub-Ereignisüberwachung, pausierbare Aufgaben, zeitgesteuerte Prompts, Programmupdate gegen GitHub-Releases, CLI-Aufzeichnung/Konsolentestfenster, automatisches Herunterfahren |
| `## Terminalintegration` | Details zum eingebetteten Terminal (VT100/ANSI-Renderer, Alternate Screen, Scrollback, Preflight-Diagnose, Pipe-Fallback, `.clireplay`-Mitschnitt) |
| `## Programmupdate` | Update-Mechanismus gegen `martin-stromberg/Softwareschmiede`-Releases mit `release.zip`/`update.json`-Manifest — begründet den Download-Verweis auf `releases/latest` |

Sprache der Dokumentation durchgehend **Deutsch** (`README.md`, `docs/**`, `CONTRIBUTING.md`, `CHANGELOG.md`).

### `docs/help/`
Datei: `docs/help/index.md` plus Themen-Unterverzeichnisse

Bereits strukturierte, deutschsprachige Anwenderdokumentation mit `index.md` je Bereich: `anwendung`, `aufgaben`, `dateiexplorer`, `dateisystem-integration`, `diff`, `einstellungen`, `entwicklungsumgebungen`, `plugins` (inkl. `bitbucket-plugin`, `github-ereignisse`), `programmupdate`, `projekte`, `stabilitaet`, `terminal`, `e2e-test-infrastruktur` (Entwicklerdoku). Geeignet als Linkziel „Dokumentation" auf der Page — die Dateien sind Markdown, nicht HTML; ein direkter Verweis auf das Repo (`docs/help/index.md` auf GitHub) ist der pragmatische Linkpfad.

### `docs/CI_CD.md`
Datei: `docs/CI_CD.md`

Beschreibt Release-Pipeline, Staging-PR-Workflow, Review-Workflows, Secrets (`REVIEW_ISSUE_TOKEN`, `GITHUB_TOKEN`), Troubleshooting und Recovery. Enthält das Muster „Einmalige Betriebshandlungen (Rollout)" (Zeilen 96–101), an das sich die geforderte Pages-Aktivierungs-Anleitung anlehnen kann. **Drift-Befund:** nennt `test.yml`/`pr.yml` und `softprops/action-gh-release`, die so nicht (mehr) existieren (Details in [workflows.md](workflows.md)).

### Weitere Quellen
- `CHANGELOG.md` (~21 KB): semantisch-versionierte Änderungshistorie — potenzielle Inhaltsquelle, laut Scope-Ausschluss aber keine eigene Release-Notes-Darstellung gefordert.
- `CONTRIBUTING.md`: beschreibt Branch-Workflow und manuellen Release-Tag-Override — Referenz für die Pflegeanleitung der Seite.
- `LICENSE` (MIT), `SECURITY.md`, `THIRD_PARTY_LICENSES.md`: Footer-/Rechtsverweise der Page.

## Design-Referenz: App-Themes

Die Anforderung verlangt eine Anlehnung an das WPF-Erscheinungsbild. Die Theme-ResourceDictionaries definieren die komplette Farbpalette:

### `DarkTheme.xaml`
Datei: `src/Softwareschmiede.App/Themes/DarkTheme.xaml` (Zeilen 5–37)

| Rolle | Farbe |
|-------|-------|
| `BackgroundColor` | `#1C1C1C` |
| `SurfaceColor` | `#2D2D2D` |
| `SidebarBackgroundColor` | `#252525` |
| `BorderColor` / `SeparatorColor` | `#3D3D3D` / `#404040` |
| `PrimaryTextColor` / `SecondaryTextColor` / `DisabledTextColor` | `#F0F0F0` / `#A0A0A0` / `#606060` |
| `AccentColor` / `AccentHoverColor` | `#4CC2FF` / `#60CDFF` |
| `SuccessColor` / `WarningColor` / `ErrorColor` / `InfoColor` | `#6CCB5F` / `#FCE100` / `#FF6B6B` / `#4CC2FF` |
| Statusfarben (Neu/Arbeitsverzeichnis/Gestartet/InArbeit/Wartend/Beendet/Archiviert) | `#909090` / `#4CC2FF` / `#00D5E0` / `#6CCB5F` / `#FCE100` / `#909090` / `#606060` |

### `LightTheme.xaml`
Datei: `src/Softwareschmiede.App/Themes/LightTheme.xaml` (Zeilen 5–37)

| Rolle | Farbe |
|-------|-------|
| `BackgroundColor` | `#FAFAFA` |
| `SurfaceColor` | `#FFFFFF` |
| `SidebarBackgroundColor` | `#F0F0F0` |
| `BorderColor` / `SeparatorColor` | `#E0E0E0` / `#D0D0D0` |
| `PrimaryTextColor` / `SecondaryTextColor` / `DisabledTextColor` | `#1A1A1A` / `#666666` / `#AAAAAA` |
| `AccentColor` / `AccentHoverColor` | `#0078D4` / `#005A9E` |
| `SuccessColor` / `WarningColor` / `ErrorColor` / `InfoColor` | `#107C10` / `#FF8C00` / `#C42B1C` / `#0078D4` |

Die im `requirement.md` genannten Werte (`#1C1C1C`, `#252525`, `#FAFAFA`, `#F0F0F0`, `#0078D4`, `#4CC2FF`, `#60CDFF`) stimmen mit diesen Dateien überein.

## Vorhandenes Bildmaterial

Verzeichnis: `src/Softwareschmiede.App/images/`

| Datei | Größe | Inhalt / Verwendung |
|-------|-------|---------------------|
| `favicon-hammer-pick.svg` | 619 B | SVG-Favicon: gekreuzter Hammer und Spitzhacke (`#f59e0b` Amber) auf dunklem Kreis (`#111827`); `<desc>` nennt ausdrücklich „for the Softwareschmiede web application" — direkt als Site-Icon wiederverwendbar |
| `Softwareschmiede.ico` | ~16 KB | Produktiv-Icon der Anwendung (Programmsymbol, dokumentiert in `docs/help/anwendung/beschreibung.md`) |
| `Softwareschmiede-RC.ico` | ~270 KB | Icon-Variante für Release-Kandidaten |

**Screenshots:** Es existieren **keine** Screenshots der Anwendung im Repository (keine Bilddateien außer den drei oben genannten Icons unter `src/Softwareschmiede.App/images/`; `docs/help/` enthält keine Bildassets). Diese müssen gemäß Anforderung manuell erzeugt und committed werden.

## Nicht vorhandene Artefakte (Lücken)

- Kein `site/`-Ordner oder anderer Seitenquell-Baum; keine `index.html`, keine CSS-/JS-Dateien.
- Kein `_config.yml`, `.nojekyll` oder sonstige Jekyll-/Pages-Konfiguration.
- Kein `CNAME` (keine eigene Domain vorgesehen — passt zum Scope-Ausschluss).
- `.gitignore` enthält `publish/` (Zeile 180) und `DocProject/Help/html` (Zeile 177) — für einen künftigen Build-Output der Seite ggf. relevant; ein reiner Quellordner `site/` wäre davon nicht betroffen.
- `package.json` enthält nur semantic-release-DevDependencies (`semantic-release`, `@semantic-release/commit-analyzer`, `-release-notes-generator`, `-github`) — kein Static-Site-Generator, kein Node-Build für Webinhalte.
