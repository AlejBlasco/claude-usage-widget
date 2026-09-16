# Skill: SOLID Principles (applied in C#/.NET)

Load this skill whenever designing or reviewing classes/interfaces, not
just when explicitly asked about "SOLID" — it should shape how you write
code by default.

## S — Single Responsibility Principle

A class should have one reason to change. If a class both talks to the
database and contains business validation and formats output, split it.
In this kit's CQRS setup, this is naturally enforced: one handler per
use case, one responsibility per handler.

## O — Open/Closed Principle

Classes should be open for extension, closed for modification. Prefer
adding a new implementation of an interface (e.g. a new
`IPaymentProcessor` for a new payment method) over adding `if/else`/`switch`
branches to an existing class every time a new case appears.

## L — Liskov Substitution Principle

A derived class/implementation must be usable anywhere its base
type/interface is expected, without surprising behavior. Watch for
implementations that throw `NotImplementedException` for some interface
members — that's usually a sign the interface is wrong, not that the
exception is fine.

## I — Interface Segregation Principle

Prefer several small, focused interfaces over one large interface that
forces implementers to support methods they don't need
(`IOrderReader`/`IOrderWriter` instead of one fat `IOrderRepository` with
20 methods, when consumers only ever need one side).

## D — Dependency Inversion Principle

High-level modules (application/business logic) should depend on
abstractions, not concrete infrastructure. In this kit's layering
(`software-architect/dotnet-solution-structure.md`), this is why
`Application` defines interfaces like `IOrderRepository` and
`Infrastructure` implements them — never the other way around.

## Practical check before finishing a class

- Can I describe this class's job in one sentence without "and"?
- If I need a new variant of behavior, do I add a new class, or do I edit
  this one?
- Does everything implementing this interface actually need every member?
- Does this class new-up its own dependencies, or receive them via
  constructor injection?

Don't apply SOLID dogmatically to trivial code (a small DTO doesn't need an
interface) — use judgment, and note in the implementation summary if you
deliberately kept something simple despite a "textbook" SOLID concern.
