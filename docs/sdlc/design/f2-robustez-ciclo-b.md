# Design: F2 — Robustez, Ciclo B

## Requirements Reference

`docs/sdlc/requirements/f2-robustez-ciclo-b.md` — cubre US-1 (`config.json`:
intervalo, posición inicial, chime) y US-2 (arrastrar el widget y persistir
la nueva posición), ambas dentro de la issue única **#13**. Documento
cerrado, sin "Open question" pendientes: el disparador del chime (transición
a `UsageThreshold.Critical`, una sola vez por transición) ya viene confirmado
por el usuario. El Ciclo A de F2 (`docs/sdlc/requirements/f2-robustez-ciclo-a.md`
/ `docs/sdlc/design/f2-robustez-ciclo-a.md`, issues #10→#11→#12) queda fuera
de alcance de este documento salvo como base ya mergeada sobre la que este
ciclo construye (reintento, Serilog, `ReauthNotice`).

## Architecture Overview

Este ciclo no toca Domain más que en un único punto acotado, no toca
Application ni Infrastructure en absoluto, y añade **trabajo completamente
nuevo** en Desktop (confirmado por inspección: no existe hoy ningún lector
de configuración ni lógica de arrastre en el repositorio). Sigue el mismo
patrón ya usado por `SerilogConfigurator` en F2/Ciclo A: la configuración es
un detalle de composition root/presentación, no un puerto de Application —
ver rationale en Technology Choices.

Piezas nuevas/modificadas, de fuera hacia dentro:

1. **`ClaudeMeter.Domain.Usage.ThresholdTransition`** (Domain, único cambio
   fuera de Desktop) — función pura y sin dependencias, hermana de
   `UsageThresholdClassifier` (F1) ya existente en el mismo fichero/namespace:
   dado un `UsageThreshold?` anterior y uno actual, responde si la ventana
   **acaba de entrar** en `Critical`. Es la regla de negocio exacta que pide
   el AC del chime ("una sola vez por transición"), y vive en Domain por el
   mismo motivo que `UsageThresholdClassifier` ya vive allí (lógica pura
   reutilizable, sin UI ni E/S, candidata a que `MascotState`/`MascotPage`
   de F3 la reutilice si también necesita reaccionar a transiciones).
2. **`ClaudeMeter.Desktop.Configuration.AppConfig` / `WindowPosition`**
   (Desktop, nuevo) — DTOs inmutables en memoria que representan la
   configuración ya resuelta (con valores por defecto aplicados donde
   corresponda). `AppConfig.Default` fija los 3 valores por defecto actuales
   del código (intervalo 60s, posición `null` = "calcular esquina inferior
   derecha", chime desactivado).
3. **`ClaudeMeter.Desktop.Configuration.AppConfigStore`** (Desktop, nuevo) —
   única clase que lee/escribe `config.json` con `System.Text.Json` (ya
   incluido en el framework compartido, sin `PackageReference` nuevo — ver
   Technology Choices). `Load()` nunca lanza: JSON corrupto → todos los
   valores por defecto; campo concreto fuera de rango (p. ej. intervalo ≤0)
   → solo ese campo cae a su valor por defecto, con un `Warning` en el log
   por cada caso. `Save()`/`SavePosition()` escriben de forma atómica
   (fichero temporal + `File.Move(..., overwrite: true)`) y nunca lanzan:
   un fallo de E/S se registra (`Warning`/`Error`) y se traga, exactamente
   el comportamiento que pide el último AC de US-2.
4. **`ClaudeMeter.Desktop.Windowing.WindowPositionResolver`** (Desktop,
   nuevo) — función pura (sin `Window` ni Win32 real) que decide la posición
   final `(Left, Top)` a partir de la posición configurada (si la hay) y la
   lista de áreas de trabajo de los monitores conectados: si la posición
   configurada cabe entera dentro de alguna, se usa tal cual; si no (o si no
   hay ninguna configurada), se aplica el mismo cálculo de esquina inferior
   derecha del monitor principal que ya existe hoy en `MainWindow.xaml.cs`.
   Separar esta decisión en una función pura permite testearla con xUnit
   puro pasando rectángulos de pantalla simulados, sin ningún monitor real
   ni `Window` real (AC de la Definition of Done heredada).
5. **`ClaudeMeter.Desktop.Windowing.Win32ScreenInfo`** (Desktop, nuevo) —
   adaptador fino que envuelve `EnumDisplayMonitors`/`GetMonitorInfo` de
   `user32.dll` para obtener las áreas de trabajo reales de los monitores
   conectados en producción. Es la única pieza de este ciclo, junto con
   `MainWindow.xaml.cs`, que no se testea automáticamente (igual que
   `MainWindow`/`OnStartup` ya son intocables por los agentes de este
   pipeline, ver Definition of Done) — `WindowPositionResolver` absorbe toda
   la lógica testeable, `Win32ScreenInfo` es solo el "dato de entrada real".
6. **`ClaudeMeter.Desktop.Windowing.WindowDragService`** (Desktop, nuevo) —
   resuelve el problema de "airspace" de arrastrar una `Window` cuyo `Grid`
   raíz está completamente cubierto por `BlazorWebView`/WebView2 (ver
   Technology Choices para el rationale completo de la decisión). Expone un
   único método `[JSInvokable] BeginDrag()` invocado desde JavaScript al
   detectar `mousedown` sobre el contenido Razor; internamente llama a
   `Window.DragMove()` (bloqueante hasta que el usuario suelta el botón) y,
   al volver, persiste la posición final vía `AppConfigStore.SavePosition`.
7. **`wwwroot/js/drag.js`** (Desktop, nuevo) — módulo mínimo que registra un
   listener `mousedown` a nivel de documento y reenvía el gesto al lado .NET
   vía `DotNetObjectReference`/`invokeMethodAsync`.
8. **`ClaudeMeter.Desktop.Audio.IChimePlayer` / `SystemSoundChimePlayer`**
   (Desktop, nuevo) — abstrae "reproducir el chime" detrás de una interfaz
   de una sola línea para que bUnit pueda sustituirlo por un doble de test
   (ninguna prueba automatizada reproduce audio real, igual que ninguna
   toca un monitor real — mismo principio que la Definition of Done ya
   exige). La implementación real usa `System.Media.SystemSounds` (ver
   Technology Choices).
9. **`App.xaml.cs`** (Desktop, modificado) — composition root: añade el
   registro de `AppConfigStore`, `AppConfig` (resuelto vía `Load()` la
   primera vez que se resuelve del contenedor), `IChimePlayer` y
   `WindowDragService` al `IServiceCollection` ya existente. Ningún cambio
   en el registro de `IUsageDataSource`/Serilog ya hecho en Ciclo A.
10. **`MainWindow.xaml.cs`** (Desktop, modificado) — sustituye el cálculo
    fijo de esquina inferior derecha por una llamada a
    `WindowPositionResolver.Resolve(...)` con la posición configurada y las
    pantallas reales de `Win32ScreenInfo`; además resuelve `WindowDragService`
    del contenedor y le "adjunta" `this` (la ventana no existe todavía
    cuando `App.OnStartup` construye el `IServiceCollection` — ver Technology
    Choices sobre por qué no puede inyectarse por constructor).
11. **`UsagePage.razor`** (Desktop, modificado) — 3 cambios: (a) el
    intervalo de poll pasa de la constante `RefreshInterval` a
    `Config.PollingInterval`; (b) registra el listener de arrastre
    (`DotNetObjectReference` de `WindowDragService`) en
    `OnAfterRenderAsync(firstRender)`, liberado en `Dispose()`; (c) tras
    cada snapshot exitoso, calcula el `UsageThreshold` de sesión/semana,
    detecta con `ThresholdTransition.EnteredCritical` si alguna de las dos
    acaba de entrar en Crítico y, si `Config.ChimeEnabled`, llama a
    `IChimePlayer.Play()`.

Diagrama de dependencias resultante (piezas nuevas/modificadas resaltadas):

```
Domain.Usage
 └── ThresholdTransition.cs (nuevo, función pura, junto a UsageThresholdClassifier)

Desktop
 ├── Configuration/
 │    ├── AppConfig.cs (nuevo, DTO inmutable + Default)
 │    └── AppConfigStore.cs (nuevo) ──> System.Text.Json (ya en el framework)
 ├── Windowing/
 │    ├── WindowPositionResolver.cs (nuevo, función pura)
 │    ├── Win32ScreenInfo.cs (nuevo, P/Invoke user32.dll)
 │    └── WindowDragService.cs (nuevo) ──> Window.DragMove(), AppConfigStore
 ├── Audio/
 │    ├── IChimePlayer.cs (nuevo)
 │    └── SystemSoundChimePlayer.cs (nuevo) ──> System.Media.SystemSounds
 ├── wwwroot/js/drag.js (nuevo)
 ├── App.xaml.cs (modificado) ──> registra AppConfigStore/AppConfig/
 │                                  IChimePlayer/WindowDragService
 ├── MainWindow.xaml.cs (modificado) ──> WindowPositionResolver, Win32ScreenInfo,
 │                                         WindowDragService.AttachWindow(this)
 └── Pages/UsagePage.razor (modificado) ──> Config.PollingInterval,
                                              ThresholdTransition, IChimePlayer,
                                              JS interop de arrastre
```

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| **Mecanismo de arrastre: `mousedown` capturado en JavaScript dentro del contenido Razor → JS interop (`DotNetObjectReference`) → `Window.DragMove()` en el lado WPF** | Resuelve el riesgo de "airspace" que Requirements deja explícitamente para Design: el `Grid` raíz de `MainWindow.xaml` está cubierto al 100% por `BlazorWebView`/WebView2 (mismo `HWND` hijo documentado en F1 para la transparencia), así que un `MouseLeftButtonDown` capturado a nivel de `Window` nunca llegaría a dispararse con el puntero sobre el contenido visible — WebView2 se queda con el evento nativo primero. La clave que hace viable este mecanismo es que `Window.DragMove()` comprueba el estado *físico* actual del botón izquierdo (`Mouse.LeftButton`, que refleja el estado global de Win32, no "qué `HWND` recibió el último `WM_LBUTTONDOWN`"), y una vez esa comprobación pasa, dispara internamente `WM_SYSCOMMAND`/`SC_MOVE`, que inicia un bucle de movimiento nativo atado a la ventana top-level — no importa qué ventana hija recibió el clic original. Confirmado por búsqueda dirigida: la propia issue de GitHub `dotnet/maui#1304` ("Placing WPF Controls on top of BlazorWebView for a movable/draggable window") documenta este mismo problema de "airspace" para `BlazorWebView`+WPF y apunta a variantes de esta técnica (superponer un control WPF nativo, o repetir el patrón `mousedown`→`DragMove()`) como solución práctica ya usada por la comunidad. Además `DragMove()` ya viene verificado como API soportada por el propio documento de requisitos ([`Window.DragMove` — Microsoft Learn](https://learn.microsoft.com/dotnet/api/system.windows.window.dragmove)). Requiere cero paquetes NuGet nuevos: `IJSRuntime`/`DotNetObjectReference` ya forman parte de `Microsoft.AspNetCore.Components.WebView.Wpf` (ya referenciado desde F1). | (1) **Franja/handle WPF nativo fuera del `BlazorWebView`** (p. ej. reservar unos px superiores del `Grid` para un control WPF puro que sí reciba `MouseLeftButtonDown` directamente) — rechazada: el widget mide 280×140px, deliberadamente pequeño y "inmersivo" desde F1 (todo el contenido visible vive en Razor); reservar una franja fija le quitaría espacio útil de forma permanente solo para resolver un gesto que hoy solo ocurre de forma ocasional (arrastrar para reposicionar), y contradice la dirección de diseño ya fijada en F1 de que "todo el contenido visible vive en Razor". (2) **Regiones de arrastre nativas de WebView2** (`CoreWebView2Settings.IsNonClientRegionSupportEnabled` + CSS `app-region: drag`, confirmado por búsqueda dirigida como mecanismo real y soportado) — rechazada por mayor complejidad de wiring para este ciclo: exige acceder al `CoreWebView2` subyacente después de que `BlazorWebView` complete su inicialización asíncrona (un ciclo de vida adicional a orquestar), y trata la región arrastrable como una barra de título real del sistema operativo (menú de sistema al clic derecho, maximizar/restaurar al doble clic) — comportamiento no pedido por ningún AC y potencialmente confuso en una ventana `ResizeMode="NoResize"` sin controles de maximizado. Queda documentada como alternativa válida a reconsiderar si F3 introduce una cabecera/barra de título real. |
| `Window.DragMove()` invocado dentro de `[JSInvokable] WindowDragService.BeginDrag()`, con guarda `Dispatcher.CheckAccess()`/`Dispatcher.Invoke` como red de seguridad | `DragMove()` exige ejecutarse en el hilo propietario de la ventana (el hilo STA de UI de WPF). Aunque `BlazorWebView` en modo WPF (a diferencia de Blazor Server) ejecuta el código de componentes y, por diseño, los callbacks de interop JS→.NET en el mismo hilo de UI (no hay "circuito"/hilo de SignalR de por medio), añadir la comprobación defensiva `CheckAccess()`/`Invoke` es una red de seguridad barata que elimina cualquier duda razonable sobre el hilo de ejecución sin coste de legibilidad. | Asumir sin guarda que el callback siempre llega en el hilo de UI — rechazada: es probablemente cierto, pero una `InvalidOperationException` en tiempo de arrastre (una interacción visible y frecuente para el usuario) es un coste de robustez demasiado alto para ahorrarse 3 líneas defensivas. |
| Persistencia de la posición: `AppConfigStore.SavePosition(left, top)` = `Load()` + `with { Position = ... }` + `Save()` (sin ningún lock/mutex adicional) | Como `DragMove()` es una llamada **bloqueante** (el bucle de movimiento nativo no devuelve el control hasta que el usuario suelta el botón), dos arrastres nunca pueden solaparse en el tiempo: el segundo `mousedown` no puede procesarse hasta que el primer `BeginDrag()` haya retornado (y por tanto ya haya persistido). Esto satisface directamente el AC "dos arrastres consecutivos... refleja siempre la posición del último arrastre completado, sin escritura a medias" sin necesitar ninguna primitiva de concurrencia explícita — la escritura atómica (fichero temporal + `File.Move(..., overwrite: true)`) ya cubre el caso "no queda un `config.json` a medio escribir" a nivel de sistema de ficheros, que es un riesgo ortogonal (falla de E/S a mitad de escritura), no de solapamiento de arrastres. | Un `SemaphoreSlim`/lock explícito alrededor de la escritura — rechazada por innecesaria: no existe ninguna vía por la que este código pueda ejecutarse dos veces en paralelo dado que `DragMove()` ya serializa los arrastres por construcción: añadir un lock sería ceremonia sin ningún escenario real que lo dispare. |
| `WindowDragService` registrado como singleton en el `IServiceCollection` de `App.xaml.cs`, pero **sin** `Window` en su constructor — se "adjunta" después vía `AttachWindow(this)` desde `MainWindow.xaml.cs` | `StartupUri="MainWindow.xaml"` (ya fijado en `App.xaml`) hace que WPF cree `MainWindow` **después** de que `App.OnStartup()` termine de construir `Services` (la creación vía `StartupUri` ocurre en el post-procesamiento de `Application.Run`, no dentro de la llamada a `base.OnStartup(e)`) — es el mismo motivo por el que hoy `MainWindow.xaml.cs` ya lee `((App)Application.Current).Services` en su propio constructor en vez de recibirlo inyectado. `WindowDragService` no puede depender de una `Window` por constructor porque, en el momento en que se construye el `IServiceCollection`, esa `Window` todavía no existe. | Resolver `WindowDragService` como *no-singleton* (`AddTransient`) construido directamente por `MainWindow` con `new WindowDragService(this, ...)` — rechazada: `UsagePage.razor` necesita la **misma** instancia para registrar el listener JS (`DotNetObjectReference.Create(DragService)`), y `MainWindow`/`UsagePage` no comparten un mismo punto de construcción; con `AddSingleton` + `AttachWindow`, ambos consumidores obtienen la misma instancia sin acoplar `UsagePage.razor` a `MainWindow` directamente. |
| `AppConfigStore`/`AppConfig` en `ClaudeMeter.Desktop.Configuration` (Desktop), no como puerto de Application (`IAppConfigStore`) implementado en Infrastructure | Mismo criterio ya aplicado a `SerilogConfigurator` en F2/Ciclo A ("solo Desktop, como composition-root/presentación, referencia el detalle de E/S concreto"): `config.json` alimenta exclusivamente decisiones de Desktop (intervalo pasado a `UsagePollingCoordinator`, posición de `MainWindow`, activación de un `IChimePlayer` que solo tiene sentido en presentación) — ninguna de las tres cosas la necesita Domain/Application, y `CLAUDE.md` es explícito en que los puertos de Application son deliberadamente pocos y con propósito ("no single catch-all `IUsageService`"); introducir un puerto nuevo para esto sería la misma ceremonia sin caso de uso real que F1 ya rechazó para MediatR. | Puerto `IAppConfigStore` en `ClaudeMeter.Application.Abstractions` + adaptador `JsonFileAppConfigStore` en Infrastructure — rechazada: ningún caso de uso de Application necesita leer configuración hoy (a diferencia de `IUsageDataSource`, consumido por un caso de uso real); la abstracción no tendría más que un consumidor (Desktop) y una implementación, exactamente el patrón que `CLAUDE.md` pide evitar. Se reconsiderará si F5 (multi-cuenta, varios `config_dirs`) necesita que Application orqueste configuración real. |
| `System.Text.Json` (ya parte del framework compartido de `net8.0-windows`, sin `PackageReference` nuevo) para (de)serializar `config.json` | Confirmado por inspección del `.csproj` de Desktop: el proyecto usa `Microsoft.NET.Sdk.Razor` sobre `net8.0-windows`, que referencia el framework compartido `Microsoft.NETCore.App` (incluye `System.Text.Json` desde .NET Core 3.0) sin necesidad de ningún paquete adicional — es la opción de menor superficie posible, coherente con el criterio de "sin dependencias nuevas si no hace falta" que la propia issue #13 sugiere y que Ciclo A ya aplicó para rechazar Polly. | `Newtonsoft.Json` — rechazada: paquete de terceros adicional sin ninguna necesidad funcional no cubierta por `System.Text.Json` (el esquema de `config.json` es trivial: 3 campos planos + un objeto anidado). |
| Escritura atómica de `config.json`: fichero temporal (`config.json.tmp`) + `File.Move(tempPath, path, overwrite: true)` | `File.Move` con reemplazo es una operación atómica a nivel de sistema de archivos en NTFS (rename), evitando que un fallo a mitad de la escritura del JSON dañe el fichero existente — requisito directo del AC "no queda una escritura a medias ni una mezcla de coordenadas". `File.Move(string, string, bool)` con el parámetro `overwrite` está disponible en el BCL desde .NET Core 3.0, ya cubierto por el TFM `net8.0-windows` fijado en `Directory.Build.props`, sin comprobación de versión de paquete adicional (es una firma del BCL, no un paquete NuGet). | Escribir directamente sobre `config.json` con `File.WriteAllText` — rechazada: una excepción de E/S a mitad de esa llamada (disco lleno, ya contemplado como riesgo explícito por el propio AC) podría dejar un JSON truncado e inválido, que en el siguiente arranque activaría el camino de "JSON corrupto → valores por defecto" y **perdería silenciosamente** toda la configuración previa del usuario (intervalo/chime incluidos, no solo la posición) — un efecto colateral peor que el que el AC intenta evitar. |
| `EnumDisplayMonitors`/`GetMonitorInfo` (P/Invoke directo a `user32.dll`) para enumerar las áreas de trabajo de los monitores conectados, en vez de `System.Windows.Forms.Screen.AllScreens` | Evita activar `<UseWindowsForms>true</UseWindowsForms>` en el `.csproj` de Desktop solo para una única llamada de enumeración de monitores — dos declaraciones `[DllImport]` (firma estándar, ampliamente documentada) logran lo mismo sin ampliar la superficie del framework compartido referenciado. Firma verificada por búsqueda dirigida contra fuentes reconocidas de P/Invoke para .NET (`pinvoke.net`, artículos de enumeración de monitores en WPF con alta DPI). Aislado detrás de `Win32ScreenInfo`, la única pieza no testeada de este mecanismo — `WindowPositionResolver` (la lógica real) sí es xUnit puro con rectángulos simulados. | `System.Windows.Forms.Screen.AllScreens` (activando `UseWindowsForms`) — rechazada por ahora: añadiría una capacidad de framework compartido completa (WinForms) para una sola llamada; se reconsiderará en F3, que según `CLAUDE.md` necesitará de todos modos un icono de bandeja (`System.Windows.Forms.NotifyIcon` es la vía estándar para esa función en WPF), momento en el que `UseWindowsForms` pasaría a estar justificado por una necesidad real adicional y `Win32ScreenInfo` podría sustituirse sin romper `WindowPositionResolver` (que no conoce el origen del `IReadOnlyList<Rect>`). |
| `System.Media.SystemSounds.Exclamation.Play()` (vía `IChimePlayer`/`SystemSoundChimePlayer`) para el chime, en vez de un `.wav` propio empaquetado | `System.Media` (con `SystemSounds`/`SoundPlayer`) es parte de `System.Windows.Extensions`, incluido en el framework compartido `Microsoft.WindowsDesktop.App` que el proyecto ya referencia vía `UseWPF=true` — cero coste de empaquetado, cero asset nuevo que mantener, y evita decidir/alojar un fichero de audio propio para un MVP cuyo único requisito es "un sonido audible". `Exclamation` es semánticamente el sonido del sistema más cercano a "algo requiere tu atención", coherente con que el chime solo suena al cruzar el umbral rojo/Crítico. Envuelto detrás de `IChimePlayer` para que bUnit pueda sustituirlo por un doble de test sin reproducir audio real (mismo principio ya aplicado a "sin monitor real" en Definition of Done). | Empaquetar un `.wav` propio y reproducirlo con `System.Media.SoundPlayer` — rechazada por ahora: añade un asset binario al repositorio y una decisión de diseño de sonido (duración, volumen, timbre) fuera del alcance funcional de este ciclo (la issue solo pide "un sonido audible"); candidato natural para F3 (pulido de UX) si el usuario quiere un sonido de marca propio. |
| `ThresholdTransition.EnteredCritical(UsageThreshold? previous, UsageThreshold? current)` como función pura en `ClaudeMeter.Domain.Usage`, evaluada solo sobre snapshots `Success` | Igual que `UsageThresholdClassifier`, es una regla de negocio pura ("¿acabas de entrar en Crítico?") sin ninguna dependencia de UI/E/S, así que vive en Domain junto a él. Evaluarla **solo** dentro de la rama `snapshot.IsSuccess` de `UsagePage.Apply()` (no tocar el estado `_previousSessionThreshold`/`_previousWeeklyThreshold` en ciclos `RequestFailed`/`TokenUnavailable`/`Unauthorized`/`MalformedResponse`) evita que un fallo transitorio de red "resetee" la detección de transición y provoque un chime falso al recuperarse — el AC habla de transiciones sobre el consumo real, no sobre la disponibilidad de la API. | Calcular la transición dentro de `UsageThresholdClassifier.Classify` (que ya conoce el umbral) en vez de una función/estado aparte — rechazada: `Classify` es una función *sin estado* (un `PercentageUsed` → un `UsageThreshold`), mientras que detectar una transición exige conservar el umbral anterior entre ciclos; mezclar ambas responsabilidades en la misma función rompería su pureza actual y la obligaría a dejar de ser `static`. |

## Data Model / Interfaces

### `ThresholdTransition.cs` (Domain, nuevo)

```csharp
// src/ClaudeMeter.Domain/Usage/ThresholdTransition.cs
namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Detecta transiciones de severidad entre dos lecturas consecutivas de
/// <see cref="UsageThreshold"/>. Función pura, sin dependencias externas,
/// sin estado propio (el estado "anterior" lo conserva el llamador, igual
/// que <see cref="UsageThresholdClassifier"/> no conserva ningún historial).
/// </summary>
public static class ThresholdTransition
{
    /// <summary>
    /// <c>true</c> únicamente cuando <paramref name="current"/> es
    /// <see cref="UsageThreshold.Critical"/> y <paramref name="previous"/>
    /// no lo era (incluido el caso "no había lectura previa todavía",
    /// <c>previous == null</c>, que SÍ cuenta como transición la primera
    /// vez que se observa Crítico). No dispara de nuevo mientras
    /// <paramref name="current"/> se mantenga en Crítico ciclo tras ciclo.
    /// </summary>
    public static bool EnteredCritical(UsageThreshold? previous, UsageThreshold? current) =>
        current == UsageThreshold.Critical && previous != UsageThreshold.Critical;
}
```

### `AppConfig.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Configuration/AppConfig.cs
namespace ClaudeMeter.Desktop.Configuration;

/// <summary>Posición de ventana ya resuelta (no necesariamente válida para las pantallas actuales — esa validación la hace <c>WindowPositionResolver</c>).</summary>
public sealed record WindowPosition(double Left, double Top);

/// <summary>
/// Configuración de la aplicación ya resuelta en memoria, con los valores
/// por defecto aplicados donde <c>config.json</c> no exista, esté corrupto,
/// o tenga campos concretos fuera de rango (ver <see cref="AppConfigStore"/>).
/// </summary>
/// <param name="PollingInterval">Intervalo de refresco del poll. Por defecto 60s (idéntico al hardcodeado hasta este ciclo).</param>
/// <param name="Position"><c>null</c> = "sin posición configurada, calcular la esquina inferior derecha por defecto", igual que el comportamiento actual de <c>MainWindow</c>.</param>
/// <param name="ChimeEnabled">Por defecto <c>false</c> — un sonido inesperado en el primer arranque sería mala primera experiencia (decisión ya fijada por el documento de requisitos).</param>
public sealed record AppConfig(TimeSpan PollingInterval, WindowPosition? Position, bool ChimeEnabled)
{
    public static AppConfig Default { get; } = new(TimeSpan.FromSeconds(60), Position: null, ChimeEnabled: false);
}
```

### `AppConfigStore.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Configuration/AppConfigStore.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Configuration;

/// <summary>
/// Única clase que lee/escribe <c>config.json</c>. <see cref="Load"/> nunca
/// lanza: JSON sintácticamente inválido o ilegible cae a
/// <see cref="AppConfig.Default"/> completo; un campo concreto fuera de
/// rango (p. ej. intervalo &lt;= 0) cae solo ese campo a su valor por
/// defecto, con un <c>Warning</c> en el log por cada caso (AC de US-1).
/// <see cref="Save"/>/<see cref="SavePosition"/> nunca lanzan: un fallo de
/// E/S se registra y se traga, sin abortar la aplicación (AC de US-2).
/// </summary>
public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ILogger<AppConfigStore> _logger;

    public AppConfigStore(ILogger<AppConfigStore> logger) => _logger = logger;

    /// <summary><c>%LOCALAPPDATA%\ClaudeMeter\config.json</c> — misma carpeta base que los logs de F2/Ciclo A (<c>%LOCALAPPDATA%\ClaudeMeter\logs</c>).</summary>
    public static string DefaultConfigPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeMeter", "config.json");

    public AppConfig Load(string? configPath = null)
    {
        var path = configPath ?? DefaultConfigPath();
        if (!File.Exists(path))
        {
            return AppConfig.Default; // AC: sin fichero -> valores por defecto, sin crearlo como efecto secundario
        }

        AppConfigDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AppConfigDto>(File.ReadAllText(path), SerializerOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "config.json no se pudo leer o es JSON inválido; se usan los valores por defecto");
            return AppConfig.Default;
        }

        if (dto is null)
        {
            _logger.LogWarning("config.json está vacío o es literalmente 'null'; se usan los valores por defecto");
            return AppConfig.Default;
        }

        return new AppConfig(
            PollingInterval: ResolveInterval(dto.PollingIntervalSeconds),
            Position: ResolvePosition(dto.WindowPosition),
            ChimeEnabled: dto.ChimeEnabled ?? AppConfig.Default.ChimeEnabled);
    }

    public void Save(AppConfig config, string? configPath = null)
    {
        var path = configPath ?? DefaultConfigPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var dto = new AppConfigDto
            {
                PollingIntervalSeconds = config.PollingInterval.TotalSeconds,
                ChimeEnabled = config.ChimeEnabled,
                WindowPosition = config.Position is { } p ? new WindowPositionDto { Left = p.Left, Top = p.Top } : null,
            };

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(dto, SerializerOptions));
            File.Move(tempPath, path, overwrite: true); // escritura atómica: nunca deja config.json a medias (AC de US-2)
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "No se pudo escribir config.json; el cambio permanece solo en memoria para esta sesión");
        }
    }

    /// <summary>Usado por <c>WindowDragService</c> tras soltar un arrastre (AC de US-2). Conserva intervalo/chime ya persistidos, solo actualiza la posición.</summary>
    public void SavePosition(double left, double top, string? configPath = null)
    {
        var current = Load(configPath);
        Save(current with { Position = new WindowPosition(left, top) }, configPath);
    }

    private TimeSpan ResolveInterval(double? seconds)
    {
        if (seconds is not { } value || value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            if (seconds is not null)
            {
                _logger.LogWarning("pollingIntervalSeconds inválido en config.json ({Value}); se usa el valor por defecto de 60s", seconds);
            }
            return AppConfig.Default.PollingInterval;
        }
        return TimeSpan.FromSeconds(value);
    }

    private WindowPosition? ResolvePosition(WindowPositionDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.Left is not { } left || dto.Top is not { } top ||
            double.IsNaN(left) || double.IsInfinity(left) || double.IsNaN(top) || double.IsInfinity(top))
        {
            _logger.LogWarning("windowPosition inválida en config.json; se usa el cálculo de posición por defecto");
            return null;
        }

        return new WindowPosition(left, top);
    }

    /// <summary>DTO de (de)serialización, deliberadamente con campos anulables: distingue "campo ausente" (usar valor por defecto en silencio) de "campo presente pero fuera de rango" (usar valor por defecto y loguear Warning).</summary>
    private sealed class AppConfigDto
    {
        [JsonPropertyName("pollingIntervalSeconds")]
        public double? PollingIntervalSeconds { get; set; }

        [JsonPropertyName("chimeEnabled")]
        public bool? ChimeEnabled { get; set; }

        [JsonPropertyName("windowPosition")]
        public WindowPositionDto? WindowPosition { get; set; }
    }

    private sealed class WindowPositionDto
    {
        [JsonPropertyName("left")]
        public double? Left { get; set; }

        [JsonPropertyName("top")]
        public double? Top { get; set; }
    }
}
```

### Esquema de `config.json`

```json
{
  "pollingIntervalSeconds": 60,
  "chimeEnabled": false,
  "windowPosition": {
    "left": 1200.0,
    "top": 800.0
  }
}
```

- Los 3 campos son opcionales de forma independiente; cualquiera ausente usa
  su valor por defecto sin generar ningún log (es el caso normal de un
  fichero parcial escrito a mano por el usuario).
- `windowPosition` ausente o `null` ⇒ se aplica el cálculo de esquina
  inferior derecha del monitor principal (comportamiento actual sin
  cambios). Presente pero con coordenadas que no caben en ningún monitor
  conectado ⇒ mismo fallback (ver `WindowPositionResolver` más abajo).
- Unidades de `windowPosition`: mismas unidades que `Window.Left`/`Window.Top`
  de WPF (unidades independientes del dispositivo, 1/96", no píxeles físicos)
  — es exactamente lo que `WindowDragService` lee de `Window.Left`/`Window.Top`
  tras un arrastre y lo que `MainWindow` vuelve a asignar a esas mismas
  propiedades al arrancar, así que no hace falta ninguna conversión en el
  propio `AppConfigStore`.

### `WindowPositionResolver.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Windowing/WindowPositionResolver.cs
using System.Windows;
using ClaudeMeter.Desktop.Configuration;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Decide la posición final (Left, Top) de <c>MainWindow</c> a partir de la
/// posición configurada (si la hay) y las áreas de trabajo de las pantallas
/// actualmente conectadas. Función pura, sin ninguna dependencia de WPF real
/// ni de Win32 — testeable con listas de <see cref="Rect"/> simuladas (AC de
/// "posición fuera de pantalla" de US-1 y del último punto de la Definition
/// of Done heredada: "sin monitor/resolución real").
/// </summary>
public static class WindowPositionResolver
{
    public static (double Left, double Top) Resolve(
        WindowPosition? configuredPosition,
        double windowWidth,
        double windowHeight,
        double screenMargin,
        Rect primaryScreenWorkArea,
        IReadOnlyList<Rect> allScreenWorkAreas)
    {
        if (configuredPosition is { } position &&
            FitsWithinAnyScreen(position.Left, position.Top, windowWidth, windowHeight, allScreenWorkAreas))
        {
            return (position.Left, position.Top);
        }

        // Sin posición configurada, o configurada pero fuera de toda pantalla
        // conectada hoy (AC explícito: fallback al monitor PRINCIPAL, no a
        // cualquier monitor disponible).
        return (
            primaryScreenWorkArea.Right - windowWidth - screenMargin,
            primaryScreenWorkArea.Bottom - windowHeight - screenMargin);
    }

