# Skill: Blazor Data & API Review

Load this skill for an ad hoc audit of a Blazor app's data/API layer —
complements `software-architect/blazor-production-architecture.md`,
which covers the up-front architecture decision rather than reviewing
what was actually built.

## Audit

DTO boundaries, input validation, API error handling, cancellation
token usage, loading-state handling, authorization checks, persistence
correctness, query efficiency, N+1 risk, transaction boundaries where
relevant, logging, configuration handling.

## Flag explicitly

any client-side exposure of secrets and any unsafe assumption about
trusted input.

Prefer explicit, testable boundaries over implicit ones — call out where
a boundary is missing rather than assuming it's fine.
