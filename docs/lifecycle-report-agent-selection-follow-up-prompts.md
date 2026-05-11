# Lifecycle Report: Agent Selection for Follow-up Prompts

## Planned
- Requirements: [agent-auswahl-bei-folgeanweisungen-requirements-analysis](./requirements/agent-auswahl-bei-folgeanweisungen-requirements-analysis.md)
- Architecture blueprint: [agent-auswahl-bei-folgeanweisungen-architecture-blueprint](./architecture/agent-auswahl-bei-folgeanweisungen-architecture-blueprint.md)
- ER model: [agent-auswahl-bei-folgeanweisungen-entity-relationship-model](./architecture/agent-auswahl-bei-folgeanweisungen-entity-relationship-model.md)
- Architecture review: [agent-auswahl-bei-folgeanweisungen-architecture-review](./improvements/agent-auswahl-bei-folgeanweisungen-architecture-review.md)

## Implemented
- Added agent selection for follow-up prompts in `AufgabeDetail` UI and code-behind.
- Default selection for follow-up prompts is the initially selected agent.
- Users can change the selected agent before sending each follow-up prompt.
- Follow-up prompts are routed to the actually selected agent.
- Initial prompt behavior remains unchanged.

## Tests Added
- New component-focused tests for follow-up prompt behavior:
  - `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`
- Extended service-level tests:
  - `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

## Documentation Updated
- Updated central and feature documentation across README, requirements, architecture, flow, API, and business docs.
- New feature-specific docs:
  - `docs/requirements/agent-selection-follow-up-prompts-requirements-analysis.md`
  - `docs/architecture/agent-selection-follow-up-prompts-architecture-blueprint.md`
  - `docs/improvements/agent-selection-follow-up-prompts-architecture-review.md`
  - `docs/business/features/F011-agent-auswahl-bei-folgeanweisungen.md`

## Open Points / Notes
- One existing, feature-unrelated unit test failure remains in the suite:
  - `GitHubCopilotPluginTests.StartDevelopmentAsync_ShouldWritePromptFile_AndPassAgentAndModelToCli`
- Follow-up UI visibility is covered by markup assertion; no interactive bUnit flow test was added for selector interaction.
