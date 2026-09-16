# Skill: Visual Design Non-Negotiables

Load this skill whenever you are writing markup/CSS/Razor that a real
user will look at — landing pages, marketing sites, customer-facing
Blazor UI, templates. Skip it for purely internal/admin tooling with no
design ambition.

## Non-negotiables

1. Design must have a point of view — don't default to a generic look.
2. Every visual choice needs a purpose.
3. Minimalism is not the same as emptiness; complexity is not the same
   as premium.
4. Content hierarchy comes before decoration.
5. Accessibility is part of quality, not a separate pass — see
   `qa-engineer/accessibility-auditor.md`.
6. Responsive behavior is designed, not accidental — see
   `responsive-engineering.md`.
7. A template/component library must stay easy to customize without
   breaking its own visual system.

## Avoid by default (the generic "AI-made" look)

purple/blue gradients, glassmorphism, floating blobs, giant rounded
cards, excessive shadows, Inter as the only typeface considered,
dashboard-card grids used for everything, fake metrics, centered hero +
three feature cards as the default layout, excessive icon decoration.

## Where premium perception actually comes from

typography, proportion, whitespace, restrained motion, precise detail,
coherent content — not decoration piled on top.

Pick a deliberate visual direction from the product's actual context
(editorial, technical, brutalist, corporate premium, soft minimal,
playful, etc. — see `style-direction.md`) instead of reaching for the
nearest SaaS template look.
