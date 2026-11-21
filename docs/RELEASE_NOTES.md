# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Fixed: SCM event monitoring no longer exhausts the GitHub REST rate limit — repository aliases are now resolved primarily via a single GraphQL query with a time-limited cache, alias spellings are grouped canonically, and an exhausted quota gates follow-up requests instead of firing further API calls; the monitoring stays functional and the REST quota remains free for the `gh` CLI and other API calls. Optionally tunable via the new `ScmEvents` setting `RepositoryResolutionCacheTtl` (default 15 minutes).

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Fehlerkorrektur: Die SCM-Ereignisüberwachung erschöpft das GitHub-REST-Rate-Limit nicht mehr — Repository-Aliase werden jetzt primär über eine einzige GraphQL-Abfrage mit zeitlich begrenztem Cache aufgelöst, Alias-Schreibweisen laufen kanonisch gruppiert zusammen, und ein erschöpftes Kontingent sperrt Folgeaufrufe statt weiterer API-Calls; die Überwachung bleibt funktionsfähig und das REST-Kontingent bleibt für die `gh`-CLI und andere API-Aufrufe frei. Optional über die neue `ScmEvents`-Einstellung `RepositoryResolutionCacheTtl` einstellbar (Standard 15 Minuten).