    private static bool FitsWithinAnyScreen(double left, double top, double width, double height, IReadOnlyList<Rect> screens) =>
        screens.Any(screen =>
            left >= screen.Left && top >= screen.Top &&
            left + width <= screen.Right && top + height <= screen.Bottom);
}
```

### `Win32ScreenInfo.cs` (Desktop, nuevo — no testeado automáticamente)

```csharp
// src/ClaudeMeter.Desktop/Windowing/Win32ScreenInfo.cs
using System.Runtime.InteropServices;
using System.Windows;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Adaptador fino sobre <c>user32.dll</c> para obtener las áreas de trabajo
/// reales de los monitores conectados. Deliberadamente sin lógica de
/// decisión (esa vive en <see cref="WindowPositionResolver"/>, xUnit puro) —
/// esta clase, junto con <c>MainWindow</c>, queda fuera de la cobertura
/// automatizada de este ciclo (no hay forma determinista de testear
/// monitores reales, ver Definition of Done).
/// </summary>
internal static class Win32ScreenInfo
{
    public static IReadOnlyList<Rect> GetAllWorkAreas()
    {
        var workAreas = new List<Rect>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref Rect_ lprcMonitor, IntPtr _) =>
        {
            var info = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(lprcMonitor.ToHandleUnused(), ref info)) // ver nota de implementación abajo
            {
                var wa = info.RcWork;
                workAreas.Add(new Rect(wa.Left, wa.Top, wa.Right - wa.Left, wa.Bottom - wa.Top));
            }
            return true;
        }, IntPtr.Zero);

        return workAreas;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect_ lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect_ { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect_ RcMonitor;
        public Rect_ RcWork;
        public int DwFlags;
    }
}
```

> **Nota de implementación para Development:** el delegado de
> `EnumDisplayMonitors` recibe el `HMONITOR` como primer parámetro
> (`hMonitor`), no como el `lprcMonitor` — el pseudocódigo de arriba usa
> `lprcMonitor.ToHandleUnused()` como marcador deliberadamente inválido para
> dejar explícito en este documento que el `hMonitor` correcto (primer
> parámetro del delegado) es el que debe pasarse a `GetMonitorInfo`, no el
> `RECT`. Development debe corregir esa línea a
> `GetMonitorInfo(hMonitor, ref info)` al implementar — no se deja como una
> decisión abierta, es una precisión de firma que cualquier compilación con
> un test manual contra un monitor real revela de inmediato.

### `WindowDragService.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Windowing/WindowDragService.cs
using System.Windows;
using ClaudeMeter.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Único punto de entrada del gesto de arrastre (US-2): se registra como
/// singleton en el composition root (ver rationale de "por qué no recibe
/// Window por constructor" en Technology Choices) y se "adjunta" a la
/// instancia real de <c>MainWindow</c> tan pronto como esta existe. Expuesto
/// a JavaScript vía <see cref="BeginDrag"/> — ver <c>wwwroot/js/drag.js</c>.
/// </summary>
public sealed class WindowDragService
{
    private readonly AppConfigStore _configStore;
    private readonly ILogger<WindowDragService> _logger;
    private Window? _window;

