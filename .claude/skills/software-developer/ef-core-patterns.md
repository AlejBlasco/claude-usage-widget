# Skill: EF Core Best Practices

Load this skill whenever touching data access code with Entity Framework
Core.

## DbContext lifetime & usage

- `DbContext` is **scoped** per request/operation — never store it in a
  singleton or hold it across unrelated operations.
- Inject `DbContext` (or a repository wrapping it) via constructor
  injection; avoid `IDbContextFactory` unless the workload genuinely needs
  short-lived contexts outside the DI scope (e.g. background jobs, Blazor
  Server with long-lived circuits).

## Querying

- Use `AsNoTracking()` for read-only queries — this is the default you
  should reach for unless you're about to update the entity.
- Project directly to DTOs with `.Select(...)` for read/query use cases
  instead of loading full entities and mapping in memory — avoids
  over-fetching.
- Avoid N+1 queries: use `.Include()`/`.ThenInclude()` deliberately, or
  split into a second query, rather than lazy-loading in a loop.
- Always use the async EF Core methods (`ToListAsync`, `FirstOrDefaultAsync`,
  `SaveChangesAsync`, etc.).

## Migrations

- One migration per logical schema change, with a descriptive name
  (`dotnet ef migrations add AddOrderStatusColumn`).
- Never edit an already-applied migration that's been shared/deployed —
  add a new migration instead.
- Review the generated migration before considering the task done — EF's
  inferred migration isn't always what you intended, especially for
  renames (which EF sees as drop+add unless you help it with
  `migrationBuilder.RenameColumn`).

## Concurrency & transactions

- Use optimistic concurrency (`[ConcurrencyCheck]` / `RowVersion`) for
  entities that can be edited concurrently by multiple users.
- Wrap multi-step writes that must be atomic in an explicit
  `IDbContextTransaction`, or rely on `SaveChangesAsync`'s implicit
  transaction when a single `SaveChanges` call covers all the changes.
