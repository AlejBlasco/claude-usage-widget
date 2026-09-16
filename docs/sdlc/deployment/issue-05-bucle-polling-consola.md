# Deployment Notes: [F0] Bucle de polling con salida por consola cada 60s

## Target Environment

No aplica ningún entorno de despliegue real (ni servidor, ni nube, ni
contenedor). `ClaudeMeter .NET` sigue en fase **F0**: aplicación de
escritorio Windows (WPF + BlazorWebView) que hoy solo se ejecuta localmente,
en la máquina del propio desarrollador, vía `dotnet run` / Visual Studio.

Verificado para esta fase:
- No existe `.github/workflows/` (comprobado: la carpeta no existe en el
  repositorio) ni ningún otro pipeline de CI/CD — `CLAUDE.md` marca CI/CD
  como "planned", no implementado.
- No hay `docker-compose*.yml` ni `Dockerfile*` en el repositorio, y no
  aplican a una app de escritorio/consola Windows.
- La distribución real (instalador Velopack/MSIX, autostart, auto-update)
  es el hito **F6**, que no ha comenzado.

Lo que sí introduce el issue #5, y que sí es relevante para esta fase, es un
**programa ejecutable nuevo** (`src/ClaudeMeter.Console`) pensado para
correrse de verdad, a mano, en el equipo del desarrollador — no una librería
pasiva como el issue #4. Por eso este documento incluye, además de la
evaluación de "no aplica infraestructura", una nota operativa breve de cómo
ejecutarlo localmente, distinta de un runbook de despliegue tradicional.

## Files Changed

Ninguno por parte de esta fase (DevOps/Implementación). Los ficheros de
`src/ClaudeMeter.Console/*` y `ClaudeMeter.sln` ya fueron creados/modificados
en la fase de desarrollo (`sdlc-development`, ver
`docs/sdlc/development/issue-05-bucle-polling-consola.md`) — no hay ningún
fichero de infraestructura, pipeline o configuración de despliegue que esta
fase deba crear o tocar.

## Assessment (infraestructura/CI)

Igual que en el issue #4, esta fase es un **no-op deliberado en cuanto a
infraestructura/CI**: no se generan Compose stacks, ni pipelines de CI/CD, ni
Dockerfiles, porque:

- No existe todavía ningún pipeline de build/CI al que "engancharse" (sigue
  "planned" según `CLAUDE.md`).
- No hay servidor remoto, nube ni Portainer/GitOps al que este cambio
  pueda desplegarse — es software de escritorio/consola que corre en local.
- El empaquetado/distribución (F6) no ha empezado.

Fabricar esos artefactos ahora sería infraestructura ficticia no solicitada,
contraria a las reglas de este agente. Esta conclusión no requiere
confirmación del usuario: `CLAUDE.md` ya deja explícito que el pipeline de
CI está solo planeado y que F6 no ha comenzado.

## Cómo ejecutar `ClaudeMeter.Console` en local (nota operativa, no un despliegue)

Esto no sustituye ni pretende ser un runbook de despliegue — es la
información práctica mínima para que quien revise el issue pueda arrancar
el harness de validación de F0 en su propia máquina:

1. **Requisito previo**: un fichero `.credentials.json` válido en
   `%USERPROFILE%\.claude\.credentials.json` (mismo fichero que usa Claude
   Code / la CLI de Anthropic), con un token OAuth real. Si no existe o no
   es válido, el programa no falla ni se cierra: imprime por `stderr` la
   línea "Token no disponible" en cada iteración y sigue corriendo.
2. **Build**: `dotnet build ClaudeMeter.sln` (o `-c Release`) desde la raíz
   del repositorio.
3. **Ejecución**: `dotnet run --project src/ClaudeMeter.Console` desde la
   raíz del repositorio.
4. **Salida esperada**: una línea por iteración en `stdout` en caso de
   éxito (`[HH:mm:ss] Sesión: X% (Y min) | Semana: ...`), o una línea de
   error correspondiente por `stderr` (token no disponible, 401/403, fallo
   de red, error inesperado) — nunca termina el proceso por sí solo.
5. **Cadencia**: primera línea inmediata, siguientes cada ~60s
   (`Task.Delay` tras completar cada llamada — ver diseño para el porqué).
6. **Cómo detenerlo**: no hay apagado controlado en F0 (decisión ya fijada
   en requisitos/diseño) — se para manualmente con `Ctrl+C` en la consola o
   cerrando la ventana del terminal.

