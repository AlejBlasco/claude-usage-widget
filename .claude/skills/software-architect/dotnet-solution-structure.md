# Skill: .NET Solution Structure & Naming (Microsoft conventions)

Load this skill when designing where new code should live in a .NET
solution, or when scaffolding a new solution from scratch.

## Default layering (Clean Architecture, adjust to what already exists)

```
<Company>.<Product>.sln
src/
  <Company>.<Product>.Domain/         # entities, value objects, domain events
  <Company>.<Product>.Application/    # use cases, commands/queries, interfaces
  <Company>.<Product>.Infrastructure/ # EF Core, external services, Azure SDKs
  <Company>.<Product>.Api/            # ASP.NET Core minimal APIs / controllers
  <Company>.<Product>.Web/            # Blazor UI, if applicable
tests/
  <Company>.<Product>.UnitTests/
  <Company>.<Product>.IntegrationTests/
```

Adapt to the existing repo's structure if one already exists — consistency
beats textbook purity.

## Naming conventions (Microsoft's official guidance)

- **Namespaces/Projects**: `PascalCase`, dot-separated,
  `<Company>.<Product>.<Layer>` (e.g. `Contoso.Orders.Application`).
- **Classes, records, interfaces**: `PascalCase`; interfaces prefixed with
  `I` (e.g. `IOrderRepository`).
- **Methods, properties**: `PascalCase`.
- **Local variables, parameters**: `camelCase`.
- **Private fields**: `_camelCase` with leading underscore.
- **Constants**: `PascalCase` (not `ALL_CAPS` — that's not the .NET
  convention).
- **Async methods**: suffix with `Async` (e.g. `GetOrderAsync`).
- **Folders mirror namespaces** one-to-one.

See `software-developer/microsoft-naming-conventions.md` for the full
developer-facing checklist — this file is the architecture-level summary
used when laying out a new solution/module.
