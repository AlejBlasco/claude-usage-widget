# Design: F3 — UX + página Mascota, Ciclo A

## Requirements Reference

`docs/sdlc/requirements/f3-ux-mascota-ciclo-a.md` — cubre US-1 (issue #14,
tema claro/oscuro) y US-2 (issue #15, animación del countdown), dos user
stories independientes entre sí (sin fichero ni superficie de UI compartida)
tratadas en un único documento de requisitos y, por proporcionalidad, en un
único ciclo de diseño. Documento cerrado, sin "Open question" pendientes.

Quedan fuera de alcance de este documento: #16 (click-through), #17 (icono
de bandeja), #18 (`ScreenNavigator`/`IWidgetScreen`) y #19 (`MascotPage.razor`) —
Ciclos B y C posteriores.

## Architecture Overview

Este ciclo no toca Domain ni Application en absoluto (confirmado por
inspección: `RateLimitWindow.MinutesRemaining` ya existe y no requiere
ningún cambio; el porcentaje/umbral tampoco cambia). Todo el trabajo vive en
Desktop, extendiendo dos patrones ya establecidos en ciclos anteriores en
vez de introducir mecanismos nuevos:

- **US-1 (tema):** extiende el patrón `AppConfig`/`AppConfigStore` ya
  mergeado en F2/Ciclo B (DTO anulable, resolución con fallback + `Warning`
  de log, escritura atómica) con un campo `Theme` nuevo, resuelto una única
  vez en el arranque (`App.xaml.cs`, sin cambios) — igual que
  `PollingInterval`/`ChimeEnabled` hoy, sin recarga en caliente en este
  ciclo (confirmado como límite de alcance por el propio documento de
  requisitos). El tema se materializa como **custom properties CSS**
  redefinidas por una clase `theme-dark`/`theme-light` en un nuevo
  contenedor raíz `.claudemeter-root`, renderizado por `UsagePage.razor`
  (hoy el único componente raíz del `BlazorWebView`) envolviendo tanto las
  dos `UsageBar` como `ReauthNotice` — las dos superficies de UI que existen
  hoy, cubriendo el AC de "ambas páginas reflejan el tema de forma
  consistente" con un único punto de aplicación.
- **US-2 (countdown):** añade el texto de `MinutesRemaining` (hoy calculado
  en Domain pero nunca renderizado) a `UsageBar.razor`, y lo anima con una
  animación CSS pura (`@keyframes`) disparada por `@key` — sin JavaScript,
  sin librería de animación, coherente con el patrón ya usado por
  `.usage-bar__fill { transition: width ... }` en F1 y con el AC explícito
  de la issue #15 de no impactar CPU/GPU.

Piezas nuevas/modificadas, de fuera hacia dentro:

1. **`ClaudeMeter.Desktop.Configuration.AppTheme` / `AppThemeExtensions`**
   (Desktop, nuevo, en `AppConfig.cs`) — enum de dos valores (`Dark`,
   `Light`) más una extensión `ToCssClass()` que es la única fuente de
   verdad del mapeo enum→clase CSS, para que la futura `MascotPage`
   (Ciclo C) la reutilice sin duplicar el mapeo (riesgo señalado
   explícitamente por el documento de requisitos).
2. **`ClaudeMeter.Desktop.Configuration.AppConfig`** (Desktop, modificado) —
   nuevo campo `Theme` (`AppTheme`, por defecto `Dark` — paleta actual sin
   cambios).
3. **`ClaudeMeter.Desktop.Configuration.AppConfigStore`** (Desktop,
   modificado) — `Load()` resuelve `theme` igual que los demás campos
   (ausente → por defecto en silencio; `"dark"`/`"light"`
   case-insensitive → valor correspondiente; cualquier otro valor/tipo →
   por defecto + `Warning`); `Save()` serializa `Theme` de vuelta a
   `"dark"`/`"light"`.
4. **`ClaudeMeter.Desktop.Rendering.CountdownFormatter`** (Desktop, nuevo) —
   función pura `int → string` que formatea `MinutesRemaining` de forma
   compacta (`"4h 12m"`, `"7d 3h"`, `"0m"`), análoga en intención (aunque no
   en ubicación) a `ClaudeMeter.ConsoleApp.Rendering.UsagePollingLineFormatter`
   de F0 — ver Technology Choices sobre por qué el formato difiere del
   `"N min"` crudo de la consola.
5. **`Pages/UsagePage.razor`** (Desktop, modificado) — envuelve su contenido
   existente (`ReauthNotice` / `.usage-widget`) en un `<div class="claudemeter-root @Config.Theme.ToCssClass()">`
   nuevo. Único cambio de este fichero en este ciclo; el resto de su lógica
   (polling, chime, arrastre) no se toca.
6. **`Pages/UsageBar.razor`** (Desktop, modificado) — añade un `<span>` de
   countdown, formateado con `CountdownFormatter`, con `@key="minutes"` para
   forzar el remount que dispara la animación CSS en cada cambio de valor.
