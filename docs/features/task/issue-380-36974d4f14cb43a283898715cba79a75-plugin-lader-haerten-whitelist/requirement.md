# Anforderung (übersetzt)

Quelle: GitHub-Issue #380 — „Plugin-Lader härten: Whitelist/Strong-Name für plugins/*.dll (aus #374)", Prio 2 aus dem Architektur- und Sicherheitsreview 2026-11.

## Ausgangslage

`PluginManager` lädt jede `*.dll` aus `plugins/` (Top-Level und eine Unterverzeichnis-Ebene) in den Default-`AssemblyLoadContext` — ohne Signatur- oder Namens-Whitelist. Bei nutzerschreibbarer Installation (Desktop) kann eine dort abgelegte fremde DLL mit vollen App-Rechten inkl. Credential-Store-Zugriff laufen. Bedrohungsnähe ist begrenzt (gleicher Benutzerkontext), dennoch empfohlen.

## Ziel

- Nur bekannte First-Party-Plugin-Assemblies werden geladen (Whitelist der Assembly-Namen).
- Fremde/unbekannte DLLs werden nicht geladen und erzeugen einen Warn-Log-Eintrag.
- Optional: Strong-Name-Prüfung (nur sinnvoll, wenn Assemblies signiert sind — derzeit nicht der Fall).

## Akzeptanzkriterien

1. Eine DLL, deren Name nicht auf der Whitelist steht, wird nicht geladen; ein Warn-Log wird geschrieben.
2. Alle 8 mitgelieferten Plugins (BitBucket, ClaudeCli, Codex, Devin, GitHub, GitHubCopilot, KiSimulator, LocalDirectory) laden weiterhin.
3. Dependency-DLLs in den Plugin-Unterordnern erzeugen keine Warn-Flut (sie sind keine Ladekandidaten).
4. Bestehende Tests bleiben grün; neue Tests decken Whitelist-Akzeptanz, -Ablehnung und Warn-Log ab.
5. Dokumentation (`docs/help/plugins/`) beschreibt das Härtungsverhalten.
