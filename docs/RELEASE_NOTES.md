# Release Notes

## Important Notes Before Update

- Plugin loading is restricted to a hard-coded whitelist of the shipped first-party plugin assemblies: a DLL in `plugins/` is only loaded if both its file name and its real assembly name (read from metadata) match the whitelist — foreign or self-added plugin DLLs are skipped and logged as a warning. In plugin subfolders, only the entry DLL matching the folder name is a load candidate.

## What's New

- New public project website via GitHub Pages: the repository now ships a static landing page (`site/`, deployed from `main` to `https://martin-stromberg.github.io/Softwareschmiede/`) presenting the application — purpose, key features, screenshots, and links to releases, repository, and documentation — with responsive light/dark styling matching the app themes.
- GitHub Enterprise: HTTPS git operations now authenticate against GitHub Enterprise hosts — the `http.extraheader` key is bound per operation to the host of the actually contacted remote URL instead of being hard-wired to `github.com`; PR checkouts from forks bind the fetch target to the fork's source repository.

## Wichtige Hinweise vor dem Update

- Das Laden von Plugins ist auf eine fest kodierte Whitelist der mitgelieferten First-Party-Plugin-Assemblies beschränkt: Eine DLL in `plugins/` wird nur geladen, wenn sowohl ihr Dateiname als auch ihr echter Assembly-Name (aus den Metadaten gelesen) auf der Whitelist stehen — fremde oder selbst hinzugefügte Plugin-DLLs werden übersprungen und als Warnung protokolliert. In Plugin-Unterordnern ist nur noch die zum Ordnernamen passende Entry-DLL ein Ladekandidat.

## Neuerungen

- Neue öffentliche Projektwebsite über GitHub Pages: Das Repository enthält jetzt eine statische Landing-Page (`site/`, deployed von `main` nach `https://martin-stromberg.github.io/Softwareschmiede/`), die die Anwendung präsentiert — Zweck, zentrale Features, Screenshots sowie Verweise auf Releases, Repository und Dokumentation — mit responsivem Hell-/Dunkel-Design angelehnt an die App-Themes.
- GitHub Enterprise: HTTPS-Git-Operationen authentifizieren jetzt auch gegen GitHub-Enterprise-Hosts — der `http.extraheader`-Key wird pro Operation an den Host der tatsächlich kontaktierten Remote-URL gebunden statt fest auf `github.com` verdrahtet; PR-Checkouts aus Forks binden das Fetch-Ziel an das Source-Repository des Forks.
