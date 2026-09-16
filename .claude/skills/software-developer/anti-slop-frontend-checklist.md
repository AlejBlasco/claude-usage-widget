# Skill: Anti-Slop Frontend Checklist

Source: [Taste Skill](https://github.com/Leonxlnx/taste-skill) (MIT).
The original is written for a React/Next/Tailwind/GSAP stack; this file
keeps only the stack-agnostic parts (the AI-tell catalog and the
calibration approach) and drops the framework-specific code. Applies to
landing pages, marketing sites, and portfolios — not to dense
admin/dashboard UI (see `design-modes.md`'s **Operate** mode for those;
for a Microsoft-ecosystem dashboard, reach for the official
`Microsoft.FluentUI.AspNetCore.Components` Blazor package rather than
hand-rolling one, the same way this source recommends official Fluent
UI for React).

Load this skill for any Persuade-mode surface (see `design-modes.md`),
in addition to `premium-design-constitution.md`.

## Read the brief before generating anything

State a one-line "design read" before producing markup: *"Reading this
as: \<page kind> for \<audience>, with a \<vibe> language."* If the brief
is genuinely ambiguous, ask exactly **one** clarifying question — never
a multi-question dump. If you can confidently infer it, don't ask.

## Calibrate with three dials instead of guessing

Rather than picking an aesthetic blindly, set explicit levels (1-10) and
let them drive layout/motion/density decisions consistently:

- **Variance** — 1 = perfectly symmetrical grid, 10 = asymmetric/artsy
  (masonry, fractional grid columns, large deliberate empty zones).
- **Motion** — 1 = static (`:hover`/`:active` only), 10 = advanced
  choreography (scroll-driven reveals, parallax).
- **Density** — 1 = airy/gallery-like (large section gaps), 10 =
  packed/cockpit (tight padding, no card containers, tabular numerals).

A "minimalist/calm/editorial" brief reads as low variance/motion, low-
mid density. A "playful/experimental/agency" brief reads as high
variance/motion. A "trust-first/public-sector/accessibility-critical"
brief reads as low on all three, regardless of what looks impressive.

## The AI-tell catalog

These are patterns that showed up repeatedly in real tests of
LLM-generated pages — avoid them by default, not because they're
inherently wrong but because they're the reflexive default and read as
templated the moment more than one appears together.

**Visual**: purple/blue glow gradients, pure `#000000` black, oversaturated
accent colors, gradient text on large headers, custom cursors.

**Typography**: Inter as the unexamined default sans; reaching for a
serif "because it feels premium/creative" with no other justification;
oversized H1s used instead of real hierarchy.

**Layout**: mathematically-identical spacing everywhere; three equal
feature cards in a row as the default feature section; more than 2
consecutive sections using the same left-image/right-text split
("zigzag"); a section header with a big left headline and a small
explainer paragraph floating in the top-right corner; an eyebrow
(small uppercase label) above *every* section — cap it at roughly 1 per
3 sections, hero included.

**Content**: generic placeholder names ("John Doe"); generic avatar
icons; unrealistically round numbers (`99.99%`, `50%` exactly); invented
startup-sounding brand names ("Acme", "Nexus", "SmartFlow"); filler verbs
("Elevate", "Seamless", "Unleash", "Next-Gen").

**Fake chrome**: div-based fake product screenshots/dashboards/terminals
built from styled rectangles; fake version footers (`v1.4.2`,
`Build 0048`) on a marketing page; version/beta labels in a hero
("V0.6", "EARLY ACCESS") unless the brief is genuinely about a launch;
section-number eyebrows (`00 / INDEX`, `001 · Capabilities`); rotated
vertical agency-portfolio text; scroll cues ("Scroll to explore", an
animated mouse-wheel icon) — if the visitor hasn't scrolled yet, they're
looking at the hero and already know it scrolls.

**Copy tells**: "Quietly in use at…" / "Quietly trusted by…" as a
social-proof header; poetic section labels ("Field notes", "On our
desks") standing in for a plain functional label; a micro-meta sentence
explaining the section directly under its eyebrow.

**Two CTAs with the same intent on one page** ("Get in touch" in the nav
+ "Let's talk" in the hero + "Start a project" in the footer are all
"contact" intent) — pick one label, use it everywhere.

**Logo walls**: never print a category label under a customer logo
("Vercel" + "hosting"); the logo alone is the credibility signal.

## The em-dash rule

No em-dash (`—`) or en-dash-as-separator (`–`) anywhere visible to the
user — not in headlines, labels, body copy, quote attribution, or alt
text. Use a regular hyphen, a comma, or restructure into two sentences.
This is the single most-cited "AI tell" in the source material — treat
it as binary, not "use sparingly."

## Mandatory, checkable details

- Every CTA's text is readable against its background at WCAG AA
  (4.5:1) — no white-on-white, no ghost button with no border over a
  photo.
- CTA labels fit on one line at desktop; 1-3 words for a primary CTA.
- Form inputs, placeholders, focus rings, and error text all pass WCAG
  AA against the section background.
- One accent color, one corner-radius system, one light/dark theme —
  applied identically across every section of the page. A page doesn't
  flip theme or palette mid-scroll.
- Quotes/testimonials: 3 lines of body max, real attribution (name +
  role, never just a first name).
- Before shipping, re-read every visible string (headlines, buttons,
  captions, alt text) and rewrite anything grammatically broken or that
  reads like forced AI wordplay.
