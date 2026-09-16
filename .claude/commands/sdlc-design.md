---
description: "SDLC phase 2 — Design. Turns a requirements document (or description) into an implementation guide via the Software Architect agent."
argument-hint: "[requirements-file-path | issue/story URL | free-text requirements]"
---

# sdlc-design

Input received: `$ARGUMENTS`

Delegate this entire task to the **software-architect** subagent using the
Task tool (`subagent_type: "software-architect"`). Do not perform the design
work yourself in this top-level context.

Before delegating:

1. If `.claude/sdlc.config.yaml` exists, note its contents so you can pass
   `documentation` and `paths.design` to the subagent. Otherwise use the
   defaults (`documentation: es`, `paths.design: docs/sdlc/design`).
2. Classify `$ARGUMENTS`:
   - Path to an existing `.md` file (typically produced by `sdlc-analysis`):
     tell the subagent to read it as the requirements source.
   - URL to a tracker item: tell the subagent to fetch it.
   - Free-text requirements: pass verbatim.
3. Remind the subagent to inspect the current repository structure before
   proposing technologies/patterns, so the design fits the existing codebase.

Pass all of this to the subagent in a single, clear task description,
including the reminder that it must never run `git commit` or `git push`.

After the subagent finishes, relay its summary to the user, including the
path of the design document it created and any risks/open decisions it
flagged.

## Closing the loop on open decisions

Read the "Risks & Open Decisions" section of the design document that was
just created:

1. If it says "None" (or is effectively empty), mention `sdlc-development`
   as the likely next step and stop here — there's nothing else to do.
2. If it lists real open items, do **not** hand them off silently or let
   the user discover them buried in the file. Instead:
   - Rephrase each open item as a direct, answerable question (one per
     item) and present them to the user as a clear numbered list, asking
     them to answer so the design can be finalized before moving on.
   - Wait for the user's answers in their next message — do not guess or
     invent answers yourself, and do not proceed to `sdlc-development` in
     the meantime.
   - Once the user has answered, delegate back to the
     **software-architect** subagent (`subagent_type:
     "software-architect"`), passing the original design document's path,
     the open items, and the user's answers, and instruct it to update
     that same file in place: resolve each item in the "Risks & Open
     Decisions" section (removing it once resolved, or marking it
     resolved with the decision taken) and adjust any other section the
     answers affect (e.g. Architecture Overview, Technology Choices).
   - Relay the updated summary to the user and only then mention
     `sdlc-development` as the next step.

