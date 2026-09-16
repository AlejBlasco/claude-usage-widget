# Skill: Design Modes (Persuade / Operate / Read / Experience)

Source: conceptual framework from Paul Bakaus's
[Impeccable](https://github.com/pbakaus/impeccable) (Apache-2.0). The
full tool is a heavyweight CLI with its own scripts and 35 reference
playbooks, out of scope for this kit — this file keeps only the
classification idea, which is genuinely useful on its own.

Load this skill before starting any UI-heavy work, to decide what kind
of interface you're actually building — this determines which other
skills matter most (`premium-design-constitution.md` and
`style-direction.md` for Persuade; `frontend-production-quality.md` and
plain accessibility/scanability for Operate).

## Pick the mode from the surface, not the product

A single product can contain surfaces in different modes — classify per
page/screen, not once for the whole app. A SaaS product's own landing
page is still **Persuade** even though its in-app dashboard is
**Operate**. A design agency's documentation page is still **Read** even
though their portfolio is **Experience**.

## The four modes

- **Persuade** — the visitor decides and acts; design *is* the product.
  Landing pages, marketing, pricing pages, campaigns. Earn attention and
  action. Expression and craft matter more here than anywhere else in
  the app.
- **Operate** — the visitor completes a task. App UI, dashboards,
  admin screens, settings, internal tools — most of what
  `software-developer` builds in this kit's typical Blazor projects.
  Scanability, consistency with existing patterns, and predictable
  interaction outrank visual expression. Brand shows up in precise
  details, not in loud composition.
- **Read** — the visitor understands something. Docs, articles,
  changelogs, help content. Structure for comprehension first, then make
  the reading experience worth staying in.
- **Experience** — the visitor is inside the work itself. Portfolios,
  galleries, showcases. The content leads; the interface recedes.

## Two supporting principles

- **The brief wins.** If the user has pinned an aesthetic, era, palette,
  or reference, honor it even when it conflicts with a general
  best-practice default (like `premium-design-constitution.md`'s
  anti-generic list). Redirecting a clear brief toward your own taste is
  a failure mode, not a improvement.
- **Refinement preserves; redesign replaces.** Refinement keeps the
  existing identity, behavior, copy, and everything outside the
  requested scope — ask before replacing factual copy or adding claims.
  Redesign treats the old look as evidence/anti-reference and replaces
  it deliberately, but still keeps product truth, content, and
  constraints. Never split the difference by half-polishing a look
  you've actually decided to discard — see `redesign-protocol.md` for
  the concrete audit steps.
