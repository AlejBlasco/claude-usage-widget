---
description: "SDLC phase 4 — Documentation. Produces technical and functional markdown docs (with mermaid diagrams) from an implementation summary or description, via the Technical Writer agent."
argument-hint: "[implementation-summary-file-path | free-text description of what to document]"
---

# sdlc-documentation

Input received: `$ARGUMENTS`

Delegate this entire task to the **technical-writer** subagent using the Task
tool (`subagent_type: "technical-writer"`). Do not write the documentation
yourself in this top-level context.

Before delegating:

1. If `.claude/sdlc.config.yaml` exists, note its contents so you can pass
   `documentation`, `paths.technicalDocs`, and `paths.functionalDocs` to the
   subagent. Otherwise use the defaults (`documentation: es`,
   `paths.technicalDocs: docs/technical`, `paths.functionalDocs:
   docs/functional`).
2. Classify `$ARGUMENTS`:
   - Path to an existing `.md` file (typically produced by
     `sdlc-development`): tell the subagent to read it and, if needed,
     inspect the referenced source files for accuracy.
   - Free-text description: pass verbatim.
3. Remind the subagent that it must produce **two separate files** (technical
   and functional) and that both must include at least one Mermaid diagram.

Pass all of this to the subagent in a single, clear task description,
including the reminder that it must never run `git commit` or `git push`.

After the subagent finishes, relay its summary to the user with both file
paths (technical and functional).
