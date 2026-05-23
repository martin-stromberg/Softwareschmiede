# Lifecycle report: Status zurücksetzen bei KI aktiv ohne Lauf

## Planned
- `docs/requirements/status-zuruecksetzen-ki-aktiv-ohne-lauf-requirements-analysis.md`
- `docs/architecture/status-zuruecksetzen-ki-aktiv-ohne-lauf-architecture-blueprint.md`
- `docs/improvements/status-zuruecksetzen-ki-aktiv-ohne-lauf-architecture-review.md`

## Implemented
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`

## Tests added
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`

## Documented
- `docs/business/features/F023-status-zuruecksetzen-ki-aktiv.md`
- `docs/business/features.md`
- `docs/user-guide.md`
- `docs/business/features/F016-fehlerbehandlung-und-recovery.md`

## Notes
- The reset action is only available when no KI run is active.
- No open issues remain for the documented scope.
