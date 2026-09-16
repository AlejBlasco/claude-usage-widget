# Skill: Unit Testing Strategy

Load this skill before writing any tests.

## What makes a test worth writing

- It fails when the behavior it targets breaks.
- It tests one behavior/unit of logic per test case.
- It's independent of test execution order and of other tests' state.
- It uses clear, descriptive test names that state the scenario and expected
  outcome (e.g. `returns_403_when_user_lacks_permission`).

## Coverage priorities (in order)

1. Business-critical logic (money, auth, data integrity).
2. Newly added/changed code from the current task.
3. Previously untested code that the new change now depends on.
4. Everything else, only if needed to hit the configured threshold.

## What NOT to do

- Do not test framework/library internals.
- Do not write tests that mock so much that they only verify the mocks were
  called, not real behavior.
- Do not delete or weaken existing tests to make new code "pass" — fix the
  code or the test's expectations honestly.