7. **`wwwroot/css/app.css`** (Desktop, modificado en profundidad) — introduce
   custom properties por tema (`--bg-color`, `--text-color`, `--text-muted`,
   `--value-color`, `--unavailable-color`, `--track-bg`, `--color-normal`,
   `--color-warning`, `--color-critical`, `--color-neutral`), dos bloques
   `.claudemeter-root.theme-dark` / `.claudemeter-root.theme-light` que las
   definen, y el `@keyframes usage-countdown-fade` de US-2. `html, body`
   mantienen su fondo oscuro hardcodeado actual como fallback pre-render
   (ver Technology Choices).

Diagrama de dependencias resultante (piezas nuevas/modificadas resaltadas):

```
Desktop
 ├── Configuration/
 │    ├── AppConfig.cs (modificado) ──> + AppTheme, AppThemeExtensions, AppConfig.Theme
 │    └── AppConfigStore.cs (modificado) ──> resuelve/serializa "theme"
 ├── Rendering/
 │    └── CountdownFormatter.cs (nuevo, función pura)
 ├── Pages/
 │    ├── UsagePage.razor (modificado) ──> envuelve en .claudemeter-root + clase de tema
 │    ├── UsageBar.razor (modificado) ──> countdown + @key + CountdownFormatter
 │    └── ReauthNotice.razor (sin cambios de código — hereda tema vía CSS)
 └── wwwroot/css/app.css (modificado) ──> custom properties, 2 temas, @keyframes

Domain / Application / Infrastructure: sin cambios.
```

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| **Tema vía custom properties CSS (`--variable`) redefinidas por `.theme-dark`/`.theme-light` en un contenedor `.claudemeter-root` renderizado por `UsagePage`, resuelto una vez en el arranque desde `AppConfig.Theme`** | Es la sugerencia del propio documento de requisitos y la que mejor encaja con el CSS escrito a mano ya existente (sin frameworks de terceros, decisión ya fijada en F1): cero JS, cero interop, cero coste de render — el navegador resuelve `var(--x)` de forma nativa. Al vivir las variables en un contenedor con nombre de clase estable (`.claudemeter-root`) en vez de en `UsagePage.razor` directamente, la futura `MascotPage` (Ciclo C) puede envolver su propio contenido con la misma clase y heredar el tema sin duplicar ninguna regla de color — mitiga directamente el riesgo "Alcance/consistencia futura" del documento de requisitos. | (1) Clases utilitarias por color (`.text-dark`/`.text-light` en cada elemento) — rechazada: obligaría a anotar cada elemento individualmente con la variante activa, exactamente la duplicación que el requisito pide evitar de cara a `MascotPage`. (2) Hoja de estilos separada por tema (`app.dark.css`/`app.light.css`) cargada condicionalmente — rechazada: `index.html` es estático (servido por `BlazorWebView` antes de que `AppConfig` exista en tiempo de ejecución), así que decidir qué hoja cargar exigiría JS interop o reescribir `index.html` por arranque; las custom properties resuelven lo mismo con una única hoja siempre cargada. |
| **El tema se aplica en `UsagePage.razor` (el único componente raíz hoy), no en `wwwroot/index.html`** | `AppConfig` es un servicio de Blazor (inyectado vía `@inject`), no existe en tiempo de renderizado del HTML estático — `index.html` se sirve una vez, antes de que `BlazorWebView`/`App.xaml.cs` hayan resuelto ningún `AppConfig`. Envolver el contenido de `UsagePage` es la única forma de que la clase de tema dependa de un valor leído en `OnStartup`. | Aplicar la clase de tema al `<body>` vía JS interop en `OnAfterRenderAsync` (mismo patrón que `claudeMeterDrag.init` de F2/Ciclo B) — rechazada: añade una dependencia de JS/interop a un requisito que las propias Technical Notes del documento de requisitos prefieren resolver sin JS si es posible («CSS pura... preferible a una librería JS»); el enfoque de contenedor Razor logra el mismo resultado visual (el contenedor cubre el 100% del área visible, igual que hoy hace `body`) sin ningún riesgo de que `InvokeVoidAsync` falle o se ejecute tarde. |
| **`html, body` conservan su fondo oscuro hardcodeado actual (`#20232aee`/`#e8e8e8`) como *fallback* pre-render, en vez de heredar de las custom properties** | Es exactamente el mismo fondo que ve el usuario durante el "Cargando…" inicial de `index.html`, antes de que `UsagePage` monte y aplique `.claudemeter-root.theme-*`. Mantenerlo hardcodeado (no derivado de `AppConfig.Theme`, que no existe en ese momento) garantiza que el primer frame sea siempre oscuro, consistente con el AC de US-1 "sin `config.json`, tema oscuro, sin regresión" incluso antes de que Blazor termine de montar — un caso límite que ninguna AC pide cubrir explícitamente pero que evita un parpadeo blanco/inconsistente. | Fondo transparente/`inherit` en `html, body` — rechazada: en Chromium embebido sin la API nativa de fondo transparente (fuera de alcance desde F1, ver `docs/sdlc/design/f1-widget-visual-base.md`), un `html, body` sin fondo explícito se resuelve en blanco por defecto durante el "Cargando…", peor experiencia que el fondo oscuro intencional ya aceptado en F1. |
| **Paleta clara con colores de umbral propios (más saturados/oscuros que los del tema oscuro), no una copia directa de los mismos hex** | AC explícito de US-1: "no una simple reutilización sin ajuste de los colores oscuros". Los verdes/ámbares/rojos brillantes pensados para un fondo `#20232aee` (oscuro) se verían pálidos y de bajo contraste sobre un fondo claro (`#f4f5f7ee`); se eligen variantes más saturadas/oscuras (`#1e8a3c`/`#a3660a`/`#c4291f`) con el objetivo aproximado de WCAG AA para gráficos no textuales (≥3:1 frente al fondo) y AA para el texto que sí las usa como color (`.usage-bar__value`, `.usage-reauth__message` usan `--text-color`/`--value-color`, no los colores de umbral directamente). La confirmación visual exacta queda en la validación manual ya prevista por la Definition of Done heredada (ningún agente de este pipeline puede renderizar WebView2 real para medir contraste real en pantalla). | Reutilizar los mismos hex del tema oscuro para el tema claro — rechazada explícitamente por el AC de US-1 citado arriba. |
| **`CountdownFormatter` en `ClaudeMeter.Desktop.Rendering` (Desktop), formato compacto `"{d}d {h}h"` / `"{h}h {m}m"` / `"{m}m"` según magnitud, en vez del `"N min"` crudo que ya usa `UsagePollingLineFormatter` en la consola (F0)** | El widget es una superficie de 280×140px (tamaño fijado en F1); `MinutesRemaining` de la ventana semanal puede llegar a ~10080 (7 días), y `"10080 min"` desbordaría o forzaría un texto minúsculo en un espacio ya ajustado con el porcentaje y la barra. Un formato tipo "7d 3h" (o "4h 12m"/"35m" para la ventana de sesión, ≤5h) es más corto y legible de un vistazo — coherente con que `UsageBar.razor` ya redondea el porcentaje a un entero (`pct.ToString("0")`) por el mismo motivo de legibilidad a escala de widget. La consola (F0) es una herramienta de inspección/depuración donde la precisión en minutos importa más que la compacidad visual — no hay AC ni precedente en `CLAUDE.md` que exija que ambas superficies compartan formato exacto de texto. Vive en Desktop (no Domain) por el mismo motivo que el mapeo severidad→color de `UsageBar` vive en Desktop: es una decisión de presentación, no una regla de negocio. | (1) Reutilizar literalmente `"{N} min"` como en consola — rechazada por el riesgo de desbordamiento/legibilidad explicado arriba, agravado en la ventana semanal. (2) Formato `HH:mm:ss` tipo temporizador — rechazada: sugiere una cuenta atrás con precisión de segundos que `MinutesRemaining` (ya redondeado a minutos en Domain, ver `RateLimitWindowParser`) no tiene; mostrar segundos falsos (siempre `:00` o inventados) sería engañoso. |
| **Animación del countdown: CSS `@keyframes` puro disparado forzando el remount del `<span>` vía `@key="minutes"`, sin JavaScript ni librería** | Confirmado contra la documentación oficial de Blazor (Microsoft Learn, "Retain element, component, and model relationships in ASP.NET Core Blazor", verificado por fetch directo a fecha de este diseño): *"If a `person` instance changes, the `@key` attribute directive forces Blazor to: Discard the entire `<li>` or `<div>` and their descendants. Rebuild the subtree within the UI with new elements and components."* — el mismo comportamiento aplica a un único elemento fuera de un bucle (el propio artículo lo ilustra con un `<div @key="person">` suelto, no solo en `@foreach`). Al usar `int MinutesRemaining` como clave, cada ciclo de poll que cambia el valor fuerza a Blazor a destruir el `<span>` anterior e insertar uno nuevo, lo que dispara de forma nativa la animación `@keyframes` aplicada a ese elemento (comportamiento estándar de CSS al insertar un nodo con esa propiedad — sin necesidad de JS que detecte el cambio). Las propiedades animadas (`opacity`, `transform: translateY`) las compone el navegador sin relayout, igual coste que la `transition: width` ya usada en `.usage-bar__fill` desde F1 — satisface directamente el AC explícito de la issue #15 sobre no introducir impacto perceptible de CPU/GPU, y la propia documentación de Microsoft advierte que `@key` tiene "a performance cost [que] isn't large" para un único elemento pequeño (no una lista). | (1) Interop JS + librería de animación (p. ej. GSAP) para un efecto "odómetro" dígito a dígito — rechazada: la propia issue #15 marca el coste de CPU/GPU como AC explícito, y el documento de requisitos ya señala que una transición CSS pura es preferible por coste frente a una librería no evaluada; un odómetro real además exigiría trocear el texto en dígitos individuales, complejidad no pedida por ningún AC ("p. ej. fundido/deslizamiento" ya es suficiente). (2) `transition` en vez de `@keyframes` sobre una propiedad que cambia in-place — rechazada: `transition` anima un cambio de valor de una propiedad CSS en el **mismo** nodo DOM (como ya hace `.usage-bar__fill` con `width`), pero aquí el contenido es texto discreto (`"4h 12m"` → `"4h 11m"`), no interpolable; sin forzar un remount (vía `@key`) Blazor solo parchea el nodo de texto in-place y ninguna `transition` se dispararía. |
| **Contenido del countdown en `.usage-bar` (mismo componente/fichero `UsageBar.razor`), no un componente nuevo `Countdown.razor`** | El countdown es un dato más de la misma `RateLimitWindow` que ya renderiza `UsageBar` (porcentaje + countdown proceden del mismo `MinutesRemaining`/`PercentageUsed` calculados juntos por `RateLimitWindowParser`); separarlo en un componente aparte añadiría un límite de componente y un `[Parameter]` extra sin ningún caso de reutilización real hoy (ninguna otra página necesita el countdown de forma aislada sin el resto de la barra). | Componente `Countdown.razor` reutilizable independiente — rechazada por prematura, mismo criterio que F1 ya aplicó para no separar `ThresholdClassifier` de Desktop en su momento: se reconsiderará si `MascotPage` (Ciclo C) necesita el countdown desacoplado de la barra de progreso. |

