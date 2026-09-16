# Skill: Blazor Component Testing with bUnit

Load this skill when testing Blazor components (`.razor` files).

## What bUnit is for

bUnit renders Blazor components in an isolated test host (no browser
needed) so you can assert on the rendered markup, simulate user
interactions, and verify component behavior — it is the standard testing
library for Blazor components in the .NET ecosystem.

## Basic test shape

```csharp
public class OrderSummaryTests : TestContext
{
    [Fact]
    public void RendersOrderTotal_WhenOrderHasItems()
    {
        // Arrange
        var order = new OrderDto { Total = 42.50m };
        // Act
        var cut = RenderComponent<OrderSummary>(parameters => parameters
            .Add(p => p.Order, order));
        // Assert
        cut.Find("[data-testid='order-total']").TextContent
            .Should().Be("$42.50");
    }
}
```

## Conventions

- Add `data-testid="..."` attributes to key elements in components you
  expect to test, instead of relying on fragile CSS selectors or text
  matching.
- Use `cut.Find(...)`/`cut.FindAll(...)` to query rendered markup, and
  `cut.InvokeAsync(() => ...)` when triggering something that causes a
  re-render outside bUnit's normal event dispatch.
- Simulate user interaction via bUnit's element extensions
  (`cut.Find("button").Click()`, `input.Change("new value")`) rather than
  calling component methods directly — this exercises the same path a real
  user interaction would.

## Mocking dependencies

- Register mocked services (via the typed wrappers from
  `software-developer/blazor-components.md`, e.g. a mocked JS interop
  service) into bUnit's `Services` collection (`Services.AddSingleton<...>`)
  before rendering the component under test.
- For components using cascading parameters (auth state, theme), supply
  them explicitly via `RenderComponent<T>(parameters =>
  parameters.AddCascadingValue(...))`.

## What NOT to do

- Don't test Blazor's own rendering engine or routing — trust the
  framework, test your component's logic and markup output.
- Don't assert on exact whitespace/HTML formatting — assert on meaningful
  content (text, attribute values, element presence/absence).
