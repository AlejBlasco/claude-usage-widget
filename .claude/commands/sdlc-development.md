---
description: "SDLC phase 3 — Development and Programming. Implements code per a design document (or description) and writes an implementation summary, via the Software Developer agent."
argument-hint: "[design-file-path | free-text description of what to build/modify]"
---

# sdlc-development

Input received: `$ARGUMENTS`

Delegate this entire task to the **software-developer** subagent using the
Task tool (`subagent_type: "software-developer"`). Do not write the
implementation yourself in this top-level context — the subagent owns this
phase.

Before delegating:

1. If `.claude/sdlc.config.yaml` exists, note its contents so you can pass
   `xmlDocComments`, `documentation`, and `paths.development` to the
   subagent. Otherwise use the defaults (`xmlDocComments: es`,
   `documentation: es`, `paths.development: docs/sdlc/development`).
2. Classify `$ARGUMENTS`:
   - Path to an existing `.md` file (typically produced by `sdlc-design`):
     tell the subagent to read it and follow its implementation plan.
   - Free-text description: pass verbatim as the task to implement.

Pass all of this to the subagent in a single, clear task description,
including two **hard** reminders: (1) it must never run `git commit` or
`git push`, no matter what — changes must be left uncommitted in the working
tree for the user to review; and (2) it must never create, modify, or run
any test files or test commands (`dotnet test`, `npm test`, etc.) — its only
automated verification step is building the project. All test writing and
execution belongs exclusively to `sdlc-testing`.

After the subagent finishes, relay its summary to the user: files changed,
any deviations from the design, how to verify the change, and an explicit
reminder that nothing was committed or pushed. Mention `sdlc-testing` and
`sdlc-documentation` as likely next steps.