**Aviso de coste real**: cada iteración exitosa hace una llamada real
`POST /v1/messages` (`max_tokens: 1`) contra la API de Anthropic y consume
una pequeña cantidad de cuota real de la cuenta cuyo token se use. Este
riesgo ya está documentado en el diseño (`docs/sdlc/design/issue-05-bucle-polling-consola.md`,
sección "Risks & Open Decisions") y es responsabilidad de quien ejecute el
programa, no de este agente.

## Manual Setup Required (one-time)

Ninguno de infraestructura. El único "setup" es el ya existente
`.credentials.json` en `%USERPROFILE%\.claude\.credentials.json` (no es algo
que este issue introduzca ni que este agente pueda o deba tocar — es un
fichero local del propio desarrollador).

## Deployment Flow

No aplica — no hay flujo de build→release→producción definido todavía para
este proyecto. El único "flujo" existente hoy es el manual descrito arriba
(`dotnet build` + `dotnet run` en la máquina del desarrollador).

## Rollback

No aplica — no hay ningún artefacto de despliegue que revertir. Si algo
falla al ejecutar el harness localmente, la única "reversión" es detener el
proceso (`Ctrl+C`) y, si aplica, volver al commit anterior con las
herramientas de git habituales del propio desarrollador (fuera del alcance
de este agente).

## Validación manual de extremo a extremo — estado: PENDIENTE

El propio diseño (`docs/sdlc/design/issue-05-bucle-polling-consola.md`,
paso 12 del "Implementation Plan") y el resumen de desarrollo
(`docs/sdlc/development/issue-05-bucle-polling-consola.md`, sección
"Deviations from the Design"/"Follow-ups") dejan constancia explícita de que
este paso **no se ha ejecutado todavía**:

> Ejecutar `dotnet run --project src/ClaudeMeter.Console` con un
> `.credentials.json` real y comprobar durante 2-3 iteraciones (~2-3
> minutos): (a) la primera línea aparece de inmediato; (b) las siguientes
> cada ~60s; (c) renombrar temporalmente `.credentials.json` produce la
> línea de error "Token no disponible" sin terminar el proceso, y restaurar
> el fichero vuelve a mostrar éxito en la siguiente iteración.

Esta fase (DevOps/Implementación) **no ejecuta este paso**, por regla
explícita: este agente no tiene acceso a red hacia hosts remotos y no debe
ejecutar el programa contra la API real ni consumir cuota real en nombre
del usuario. Sigue siendo, como ya estaba documentado, un paso manual
pendiente para quien revise/cierre el issue — ver la sección "Cómo ejecutar"
arriba para los pasos exactos.

## Follow-ups / Known Limitations

- **Validación manual de extremo a extremo (pendiente)**: ver sección
  anterior. Debe ejecutarla el propio desarrollador con su token real; no
  es delegable a este agente ni a ningún pipeline automático sin acceso a
  red/credenciales reales.
- Cuando exista de verdad el pipeline de CI planeado en `CLAUDE.md`
  (`restore → build → xUnit/bUnit → analyzers`), `ClaudeMeter.Console`
  debería incluirse en el `dotnet build`/`dotnet test` de la solución
  completa igual que el resto de proyectos — sin ningún paso especial,
  dado que no tiene dependencias nuevas ni requiere red en tiempo de
  compilación.
- Al ser un "arnés desechable" exclusivo de F0 (confirmado en el resumen de
  desarrollo: F1 reutilizará `IUsageDataSource`/`RateLimitWindowParser`
  directamente desde `ClaudeMeter.Desktop`, no este proyecto de consola),
  no se espera que este proyecto necesite nunca un pipeline de
  build/despliegue propio más allá de las comprobaciones de CI genéricas.
- El primer trabajo real de DevOps/empaquetado para este repositorio
  llegará, como pronto, en el hito **F6** (instalador Velopack/MSIX,
  autostart, auto-update) o al montar el pipeline de CI de `CLAUDE.md`.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio, y no se
ha ejecutado el programa contra la API real de Anthropic ni contra ningún
otro host remoto: este agente no tiene acceso de red a hosts remotos y no
debe consumir cuota real en nombre del usuario. La validación manual de
extremo a extremo (paso 12 del diseño) sigue pendiente y queda como acción
del propio desarrollador, tal y como ya señalaban las fases anteriores.
