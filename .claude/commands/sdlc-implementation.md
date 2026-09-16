---
description: "SDLC phase 6 — Implementation and Maintenance. Prepares/reviews deployment artifacts (Compose stacks, CI/CD pipeline, runbooks) via the DevOps Engineer agent. Never deploys anything itself."
argument-hint: "[implementation-summary-file-path | free-text description of what to prepare/review]"
---

# sdlc-implementation

Input received: `$ARGUMENTS`

Delegate this entire task to the **devops-engineer** subagent using the
Task tool (`subagent_type: "devops-engineer"`). Do not prepare deployment
artifacts yourself in this top-level context — the subagent owns this
phase.

Before delegating:

1. If `.claude/sdlc.config.yaml` exists, note its contents so you can pass
   `documentation` and `paths.deployment` to the subagent. Otherwise use
   the defaults (`documentation: es`, `paths.deployment:
   docs/sdlc/deployment`).
2. Classify `$ARGUMENTS`:
   - Path to an existing `.md` file (typically an implementation summary
     from `sdlc-development`): tell the subagent to read it for context on
     what was built and needs to ship.
   - Free-text description: pass verbatim.
3. If the target environment (custom server/self-hosted vs. cloud/Azure)
   isn't
   obvious from the request or from files already in the repository, tell
   the subagent to ask the user which one applies before writing anything.

Pass all of this to the subagent in a single, clear task description,
including two **hard** reminders: (1) it must never run `git commit` or
`git push`; and (2) it must never connect to or execute anything against a
remote host (no SSH, no live Portainer/cloud API calls) — it only edits
files in this repository and documents the manual/automated steps that
apply the change elsewhere.

After the subagent finishes, relay its summary to the user: files
changed, any manual one-time setup required, the deployment flow, and the
rollback procedure — and an explicit reminder that nothing was committed,
pushed, or deployed.
