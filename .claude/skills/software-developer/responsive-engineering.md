# Skill: Responsive Engineering

Load this skill when building or reviewing layout that must work across
viewport sizes — treat it as layout adaptation, not proportional scaling.

## Per component, decide explicitly

what remains, what collapses, what stacks, what moves, what becomes
scrollable, what disappears, what changes interaction model entirely
(e.g. a table becoming a card list).

## Test at

wide desktop, standard laptop, tablet, narrow mobile, and with unusually
long content (long labels, long titles, missing images) — not just the
happy-path content used in the mockup.

Do not simply scale everything down with a single breakpoint change;
each component's adaptation is a design decision, not an afterthought.