    public WindowDragService(AppConfigStore configStore, ILogger<WindowDragService> logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    [JSInvokable]
    public void BeginDrag()
    {
        if (_window is null)
        {
            _logger.LogWarning("BeginDrag invocado antes de que MainWindow estuviera adjunta; se ignora el gesto");
            return;
        }

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.Invoke(BeginDrag);
            return;
        }

        try
        {
            _window.DragMove(); // bloqueante: no vuelve hasta que el usuario suelta el botón izquierdo
        }
        catch (InvalidOperationException)
        {
            // El botón ya no estaba pulsado cuando este callback llegó a
            // ejecutarse (carrera benigna entre el mousedown de JS y el
            // interop) — DragMove ya no inicia ningún movimiento; no hay
            // nada que persistir.
            return;
        }

        _configStore.SavePosition(_window.Left, _window.Top); // AC de US-2: persistir al soltar
    }
}
```

### `IChimePlayer.cs` / `SystemSoundChimePlayer.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Audio/IChimePlayer.cs
namespace ClaudeMeter.Desktop.Audio;

/// <summary>Abstrae "reproducir el chime" para que bUnit pueda sustituirlo por un doble de test — ninguna prueba automatizada reproduce audio real.</summary>
public interface IChimePlayer
{
    void Play();
}

