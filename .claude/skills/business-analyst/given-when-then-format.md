# Skill: GIVEN-WHEN-THEN Formatting

Load this skill whenever writing or reviewing acceptance criteria.

## Structure

- **GIVEN** — the initial context/state before the action (preconditions).
- **WHEN** — the single action or event being tested.
- **THEN** — the observable, verifiable outcome.

One scenario should test one behavior. Do not chain multiple unrelated
WHEN/THEN pairs into a single scenario.

## Good example

```
GIVEN a logged-in user with an empty shopping cart
WHEN they add a product that is in stock
THEN the cart shows 1 item and the total price updates accordingly
```

## Bad example (too vague, not testable)

```
GIVEN the user is on the site
WHEN they use the cart
THEN it works correctly
```

## Coverage checklist per user story

- Happy path (at least one scenario).
- At least one edge case (boundary values, empty states, max limits).
- At least one negative/error case (invalid input, unauthorized access,
  unavailable dependency).
- Idempotency/repetition case, if relevant (what happens if the same action
  is performed twice).

## Expanding an Acceptance Criteria checklist item into a scenario

Issues filed with this kit's `feature_request.md` template list Acceptance
Criteria as a flat checklist (`- [ ] ...`), not as GIVEN-WHEN-THEN. Each
checklist item is a terse statement of *what* must be true, not a full
scenario — don't just wrap it in GIVEN/WHEN/THEN labels unchanged. Instead:

1. Identify the implicit action (WHEN) and outcome (THEN) inside the
   checklist item.
2. Reconstruct a plausible precondition (GIVEN) that the item didn't
   bother stating, since checklists assume shared context.
3. Split one checklist item into **multiple** scenarios if it's really
   several behaviors bundled together (common with checklist items like
   "System handles parsing errors and displays clear messages" — that's
   at least one scenario per error type worth distinguishing).

Example — checklist item: `- [ ] System detects and validates expected
columns (title, date, description, location, capacity, etc.)`

Expanded scenarios:

```
GIVEN an uploaded CSV with all expected columns present
WHEN the system validates the file
THEN it proceeds to the preview step without errors

GIVEN an uploaded CSV missing a required column (e.g. "date")
WHEN the system validates the file
THEN it rejects the file and reports which column is missing
```
