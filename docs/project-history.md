# Historial del proyecto por fases

Registro de lo entregado en cada fase del roadmap: **F0-F3** (MVP) y **F6** (distribución). Las fases **F4** (historial y pico/valle), **F5** (multicuenta) y **F7** (hardware BLE) del roadmap de [`CLAUDE.md`](../CLAUDE.md) son post-MVP y todavía no se han abordado.

**F0 — validación por consola** (issues #1-#5, cerradas):

- Lectura del token OAuth desde `.credentials.json`.
- Llamada a la API de Anthropic con los headers OAuth correctos.
- Parseo de las cabeceras `anthropic-ratelimit-*` a `RateLimitWindow` — porcentaje consumido y minutos restantes.
- Bucle de consola que imprime sesión/semana cada 60s y sigue vivo ante errores de red o token inválido, sin caerse.

**F1 — primer widget visual** (issues [#6](https://github.com/AlejBlasco/claude-usage-widget/issues/6)-[#9](https://github.com/AlejBlasco/claude-usage-widget/issues/9), tratadas como un único ciclo SDLC):

- `MainWindow` WPF sin bordes, siempre encima y transparente fuera del `BlazorWebView`, anclada a la esquina inferior derecha.
- `UsagePage.razor` con dos barras de progreso (sesión/semana) reutilizando el mismo core de F0, sin reimplementar parseo ni llamada HTTP.
- Color por umbral verde/ámbar/rojo (`UsageThresholdClassifier`, función pura en `Domain`) — misma regla para ambas barras.
- Refresco automático cada 60s (`UsagePollingCoordinator`), con guard anti-solape y liberación limpia del timer al cerrar la ventana.
- 132 tests en la solución (31 nuevos en `ClaudeMeter.Desktop.Tests`, 16 en `ClaudeMeter.Domain.Tests`), 94-100% de cobertura en las clases de negocio nuevas.

Pendiente de validación manual (no automatizable por la pipeline SDLC, ver [`docs/sdlc/testing/f1-widget-visual-base.md`](./sdlc/testing/f1-widget-visual-base.md)): comportamiento visual real de `MainWindow` en un equipo Windows, y estabilidad de memoria/handles en una ejecución prolongada (30-60 min).

**F2 — robustez** ([#10](https://github.com/AlejBlasco/claude-usage-widget/issues/10)-[#13](https://github.com/AlejBlasco/claude-usage-widget/issues/13)), en dos ciclos SDLC:

- **Ciclo A** — manejo de 401/403 con aviso claro en vez de refresco propio de token (`ReauthNotice`), reintentos con backoff ante fallos transitorios, logging estructurado (Serilog) a `%LOCALAPPDATA%\ClaudeMeter\logs`. 162 tests, 91.7-100% de cobertura en las clases de negocio nuevas. Detalle en [`docs/sdlc/technical/f2-robustez-ciclo-a.md`](./sdlc/technical/f2-robustez-ciclo-a.md).
- **Ciclo B** — `config.json` para intervalo de polling, posición inicial y chime activable/desactivable (umbral crítico, una sola vez por transición); arrastrar el widget con el ratón y persistir la nueva posición para el siguiente arranque. 216 tests, 94-100% de cobertura en las clases de negocio nuevas. Validado a mano en Windows real, incluyendo un mecanismo de arrastre corregido tres veces en vivo durante esa validación (`Window.DragMove()` no es compatible con `BlazorWebView`/WebView2 por un problema de captura de ratón entre procesos; sustituido por Pointer Events + `setPointerCapture` reenviando deltas por JS interop, más un fix de hit-test para un bug documentado de WPF+WebView2 con `AllowsTransparency`). Detalle técnico en [`docs/sdlc/technical/f2-robustez-ciclo-b.md`](./sdlc/technical/f2-robustez-ciclo-b.md), explicación sin jerga en [`docs/functional/f2-robustez-ciclo-b.md`](./functional/f2-robustez-ciclo-b.md).

**F3 — UX + página Mascota** (milestone 4), en tres ciclos SDLC:

- **Ciclo A** — tema claro/oscuro (paleta propia por tema, sin regresión visual sobre F1/F2), countdown animado al cambiar de minuto, auto-ajuste de altura de ventana al contenido real (`ResizeObserver` vía JS interop, en vez de una altura fija que recortaba contenido con "Tamaño de texto" de Windows alto). Detalle en [`docs/sdlc/technical/f3-ux-mascota-ciclo-a.md`](./sdlc/technical/f3-ux-mascota-ciclo-a.md).
- **Ciclo B** — click-through configurable desde la bandeja ([#16](https://github.com/AlejBlasco/claude-usage-widget/issues/16)), icono de bandeja (`NotifyIcon`) con menú Pausar/Reanudar, Recargar, Ignorar clics y Salir, y botón de cierre directo `✕` en la esquina del propio widget ([#17](https://github.com/AlejBlasco/claude-usage-widget/issues/17); antes solo se podía cerrar desde el Administrador de tareas). Detalle en [`docs/sdlc/technical/f3-ux-mascota-ciclo-b.md`](./sdlc/technical/f3-ux-mascota-ciclo-b.md).
- **Ciclo C** — `ScreenNavigator`/`IWidgetScreen` (nuevo `RootComponent`, sustituye a `UsagePage` como único componente Razor garantizado vivo durante toda la app) con un botón `⇄` para alternar pantallas, y `MascotPage.razor` ("Clawd"), un segundo view derivado únicamente del snapshot actual — ver [Iconos y textos del widget](../README.md#iconos-y-textos-del-widget). Detalle técnico en [`docs/sdlc/technical/f3-ux-mascota-ciclo-c.md`](./sdlc/technical/f3-ux-mascota-ciclo-c.md), explicación sin jerga en [`docs/functional/f3-ux-mascota-ciclo-c.md`](./functional/f3-ux-mascota-ciclo-c.md).

**F6 — Distribución** (milestone 7, issues [#25](https://github.com/AlejBlasco/claude-usage-widget/issues/25)-[#27](https://github.com/AlejBlasco/claude-usage-widget/issues/27)), tres historias encadenadas. A diferencia de F1-F3, no toca `Domain` ni `Application`: es trabajo de build, CI/CD y ciclo de vida del host de escritorio.

- **US-1 (#27) — versionado automático** con Nerdbank.GitVersioning (`version.json`): la versión se calcula desde el historial de git, sin tocarla a mano. Desviación documentada del diseño: la referencia al paquete se repite en cada `.csproj` en lugar de centralizarla en `Directory.Build.props`, porque ahí rompe `dotnet build` a nivel de solución con `NETSDK1013`.
- **US-2 (#25) — empaquetado y CI/CD**: `ci.yml` (build + tests en cada push y PR) y `release.yml` (al empujar un tag `v*`, empaqueta con Velopack y publica el instalador como asset de la release).
- **US-3 (#26) — autoarranque y auto-actualización**: `AutoStartService` (clave `Run` del registro, con la ruta estable del stub de Velopack y no la de `current\`, que cambia en cada actualización), `AppUpdateService` (envuelve `UpdateManager` + `GithubSource`, comprobación al arrancar y cada 4 h, degradación silenciosa sin conexión) y un nuevo ítem "Iniciar con Windows" en el menú de la bandeja, desactivado por defecto.

Detalle técnico en [`docs/sdlc/technical/f6-distribucion.md`](./sdlc/technical/f6-distribucion.md), explicación sin jerga en [`docs/functional/f6-distribucion.md`](./functional/f6-distribucion.md) y resultados de test en [`docs/sdlc/testing/f6-distribucion.md`](./sdlc/testing/f6-distribucion.md). Mergeada en `master` (PR #36); la primera release publicada (`v1.0.0`) queda pendiente de empujar el tag.

Roadmap completo por fases en [`CLAUDE.md`](../CLAUDE.md).
