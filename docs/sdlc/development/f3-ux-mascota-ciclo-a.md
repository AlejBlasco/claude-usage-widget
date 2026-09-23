# Implementation Summary: F3 — UX + página Mascota, Ciclo A

## Design Reference

`docs/sdlc/design/f3-ux-mascota-ciclo-a.md` (US-1 tema claro/oscuro, issue
#14; US-2 animación del countdown, issue #15). Se ha seguido su
Implementation Plan paso a paso (pasos 1, 2, 4, 6, 7, 8 y 11; los pasos 3,
5, 9 y 10 son de tests y quedan fuera de esta fase — ver Deviations).

## Files Changed

- `src/ClaudeMeter.Desktop/Configuration/AppConfig.cs` — nuevo enum
  `AppTheme` (`Dark`/`Light`), `AppThemeExtensions.ToCssClass()` (única
  fuente del mapeo enum→clase CSS) y nuevo campo `Theme` en `AppConfig`
  (incluido en `AppConfig.Default`, por defecto `Dark`).
- `src/ClaudeMeter.Desktop/Configuration/AppConfigStore.cs` — nuevo
  `AppConfigDto.Theme` (`string?`), `ResolveTheme(string?)` (ausente →
  `Dark` en silencio; `"dark"`/`"light"` case-insensitive → tema
  correspondiente; otro valor de cadena → `Dark` + `Warning`; tipo JSON no
  válido ya cae al bloque `catch (JsonException ...)` existente de
  `Load()`), y las líneas correspondientes de `Load()`/`Save()`.
- `src/ClaudeMeter.Desktop/Rendering/CountdownFormatter.cs` (nuevo) —
  función pura `Format(int minutesRemaining)` que compacta
  `RateLimitWindow.MinutesRemaining` (`"0m"` / `"Nm"` / `"NhNm"` / `"NdNh"`).
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` — envuelve el `@if/else`
  existente (`ReauthNotice` / `.usage-widget`) en
  `<div class="claudemeter-root @Config.Theme.ToCssClass()">`. Ningún otro
  cambio en el fichero.
- `src/ClaudeMeter.Desktop/Pages/UsageBar.razor` — añade
  `@using ClaudeMeter.Desktop.Rendering` y un bloque
  `@if (Window.MinutesRemaining is { } minutes)` con
  `<span class="usage-bar__countdown" @key="minutes">Restablece en
  @CountdownFormatter.Format(minutes)</span>` tal cual el Data Model del
  diseño.
- `src/ClaudeMeter.Desktop/wwwroot/css/app.css` — reescrito: custom
  properties por tema en `.claudemeter-root.theme-dark` (valores idénticos
  a los hardcodeados hasta F2) y `.claudemeter-root.theme-light` (paleta
  clara con umbrales propios, más saturados), todas las reglas existentes
  migradas a `var(--...)`, y el nuevo `@keyframes usage-countdown-fade`
  aplicado a `.usage-bar__countdown`. `html, body` conservan su fondo
  oscuro hardcodeado como fallback pre-render (sin cambios de intención
  respecto al diseño).

## Deviations from the Design

- **`AppConfig.Theme` con valor por defecto en la firma del constructor
  posicional** (`AppTheme Theme = AppTheme.Dark`), en vez de un parámetro
  obligatorio como muestra literalmente el Data Model del documento de
  diseño. Motivo: `test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs`
  ya contiene dos construcciones posicionales de `AppConfig` anteriores a
  este ciclo (líneas ~145 y ~225) que no pasan `Theme`; sin un valor por
  defecto, el `dotnet build` del solution completo (que incluye los
  proyectos de test) habría fallado en Release con
  `TreatWarningsAsErrors`/errores de compilación por argumento obligatorio
  ausente. Esta fase tiene explícitamente prohibido crear, modificar o
  ejecutar ficheros de test — la única forma de mantener el build verde sin
  tocarlos es dar a `Theme` un valor por defecto, que además coincide
  exactamente con `AppConfig.Default.Theme` (`Dark`), por lo que no cambia
  ningún comportamiento en tiempo de ejecución. `sdlc-testing` puede,
  opcionalmente, actualizar esas dos líneas para pasar `Theme` de forma
  explícita si lo considera más claro; el valor por defecto seguirá siendo
  válido de todas formas.
- **Pasos 3, 5, 9 y 10 del Implementation Plan (tests de `AppConfigStore`,
  `CountdownFormatter`, `UsagePage` y `UsageBar`) no se han ejecutado en
  esta fase.** Es responsabilidad exclusiva de `sdlc-testing` (fuera del
  alcance de `sdlc-development` por restricción explícita de esta fase).
  El resto del plan (pasos 1, 2, 4, 6, 7, 8) se implementó tal cual el
  documento de diseño, sin más desviaciones.

## How to Verify

1. `dotnet build ClaudeMeter.sln -c Debug` y `-c Release` — ambos
   compilan sin advertencias ni errores (ya verificado en esta sesión).
2. **Validación manual (US-1, pendiente — ningún agente de este pipeline
   puede renderizar WebView2 real):** ejecutar la app con
   `%LOCALAPPDATA%\ClaudeMeter\config.json` conteniendo
   `"theme": "light"` y con el campo ausente/`"dark"`, y confirmar
   visualmente ambas paletas sobre `UsagePage`/`ReauthNotice`.
3. **Validación manual (US-2, pendiente):** dejar la app corriendo varios
   ciclos de poll (60s por defecto) y confirmar que el texto de countdown
   bajo cada barra hace un fundido suave al cambiar, sin impacto visible de
   CPU/GPU.
4. Revisar que un `config.json` de un ciclo anterior sin `"theme"` sigue
   cargando con normalidad (tema oscuro, sin regresión visual).

## Follow-ups / Known Limitations

- `sdlc-testing` debe cubrir los pasos 3, 5, 9 y 10 del Implementation Plan
  (incluido el caso de `theme` con tipo JSON incorrecto, ya señalado en el
  documento de diseño como cubierto por el test existente de JSON
  sintácticamente inválido) para alcanzar el `testingCoverage: 70`
  configurado.
- Contraste WCAG de la paleta clara nueva: objetivo aproximado ya razonado
  en el documento de diseño (Risks & Open Decisions); pendiente de
  confirmación visual manual, no medible por este pipeline.