// src/ClaudeMeter.Desktop/Audio/SystemSoundChimePlayer.cs
namespace ClaudeMeter.Desktop.Audio;

/// <summary>Implementación real: sonido del sistema, sin ningún asset propio (ver rationale en Technology Choices).</summary>
public sealed class SystemSoundChimePlayer : IChimePlayer
{
    public void Play() => System.Media.SystemSounds.Exclamation.Play();
}
```

### `wwwroot/js/drag.js` (Desktop, nuevo)

```javascript
// Reenvía el mousedown del contenido Razor al lado WPF para poder llamar a
// Window.DragMove() — ver rationale del mecanismo de arrastre en el
// documento de diseño (problema de "airspace" de BlazorWebView/WebView2).
window.claudeMeterDrag = {
    init: function (dotNetDragService) {
        document.addEventListener('mousedown', function (e) {
            if (e.button === 0) {
                dotNetDragService.invokeMethodAsync('BeginDrag');
            }
        });
    }
};
```

### `wwwroot/index.html` (Desktop, adición)

```html
<script src="_framework/blazor.webview.js"></script>
<script src="js/drag.js"></script>
```

### `App.xaml.cs` (Desktop, modificado — excerpt)

```csharp
services.AddSingleton<AppConfigStore>();
services.AddSingleton(sp => sp.GetRequiredService<AppConfigStore>().Load()); // AppConfig, resuelto una vez, perezosamente
services.AddSingleton<IChimePlayer, SystemSoundChimePlayer>();
services.AddSingleton<WindowDragService>();
// Resto (HttpClient, ITokenProvider, IUsageDataSource, Serilog) sin cambios respecto a Ciclo A.
```

### `MainWindow.xaml.cs` (Desktop, modificado)

```csharp
using System.Windows;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeMeter.Desktop;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;

    public MainWindow()
    {
        InitializeComponent();

        var app = (App)System.Windows.Application.Current;
        BlazorWebViewHost.Services = app.Services;

        var config = app.Services.GetRequiredService<AppConfig>();
        var (left, top) = WindowPositionResolver.Resolve(
            config.Position, Width, Height, ScreenMargin,
            SystemParameters.WorkArea, Win32ScreenInfo.GetAllWorkAreas());
        Left = left;
        Top = top;

        // US-2: la misma instancia que UsagePage.razor usará para registrar
        // el listener de JS interop de arrastre.
        app.Services.GetRequiredService<WindowDragService>().AttachWindow(this);
    }
}
```

### `UsagePage.razor` (Desktop, modificado — excerpt)

```razor
@implements IDisposable
@inject IUsageDataSource UsageDataSource
@inject ILogger<UsagePollingCoordinator> Logger
@inject AppConfig Config
@inject IChimePlayer ChimePlayer
@inject WindowDragService DragService
@inject IJSRuntime JS

