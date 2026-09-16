# Skill: Safe Implementation Workflow

Load this skill for any change that touches more than a trivial single
file, or anything security/data-sensitive.

## Workflow

1. Restate the plan as a short checklist before touching files.
2. Make changes in logical, reviewable units — even though you won't commit
   them, structure your work as if each unit were a future commit.
3. After each unit, build the project to catch obvious breakage early
   rather than waiting until the very end. Do not run the test suite —
   that's not this agent's job and just burns tokens re-running tests that
   `sdlc-testing` will run properly anyway.
4. Re-read your own diff before declaring the task done — check for leftover
   debug code, unused imports, and consistency with the design doc.

## Risk flags — slow down and double-check when touching

- Authentication/authorization logic.
- Payment or billing code.
- Data deletion or destructive migrations.
- Anything that changes a public API contract.

## Reminder

Never stage, commit, or push. If you're tempted to run `git commit` "just to
checkpoint progress," don't — leave the working tree as-is and mention the
state of the changes in your summary instead.

Never write, modify, or run test files/test suites at any point in this
workflow. "The fast feedback loop" in step 3 means building only — no
`dotnet test`, `npm test`, or equivalent. Testing is entirely
`sdlc-testing`'s job, both writing and executing it.
