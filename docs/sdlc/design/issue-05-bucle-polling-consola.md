# Design: [F0] Bucle de polling con salida por consola cada 60s

## Requirements Reference

`docs/sdlc/requirements/issue-05-bucle-polling-consola.md` (Issue #5, GitHub — cierre del milestone "F0 — Núcleo de validación"). Documento cerrado: la decisión de arquitectura de mayor calado (dónde vive el punto de entrada) ya viene fijada por el propio documento como "Decisión confirmada" — nuevo proyecto `src/ClaudeMeter.Console`, exclusivo de F0, que orquesta el core ya implementado (`ITokenProvider` → `IUsageDataSource` → `RateLimitWindowParser`) sin tocar Domain/Application/Infrastructure. Este documento trata esa decisión como restricción y se centra en lo que el documento de requisitos delega explícitamente a la fase de Diseño: forma exacta de las clases de orquestación/renderizado, ubicación de ficheros, `.csproj`/`.sln` exactos, formato de línea completo y estrategia de testing.

## Resumen / Alcance

Este diseño cubre exclusivamente la orquestación del bucle de polling y su salida por consola. Introduce un proyecto nuevo, `ClaudeMeter.Console`, con cuatro piezas nuevas (ninguna existe todavía en el repo):

- `Program.cs` — composition root minimalista (`Main`).
- `UsagePollingLoop` — orquestador del bucle (`while(true)` + `Task.Delay`), con manejo de errores por iteración.
- `IUsagePollingRenderer` / `ConsoleUsagePollingRenderer` — renderizado por consola (éxito y los cuatro casos de error).
- `UsagePollingLineFormatter` — formateo puro de cada línea (sin tocar `Console` en absoluto), consumido por el renderer.

Fuera de alcance (explícitamente, según el propio documento de requisitos y `CLAUDE.md`):
- Cualquier cambio en `ClaudeMeter.Domain`, `ClaudeMeter.Application` o `ClaudeMeter.Infrastructure` — el core (`CredentialsFileTokenProvider`, `AnthropicApiUsageDataSource`, `RateLimitWindowParser`) se consume tal cual, sin modificaciones.
- Reintentos con backoff, `config.json`, logging estructurado (Serilog) — alcance de F2 (issues #11/#12).
- Apagado controlado / cancelación real — el propio documento de requisitos lo excluye explícitamente para F0 ("el proceso corre hasta que se mate manualmente").
- Cualquier reutilización de este proyecto desde `ClaudeMeter.Desktop` — F1 reutiliza el mismo core de Application/Infrastructure directamente, no este proyecto de consola (ver "Decisión confirmada" del documento de requisitos).

## Architecture Overview

### Ubicación exacta de cada pieza nueva

| Tipo | Proyecto | Namespace | Fichero propuesto |
|---|---|---|---|
| `Program` | `ClaudeMeter.Console` | `ClaudeMeter.ConsoleApp` | `src/ClaudeMeter.Console/Program.cs` |
| `UsagePollingLoop` | `ClaudeMeter.Console` | `ClaudeMeter.ConsoleApp` | `src/ClaudeMeter.Console/Polling/UsagePollingLoop.cs` |
| `IUsagePollingRenderer`, `ConsoleUsagePollingRenderer` | `ClaudeMeter.Console` | `ClaudeMeter.ConsoleApp.Rendering` | `src/ClaudeMeter.Console/Rendering/ConsoleUsagePollingRenderer.cs` |
| `UsagePollingLineFormatter` | `ClaudeMeter.Console` | `ClaudeMeter.ConsoleApp.Rendering` | `src/ClaudeMeter.Console/Rendering/UsagePollingLineFormatter.cs` |
| `UsagePollingLoopTests` | `ClaudeMeter.Console.Tests` | `ClaudeMeter.ConsoleApp.Tests.Polling` | `test/ClaudeMeter.Console.Tests/Polling/UsagePollingLoopTests.cs` |
| `UsagePollingLineFormatterTests` | `ClaudeMeter.Console.Tests` | `ClaudeMeter.ConsoleApp.Tests.Rendering` | `test/ClaudeMeter.Console.Tests/Rendering/UsagePollingLineFormatterTests.cs` |
| `FakeUsageDataSource`, `SpyUsagePollingRenderer` (test doubles) | `ClaudeMeter.Console.Tests` | `ClaudeMeter.ConsoleApp.Tests.Polling` | `test/ClaudeMeter.Console.Tests/Polling/*.cs` |

**Rationale de sub-namespaces (`Polling`/`Rendering`)**: el proyecto es pequeño, pero separa explícitamente "orquestar el bucle" de "convertir un resultado en texto/consola", reflejando en la estructura de carpetas la misma separación de responsabilidades que exige la regla de `CLAUDE.md` ("reading the token, calling the API, computing the countdown, and rendering are separate classes"). Alternativa considerada y rechazada: todo en la raíz del proyecto sin subcarpetas (como hace hoy `ClaudeMeter.Domain/Usage`) — rechazada porque aquí, a diferencia de Domain, conviven dos responsabilidades claramente distintas (orquestación con estado/tiempo vs. formateo puro) yends up siendo más legible con la subdivisión.

### Por qué el namespace del proyecto NO es literalmente `ClaudeMeter.Console`

El nombre de proyecto/carpeta/ensamblado `ClaudeMeter.Console` viene fijado por el documento de requisitos y no se cuestiona. Sin embargo, si el **namespace** de C# fuera literalmente `ClaudeMeter.Console`, cualquier código dentro de ese namespace que escriba `Console.WriteLine(...)` fallaría en compilación (`CS0118: 'Console' is a namespace but is used like a type`): la resolución de nombres de C# encuentra primero el namespace anidado `ClaudeMeter.Console` (declarado como miembro de `ClaudeMeter`) antes de considerar el tipo `System.Console` traído por `using System;`, porque la búsqueda por namespaces encerrantes tiene prioridad sobre los `using` de tipos. Esto no es una hipótesis: es un problema real y conocido de C# al nombrar un proyecto/namespace `*.Console`.

**Decisión (resuelta en esta fase, sin necesidad de escalar)**: se fija `<RootNamespace>ClaudeMeter.ConsoleApp</RootNamespace>` en el `.csproj`, dejando `AssemblyName` (por defecto, igual al nombre del `.csproj`) como `ClaudeMeter.Console` para cumplir literalmente el nombre de proyecto exigido por el documento de requisitos. El código fuente usa `namespace ClaudeMeter.ConsoleApp;` en todos los ficheros, de forma que `Console.WriteLine`/`Console.Error.WriteLine` siguen refiriéndose sin ambigüedad a `System.Console`.

Alternativa considerada y rechazada: mantener el namespace `ClaudeMeter.Console` y cualificar cada uso como `System.Console.WriteLine(...)` (o un alias `using Console = System.Console;` en cada fichero) — rechazada porque es ruido repetido en cada fichero nuevo y un error fácil de cometer (un desarrollador nuevo escribe `Console.WriteLine` sin pensarlo, obtiene un error de compilación confuso, y pierde tiempo diagnosticando algo que un `RootNamespace` distinto evita de raíz).

### Flujo de dependencias y de datos

```
ClaudeMeter.Console.Tests
        │ (ProjectReference)
        ▼
ClaudeMeter.Console  (namespace ClaudeMeter.ConsoleApp)
   Program.Main()
        │ construye (composition root, sin contenedor DI)
        ▼
   UsagePollingLoop.RunAsync(CancellationToken.None)
        │ while(true): ExecuteIterationAsync → Task.Delay(60s) → repetir
        ▼
   IUsageDataSource.GetUsageAsync()  ────────► ClaudeMeter.Application.Abstractions (puerto existente, sin cambios)
        │ implementado por
        ▼
   AnthropicApiUsageDataSource (Infrastructure, sin cambios)
        │ internamente ya llama a ITokenProvider.GetTokenAsync()
        ▼
   CredentialsFileTokenProvider (Infrastructure, sin cambios)

   UsagePollingLoop, tras recibir UsageSnapshot:
        ├─ Success        → RateLimitWindowParser.ParseSnapshot(snapshot, now) [Domain, sin cambios]
        │                     → IUsagePollingRenderer.RenderSuccess(now, session, weekly)
        ├─ TokenUnavailable → IUsagePollingRenderer.RenderTokenUnavailable(now)
        ├─ Unauthorized     → IUsagePollingRenderer.RenderUnauthorized(now)
        ├─ RequestFailed    → IUsagePollingRenderer.RenderRequestFailed(now)
        └─ Exception no controlada (catch en ExecuteIterationAsync)
                            → IUsagePollingRenderer.RenderUnexpectedError(now, ex)

   ConsoleUsagePollingRenderer (única implementación de IUsagePollingRenderer):
        delega el texto en UsagePollingLineFormatter (puro) y solo añade la I/O:
        Console.WriteLine (éxito) / Console.Error.WriteLine (los cuatro casos de error)
```

Puntos clave de este flujo:
- `UsagePollingLoop` **nunca** llama directamente a `ITokenProvider`: pasa siempre por `IUsageDataSource.GetUsageAsync()`, que ya internamente resuelve el token (ver `AnthropicApiUsageDataSource.GetUsageAsync`, línea 61 del fichero existente). Esto es lo que permite que Console no duplique lógica de lectura de token — el documento de requisitos ya lo anticipa ("compone: `CredentialsFileTokenProvider`... y el propio bucle", es decir, Console solo necesita **construir** `CredentialsFileTokenProvider` para inyectarlo en `AnthropicApiUsageDataSource`, no invocarlo él mismo).
- Como consecuencia, el bucle nunca distingue entre `TokenResultStatus.FileNotFound`/`InvalidJson`/`TokenMissing`: los tres colapsan en `UsageSnapshotStatus.TokenUnavailable` antes de llegar a Console. Esto es exactamente lo que pide el Acceptance Criteria de "Token no disponible" (un único mensaje genérico, sin distinguir la sub-causa) — no hace falta ningún cambio en Infrastructure para lograrlo.
- `RateLimitWindowParser.ParseSnapshot` solo se invoca cuando `UsageSnapshotStatus.Success`; en cualquier otro estado, `UsageSnapshot.Session`/`Weekly` son `null` por contrato y no hay nada que parsear — el renderer de error correspondiente se llama directamente, sin pasar por el parser.
- `DateTimeOffset.UtcNow` se captura una única vez al principio de cada iteración (`ExecuteIterationAsync`), tanto para pasarlo a `RateLimitWindowParser` como para el timestamp mostrado — evita dos lecturas de reloj ligeramente distintas dentro de la misma iteración.

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| `while(true)` + `Task.Delay(TimeSpan.FromSeconds(60), ct)` **entre** iteraciones (delay-after, no fixed-rate) | Ya fijado por el documento de requisitos ("Mecánica del bucle"), y además encaja literalmente con la redacción del Acceptance Criteria — "transcurren 60 segundos **desde la última impresión**" describe un intervalo medido desde el final de la iteración anterior, no un tick de reloj de pared fijo. Con `Task.Delay` tras completar la llamada, el ciclo total es `duración_de_la_llamada + 60s`, autocorrectivo ante una llamada lenta (nunca se solapan dos iteraciones). | `PeriodicTimer` (tick a intervalo fijo de pared, independientemente de cuánto tarde la iteración anterior) — rechazado por el propio documento de requisitos (ya decidido) y, adicionalmente, porque su semántica de "tick fijo" no encaja con la redacción del AC ("desde la última impresión") y permitiría, en teoría, que una iteración empezase antes de que la anterior hubiera terminado de imprimir si la llamada HTTP tardase más que el intervalo. |
| `RootNamespace = ClaudeMeter.ConsoleApp` (proyecto/ensamblado se mantiene `ClaudeMeter.Console`) | Evita la colisión de nombres `Console` (namespace) vs. `System.Console` (tipo) explicada arriba, sin ensuciar cada fichero con cualificación explícita. | Cualificar `System.Console.WriteLine` en cada sitio, o `using Console = System.Console;` por fichero — más ruido y más fácil de olvidar en un fichero nuevo. |
| `HttpClient` único, creado en `Program.cs` y compartido durante toda la vida del proceso (`using var httpClient = new HttpClient();`) | Mismo patrón que ya documenta `AnthropicApiUsageDataSource` (constructor recibe el `HttpClient` ya construido; el rationale de por qué debe ser compartido/de larga vida vive en Infrastructure). Un proceso de consola de larga duración con un único `HttpClient` de por vida evita el agotamiento de sockets de crear uno nuevo por iteración. | `IHttpClientFactory` vía `Microsoft.Extensions.Http` — rechazado por sobre-ingeniería: introduciría un contenedor DI completo (`Microsoft.Extensions.Hosting`) solo para un arnés desechable de una única clase consumidora; el propio comentario XMLDoc de `AnthropicApiUsageDataSource` ya deja esa decisión para el composition root real (Desktop, F1), no para este harness. |
| Composición manual (`new` directo en `Program.cs`), sin contenedor de DI | El proyecto tiene un único grafo de dependencias, construido una sola vez, sin necesidad de resolver ámbitos ni ciclos de vida complejos — un contenedor DI no aporta nada aquí. Coherente con que hoy ningún proyecto del repo usa todavía `Microsoft.Extensions.DependencyInjection` (ni Infrastructure ni Application lo referencian). | `Microsoft.Extensions.Hosting` (`HostBuilder` + DI) — rechazado por prematuro: F1 (Desktop, con `MediatR` ya en el roadmap) es el lugar natural para introducir DI de verdad; añadirlo aquí solo para un harness que se descarta en F1 sería trabajo que no se reaprovecha. |
| `IUsagePollingRenderer` como interfaz (con `ConsoleUsagePollingRenderer` como única implementación) | Único punto de esta pieza donde SÍ vale la pena una interfaz (a diferencia de `HttpClient`/DI arriba): permite testear `UsagePollingLoop.ExecuteIterationAsync` con un doble de test (`SpyUsagePollingRenderer`) que verifica qué método se invocó para cada `UsageSnapshotStatus`, sin necesidad de capturar `Console.Out`/`Console.Error` reales ni de mockear HTTP. Coste de mantenimiento nulo (una sola implementación real). | Sin interfaz, `UsagePollingLoop` referencia directamente `ConsoleUsagePollingRenderer` — rechazado porque forzaría a los tests de la lógica de despacho (switch por `UsageSnapshotStatus`) a capturar la salida real de consola (`Console.SetOut`), más frágil y menos expresivo que verificar llamadas a un spy. |
| `xUnit` en `ClaudeMeter.Console.Tests`, mismas versiones de paquete que el resto de proyectos de test (`xunit 2.9.2`, `Microsoft.NET.Test.Sdk 17.12.0`, `coverlet.collector 6.0.2`) | Consistencia con `ClaudeMeter.Domain.Tests`/`ClaudeMeter.Infrastructure.Tests` ya existentes — mismo runner, misma forma de `.csproj`. | N/A — no hay alternativa razonable dado que el resto del repo ya fijó xUnit como framework de testing (`CLAUDE.md`, sección Testing). |
| Salida de éxito por `Console.Out` (`Console.WriteLine`), las cuatro variantes de error por `Console.Error` (`Console.Error.WriteLine`) | `CLAUDE.md` menciona explícitamente ambos mecanismos ("`Console.WriteLine`/`Console.Error.WriteLine`") como correctos para F0, lo que sugiere la distinción por stream; separar éxito de error por stream permite además redirigir/filtrar cada uno por separado al ejecutar el harness manualmente (p. ej. `ClaudeMeter.Console.exe 2> errores.log`). | Todo por `Console.WriteLine` (un solo stream) — rechazado: perdería la distinción que `CLAUDE.md` ya sugiere y dificultaría filtrar errores durante la validación manual de extremo a extremo que exige el issue. |

## Data Model / Interfaces

No se introduce ningún tipo de dominio nuevo — `RateLimitWindow`, `UsageSnapshot`, `UsageSnapshotStatus`, `TokenResult` se consumen tal cual. Las interfaces/clases nuevas son exclusivamente de orquestación y presentación de consola, todas en `ClaudeMeter.Console`.

### `Program.cs` (composition root)

```csharp
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.ConsoleApp.Polling;
using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;

namespace ClaudeMeter.ConsoleApp;

internal static class Program
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private static async Task Main()
    {
        using var httpClient = new HttpClient();

        ITokenProvider tokenProvider = new CredentialsFileTokenProvider();
        IUsageDataSource usageDataSource = new AnthropicApiUsageDataSource(tokenProvider, httpClient);
        IUsagePollingRenderer renderer = new ConsoleUsagePollingRenderer();

        var loop = new UsagePollingLoop(usageDataSource, renderer, PollInterval);

        // F0 no requiere apagado controlado (decisión ya fijada por el
        // documento de requisitos): el proceso corre hasta que se mate
        // manualmente (Ctrl+C / cierre de la ventana de consola).
        await loop.RunAsync(CancellationToken.None);
    }
}
```

### `UsagePollingLoop` (orquestador — `Polling/UsagePollingLoop.cs`)

```csharp
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Polling;

/// <summary>
/// Orquesta el bucle de polling: pide un <see cref="UsageSnapshot"/> a
/// <see cref="IUsageDataSource"/>, calcula las ventanas con
/// <see cref="RateLimitWindowParser"/> y delega el resultado (o el error)
/// en <see cref="IUsagePollingRenderer"/>. No lee el token ni llama a la
/// API directamente, y no formatea ni imprime texto — únicamente
/// coordina, cumpliendo la separación de responsabilidades de CLAUDE.md.
/// </summary>
public sealed class UsagePollingLoop
{
    private readonly IUsageDataSource _usageDataSource;
    private readonly IUsagePollingRenderer _renderer;
    private readonly TimeSpan _interval;

    public UsagePollingLoop(IUsageDataSource usageDataSource, IUsagePollingRenderer renderer, TimeSpan interval)
    {
        _usageDataSource = usageDataSource;
        _renderer = renderer;
        _interval = interval;
    }

    /// <summary>
    /// Ejecuta el bucle indefinidamente. Ninguna excepción de una
    /// iteración individual propaga fuera de este método — ver
    /// <see cref="ExecuteIterationAsync"/>.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await ExecuteIterationAsync(cancellationToken);
            await Task.Delay(_interval, cancellationToken);
        }
    }

    /// <summary>
    /// Ejecuta una única iteración (una llamada al core + un render).
    /// <c>internal</c> + <c>InternalsVisibleTo</c> hacia
    /// <c>ClaudeMeter.Console.Tests</c> únicamente para poder testear la
    /// lógica de despacho sin esperar 60s reales por iteración de test.
    /// </summary>
    internal async Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var snapshot = await _usageDataSource.GetUsageAsync(cancellationToken);
            Render(now, snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Cualquier excepción no controlada por las capas inferiores
            // (que hoy no deberían lanzar ninguna para los casos
            // esperados, ver UsageSnapshot/TokenResult) no debe terminar
            // el proceso — se muestra y el bucle sigue en la siguiente
            // iteración. OperationCanceledException genuina se propaga
            // (mismo criterio que AnthropicApiUsageDataSource).
            _renderer.RenderUnexpectedError(now, ex);
        }
    }

    private void Render(DateTimeOffset now, UsageSnapshot snapshot)
    {
        switch (snapshot.Status)
        {
            case UsageSnapshotStatus.Success:
                var (session, weekly) = RateLimitWindowParser.ParseSnapshot(snapshot, now);
                _renderer.RenderSuccess(now, session, weekly);
                break;
            case UsageSnapshotStatus.TokenUnavailable:
                _renderer.RenderTokenUnavailable(now);
                break;
            case UsageSnapshotStatus.Unauthorized:
                _renderer.RenderUnauthorized(now);
                break;
            case UsageSnapshotStatus.RequestFailed:
                _renderer.RenderRequestFailed(now);
                break;
        }
    }
}
```

### `IUsagePollingRenderer` / `ConsoleUsagePollingRenderer` (`Rendering/ConsoleUsagePollingRenderer.cs`)

```csharp
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Rendering;

public interface IUsagePollingRenderer
{
    void RenderSuccess(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly);
    void RenderTokenUnavailable(DateTimeOffset timestamp);
    void RenderUnauthorized(DateTimeOffset timestamp);
    void RenderRequestFailed(DateTimeOffset timestamp);
    void RenderUnexpectedError(DateTimeOffset timestamp, Exception exception);
}

/// <summary>
/// Única implementación real: delega todo el formateo de texto en
/// <see cref="UsagePollingLineFormatter"/> (puro) y añade exclusivamente
/// la I/O de consola — éxito por <see cref="Console.Out"/>, los cuatro
/// casos de error por <see cref="Console.Error"/>.
/// </summary>
public sealed class ConsoleUsagePollingRenderer : IUsagePollingRenderer
{
    public void RenderSuccess(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly) =>
        Console.WriteLine(UsagePollingLineFormatter.FormatSuccessLine(timestamp, session, weekly));

    public void RenderTokenUnavailable(DateTimeOffset timestamp) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatTokenUnavailableLine(timestamp));

    public void RenderUnauthorized(DateTimeOffset timestamp) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatUnauthorizedLine(timestamp));

    public void RenderRequestFailed(DateTimeOffset timestamp) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatRequestFailedLine(timestamp));

    public void RenderUnexpectedError(DateTimeOffset timestamp, Exception exception) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatUnexpectedErrorLine(timestamp, exception));
}
```

### `UsagePollingLineFormatter` (puro — `Rendering/UsagePollingLineFormatter.cs`)

```csharp
using System.Globalization;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Rendering;

/// <summary>
/// Construye cada línea de texto como un <see cref="string"/>, sin tocar
/// <see cref="Console"/> en ningún punto — permite testear el formato
/// exacto sin capturar streams de consola.
/// </summary>
public static class UsagePollingLineFormatter
{
    private const string NotAvailable = "no disponible";

    public static string FormatSuccessLine(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly) =>
        $"[{FormatTimestamp(timestamp)}] Sesión: {FormatWindow(session)} | Semana: {FormatWindow(weekly)}";

    public static string FormatTokenUnavailableLine(DateTimeOffset timestamp) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Token no disponible: no se pudo leer un token OAuth válido desde .credentials.json.";

    public static string FormatUnauthorizedLine(DateTimeOffset timestamp) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Token inválido/expirado (401/403): la API rechazó el token; no se intentará refrescarlo automáticamente.";

    public static string FormatRequestFailedLine(DateTimeOffset timestamp) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Fallo de conexión con la API: no se pudo completar la petición (red, timeout, error 5xx o cabeceras de rate-limit ausentes).";

    public static string FormatUnexpectedErrorLine(DateTimeOffset timestamp, Exception exception) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Fallo inesperado: {exception.GetType().Name}: {exception.Message}";

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatWindow(RateLimitWindow window) =>
        $"{FormatPercentage(window.PercentageUsed)} ({FormatMinutes(window.MinutesRemaining)})";

    private static string FormatPercentage(double? value) =>
        value is null ? NotAvailable : $"{value.Value.ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static string FormatMinutes(int? value) =>
        value is null ? NotAvailable : $"{value.Value.ToString(CultureInfo.InvariantCulture)} min";
}
```

### Ejemplos de salida (mapeo directo a los GIVEN-WHEN-THEN)

| Escenario | Línea producida |
|---|---|
| Éxito, ambas ventanas con datos | `[14:32:07] Sesión: 42% (23 min) | Semana: 10% (620 min)` |
| Éxito, ventana semanal sin `Reset` interpretable | `[14:32:07] Sesión: 42% (23 min) | Semana: 10% (no disponible)` |
| Éxito, ambos campos de una ventana ausentes | `[14:32:07] Sesión: 42% (23 min) | Semana: no disponible (no disponible)` |
| `TokenUnavailable` | `[14:33:07] ERROR - Token no disponible: no se pudo leer un token OAuth válido desde .credentials.json.` |
| `Unauthorized` (401/403) | `[14:34:07] ERROR - Token inválido/expirado (401/403): la API rechazó el token; no se intentará refrescarlo automáticamente.` |
| `RequestFailed` (red/5xx/headers ausentes) | `[14:35:07] ERROR - Fallo de conexión con la API: no se pudo completar la petición (red, timeout, error 5xx o cabeceras de rate-limit ausentes).` |
| Excepción no controlada inesperada | `[14:36:07] ERROR - Fallo inesperado: InvalidOperationException: <mensaje>` |

### `ClaudeMeter.Console.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>ClaudeMeter.ConsoleApp</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClaudeMeter.Application\ClaudeMeter.Application.csproj" />
    <ProjectReference Include="..\ClaudeMeter.Infrastructure\ClaudeMeter.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Permite que ClaudeMeter.Console.Tests invoque el método interno
         UsagePollingLoop.ExecuteIterationAsync sin esperar 60s reales
         por iteración de test — mismo patrón ya usado por
         ClaudeMeter.Infrastructure con CredentialsFileTokenProvider. -->
    <InternalsVisibleTo Include="ClaudeMeter.Console.Tests" />
  </ItemGroup>

</Project>
```

`TargetFramework`, `ImplicitUsings`, `Nullable`, análisis Roslyn, etc. se heredan de `Directory.Build.props` (raíz del repo) sin necesidad de repetirlos — igual que Domain/Application/Infrastructure. No se fija `net8.0-windows` (eso es exclusivo de `ClaudeMeter.Desktop` por `UseWPF`); un `Exe` de consola puro no lo necesita.

### `ClaudeMeter.Console.Tests.csproj` (mismo patrón que los test projects existentes)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ClaudeMeter.Console\ClaudeMeter.Console.csproj" />
  </ItemGroup>

</Project>
```

### Wiring de `ClaudeMeter.sln`

**Decisión**: no editar el `.sln` a mano (riesgo de GUID mal formado o colisión). Usar la CLI de `dotnet`, que genera los GUIDs y todas las secciones `ProjectConfigurationPlatforms`/`NestedProjects` automáticamente, exactamente igual que se hizo para los cuatro proyectos existentes:

```bash
dotnet new console -n ClaudeMeter.Console -o src/ClaudeMeter.Console --no-restore
dotnet new xunit    -n ClaudeMeter.Console.Tests -o test/ClaudeMeter.Console.Tests --no-restore

dotnet sln ClaudeMeter.sln add src/ClaudeMeter.Console/ClaudeMeter.Console.csproj --solution-folder src
dotnet sln ClaudeMeter.sln add test/ClaudeMeter.Console.Tests/ClaudeMeter.Console.Tests.csproj --solution-folder test

dotnet add src/ClaudeMeter.Console/ClaudeMeter.Console.csproj reference src/ClaudeMeter.Application/ClaudeMeter.Application.csproj
dotnet add src/ClaudeMeter.Console/ClaudeMeter.Console.csproj reference src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj
dotnet add test/ClaudeMeter.Console.Tests/ClaudeMeter.Console.Tests.csproj reference src/ClaudeMeter.Console/ClaudeMeter.Console.csproj
```

Tras esto, editar a mano únicamente el contenido de los `.csproj` generados para dejarlos exactamente como en los bloques anteriores (`RootNamespace`, `InternalsVisibleTo`, y borrar el `Program.cs`/`UnitTest1.cs` de plantilla que genera `dotnet new`).

## Cross-Cutting Concerns

- **Seguridad**: el token OAuth nunca se expone en ninguna línea de consola — ninguno de los formatos de `UsagePollingLineFormatter` incluye `TokenResult.AccessToken` ni ningún dato de `RawRateLimitHeaders` distinto de `Utilization`/`Reset` ya parseados. Se respeta explícitamente la regla de `CLAUDE.md` de no reintentar refrescar el token: el mensaje de `Unauthorized` lo indica de forma literal ("no se intentará refrescarlo automáticamente"), reforzando la regla también de cara al usuario final que lea la consola.
- **Rendimiento**: cadencia de ~60s entre iteraciones (delay-after, ver "Technology Choices"); un único `HttpClient` de por vida evita el agotamiento de sockets. Cada iteración de éxito realiza como mucho una llamada HTTP real (heredado de `AnthropicApiUsageDataSource`, sin cambios) — ver Risks para el coste de quota real que esto implica al correr el harness de forma prolongada.
- **Manejo de errores**: cuatro categorías explícitas y mutuamente excluyentes (`TokenUnavailable`, `Unauthorized`, `RequestFailed`, excepción no controlada), cada una con su propio texto distintivo y ninguna de ellas termina el proceso — el único `try/catch` vive en `UsagePollingLoop.ExecuteIterationAsync`, envolviendo exclusivamente la llamada a `IUsageDataSource.GetUsageAsync` + el despacho a `Render`; no hay ningún otro punto del código de Console que pueda lanzar sin capturar (el formateo es aritmética/string pura, sin I/O).
- **Retrocompatibilidad**: proyecto enteramente nuevo; no modifica ningún tipo ni comportamiento de Domain/Application/Infrastructure ya mergeados (Issues #2, #3, #4) — cero riesgo de romper sus tests existentes.
- **Observabilidad**: ninguna (Serilog es F2/issue #12, como ya fija `CLAUDE.md`); la propia consola es la única superficie de observabilidad de este issue, por diseño.

## Implementation Plan

1. Generar los dos proyectos nuevos y su wiring en `ClaudeMeter.sln` con los comandos `dotnet` de la sección "Wiring de `ClaudeMeter.sln`" (evitar edición manual del `.sln`).
2. Ajustar `src/ClaudeMeter.Console/ClaudeMeter.Console.csproj` para que quede exactamente como el bloque de "Data Model / Interfaces" (`RootNamespace`, `ProjectReference` a Application e Infrastructure, `InternalsVisibleTo` hacia `ClaudeMeter.Console.Tests`). Borrar el `Program.cs` de plantilla generado por `dotnet new console`.
3. Crear `src/ClaudeMeter.Console/Rendering/UsagePollingLineFormatter.cs` con los cinco métodos de formateo puro, documentados con XMLDoc en español, exactamente según el snippet de diseño.
4. Crear `src/ClaudeMeter.Console/Rendering/ConsoleUsagePollingRenderer.cs` con `IUsagePollingRenderer` + `ConsoleUsagePollingRenderer`, delegando todo el texto en el formatter del paso anterior.
5. Crear `src/ClaudeMeter.Console/Polling/UsagePollingLoop.cs` con el constructor, `RunAsync` y `ExecuteIterationAsync` (`internal`) según el snippet de diseño.
6. Crear `src/ClaudeMeter.Console/Program.cs` con el composition root mínimo del snippet de diseño (construcción manual de `HttpClient`, `CredentialsFileTokenProvider`, `AnthropicApiUsageDataSource`, `ConsoleUsagePollingRenderer`, `UsagePollingLoop`, y `await loop.RunAsync(CancellationToken.None)`).
7. Ajustar `test/ClaudeMeter.Console.Tests/ClaudeMeter.Console.Tests.csproj` según el bloque de diseño; borrar `UnitTest1.cs` de plantilla.
8. Crear los dobles de test en `test/ClaudeMeter.Console.Tests/Polling/`: `FakeUsageDataSource` (implementa `IUsageDataSource`, devuelve un `UsageSnapshot` configurado por el test) y `SpyUsagePollingRenderer` (implementa `IUsagePollingRenderer`, registra qué método se invocó y con qué argumentos) — mismo patrón que `FakeTokenProvider`/`StubHttpMessageHandler` ya existentes en `ClaudeMeter.Infrastructure.Tests`.
9. Crear `test/ClaudeMeter.Console.Tests/Rendering/UsagePollingLineFormatterTests.cs` cubriendo: línea de éxito con ambas ventanas completas; línea de éxito con una ventana parcialmente `null` (solo `PercentageUsed` o solo `MinutesRemaining`); línea de éxito con una ventana totalmente `RateLimitWindow.Unavailable`; las cuatro líneas de error (`TokenUnavailable`, `Unauthorized`, `RequestFailed`, `UnexpectedError` con una excepción de ejemplo) — verificando el texto exacto producido.
10. Crear `test/ClaudeMeter.Console.Tests/Polling/UsagePollingLoopTests.cs` invocando directamente `ExecuteIterationAsync` (vía `InternalsVisibleTo`) con un `FakeUsageDataSource` configurado en cada uno de los cuatro `UsageSnapshotStatus` y con una excepción simulada, verificando en cada caso que el `SpyUsagePollingRenderer` recibió la llamada correcta (`RenderSuccess`/`RenderTokenUnavailable`/`RenderUnauthorized`/`RenderRequestFailed`/`RenderUnexpectedError`) con el `timestamp`/argumentos esperados. No se testea `RunAsync` en sí (bucle infinito con `Task.Delay(60s)` real) ni `Program.cs` — ver "Risks & Open Decisions" para el rationale de por qué quedan fuera de la cobertura automática.
11. Ejecutar `dotnet test` sobre la solución completa y comprobar que no se introducen warnings nuevos bajo `Release` (Roslyn analyzers + `TreatWarningsAsErrors` ya activos vía `Directory.Build.props`).
12. **Validación manual de extremo a extremo (obligatoria, no sustituible por los tests automáticos)**: ejecutar `dotnet run --project src/ClaudeMeter.Console` con un `.credentials.json` real y comprobar visualmente, durante al menos 2-3 iteraciones (~2-3 minutos), que: (a) la primera línea aparece de inmediato sin esperar 60s; (b) las siguientes líneas aparecen cada ~60s; (c) si se renombra temporalmente `.credentials.json` durante la ejecución, la siguiente iteración muestra el error de "Token no disponible" sin que el proceso termine, y al restaurar el fichero la iteración siguiente vuelve a mostrar éxito con normalidad.

## Risks & Open Decisions

**No quedan decisiones de arquitectura abiertas que requieran sign-off humano antes de desarrollo.** Todas las decisiones que este documento tenía margen para resolver (ninguna estaba ya fijada salvo la ubicación del proyecto, que viene del propio documento de requisitos) se han resuelto explícitamente aquí, con su rationale y alternativa rechazada documentados en la sección correspondiente:

1. **Colisión de nombres `Console` (namespace) vs. `System.Console` (tipo)** — **Resuelta**: `RootNamespace = ClaudeMeter.ConsoleApp`, proyecto/ensamblado se mantiene `ClaudeMeter.Console` tal como exige el documento de requisitos. Ver "Por qué el namespace del proyecto NO es literalmente `ClaudeMeter.Console`".
2. **Reparto éxito/error entre `Console.Out`/`Console.Error`** — **Resuelta**: éxito por `stdout`, las cuatro variantes de error por `stderr`, apoyándose en que `CLAUDE.md` ya menciona ambos mecanismos como válidos para F0.
3. **Necesidad de una interfaz `IUsagePollingRenderer`** — **Resuelta: sí, exclusivamente como costura de test** (una sola implementación real), para poder testear la lógica de despacho de `UsagePollingLoop` sin capturar streams de consola reales.
4. **Alcance de los tests automáticos de un proyecto explícitamente "desechable"** — **Resuelta**: se testea el formateo puro (`UsagePollingLineFormatter`) y la lógica de despacho por estado (`UsagePollingLoop.ExecuteIterationAsync`, vía `internal` + `InternalsVisibleTo`, mismo patrón ya usado por `CredentialsFileTokenProvider`); se excluye deliberadamente de los tests automáticos `RunAsync` (bucle infinito con espera real de 60s — no aporta señal adicional sobre `ExecuteIterationAsync` ya testeado, solo tiempo de ejecución) y `Program.cs` (composition root trivial, sin lógica propia) — ambos quedan cubiertos únicamente por la validación manual del paso 12 del Implementation Plan, igual que ocurrirá previsiblemente con el futuro composition root de `ClaudeMeter.Desktop` en F1.
5. **Ciclo de vida y configuración de `HttpClient`** — **Resuelta**: instancia única compartida durante toda la vida del proceso, sin configuración adicional (timeout por defecto de 100s, sin `IHttpClientFactory`, sin reintentos — éstos últimos son explícitamente F2).
6. **Mecanismo de wiring de `.csproj`/`.sln`** — **Resuelta**: generar con `dotnet new`/`dotnet sln add`/`dotnet add reference` en vez de editar el `.sln` a mano, evitando GUIDs inválidos o mal insertados en las secciones `ProjectConfigurationPlatforms`/`NestedProjects`.

**Riesgos operativos aceptados (no bloquean el diseño ni el desarrollo; heredados o inherentes a este issue, no items pendientes de decisión)**:

- **Coste de quota real**: `AnthropicApiUsageDataSource` (sin cambios) realiza una llamada real `POST /v1/messages` con `max_tokens: 1` en cada iteración exitosa — este riesgo ya estaba documentado en el diseño del Issue #3, pero este issue es el primero que lo ejecuta de forma continua y prolongada (antes solo se ejercitaba en tests con HTTP simulado). Ejecutar el harness durante horas consume una pequeña cantidad de quota real de la cuenta cada 60s; se acepta explícitamente para F0 (es justamente el propósito del harness: validar contra la API real), pero quien lo ejecute debe ser consciente de que no es gratis ni instantáneo de detener (no hay apagado controlado, solo matar el proceso).
- **Cadencia no estrictamente periódica bajo red lenta**: como el `Task.Delay(60s)` corre *después* de que la llamada HTTP complete (incluyendo su posible timeout por defecto de 100s de `HttpClient`), una iteración excepcionalmente lenta estira temporalmente el ciclo total por encima de 60s en vez de solaparse con la siguiente. Se acepta como comportamiento correcto (no hay ningún Acceptance Criteria que exija precisión de reloj de pared), y es preferible a la alternativa (`PeriodicTimer`) que sí podría solapar iteraciones.
- **Formato de `anthropic-ratelimit-unified-*` no documentado oficialmente** (heredado de Issues #3/#4): cualquier cambio de formato futuro por parte de Anthropic se degradaría de forma segura a "no disponible" en la línea de consola (vía `RateLimitWindow.PercentageUsed`/`MinutesRemaining` en `null`), sin excepción — comportamiento ya garantizado por `RateLimitWindowParser`, sin necesidad de ningún cambio en este issue.

## Summary

Se añade un proyecto ejecutable nuevo, `src/ClaudeMeter.Console` (namespace de código `ClaudeMeter.ConsoleApp`, para evitar la colisión real con `System.Console`), con cuatro piezas: `Program` (composition root), `UsagePollingLoop` (orquestador del `while(true)` + `Task.Delay(60s)`, con manejo de errores por iteración vía `ExecuteIterationAsync`), y `UsagePollingLineFormatter`/`ConsoleUsagePollingRenderer` (formateo puro + I/O de consola, separados entre sí y de la orquestación, cumpliendo la regla de `CLAUDE.md` de mantener lectura de token, llamada a la API, cálculo de countdown y renderizado en clases distintas). El bucle consume exclusivamente los puertos ya existentes de `ClaudeMeter.Application`/`Infrastructure`/`Domain` (`IUsageDataSource`, `RateLimitWindowParser`) sin modificar ninguno de los tres. Se añade también `ClaudeMeter.Console.Tests`, cubriendo el formateo puro y la lógica de despacho por `UsageSnapshotStatus` mediante un doble de test, dejando fuera de la cobertura automática (por diseño, y documentado explícitamente) el bucle infinito real y el composition root trivial, validados en su lugar manualmente. No quedan decisiones de arquitectura abiertas: todas las resueltas en esta fase están listadas en "Risks & Open Decisions" junto con su rationale.

Próximo paso sugerido: `sdlc-development` con este documento (`docs/sdlc/design/issue-05-bucle-polling-consola.md`) como entrada.
