# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Saving the plugin settings now warns when a secret field contains a GitHub App installation token (`ghs_…` prefix): a confirmation dialog names the affected fields and requires explicit approval — "No" cancels the entire save, "Yes" stores the value unchanged. The token field descriptions carry the same hint and now wrap long text.
- GitHub App installation tokens (`ghs_…`) are transported unchanged through the entire token chain — settings field, Windows Credential Manager, `Authorization: Bearer` header, agent/git environment (`GH_TOKEN`, `http.…extraheader`) and output masking. The documented limits remain: such tokens expire after about one hour and provide no user identity, so they are unsuitable as permanent credentials (help pages updated accordingly).

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Beim Speichern der Plugin-Einstellungen warnt die Anwendung jetzt, wenn ein Secret-Feld ein GitHub-App-Installationstoken enthält (Präfix `ghs_…`): Ein Bestätigungsdialog nennt die betroffenen Felder und verlangt eine ausdrückliche Zustimmung — „Nein" verwirft den gesamten Speichervorgang, „Ja" übernimmt den Wert unverändert. Die Beschreibungen der Token-Felder tragen denselben Hinweis und brechen langen Text jetzt um.
- GitHub-App-Installationstokens (`ghs_…`) werden unverändert durch die gesamte Token-Kette transportiert — Einstellungsfeld, Windows Credential Manager, `Authorization: Bearer`-Header, Agenten-/git-Umgebung (`GH_TOKEN`, `http.…extraheader`) und Ausgabemaskierung. Die dokumentierten Grenzen bleiben: Solche Tokens laufen nach etwa einer Stunde ab und liefern keine Benutzeridentität — als dauerhafte Zugangsdaten sind sie ungeeignet (Hilfeseiten entsprechend ergänzt).
