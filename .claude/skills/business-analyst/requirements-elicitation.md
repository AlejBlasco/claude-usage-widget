# Skill: Requirements Elicitation

Load this skill when the input is vague, incomplete, or comes from an
informal source (a chat message, a one-line ticket title, a verbal request
relayed by the user).

## Checklist before writing requirements

- **Actors**: who triggers this behavior? End user, admin, another system, a
  scheduled job?
- **Trigger**: what starts the flow (an action, an event, a schedule)?
- **Happy path outcome**: what does success look like, concretely?
- **Failure modes**: what can go wrong, and what should happen when it does?
- **Boundaries**: what is explicitly out of scope for this piece of work?
- **Data**: what data is read, created, updated, or deleted?
- **Permissions**: does this require authorization/role checks?

## Handling missing information

Never invent business rules to fill a gap silently. Instead:

1. State the assumption you are making and mark it clearly — either as an
   `Open question:` bullet under "Dependencies" if it genuinely needs the
   user's confirmation, or noted inline where it applies (e.g. within a
   GIVEN-WHEN-THEN scenario or a Risk) if it's a reasonable default you're
   flagging rather than truly blocking.
2. If the assumption is high-risk (affects security, money, or data
   integrity), ask the user directly instead of assuming — that always
   goes in as an `Open question:` bullet, not a silent assumption.

## Red flags that require clarification

- Requirements phrased only as implementation ("add a column to the DB")
  with no stated business reason — dig for the underlying need.
- Contradictory statements between the ticket and what the user says in
  chat — surface the contradiction, don't silently pick one.
- Success criteria that can't be observed/tested ("make it better", "more
  robust") — push for a measurable definition.
