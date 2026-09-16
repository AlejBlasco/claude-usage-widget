# Skill: .NET 10 + Azure Architecture Choices

Load this skill when designing a solution that targets Microsoft's
ecosystem (.NET, Azure).

## Compute — pick based on workload shape

- **Azure App Service** — default choice for a standard web app/API with
  predictable traffic and no need for custom OS-level control.
- **Azure Container Apps** — default choice when the workload is
  containerized (Docker) and needs microservices, background processing, or
  event-driven scaling (KEDA) without managing Kubernetes directly.
- **Azure Kubernetes Service (AKS)** — only justify this when there's a real
  need for full Kubernetes control (multi-cluster, custom operators,
  existing K8s investment). Don't default to it "for scale."
- **Azure Functions** — event-driven, short-lived, bursty workloads
  (webhooks, queue processing, scheduled jobs).

## Data

- **Azure SQL Database** — default relational store when the domain is
  relational and the team already uses EF Core / SQL Server.
- **Azure Cosmos DB** — only when there's a genuine need for global
  distribution, flexible schema, or single-digit-ms latency at scale — not
  as a default "modern" choice.
- **Azure Cache for Redis** — for caching/session state, especially with
  multiple App Service/Container App instances.

## Identity & secrets

- **Microsoft Entra ID (Azure AD)** for authentication/authorization in
  Microsoft-ecosystem solutions, using OpenID Connect.
- **Azure Key Vault** for secrets/certificates — never hardcode connection
  strings or API keys; reference Key Vault via managed identity.
- **Managed Identities** — always prefer over service principals with
  stored secrets when the resource supports it.

## Observability

- **Application Insights / Azure Monitor** as the default telemetry stack
  for .NET apps — mention this in the design when the team doesn't already
  have an alternative in place.

## Rationale template (reuse from architecture-patterns.md)

```
**Choice**: <Azure service>
**Why**: <tied to the actual requirement — traffic pattern, team skillset,
existing infra>
**Alternative considered**: <service> — rejected because <reason>
```
