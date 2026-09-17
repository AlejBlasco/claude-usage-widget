# Design: F1 — Widget visual base

## Requirements Reference

`docs/sdlc/requirements/f1-widget-visual-base.md` — cubre US-1..US-4 (issues
#6, #7, #8, #9), tratadas como un único ciclo SDLC con cadena de dependencia
interna #6 → #7 → #8 → #9.

## Architecture Overview

F1 convierte `ClaudeMeter.Desktop` de scaffolding WPF puro a la primera
versión visual real del widget, sin tocar Domain/Application/Infrastructure
más que en un único punto acotado (US-3, ver más abajo). Todo lo demás se
**reutiliza tal cual** desde Infrastructure/Application, replicando —no
reusando el proyecto— la construcción manual que hacía
`ClaudeMeter.Console/Program.cs` en F0.

Piezas nuevas, de fuera hacia dentro:

1. **`MainWindow.xaml`** (US-1) — ventana WPF sin bordes/topmost/transparente
   que aloja un único control `BlazorWebView`. Ya no es una `Window` con
   contenido XAML propio: todo el contenido visible vive en Razor.
2. **`App.xaml.cs`** — composition root de verdad (hoy vacío). En
   `OnStartup` construye a mano (sin contenedor de terceros, mismo estilo
   que `ClaudeMeter.Console/Program.cs`) el `HttpClient` compartido, un
   `IServiceCollection` con `AddWpfBlazorWebView()` + los puertos de
   Application/Infrastructure, y expone el `IServiceProvider` resultante
   para que `MainWindow` se lo pase al `BlazorWebView`. En `OnExit` libera el
   `HttpClient`.
3. **`ClaudeMeter.Desktop.Polling.UsagePollingCoordinator`** (nueva, US-4) —
   clase C# plana (sin Blazor ni WPF) que envuelve un `System.Timers.Timer`
   de 60s sobre `IUsageDataSource.GetUsageAsync`, y notifica cada
   `UsageSnapshot` recibido vía un evento `.NET` (`Action<UsageSnapshot,
   DateTimeOffset>`). Es el "cuarto peldaño" que le falta al widget para
   igualar la separación reading/calling/computing/rendering que ya exige
   `CLAUDE.md`: aquí es el peldaño de "orquestar el polling repetido",
   idéntico en intención a `ClaudeMeter.ConsoleApp.Polling.UsagePollingLoop`
   de F0 pero basado en eventos/timer en vez de en un bucle bloqueante
   `while(true)` (que no encaja con el ciclo de vida de un componente Razor
   que se monta/desmonta). Se construye una instancia nueva por cada
   instancia de `UsagePage`, nunca como singleton de DI — ver rationale en
   Cross-Cutting/Error handling y en el AC de US-4 sobre atar el timer al
   componente, no a la ventana.
4. **`ClaudeMeter.Desktop.Pages.UsagePage.razor`** (US-2/US-4) — componente
   raíz que se inyecta como `RootComponent` del `BlazorWebView`. Crea su
   propio `UsagePollingCoordinator`, mantiene el último
   `(RateLimitWindow Session, RateLimitWindow Weekly)` calculado con el
   `RateLimitWindowParser` ya existente, y renderiza dos
   `UsageBar`.
5. **`ClaudeMeter.Desktop.Pages.UsageBar.razor`** (US-2/US-3) — componente
   hijo reutilizado para sesión y semana, única fuente de la barra +
   etiqueta + color, para garantizar que ambas ventanas usan exactamente la
   misma regla de umbral (AC de US-3).
6. **`ClaudeMeter.Domain.Usage.UsageThreshold` / `UsageThresholdClassifier`**
   (US-3, único cambio a Domain) — enum + función pura sin dependencias que
   clasifica un `PercentageUsed` en `Normal`/`Warning`/`Critical`/`null`
   ("sin datos"). Ver rationale de ubicación en Technology Choices.

Diagrama de dependencias resultante (las flechas existentes de Clean
Architecture no cambian; solo se listan las piezas nuevas):

```
Desktop
 ├── App.xaml.cs (composition root) ──> Application.Abstractions, Infrastructure
 ├── MainWindow.xaml(.cs) ──> BlazorWebView, App.Services
 ├── Polling/UsagePollingCoordinator ──> Application.Abstractions (IUsageDataSource)
 └── Pages/UsagePage.razor ──> Polling.UsagePollingCoordinator, Domain.Usage (RateLimitWindowParser)
      └── Pages/UsageBar.razor ──> Domain.Usage (UsageThreshold, RateLimitWindow)

Domain.Usage
 └── UsageThreshold.cs (nuevo, sin dependencias)
```

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| `Microsoft.AspNetCore.Components.WebView.Wpf`, SDK del proyecto `Microsoft.NET.Sdk.Razor` | Único camino soportado por Microsoft para alojar Blazor en WPF (tutorial oficial verificado); ya confirmado en requisitos. Versión de paquete verificada en el índice de NuGet (`api.nuget.org/v3-flatcontainer/.../index.json`) a fecha de diseño: **8.0.100** es la última estable de la serie 8.x compatible con `net8.0-windows` (las series 9.x/10.x exigen TFM `net9.0-windows`/`net10.0-windows10.0.*`, incompatibles con el `net8.0-windows` ya fijado por `Directory.Build.props`). Confirmar en Development si existe un 8.0.x más reciente antes de fijar la versión final. | Mantener `Microsoft.NET.Sdk` y renderizar todo en XAML puro (sin Blazor) — rechazada porque contradice el objetivo de producto ya fijado en `CLAUDE.md` ("Blazor Hybrid: WPF host + BlazorWebView") y las 4 AC de US-1/US-2 exigen explícitamente un `BlazorWebView` operativo. |
| `System.Timers.Timer` + `IDisposable.Dispose()` dentro del componente/coordinador, con `InvokeAsync` para marshaling al hilo de render | Patrón demostrado literalmente por la documentación oficial "ASP.NET Core Razor component disposal" para la versión `aspnetcore-8.0` (verificado por fetch directo, incluye el mismo aviso de envolver `StateHasChanged` en `InvokeAsync` porque el callback de `Elapsed` corre fuera del contexto de sincronización de Blazor). Es el ejemplo que la propia Microsoft usa para enseñar exactamente el AC que aquí más preocupa (fuga de timer). | `PeriodicTimer` con un bucle `await WaitForNextTickAsync()` en `OnInitializedAsync` — rechazada: fuentes de la comunidad (Havit Knowledge Base) documentan que ese patrón es más apropiado para `BackgroundService`/consola que para el ciclo de vida corto y desmontable de un componente Razor, y complica el test bUnit de "el timer se para al desmontar" frente a la simple llamada síncrona `Dispose()` del patrón elegido. |
| Nueva clase `UsagePollingCoordinator` en `ClaudeMeter.Desktop.Polling` (no en `ClaudeMeter.Application`) | Reutiliza `IUsageDataSource` sin reimplementar llamada/parseo (AC de US-4), pero es un detalle de "cómo programar el refresco para una UI concreta", no un caso de uso de negocio — encaja mejor como colaborador de presentación en Desktop, análogo a `UsagePollingLoop` en el proyecto de consola (mismo patrón, mismo nivel de capa). Al ser una clase C# plana sin `@code` de Blazor, es reutilizable sin cambios por `MascotPage.razor` en F3 (que según `CLAUDE.md` debe "estar dirigido solo por el snapshot actual"). | Meter la lógica de timer directamente en el `@code` de `UsagePage.razor` — rechazada: mezclaría "orquestar el polling" con "renderizar", justo la separación que `CLAUDE.md` pide preservar, y no sería reutilizable por `MascotPage.razor` sin copiar/pegar. |
| `UsageThreshold`/`UsageThresholdClassifier` en `ClaudeMeter.Domain.Usage` (no en Desktop) | Es lógica pura sin ninguna dependencia de UI (AC explícito de US-3), y Domain ya tiene cero dependencias externas, cumpliendo el requisito al 100 %. Además `CLAUDE.md` ya prevé `MascotState` como value object de Domain para F3, que muy probablemente también se calculará a partir de umbrales de porcentaje — colocarlo en Domain ahora evita duplicar la regla de umbral entre `UsagePage` (F1) y `MascotPage` (F3). El enum no modela colores (`Green/Amber/Red`) sino severidad (`Normal/Warning/Critical`); el mapeo severidad→color CSS es responsabilidad exclusiva de `UsageBar.razor` en Desktop. | Colocarlo como método estático en Desktop (p. ej. `ClaudeMeter.Desktop.Rendering.ThresholdClassifier`) — rechazada: habría que moverlo a Domain de todos modos en F3 para que `MascotState` lo reutilice, y Domain ya es un lugar seguro (no hay riesgo de arrastrar dependencias de UI porque el proyecto no las tiene). |
| Barra de progreso como `<div>` + `width` dinámico en CSS (no `<progress>` HTML nativo) | Control total y sencillo del color por umbral vía clases CSS; `<progress>` nativo requiere pseudo-elementos específicos de motor (`::-webkit-progress-value`) para recolorear, con soporte más frágil dentro de WebView2/Chromium embebido. Trivial de verificar en bUnit inspeccionando el `style`/clase del `<div>`. | `<progress value="X" max="100">` — rechazada por la fragilidad de estilizado entre motores mencionada arriba; el issue original deja la tecnología a criterio de Design. |
| bUnit 2.11.3 (ya fijado en el csproj) con la clase base **`BunitContext`** | Verificado contra la guía oficial de migración bUnit 1→2: en bUnit v2 `TestContext` fue renombrada a `BunitContext` sin alias de compatibilidad. El documento de requisitos citaba `TestContext` (nombre de la API v1); como la versión ya fijada en el repo es 2.11.3, el nombre correcto a usar en Development es `BunitContext`, y `DisposeComponentsAsync()` (no `DisposeComponents()`, también renombrado/asíncrono en v2). | Ninguna — es una corrección de nombre de API frente a una versión de paquete ya fijada, no una elección de diseño. |
| Sin MediatR en F1 | `CLAUDE.md` describe Application como "casos de uso vía MediatR ... más puertos pequeños", pero a día de hoy (F0 incluido) ningún caso de uso usa MediatR todavía — `IUsageDataSource.GetUsageAsync` es un paso directo sin ninguna orquestación adicional que justifique un `IRequestHandler`. Añadirlo ahora sería ceremonia sin beneficio para una única llamada de paso. Se mantiene la inyección directa del puerto (`IUsageDataSource`) en `UsagePollingCoordinator`, igual que ya hace `UsagePollingLoop` en F0. | Introducir ahora un `GetCurrentUsageQuery`/`GetCurrentUsageQueryHandler` con MediatR — rechazada por prematura; se reconsiderará cuando exista un caso de uso con lógica de orquestación real que lo justifique (candidato natural: F4, con `IUsageHistoryStore` de por medio). |

## Data Model / Interfaces

### Nuevo tipo de Domain

```csharp
// src/ClaudeMeter.Domain/Usage/UsageThreshold.cs
namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Nivel de severidad de un porcentaje de consumo, sin ninguna noción de
/// color ni de UI — el mapeo a color concreto es responsabilidad exclusiva
/// de la capa de presentación (Desktop).
/// </summary>
public enum UsageThreshold
{
    Normal,
    Warning,
    Critical
}

/// <summary>
/// Clasifica un <see cref="RateLimitWindow.PercentageUsed"/> en un
/// <see cref="UsageThreshold"/>. Función pura, sin dependencias externas,
/// sin acceso a reloj ni E/S — cumple el mismo estándar de "lógica pura y
/// determinista" que <see cref="RateLimitWindowParser"/>.
/// </summary>
public static class UsageThresholdClassifier
{
    private const double WarningThreshold = 70;
    private const double CriticalThreshold = 90;

    /// <summary>
    /// Devuelve <c>null</c> cuando <paramref name="percentageUsed"/> es
    /// <c>null</c> ("sin datos" — ningún umbral de color aplica, AC de
    /// US-3). En caso contrario: &lt;70 → Normal, [70,90) → Warning,
    /// &gt;=90 → Critical.
    /// </summary>
    public static UsageThreshold? Classify(double? percentageUsed)
    {
        if (percentageUsed is not { } value)
        {
            return null;
        }

        if (value >= CriticalThreshold)
        {
            return UsageThreshold.Critical;
        }

        return value >= WarningThreshold ? UsageThreshold.Warning : UsageThreshold.Normal;
    }
}
```

### Nueva clase de Desktop (orquestación, US-4)

```csharp
// src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs
namespace ClaudeMeter.Desktop.Polling;

/// <summary>
/// Orquesta el poll periódico de <see cref="IUsageDataSource"/> sin conocer
/// Blazor ni WPF: gestiona un <see cref="System.Timers.Timer"/> y notifica
/// cada <see cref="UsageSnapshot"/> obtenido (junto al instante <c>now</c>
/// usado como referencia) mediante un evento .NET. Cualquier página Razor
/// (hoy <c>UsagePage</c>, en F3 <c>MascotPage</c>) puede suscribirse sin
/// duplicar temporización. Se crea una instancia nueva por cada componente
/// que la usa — nunca un singleton de DI — para que su ciclo de vida quede
/// atado 1:1 al del componente (ver AC de US-4 y Cross-Cutting/Error handling).
/// </summary>
public sealed class UsagePollingCoordinator : IDisposable
{
    private readonly IUsageDataSource _usageDataSource;
    private readonly System.Timers.Timer _timer;
    private volatile bool _isPolling;
    private bool _disposed;

    public event Action<UsageSnapshot, DateTimeOffset>? SnapshotReceived;

    public UsagePollingCoordinator(IUsageDataSource usageDataSource, TimeSpan interval)
    {
        _usageDataSource = usageDataSource;
        _timer = new System.Timers.Timer(interval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>Dispara el primer fetch inmediatamente y arranca el timer para los siguientes.</summary>
    public void Start()
    {
        _ = PollAsync();
        _timer.Start();
    }

    // internal + InternalsVisibleTo hacia ClaudeMeter.Desktop.Tests, mismo
    // patrón ya usado por UsagePollingLoop.ExecuteIterationAsync (F0) para
    // no esperar 60s reales por test.
    internal Task PollOnceForTestsAsync() => PollAsync();
    internal bool IsRunningForTests => _timer.Enabled;

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e) => _ = PollAsync();

    private async Task PollAsync()
    {
        if (_isPolling)
        {
            return; // evita solapes si una llamada tarda más que el intervalo (AC: nunca dos timers/llamadas en paralelo)
        }

        _isPolling = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var snapshot = await _usageDataSource.GetUsageAsync();
            SnapshotReceived?.Invoke(snapshot, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ninguna capa inferior debería lanzar para los casos esperados
            // (ver UsageSnapshot); esto es solo la red de seguridad final,
            // igual que en UsagePollingLoop (F0). Sin renderer al que
            // delegar aquí: simplemente no se levanta ningún evento y el
            // timer sigue vivo para el siguiente ciclo.
        }
        finally
        {
            _isPolling = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
        _disposed = true;
    }
}
```

### `UsagePage.razor` (US-2 + US-4)

```razor
@* src/ClaudeMeter.Desktop/Pages/UsagePage.razor *@
@implements IDisposable
@inject IUsageDataSource UsageDataSource

<div class="usage-widget">
    <UsageBar Title="Sesión" Window="@_session" Stale="@_isStale" />
    <UsageBar Title="Semana" Window="@_weekly" Stale="@_isStale" />
</div>

@code {
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private UsagePollingCoordinator? _coordinator;
    private RateLimitWindow _session = RateLimitWindow.Unavailable;
    private RateLimitWindow _weekly = RateLimitWindow.Unavailable;
    private bool _hasEverSucceeded;
    private bool _isStale;

    protected override void OnInitialized()
    {
        _coordinator = new UsagePollingCoordinator(UsageDataSource, RefreshInterval);
        _coordinator.SnapshotReceived += OnSnapshotReceived;
        _coordinator.Start();
    }

    private void OnSnapshotReceived(UsageSnapshot snapshot, DateTimeOffset now)
    {
        // Llega desde el hilo del Timer, fuera del contexto de
        // sincronización de Blazor -> hay que pasar por InvokeAsync (mismo
        // motivo documentado por Microsoft para System.Timers.Timer.Elapsed).
        _ = InvokeAsync(() =>
        {
            Apply(snapshot, now);
            StateHasChanged();
        });
    }

    // internal + InternalsVisibleTo hacia ClaudeMeter.Desktop.Tests: permite
    // a bUnit simular un ciclo de refresco concreto sin esperar el timer real.
    internal void ApplyForTests(UsageSnapshot snapshot, DateTimeOffset now) => Apply(snapshot, now);
    internal bool IsPollingActiveForTests => _coordinator?.IsRunningForTests ?? false;

    private void Apply(UsageSnapshot snapshot, DateTimeOffset now)
    {
        var (session, weekly) = RateLimitWindowParser.ParseSnapshot(snapshot, now);

        if (snapshot.IsSuccess)
        {
            _session = session;
            _weekly = weekly;
            _isStale = false;
            _hasEverSucceeded = true;
        }
        else if (_hasEverSucceeded)
        {
            // Hubo un dato válido antes: se conserva el último _session/_weekly
            // pero se marca como potencialmente desactualizado (AC de US-4).
            _isStale = true;
        }
        else
        {
            // Nunca hubo éxito: se muestra "sin datos" directamente (US-2/US-3).
            _session = session; // == RateLimitWindow.Unavailable
            _weekly = weekly;
        }
    }

    public void Dispose()
    {
        if (_coordinator is not null)
        {
            _coordinator.SnapshotReceived -= OnSnapshotReceived;
            _coordinator.Dispose();
        }
    }
}
```

Nota de robustez a implementar: si `MainWindow` se cierra mientras una
llamada HTTP está en vuelo, `OnSnapshotReceived` puede intentar invocar
`InvokeAsync` sobre un componente ya desmontado. El desarrollador debe
envolver el cuerpo de `OnSnapshotReceived` en un `try/catch` que ignore
`ObjectDisposedException`/`InvalidOperationException` de renderer, ya que en
ese momento el componente está siendo destruido y no hay nada más que hacer;
esto no es una fuga (el timer ya fue detenido por `Dispose()` antes de que
el renderer se destruya) sino una carrera benigna de la última respuesta en
vuelo.

### `UsageBar.razor` (US-2 + US-3)

```razor
@* src/ClaudeMeter.Desktop/Pages/UsageBar.razor *@

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
</div>

@code {
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public RateLimitWindow Window { get; set; } = RateLimitWindow.Unavailable;

    [Parameter]
    public bool Stale { get; set; }

    // Única función de umbral para ambas barras -> AC de US-3.
    private string ColorClass => UsageThresholdClassifier.Classify(Window.PercentageUsed) switch
    {
        UsageThreshold.Normal => "green",
        UsageThreshold.Warning => "amber",
        UsageThreshold.Critical => "red",
        _ => "neutral"
    };
}
```

### `_Imports.razor` (nuevo)

```razor
@using Microsoft.AspNetCore.Components.Web
@using ClaudeMeter.Application.Abstractions
@using ClaudeMeter.Domain.Usage
@using ClaudeMeter.Desktop.Polling
@using ClaudeMeter.Desktop.Pages
```

`ClaudeMeter.Application.Abstractions` es un namespace **hijo** de
`ClaudeMeter.Application`, no el namespace raíz del proyecto — importarlo no
trae el identificador `Application` a secas al alcance, así que no reabre la
colisión con `System.Windows.Application` (ver la comprobación de
colisiones más abajo).

### `App.xaml.cs` (composition root)

```csharp
using System.Windows;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeMeter.Desktop;

public partial class App : System.Windows.Application
{
    private HttpClient? _httpClient;

    /// <summary>Proveedor de servicios construido en <see cref="OnStartup"/>, leído por <c>MainWindow</c>.</summary>
    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _httpClient = new HttpClient();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddSingleton(_httpClient);
        services.AddSingleton<ITokenProvider, CredentialsFileTokenProvider>();
        services.AddSingleton<IUsageDataSource, AnthropicApiUsageDataSource>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
```

`using ClaudeMeter.Application.Abstractions;` no hace falta aquí porque los
tipos `ITokenProvider`/`IUsageDataSource` solo se usan como parámetro de tipo
genérico de `AddSingleton<TService, TImplementation>()`; si Development
decide añadir el `using` igualmente, sigue siendo el namespace hijo, no el
raíz — mismo razonamiento que en `_Imports.razor`.

### `MainWindow.xaml` (US-1)

```xml
<Window x:Class="ClaudeMeter.Desktop.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:ClaudeMeter.Desktop"
        xmlns:pages="clr-namespace:ClaudeMeter.Desktop.Pages"
        xmlns:blazor="clr-namespace:Microsoft.AspNetCore.Components.WebView.Wpf;assembly=Microsoft.AspNetCore.Components.WebView.Wpf"
        mc:Ignorable="d"
        Title="ClaudeMeter"
        Width="280" Height="140"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        WindowStartupLocation="Manual">
    <Grid>
        <blazor:BlazorWebView x:Name="BlazorWebViewHost"
                              HostPage="wwwroot\index.html">
            <blazor:BlazorWebView.RootComponents>
                <blazor:RootComponent Selector="#app" ComponentType="{x:Type pages:UsagePage}" />
            </blazor:BlazorWebView.RootComponents>
        </blazor:BlazorWebView>
    </Grid>
</Window>
```

Defaults de tamaño/posición decididos aquí (ninguna issue los especifica,
delegado a Design por el propio documento de requisitos):

- **280×280 → 280×140**, tamaño fijo compacto acorde a un widget con dos
  barras + etiquetas (el 800×450 de la plantilla WPF por defecto es
  claramente excesivo para este contenido).
- **`ResizeMode="NoResize"`**: sin bordes no hay tirador de redimensionado
  visible; permitirlo solo generaría redimensionados accidentales por
  teclado/API sin ningún control visual para deshacerlos.
- **`ShowInTaskbar="False"`**: consistente con el "so that" de US-1 ("se
  sienta como un overlay de escritorio y no como una ventana de aplicación
  normal") — una ventana de overlay no debería competir por espacio en la
  barra de tareas ni en Alt-Tab.
- **Posición**: esquina inferior derecha de la pantalla principal con un
  margen de 16px, calculada en `MainWindow.xaml.cs` a partir de
  `SystemParameters.WorkArea` (ver más abajo) — comportamiento típico de
  widgets de escritorio (reloj, Rainmeter). La posición configurable es
  explícitamente F2 (`config.json`), fuera de alcance aquí.

### `MainWindow.xaml.cs`

```csharp
using System.Windows;

namespace ClaudeMeter.Desktop;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;

    public MainWindow()
    {
        InitializeComponent();

        BlazorWebViewHost.Services = ((App)System.Windows.Application.Current).Services;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - ScreenMargin;
        Top = workArea.Bottom - Height - ScreenMargin;
    }
}
```

`System.Windows.Application.Current` se escribe siempre completamente
cualificado en este fichero (ver comprobación de colisiones abajo), aunque
en este `.cs` concreto no haya hoy ningún `using ClaudeMeter.Application;`
que la haga ambigua — es una norma defensiva para todo composition-root code
de Desktop, no solo una corrección puntual.

### `wwwroot/index.html` (nuevo, mínimo)

```html
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>ClaudeMeter</title>
    <base href="/" />
    <link href="css/app.css" rel="stylesheet" />
</head>
<body>
    <div id="app">Cargando…</div>
    <div id="blazor-error-ui" data-nosnippet>
        Ha ocurrido un error inesperado.
        <a href="" class="reload">Recargar</a>
        <a class="dismiss">🗙</a>
    </div>
    <script src="_framework/blazor.webview.js"></script>
</body>
</html>
```

Se omite deliberadamente Bootstrap (presente en la plantilla oficial de
Microsoft): el widget solo necesita dos barras y unas etiquetas, y añadir
~200 KB de CSS de terceros no aporta nada a un proceso que se quiere ligero
(ver Risk de performance heredado de requisitos). `wwwroot/css/app.css` se
escribe a mano con las clases usadas por `UsageBar.razor`
(`.usage-widget`, `.usage-bar`, `.usage-bar--green/amber/red/neutral`,
`.usage-bar__*`).

Sobre la transparencia dentro del `BlazorWebView` (ver también Technical
Notes del documento de requisitos): WebView2/Chromium pinta su rectángulo de
forma opaca salvo que el host llame explícitamente a una API nativa
adicional (`CoreWebView2Controller.DefaultBackgroundColor` con alfa 0), lo
cual el propio documento de requisitos marca como fuera de alcance de F1.
Por tanto `app.css` fija un fondo **opaco** deliberado con estética de
widget/HUD oscuro (p. ej. `#20232aee`) en vez de intentar `background:
transparent` en el `<body>`, que en Chromium embebido sin esa API nativa se
resolvería en blanco por defecto y sería peor experiencia visual que un
fondo oscuro intencional. La transparencia real solo se ve **alrededor** del
`BlazorWebView`, tal y como pide el AC.

## Implementation Plan

1. **Domain (US-3, único cambio fuera de Desktop):** crear
   `src/ClaudeMeter.Domain/Usage/UsageThreshold.cs` con el enum
   `UsageThreshold` y la clase estática `UsageThresholdClassifier` de la
   sección Data Model. Sin cambios en `ClaudeMeter.Domain.csproj`.
2. **csproj de Desktop (US-1):** cambiar `Sdk="Microsoft.NET.Sdk"` →
   `Sdk="Microsoft.NET.Sdk.Razor"`; añadir `<RootNamespace>ClaudeMeter.Desktop</RootNamespace>`
   (workaround documentado por Microsoft para inferencia de namespace de
   Razor en proyectos WPF — relevante aquí porque los `.razor` nuevos viven
   en la subcarpeta `Pages/`); añadir
   `<PackageReference Include="Microsoft.AspNetCore.Components.WebView.Wpf" Version="8.0.100" />`
   (confirmar en NuGet si hay un 8.0.x más reciente en el momento de
   implementar); añadir `<InternalsVisibleTo Include="ClaudeMeter.Desktop.Tests" />`.
   Mantener `OutputType=WinExe`, `TargetFramework=net8.0-windows`,
   `UseWPF=true` sin cambios (el tutorial oficial solo toca el `Sdk` y añade
   `RootNamespace`, no toca estas tres propiedades).
3. **Andamiaje Blazor (US-1):** crear `_Imports.razor`,
   `wwwroot/index.html`, `wwwroot/css/app.css` según la sección anterior.
4. **`App.xaml.cs` (US-1/US-4):** implementar el composition root descrito
   arriba (`OnStartup`/`OnExit`).
5. **`MainWindow.xaml` + `.xaml.cs` (US-1):** reemplazar el contenido actual
   por el XAML/code-behind de la sección anterior.
6. **`Polling/UsagePollingCoordinator.cs` (US-4):** implementar tal cual la
   sección Data Model.
7. **`Pages/UsageBar.razor` (US-2/US-3):** implementar tal cual la sección
   Data Model.
8. **`Pages/UsagePage.razor` (US-2/US-4):** implementar tal cual la sección
   Data Model, incluyendo el `try/catch` defensivo en `OnSnapshotReceived`
   descrito en la nota de robustez.
9. **Tests de Domain (US-3):** `test/ClaudeMeter.Domain.Tests/Usage/UsageThresholdClassifierTests.cs`,
   xUnit puro, cubriendo `< 70`, `[70, 90)`, `>= 90`, ambas fronteras exactas
   (69.9/70/89.9/90) y `null`.
10. **Migrar `test/ClaudeMeter.Desktop.Tests` a bUnit (US-2/US-4):** cambiar
    su `Sdk="Microsoft.NET.Sdk"` → `Sdk="Microsoft.NET.Sdk.Razor"` (resto del
    csproj sin cambios: `bunit 2.11.3`, `xunit 2.9.2`, etc. ya están fijados
    y son compatibles).
11. **Test double (US-2/US-4):** crear
    `test/ClaudeMeter.Desktop.Tests/TestDoubles/FakeUsageDataSource.cs`,
    inspirado en `test/ClaudeMeter.Console.Tests/Polling/FakeUsageDataSource.cs`
    pero con un resultado **reconfigurable entre llamadas** (p. ej. una cola
    `Queue<UsageSnapshot>` o un campo mutable con un método
    `SetNextResult(UsageSnapshot)`), porque aquí sí hace falta simular varios
    ciclos de refresco consecutivos con resultados distintos (AC de US-4
    sobre dos ciclos consecutivos).
12. **Tests bUnit (US-2/US-3/US-4):**
    `test/ClaudeMeter.Desktop.Tests/Pages/UsageBarTests.cs` (umbral →
    clase CSS, `null` → estado neutro) y
    `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`, usando
    `BunitContext` (no `TestContext`) y `DisposeComponentsAsync()`, apoyándose
    en `ApplyForTests`/`IsPollingActiveForTests`/`PollOnceForTestsAsync`
    (`internal`, visibles vía `InternalsVisibleTo`) para no depender de
    esperas reales de 60s. Casos mínimos: snapshot `Success` con ambos
    porcentajes → dos barras con su color; `PercentageUsed` nulo → "no
    disponible"; snapshot no-`Success` → ambas barras "no disponible"; tras
    un ciclo exitoso seguido de un fallo → se conserva el valor anterior
    marcado "(desactualizado)"; `DisposeComponentsAsync()` → el timer
    subyacente queda detenido.
13. **Validación de compilación:** `dotnet build ClaudeMeter.sln` en
    Debug y Release (Release activa `TreatWarningsAsErrors`, ver
    `Directory.Build.props`) para confirmar que el cambio de SDK y el nuevo
    paquete no introducen advertencias de analizadores.

## Cross-Cutting Concerns

- **Seguridad:** ningún cambio respecto a F0 — el token nunca se registra
  en logs (F1 no añade logging todavía, ver más abajo) ni se expone en la UI;
  `AnthropicApiUsageDataSource` sigue sin intentar refrescar el token ante
  401/403. Por AC explícito de US-2 (punto 3), F1 muestra el mismo estado
  neutro "no disponible" para `TokenUnavailable`/`Unauthorized`/`RequestFailed`
  sin distinguir mensaje — esto ya satisface la regla de `CLAUDE.md` de "no
  reintentar el refresco y mostrar un aviso claro" en su forma mínima; un
  mensaje específico tipo "sesión caducada" queda para una fase posterior
  si el usuario lo pide, no se introduce aquí porque el AC de F1 pide
  explícitamente el mismo tratamiento para los tres estados de fallo.
- **Rendimiento:** riesgos de `AllowsTransparency` + `BlazorWebView`
  (proceso WebView2 embebido) ya aceptados como Low en el documento de
  requisitos, sin mitigación adicional en F1 más allá de no cargar
  Bootstrap/CSS de terceros innecesario. El guard `_isPolling` en
  `UsagePollingCoordinator` evita acumular llamadas HTTP en paralelo si una
  tarda más que el intervalo de 60s.
- **Manejo de errores:** ninguna excepción de `IUsageDataSource`/
  `RateLimitWindowParser` debería llegar a `UsagePollingCoordinator.PollAsync`
  en el caso esperado (los tipos de Domain ya modelan "sin datos" sin
  lanzar); el `catch` en `PollAsync` es una red de seguridad de último
  recurso, igual que en `UsagePollingLoop` (F0). La carrera "componente
  desmontado mientras hay una respuesta en vuelo" se documenta explícitamente
  en la sección Data Model con su mitigación (try/catch alrededor de
  `InvokeAsync` en `OnSnapshotReceived`).
- **Observabilidad/logging:** F1 no añade Serilog ni ningún logging
  estructurado — eso es F2 (`config.json`, logging a `%LOCALAPPDATA%` según
  `CLAUDE.md`). No hay regresión: F0 tampoco lo tenía.
- **Compatibilidad hacia atrás:** `ClaudeMeter.Console` (F0) no se toca; el
  proyecto de consola sigue funcionando de forma independiente. No hay
  ningún consumidor externo de `ClaudeMeter.Desktop` hoy, por lo que no hay
  superficie pública que romper.

### Comprobación de colisiones de nombres (paso obligatorio)

- **`ClaudeMeter.Application` vs `System.Windows.Application`** (incidente
  ya conocido en este repo): la mitigación ya presente en
  `App.xaml.cs` (`public partial class App : System.Windows.Application`,
  totalmente cualificado en la declaración de clase) se mantiene y se
  extiende como norma a todo el composition-root code nuevo de este diseño:
  - Ningún fichero de Desktop (`App.xaml.cs`, `MainWindow.xaml.cs`,
    `_Imports.razor`) añade `using ClaudeMeter.Application;` a secas — solo
    se importa el namespace **hijo** `ClaudeMeter.Application.Abstractions`,
    que no trae el identificador `Application` al alcance y por tanto no
    puede colisionar con `System.Windows.Application` (CS0104).
  - Toda referencia a la aplicación WPF activa se escribe cualificada como
    `System.Windows.Application.Current` (usado en `MainWindow.xaml.cs`),
    nunca como `Application.Current` a secas, aunque en el fichero concreto
    no haya hoy ninguna ambigüedad real — es la misma disciplina defensiva
    que ya se aplicó en la declaración de `App`.
- **`System.Timers.Timer` vs `System.Threading.Timer`:** `UsagePollingCoordinator.cs`
  usa el tipo completamente cualificado `System.Timers.Timer` (sin
  `using System.Timers;`) para que, si en el futuro alguna refactorización
  añade `using System.Threading;` al mismo fichero, no se reabra una
  ambigüedad de nombre `Timer` entre ambos namespaces.
- **`ClaudeMeter.Console` / `System.Console`:** ya mitigado desde F0 con
  `<RootNamespace>ClaudeMeter.ConsoleApp</RootNamespace>` en
  `ClaudeMeter.Console.csproj` — no afecta a este diseño (Desktop no
  referencia el proyecto de consola), se documenta solo como precedente del
  mismo tipo de mitigación ya aceptado en este repo.
- **`ClaudeMeter.Desktop.Pages`/`UsageBar`/`UsagePage`:** comprobados contra
  namespaces de WPF/BCL habituales (`System.Windows.Controls.Page`,
  `System.Windows.Controls.ProgressBar`) — no hay colisión porque
  `UsagePage`/`UsageBar` son nombres de tipo distintos y vivirán en Razor
  (`.razor`), no como `using System.Windows.Controls;` + tipo del mismo
  nombre en el mismo fichero.
- **`RootNamespace` explícito (`ClaudeMeter.Desktop`) en el csproj:** no
  cambia el valor por defecto (ya coincide con el nombre de ensamblado),
  pero se fija explícitamente porque el SDK de Razor infiere el namespace de
  cada `.razor` a partir de su ruta de carpeta relativa al `RootNamespace`
  — con los `.razor` nuevos viviendo en `Pages/`, un `RootNamespace`
  correctamente resuelto es lo que garantiza que `UsagePage`/`UsageBar`
  terminen en `ClaudeMeter.Desktop.Pages` (el namespace que `_Imports.razor`
  y `MainWindow.xaml` (`xmlns:pages=...`) ya asumen).

## Risks & Open Decisions

- **Riesgo de plataforma (heredado, US-1):** limitación de "airspace" entre
  la transparencia de `MainWindow` y el rectángulo opaco de WebView2 —
  inherente a la plataforma, sin alternativa dentro de alcance de F1 (la
  técnica nativa para hacer transparente el propio `BlazorWebView` queda
  fuera de alcance según el propio documento de requisitos). Impact: Low,
  ya aceptado. Mitigado por diseño (fondo oscuro intencional dentro del
  `BlazorWebView`, transparencia real solo alrededor).
- **Riesgo de rendimiento (heredado, US-1):** overhead de memoria/composición
  de `AllowsTransparency` + proceso WebView2 embebido en un widget que se
  pretende ligero. Impact: Low, ya aceptado explícitamente en el issue
  original; sin mitigación adicional más allá de no cargar CSS/JS de
  terceros innecesarios.
- **Riesgo técnico nuevo, con mitigación ya decidida (no requiere sign-off):**
  carrera entre el cierre de `MainWindow`/desmontaje de `UsagePage` y una
  respuesta HTTP en vuelo — mitigada con `try/catch` alrededor de
  `InvokeAsync` en `OnSnapshotReceived` (ver Data Model). Se deja documentado
  aquí como riesgo conocido a verificar en Development/Testing, no como
  decisión pendiente.
- **Versión exacta de `Microsoft.AspNetCore.Components.WebView.Wpf`:**
  verificada como 8.0.100 en el índice de NuGet a fecha de diseño
  (2026-09-16); Development debe confirmar si existe un patch 8.0.x más
  reciente antes de fijar la versión final en el csproj — no es una decisión
  de arquitectura, es una comprobación mecánica de packaging.
- Sin decisiones de producto/arquitectura genuinamente abiertas: el propio
  documento de requisitos ya delegó explícitamente a Design el tamaño/
  posición de ventana, la tecnología de barra de progreso y el mecanismo de
  timer — las tres quedan resueltas arriba con su rationale, sin necesidad
  de validación humana adicional.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1, US-2, US-3 y US-4 del documento
      de requisitos se cumplen
- [ ] Cobertura de tests automatizados (bUnit para US-2/US-4 con
      `BunitContext`, xUnit puro para `UsageThresholdClassifier` de US-3)
      según el mínimo configurado en `.claude/sdlc.config.yaml`
      (`testingCoverage: 70`)
- [ ] **Validación manual (US-1):** se confirma visualmente, ejecutando la
      aplicación en un equipo Windows real, que `MainWindow` aparece sin
      bordes/barra de título, permanece topmost sobre otras ventanas, y que
      el fondo es realmente transparente fuera del contenido Razor —
      ningún agente de este pipeline puede verificar renderizado real de
      WPF/WebView2 de forma automática
- [ ] **Validación manual (US-4):** se confirma, dejando la aplicación
      corriendo un periodo extendido (p. ej. 30-60 minutos) contra la API
      real o un mock de larga duración, que no se acumulan timers ni
      llamadas HTTP crecientes y que el proceso libera el timer al cerrar
      la ventana (uso de memoria/handles estable) — es una validación de
      un proceso de larga duración que ningún agente de este pipeline puede
      ejecutar de forma automática