## Data Model / Interfaces

### `AppConfig.cs` (Desktop, modificado)

```csharp
// src/ClaudeMeter.Desktop/Configuration/AppConfig.cs
namespace ClaudeMeter.Desktop.Configuration;

public sealed record WindowPosition(double Left, double Top);

/// <summary>
/// Tema visual del widget (F3/Ciclo A, US-1). El nombre de cada miembro es,
/// por convención, la única fuente de verdad de dos representaciones
/// derivadas: la clase CSS (<see cref="AppThemeExtensions.ToCssClass"/>,
/// "theme-{nombre en minúsculas}") y el valor de texto de "theme" en
/// <c>config.json</c> (ver <see cref="AppConfigStore"/>, también el nombre
/// en minúsculas) — evita que ambas representaciones diverjan si se añade
/// un tercer tema en el futuro.
/// </summary>
public enum AppTheme
{
    Dark,
    Light
}

/// <summary>
/// Traduce <see cref="AppTheme"/> a la clase CSS que consume
/// <c>app.css</c>. Única fuente de verdad del mapeo para que cualquier
/// página Razor futura (p. ej. <c>MascotPage</c>, Ciclo C) no lo duplique
/// (riesgo "Alcance/consistencia futura" del documento de requisitos).
/// </summary>
public static class AppThemeExtensions
{
    public static string ToCssClass(this AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => "theme-light",
            _ => "theme-dark",
        };
}

/// <param name="PollingInterval">Sin cambios (F2/Ciclo B).</param>
/// <param name="Position">Sin cambios (F2/Ciclo B).</param>
/// <param name="ChimeEnabled">Sin cambios (F2/Ciclo B).</param>
/// <param name="Theme">
/// Por defecto <see cref="AppTheme.Dark"/> — paleta actual sin cambios
/// (AC de US-1: sin <c>config.json</c>/campo, tema oscuro, sin regresión).
/// </param>
public sealed record AppConfig(TimeSpan PollingInterval, WindowPosition? Position, bool ChimeEnabled, AppTheme Theme)
{
    public static AppConfig Default { get; } =
        new(TimeSpan.FromSeconds(60), Position: null, ChimeEnabled: false, Theme: AppTheme.Dark);
}
```

