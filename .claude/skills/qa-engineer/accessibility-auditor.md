# Skill: Accessibility Audit

Load this skill for an ad hoc accessibility review of UI (see the "Ad hoc
product quality audits" section of this agent's role) — this is a manual
review skill, separate from the automated unit-testing workflow this
agent normally runs.

## Review against practical WCAG principles

semantic HTML, heading hierarchy, keyboard navigation, focus visibility,
form labels, error messaging, color contrast, alt text, reduced-motion
support, touch target size, status announcements where appropriate
(e.g. live regions for async feedback).

## Prioritize real user impact

A missing `alt` on a decorative icon matters less than a form that
cannot be submitted via keyboard.

## Output

For each issue found: severity (Blocker/High/Medium/Low) and a concrete
remediation — not just "improve contrast," but which color pair and
which minimum ratio.
