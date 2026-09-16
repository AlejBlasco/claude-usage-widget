---
description: "SDLC phase 1 — Requirements Analysis. Turns a description or a tracker link into a GIVEN-WHEN-THEN requirements document via the Business Analyst agent."
argument-hint: "[file-path | issue/story URL | free-text description]"
---

# sdlc-analysis

Input received: `$ARGUMENTS`

Delegate this entire task to the **business-analyst** subagent using the Task
tool (`subagent_type: "business-analyst"`). Do not perform the requirements
analysis yourself in this top-level context — the subagent owns this phase.

Before delegating:

1. If `.claude/sdlc.config.yaml` exists, note its contents so you can pass the
   relevant settings (`documentation` language, `paths.requirements`) to the
   subagent as part of its task context. If it doesn't exist, tell the
   subagent to use the documented defaults (`documentation: es`,
   `paths.requirements: docs/sdlc/requirements`).
2. Classify `$ARGUMENTS`:
   - If it looks like a path to an existing `.md` file, tell the subagent to
     read that file as its primary input.
   - If it looks like a URL (Azure DevOps, GitHub, GitLab, Jira, etc.), tell
     the subagent to fetch it as its primary input.
   - Otherwise, treat it as a free-text description and pass it verbatim.

Pass all of this to the subagent in a single, clear task description,
including the reminder that it must never run `git commit` or `git push`.

After the subagent finishes, relay its summary to the user, including the
path of the requirements document it created and any dependencies/open
questions/risks it flagged.

## Closing the loop on open questions

Read the "Dependencies" section of the requirements document that was
just created (it's shaped like `.github/ISSUE_TEMPLATE/feature_request.md`
— see the business-analyst agent's Output format):

1. If it says "None", or only contains `Depends on:`/`Related to:` items
   that are already satisfied, mention `sdlc-design` as the likely next
   step and stop here — there's nothing else to do.
2. If it contains any `Open question:` bullet, or a `Depends on:` item
   that is **not** yet confirmed as already in place, do **not** hand
   them off silently or let the user discover them buried in the file.
   Instead:
   - Rephrase each such item as a direct, answerable question (one per
     item) and present them to the user as a clear numbered list, asking
     them to confirm or correct each one so the requirements can be
     finalized before moving on.
   - Wait for the user's answers in their next message — do not guess or
     invent answers yourself, and do not proceed to `sdlc-design` in the
     meantime.
   - Once the user has answered, delegate back to the **business-analyst**
     subagent (`subagent_type: "business-analyst"`), passing the original
     requirements document's path, the open items, and the user's answers,
     and instruct it to update that same file in place: resolve each item
     under "Dependencies" (removing the `Open question:` bullet once
     resolved, or confirming a `Depends on:` item explicitly) and adjust
     any affected user story/acceptance criteria (e.g. if an answer
     changes a GIVEN-WHEN-THEN scenario).
   - Relay the updated summary to the user and only then mention
     `sdlc-design` as the next step.