@code {
    private UsagePollingCoordinator? _coordinator;
    private DotNetObjectReference<WindowDragService>? _dragServiceRef;
    private UsageThreshold? _previousSessionThreshold;
    private UsageThreshold? _previousWeeklyThreshold;
    // ... _session/_weekly/_status/_hasEverSucceeded/_isStale sin cambios ...

    protected override void OnInitialized()
    {
        _coordinator = new UsagePollingCoordinator(UsageDataSource, Config.PollingInterval, Logger); // antes: RefreshInterval hardcodeado
        _coordinator.SnapshotReceived += OnSnapshotReceived;
        _coordinator.Start();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dragServiceRef = DotNetObjectReference.Create(DragService);
            await JS.InvokeVoidAsync("claudeMeterDrag.init", _dragServiceRef);
        }
    }

    // OnSnapshotReceived sin cambios (sigue envolviendo Apply+StateHasChanged en InvokeAsync).

    private void Apply(UsageSnapshot snapshot, DateTimeOffset now)
    {
        _status = snapshot.Status;

        var (session, weekly) = RateLimitWindowParser.ParseSnapshot(snapshot, now);

        if (snapshot.IsSuccess)
        {
            var sessionThreshold = UsageThresholdClassifier.Classify(session.PercentageUsed);
            var weeklyThreshold = UsageThresholdClassifier.Classify(weekly.PercentageUsed);

            if (Config.ChimeEnabled &&
                (ThresholdTransition.EnteredCritical(_previousSessionThreshold, sessionThreshold) ||
                 ThresholdTransition.EnteredCritical(_previousWeeklyThreshold, weeklyThreshold)))
            {
                ChimePlayer.Play();
            }

            _previousSessionThreshold = sessionThreshold;
            _previousWeeklyThreshold = weeklyThreshold;

            _session = session;
            _weekly = weekly;
            _isStale = false;
            _hasEverSucceeded = true;
        }
        else if (snapshot.Status == UsageSnapshotStatus.Unauthorized)
        {
            _isStale = false; // sin cambios respecto a Ciclo A
        }
        else if (_hasEverSucceeded)
        {
            _isStale = true; // sin cambios; _previousXThreshold NO se toca aquí (ver Technology Choices)
        }
        else
        {
            _session = session;
            _weekly = weekly;
        }
    }

    /// <summary><c>internal</c>, para bUnit: expone si el chime sonó en el último Apply(), sin depender de mockear System.Media real.</summary>
    internal bool ChimePlayedForTests { get; private set; } // ver nota de test doble en Implementation Plan

    public void Dispose()
    {
        _dragServiceRef?.Dispose();
        if (_coordinator is not null)
        {
            _coordinator.SnapshotReceived -= OnSnapshotReceived;
            _coordinator.Dispose();
        }
    }
}
```

## Implementation Plan

1. **Domain (base para US-1):** crear
   `src/ClaudeMeter.Domain/Usage/ThresholdTransition.cs` tal cual Data
   Model, junto a `UsageThreshold.cs` (mismo fichero o uno nuevo en el mismo
   namespace, a criterio de Development — no es una decisión arquitectónica).
2. **Tests de Domain:** crear
   `test/ClaudeMeter.Domain.Tests/Usage/ThresholdTransitionTests.cs` (xUnit
   puro): casos mínimos — `null→Normal` no transiciona; `Normal→Critical`
   transiciona; `Critical→Critical` (mismo ciclo repetido) NO transiciona;
   `Critical→Warning→Critical` transiciona de nuevo en el segundo cruce;
   `null→Critical` (primera lectura ya en rojo) transiciona.
3. **`Configuration/AppConfig.cs` + `Configuration/AppConfigStore.cs`
   (Desktop, US-1 completo):** crear en
   `src/ClaudeMeter.Desktop/Configuration/` tal cual Data Model. No requiere
   ningún `PackageReference` nuevo (`System.Text.Json` ya disponible).
4. **Tests de `AppConfigStore` (US-1, xUnit puro con rutas temporales):**
   crear `test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs`
   usando `Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")` como
   `configPath` (nunca el `%LOCALAPPDATA%` real de la máquina de CI — AC
   heredado). Casos mínimos, mapeados 1:1 con los AC de US-1: fichero
   inexistente → `AppConfig.Default`; fichero con los 3 campos válidos →
   se reflejan tal cual; JSON sintácticamente inválido (p. ej. `"{"` sin
   cerrar) → `AppConfig.Default` completo; `pollingIntervalSeconds` negativo
   o cero con el resto de campos válidos → solo el intervalo cae a 60s, el
   resto de campos configurados se conservan; `windowPosition` con
   `left`/`top` ausentes o no numéricos → `Position == null` (resto de
   campos conservados); `Save()` seguido de `Load()` sobre el mismo `configPath`
   round-trips los 3 campos; `SavePosition()` sobre un fichero ya existente
   conserva el intervalo/chime previos y solo actualiza la posición; limpiar
   cada fichero temporal en un `finally`.
