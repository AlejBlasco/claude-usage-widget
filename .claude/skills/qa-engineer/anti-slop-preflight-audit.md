# Skill: Anti-Slop Pre-Flight Audit

Source: [Taste Skill](https://github.com/Leonxlnx/taste-skill) (MIT),
Section 14 ("Final Pre-Flight Check"), condensed to the stack-agnostic
checks — the original's React/Next/GSAP-specific items (Framer Motion
prop usage, `'use client'` isolation, ScrollTrigger skeleton compliance)
are dropped since they don't apply to this kit's stack. Complements
`anti-generic-ai-visual-critique.md` (holistic critique) and
`production-readiness-checklist.md` (broader release gate) with a more
mechanical, checkbox-level pass specifically for AI-generated-feeling
frontend output.

Load this skill for an ad hoc pre-release audit of a Persuade-mode
surface (landing page, marketing site, portfolio — see
`software-developer/design-modes.md`). Run every box; if a box fails,
the page is not done.

## Consistency locks

- [ ] One accent color used identically across every section — no
      section quietly introduces a second accent.
- [ ] One corner-radius system applied consistently (not round buttons
      on an otherwise sharp-edged layout).
- [ ] One theme (light, dark, or auto) for the whole page — no section
      flips to inverted mode mid-scroll.
- [ ] One design system/component library — no mixing two unrelated UI
      kits in the same page.

## Contrast and forms

- [ ] Every CTA's text is readable against its background, WCAG AA
      (4.5:1) minimum — no white-on-white, no unbordered ghost button
      over a photo.
- [ ] No CTA label wraps to 2+ lines at desktop.
- [ ] Form inputs, placeholders, focus rings, and error text all pass
      WCAG AA against the section background.

## Hero discipline

- [ ] Headline ≤ 2 lines, subtext ≤ 20 words and ≤ 4 lines, primary CTA
      visible without scrolling.
- [ ] At most 4 text elements in the hero (eyebrow-or-brand-strip,
      headline, subtext, CTAs) — no trust micro-strip or pricing teaser
      squeezed into the hero itself.
- [ ] No version/beta label in the hero unless the brief is genuinely
      about a launch.

## Layout variety

- [ ] No 3+ consecutive sections using the same layout family (e.g.
      image-left/text-right repeated three times in a row).
- [ ] No more than roughly 1 eyebrow (small uppercase label above a
      section headline) per 3 sections, hero included.
- [ ] No two CTAs on the page carry the same intent under different
      wording ("Get in touch" + "Let's talk" + "Start a project" all
      mean "contact" — pick one label).
- [ ] Logo walls show logos only, no category label printed underneath.

## Copy and content

- [ ] Every visible string re-read once for grammatical breaks or
      AI-sounding forced wordplay.
- [ ] Zero em-dashes (`—`) or en-dash-as-separator (`–`) anywhere
      visible — headlines, labels, body, quotes, attribution, alt text.
- [ ] Quotes/testimonials ≤ 3 lines of body, real attribution (name +
      role).
- [ ] No invented precise-sounding statistics presented as real data.

## Motion and responsiveness

- [ ] Every animation can be justified in one sentence (hierarchy,
      storytelling, feedback, or state transition) — none shipped "for
      show."
- [ ] Anything above a light motion level respects
      `prefers-reduced-motion`.
- [ ] Responsive behavior tested at desktop, tablet, mobile, and with
      unusually long content — not just the happy-path width.

If a single box can't be honestly ticked, the page isn't done — fix it
before handing back a release-ready verdict.
