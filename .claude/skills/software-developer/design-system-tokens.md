# Skill: Design System Tokens

Load this skill when a UI needs a consistent, reusable visual foundation
— more than one or two components — rather than a one-off page.

## Tokens to define

color roles, type scale, font weights, line heights, spacing, container
widths, breakpoints, radii, borders, shadows, motion timings, focus
rings, component heights.

## Semantic colors

Separate raw palette values from semantic roles: background, surface,
text, muted, accent, success, warning, danger, focus. Components should
reference the semantic role, never the raw value.

## Per component

For each component define: anatomy, variants, states, responsive
behavior, accessibility requirements, and content rules (what happens
with a missing image, a very long label, etc.).

## Rule

The system should be small enough to actually use consistently and rich
enough to prevent one-off styling. Prefer extending an existing token
over inventing a parallel one.
