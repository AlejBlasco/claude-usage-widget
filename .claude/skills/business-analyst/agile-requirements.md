# Skill: Agile Requirements Framing

Load this skill when the requirements will be worked on by a Scrum/Kanban
team, or when the source is a backlog item (Azure DevOps Work Item, Jira
story, etc.).

## INVEST criteria for user stories

Each story should be:

- **Independent** — deliverable without hard dependency on unstarted stories.
- **Negotiable** — a placeholder for a conversation, not a rigid contract.
- **Valuable** — delivers value to a user or the business on its own.
- **Estimable** — the team can size it (even roughly) from what's written.
- **Small** — fits comfortably in a single sprint; split if it doesn't.
- **Testable** — has clear acceptance criteria (GIVEN-WHEN-THEN).

If a story fails INVEST (too large, no clear value, hard external
dependency), say so explicitly and propose how to split it instead of
writing one giant requirements document.

## Definition of Ready (DoR) checklist

Before a story is considered ready for the Design phase, it should have:

- A clear "why" (business value), not just a "what."
- Acceptance criteria in GIVEN-WHEN-THEN form.
- Known dependencies/blockers identified.
- No open question that blocks estimation.

## Definition of Done (DoD) reminder

Mention in the requirements doc, when relevant, what "done" means beyond
code complete: tests passing, documentation updated, deployed to a given
environment, etc. — pull this from the team's existing DoD if the repo
documents one (e.g. `CONTRIBUTING.md`, wiki links), otherwise state the
generic default as a `Technical Notes` bullet, flagged as an assumption
rather than presented as settled fact.

## Azure DevOps / Jira specifics

- If the source is an Azure DevOps Work Item (User Story/PBI), preserve its
  ID in the "Source" section of the requirements doc for traceability.
- Respect the original story's title intent, but do not treat its
  description as complete — most tracker items are underspecified by design
  and need the elicitation pass.