### `AppConfigStore.cs` (Desktop, modificado — excerpt)

```csharp
// src/ClaudeMeter.Desktop/Configuration/AppConfigStore.cs

public AppConfig Load(string? configPath = null)
{
    // ... sin cambios hasta la deserialización del dto ...

    return new AppConfig(
        PollingInterval: ResolveInterval(dto.PollingIntervalSeconds),
        Position: ResolvePosition(dto.WindowPosition),
        ChimeEnabled: dto.ChimeEnabled ?? AppConfig.Default.ChimeEnabled,
        Theme: ResolveTheme(dto.Theme)); // F3/Ciclo A
}

public void Save(AppConfig config, string? configPath = null)
{
    // ...
    var dto = new AppConfigDto
    {
        PollingIntervalSeconds = config.PollingInterval.TotalSeconds,
        ChimeEnabled = config.ChimeEnabled,
        Theme = config.Theme.ToString().ToLowerInvariant(), // F3/Ciclo A: "dark"/"light"
        WindowPosition = config.Position is { } p ? new WindowPositionDto { Left = p.Left, Top = p.Top } : null,
    };
    // ... resto sin cambios ...
}

/// <summary>
/// Resuelve el campo "theme" (F3/Ciclo A, US-1): ausente -> valor por
/// defecto en silencio (mismo criterio que el resto de campos); presente
/// pero no reconocido (tipo incorrecto ya descartado por
/// <see cref="JsonSerializer"/>, que dejaría <c>Theme</c> a <c>null</c> si
/// no es una cadena JSON) o con un valor distinto de "dark"/"light" ->
/// valor por defecto + <c>Warning</c> (AC de US-1).
/// </summary>
private AppTheme ResolveTheme(string? theme)
{
    if (theme is null)
    {
        return AppConfig.Default.Theme;
    }

    if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
    {
        return AppTheme.Dark;
    }

    if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
    {
        return AppTheme.Light;
    }

    _logger.LogWarning("theme inválido en config.json ({Value}); se usa el valor por defecto (dark)", theme);
    return AppConfig.Default.Theme;
}

// AppConfigDto (privado, existente): añadir
[JsonPropertyName("theme")]
public string? Theme { get; set; }
```

