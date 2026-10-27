# Design: F6 — Distribución

## Requirements Reference

`docs/sdlc/requirements/f6-distribucion.md` — milestone **F6 — Distribución**
(GitHub milestone #7), issues #27 (US-1, versionado), #25 (US-2, empaquetado)
y #26 (US-3, autoarranque + auto-actualización), encadenadas
US-1 → US-2 → US-3.

## Architecture Overview

F6 es trabajo de build/CI/empaquetado, no de dominio de negocio. Tras
inspeccionar `ClaudeMeter.sln`, `Directory.Build.props`, los 4 `.csproj` de
`src/` y `App.xaml.cs`/`TrayIconService.cs`/`AppConfigStore.cs` de F2-F3, se
confirma la premisa del documento de requisitos: **ninguna de las tres
historias toca `ClaudeMeter.Domain` ni `ClaudeMeter.Application`**. Los
puertos de Application (`ITokenProvider`, `IUsageDataSource`) ya cubren la
única frontera externa del dominio (la API de Anthropic); versionado,
empaquetado y auto-arranque/auto-actualización son preocupaciones del
proceso de build y del ciclo de vida del *host* de escritorio, exactamente
del mismo tipo que `ClickThroughService`, `WindowCloseService` o
`TrayIconService` (F3) — colaboradores planos de `ClaudeMeter.Desktop`, sin
envoltura MediatR ni puerto de Application propio. Se documenta esto también
como una decisión explícita (ver Technology Choices, "Sin puerto
`IUpdateService`/`IAutoStartService` en Application") porque el enunciado de
la tarea pide confirmarlo o corregirlo con criterio propio: se confirma, no
se corrige.

Dónde vive cada pieza nueva:

```
<raíz del repo>
├── version.json                          ← NUEVO (US-1)
├── Directory.Build.props                 ← MODIFICADO (US-1: PackageReference Nerdbank.GitVersioning)
├── .github/workflows/
│   ├── ci.yml                            ← NUEVO (build→test en cada push/PR)
│   └── release.yml                       ← NUEVO (empaquetado Velopack solo en tags v*, US-2)
└── src/ClaudeMeter.Desktop/
    ├── Program.cs                        ← NUEVO (Main explícito; primera línea VelopackApp...Run(), US-2/US-3)
    ├── App.xaml                          ← MODIFICADO (Build Action ApplicationDefinition → Page)
    ├── App.xaml.cs                       ← MODIFICADO (composition root: registra AutoStartService/AppUpdateService)
    ├── ClaudeMeter.Desktop.csproj        ← MODIFICADO (PackageReference Velopack; StartupObject)
    ├── Configuration/AppConfig.cs        ← MODIFICADO (nuevo campo AutoStartEnabled)
    ├── Configuration/AppConfigStore.cs   ← MODIFICADO (persistir/leer autoStartEnabled)
    ├── Startup/AutoStartService.cs       ← NUEVO (US-3, registro HKCU\...\Run)
    ├── Updates/AppUpdateService.cs       ← NUEVO (US-3, UpdateManager de Velopack)
    └── Tray/TrayIconService.cs           ← MODIFICADO (nuevo ítem "Iniciar con Windows")
```

`ClaudeMeter.Infrastructure` no cambia: no hay ningún adaptador nuevo que
implemente un puerto de Application. `ClaudeMeter.Console` (el arnés F0)
también queda intacto salvo por heredar el versionado automático vía
`Directory.Build.props` (efecto colateral inevitable y deseable de US-1 —
un único ensamblado sin versión sería inconsistente).

### Encadenamiento de las 3 historias

- **US-1** produce el número de versión (`version.json` + Nerdbank.GitVersioning
  integrado en el build de MSBuild). No depende de nada nuevo.
- **US-2** consume ese número (`vpk pack --packVersion <version calculada>`)
  para generar el instalador, y añade el job de CI de empaquetado. Depende de
  US-1 para tener algo que empaquetar con una versión real.
- **US-3** depende del canal de releases de GitHub que US-2 deja funcionando
  (`UpdateManager` + `GithubSource` apuntan al mismo repositorio donde US-2
  publica) y añade autoarranque (mecanismo de Windows, no de Velopack) más
  el flujo `CheckForUpdatesAsync`/`DownloadUpdatesAsync`/
  `ApplyUpdatesAndRestart` de Velopack.

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| **Nerdbank.GitVersioning**, serie `3.10.x` (última estable verificada en `api.nuget.org` al escribir este documento: **3.10.85** — [nuget.org/packages/Nerdbank.GitVersioning](https://www.nuget.org/packages/Nerdbank.GitVersioning)). Instalación vía herramienta global `nbgv` (`dotnet tool install -g nbgv` → `nbgv install` desde la raíz del repo), tal y como recomienda la [documentación oficial](https://dotnet.github.io/Nerdbank.GitVersioning/docs/getting-started.html). | Decisión ya fijada por el documento de requisitos (US-1); es el estándar de facto en el ecosistema .NET OSS para versionado basado en altura de git, sin intervención manual. Siguiendo el mismo criterio ya aplicado en los documentos de diseño de F1/F2 de este repo (ver `f2-robustez-ciclo-a.md`, sección "Decisiones... confirmadas"), **este documento fija el nombre del paquete y la serie compatible, no el patch exacto** — Development confirma el último `3.10.x` real en `api.nuget.org` en el momento de implementar; es una comprobación mecánica de packaging, no una decisión de arquitectura. | GitVersion (otro versionador basado en git) — descartado: más pesado de configurar (YAML de convenciones de ramas), y NBGV es el que las propias issues F6 nombran explícitamente. |
| `version.json` en la **raíz del repositorio** (mismo nivel que `ClaudeMeter.sln` y `Directory.Build.props`), generado por `nbgv install`. Semilla `"version": "1.0"`, `"publicReleaseRefSpec": ["^refs/heads/master$", "^refs/tags/v\\d+\\.\\d+"]`. | El nombre del fichero en minúsculas es obligatorio ([version.json file](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html)). Semilla `1.0` (no `0.1`): la propia AC de US-2 usa `v1.0.0` como ejemplo de primer tag de release, y F6 es literalmente lo que convierte el widget en algo instalable/distribuible por primera vez — encaja mejor como "1.0" que como un `0.x` interno. `publicReleaseRefSpec` incluye `master` y tags `v*` para que un build en una rama de feature (como `features/f6`) quede marcado como no-oficial (AC "build dirty/local"), mientras que un tag `v1.0.0` produce la versión pública exacta que consume US-2. | Semilla `0.1.0`: rechazada por la razón de arriba (F6 marca el primer release real, no un hito interno más). |
| `PackageReference` de Nerdbank.GitVersioning centralizado en **`Directory.Build.props`** (no repetido en cada `.csproj`), con `PrivateAssets="all"` (es un generador de versión en tiempo de build, no una dependencia de runtime). | Sigue el patrón ya establecido por este mismo fichero (TFM, Nullable, analizadores ya centralizados ahí para los 4 proyectos de `src/`) — evita duplicar la referencia 4 veces. `nbgv install` puede insertarla en cada `.csproj` individual por defecto: si lo hace así, Development debe consolidarla manualmente en `Directory.Build.props` tras ejecutar la herramienta, para mantener la consistencia ya existente en el repo. | Dejar que `nbgv install` la reparta por proyecto tal cual — rechazada por duplicación innecesaria dado que ya existe un `Directory.Build.props` compartido. |
| **Velopack** (paquete NuGet `Velopack`, última estable verificada al escribir este documento: **1.2.158** — [nuget.org/packages/velopack](https://www.nuget.org/packages/velopack)) + CLI global `vpk` (paquete `vpk` en NuGet, herramienta .NET global: `dotnet tool install -g vpk`). | Decisión de producto ya cerrada por el usuario (ver requisitos, Dependencies) — no se evalúa MSIX. Verificado contra el sitio oficial y GitHub: soporta empaquetado + delta updates + publicación/descarga directa contra GitHub Releases sin infraestructura adicional (`vpk upload github` / `vpk download github`, [docs.velopack.io/distributing/github-actions](https://docs.velopack.io/distributing/github-actions)), encajando con el flujo de "instalador como asset de la release" exigido por la AC de US-2. | MSIX — explícitamente fuera de alcance por instrucción del usuario, no se evalúa. |
| `dotnet publish` **self-contained**, `-r win-x64`, para el paquete que empaqueta `vpk` (no framework-dependent). | La AC de US-2 exige que el instalador funcione en "una máquina Windows limpia... sin SDK de .NET ni ninguna otra herramienta de desarrollo instalada" — un publish framework-dependent exigiría que el .NET 8 Desktop Runtime esté ya presente o se instale aparte, lo que no está garantizado en una máquina limpia. Self-contained embebe el runtime en el propio paquete, satisfaciendo la AC sin depender de un segundo instalador de prerrequisitos para el runtime de .NET (el único prerrequisito externo real que queda es el WebView2 Runtime, ver fila siguiente). | Framework-dependent + bootstrapper del .NET Desktop Runtime como prerrequisito de Velopack — rechazada: añade una segunda descarga/instalación silenciosa gestionada aparte, más superficie de fallo, por un ahorro de tamaño de instalador que no es un requisito de este documento. |
| **Bundle del WebView2 Evergreen Bootstrapper** (~2MB, descarga bajo demanda el runtime real desde servidores de Microsoft) como prerrequisito de instalación, en vez de asumir que el WebView2 Runtime ya está presente. | Verificado contra la documentación oficial de Microsoft Edge ([Distribute your app and the WebView2 Runtime](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)): el runtime Evergreen viene preinstalado en Windows 11 "en dispositivos elegibles", pero Microsoft recomienda explícitamente distribuir igualmente el bootstrapper "para cubrir casos límite donde el runtime no esté preinstalado" (p. ej. Windows 10, o una imagen de Windows 11 recortada) — justo el escenario "máquina limpia" que exige la AC de US-2. Riesgo nuevo detectado por este documento de diseño que el de requisitos no cubría explícitamente (ver Risks). | Confiar en que WebView2 ya está presente (comportamiento por defecto sin bundle) — rechazada: no está garantizado fuera de Windows 11 "elegible", y el fallo resultante (BlazorWebView no arranca) sería mucho más opaco para el usuario que una descarga silenciosa de 2MB gestionada por el propio instalador. |
| Autoarranque vía **clave de registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`**, escrita/borrada a mano desde `AutoStartService` (Desktop), resolviendo la ruta estable del ejecutable a través de la API de localización de Velopack (`Velopack.Locators.IVelopackLocator`, propiedades `RootAppDir`/`AppId` — [docs.velopack.io/reference/cs/Velopack.Locators/IVelopackLocator](https://docs.velopack.io/reference/cs/Velopack.Locators/IVelopackLocator)), **no** `Environment.ProcessPath`. | Ver rationale completo en Cross-Cutting Concerns / Data Model. Resumen: la app en ejecución normal corre como `{RootAppDir}\current\ClaudeMeter.Desktop.exe` (ruta que Velopack reemplaza en cada actualización), mientras que el lanzador estable que sobrevive a las actualizaciones es el stub `{RootAppDir}\{AppId}.exe` en la raíz de instalación ([Windows Overview — Velopack](https://docs.velopack.io/packaging/operating-systems/windows)). `Environment.ProcessPath` devolvería la ruta *inestable* de `current\`. El accesor exacto para obtener el `IVelopackLocator` de la instancia en ejecución (método estático o propiedad de `VelopackApp`/`UpdateManager`) no se fija aquí con literalidad de API — Development lo confirma contra `docs.velopack.io/reference/cs/Velopack.Locators` al implementar; lo que sí es una decisión de arquitectura ya cerrada por este documento es **de dónde sale la ruta** (el locator de Velopack, nunca `Environment.ProcessPath` ni una ruta hardcodeada). | **`Velopack.Windows.Shortcuts.CreateShortcutForThisExe(ShortcutLocation.Startup)`** (helper nativo de Velopack para acceso directo en la carpeta de inicio) — investigada y **rechazada**: la propia documentación oficial ([Windows Shortcuts — Velopack](https://docs.velopack.io/integrating/shortcuts)) advierte que esa clase "se proporciona por razones heredadas (legacy), y en general no se garantiza la estabilidad de esas funciones", y su patrón de uso documentado es un hook de instalación (`OnAfterInstallFastCallback`, ejecutado una vez al instalar para todos los usuarios), no una preferencia de usuario que se activa/desactiva en cualquier momento desde el menú de bandeja — mal encaje para la AC de US-3 ("el usuario activa/desactiva la opción... desde el menú de bandeja"). El registro es además el mecanismo estándar que usan apps comparables con un checkbox de "iniciar con Windows" (Slack, Discord, Spotify). |
| Estado de autoarranque persistido en **`config.json`** (`AppConfig.AutoStartEnabled`, mismo patrón que `ChimeEnabled`), aplicado (de forma idempotente) al registro en cada arranque de `App.OnStartup`, no solo al hacer toggle. | Resuelve dos cosas a la vez: (1) cumple la AC "tras una actualización, se conserva el estado de la opción de autoarranque" reutilizando la ubicación de `config.json` ya fuera del árbol `current\` que Velopack sustituye en cada update (`%LOCALAPPDATA%\ClaudeMeter\config.json`, ver [Preserving Files — Velopack](https://docs.velopack.io/integrating/preserved-files): "durante las actualizaciones, todo el directorio `current` se reemplaza; guarda configuración/logs fuera de ahí" — exactamente el patrón que F2 ya adoptó sin saberlo); (2) permite "auto-reparar" la clave de registro en cada arranque por si la ruta del stub necesitase reconfirmarse, sin coste (operación idempotente). | Usar el registro como única fuente de verdad, sin espejo en `config.json` — rechazada: aunque el registro por sí solo ya sobrevive a las actualizaciones de Velopack (es una clave de usuario independiente del árbol de instalación), no habría forma barata de re-sincronizar/auto-reparar la ruta del stub tras una actualización sin releer la intención del usuario desde algún sitio; además el enunciado de esta tarea pide explícitamente extender el modelo de `config.json` de F2 para este toggle. |
| `UpdateManager` de Velopack + `GithubSource` apuntando al repo (`new UpdateManager(new GithubSource("https://github.com/AlejBlasco/claude-usage-widget", null, false))`), comprobación al arrancar + timer periódico de baja frecuencia (**cada 4 horas**, `System.Timers.Timer`, mismo tipo ya usado por `UsagePollingCoordinator`/F1). | La API exacta (`GithubSource(repoUrl, accessToken, prerelease)`) está verificada contra [Update Sources — Velopack](https://docs.velopack.io/integrating/update-sources). 4h es una decisión de implementación ya delegada a Design por el propio documento de requisitos ("ninguna de las issues originales especifica cada cuánto"); 4h evita tanto dejar una sesión larga sin actualizar como generar tráfico innecesario contra GitHub Releases (repositorio público, sin rate limit de token). `SetAutoApplyOnStartup` (activado por defecto en Velopack, [referencia oficial](https://docs.velopack.io/reference/cs/Velopack/VelopackApp/methods/SetAutoApplyOnStartup)) se deja tal cual — aplica en el siguiente arranque cualquier actualización ya descargada pero no aplicada, sin lógica adicional. | Comprobar solo al arrancar (sin timer periódico) — rechazada: el propio widget está pensado para quedarse abierto horas/días (es un widget de escritorio, no una app que se cierra y reabre), así que solo comprobar al arrancar dejaría sesiones largas sin actualizar potencialmente durante días. |
| `Program.cs` explícito con `[STAThread] static void Main()`, `App.xaml` con Build Action `Page` (no `ApplicationDefinition`) y `<StartupObject>ClaudeMeter.Desktop.App</StartupObject>` en el `.csproj`. `VelopackApp.Build().Run()` como primera línea de `Main()`, antes de construir `App`. | Técnica oficial documentada por Velopack para WPF ([Integrating Overview](https://docs.velopack.io/integrating/overview)): WPF genera su propio `Main()` a partir de `App.xaml` cuando este tiene Build Action `ApplicationDefinition`, sin dar acceso a insertar código antes de `InitializeComponent()`; cambiar el Build Action a `Page` desactiva esa generación automática y permite escribir un `Main()` propio, requisito estricto de la AC ("como primera línea de Main()"). | Invocar `VelopackApp.Build().Run()` dentro de `App.OnStartup` (como hacen hoy Serilog/DI) — rechazada explícitamente por la propia documentación de Velopack, que advierte que ese orden añade *overhead* de WPF innecesario a la ruta de aplicar una actualización (WPF/BlazorWebView llegan a inicializarse en el peor caso, cuando `Run()` va a terminar el proceso de inmediato para reiniciar tras aplicar la actualización). |
| Sin puerto `IUpdateService`/`IAutoStartService` en `ClaudeMeter.Application`. `AutoStartService`/`AppUpdateService` son clases planas en `ClaudeMeter.Desktop`, mismo nivel que `ClickThroughService`/`TrayIconService`/`PollingControlService`. | Los puertos de Application existentes (`ITokenProvider`, `IUsageDataSource`) existen porque tienen — o está previsto que tengan (`BleUsageSink` en F7) — múltiples implementaciones intercambiables y forman parte del caso de uso de negocio (obtener consumo de Claude Code). Autoarranque/auto-actualización no tienen ninguna implementación alternativa plausible (no hay un "auto-arranque simulado" ni un segundo instalador que sustituir) y son puramente ciclo de vida del host de escritorio — introducir un puerto aquí sería ceremonia sin beneficio, mismo criterio ya aplicado en el diseño de F1 para rechazar MediatR prematuro. | Puerto `IUpdateChecker` en Application, implementado por un adaptador `VelopackUpdateChecker` en Infrastructure — rechazada por la razón de arriba; además obligaría a Infrastructure (hoy sin ninguna dependencia de UI/WPF) a exponer un flujo que en la práctica solo tiene sentido siendo orquestado desde el composition root de Desktop (`VelopackApp.Run()` en `Main()`). |
| GitHub Actions oficiales (`actions/checkout`, `actions/setup-dotnet`) para el pipeline de CI/CD — **versión mayor exacta sin fijar en este documento**. | Mismo criterio que las versiones de paquete NuGet: son detalles mecánicos de tooling de CI, no decisiones de arquitectura. Los resultados de búsqueda al escribir este documento son inconsistentes entre sí sobre cuál es la última mayor de `actions/setup-dotnet` (v5/v6 según la fuente) — Development/quien implemente el workflow debe fijar la última mayor estable consultando directamente `github.com/actions/checkout/releases` y `github.com/actions/setup-dotnet/releases` en el momento de crear el fichero YAML. | Fijar una versión mayor concreta ahora mismo — rechazada por rule 4: preferible dejarlo como comprobación mecánica explícita a que un número posiblemente ya obsoleto se cuele en el YAML generado. |

## Data Model / Interfaces

### `version.json` (nuevo, raíz del repo)

```json
{
  "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json",
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/heads/master$",
    "^refs/tags/v\\d+\\.\\d+"
  ],
  "cloudBuild": {
    "setVersionVariables": true
  }
}
```

Development confirma el esquema exacto contra
[version.json file (dotnet.github.io)](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html)
al ejecutar `nbgv install` (la herramienta genera el fichero correcto por sí
sola; lo de arriba es la intención a validar, no una plantilla a copiar
literalmente si `nbgv install` produce algo distinto).

### `Directory.Build.props` (extensión, US-1)

```xml
<ItemGroup>
  <!-- US-1 (F6): versionado automático. Serie 3.10.x — Development confirma
       el último patch estable en api.nuget.org al implementar (ver
       Technology Choices). PrivateAssets="all": es un generador de versión
       en build-time, no una dependencia de runtime del widget. -->
  <PackageReference Include="Nerdbank.GitVersioning" Version="3.10.85" PrivateAssets="all" />
</ItemGroup>
```

### `AppConfig` (extensión, US-3)

```csharp
// src/ClaudeMeter.Desktop/Configuration/AppConfig.cs
public sealed record AppConfig(
    TimeSpan PollingInterval,
    WindowPosition? Position,
    bool ChimeEnabled,
    AppTheme Theme = AppTheme.Dark,
    bool AutoStartEnabled = false) // NUEVO (US-3) — opt-in, igual criterio que ChimeEnabled
{
    public static AppConfig Default { get; } =
        new(TimeSpan.FromSeconds(60), Position: null, ChimeEnabled: false, Theme: AppTheme.Dark, AutoStartEnabled: false);
}
```

`AppConfigStore.AppConfigDto` gana un campo `bool? AutoStartEnabled` con
`[JsonPropertyName("autoStartEnabled")]`, resuelto en `Load()` con el mismo
patrón sin validación adicional que `ChimeEnabled` (`dto.AutoStartEnabled ??
AppConfig.Default.AutoStartEnabled`) y serializado en `Save()` igual que los
demás campos booleanos. No requiere una función `Resolve*` propia porque,
igual que `ChimeEnabled`, un `bool` no tiene un "rango inválido" que
detectar.

### `AutoStartService` (nuevo, `Desktop/Startup/AutoStartService.cs`)

```csharp
namespace ClaudeMeter.Desktop.Startup;

/// <summary>
/// Registra/retira ClaudeMeter de HKCU\...\Run (US-3, issue #26). La ruta
/// escrita se resuelve vía el locator de Velopack (RootAppDir + AppId), NO
/// Environment.ProcessPath -- ver rationale en el documento de diseño
/// (Technology Choices): el proceso en ejecución normal corre desde
/// "current\", que Velopack reemplaza en cada actualización; el stub
/// "{RootAppDir}\{AppId}.exe" es la única ruta estable entre versiones.
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ClaudeMeter";

    private readonly AppConfigStore _configStore;
    private readonly ILogger<AutoStartService> _logger;

    public AutoStartService(AppConfig initialConfig, AppConfigStore configStore, ILogger<AutoStartService> logger)
    {
        _configStore = configStore;
        _logger = logger;
        IsEnabled = initialConfig.AutoStartEnabled;
    }

    public bool IsEnabled { get; private set; }

    /// <summary>Mismo patrón que ClickThroughService.StateChanged -- TrayIconService lo usa para sincronizar el Checked del ítem de menú.</summary>
    public event Action? StateChanged;

    /// <summary>Llamado una única vez desde App.OnStartup: re-aplica (idempotente) el estado ya persistido en config.json al registro.</summary>
    public void Initialize()
    {
        if (IsEnabled)
        {
            ApplyToRegistry(enabled: true);
        }
    }

    public void Toggle() => SetEnabled(!IsEnabled);

    public void SetEnabled(bool enabled)
    {
        ApplyToRegistry(enabled);
        IsEnabled = enabled;
        _configStore.Save(_configStore.Load() with { AutoStartEnabled = enabled });
        StateChanged?.Invoke();
    }

    private void ApplyToRegistry(bool enabled)
    {
        try
        {
            var exePath = ResolveStableExePath(); // IVelopackLocator.RootAppDir + AppId -- API exacta a confirmar (ver Technology Choices)
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                key.SetValue(RunValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _logger.LogError(ex, "No se pudo actualizar la entrada de autoarranque en el registro");
        }
    }

    // internal + InternalsVisibleTo para xUnit, mismo patrón que AutoStartServiceTests simulando SetEnabled sin registro real.
}
```

### `AppUpdateService` (nuevo, `Desktop/Updates/AppUpdateService.cs`)

```csharp
namespace ClaudeMeter.Desktop.Updates;

/// <summary>
/// Envuelve Velopack.UpdateManager (US-3, issue #26). Nombrada
/// "AppUpdateService", no "UpdateManager", para no colisionar por lectura
/// con Velopack.UpdateManager (mismo espacio de soluciones, aunque
/// namespaces distintos evitarían el choque en compilación -- se evita la
/// ambigüedad de todos modos). Comprobación al arrancar + timer periódico
/// de baja frecuencia; degradación silenciosa ante cualquier fallo, mismo
/// criterio que RetryingUsageDataSource (F2) y la propia AC de US-3.
/// </summary>
public sealed class AppUpdateService : IDisposable
{
    private static readonly TimeSpan PeriodicCheckInterval = TimeSpan.FromHours(4);

    private readonly Velopack.UpdateManager _updateManager;
    private readonly ILogger<AppUpdateService> _logger;
    private System.Timers.Timer? _timer;

    public AppUpdateService(ILogger<AppUpdateService> logger)
    {
        _logger = logger;
        _updateManager = new Velopack.UpdateManager(
            new Velopack.Sources.GithubSource("https://github.com/AlejBlasco/claude-usage-widget", null, false));
    }

    /// <summary>Llamado una vez desde App.OnStartup, tras VelopackApp.Run().</summary>
    public void Start()
    {
        _ = CheckAndApplyAsync();

        _timer = new System.Timers.Timer(PeriodicCheckInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await CheckAndApplyAsync();
        _timer.Start();
    }

    private async Task CheckAndApplyAsync()
    {
        try
        {
            // IsInstalled evita comprobar actualizaciones cuando se ejecuta
            // sin instalar vía Velopack (p. ej. F5 -- dotnet run local) --
            // nombre exacto de la propiedad a confirmar contra
            // docs.velopack.io/reference/cs/Velopack/UpdateManager al implementar.
            if (!_updateManager.IsInstalled)
            {
                return;
            }

            var info = await _updateManager.CheckForUpdatesAsync();
            if (info is null)
            {
                return; // sin actualización disponible
            }

            await _updateManager.DownloadUpdatesAsync(info);
            _updateManager.ApplyUpdatesAndRestart(info); // Velopack gestiona el reinicio del proceso
        }
        catch (Exception ex)
        {
            // Catch-all deliberado (desviación justificada del criterio de
            // excepciones específicas usado en el resto del repo, p. ej.
            // AppConfigStore/RetryingUsageDataSource): la AC de US-3 exige
            // degradación silenciosa ante *cualquier* fallo de red/feed
            // inaccesible, y la superficie de excepciones de un pipeline de
            // descarga+aplicación de Velopack no es enumerable de forma
            // estable entre versiones del paquete.
            _logger.LogWarning(ex, "No se pudo comprobar o aplicar actualizaciones; se continúa con la versión actual");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
```

### `Program.cs` (nuevo, entry point real)

```csharp
namespace ClaudeMeter.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // US-2/US-3: primera línea, antes de cualquier inicialización de
        // WPF/Serilog/DI -- exigencia estricta de Velopack (ver Technology
        // Choices). Con SetAutoApplyOnStartup por defecto (activado),
        // aplica automáticamente cualquier actualización ya descargada.
        Velopack.VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```

`App.xaml`: Build Action pasa de `ApplicationDefinition` a `Page` (elimina
el `Main()` autogenerado). `ClaudeMeter.Desktop.csproj` gana
`<StartupObject>ClaudeMeter.Desktop.Program</StartupObject>` y el
`PackageReference` a `Velopack`.

### `TrayIconService` (extensión, US-3)

Nuevo `ToolStripMenuItem` `_autoStartItem` ("Iniciar con Windows"), mismo
patrón exacto que `_clickThroughItem` (`CheckOnClick = false`, toggle manual
+ suscripción a `StateChanged` para sincronizar `Checked`), insertado en el
menú entre `_clickThroughItem` y el separador. `TrayIconService.Initialize()`
sincroniza el `Checked` inicial leyendo `_autoStart.IsEnabled` (a diferencia
de pausa/click-through, que siempre arrancan en `false`: el autoarranque sí
debe reflejar el estado persistido desde el primer render del menú).

### `App.xaml.cs` (composition root, extensión)

```csharp
// tras los registros ya existentes de F2/F3:
services.AddSingleton<Startup.AutoStartService>();
services.AddSingleton<Updates.AppUpdateService>();
...
Services = services.BuildServiceProvider();

Services.GetRequiredService<TrayIconService>().Initialize();
Services.GetRequiredService<Startup.AutoStartService>().Initialize(); // US-3
Services.GetRequiredService<Updates.AppUpdateService>().Start();      // US-3
```

`OnExit` gana `Services.GetRequiredService<Updates.AppUpdateService>().Dispose();`
junto al `Dispose()` ya existente de `TrayIconService`.

## Diseño del pipeline de CI/CD

Hoy no existe ningún workflow de build (`CLAUDE.md` lo marca como
"planeado"; el único workflow existente, `close-milestone.yml`, es
administrativo). Se diseñan **dos workflows nuevos**, ambos en
`windows-latest` (obligatorio: `ClaudeMeter.Desktop`/`ClaudeMeter.Desktop.Tests`
son `net8.0-windows` con `UseWPF`, no compilan en runners Linux/macOS).

### `.github/workflows/ci.yml` — build→test en cada push

- **Trigger:** `push` (todas las ramas) y `pull_request` contra `master`.
- **Job único `build-and-test`:**
  1. `actions/checkout` con `fetch-depth: 0` — **obligatorio**, no opcional:
     Nerdbank.GitVersioning calcula la versión a partir de la "altura" de
     commits desde el tag/rama de referencia, y un checkout superficial
     (`fetch-depth: 1`, el valor por defecto de la acción) le impide ver esa
     historia, rompiendo el cálculo de versión silenciosamente.
  2. `actions/setup-dotnet` con el SDK de .NET 8 (versión exacta de la
     acción a confirmar al implementar, ver Technology Choices).
  3. `dotnet restore ClaudeMeter.sln`
  4. `dotnet build ClaudeMeter.sln -c Release --no-restore` — los
     analizadores Roslyn con warnings-as-errors en Release (ya configurado
     en `Directory.Build.props`) hacen que este paso falle solo si hay
     warnings, sin necesidad de un paso de "analizadores" separado.
  5. `dotnet test ClaudeMeter.sln -c Release --no-build` — xUnit
     (Domain/Application/Infrastructure) + bUnit (Desktop), exactamente como
     describe `CLAUDE.md`.

Esto por sí solo ya satisface la AC de US-2 "en un push que no es tag, solo
se ejecuta restore→build→tests→analizadores" — el job de empaquetado vive en
un workflow/trigger completamente distinto (abajo), así que nunca se dispara
por un push normal.

### `.github/workflows/release.yml` — empaquetado en tags `v*`

- **Trigger:** `push` con `tags: ['v*']` (p. ej. `v1.0.0`, coincide con el
  ejemplo de la AC de US-2).
- **Job `package`** (`needs` no aplica — es un workflow distinto que corre
  en paralelo/independiente; si se prefiere encadenarlo al resultado de
  `ci.yml` sobre el mismo commit, usar `workflow_run` con `conclusion ==
  'success'` en vez de duplicar build+test aquí — decisión de detalle de
  implementación, no de arquitectura):
  1. `actions/checkout` con `fetch-depth: 0` (mismo motivo que en `ci.yml`).
  2. `actions/setup-dotnet` (.NET 8 SDK).
  3. `dotnet tool install --global nbgv` y `dotnet tool install --global vpk`.
  4. `dotnet build ClaudeMeter.sln -c Release` — si falla (p. ej. por
     warnings-as-errors), el job se detiene aquí y **ningún paso posterior
     se ejecuta**, cumpliendo la AC "no se publica ningún instalador
     incompleto o roto" por construcción (comportamiento por defecto de
     GitHub Actions: un step fallido detiene el job).
  5. `dotnet test ClaudeMeter.sln -c Release --no-build` — repetir la
     batería de tests también en el commit del tag, no asumir que el tag
     apunta a un commit que ya pasó `ci.yml`.
  6. `dotnet publish src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj -c Release -r win-x64 --self-contained true -o publish/`.
  7. Consultar la versión calculada por NBGV para este commit/tag (p. ej.
     `nbgv get-version`; el nombre exacto de la opción/campo para extraer
     solo el número de versión se confirma contra la documentación de la
     CLI de `nbgv` al implementar) y capturarla en una variable de entorno
     del job.
  8. `vpk pack --packId ClaudeMeter --packVersion <versión de NBGV> --packDir publish --mainExe ClaudeMeter.Desktop.exe --packTitle "ClaudeMeter" --outputDir Releases`
     (nombres de flag verificados contra
     [vpk (Windows) — Velopack](https://docs.velopack.io/reference/cli/content/vpk-windows);
     confirmar el listado completo con `vpk pack -h` al implementar, por si
     hay flags adicionales relevantes como bundling del bootstrapper de
     WebView2).
  9. `vpk upload github --repoUrl https://github.com/AlejBlasco/claude-usage-widget --token ${{ secrets.GITHUB_TOKEN }} --publish --tag ${{ github.ref_name }}`
     — publica el instalador directamente como asset de la release de
     GitHub correspondiente al tag empujado (AC de US-2), usando el
     `GITHUB_TOKEN` que la propia Action ya provee por defecto (con permiso
     `contents: write` explícito en el workflow), sin necesidad de un PAT
     adicional en secrets.

Este diseño cumple las 5 AC de US-2: versión embebida sin intervención
manual (paso 7-8), funciona en máquina limpia (self-contained + WebView2
bootstrap, ver Technology Choices), queda publicado como asset de la
release (paso 9), no se dispara en pushes normales (trigger `tags: v*`
frente al `push`/`pull_request` de `ci.yml`), y no publica nada roto (fallo
temprano en build/test detiene el job antes de llegar a `vpk upload`).

## Cross-Cutting Concerns

- **Seguridad:** el instalador no está firmado (fuera de alcance, ya
  señalado como riesgo por el documento de requisitos) — SmartScreen/
  Defender mostrarán "editor desconocido". `vpk upload github` usa
  `secrets.GITHUB_TOKEN` (con scope `contents: write` limitado al propio
  repo mediante el bloque `permissions:` del workflow), nunca un PAT de
  usuario con scopes más amplios — ningún secreto nuevo que gestionar a
  mano. `AutoStartService` escribe en `HKCU` (por usuario), nunca en
  `HKLM` — no requiere elevación y no afecta a otras cuentas de la máquina.
- **Rendimiento:** el timer de `AppUpdateService` corre cada 4h, muy por
  debajo de cualquier límite de tasa razonable de GitHub Releases
  (repositorio público, sin autenticación en la comprobación). El coste de
  autoarranque (poll de 60s + proceso WebView2 desde el login) ya está
  evaluado como riesgo Low en el documento de requisitos por ser
  estrictamente opt-in.
- **Manejo de errores:** `AppUpdateService.CheckAndApplyAsync` degrada en
  silencio ante cualquier excepción (log `Warning`, sin interrumpir el
  widget) — mismo patrón ya validado en F2 para fallos de red
  (`RetryingUsageDataSource`). `AutoStartService.ApplyToRegistry` captura
  específicamente `UnauthorizedAccessException`/`SecurityException`/
  `IOException` (permisos de registro denegados) con log `Error`, sin
  lanzar — coherente con el resto de servicios de `Windowing`/`Tray`, que
  nunca interrumpen el ciclo de vida de la ventana por un fallo periférico.
- **Observabilidad:** ambos servicios nuevos inyectan `ILogger<T>` vía el
  Serilog ya configurado en F2 (`%LOCALAPPDATA%\ClaudeMeter\logs`) — ninguna
  infraestructura de logging nueva.
- **Compatibilidad hacia atrás:** `AppConfig.AutoStartEnabled` se añade con
  valor por defecto (`false`) al final del `record`, igual que `Theme` se
  añadió en F3 — un `config.json` de una instalación F3 sin este campo
  sigue cargando sin error (`ResolveAutoStart` inexistente porque un `bool?`
  ausente en el DTO ya resuelve a `false` por el operador `??`).

## Implementation Plan

1. **US-1 — Versionado:**
   1.1. `dotnet tool install -g nbgv`; `nbgv install` desde la raíz del repo.
   1.2. Confirmar/ajustar `version.json` (semilla `1.0`, `publicReleaseRefSpec`
        con `master` + tags `v*`) según lo generado por la herramienta.
   1.3. Consolidar el `PackageReference` de Nerdbank.GitVersioning en
        `Directory.Build.props` si `nbgv install` lo repartió por proyecto.
   1.4. Verificar manualmente: build local en `features/f6` produce una
        versión con sufijo de commit/dirty; no debe fallar el build por
        ausencia/formato inválido de `version.json` en el caso contrario
        (AC 5 — probar renombrando temporalmente el fichero).
2. **CI/CD base:** crear `.github/workflows/ci.yml` (restore→build→test,
   `fetch-depth: 0`), verificar que se dispara en push a `features/f6` y en
   PR contra `master`.
3. **US-2 — Empaquetado:**
   3.1. Añadir `PackageReference` a `Velopack` en `ClaudeMeter.Desktop.csproj`.
   3.2. Reestructurar el entry point: `App.xaml` a `Page`, `Program.cs` con
        `Main()` explícito y `VelopackApp.Build().Run()` como primera línea,
        `<StartupObject>` en el `.csproj`.
   3.3. Crear `.github/workflows/release.yml` (trigger `tags: v*`), con los
        9 pasos descritos en el pipeline de CI/CD de arriba.
   3.4. Confirmar contra `vpk pack -h`/`vpk upload github -h` (CLI instalada
        localmente o en el log del job) los flags exactos, incluyendo si
        `vpk pack` soporta bundling directo del bootstrapper de WebView2 o
        si hace falta un paso adicional para descargarlo/incluirlo en
        `publish/` antes de empaquetar.
4. **US-3 — Autoarranque + auto-actualización:**
   4.1. Añadir `AutoStartEnabled` a `AppConfig`/`AppConfigStore`
        (DTO + resolución, siguiendo el patrón de `ChimeEnabled`).
   4.2. Implementar `Startup/AutoStartService.cs` (resolución de ruta
        estable vía `IVelopackLocator`, lectura/escritura de
        `HKCU\...\Run`, persistencia del toggle en `config.json`).
   4.3. Implementar `Updates/AppUpdateService.cs` (`UpdateManager` +
        `GithubSource`, comprobación al arrancar + timer de 4h,
        degradación silenciosa).
   4.4. Extender `TrayIconService` con el ítem "Iniciar con Windows"
        (mismo patrón que `_clickThroughItem`).
   4.5. Registrar ambos servicios en `App.xaml.cs` (DI + `Initialize()`/
        `Start()` al final de `OnStartup`, `Dispose()` en `OnExit`).
5. **Verificación cruzada:** confirmar que `dotnet build`/`dotnet test`
   siguen en verde con los cambios de entry point (`Program.cs` no debe
   romper `ClaudeMeter.Desktop.Tests`, que sigue referenciando `App`/
   componentes Razor directamente, no `Program.Main`).

## Risks & Open Decisions

- **Riesgo nuevo (US-2), no cubierto por el documento de requisitos —
  WebView2 Runtime en máquina limpia.** La AC "instalación limpia, sin
  herramientas de desarrollo" no garantiza por sí sola que el WebView2
  Runtime esté presente (solo Windows 11 "elegible" lo trae preinstalado,
  según la documentación oficial de Microsoft citada en Technology
  Choices). Impact: Medium — sin el runtime, `BlazorWebView` no arranca y
  el widget queda inutilizable tras una instalación aparentemente exitosa.
  **Resuelto en este documento** (no es una decisión abierta): bundle del
  WebView2 Evergreen Bootstrapper como prerrequisito de instalación (ver
  Technology Choices) — el mecanismo exacto de Velopack para adjuntarlo
  (flag de `vpk pack` vs. hook `OnBeforeInstall` propio) queda como tarea
  4.4 del Implementation Plan, no como decisión de producto pendiente.
- **API exacta de `IVelopackLocator` para resolver la ruta estable del
  ejecutable (US-3).** Se ha verificado que la interfaz existe y expone
  `RootAppDir`/`AppId` (entre otras propiedades), pero no el método/
  propiedad estático exacto para obtener la instancia activa desde
  `AutoStartService` en tiempo de ejecución — Development lo confirma
  contra `docs.velopack.io/reference/cs/Velopack.Locators` (ver tarea 4.2).
  No se considera una decisión de arquitectura abierta (la decisión —
  "usar el locator de Velopack, no `Environment.ProcessPath`" — ya está
  tomada), sino una comprobación mecánica de superficie de API, mismo
  criterio que las versiones exactas de paquete NuGet.
- **Certificado de firma de código:** ya señalado como riesgo Medium por el
  documento de requisitos y explícitamente fuera de alcance de este
  documento (decisión de inversión, no de arquitectura). Ninguna acción de
  este diseño lo bloquea ni lo precondiciona — el instalador sin firmar
  puede reemplazarse más adelante por uno firmado sin cambios de
  arquitectura (solo añadiría un paso de firma antes de `vpk pack`).
- **Ninguna otra decisión genuinamente abierta.** Las dos investigadas
  arriba (WebView2 bootstrap, locator de Velopack) ya tienen una decisión
  de arquitectura cerrada en este documento; lo pendiente en ambos casos es
  exclusivamente la confirmación de un nombre de API/flag concreto contra
  la documentación oficial en el momento de implementar, no una elección
  que requiera criterio humano adicional.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1, US-2 y US-3 del documento de
      requisitos se cumplen
- [ ] **Validación manual (US-2):** instalación limpia del instalador
      generado por CI en una máquina Windows real sin Visual Studio ni SDK
      de .NET, confirmando que la aplicación arranca y funciona — ningún
      agente de este pipeline dispone de una máquina Windows limpia real
      para ejecutar esta comprobación
- [ ] **Validación manual (US-2):** confirmar que el instalador queda
      publicado como asset en una release real de GitHub tras empujar un
      tag real — requiere credenciales/permiso de publicación de releases
      que ningún agente de este pipeline tiene
- [ ] **Validación manual (US-3):** confirmar el autoarranque real tras
      cerrar sesión/reiniciar Windows, y la aplicación real de una
      actualización entre dos versiones publicadas consecutivas — requiere
      un ciclo real de publicación de al menos dos releases y una máquina
      Windows real, fuera del alcance de cualquier agente automatizado de
      este pipeline
- [ ] **Validación manual añadida por este documento de diseño (US-2):**
      confirmar en una máquina Windows real sin el WebView2 Runtime
      preinstalado (p. ej. una imagen de Windows 10 limpia, o Windows 11
      con el componente deshabilitado) que el bootstrapper embebido lo
      instala correctamente antes de que el widget intente arrancar
      `BlazorWebView` — mismo motivo que las validaciones manuales
      anteriores: requiere una máquina Windows real fuera del alcance de
      este pipeline.
