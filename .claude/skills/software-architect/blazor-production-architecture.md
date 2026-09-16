# Skill: Blazor Production Architecture

Load this skill when deciding the overall architecture of a Blazor
application — the render mode and layering — not when reviewing a single
component (see `software-developer/blazor-components.md` and
`software-developer/blazor-ui-system.md` for that level).

## Choosing the render mode

Base the choice on the project's actual needs, not habit:
- interactivity required (rich client-side behavior vs. mostly static pages)
- SEO (server-rendered/prerendered content indexes better)
- hosting constraints and target infrastructure
- auth model
- API/data access needs
- persistence and offline requirements
- deployment target and expected scale

Cross-reference `dotnet-azure-architecture.md` once the render mode is
decided — it covers where the app actually runs (App Service, Container
Apps, etc.), not which Blazor model to use.

## Layering

Separate presentation / application / domain / infrastructure only where
it earns its keep. Do not add layers a small Blazor app doesn't need —
state explicitly in the design doc why each layer exists.

## Security review checklist

auth/authz, input validation, output encoding, secret handling, API
authorization, data access boundaries, least privilege.

## Performance review checklist

rendering behavior, data fetching, database query shape (N+1 risk),
payload size, caching, unnecessary re-renders.

Fold both checklists into the design doc's "Cross-Cutting Concerns"
section rather than producing a separate report.