Nota sobre "tipo incorrecto" (AC de US-1, p. ej. un número en vez de una
cadena): `System.Text.Json` con `AppConfigDto.Theme` tipado como
`string?` lanzará `JsonException` al encontrar un valor JSON no-cadena
(p. ej. `"theme": 5`) — ese caso ya cae en el bloque `catch (JsonException ...)`
de `Load()` existente (fichero completo a valores por defecto, con su
propio `Warning` ya implementado), **no** en `ResolveTheme`. Esto es un
comportamiento ligeramente más amplio que los demás campos individuales
(un `theme` de tipo incorrecto invalida los tres campos, no solo el tema)
pero es el mismo comportamiento que ya tiene hoy cualquier campo con tipo
JSON incorrecto en este fichero (p. ej. `"pollingIntervalSeconds": "abc"`)
— no es una regresión introducida por este ciclo, y ya está cubierto por
un test existente (`Load_ConJsonSintacticamenteInvalido_...`) que
Development debe extender con un caso de tipo incorrecto específico para
`theme` si quiere verificarlo de forma explícita.

### `CountdownFormatter.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Rendering/CountdownFormatter.cs
namespace ClaudeMeter.Desktop.Rendering;

/// <summary>
/// Formatea <see cref="ClaudeMeter.Domain.Usage.RateLimitWindow.MinutesRemaining"/>
/// de forma compacta para un widget de 280x140px (F3/Ciclo A, US-2). Función
/// pura, sin dependencias externas ni acceso al reloj — mismo estándar que
/// <see cref="ClaudeMeter.Domain.Usage.RateLimitWindowParser"/>. Recibe un
/// <see cref="int"/> no anulable a propósito: el llamador
/// (<c>UsageBar.razor</c>) solo la invoca tras desenvolver un
/// <c>int? MinutesRemaining</c> no nulo vía pattern matching — "sin datos"
/// se resuelve en el propio Razor sin llamar a esta función (AC de US-2).
/// </summary>
public static class CountdownFormatter
{
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = 24 * MinutesPerHour;

    /// <summary>
    /// &gt;=1 día -> "{d}d {h}h"; &gt;=1 hora -> "{h}h {m}m"; en caso
    /// contrario -> "{m}m" (incluye el caso 0 -> "0m", AC explícita de
    /// US-2 sobre el estado de cero).
    /// </summary>
    public static string Format(int minutesRemaining)
    {
        if (minutesRemaining <= 0)
        {
            return "0m";
        }

        var days = minutesRemaining / MinutesPerDay;
        var hours = minutesRemaining % MinutesPerDay / MinutesPerHour;
        var minutes = minutesRemaining % MinutesPerHour;

        if (days > 0)
        {
            return $"{days}d {hours}h";
        }

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{minutes}m";
    }
}
```

### `UsageBar.razor` (Desktop, modificado)

```razor
@* src/ClaudeMeter.Desktop/Pages/UsageBar.razor *@
@using ClaudeMeter.Desktop.Rendering

<div class="usage-bar usage-bar--@ColorClass">
    <span class="usage-bar__title">@Title</span>
    @if (Window.PercentageUsed is { } pct)
    {
        <div class="usage-bar__track">
            <div class="usage-bar__fill" style="width:@(pct.ToString("0"))%"></div>
        </div>
        <span class="usage-bar__value">
            @pct.ToString("0")%@(Stale ? " (desactualizado)" : "")
        </span>
    }
    else
    {
        <span class="usage-bar__value usage-bar__value--unavailable">No disponible</span>
    }
    @if (Window.MinutesRemaining is { } minutes)
    {
        @* F3/Ciclo A (US-2): @key="minutes" fuerza a Blazor a destruir y
           recrear este <span> cada vez que MinutesRemaining cambia entre
           renders (en vez de parchear el texto in-place), lo que dispara
           la animación @keyframes de app.css en cada ciclo con un valor
           distinto -- ver Technology Choices para el rationale verificado
           contra la documentación oficial de Blazor. Cuando
           MinutesRemaining es null (sin datos), este bloque no se
           renderiza en absoluto: no se intenta animar ningún valor
           ausente (AC de US-2). *@
        <span class="usage-bar__countdown" @key="minutes">
            Restablece en @CountdownFormatter.Format(minutes)
        </span>
    }
</div>

@code {
    // Sin cambios: Title, Window, Stale, ColorClass tal cual F1/F2.
}
```

### `UsagePage.razor` (Desktop, modificado — excerpt)

```razor
@* src/ClaudeMeter.Desktop/Pages/UsagePage.razor *@
@* ... @implements/@inject sin cambios ... *@