5. **`Windowing/WindowPositionResolver.cs` (Desktop, US-1, xUnit puro):**
   crear tal cual Data Model.
6. **Tests de `WindowPositionResolver`:** crear
   `test/ClaudeMeter.Desktop.Tests/Windowing/WindowPositionResolverTests.cs`
   con `Rect` simulados (sin ningún monitor real — AC heredado de la
   Definition of Done). Casos: sin posición configurada → esquina inferior
   derecha del `primaryScreenWorkArea`; posición configurada que cabe en una
   pantalla secundaria de la lista `allScreenWorkAreas` → se usa tal cual;
   posición configurada que no cabe en ninguna pantalla de la lista (p. ej.
   quedó guardada con un segundo monitor ya desconectado) → fallback a la
   esquina del monitor principal, no a cualquier otro monitor disponible.
7. **`Windowing/Win32ScreenInfo.cs` (Desktop, US-1):** crear tal cual Data
   Model, corrigiendo la firma del delegado según la nota de implementación
   (pasar el `hMonitor` real a `GetMonitorInfo`, no el `RECT`). Sin test
   automatizado (ver Data Model) — validación manual en Definition of Done.
8. **`MainWindow.xaml.cs` (US-1):** sustituir el cálculo fijo por la llamada
   a `WindowPositionResolver.Resolve(...)` tal cual Data Model.
9. **`Audio/IChimePlayer.cs` + `Audio/SystemSoundChimePlayer.cs` (Desktop,
   US-1):** crear en `src/ClaudeMeter.Desktop/Audio/` tal cual Data Model.
