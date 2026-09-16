# Skill: Motion Design

Load this skill when adding animation/transitions to a UI, not as a
default pass on every build.

## Use motion for

orientation, hierarchy, feedback, continuity, delight — never as
decoration with no functional purpose.

## Define, per animation

duration, easing, trigger, distance/scale, and the reduced-motion
fallback (`prefers-reduced-motion`) — the last one is not optional.

## Avoid

constant/looping animation, distracting parallax, animating every
element on a page, motion that blocks or delays interaction.

Motion should feel native to the chosen visual direction (see
`style-direction.md`) — a brutalist direction and a soft-minimal
direction should not move the same way.
