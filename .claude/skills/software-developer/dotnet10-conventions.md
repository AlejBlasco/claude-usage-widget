# Skill: .NET 10 / C# Language Conventions

Load this skill for any code written against .NET 10.

## Prefer modern C# idioms already in use in the repo

- File-scoped namespaces (`namespace Foo.Bar;`) instead of block-scoped,
  unless the repo still uses block-scoped consistently.
- Primary constructors for simple DI-injected classes
  (`public class OrderService(IOrderRepository repo) : IOrderService`) when
  the class doesn't need extra constructor logic.
- `record`/`record struct` for immutable DTOs and value objects instead of
  plain classes with manual `Equals`/`GetHashCode`.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`) — respect
  the existing project setting; don't silently sprinkle `!` to bypass
  warnings, fix the actual nullability.
- Pattern matching (`is`, `switch` expressions) over chains of `if/else`
  when it improves readability.
- `async`/`await` all the way down for I/O-bound code — never block on async
  code with `.Result` or `.Wait()`.

## Minimal APIs vs Controllers

- Use **Minimal APIs** for small, focused endpoint sets or when the project
  already uses them.
- Use **Controllers** when the project already has a controller-based
  structure, or when the API surface is large enough that route grouping via
  attributes improves maintainability.
- Don't mix both styles within the same API surface without a clear reason.

## Configuration & options

- Bind configuration via the **Options pattern**
  (`IOptions<T>`/`IOptionsSnapshot<T>`) instead of reading
  `IConfiguration` directly inside business logic.

## Dependency Injection

- Register services with the narrowest lifetime that's correct
  (`Scoped` for anything using `DbContext`, `Singleton` only for genuinely
  stateless/thread-safe services, `Transient` for lightweight stateless
  helpers).
