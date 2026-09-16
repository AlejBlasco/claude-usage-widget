# Skill: Redesign Protocol

Source: [Taste Skill](https://github.com/Leonxlnx/taste-skill) (MIT),
Section 11 ("Redesign Protocol"), condensed and stack-agnostic. See also
`design-modes.md`'s "refinement preserves; redesign replaces" principle
— this file is the concrete audit process behind that distinction.

Load this skill when the task is modernizing/restyling an **existing**
UI, not building one from scratch. Misclassifying "redesign" as
"greenfield" (or vice versa) is the single biggest source of bad
redesign output — a full rebuild throws away IA and copy that were
working; a timid patch fails to fix a genuinely broken layout.

## First, detect the mode

- **Preserve** — modernize without breaking the brand. Audit first,
  extract what already works, evolve gradually.
- **Overhaul** — new visual language on top of existing content. Treat
  the visuals as greenfield; still preserve content and information
  architecture.

If ambiguous, ask **once**: *"Should this preserve the existing brand,
or are we starting visually from scratch?"*

## Audit before touching anything

Document the current state before proposing changes:
- Brand tokens already in use (colors, type, radii, logo treatment).
- Information architecture: page tree, primary nav, key conversion/task
  paths.
- What content is doing real work vs. what's filler.
- Patterns worth preserving (a signature interaction, a recognizable
  hero, the copy voice) vs. patterns worth retiring (broken layouts,
  dead links, generic stock imagery).
- The existing accessibility state — do not regress focus states, alt
  text, keyboard nav, or contrast that already worked.

## Preservation rules

- Don't change the information architecture unless asked — keep routes,
  anchor IDs, and primary nav labels stable (SEO and muscle memory both
  depend on them).
- Extract the existing brand color before applying any generic palette
  guidance — a brand that's already purple stays purple.
- Preserve the copy voice unless a rewrite was explicitly requested;
  visual modernization is not content modernization.
- Don't rename buttons, form fields, or section IDs that analytics or
  tests depend on.

## What never changes without explicit approval

URL/route structure, primary nav labels, form field names/order, the
brand logo/wordmark, existing legal/consent copy.

## Modernization levers, in priority order

Apply in order and stop once the brief is satisfied — most of the value
comes from the first few, at much lower risk than a full rebuild:

1. Typography refresh — the biggest visual lift per unit of risk.
2. Spacing/rhythm — section padding, vertical rhythm consistency.
3. Color recalibration — desaturate, unify neutrals, keep the brand
   accent.
4. Add motion appropriate to the product (see `motion-design.md` /
   `emil-kowalski-motion-craft.md`) to existing components.
5. Recompose the hero and other key sections.
6. Full block replacement — only when a block is genuinely
   unsalvageable, not as a default.

## Deciding preserve vs. full redesign vs. greenfield

- IA, content, and structure are sound → targeted evolution (levers
  1-4). Most of the value at a fraction of the risk.
- The visual debt is structural (broken IA, no consistent system, broken
  responsive behavior) → full redesign, with strict content
  preservation.
- The brand itself is changing → greenfield; the old site is now
  reference material, not a constraint.
