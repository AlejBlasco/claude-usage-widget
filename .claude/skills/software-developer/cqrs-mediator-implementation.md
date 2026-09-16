# Skill: Implementing CQRS with MediatR

Load this skill when writing Commands, Queries, or Handlers in a project
that has adopted MediatR (see
`software-architect/cqrs-mediator-pattern.md` for the "when to use it"
decision).

## Naming conventions

- Commands: `<Verb><Noun>Command` (e.g. `CreateOrderCommand`,
  `CancelOrderCommand`).
- Queries: `Get<Noun>[By<Criteria>]Query` (e.g. `GetOrderByIdQuery`,
  `GetOrdersByCustomerQuery`).
- Handlers: `<CommandOrQueryName>Handler` (e.g. `CreateOrderCommandHandler`).
- Validators (FluentValidation): `<CommandOrQueryName>Validator`.

## Structure (vertical slice, one folder per use case)

```
Features/
  Orders/
    CreateOrder/
      CreateOrderCommand.cs
      CreateOrderCommandHandler.cs
      CreateOrderCommandValidator.cs
    GetOrderById/
      GetOrderByIdQuery.cs
      GetOrderByIdQueryHandler.cs
```

## Handler discipline

- One handler per command/query — do not reuse a handler for multiple
  message types.
- Commands return only what the caller needs to proceed (e.g. the new
  entity's ID), not the full entity, unless the caller genuinely needs it.
- Queries never mutate state — if a "query" needs to write (e.g. caching a
  computed result), that's a code smell worth flagging.
- Keep handlers thin: orchestration only — push actual business rules into
  domain entities/services, not into the handler body.

## Pipeline behaviors

- Validation behavior: short-circuit and return a validation failure before
  the handler runs, using FluentValidation validators registered per
  command/query.
- Logging behavior: log the request type and duration, not sensitive
  payload data.
- Transaction behavior: wrap command handlers (not queries) in a
  transaction/unit-of-work boundary when the handler performs multiple
  writes.
