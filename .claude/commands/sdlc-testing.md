---
description: "SDLC phase 5 — Testing. Writes unit tests to reach the configured minimum coverage, from an implementation summary or description, via the QA Engineer agent."
argument-hint: "[implementation-summary-file-path | free-text description of what to test]"
---

# sdlc-testing

Input received: `$ARGUMENTS`

Delegate this entire task to the **qa-engineer** subagent using the Task tool
(`subagent_type: "qa-engineer"`). Do not write the tests yourself in this
top-level context.

Before delegating:

1. If `.claude/sdlc.config.yaml` exists, note its contents so you can pass
   `testingCoverage`, `documentation`, and `paths.testing` to the subagent.
   Otherwise use the defaults (`testingCoverage: 70`, `documentation: es`,
   `paths.testing: docs/sdlc/testing`).
2. Classify `$ARGUMENTS`:
   - Path to an existing `.md` file (typically produced by
     `sdlc-development`): tell the subagent to read it to know exactly which
     files/functions need coverage.
   - Free-text description: pass verbatim.

Pass all of this to the subagent in a single, clear task description,
including the reminder that it must never run `git commit` or `git push`,
and that tests must be meaningful, never written just to inflate the
coverage number.

After the subagent finishes, relay its summary to the user: tests
added/modified, coverage achieved vs. the configured target, and any gaps it
flagged.