@* F3/Ciclo A (US-1): .claudemeter-root envuelve TODO el contenido que
   UsagePage puede renderizar (ReauthNotice y .usage-widget), aplicando la
   clase de tema una única vez -- cubre el AC "ambas superficies (UsagePage/
   UsageBar y ReauthNotice) reflejan el tema de forma consistente" con un
   solo punto de aplicación. Config.Theme se resuelve una única vez en el
   arranque (App.xaml.cs, sin cambios), consistente con el resto de
   AppConfig hoy -- sin recarga en caliente en este ciclo. *@
<div class="claudemeter-root @Config.Theme.ToCssClass()">
    @if (_status == UsageSnapshotStatus.Unauthorized)
    {
        <ReauthNotice />
    }
    else
    {
        <div class="usage-widget">
            <UsageBar Title="Sesión" Window="@_session" Stale="@_isStale" />
            <UsageBar Title="Semana" Window="@_weekly" Stale="@_isStale" />
        </div>
    }
</div>

@code {
    // Sin cambios: todo el resto (_coordinator, Apply, OnSnapshotReceived,
    // Dispose, chime, arrastre) permanece tal cual F2/Ciclo B.
}
```

### `wwwroot/css/app.css` (Desktop, reescrito)

```css
/* Estilos escritos a mano -- deliberadamente sin Bootstrap ni ningún otro
   CSS de terceros (decisión fijada en F1). Desde F3/Ciclo A: custom
   properties por tema (US-1) + animación del countdown (US-2). */

html, body {
    margin: 0;
    padding: 0;
    height: 100%;
    font-family: "Segoe UI", system-ui, sans-serif;
    overflow: hidden;
    /* Fondo/color de respaldo mostrado antes de que Blazor monte UsagePage
       (el "Cargando..." de index.html): SIEMPRE la paleta oscura actual,
       nunca derivado de AppConfig.Theme (que no existe todavía en ese
       momento) -- ver Technology Choices. */
    color: #e8e8e8;
    background-color: #20232aee;
}

#app {
    height: 100%;
}

/* F3/Ciclo A (US-1): valores IDÉNTICOS a los hardcodeados hasta este ciclo
   -- preserva el aspecto visual sin ninguna regresión (AC de US-1). */
.claudemeter-root.theme-dark {
    --bg-color: #20232aee;
    --text-color: #e8e8e8;
    --text-muted: #9aa0ab;
    --value-color: #cfd3da;
    --unavailable-color: #7d8290;
    --track-bg: #3a3f4b;
    --color-normal: #3fb950;
    --color-warning: #d29922;
    --color-critical: #f85149;
    --color-neutral: #5a6070;
}

/* Paleta clara nueva (US-1): colores de umbral propios y más saturados que
   los del tema oscuro -- ver Technology Choices sobre contraste. */
.claudemeter-root.theme-light {
    --bg-color: #f4f5f7ee;
    --text-color: #20232a;
    --text-muted: #5b6270;
    --value-color: #3a3f4b;
    --unavailable-color: #8b92a1;
    --track-bg: #d8dbe2;
    --color-normal: #1e8a3c;
    --color-warning: #a3660a;
    --color-critical: #c4291f;
    --color-neutral: #7a8090;
}

.claudemeter-root {
    box-sizing: border-box;
    height: 100%;
    color: var(--text-color);
    background-color: var(--bg-color);
}

.usage-widget {
    box-sizing: border-box;
    height: 100%;
    padding: 12px 16px;
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 14px;
}