10. **`UsagePage.razor` — parte de intervalo y chime (US-1):** inyectar
    `AppConfig`/`IChimePlayer`, sustituir `RefreshInterval` por
    `Config.PollingInterval`, añadir `_previousSessionThreshold`/
    `_previousWeeklyThreshold` y la lógica de `Apply()` descrita en Data
    Model.
11. **Tests bUnit de US-1 (chime):** en
    `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`, registrar un
    `FakeChimePlayer : IChimePlayer` (test double con un contador `PlayCount`,
    mismo espíritu que `FakeUsageDataSource` ya existente en
    `TestDoubles/`) en el contenedor bUnit. Casos mínimos: secuencia
    `Warning→Critical` con `ChimeEnabled=true` → `PlayCount == 1`;
    `Critical→Critical` (dos snapshots consecutivos en rojo) → `PlayCount`
    sigue en 1 (no se repite); `Critical→Warning→Critical` → `PlayCount == 2`;
    misma secuencia con `ChimeEnabled=false` → `PlayCount == 0` en todos los
    casos; un `RequestFailed` intercalado entre dos snapshots `Critical` no
    duplica el chime (verifica que `_previousXThreshold` no se resetea en la
    rama de fallo). También añadir un caso de intervalo: construir
    `UsagePage` con un `AppConfig` de intervalo distinto de 60s inyectado y
    comprobar (vía el mismo mecanismo interno ya usado por
    `UsagePollingCoordinatorTests`) que el `UsagePollingCoordinator`
    resultante usa ese intervalo.
12. **`Windowing/WindowDragService.cs` (Desktop, US-2):** crear tal cual
    Data Model.
13. **Tests de `WindowDragService` (US-2, xUnit puro):** crear
    `test/ClaudeMeter.Desktop.Tests/Windowing/WindowDragServiceTests.cs`.
    Como `Window.DragMove()`/`Window.Dispatcher` no son sustituibles de
    forma trivial en xUnit puro (requieren un `Window` WPF real con
    handle), estos tests se limitan a la parte determinista y desacoplada
    de WPF: construir `WindowDragService` con un `AppConfigStore` apuntando
    a un fichero temporal y verificar, invocando directamente
    `SavePosition(left, top)` sobre el `AppConfigStore` (sin pasar por
    `BeginDrag()`/`DragMove()`), que el fichero resultante contiene esas
    coordenadas — la integración real de `BeginDrag()` con `DragMove()`
    queda en la validación manual de US-2 (Definition of Done, ya prevista
    como no automatizable).
14. **`wwwroot/js/drag.js` + adición a `wwwroot/index.html` (US-2):** tal
    cual Data Model.
15. **`App.xaml.cs` (US-1 + US-2, composition root):** añadir los 4
    registros nuevos al `IServiceCollection` tal cual Data Model.
16. **`UsagePage.razor` — parte de arrastre (US-2):** inyectar
    `WindowDragService`/`IJSRuntime`, añadir `OnAfterRenderAsync` y el
    `Dispose()` del `DotNetObjectReference` tal cual Data Model.
17. **Tests bUnit del registro de interop (US-2):** en
    `UsagePageTests.cs`, verificar (usando el modo `Loose` del `JSInterop`
    de `BunitContext` — o configurando explícitamente la invocación
    esperada `claudeMeterDrag.init` si el modo `Loose` no estuviera
    disponible en la versión de bUnit ya fijada, 2.11.3 según F1 — sin
    necesitar ningún mock de WebView2 real) que `OnAfterRenderAsync` invoca
    `claudeMeterDrag.init` exactamente una vez tras el primer render, no en
    renders posteriores.
18. **Validación de compilación:** `dotnet build ClaudeMeter.sln` en Debug y
    Release (`TreatWarningsAsErrors`), prestando atención a que
    `Win32ScreenInfo`/`WindowDragService` (con P/Invoke y tipos de
    `System.Windows`) no introduzcan advertencias de analizadores nuevas, y
    a que ningún `PackageReference` nuevo se haya añadido por error (este
    ciclo no necesita ninguno).

## Cross-Cutting Concerns

- **Seguridad:** `config.json` no contiene ningún dato sensible (intervalo,
  posición de ventana, booleano de chime) — a diferencia de
  `.credentials.json`, no requiere ningún tratamiento especial de
  confidencialidad. Los mensajes de log de `AppConfigStore` nunca vuelcan el
  contenido completo del fichero, solo el nombre del campo afectado y, para
  el intervalo, su valor numérico (no sensible).
- **Rendimiento:** la escritura de `config.json` tras un arrastre ocurre
  como mucho una vez por gesto de arrastre completado (nunca por tick de
  poll), y el fichero es trivial (3 campos planos) — sin impacto medible.
  `EnumDisplayMonitors` se invoca una única vez por arranque de
  `MainWindow`, no en cada ciclo de poll.
- **Manejo de errores:** `AppConfigStore.Load()`/`Save()`/`SavePosition()`
  nunca lanzan hacia el llamador (AC explícito de ambas user stories) —
  cualquier fallo de E/S o de parseo se registra y se absorbe, preservando
  la misma filosofía de sustituibilidad sin excepciones ya aplicada a
  `IUsageDataSource` en `CLAUDE.md`, extendida aquí por analogía a
  configuración. `WindowDragService.BeginDrag()` atrapa específicamente la
  única excepción documentada de `DragMove()` (botón ya no pulsado al
  invocar) sin dejarla escapar hacia el interop de Blazor.
- **Observabilidad/logging:** reutiliza Serilog ya disponible desde
  Ciclo A, sin sinks ni configuración nuevos. Niveles: `Warning` (JSON
  corrupto, campo fuera de rango, fallo de escritura no crítico,
  `BeginDrag()` invocado sin ventana adjunta — carrera de arranque
  improbable pero no catastrófica), `Error` (fallo de escritura de disco
  tras un arrastre, ya que el usuario sí ejecutó una acción explícita que
  esperaba persistencia). No se añade ningún log de `Information`/`Debug`
  nuevo por ciclo de poll — el intervalo configurado simplemente sustituye
  el valor ya logueado por `UsagePollingCoordinator.Start()` desde Ciclo A.
- **Compatibilidad hacia atrás:** el constructor de `UsagePollingCoordinator`
  no cambia de firma (sigue recibiendo un `TimeSpan interval`; solo cambia
  qué valor le pasa `UsagePage.razor`). Los constructores que sí cambian de
  firma son los de `MainWindow` (ya no dependiente solo de `App.Services`,
  también de `AppConfig`/`Win32ScreenInfo`/`WindowDragService`, todos
  resueltos internamente, sin nuevos parámetros de constructor visibles
  desde fuera) y `UsagePage.razor` (nuevos `@inject`) — ya identificados en
  Implementation Plan (tests de `UsagePageTests.cs` a actualizar con los
  nuevos servicios registrados en el contenedor bUnit).

## Risks & Open Decisions

**Riesgos (heredados del documento de requisitos, con valoración de
mitigación de este diseño):**

- **Data quality (US-1):** `config.json` corrupto o mal formado cae a
  valores por defecto, nunca provoca un crash — mitigado por diseño con el
  modelo de fallback por campo de `AppConfigStore.Load()` (ver Data Model).
  Impact: Low tras la mitigación (bajó desde el riesgo genérico marcado en
  requisitos).
- **UX / multi-monitor (US-1):** una posición persistida que deja de ser
  válida (monitor desconectado, cambio de resolución) cae al monitor
  principal en vez de dejar la ventana inaccesible — mitigado
  estructuralmente por `WindowPositionResolver` (xUnit puro, testeado con
  varios escenarios de pantalla). Impact: Low tras la mitigación de diseño.
- **Persistencia (US-2):** un fallo de escritura a disco al persistir la
  posición tras un arrastre no crashea la aplicación ni revierte el
  reposicionamiento en memoria — mitigado por diseño (`Save()` atrapa
  `IOException`/`UnauthorizedAccessException` y nunca lanza; el
  reposicionamiento visual ya ocurrió vía `DragMove()` antes de intentar
  persistir, así que un fallo de escritura no puede revertirlo). Impact: Low.
