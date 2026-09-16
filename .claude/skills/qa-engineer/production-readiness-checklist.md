# Skill: Production Readiness Checklist

Load this skill for an ad hoc, release-blocking audit of a UI-facing
product/deliverable — broader than accessibility or performance alone.

## Categories

**Visual** — hierarchy, typography, spacing, imagery, consistency,
originality.
**UX** — navigation, primary flow, feedback, empty/loading/error states,
form validation.
**Responsive** — desktop, tablet, mobile, long content.
**Accessibility** — keyboard, focus, semantic structure, contrast,
labels, reduced motion.
**Technical** — bugs, duplication, maintainability, performance,
security, dependency/configuration issues.
**Commercial** (only if the deliverable is being sold/handed off as a
product) — positioning, differentiation, demo quality, product scope,
documentation.

## Classify every finding

BLOCKER / HIGH / MEDIUM / LOW. Never call a deliverable release-ready
while blockers remain.

## Checklist output

```markdown
## Release Checklist: <Title>
- [ ] Main flows work
- [ ] Loading/empty/error/success states covered
- [ ] Forms validated
- [ ] Desktop / tablet / mobile / long-content responsive
- [ ] Keyboard, focus, contrast, semantics, reduced motion
- [ ] Errors handled, security reviewed, performance reviewed
- [ ] (Commercial only) Demo, screenshots, docs, license, pricing ready
```
