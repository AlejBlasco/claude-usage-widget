# Skill: Blazor Component Conventions

Load this skill when implementing or modifying Blazor UI.

## Hosting model — respect what the project already uses

- **Blazor Server** — low-latency UI updates over SignalR, thin client,
  needs a persistent connection. Good default for internal/enterprise apps
  on a reliable network.
- **Blazor WebAssembly (WASM)** — runs client-side, works offline-ish,
  higher initial load. Good for public-facing apps needing to minimize
  server round-trips.
- **Blazor Auto (.NET 8+)** — starts as Server, switches to WASM once
  downloaded. Use when the project wants the best of both and has already
  opted into this model.

Do not switch hosting models as part of an unrelated feature change.

## Component conventions

- File name matches the component class name, `PascalCase.razor`
  (e.g. `OrderSummary.razor`).
- One component per responsibility — split large components instead of
  growing a single file with many concerns.
- Use `[Parameter]` for inputs, `[Parameter] public EventCallback<T>` for
  outputs — don't reach into parent state directly.
- Prefer `@code { }` blocks colocated in the `.razor` file for
  small/medium components; use a partial class (`.razor.cs`) for components
  with substantial logic, to keep markup and logic readable separately.
- Use `CascadingValue`/`CascadingParameter` sparingly — only for truly
  cross-cutting state (theme, current user), not as a shortcut for prop
  drilling in a couple of levels.

## State & performance

- Call `StateHasChanged()` only when necessary (e.g. after an external
  event outside Blazor's normal render pipeline) — don't sprinkle it
  defensively.
- Implement `IDisposable`/`IAsyncDisposable` for components that subscribe
  to events, timers, or JS interop, and unsubscribe/dispose properly.
- For Blazor Server, be mindful of what's kept in component state across a
  long-lived circuit — avoid unbounded growth (e.g. lists that only ever
  append).

## JS Interop

- Wrap `IJSRuntime` calls behind a small typed service instead of calling
  `InvokeAsync` directly from components, so it can be mocked in bUnit
  tests (see `qa-engineer/blazor-testing-bunit.md`).
