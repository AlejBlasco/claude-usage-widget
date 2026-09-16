# Skill: Architecture Decision Records (ADR) style

Load this skill when a design involves a decision significant enough to
outlive the current task (framework choice, data store, breaking API
change, major refactor direction).

## When a decision deserves ADR-style treatment inside the design doc

- It would be expensive to reverse later.
- It affects more than one team/module.
- Reasonable engineers could disagree about it.

## Minimal ADR shape (fold into the design doc's tables/sections, don't
create a separate file unless asked)

- **Context**: what forces are at play (technical, business, constraints).
- **Decision**: what was decided, stated as a single clear sentence.
- **Consequences**: what becomes easier, what becomes harder, what new
  risks are introduced.

Keep it short — 3-6 sentences total per decision. The goal is a future
reader understanding *why*, not a full essay.
