# Skill: .NET Unit & Integration Testing (xUnit)

Load this skill for all backend (non-Blazor-UI) .NET testing. For Blazor
component testing, use `blazor-testing-bunit.md` instead/in addition.

## Framework defaults

- **xUnit** is the default test framework for .NET unless the repo already
  uses NUnit/MSTest — follow whatever's already there.
- **Moq** or **NSubstitute** for mocking dependencies — follow whichever
  is already used in the repo; don't mix both in the same project.
- **FluentAssertions** for readable assertions, if already a dependency —
  otherwise plain xUnit `Assert.*` is fine.

## Naming & structure

- Test class mirrors the class under test: `OrderServiceTests` for
  `OrderService`.
- Test method name states scenario and expectation:
  `MethodName_Scenario_ExpectedResult`
  (e.g. `CreateOrder_WhenStockIsInsufficient_ThrowsDomainException`).
- Arrange/Act/Assert structure, with blank lines separating the three
  sections for readability.

## Testing MediatR handlers

- Unit test handlers directly (construct the handler with mocked
  dependencies), not through the MediatR pipeline, for fast focused tests.
- Test pipeline behaviors (validation, logging) separately and in
  isolation from individual handlers.

## Testing EF Core code

- Prefer **Testcontainers** (real SQL Server/PostgreSQL in a disposable
  Docker container) for integration tests that exercise actual EF Core
  queries/migrations — the EF Core InMemory provider does not behave
  identically to a real relational database (no real constraints,
  different query translation) and can hide bugs.
- Reserve the InMemory provider for very fast, low-stakes unit tests where
  the SQL semantics genuinely don't matter.

## Testing ASP.NET Core APIs

- Use `WebApplicationFactory<TEntryPoint>` for integration tests that spin
  up the full API in-memory and issue real HTTP requests via
  `HttpClient` — this is the standard approach for testing minimal
  APIs/controllers end-to-end, including middleware and DI wiring.
- Override registered services (e.g. swap the real DbContext for a
  Testcontainers-backed one) via `WithWebHostBuilder` +
  `ConfigureTestServices`.

## Keep the test pyramid honest — integration tests are expensive

Integration tests (Testcontainers, `WebApplicationFactory`) are the most
expensive tests to write, run, and debug — each one can mean a real
database or a full app bootstrap. Do not use them as the default place to
cover business-rule edge cases; that's what unit tests (mocked
repositories/handlers) are for. Reserve integration tests for the handful
of scenarios that only a real DB/HTTP pipeline can actually verify, for
example:

- One or two tests per repository/query class confirming the EF query
  actually translates and executes against a real database (not that
  every branch of business logic behaves correctly — that's a unit test's
  job).
- One test per endpoint group confirming the HTTP pipeline, routing,
  auth/middleware, and serialization work end-to-end.
- Migrations apply cleanly against a real database.

If you find yourself writing an integration test to cover an `if` branch
or a validation rule, stop — write a unit test against the handler/service
with a mocked dependency instead. As a rule of thumb, integration tests
should be a small minority of the total test count, not the primary
coverage mechanism.

## Making Testcontainers cheap: share one container per run

Never let each test (or even each test class) spin up its own database
container — a fresh SQL Server container takes 10-30+ seconds to become
ready, and paying that cost per test turns a testing phase into the
slowest, most expensive part of the pipeline. Share a single container
across the whole integration test collection using xUnit's collection
fixtures:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public MsSqlContainer Container { get; } = new MsSqlBuilder().Build();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

[Collection("Database collection")]
public class OrderRepositoryTests
{
    public OrderRepositoryTests(DatabaseFixture fixture) { /* reuse fixture.Container */ }
}
```

All integration test classes join the same `[Collection("Database
collection")]` so xUnit starts the container exactly once for the whole
run, not once per class. If tests need isolated data, reset state between
tests (e.g. wrap each test in a transaction that's rolled back, or
truncate tables in a test fixture teardown) rather than spinning a new
container.

## Running tests without burning the budget

- **Filter, don't run everything.** While iterating (e.g. fixing a unit
  test failure), run only the touched project/class:
  `dotnet test --filter "FullyQualifiedName~OrderServiceTests"`. Never
  re-run the whole solution's test suite — unit and integration alike —
  as your default iteration loop.
- **Separate unit and integration by trait** so they can be run
  independently: `[Trait("Category", "Unit")]` /
  `[Trait("Category", "Integration")]`, then
  `dotnet test --filter "Category=Unit"` for the fast iteration loop and
  `dotnet test --filter "Category=Integration"` once, near the end, to
  confirm.
- **Treat the integration subset as a final confirmation pass, not part of
  the iteration loop.** Get unit tests green first (cheap, fast, no
  containers), then run integration tests once — not after every single
  change.
- **Keep output lean.** Use
  `dotnet test --logger "console;verbosity=minimal"` (or pipe through a
  filter for failures only) instead of reading full verbose output on
  every run — most of it is noise you don't need when tests are passing.

## Coverage tooling

- `dotnet test --collect:"XPlat Code Coverage"` (Coverlet) is the standard
  way to measure coverage in .NET — report the touched-code coverage
  against the `testingCoverage` config value, per
  `coverage-analysis.md`.
