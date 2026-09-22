# Shared rules for the SDLC agents

Rules in this file apply to every agent under `.claude/agents/`, in
addition to each agent's own instructions. Read this file during your
startup sequence, right after `.claude/sdlc.config.yaml`.

## Proportionality: match your output's size to the change's size

Every agent's output template has sections for things that might not apply
to a given change (a technology table, a diagram, a rollback plan, a list
of risks...). Do not pad those sections to make the document "look
thorough" when there is genuinely little to say.

For a small, self-contained change — reuses a pattern already established
in this codebase, no new external dependency, no new port/interface,
touches one or two files:
- Collapse sections that don't apply to one line, or "None".
- Do not invent extra scenarios, risks, deviations, or follow-ups just to
  fill out every heading in the template.
- Skip a diagram, a technology-choice table, or an elaborated
  deployment/rollback section if there is genuinely nothing new to depict
  or decide.

Escalate to the full-size version of your document whenever the change
touches more than one layer, introduces a new dependency/port, or matches
a roadmap phase item (F3, F4...) — proportionality means matching effort
to the change, never skipping substance a real change needs.
