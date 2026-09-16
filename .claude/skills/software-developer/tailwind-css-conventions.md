# Skill: Tailwind / CSS Engineering Conventions

Load this skill when the project uses Tailwind (or utility-first CSS) for
Blazor/Razor or any other frontend output. Skip it if the project uses a
different styling approach (CSS Modules, Sass, component libraries with
their own theming) — don't introduce Tailwind mid-project without a
reason.

## Rules

- Use design tokens (see `design-system-tokens.md`) instead of arbitrary
  values when a token should exist.
- Group styles by intent, not alphabetically.
- Avoid excessive utility duplication — extract a component class or a
  Razor component when the same utility cluster repeats.
- Use variants (e.g. component states) deliberately, not ad hoc.
- Preserve accessibility-related states (focus rings, disabled styles)
  when refactoring utility classes.
- Keep responsive utility classes explicit rather than relying on
  implicit inheritance.

## Responsive breakpoints

Design breakpoints around where content actually breaks, not around
device marketing names.

## CSS quality

Avoid specificity battles, magic numbers, duplicated values, accidental
layout dependencies, and fragile absolute positioning. Use custom CSS
when it genuinely improves clarity or enables an effect utilities would
make worse — don't force everything into utility classes on principle.
