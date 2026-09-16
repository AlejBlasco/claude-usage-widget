# Skill: CQRS with Mediator (MediatR) — Architecture Decision

Load this skill when deciding whether to introduce CQRS/Mediator for a
.NET solution.

## When CQRS + Mediator actually earns its complexity

- The application layer has many distinct use cases (commands/queries) and
  a mediator keeps controllers/endpoints thin and decoupled from handler
  implementations.
- Read and write models are starting to diverge (different shapes, different
  performance needs) — CQRS lets them evolve independently.
- Cross-cutting concerns (validation, logging, authorization, transactions)
  need to be applied uniformly via pipeline behaviors instead of being
  duplicated per handler.

## When NOT to introduce it

- A small CRUD service with a handful of endpoints — plain
  service/repository classes are simpler to understand and maintain.
- The team has no prior exposure to the pattern and the project timeline
  doesn't allow for the ramp-up — document this as a trade-off if you
  recommend against it despite the "trendiness" of CQRS.

## Recommended shape when adopting it

- Library: **MediatR** (the de facto standard in the .NET ecosystem).
- One folder per feature/use case (vertical slice) containing its
  Command/Query, Handler, and Validator — prefer this over horizontal
  layers (`Commands/`, `Queries/`, `Handlers/` folders spread across the
  whole app) for discoverability.
- Pipeline behaviors for: validation (FluentValidation), logging, and
  transaction/unit-of-work wrapping around commands.
- Queries bypass the write model's business rules and can use lighter,
  read-optimized projections (e.g. Dapper or EF `AsNoTracking` +
  projection) when performance matters.

## Rationale template

Use the same `**Choice** / **Why** / **Alternative considered**` template as
`architecture-patterns.md` — CQRS/Mediator is exactly the kind of decision
that deserves ADR-style treatment (see `adr-writing.md`).
