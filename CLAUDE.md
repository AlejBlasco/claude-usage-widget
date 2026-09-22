# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**ClaudeMeter .NET** — a Windows desktop widget (Blazor Hybrid: WPF host + `BlazorWebView`) that shows real-time Claude Code usage (session/weekly rate limits, countdowns, a reactive "mascot" view). Personal project. The F0 solution scaffolding (`ClaudeMeter.sln` + the 4 layer projects under `src/` and their test projects under `test/`, empty of business logic) is in place; implementation of the roadmap phases below is still in progress.

Stack: .NET 8, WPF, BlazorWebView, MediatR, EF Core (SQLite, post-MVP), xUnit / bUnit, Serilog.

## Architecture

Clean Architecture, 4 layers, dependencies point inward (Desktop and Infrastructure depend on Application; Application depends only on Domain — never the reverse):

- **Domain** — pure core, no external dependencies: `UsageSnapshot`, `RateLimitWindow`, `MascotState` value objects.
- **Application** — use cases via MediatR queries/commands, plus small ports: `ITokenProvider`, `IUsageDataSource`, `IUsageHistoryStore` (deliberately no single catch-all `IUsageService`).
- **Infrastructure** — adapters implementing those ports: `AnthropicApiUsageDataSource`, `CredentialsFileTokenProvider`, `SqliteUsageHistoryStore`, tray icon integration.
- **Desktop** — presentation: borderless/topmost/transparent WPF `MainWindow`, Razor components, composition root.

Design rules to preserve as the code lands:
- Any `IUsageDataSource` (real API, simulated, future BLE) must return "no data" the same way rather than throwing for the expected/empty case (Liskov-style substitutability).
- A new data source (e.g. F7's `BleUsageSink`) must slot into Infrastructure with **zero changes to Domain/Application**.
- Reading the token, calling the API, computing the countdown, and rendering are separate classes — don't collapse them.
- Never implement the API's own OAuth token refresh; on 401/403 surface a clear error to the user instead.

## Roadmap (implementation order)

MVP, build in this order — each phase's core must work before layering the next:
- **F0** — console-only validation: read `.credentials.json` (plain token on Windows, no Credential Manager), call the API with the correct OAuth headers, parse `anthropic-ratelimit-*` headers into minutes/percentage, print every 60s.
- **F1** — first visual widget: borderless/topmost/transparent `MainWindow`, `UsagePage.razor` with session/week bars, threshold colors (green/amber/red), 60s poll reusing the F0 core.
- **F2** — robustness: no token self-refresh (401/403 → clear warning only), retry with backoff on transient failures, structured logging (Serilog) to `%LOCALAPPDATA%`, `config.json` for interval/position/chime.
- **F3** — UX polish + second view: light/dark theme, animated countdown, click-through, tray icon menu (pause/reload/quit), `ScreenNavigator` + `IWidgetScreen` to cycle pages, `MascotPage.razor` driven only by the current snapshot (no history dependency).

Post-MVP / optional — only pursued if the MVP proves worth extending further:
- **F4** — history + peak/off-peak (needs `SqliteUsageHistoryStore`/EF Core first): `UsageHistoryEntry`, `IUsageHistoryStore`, `PeakOffPeakAnalyzer` (pure, testable), `PeakOffPeakPage.razor`.
- **F5** — multi-account: poll multiple `config_dirs` per cycle, pick "active plan" by recent activity (mirrors the original Python daemon's behavior).
- **F6** — distribution: Velopack or MSIX installer, autostart + auto-update, versioning via Nerdbank.GitVersioning.
- **F7** (stretch) — real hardware: `BleUsageSink` in Infrastructure feeding a physical ClaudeMeter, reusing the same `UsageSnapshot`.

## Testing

- Domain/Application: pure xUnit, deterministic — given simulated rate-limit headers, assert the computed countdown.
- Infrastructure: fake `HttpMessageHandler` or WireMock.Net with recorded headers — **never a real token in tests**.
- Desktop: bUnit against the Razor components, without spinning up real WPF.
- Roslyn analyzers enabled; warnings as errors in Release builds.
- Target test coverage: 70% (per `.claude/sdlc.config.yaml`).

## CI/CD (planned)

- Every push: restore → build → xUnit + bUnit → analyzers. Infrastructure tests are always mocked — no real Anthropic token ever enters the pipeline.
- On tags/releases: separate job builds the installer (Velopack/MSIX) and publishes it as a release; versioning is automatic via Nerdbank.GitVersioning.

## SDLC agent pipeline

This repo uses the `.claude/` SDLC kit (`sdlc-analysis` → `sdlc-design` → `sdlc-development` → `sdlc-testing` → `sdlc-documentation` → `sdlc-implementation` slash commands, backed by dedicated agents). Config lives in `.claude/sdlc.config.yaml` and every agent must read it before working:

- In-code doc comments (XMLDoc etc.) and generated markdown docs (requirements/design/implementation/technical/functional): Spanish (`es`). GitHub issue templates: English (`en`).
- Generated artifacts go under `docs/sdlc/{requirements,design,development,technical,testing,deployment}` and `docs/functional`, created on demand.
- **When to launch it**: only decide this yourself for requests that arrive as free text, without naming an `/sdlc-*` command — if the user runs one explicitly, just do that phase, no triage needed. For a free-text request that's a trivial, single-file change with no new class/component/architectural decision (a hardcoded constant, a log level, a typo, a one-line condition fix), edit it directly and run the existing tests — skip the pipeline entirely. For anything else — a roadmap phase item (F3, F4...), a new port/interface, a new external integration, or a change spanning more than one layer — propose starting with `sdlc-analysis`.
- **Hard invariant**: no agent/command/skill in this kit may ever run `git commit`, `git push`, or equivalent — committing/pushing is always manual, done by the user.
