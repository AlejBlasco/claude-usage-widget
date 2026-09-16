# Skill: Architecture Patterns Reference

Load this skill when deciding how to structure a new feature or subsystem.

## Pick based on the actual problem, not fashion

- **Layered / N-tier**: default choice for most CRUD-shaped business
  features inside an existing monolith. Keep it unless there's a concrete
  reason not to.
- **Hexagonal / Ports & Adapters**: when the core logic must stay independent
  of a volatile external system (third-party API, swappable storage).
- **Event-driven**: when multiple independent consumers need to react to the
  same fact, or when decoupling producers/consumers in time matters.
- **CQRS**: only when read and write models genuinely diverge in shape or
  scale — not as a default for "clean" separation.
- **Microservice extraction**: only justify this when there's a real
  deployment, scaling, or ownership boundary — not because "microservices are
  best practice."

## Always check first

1. What does the existing repository already do for similar features? Prefer
   consistency over a theoretically "better" pattern.
2. What's the team's actual operational maturity (CI/CD, observability)? Do
   not propose patterns that assume infrastructure that doesn't exist.
3. What is the blast radius of getting this wrong? Higher risk → more
   conservative, well-understood pattern.

## Rationale template for each decision

```
**Choice**: <pattern/technology>
**Why**: <1-2 sentences tied to the actual requirement>
**Alternative considered**: <name> — rejected because <reason>
```
