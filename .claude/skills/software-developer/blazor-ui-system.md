# Skill: Blazor UI System

Load this skill when building more than a couple of related Blazor
components that need to stay visually and structurally consistent — i.e.
when there's a design system to respect, not just one isolated component.
See `blazor-components.md` for the underlying per-component conventions.

## Before implementing large screens

Write a short component inventory: name, purpose, variants, and which
screens reuse it. This catches duplication before it gets built twice
under two different names.

## Prioritize

consistent `[Parameter]` naming across similar components, predictable
variants, semantic markup, keyboard/focus support, responsive behavior,
composability — small components composed into screens rather than one
large page component doing everything.

## Avoid

giant page-level `.razor` files that mix layout, business logic and
markup for many unrelated concerns. Split by responsibility instead.
