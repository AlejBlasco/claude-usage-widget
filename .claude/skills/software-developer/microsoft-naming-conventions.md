# Skill: Microsoft C#/.NET Naming & Design Conventions

Load this skill for every piece of C# code you write — this is the
baseline style checklist, based on Microsoft's official .NET naming and
design guidelines.

## Casing rules

| Element | Casing | Example |
|---|---|---|
| Namespace, class, record, struct, enum | PascalCase | `OrderService` |
| Interface | PascalCase, `I` prefix | `IOrderService` |
| Public method, property, event | PascalCase | `GetOrderAsync` |
| Public/protected field (avoid if possible) | PascalCase | rare, prefer properties |
| Private field | `_camelCase` | `_orderRepository` |
| Local variable, parameter | camelCase | `orderId` |
| Constant | PascalCase | `MaxRetryCount` |
| Generic type parameter | `T` prefix | `TEntity`, `TResult` |
| Async method | PascalCase + `Async` suffix | `SaveOrderAsync` |

## Naming guidance beyond casing

- Choose names that describe **intent**, not implementation
  (`CalculateTotalPrice`, not `LoopThroughItemsAndSum`).
- Boolean properties/methods read as an assertion:
  `IsValid`, `HasItems`, `CanCancel`.
- Avoid abbreviations except universally known ones (`Id`, `Url`, `Http`).
- Don't prefix class names with the project/module name — the namespace
  already provides that context.
- Enum names are singular (`OrderStatus`, not `OrderStatuses`); use `Flags`
  attribute + plural naming only for bit-flag enums.

## Design guideline highlights

- Prefer exceptions for exceptional/unexpected failures; use result
  types/return values for expected failure paths (validation errors,
  "not found") rather than exceptions as control flow.
- Favor immutability for DTOs and value objects (`record`, `init`-only
  properties).
- Keep public API surface minimal — default to `internal` and only expose
  what other assemblies genuinely need.
- XML documentation comments (`///`) on all public members of libraries;
  content language follows the `xmlDocComments` config setting, but the
  tags themselves (`<summary>`, `<param>`, `<returns>`) stay as-is —
  they're part of the .NET doc tooling, not translated.