.usage-bar {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.usage-bar__title {
    font-size: 12px;
    font-weight: 600;
    letter-spacing: 0.02em;
    text-transform: uppercase;
    color: var(--text-muted);
}

.usage-bar__track {
    height: 10px;
    border-radius: 5px;
    background-color: var(--track-bg);
    overflow: hidden;
}

.usage-bar__fill {
    height: 100%;
    border-radius: 5px;
    transition: width 0.3s ease-in-out;
}

.usage-bar__value {
    font-size: 12px;
    color: var(--value-color);
}

.usage-bar__value--unavailable {
    color: var(--unavailable-color);
    font-style: italic;
}

/* F3/Ciclo A (US-2): countdown de MinutesRemaining. El <span> se recrea
   (no se parchea) cada vez que su @key cambia entre renders -- ver
   Technology Choices -- lo que dispara esta animación en cada ciclo con un
   valor distinto, incluida la transición final a "0m" (AC de US-2).
   opacity/transform: el navegador las compone sin relayout (AC explícito
   de no impactar CPU/GPU). */
.usage-bar__countdown {
    display: inline-block;
    font-size: 12px;
    color: var(--text-muted);
    animation: usage-countdown-fade 0.3s ease-in-out;
}

@keyframes usage-countdown-fade {
    from {
        opacity: 0;
        transform: translateY(-3px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.usage-bar--green .usage-bar__fill {
    background-color: var(--color-normal);
}

.usage-bar--amber .usage-bar__fill {
    background-color: var(--color-warning);
}

.usage-bar--red .usage-bar__fill {
    background-color: var(--color-critical);
}

.usage-bar--neutral .usage-bar__fill {
    background-color: var(--color-neutral);
}

.usage-reauth {
    height: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 8px;
    text-align: center;
    padding: 12px 16px;
    box-sizing: border-box;
}

.usage-reauth__icon {
    font-size: 22px;
    color: var(--color-critical);
}

.usage-reauth__message {
    font-size: 13px;
    color: var(--text-color);
    line-height: 1.4;
}

/* #blazor-error-ui queda fuera de alcance del tema (US-1): es un banner de
   error del framework, no una de las "páginas" (UsagePage/ReauthNotice) que
   el AC pide cubrir -- se mantiene con su paleta fija actual. */
#blazor-error-ui {
    display: none;
    position: fixed;
    inset: auto 0 0 0;
    background: #f85149;
    color: white;
    padding: 8px 16px;
    font-size: 12px;
}

#blazor-error-ui .dismiss {
    cursor: pointer;
    float: right;
}
```

### Esquema de `config.json` (campo nuevo)

```json
{
  "pollingIntervalSeconds": 60,
  "chimeEnabled": false,
  "theme": "light",
  "windowPosition": { "left": 1200.0, "top": 800.0 }
}
```

- `theme` es opcional, independiente del resto de campos (mismo criterio
  que `chimeEnabled`/`pollingIntervalSeconds`): ausente → `"dark"` sin log;
  `"dark"`/`"light"` (case-insensitive) → tema correspondiente; cualquier
  otro valor de cadena → `"dark"` + `Warning`; tipo JSON no-cadena → todo
  el fichero cae a valores por defecto (ver nota bajo `AppConfigStore.cs`
  arriba, comportamiento heredado de cómo `Load()` ya trata cualquier
  campo con tipo incorrecto).

## Implementation Plan

1. **`Configuration/AppConfig.cs` (US-1):** añadir `AppTheme` (enum),
   `AppThemeExtensions.ToCssClass()` y el campo `Theme` a `AppConfig`
   (incluido en `AppConfig.Default`), tal cual Data Model.
2. **`Configuration/AppConfigStore.cs` (US-1):** añadir `AppConfigDto.Theme`,
   `ResolveTheme(string?)`, y las dos líneas de `Load()`/`Save()` tal cual
   Data Model.
3. **Tests de `AppConfigStore` (US-1, xUnit puro, extendiendo
   `test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs`
   ya existente):** `theme` ausente → `AppTheme.Dark` sin necesidad de
   inspeccionar logs; `"light"`/`"LIGHT"`/`"Light"` → `AppTheme.Light`
   (case-insensitive); `"dark"` → `AppTheme.Dark`; valor no reconocido
   (p. ej. `"blue"`, cadena vacía) → `AppTheme.Dark` (fallback, sin
   lanzar — el propio test de JSON sintácticamente inválido ya existente
   cubre el caso de tipo incorrecto, ver nota de Data Model); `Save()`
   seguido de `Load()` hace round-trip del tema igual que ya se prueba
   para los otros 2 campos.
4. **`Rendering/CountdownFormatter.cs` (US-2):** crear en
   `src/ClaudeMeter.Desktop/Rendering/` tal cual Data Model.
5. **Tests de `CountdownFormatter` (US-2, xUnit puro,
   `test/ClaudeMeter.Desktop.Tests/Rendering/CountdownFormatterTests.cs`
   nuevo):** casos por magnitud y fronteras exactas — `0` → `"0m"`;
   `1`/`59` → `"1m"`/`"59m"`; `60` → `"1h 0m"`; `61`/`301` (sesión, algo
   por encima de 5h) → `"1h 1m"`/`"5h 1m"`; `1439` → `"23h 59m"`; `1440`
   → `"1d 0h"`; `10080` (7 días exactos, tope de la ventana semanal) →
   `"7d 0h"`.
6. **`wwwroot/css/app.css` (US-1/US-2):** reescribir tal cual Data Model.
7. **`Pages/UsagePage.razor` (US-1):** envolver el `@if/else` existente en
   el `<div class="claudemeter-root @Config.Theme.ToCssClass()">` tal cual
   Data Model. Ningún otro cambio en este fichero.
8. **`Pages/UsageBar.razor` (US-2):** añadir el bloque de countdown
   (`@using ClaudeMeter.Desktop.Rendering`, `@if (Window.MinutesRemaining is { } minutes)`,
   `@key="minutes"`) tal cual Data Model.
9. **Tests bUnit de `UsagePage` (US-1, extendiendo
   `UsagePageTests.cs`):** con `RegisterCoreServices(fake, config: AppConfig.Default with { Theme = AppTheme.Light })`,
   tras un snapshot exitoso, `cut.Find("div.claudemeter-root")` contiene
   la clase `theme-light`; con `AppConfig.Default` (sin especificar tema)
   contiene `theme-dark`; con un snapshot `Unauthorized` (`ReauthNotice`
   visible), el `div.claudemeter-root` sigue presente y con la clase de
   tema correspondiente (verifica que `ReauthNotice` también queda dentro
   del contenedor temático, AC de "ambas páginas consistentes").
10. **Tests bUnit de `UsageBar` (US-2, extendiendo `UsageBarTests.cs`):**
    `MinutesRemaining` no nulo (p. ej. `252`) → `cut.Find(".usage-bar__countdown")`
    contiene el texto formateado (`"Restablece en 4h 12m"`); `MinutesRemaining`
    `0` → `"Restablece en 0m"` (AC explícita del estado de cero);
    `RateLimitWindow.Unavailable` (`MinutesRemaining == null`) →
    `cut.FindAll(".usage-bar__countdown")` vacío (no se intenta renderizar
    ni animar nada, AC de US-2).
11. **Validación de compilación:** `dotnet build ClaudeMeter.sln` en Debug
    y Release (analizadores + `TreatWarningsAsErrors` en Release, ver
    `Directory.Build.props`), igual que en ciclos anteriores.

## Cross-Cutting Concerns

- **Seguridad:** ninguna superficie nueva — `theme` es un valor de
  presentación puro, sin ninguna interacción con el token/credenciales.
- **Rendimiento:** la animación del countdown usa únicamente `opacity`/
  `transform` (compuestas por el navegador sin relayout) y se dispara como
  mucho una vez por ciclo de poll (60s por defecto) sobre un único `<span>`
  pequeño — coste despreciable, coherente con el AC explícito de la issue
  #15. El coste adicional de usar `@key` está documentado por Microsoft
  como no significativo para un elemento no repetido en una lista (ver
  Technology Choices). Las custom properties CSS no tienen coste de
  renderizado distinto de literales hex — se resuelven en tiempo de estilo,
  no de JS.
- **Manejo de errores:** `AppConfigStore.Load()` sigue sin lanzar nunca; un
  `theme` inválido cae al valor por defecto con un `Warning`, igual que el
  resto de campos — ningún camino nuevo de excepción.
- **Observabilidad/logging:** una entrada `Warning` nueva
  (`"theme inválido en config.json..."`), mismo nivel/formato que las ya
  existentes para `pollingIntervalSeconds`/`windowPosition`.
- **Compatibilidad hacia atrás:** un `config.json` de F2/Ciclo B sin el
  campo `theme` sigue cargando con normalidad (`Theme` cae a `Dark` en
  silencio); el tema oscuro resultante es visualmente idéntico al actual
  (mismos valores hex) — sin regresión (AC de US-1). `MascotPage` (Ciclo C)
  puede envolver su propio contenido en `.claudemeter-root @Config.Theme.ToCssClass()`
  sin ningún cambio a `app.css` ni a `AppConfig`/`AppConfigStore`.

## Risks & Open Decisions

- **Contraste/accesibilidad de la paleta clara nueva (heredado del
  documento de requisitos, Impact: Medium):** los hex elegidos apuntan a un
  objetivo aproximado de contraste WCAG AA, pero ningún agente de este
  pipeline puede renderizar WebView2 real para medirlo con precisión — la
  Definition of Done ya incluye la validación manual correspondiente
  (ejecutar la aplicación real con ambos temas), no se añade ningún paso
  nuevo aquí.
- **Propiedad futura del contenedor `.claudemeter-root` (Impact: Low, no
  bloqueante):** en este ciclo, `UsagePage.razor` es el único componente
  raíz y por tanto el único que envuelve su contenido en
  `.claudemeter-root`. Cuando el Ciclo B introduzca `ScreenNavigator`/
  `IWidgetScreen` (issue #18) para alternar entre `UsagePage` y la futura
  `MascotPage`, ese componente pasará a ser el nuevo punto natural para
  aplicar `.claudemeter-root`/`Config.Theme.ToCssClass()` una única vez
  (en vez de que cada `IWidgetScreen` lo repita) — un ajuste de una línea,
  no un rediseño, gracias a que la clase CSS y las variables ya son
  reutilizables tal cual desde este ciclo. Se documenta aquí para que
  Design del Ciclo B lo tenga presente, sin requerir ninguna decisión
  humana ahora.
- Sin decisiones de producto/arquitectura genuinamente abiertas: el
  documento de requisitos ya delegó explícitamente a Design el formato del
  countdown y el mecanismo CSS vs. JS de la animación — ambos quedan
  resueltos arriba con su rationale.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados (bUnit para el renderizado de
      `UsageBar`/`UsagePage`/`ReauthNotice` bajo ambos temas y para la
      presencia/transición de clases CSS del countdown; xUnit puro para la
      resolución del nuevo campo `theme` en `AppConfigStore`, incluyendo el
      caso de valor inválido con fallback) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada depende de un tema real de Windows ni de
      un `config.json` real del usuario — los tests usan ficheros
      temporales o valores simulados
- [ ] **Validación manual (US-1):** se confirma, ejecutando la aplicación
      real en Windows con `config.json` en tema claro y en tema oscuro, que
      ambas paletas se ven correctamente y son legibles sobre el
      escritorio real — ningún agente de este pipeline puede verificar
      renderizado real de WPF/WebView2
- [ ] **Validación manual (US-2):** se confirma, ejecutando la aplicación
      real durante varios ciclos de poll, que la animación del countdown se
      percibe fluida y sin impacto notable de CPU/GPU (AC explícita de
      la issue #15) — percepción de fluidez y coste real de rendimiento en
      un equipo real que ningún agente de este pipeline puede medir de
      forma automática
