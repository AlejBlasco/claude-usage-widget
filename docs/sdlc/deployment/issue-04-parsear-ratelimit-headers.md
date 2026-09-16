# Deployment Notes: [F0] Parsear headers anthropic-ratelimit-* a minutos/porcentaje

## Target Environment

No aplica ningún entorno de despliegue real todavía. `ClaudeMeter .NET` es una
aplicación de escritorio Windows (WPF + BlazorWebView) que en este momento
solo se ejecuta localmente (`dotnet run` / Visual Studio) durante desarrollo.
No existe ni un `.github/workflows/` (verificado: no hay ningún fichero bajo
`.github/workflows/` en el repositorio) ni ningún otro pipeline de CI/CD
configurado — el propio `CLAUDE.md` marca CI/CD como "planned", no
implementado. Tampoco existe infraestructura de contenedores ni de
orquestación (`docker-compose*.yml`) porque no aplica a una app de escritorio.
La distribución real (instalador Velopack/MSIX, autoupdate) es el hito **F6**
del roadmap, que aún no ha empezado. El proyecto está en fase **F0**.

## Files Changed

Ninguno. Esta fase (Implementación y Mantenimiento / DevOps) no ha requerido
ni justificado ningún cambio en ficheros de infraestructura, pipeline o
configuración de despliegue.

## Manual Setup Required (one-time)

Ninguno.

## Assessment

El issue #4 añade exclusivamente lógica de dominio pura (`RateLimitWindow` /
`RateLimitWindowParser` en `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs`)
más un analizador Roslyn en tiempo de compilación
(`Microsoft.CodeAnalysis.BannedApiAnalyzers` vía `BannedSymbols.txt`) para
prohibir `DateTime.Now`/`UtcNow` dentro de `ClaudeMeter.Domain`. No hay E/S,
no hay UI, no hay cambios de configuración en tiempo de ejecución, no se
añade ninguna dependencia externa que se resuelva en tiempo de ejecución (el
analizador es `PrivateAssets=all`, solo build-time), y no cambia en absoluto
cómo se compila, empaqueta, ejecuta o distribuye la aplicación.

Por tanto, esta fase es un **no-op deliberado**: no se generan ni Compose
stacks, ni pipelines de CI/CD, ni pasos de runbook, porque no existe todavía
ninguna infraestructura de build/despliegue a la que este cambio pueda
"engancharse", y fabricar esos artefactos ahora introduciría infraestructura
ficticia no solicitada y contraria a las reglas de este agente ("no asumas
un entorno greenfield" / "no inventes"). Esta conclusión no requiere
confirmación adicional del usuario: el propio `CLAUDE.md` ya documenta
explícitamente que el pipeline de CI es "planned" y que la distribución
(F6) no ha comenzado, así que no hay ambigüedad sobre el "target de
despliegue" — sencillamente todavía no existe ninguno.

**Única nota a tener en cuenta para el futuro** (no accionable hoy, no
requiere ningún cambio de fichero): a partir de este issue,
`ClaudeMeter.Domain.csproj` referencia
`Microsoft.CodeAnalysis.BannedApiAnalyzers` (versión `5.6.0`, ver
`docs/sdlc/development/issue-04-parsear-ratelimit-headers.md`) como
dependencia de compilación (analizador, `PrivateAssets=all`, no se propaga
en tiempo de ejecución ni al paquete final). Quien monte en el futuro el
pipeline de CI planeado en `CLAUDE.md` (`restore → build → xUnit/bUnit →
analyzers`) debe saber que este paquete ya forma parte del `restore`/`build`
de `ClaudeMeter.Domain` y que un build en Release fallará (por diseño, vía
`RS0030`) si se introduce `DateTime.Now`/`UtcNow`/`DateTimeOffset.Now`/
`DateTimeOffset.UtcNow` en ese proyecto. No requiere ninguna acción de CI
adicional más allá del `restore` habitual (el paquete se resuelve desde
NuGet.org como cualquier otro `PackageReference`).

## Deployment Flow

No aplica — no hay flujo de despliegue definido todavía para este proyecto.

## Rollback

No aplica — no hay ningún artefacto de despliegue que revertir en esta fase.

## Follow-ups / Known Limitations

- Cuando se implemente el pipeline de CI planeado (`CLAUDE.md` → sección
  "CI/CD (planned)"), tener en cuenta la nota anterior sobre
  `Microsoft.CodeAnalysis.BannedApiAnalyzers` como dependencia de build de
  `ClaudeMeter.Domain`.
- La primera vez que exista trabajo real de DevOps para este proyecto será,
  como pronto, en el hito **F6** (empaquetado/instalador con
  Velopack o MSIX, autostart, auto-update) o cuando se cree explícitamente
  el pipeline de CI de "restore → build → test → analyzers" descrito en
  `CLAUDE.md`. Hasta entonces, las fases `sdlc-implementation` sobre issues
  de tipo Domain/Application puro como este seguirán siendo, con toda
  probabilidad, no-ops documentados como este mismo fichero.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio, y no se
ha ejecutado ninguna acción contra ningún entorno remoto o en vivo: no
existía ninguno al que actuar. Este documento es el único artefacto de esta
fase.