- **Compatibilidad arrastre vs. `BlazorWebView`/WebView2 ("airspace"):**
  resuelto arquitectónicamente por el mecanismo JS interop → `DragMove()`
  elegido en Technology Choices, con precedente de la comunidad
  (`dotnet/maui#1304`) para el mismo problema. Impact: Medium hasta la
  validación manual real en Windows (ya prevista en la Definition of Done
  heredada) — ningún agente de este pipeline puede confirmar que el gesto
  se sienta fluido/sin parpadeo visual del contenido WebView2 durante el
  arrastre (comportamiento esperable y estándar en cualquier ventana con
  contenido Chromium embebido mientras dura un `DragMove()`, pero solo
  verificable con la aplicación real corriendo).
- **Interacción futura con click-through (#16, F3):** el mecanismo de
  arrastre elegido (listener JS a nivel de `document`, sin distinguir
  ninguna región "no arrastrable") tendrá que revisarse cuando F3 implemente
  click-through — un modo click-through activo probablemente necesite
  suspenderse temporalmente durante el gesto de arrastre (o el propio
  arrastre deje de ser posible mientras esté activo, a decidir por el
  diseño de F3). Impact: Low para este ciclo (no bloquea nada hoy,
  `MainWindow` sigue siendo completamente interactiva), documentado para no
  perder contexto de cara al documento de diseño de F3.
- **Escalado DPI en configuraciones multi-monitor mixtas (US-1):**
  limitación de plataforma conocida, con el mismo tratamiento que el riesgo
  de "airspace" ya documentado en el diseño de F1 para la transparencia de
  `MainWindow` — aceptada explícitamente por el usuario, no bloquea
  Development. `WindowPositionResolver`/`Win32ScreenInfo` comparan la
  posición configurada (unidades independientes de dispositivo de WPF,
  `Window.Left`/`Top`) contra las áreas de trabajo devueltas por
  `GetMonitorInfo` (píxeles físicos del monitor). Con un único factor de
  escala o monitores homogéneos no hay ningún problema práctico porque WPF
  ya aplica transformaciones DPI consistentes; en un sistema con
  **monitores a distinto factor de escala** (p. ej. portátil a 150% +
  monitor externo a 100%) el chequeo "¿la posición configurada cabe en este
  monitor?" puede ser impreciso cerca de los bordes entre monitores. Este
  diseño **no** implementa una transformación DPI-por-monitor completa
  (pieza de complejidad real, sin ningún AC que la pida explícitamente) —
  el comportamiento resultante en ese escenario sigue siendo seguro (en el
  peor caso, cae al fallback del monitor principal en vez de colocar la
  ventana en la posición exacta esperada), nunca deja la ventana
  inaccesible. El usuario ha confirmado que acepta este trade-off tal cual
  se planteó, como limitación conocida — no se amplía el alcance de este
  ciclo con una transformación DPI-por-monitor completa. Impact: Low, ya
  aceptado.
- **Rendimiento:** N/A, sin cambios respecto a la valoración heredada —
  ninguna de las dos user stories añade llamadas de red ni trabajo por
  ciclo de poll adicional.

**Decisiones de diseño ya tomadas en este documento (con rationale, no
requieren validación humana adicional antes de Development):**

- Mecanismo de arrastre (JS interop `mousedown` → `DragMove()`, en vez de
  una franja WPF nativa o regiones de arrastre de WebView2) — la decisión
  que el documento de requisitos delegaba explícitamente a Design, resuelta
  con su rationale completo en Technology Choices.
- Ubicación de `AppConfig`/`AppConfigStore` en Desktop, no como puerto de
  Application — continuidad directa del mismo criterio ya aplicado a
  `SerilogConfigurator` en Ciclo A.
- Enumeración de monitores vía P/Invoke directo en vez de
  `System.Windows.Forms.Screen` — evita activar `UseWindowsForms` de forma
  prematura; candidato a revisar en F3 cuando el icono de bandeja sí lo
  justifique.
- Sonido del chime: `System.Media.SystemSounds.Exclamation`, sin asset
  `.wav` propio — decisión de producto menor delegada por la issue ("un
  sonido audible" sin más especificación), documentada con su alternativa
  rechazada (asset propio, candidato a F3/pulido de UX).
- Ruta por defecto de `config.json`:
  **`%LOCALAPPDATA%\ClaudeMeter\config.json`** — adoptada tal cual la
  sugerencia ya razonada por el documento de requisitos (misma carpeta base
  que los logs de Ciclo A), sin alternativa real que la supere en
  consistencia con el resto del proyecto.

**Limitaciones de plataforma conocidas y aceptadas (heredan el mismo
tratamiento que el "airspace" de F1 — riesgo de plataforma documentado,
mitigado por diseño donde es posible, aceptado explícitamente donde no lo
es; no requieren sign-off adicional):**

- "Airspace" de arrastre vs. `BlazorWebView`/WebView2 — ver riesgo
  correspondiente arriba (mitigado arquitectónicamente, pendiente solo de
  validación manual real, no de una decisión de producto).
- Escalado DPI en configuraciones multi-monitor mixtas — ver riesgo
  correspondiente arriba (**confirmado por el usuario**: acepta el
  trade-off tal cual se planteó, no se amplía el alcance de este ciclo con
  una transformación DPI-por-monitor completa).

Sin decisiones de producto/arquitectura genuinamente abiertas: las 3
delegadas explícitamente a Design por el documento de requisitos (mecanismo
de arrastre, ruta/tecnología de `config.json`, y el disparador del chime ya
confirmado antes de este documento) quedan resueltas arriba con su
rationale, y la única limitación de alcance real (DPI multi-monitor) ya
queda confirmada por el usuario como aceptada — no bloquea `sdlc-development`.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados (xUnit puro para el lector/parser de
      `config.json` — valores por defecto, fallback ante JSON corrupto o
      fuera de rango, fallback de posición fuera de pantalla — y para la
      lógica de persistencia de posición tras arrastre) según el mínimo
      configurado en `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada depende de un `config.json` real del
      usuario ni de un monitor/resolución real: los tests usan ficheros
      temporales o valores simulados de área de trabajo
- [x] Disparador del chime (transición a umbral Crítico ≥90%, una sola vez
      por transición) — confirmado por el usuario (ver Dependencies): no
      quedan "Open question" pendientes en este documento
- [x] **Limitación conocida y aceptada (US-1, no requiere validación ni
      trabajo adicional):** el escalado DPI en configuraciones multi-monitor
      mixtas (monitores a distinto factor de escala) puede hacer impreciso
      el cálculo de "¿la posición configurada cabe en este monitor?" cerca
      de los bordes entre monitores — nunca deja la ventana inaccesible (cae
      al fallback del monitor principal), y el usuario ha confirmado que
      acepta este trade-off tal cual se planteó en Risks & Open Decisions,
      sin ampliar el alcance de este ciclo con una transformación
      DPI-por-monitor completa. Mismo tratamiento que el "airspace" de
      WebView2 ya documentado como limitación conocida en el diseño de F1 —
      Testing/Documentation deben heredar esta limitación tal cual, no
      tratarla como una regresión a corregir
- [ ] **Validación manual (US-1):** se confirma, ejecutando la aplicación
      real en Windows, que un `config.json` con intervalo, posición y
      chime personalizados efectivamente cambia el comportamiento del
      widget (poll al ritmo configurado, ventana en la posición indicada,
      chime sonando/silencioso según el interruptor) — ningún agente de
      este pipeline puede verificar renderizado ni audio real de
      WPF/WebView2 de forma automática
- [ ] **Validación manual (US-2):** se confirma, arrastrando el widget con
      el ratón en un equipo Windows real y reiniciando la aplicación
      después, que la ventana reaparece exactamente en el punto donde se
      soltó el arrastre — validación de interacción de ratón en tiempo
      real que ningún agente de este pipeline puede ejecutar de forma
      automática
