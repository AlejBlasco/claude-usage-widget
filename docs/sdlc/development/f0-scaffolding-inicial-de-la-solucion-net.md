# Implementation Summary: [F0] Scaffolding inicial de la solución .NET

## Design Reference

No hay documento de diseño previo (`sdlc-design`) para esta issue — se ha
implementado directamente a partir de la issue de GitHub
[#1](https://github.com/AlejBlasco/claude-usage-widget/issues/1) y del
contexto de arquitectura descrito en `CLAUDE.md` / `README.md` del repositorio.

## Files Changed

- `ClaudeMeter.sln` — nueva solución en la raíz del repo, con carpetas de
  solución `src` y `test`, referenciando los 8 proyectos.
- `Directory.Build.props` — nuevo, en la raíz. Centraliza `TargetFramework`
  (`net8.0`), `Nullable`, `ImplicitUsings`, `LangVersion`, analizadores
  Roslyn (`EnableNETAnalyzers`, `AnalysisLevel=latest`, `AnalysisMode`,
  `EnforceCodeStyleInBuild`) y `TreatWarningsAsErrors` condicionado a
  `Configuration == Release`, de forma que se hereda por todos los `.csproj`
  sin repetir configuración.
- `src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj` — nueva librería de
  clases .NET 8, sin dependencias de proyecto ni paquetes. Se eliminó el
  `Class1.cs` de plantilla.
- `src/ClaudeMeter.Application/ClaudeMeter.Application.csproj` — nueva
  librería de clases .NET 8, con `ProjectReference` a `ClaudeMeter.Domain`.
  Se eliminó el `Class1.cs` de plantilla.
- `src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj` — nueva
  librería de clases .NET 8, con `ProjectReference` a
  `ClaudeMeter.Application`. Se eliminó el `Class1.cs` de plantilla.
- `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — nueva app WPF
  (`Microsoft.NET.Sdk`, `OutputType=WinExe`, `TargetFramework=net8.0-windows`,
  `UseWPF=true`), con `ProjectReference` a `ClaudeMeter.Application` y
  `ClaudeMeter.Infrastructure`. Se conservó el `App.xaml`/`MainWindow.xaml`
  generados por la plantilla (son el shell mínimo necesario para que una app
  WPF compile y arranque, no lógica de negocio) — no se añadió BlazorWebView
  ni MediatR ni EF Core en este proyecto todavía.
- `test/ClaudeMeter.Domain.Tests/ClaudeMeter.Domain.Tests.csproj` — nuevo
  proyecto xUnit (`xunit`, `xunit.runner.visualstudio`,
  `Microsoft.NET.Test.Sdk`, `coverlet.collector`), con `ProjectReference` a
  `ClaudeMeter.Domain`. Sin ficheros de test (`UnitTest1.cs` de plantilla
  eliminado) — shell vacío, listo para que `sdlc-testing` añada casos.
- `test/ClaudeMeter.Application.Tests/ClaudeMeter.Application.Tests.csproj` —
  igual que el anterior, referenciando `ClaudeMeter.Application`.
- `test/ClaudeMeter.Infrastructure.Tests/ClaudeMeter.Infrastructure.Tests.csproj`
  — igual que el anterior, referenciando `ClaudeMeter.Infrastructure`.
- `test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj` — proyecto
  xUnit igual que los anteriores más el paquete `bunit` (v2.11.3, resuelta
  automáticamente vía `dotnet add package`), único proyecto de test con
  bUnit tal y como indica `CLAUDE.md` para componentes Razor. Su
  `TargetFramework` se fija explícitamente a `net8.0-windows` (en lugar de
  heredar el `net8.0` por defecto de `Directory.Build.props`) porque
  referencia a `ClaudeMeter.Desktop`, que también es `net8.0-windows`.
- `src/.gitkeep`, `test/.gitkeep` — eliminados, ya no son necesarios al
  haber contenido real en ambas carpetas.

## Deviations from the Design

- **Sin `.claude/sdlc.config.yaml` de diseño previo**: se trabajó
  directamente desde la issue de GitHub, no desde un `.md` de
  `sdlc-design`, tal y como permite el flujo del kit (issue/URL o texto
  libre).
- **`ClaudeMeter.Application` sin el paquete MediatR todavía**: `CLAUDE.md`
  describe Application como dependiente "solo de Domain y del paquete
  MediatR", pero el propio enunciado de la issue #1 (Technical/Scope notes)
  pide explícitamente no añadir MediatR/BlazorWebView/EF Core en esta fase
  de scaffolding, salvo que fuera estrictamente necesario para que el grafo
  de referencias compilara — no lo era, así que se ha dejado fuera. Se
  añadirá cuando se implementen los primeros casos de uso.
- **Carpetas de solución `src`/`test`**: no estaban pedidas explícitamente
  en la issue, pero se añadieron (vía `dotnet sln add --solution-folder`)
  porque reflejan la estructura física del repo y evitan que los 8
  proyectos aparezcan sin agrupar en Visual Studio/Rider.
- Todo lo demás se ha implementado tal y como describe la issue; no ha
  hecho falta ninguna otra desviación.

## How to Verify

```bash
# Desde la raíz del repo:
dotnet build ClaudeMeter.sln            # Debug, debe compilar 0 errores / 0 warnings
dotnet build ClaudeMeter.sln -c Release  # Release, con TreatWarningsAsErrors activo
```

Ambos comandos se han ejecutado como parte de esta implementación y han
finalizado con `Compilación correcta. 0 Advertencia(s) 0 Errores` para los
8 proyectos de la solución.

No se ha ejecutado `dotnet test` ni se ha escrito ningún fichero de test —
queda fuera del alcance de esta fase (`sdlc-development`); corresponde a
`sdlc-testing`.

## Follow-ups / Known Limitations

- Los proyectos de test están vacíos (solo referencias de paquete/proyecto,
  sin ningún caso de test) — pendiente de `sdlc-testing` para añadir la
  batería de tests que alcance el `testingCoverage: 70` configurado.
- `ClaudeMeter.Application` no referencia aún el paquete MediatR ni tiene
  puertos (`ITokenProvider`, `IUsageDataSource`, `IUsageHistoryStore`) — se
  añadirán en las issues de F0 posteriores que sí implementan lógica de
  negocio.
- `ClaudeMeter.Desktop` no tiene todavía `BlazorWebView` ni `MainWindow`
  configurada como borderless/topmost/transparente — corresponde a F1 según
  el roadmap de `CLAUDE.md`.
- No se ha creado `global.json`: el entorno tenía instalados los SDKs de
  .NET 9 y .NET 10 (no el SDK de .NET 8), pero ambos son capaces de
  compilar proyectos con `TargetFramework=net8.0`/`net8.0-windows` porque
  los runtimes/packs de .NET 8 sí están instalados. Si se quiere fijar
  estrictamente el SDK de compilación a 8.x, sería una mejora a considerar
  en una issue de CI/CD (F6/DevOps), no en este scaffolding.
