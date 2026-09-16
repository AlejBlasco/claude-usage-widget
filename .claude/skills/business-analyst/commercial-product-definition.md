# Skill: Commercial Product Definition

Load this skill only when the request is explicitly to define a
sellable/reusable deliverable — a template, a product, a freelance
package meant to be sold or reused across clients — not a regular
feature for an existing internal system. For a normal feature request,
use the standard GIVEN-WHEN-THEN requirements shape instead.

This produces a **different** document shape than the standard
requirements doc — do not force commercial fields into the
GIVEN-WHEN-THEN format, and do not force feature requirements into this
one.

## Fields to capture

- Name and one-line promise
- Buyer/audience and the problem they have
- Outcome the product delivers
- What's included (contents, variants)
- Differentiator vs. alternatives
- Technology and compatibility
- Customization surface (what a buyer can change without breaking it —
  see `software-developer/premium-design-constitution.md`'s point about
  templates staying customizable)
- License and support policy
- Product ladder, if relevant: Free → Entry → Pro → Bundle → Custom
  Service — each tier must add meaningful value, not just remove an
  artificial limitation
- Pricing hypothesis: three scenarios (conservative / balanced /
  premium), explicitly labeled as hypotheses unless backed by actual
  sales or market research

## Guardrail

Do not invent market validation, testimonials, or sales data. Label
anything unverified as a hypothesis.

## Output shape

```markdown
# Product Definition: <Title>

## Buyer & Problem
...

## Promise & Outcome
...

## Contents
- ...

## Differentiator
...

## Technology & Compatibility
...

## Customization
...

## License & Support
...

## Product Ladder
Free / Entry / Pro / Bundle / Custom — ... (or "single tier")

## Pricing Hypotheses
- Conservative: ...
- Balanced: ...
- Premium: ...
```

Hand this document to `sdlc-design` the same way a normal requirements
document would be handed off — the Software Architect still owns
technology choices.
