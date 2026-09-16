# Skill: Azure CI/CD for .NET (reserved for future use)

This skill is written for when the `sdlc-implementation` phase gets a real
workflow defined. It is not currently invoked by the DevOps Engineer agent
(see its main file — it is intentionally a stub).

## Pipeline shape (Azure DevOps Pipelines or GitHub Actions)

1. **Build**: `dotnet restore` → `dotnet build` → `dotnet test` (with
   coverage) → publish artifacts.
2. **Containerize**: build the Docker image (see
   `software-developer/docker-dotnet.md`), tag with the commit SHA and
   semantic version, push to Azure Container Registry (ACR).
3. **Deploy**: deploy to the target compute (App Service / Container Apps /
   AKS) for the appropriate environment, using slot swaps (App Service) or
   revision-based rollout (Container Apps) to avoid downtime.

## Environment promotion

- `dev` → `staging`/`qa` → `production`, with manual approval gates before
  production, at minimum.
- Use the same container image across environments (build once, promote the
  artifact) rather than rebuilding per environment — avoids
  "works in staging, breaks in prod" drift.

## Infrastructure as Code

- **Bicep** is Microsoft's native IaC language for Azure — prefer it for
  pure-Azure infrastructure.
- **Terraform** if the team already manages multi-cloud or non-Azure
  resources alongside Azure ones.
- Infrastructure changes go through the same review process as code —
  never applied by hand against production.

## Secrets & config per environment

- Store environment-specific settings in Azure App Configuration and/or
  Key Vault, referenced by the app via managed identity — never baked into
  the container image or committed to the repo.

## Reminder

Even once this phase is implemented, the DevOps Engineer agent must still
never execute `git commit`/`git push` — and any deployment action that
could affect a shared/production environment should require explicit human
confirmation at that step, not run unattended.
