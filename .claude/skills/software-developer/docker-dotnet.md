# Skill: Docker for .NET Applications

Load this skill when containerizing a .NET app or editing an existing
Dockerfile/docker-compose setup.

## Multi-stage Dockerfile pattern

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/MyApp.Api/MyApp.Api.csproj", "MyApp.Api/"]
RUN dotnet restore "MyApp.Api/MyApp.Api.csproj"
COPY src/ .
WORKDIR /src/MyApp.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

Key points:

- Always multi-stage: SDK image for build, the much smaller ASP.NET runtime
  image for the final container.
- Copy `.csproj` files and `dotnet restore` **before** copying the rest of
  the source, so Docker layer caching avoids re-restoring on every code
  change.
- Use official `mcr.microsoft.com/dotnet/*` base images, pinned to the
  major version the project targets (`10.0`), not `latest`.
- Run as a non-root user in production images when the base image supports
  it.

## docker-compose for local development

- Compose the API together with its real dependencies (SQL Server/Azure SQL
  Edge emulator, Redis, Azurite for local Azure Storage emulation) so
  `docker compose up` gives a working local environment without manual
  setup.
- Use named volumes for database data so state survives container
  restarts during development.
- Keep secrets out of `docker-compose.yml` — use a local `.env` file that's
  gitignored, or `docker-compose.override.yml` for developer-specific
  values.

## .dockerignore

Always include one, mirroring `.gitignore` plus `bin/`, `obj/`, and
`**/node_modules` if there's a frontend part — keeps build context small
and avoids leaking local artifacts into the image.
