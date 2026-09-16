# Skill: Motion Craft (Emil Kowalski)

Source: Emil Kowalski, [animations.dev](https://animations.dev) /
[github.com/emilkowalski/skills](https://github.com/emilkowalski/skills)
(`emil-design-eng`, MIT). Condensed and adapted here for a CSS/Razor
context — drop the React/Framer Motion-specific hooks from the original
and keep the parts that transfer to any stack.

Load this skill whenever writing or reviewing CSS transitions/animations
— it goes deeper than `motion-design.md`'s overview with concrete
decision rules and code-level patterns.

## Core philosophy

Good taste is trained, not innate: study why the best interfaces feel
the way they do instead of guessing. Most of what makes an interface
feel great is **unseen details that compound** — no single one is
noticeable, but their absence is. Beauty is leverage: people pick tools
based on the whole experience, not just functionality.

## The animation decision framework

Answer these, in order, before writing any animation:

### 1. Should this animate at all?

| Frequency | Decision |
|---|---|
| 100+ times/day (keyboard shortcuts, command palette toggle) | No animation. Ever. |
| Tens of times/day (hover, list navigation) | Remove or drastically reduce |
| Occasional (modals, drawers, toasts) | Standard animation |
| Rare/first-time (onboarding, feedback forms, celebrations) | Can add delight |

Never animate keyboard-initiated actions — they're repeated too often
for animation to feel like anything but delay.

### 2. What is the purpose?

Valid purposes: spatial consistency (a toast always exits the direction
it entered, making swipe-to-dismiss intuitive), state indication,
explanation, feedback, preventing jarring changes. If the only answer is
"it looks cool" and the user will see it often, don't animate.

### 3. What easing?

- Entering/exiting → `ease-out` (starts fast, feels responsive)
- Moving/morphing on screen → `ease-in-out`
- Hover/color change → `ease`
- Constant motion (marquee, progress bar) → `linear`

**Never use `ease-in` for UI animations** — it delays the initial
movement, which is the exact moment the user is watching most closely,
making the same duration feel slower.

Use custom easing curves — the CSS built-ins are too weak:

```css
--ease-out: cubic-bezier(0.23, 1, 0.32, 1);
--ease-in-out: cubic-bezier(0.77, 0, 0.175, 1);
--ease-drawer: cubic-bezier(0.32, 0.72, 0, 1); /* iOS-like drawer curve */
```

### 4. How fast?

| Element | Duration |
|---|---|
| Button press feedback | 100-160ms |
| Tooltips, small popovers | 125-200ms |
| Dropdowns, selects | 150-250ms |
| Modals, drawers | 200-500ms |
| Marketing/explanatory | Can be longer |

**Rule: UI animations stay under 300ms.** Perceived speed matters as
much as real speed — a faster spinner makes loading *feel* faster even
at identical load time, and `ease-out` at 200ms feels faster than
`ease-in` at 200ms.

## Springs

Springs simulate physics instead of a fixed duration — use them for drag
with momentum, elements that should feel "alive," and gestures that can
be interrupted mid-animation. If the animation library available
supports spring configs, prefer the duration+bounce form
(`{ duration: 0.5, bounce: 0.2 }`) over raw mass/stiffness/damping — it's
easier to reason about. Keep bounce subtle (0.1-0.3); most UI contexts
shouldn't bounce at all. Springs' key advantage: they maintain velocity
when interrupted, where CSS keyframe animations restart from zero — this
matters for anything a user might reverse mid-gesture (expand then hit
Escape).

## Component-level rules

- **Buttons must feel responsive**: `transform: scale(0.97)` on
  `:active`, `transition: transform 160ms ease-out`.
- **Never animate from `scale(0)`** — nothing in the real world
  disappears/reappears from nothing. Start from `scale(0.9)`+ combined
  with `opacity`.
- **Popovers scale from their trigger**, not from center
  (`transform-origin: var(--transform-origin)`). Exception: modals stay
  centered — they aren't anchored to a specific trigger.
- **Tooltips skip the delay on subsequent hovers**: once one tooltip is
  open, adjacent tooltips open instantly (`transition-duration: 0` via a
  `data-instant` state) — feels faster without losing the point of the
  initial delay.
- **Prefer CSS transitions over `@keyframes` for anything rapidly
  re-triggered** (toasts, stacked notifications) — transitions can be
  interrupted and retargeted mid-flight; keyframes restart from zero.
- **Use blur to mask an imperfect crossfade.** If two states swapping
  looks unnatural despite tuning easing/duration, add
  `filter: blur(2px)` during the transition — it blends the two states
  instead of showing them overlap. Keep it under ~20px; heavy blur is
  expensive, especially in Safari.
- **Animate enter states with `@starting-style`** where supported —
  it replaces the common "mount, then set a flag in an effect to
  trigger the enter transition" workaround.

```css
.toast {
  opacity: 1;
  transform: translateY(0);
  transition: opacity 400ms ease, transform 400ms ease;
  @starting-style {
    opacity: 0;
    transform: translateY(100%);
  }
}
```

## CSS transform notes

- `translateY(100%)`/`translateY(-100%)` moves an element by its own
  size regardless of actual dimensions — prefer this over hardcoded
  pixels for hide/reveal patterns (drawers, toasts).
- `scale()` scales children too (font size, icons, content scale
  proportionally) — this is usually what you want for a pressed-button
  effect.
- Set `transform-origin` explicitly to match the trigger location for
  origin-aware interactions; the default (center) is wrong for almost
  every popover.

## Performance rules

- **Animate only `transform` and `opacity`** — these skip layout and
  paint. Animating `padding`/`margin`/`height`/`width` triggers all
  three rendering steps.
- **A CSS custom property set on a parent recalculates style for every
  descendant.** If updating a value on every frame (drag position,
  scroll offset), set it directly on the element that needs it, not on
  a shared ancestor.
- Prefer CSS animations for predetermined motion (they run off the main
  thread and stay smooth even while the page is busy loading/scripting);
  reserve JS-driven animation for genuinely dynamic/interruptible cases.

## Accessibility

```css
@media (prefers-reduced-motion: reduce) {
  .element {
    animation: fade 0.2s ease; /* keep opacity/color, remove movement */
  }
}
```

Reduced motion means fewer/gentler animations, not zero — keep
opacity/color transitions that aid comprehension, remove
movement/position animations.

Gate hover-triggered animation behind
`@media (hover: hover) and (pointer: fine)` — touch devices fire hover
on tap, causing false positives.

## Stagger

When multiple elements enter together, stagger them (30-80ms between
items) rather than have everything appear at once — it reads as more
natural. Keep delays short; long stagger makes the interface feel slow.
Stagger is decorative — never block interaction while it plays.

## Debugging

Play animations at 2-5x their normal duration (or use the browser's
animation inspector) to catch: states that visibly overlap instead of
blending, easing that starts/stops abruptly, a `transform-origin` that's
visibly wrong, or properties that drift out of sync. Review your own
animations again the next day — you notice things with fresh eyes that
you missed while building.

## Review checklist

| Issue | Fix |
|---|---|
| `transition: all 300ms` | Specify exact properties: `transition: transform 200ms ease-out` |
| `scale(0)` entry animation | Start from `scale(0.95)` + `opacity: 0` |
| `ease-in` on a UI element | Switch to `ease-out` or a custom curve |
| `transform-origin: center` on a popover | Set to the trigger location (modals are exempt) |
| Animation on a keyboard-triggered action | Remove it entirely |
| Duration > 300ms on a UI element | Reduce to 150-250ms |
| Hover animation with no `(hover: hover)` gate | Add the media query |
| Same enter/exit speed | Make exit faster than enter |
| Several elements appearing at once | Add 30-80ms stagger |
