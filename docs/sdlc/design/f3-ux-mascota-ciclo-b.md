# Design: F3 — UX + página Mascota, Ciclo B

## Requirements Reference

`docs/sdlc/requirements/f3-ux-mascota-ciclo-b.md` — cubre US-1 (issue #16,
click-through de la ventana) y US-2 (issue #17, icono de bandeja con
Pausar/Reanudar/Recargar/Salir + cierre directo desde `MainWindow`).
Documento cerrado, sin "Open question" pendientes: las tres ambigüedades que
la propia issue #17 dejaba abiertas (persistencia de pausa/click-through,
mecanismo del icono de bandeja, gesto exacto de cierre) ya vienen resueltas
por Requirements con un valor por defecto documentado, salvo el mecanismo
concreto del icono de bandeja (`NotifyIcon`+`UseWindowsForms` vs.
`Hardcodet.NotifyIcon.Wpf`), que Requirements deja explícitamente para este
documento.

Quedan fuera de alcance: #18 (`ScreenNavigator`/`IWidgetScreen`) y #19
(`MascotPage.razor`) — Ciclo C posterior.

## Architecture Overview

Este ciclo no toca Domain, Application ni Infrastructure en absoluto
(confirmado por inspección: ninguna de las dos user stories necesita datos
nuevos de `RateLimitWindow`/`UsageSnapshot`, ni ningún cambio de
`IUsageDataSource`). Todo el trabajo vive en Desktop, y extiende tres
patrones ya establecidos en ciclos anteriores en vez de introducir
mecanismos nuevos donde uno ya existente sirve:

- **El patrón "servicio singleton de DI + `AttachWindow(Window)`"** ya usado
  por `WindowDragService` (F2/Ciclo B) y `WindowResizeService` (F3/Ciclo A)
  se reutiliza tal cual para **`ClickThroughService`** (US-1) y
  **`WindowCloseService`** (US-2, cierre directo) — ambos necesitan actuar
  sobre la `MainWindow` real, que no existe todavía cuando `App.OnStartup`
  construye el `IServiceCollection` (mismo motivo ya documentado en
  `docs/sdlc/design/f2-robustez-ciclo-b.md`).
- **El patrón "JS interop → `[JSInvokable]` → servicio de Desktop"** ya usado
  por `drag.js`/`WindowDragService` y `resize.js`/`WindowResizeService` se
  reutiliza para el gesto de cierre directo (`close.js` nuevo →
  `WindowCloseService.RequestClose()`). El click-through, en cambio, **no**
  necesita JS/interop: se activa/desactiva íntegramente desde el menú de
  bandeja (WinForms puro), un mundo por completo ajeno a Blazor/WebView2 —
  ver Technology Choices.
- **Un puente nuevo, sin precedente exacto en ciclos anteriores:**
  `UsagePollingCoordinator` es una instancia por componente, creada dentro
  de `UsagePage.OnInitialized()` (no un singleton de DI, confirmado por
  inspección) — el icono de bandeja (que vive fuera de Blazor, en
  `App.xaml.cs`/WinForms) no tiene ninguna referencia directa a esa
  instancia. Se introduce **`PollingControlService`**, un singleton de DI al
  que `UsagePage` "adjunta" su coordinador real (mismo verbo/patrón que
  `AttachWindow`, aplicado aquí a un `UsagePollingCoordinator` en vez de a
  una `Window`), y que el icono de bandeja usa como único punto de entrada
  para Pausar/Reanudar/Recargar.

Piezas nuevas/modificadas, de fuera hacia dentro:

1. **`ClaudeMeter.Desktop.Windowing.ClickThroughService`** (Desktop, nuevo)
   — US-1. Alterna el estilo extendido `WS_EX_TRANSPARENT` del `HWND` real
   de `MainWindow` vía P/Invoke a `user32.dll` (`GetWindowLongPtr`/
   `SetWindowLongPtr`). Por defecto `IsEnabled = false` y **no toca la
   ventana en absoluto** hasta el primer `SetEnabled(true)` — arranque
   idéntico al comportamiento actual (AC de US-1). Expone `IsEnabled` y un
   evento `StateChanged` para que `TrayIconService` mantenga el `Checked`
   del ítem de menú sincronizado.
2. **`ClaudeMeter.Desktop.Windowing.WindowCloseService`** (Desktop, nuevo) —
   US-2 (cierre directo). Análogo a `WindowDragService`: expone
   `[JSInvokable] RequestClose()`, invocado desde `close.js` al pulsar un
   botón de cierre discreto superpuesto al contenido Razor. Llama a
   `System.Windows.Application.Current.Shutdown()` — el mismo punto de
   salida que usa "Salir" desde la bandeja (ver más abajo, converge en
   `App.OnExit`).
3. **`ClaudeMeter.Desktop.Polling.PollingControlService`** (Desktop, nuevo)
   — puente entre el icono de bandeja y el `UsagePollingCoordinator` real
   (US-2, Pausar/Reanudar/Recargar). `UsagePage.OnInitialized()` lo
   "adjunta" tras crear su coordinador; `TrayIconService` lo usa para
   `TogglePause()`/`RequestReload()` y se suscribe a `PauseStateChanged`
   para actualizar el texto del ítem de menú.
4. **`ClaudeMeter.Desktop.Polling.UsagePollingCoordinator`** (Desktop,
   modificado) — añade `Pause()` (detiene el timer sin `Dispose()`, AC de
   "Pausar") y `PollNow()` (dispara un ciclo de poll fuera de la cadencia
   del timer, AC de "Recargar"; `Start()` ya cubre "Reanudar" tal cual pide
   la propia AC — "mismo comportamiento que el `Start()` inicial", así que
   no se añade un método `Resume()` distinto). El método interno
   `PollOnceForTestsAsync()` (ya existente) pasa a delegar en `PollNow()`.
5. **`ClaudeMeter.Desktop.Tray.TrayIconService`** (Desktop, nuevo) — US-2.
   Única clase del proyecto que referencia `System.Windows.Forms`
   (`NotifyIcon`/`ContextMenuStrip`/`ToolStripMenuItem`). Construye el menú
   (Pausar/Reanudar, Recargar, click-through, Salir) en el constructor
   (sin tocar `NotifyIcon.Visible`, para que sea instanciable en tests sin
   sesión de escritorio real); `Initialize()` (llamado una vez desde
   `App.OnStartup`) resuelve el icono y activa `Visible = true`. `Dispose()`
   oculta y libera el `NotifyIcon` — invocado desde `App.OnExit` (US-2,
   AC de "icono de bandeja huérfano").
6. **`Pages/UsagePage.razor`** (Desktop, modificado) — (a) adjunta su
   `UsagePollingCoordinator` recién creado a `PollingControlService`; (b) se
   suscribe a `PauseStateChanged` para marcar `_isStale = true` al pausar
   (reutiliza el criterio visual existente, AC de US-2); (c) registra el
   listener JS de `close.js` en `OnAfterRenderAsync`, igual que ya hace con
   `claudeMeterDrag`/`claudeMeterResize`; (d) añade el botón de cierre
   discreto dentro de `.claudemeter-root`.
7. **`wwwroot/js/close.js`** (Desktop, nuevo) — registra el listener de
   clic sobre el botón de cierre y reenvía el gesto a
   `WindowCloseService.RequestClose()`.
8. **`wwwroot/css/app.css`** (Desktop, modificado) — añade `.claudemeter-close`
   (botón de cierre, visible solo en hover/foco) y `position: relative` en
   `.claudemeter-root` para poder posicionarlo.
9. **`MainWindow.xaml.cs`** (Desktop, modificado) — adjunta
   `ClickThroughService`/`WindowCloseService` a `this`, mismo patrón ya
   usado para `WindowDragService`/`WindowResizeService`.
10. **`App.xaml.cs`** (Desktop, modificado) — registra los 4 servicios
    nuevos; llama a `TrayIconService.Initialize()` al final de `OnStartup`
    (AC: "al completarse `App.OnStartup`, aparece el icono") y a
    `TrayIconService.Dispose()` al principio de `OnExit`, antes de
    `Log.CloseAndFlush()`.
11. **`ClaudeMeter.Desktop.csproj`** (Desktop, modificado) — añade
    `<UseWindowsForms>true</UseWindowsForms>` (ver Technology Choices para
    la decisión frente a `Hardcodet.NotifyIcon.Wpf`).

Diagrama de dependencias resultante (piezas nuevas/modificadas resaltadas):

```
Desktop
 ├── Windowing/
 │    ├── ClickThroughService.cs (nuevo) ──> P/Invoke user32.dll (GWL_EXSTYLE)
 │    └── WindowCloseService.cs (nuevo) ──> System.Windows.Application.Current.Shutdown()
 ├── Polling/
 │    ├── PollingControlService.cs (nuevo) ──> adjunta el UsagePollingCoordinator real
 │    └── UsagePollingCoordinator.cs (modificado) ──> + Pause(), + PollNow()
 ├── Tray/
 │    └── TrayIconService.cs (nuevo) ──> System.Windows.Forms.NotifyIcon,
 │                                         PollingControlService, ClickThroughService
 ├── wwwroot/js/close.js (nuevo)
 ├── wwwroot/css/app.css (modificado) ──> .claudemeter-close
 ├── Pages/UsagePage.razor (modificado) ──> PollingControlService, WindowCloseService, botón de cierre
 ├── MainWindow.xaml.cs (modificado) ──> ClickThroughService.AttachWindow, WindowCloseService.AttachWindow
 ├── App.xaml.cs (modificado) ──> registra los 4 servicios, TrayIconService.Initialize()/Dispose()
 └── ClaudeMeter.Desktop.csproj (modificado) ──> UseWindowsForms=true

Domain / Application / Infrastructure: sin cambios.
```

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| **Icono de bandeja: `System.Windows.Forms.NotifyIcon` + `<UseWindowsForms>true</UseWindowsForms>`** | (1) Cero paquetes NuGet nuevos — `NotifyIcon`/`ContextMenuStrip`/`ToolStripMenuItem` forman parte del SDK de .NET ya instalado, coherente con el patrón de minimizar dependencias que este proyecto ya ha aplicado varias veces (rechazó `Newtonsoft.Json`, `Polly`, un `.wav` propio, y — el precedente más directo — `System.Windows.Forms.Screen.AllScreens` en F2/Ciclo B, cuyo propio documento de diseño dejó escrito explícitamente: *"se reconsiderará en F3, que según CLAUDE.md necesitará de todos modos un icono de bandeja (`System.Windows.Forms.NotifyIcon` es la vía estándar para esa función en WPF), momento en el que `UseWindowsForms` pasaría a estar justificado por una necesidad real adicional"* — ese momento es este ciclo. (2) `NotifyIcon`+WPF en el mismo hilo STA es un patrón ampliamente probado: ambos framework comparten el mismo bucle de mensajes Win32 subyacente (el `Dispatcher` de WPF), sin necesitar `System.Windows.Forms.Application.Run()`; no hace falta ningún hilo/mensaje-pump adicional. (3) Verificado contra Microsoft Learn (`NotifyIcon` Class, `windowsdesktop-8.0`): namespace `System.Windows.Forms`, ensamblado `System.Windows.Forms.dll`, propiedades `Icon`/`Visible`/`ContextMenuStrip`/`Text`, evento `Click`. | **`Hardcodet.NotifyIcon.Wpf` 2.0.1** (verificada en NuGet.org por Requirements) — rechazada: es 100% WPF/XAML (evita `UseWindowsForms`), pero introduce una dependencia de terceros nueva sin necesidad funcional no cubierta por la opción del SDK — y el propio historial de decisiones de este proyecto (ver cita de F2/Ciclo B arriba) ya señalaba `NotifyIcon` como la vía "estándar" a adoptar en cuanto F3 lo justificara. Se reconsideraría solo si en el futuro se necesitase un control de bandeja con *data binding*/plantillas XAML reales (p. ej. un menú con iconos por elemento o estados visuales complejos), algo que ninguna AC de este ciclo pide. |
| **Icono real del `NotifyIcon`: `System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)`, con `SystemIcons.Application` como *fallback*** | No existe hoy ningún asset `.ico`/`.png` en el repositorio (confirmado por búsqueda) y diseñar un icono de marca propio es una decisión de producto fuera del alcance de esta issue (que solo exige "aparece un icono en la bandeja"). `ExtractAssociatedIcon` (verificado en Microsoft Learn, `System.Drawing.Icon.ExtractAssociatedIcon(string)`, disponible en `windowsdesktop-8.0`) toma el icono ya embebido en `ClaudeMeter.Desktop.exe` (hoy el genérico de .NET, ya visible en la barra de tareas de cualquier `WinExe`) — cero asset nuevo que mantener, cero decisión de diseño de icono improvisada por este pipeline. | (1) Empaquetar un `.ico` de marca propia — rechazada por ahora: ninguna AC pide una identidad visual concreta; candidato natural para F3 posterior/F6 (empaquetado) si se decide una marca. (2) `SystemIcons.Application` directamente, sin intentar `ExtractAssociatedIcon` — rechazada como *único* mecanismo: mostraría el icono genérico de Windows en vez de reflejar mínimamente que el proceso es ClaudeMeter; se mantiene solo como *fallback* si la extracción fallara (p. ej. `ProcessPath` nulo en un contexto no estándar). |
| **`ClickThroughService` en `Windowing`, con las declaraciones `[DllImport]` de `GetWindowLongPtr`/`SetWindowLongPtr` directamente en la propia clase (no separadas en un adaptador aparte, a diferencia de `Win32ScreenInfo`)** | `Win32ScreenInfo` se separó de `WindowPositionResolver` porque este último tiene lógica de decisión real y no trivial (elegir la posición final entre varios monitores) que merece xUnit puro con rectángulos simulados. Aquí no hay ninguna lógica equivalente que separar: "activar/desactivar un bit del estilo extendido de una ventana" es una operación de una sola línea sin ninguna decisión de negocio — envolverla en un adaptador aparte solo para imitar la forma de F2/Ciclo B sería ceremonia sin beneficio real (mismo criterio de proporcionalidad que ya aplicó este repositorio para no separar `ThresholdClassifier` en F1). El guard de "sin ventana adjunta" (mismo patrón que `WindowDragService`) sí es la única rama testeable con xUnit puro; la llamada real a `user32.dll` queda como gap aceptado, igual que `Win32ScreenInfo`/`MainWindow`. | Adaptador `Win32WindowStyle` separado, con `ClickThroughService` como capa fina por encima — rechazada por la razón de proporcionalidad anterior; se reconsideraría si una tercera pieza de este proyecto necesitase manipular estilos de ventana Win32 además de click-through, momento en el que compartir el adaptador sí tendría un caso de reutilización real. |
| **`WS_EX_TRANSPARENT` (`0x00000020`) sobre `GWL_EXSTYLE` (`-20`), leyendo el estilo actual y aplicando OR/AND-NOT (nunca sobrescribiendo el valor completo)** | Es el mecanismo estándar de Win32 para hacer una ventana "click-through" (patrón ampliamente documentado — confirmado por búsqueda dirigida contra ejemplos de referencia de WPF). Leer el estilo actual y aplicar `| WS_EX_TRANSPARENT` / `& ~WS_EX_TRANSPARENT` (en vez de escribir un valor fijo) es obligatorio aquí porque `MainWindow` ya tiene otros bits del estilo extendido puestos por WPF (`WS_EX_LAYERED`, activado implícitamente por `AllowsTransparency="True"`, y `WS_EX_TOPMOST` por `Topmost="True"`) — sobrescribirlos sin conservarlos rompería la transparencia/topmost ya existentes. `WS_EX_TRANSPARENT` hace que el SO **nunca** entregue ningún mensaje de ratón a la ventana con independencia del alfa de cada píxel, así que **no contradice** el workaround `Rectangle Fill="#01000000"` ya aplicado en F1/F2 (ese workaround resuelve el hit-test *basado en alfa* de `UpdateLayeredWindow`; `WS_EX_TRANSPARENT` actúa en una capa distinta y anterior — cuando está activo, el hit-test por alfa ni siquiera llega a evaluarse). Al desactivarlo, el comportamiento vuelve exactamente al de hoy sin tocar el `Rectangle` en absoluto. | Ocultar la ventana (`Window.Visibility = Hidden`) mientras "click-through" está activo — rechazada: el AC de US-1 exige explícitamente que el widget siga renderizándose y actualizándose durante el click-through, no que desaparezca. |
| **`GetWindowLongPtr`/`SetWindowLongPtr` con *shim* de 32/64 bits (`IntPtr.Size == 8` ⇒ variantes `Ptr`; si no, `GetWindowLong`/`SetWindowLong` de 32 bits)** | `GetWindowLongPtr`/`SetWindowLongPtr` no existen como tales en Windows de 32 bits (son macros que en esa plataforma se resuelven a las versiones de 32 bits) — el *shim* estándar recomendado por la documentación de referencia de P/Invoke evita un truncamiento silencioso de puntero si este widget se llegase a publicar alguna vez para `win-x86` (el `.csproj` ya construye para varias RID vía las dependencias de WebView2, ver carpetas `runtimes/win-x86` ya presentes en `bin/`). Coste: unas pocas líneas más, cero riesgo de bug de plataforma. | Usar directamente `SetWindowLong`/`GetWindowLong` (firma de 32 bits) sin *shim* — rechazada: en 64 bits trunca el valor de retorno de `GetWindowLongPtr` real si el runtime resuelve el símbolo incorrecto, un bug de plataforma sutil y difícil de reproducir en desarrollo (ambas máquinas de desarrollo típicas son de 64 bits) que solo aparecería en producción de 32 bits. |
| **`PollingControlService` como singleton de DI con `AttachCoordinator(UsagePollingCoordinator)`/`DetachCoordinator(...)`, en vez de convertir `UsagePollingCoordinator` en un singleton de DI compartido** | `UsagePollingCoordinator` es deliberadamente una instancia por componente (ver su propio XMLDoc: *"nunca un singleton de DI... para que su ciclo de vida quede atado 1:1 al del componente"*), una decisión ya fijada por F2/Ciclo B con motivo explícito (aislar el ciclo de vida del componente Razor). Cambiarla a singleton para que `TrayIconService` la alcance directamente violaría esa decisión ya tomada sin necesidad real: el problema real es solo "¿cómo llega un evento de fuera de Blazor a la instancia correcta?", no "¿debería haber una única instancia global?". `PollingControlService` resuelve exactamente ese problema con el mismo verbo ya usado por `WindowDragService`/`WindowResizeService` (`Attach...`), sin tocar el ciclo de vida ya decidido de `UsagePollingCoordinator`. | Convertir `UsagePollingCoordinator` en singleton de `IServiceCollection` — rechazada por la razón anterior; además complicaría innecesariamente el `Dispose()` de `UsagePage` (hoy simétrico: crea y libera su propia instancia) sin ningún beneficio para este ciclo. |
| **Al pausar: `UsagePage` marca `_isStale = true` de inmediato (reutilizando el criterio visual "(desactualizado)" ya existente); al reanudar, `_isStale` no se toca hasta que el fetch inmediato de `Start()` complete por el cauce normal (`OnSnapshotReceived`/`Apply`)** | AC explícita de US-2: "mismo criterio visual que hoy usa `_isStale`". Es también la resolución más simple al riesgo que el propio documento de Requisitos deja explícitamente abierto para Design ("¿vale la pena distinguir visualmente 'pausado' de 'stale por fallo transitorio'?"): no distinguirlos evita introducir un tercer estado visual (y una tercera clase CSS/mensaje) que ninguna AC pide, y reutiliza al 100% el trabajo visual ya hecho en F2/Ciclo B. Al reanudar, no forzar `_isStale = false` antes de que el primer fetch tras la pausa resuelva evita un parpadeo "al día" → "desactualizado" si ese primer fetch fallase (p. ej. token expirado durante la pausa) — el dato mostrado seguiría siendo el mismo snapshot antiguo hasta que uno nuevo (con éxito o no) llegue realmente. | Un tercer estado visual "Pausado" distinto de "(desactualizado)" (p. ej. una etiqueta o color propios) — rechazada explícitamente: el AC de Requirements ya decide reutilizar `_isStale`, y el propio Risk de Requirements deja la distinción como opcional a discreción de Design, no como requisito — no hay AC que la exija, así que añadirla sería alcance no pedido (regla de proporcionalidad). |
| **Botón de cierre directo: `<button class="claudemeter-close">` dentro de `.claudemeter-root` (visible solo en `:hover`/`:focus-visible`, vía CSS puro), con `close.js` reenviando el evento `click` del botón a `WindowCloseService.RequestClose()` — exactamente la sugerencia ya propuesta por el documento de Requisitos** | Evita reabrir el mismo problema de "airspace" WebView2 ya documentado en F2/Ciclo B para el arrastre (un gesto capturado a nivel de `Window` WPF nunca llegaría, `BlazorWebView` se queda con el evento primero) — la solución ya probada (JS interop → `[JSInvokable]`) se reutiliza sin inventar un mecanismo nuevo. `close.js` llama también a `event.stopPropagation()` en el propio `pointerdown` del botón, **antes** de que ese evento burbujee hasta el `document.documentElement` donde `drag.js` escucha `pointerdown` — sin este `stopPropagation()`, cada clic en el botón de cierre dispararía también `WindowDragService.BeginDrag()`/`EndDrag()` (un arrastre de longitud cero, inofensivo pero con una escritura de `config.json` innecesaria en cada cierre). No requiere ningún cambio en `drag.js` — el `stopPropagation()` en el listener del propio botón, registrado en la fase de burbuja antes de llegar al documento, ya es suficiente por semántica estándar del DOM. | (1) Modificar `drag.js` para ignorar `pointerdown` cuyo `target` sea `.claudemeter-close` — rechazada: exigiría tocar un fichero ya "cerrado" de un ciclo anterior por una razón que se puede resolver íntegramente dentro del fichero nuevo (`close.js`), manteniendo el principio de menor superficie de cambio. (2) Región de arrastre nativa de WebView2 con una exclusión para el botón — rechazada por el mismo motivo ya documentado en F2/Ciclo B (mayor complejidad de wiring, comportamiento de barra de título real no pedido). |
| **`WindowCloseService.RequestClose()` y el ítem "Salir" del menú de bandeja llaman ambos, de forma directa, a `System.Windows.Application.Current.Shutdown()` — sin lógica de cierre propia en ninguno de los dos sitios** | AC explícita: "cerrar desde el widget produce el mismo efecto que 'Salir' desde la bandeja" — la única forma de garantizarlo sin duplicar lógica es que ambos caminos converjan en la misma llamada, dejando que `App.OnExit` (ya existente, único punto de "limpieza de salida" del proceso) haga el resto. `Application.Shutdown()` dispara `OnExit` con independencia de `ShutdownMode` (por defecto `OnLastWindowClose`, pero `Shutdown()` fuerza el cierre explícitamente) — comportamiento estándar de WPF, ya insinuado por la propia Technical Note del documento de Requisitos. `App.OnExit` se extiende con una única línea nueva (`TrayIconService.Dispose()`, ver AC de "icono huérfano"), delante de las dos ya existentes (`_httpClient?.Dispose()`, `Log.CloseAndFlush()`) — cubre las tres vías de cierre (directo, "Salir", cualquier otra ruta de cierre de WPF) con un solo punto de mantenimiento. | Que `WindowCloseService.RequestClose()` cierre solo `MainWindow` (`Window.Close()`) en vez de llamar a `Shutdown()` directamente, confiando en `ShutdownMode.OnLastWindowClose` para terminar el proceso — rechazada: es un camino indirecto hacia el mismo resultado que además dependería de que nadie cambie `ShutdownMode` en el futuro (p. ej. si Ciclo C añadiese una segunda `Window`); llamar a `Shutdown()` explícitamente en los dos puntos es más corto y no depende de una configuración implícita. |

## Data Model / Interfaces

### `Windowing/ClickThroughService.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Windowing/ClickThroughService.cs
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Alterna el estilo extendido WS_EX_TRANSPARENT del HWND real de
/// MainWindow (US-1, issue #16). Por defecto IsEnabled=false y no toca la
/// ventana hasta el primer SetEnabled(true) -- arranque idéntico al
/// comportamiento actual (AC de US-1). No contradice el workaround
/// Rectangle Fill="#01000000" de MainWindow.xaml (F1/F2): ese workaround
/// resuelve el hit-test basado en alfa de UpdateLayeredWindow;
/// WS_EX_TRANSPARENT actúa en una capa anterior y hace que el SO no
/// entregue ningún mensaje de ratón a la ventana en absoluto, con
/// independencia del alfa de cada píxel.
/// </summary>
public sealed class ClickThroughService
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;

    private readonly ILogger<ClickThroughService> _logger;
    private Window? _window;

    public ClickThroughService(ILogger<ClickThroughService> logger) => _logger = logger;

    public bool IsEnabled { get; private set; }

    /// <summary>Se levanta tras un SetEnabled que sí llegó a aplicarse, para que TrayIconService sincronice el Checked del ítem de menú (AC de US-1).</summary>
    public event Action? StateChanged;

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    public void Toggle() => SetEnabled(!IsEnabled);

    public void SetEnabled(bool enabled)
    {
        if (_window is null)
        {
            _logger.LogWarning("SetEnabled invocado antes de que MainWindow estuviera adjunta; se ignora");
            return;
        }

        if (enabled == IsEnabled)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(_window).Handle;
        var currentStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var newStyle = enabled ? currentStyle | WsExTransparent : currentStyle & ~WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(newStyle));

        IsEnabled = enabled;
        StateChanged?.Invoke();
    }

    // Shim de 32/64 bits: GetWindowLongPtr/SetWindowLongPtr no existen como
    // tales en Windows de 32 bits (ver Technology Choices).
    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newValue) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, newValue) : new IntPtr(SetWindowLong32(hWnd, nIndex, newValue.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: permite a xUnit simular una transición de estado sin un HWND real, para probar que TrayIconService reacciona a StateChanged (ver TrayIconServiceTests).</summary>
    internal void SetEnabledForTests(bool enabled)
    {
        IsEnabled = enabled;
        StateChanged?.Invoke();
    }
}
```

### `Windowing/WindowCloseService.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Windowing/WindowCloseService.cs
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Punto de entrada del gesto de cierre directo (US-2), reenviado desde
/// <c>wwwroot/js/close.js</c>. Converge en el mismo punto de salida que
/// "Salir" desde la bandeja (<see cref="System.Windows.Application.Shutdown"/>),
/// que a su vez dispara <c>App.OnExit</c> -- así ambas vías producen
/// exactamente el mismo efecto observable (AC de US-2).
/// </summary>
public sealed class WindowCloseService
{
    private readonly ILogger<WindowCloseService> _logger;
    private Window? _window;

    public WindowCloseService(ILogger<WindowCloseService> logger) => _logger = logger;

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    [JSInvokable]
    public void RequestClose()
    {
        if (_window is null)
        {
            _logger.LogWarning("RequestClose invocado antes de que MainWindow estuviera adjunta; se ignora");
            return;
        }

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.Invoke(RequestClose);
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }
}
```

### `Polling/UsagePollingCoordinator.cs` (Desktop, modificado — excerpt)

```csharp
// src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs

/// <summary>
/// Detiene el timer sin liberar recursos (AC de "Pausar"): a diferencia de
/// <see cref="Dispose"/>, la instancia sigue viva y <see cref="Start"/>
/// puede volver a invocarse después para reanudar (AC de "Reanudar": mismo
/// comportamiento que el Start() inicial, sin un método Resume() aparte).
/// </summary>
public void Pause()
{
    _logger.LogInformation("Polling de uso pausado");
    _timer.Stop();
}

/// <summary>
/// Dispara un ciclo de poll adicional fuera de la cadencia del timer, sin
/// reiniciarlo ni afectar a su próximo disparo (AC de "Recargar"). Reutiliza
/// el guard <c>_isPolling</c> ya existente: si ya hay un poll en vuelo
/// (manual o del propio timer), esta llamada es un no-op silencioso -- el
/// mismo comportamiento que ya tenía un tick del timer que coincidiera con
/// una llamada en curso.
/// </summary>
public Task PollNow() => PollAsync();

/// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: ahora delega en <see cref="PollNow"/> (mismo método que usa producción), en vez de duplicar la llamada a <c>PollAsync()</c>.</summary>
internal Task PollOnceForTestsAsync() => PollNow();
```

### `Polling/PollingControlService.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Polling/PollingControlService.cs
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Polling;

/// <summary>
/// Puente entre el icono de bandeja (fuera de Blazor, US-2) y la instancia
/// real de <see cref="UsagePollingCoordinator"/> que posee
/// <c>UsagePage.razor</c> (no un singleton de DI -- ver rationale en
/// <see cref="UsagePollingCoordinator"/> y Technology Choices). Registrado
/// como singleton en <c>App.xaml.cs</c>; <c>UsagePage.OnInitialized()</c> lo
/// "adjunta" tras crear su coordinador, mismo verbo que
/// <see cref="Windowing.WindowDragService.AttachWindow"/>.
/// </summary>
public sealed class PollingControlService
{
    private UsagePollingCoordinator? _coordinator;

    public bool IsPaused { get; private set; }

    /// <summary>Se levanta tras Pausar/Reanudar, para que TrayIconService actualice el texto del ítem de menú (AC de US-2).</summary>
    public event Action? PauseStateChanged;

    public void AttachCoordinator(UsagePollingCoordinator coordinator)
    {
        _coordinator = coordinator;
        IsPaused = false; // AC: arranca siempre con el polling activo (no persistido)
    }

    /// <summary>Llamado desde <c>UsagePage.Dispose()</c> para no retener una instancia ya liberada.</summary>
    public void DetachCoordinator(UsagePollingCoordinator coordinator)
    {
        if (ReferenceEquals(_coordinator, coordinator))
        {
            _coordinator = null;
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused || _coordinator is null)
        {
            return;
        }

        _coordinator.Pause();
        IsPaused = true;
        PauseStateChanged?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused || _coordinator is null)
        {
            return;
        }

        _coordinator.Start(); // AC: "mismo comportamiento que el Start() inicial"
        IsPaused = false;
        PauseStateChanged?.Invoke();
    }

    /// <summary>AC de "Recargar": ciclo de poll adicional, sin tocar el estado de pausa ni el timer.</summary>
    public void RequestReload() => _coordinator?.PollNow();
}
```

### `Tray/TrayIconService.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Tray/TrayIconService.cs
using System.Drawing;
using System.Windows.Forms;
using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Tray;

/// <summary>
/// Única clase de Desktop que referencia System.Windows.Forms (US-2, issue
/// #17). El constructor solo construye el menú y suscribe los eventos --
/// nunca toca NotifyIcon.Visible -- para que sea instanciable en xUnit sin
/// sesión de escritorio real. Initialize() (llamado una vez desde
/// App.OnStartup) resuelve el icono real y muestra el NotifyIcon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly PollingControlService _pollingControl;
    private readonly ClickThroughService _clickThrough;
    private readonly ILogger<TrayIconService> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _pauseResumeItem;
    private readonly ToolStripMenuItem _reloadItem;
    private readonly ToolStripMenuItem _clickThroughItem;
    private readonly ToolStripMenuItem _exitItem;
    private bool _disposed;

    public TrayIconService(PollingControlService pollingControl, ClickThroughService clickThrough, ILogger<TrayIconService> logger)
    {
        _pollingControl = pollingControl;
        _clickThrough = clickThrough;
        _logger = logger;

        _pauseResumeItem = new ToolStripMenuItem("Pausar");
        _pauseResumeItem.Click += (_, _) => _pollingControl.TogglePause();

        _reloadItem = new ToolStripMenuItem("Recargar");
        _reloadItem.Click += (_, _) => _pollingControl.RequestReload();

        _clickThroughItem = new ToolStripMenuItem("Click-through") { CheckOnClick = false };
        _clickThroughItem.Click += (_, _) => _clickThrough.Toggle();

        _exitItem = new ToolStripMenuItem("Salir");
        // Fully-qualified a propósito (norma defensiva ya usada en
        // MainWindow.xaml.cs): este fichero tiene `using System.Windows.Forms;`,
        // que también declara su propia clase Application distinta de
        // System.Windows.Application.
        _exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(_reloadItem);
        menu.Items.Add(_clickThroughItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Text = "ClaudeMeter" };

        _pollingControl.PauseStateChanged += OnPauseStateChanged;
        _clickThrough.StateChanged += OnClickThroughStateChanged;
    }

    /// <summary>Llamado una única vez al final de <c>App.OnStartup</c> (AC: "al completarse OnStartup, aparece el icono").</summary>
    public void Initialize()
    {
        try
        {
            _notifyIcon.Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            _logger.LogWarning(ex, "No se pudo extraer el icono del ejecutable; se usa el icono por defecto del sistema");
            _notifyIcon.Icon = SystemIcons.Application;
        }

        _notifyIcon.Visible = true;
    }

    private void OnPauseStateChanged() =>
        _pauseResumeItem.Text = _pollingControl.IsPaused ? "Reanudar" : "Pausar";

    private void OnClickThroughStateChanged() =>
        _clickThroughItem.Checked = _clickThrough.IsEnabled;

    /// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: expone los ítems para que xUnit invoque <c>PerformClick()</c> directamente (Definition of Done: sin interacción de ratón real de Windows).</summary>
    internal ToolStripMenuItem PauseResumeItemForTests => _pauseResumeItem;
    internal ToolStripMenuItem ReloadItemForTests => _reloadItem;
    internal ToolStripMenuItem ClickThroughItemForTests => _clickThroughItem;
    internal ToolStripMenuItem ExitItemForTests => _exitItem;

    /// <summary>AC de "icono de bandeja huérfano": oculta y libera el NotifyIcon antes de que el proceso termine. Llamado desde <c>App.OnExit</c>, en las tres vías de cierre (converge en <c>Application.Shutdown()</c>).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pollingControl.PauseStateChanged -= OnPauseStateChanged;
        _clickThrough.StateChanged -= OnClickThroughStateChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }
}
```

### `Pages/UsagePage.razor` (Desktop, modificado — excerpt)

```razor
@* src/ClaudeMeter.Desktop/Pages/UsagePage.razor *@
@* ... @implements/@inject existentes sin cambios, + 2 nuevos: *@
@inject PollingControlService PollingControl
@inject WindowCloseService CloseService

<div class="claudemeter-root @Config.Theme.ToCssClass()">
    @* US-2 (cierre directo): botón siempre presente, fuera del @if/else,
       para que exista también sobre ReauthNotice -- visible solo en
       hover/foco vía CSS (app.css). El propio click-through (US-1) ya
       inhibe el gesto sin guardas adicionales aquí (ver Technical Notes
       del documento de requisitos). *@
    <button type="button" class="claudemeter-close" aria-label="Cerrar ClaudeMeter">✕</button>

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
    private UsagePollingCoordinator? _coordinator;
    private DotNetObjectReference<WindowDragService>? _dragServiceRef;
    private DotNetObjectReference<WindowResizeService>? _resizeServiceRef;
    private DotNetObjectReference<WindowCloseService>? _closeServiceRef;
    // ... resto de campos sin cambios ...

    protected override void OnInitialized()
    {
        _coordinator = new UsagePollingCoordinator(UsageDataSource, Config.PollingInterval, Logger);
        _coordinator.SnapshotReceived += OnSnapshotReceived;
        _coordinator.Start();

        // US-2: la instancia real que PollingControlService (icono de
        // bandeja) usará para Pausar/Reanudar/Recargar.
        PollingControl.AttachCoordinator(_coordinator);
        PollingControl.PauseStateChanged += OnPauseStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // ... registro de claudeMeterDrag/claudeMeterResize sin cambios ...

            // US-2: registra el listener del botón de cierre directo.
            _closeServiceRef = DotNetObjectReference.Create(CloseService);
            try
            {
                await JS.InvokeVoidAsync("claudeMeterClose.init", _closeServiceRef);
            }
            catch (Exception ex)
            {
                PageLogger.LogError(ex, "No se pudo registrar el listener de cierre directo (claudeMeterClose.init)");
            }
        }
    }

    /// <summary>
    /// AC de US-2 (Pausar): reutiliza el criterio visual "(desactualizado)"
    /// ya existente en vez de introducir un tercer estado visual -- ver
    /// Technology Choices. Al reanudar, _isStale NO se fuerza a false aquí:
    /// el fetch inmediato que dispara PollingControlService.Resume()
    /// (Start() interno) lo actualizará por el cauce normal
    /// (OnSnapshotReceived/Apply) en cuanto complete.
    /// </summary>
    private void OnPauseStateChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (PollingControl.IsPaused)
            {
                _isStale = true;
            }
            StateHasChanged();
        });
    }

    // ... OnSnapshotReceived/Apply/ApplyForTests sin cambios ...

    public void Dispose()
    {
        PollingControl.PauseStateChanged -= OnPauseStateChanged;
        if (_coordinator is not null)
        {
            PollingControl.DetachCoordinator(_coordinator);
            _coordinator.SnapshotReceived -= OnSnapshotReceived;
            _coordinator.Dispose();
        }
        _dragServiceRef?.Dispose();
        _resizeServiceRef?.Dispose();
        _closeServiceRef?.Dispose();
    }
}
```

### `wwwroot/js/close.js` (Desktop, nuevo)

```js
// Botón de cierre directo (US-2). stopPropagation() en el propio pointerdown
// del botón evita que ese mismo clic burbujee hasta document.documentElement
// y dispare también WindowDragService.BeginDrag()/EndDrag() (drag.js escucha
// ahí) -- ver Technology Choices. No requiere ningún cambio en drag.js.
window.claudeMeterClose = {
    init: function (dotNetCloseService) {
        const button = document.querySelector('.claudemeter-close');
        if (!button) {
            return;
        }

        button.addEventListener('pointerdown', function (e) {
            e.stopPropagation();
        });

        button.addEventListener('click', function () {
            dotNetCloseService.invokeMethodAsync('RequestClose');
        });
    }
};
```

### `wwwroot/index.html` (Desktop, modificado — una línea)

```html
<script src="js/drag.js"></script>
<script src="js/resize.js"></script>
<script src="js/close.js"></script>
```

### `wwwroot/css/app.css` (Desktop, modificado — excerpt)

```css
.claudemeter-root {
    position: relative; /* ancla .claudemeter-close */
    box-sizing: border-box;
    height: 100%;
    color: var(--text-color);
    background-color: var(--bg-color);
}

/* US-2: botón de cierre discreto, visible solo en hover/foco -- evita
   ocupar espacio visual permanente en un widget de 280x140px ya ajustado
   (mismo criterio ya aplicado en F2/Ciclo B para no reservar una franja de
   arrastre fija). */
.claudemeter-close {
    position: absolute;
    top: 4px;
    right: 4px;
    width: 18px;
    height: 18px;
    border: none;
    border-radius: 4px;
    background: transparent;
    color: var(--text-muted);
    font-size: 11px;
    line-height: 18px;
    cursor: pointer;
    opacity: 0;
    transition: opacity 0.15s ease-in-out, background-color 0.15s ease-in-out;
}

.claudemeter-root:hover .claudemeter-close,
.claudemeter-close:focus-visible {
    opacity: 1;
}

.claudemeter-close:hover {
    background-color: var(--track-bg);
    color: var(--text-color);
}
```

### `App.xaml.cs` (Desktop, modificado — excerpt)

```csharp
services.AddSingleton<ClickThroughService>();
services.AddSingleton<WindowCloseService>();
services.AddSingleton<PollingControlService>();
services.AddSingleton<TrayIconService>();

Services = services.BuildServiceProvider();

// US-2: al completarse OnStartup, el icono ya debe estar visible (AC).
Services.GetRequiredService<TrayIconService>().Initialize();
```

```csharp
protected override void OnExit(ExitEventArgs e)
{
    // US-2: evita el icono huérfano en las tres vías de cierre (todas
    // convergen aquí vía Application.Shutdown()) -- antes de cerrar logs,
    // por si Dispose() necesitase registrar algo.
    Services.GetRequiredService<TrayIconService>().Dispose();

    _httpClient?.Dispose();
    Log.CloseAndFlush();
    base.OnExit(e);
}
```

### `MainWindow.xaml.cs` (Desktop, modificado — excerpt)

```csharp
app.Services.GetRequiredService<WindowDragService>().AttachWindow(this);
app.Services.GetRequiredService<WindowResizeService>().AttachWindow(this);

// F3/Ciclo B: click-through (US-1) y cierre directo (US-2).
app.Services.GetRequiredService<ClickThroughService>().AttachWindow(this);
app.Services.GetRequiredService<WindowCloseService>().AttachWindow(this);
```

### `ClaudeMeter.Desktop.csproj` (Desktop, modificado — una línea)

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <UseWindowsForms>true</UseWindowsForms> <!-- F3/Ciclo B: NotifyIcon (US-2), ver Technology Choices -->
  <RootNamespace>ClaudeMeter.Desktop</RootNamespace>
</PropertyGroup>
```

Sin `PackageReference` nuevo: `System.Windows.Forms`/`System.Drawing.Common`
llegan con `UseWindowsForms=true` como parte del framework compartido
`Microsoft.WindowsDesktop.App` ya referenciado por `UseWPF=true`.

## Implementation Plan

1. **`ClaudeMeter.Desktop.csproj`:** añadir `<UseWindowsForms>true</UseWindowsForms>` tal cual Data Model.
2. **`Windowing/ClickThroughService.cs` (US-1):** crear tal cual Data Model, incluyendo el *shim* de 32/64 bits y `SetEnabledForTests`.
3. **Tests de `ClickThroughService` (US-1, xUnit puro, nuevo `test/ClaudeMeter.Desktop.Tests/Windowing/ClickThroughServiceTests.cs`):** estado inicial `IsEnabled == false`; `SetEnabled`/`Toggle` sin ventana adjunta no lanzan, registran `Warning` (mismo patrón que `WindowDragServiceTests`) y no cambian `IsEnabled` ni disparan `StateChanged`; `SetEnabledForTests` sí actualiza `IsEnabled` y dispara `StateChanged` exactamente una vez (usado más abajo por `TrayIconServiceTests`). La llamada real a `user32.dll` con un `HWND` real queda como gap aceptado — mismo criterio que `Win32ScreenInfo`/`MainWindow`, cubierto por la validación manual heredada.
4. **`Windowing/WindowCloseService.cs` (US-2):** crear tal cual Data Model.
5. **Tests de `WindowCloseService` (US-2, xUnit puro, nuevo `Windowing/WindowCloseServiceTests.cs`):** `RequestClose()` sin ventana adjunta no lanza y registra `Warning` (mismo patrón que `WindowDragServiceTests`). La llamada real a `Application.Current.Shutdown()` con una `Window`/`Application` reales queda fuera de cobertura automática (terminaría el proceso de test) — cubierta por la validación manual ya heredada de Requirements.
6. **`Polling/UsagePollingCoordinator.cs` (US-2):** añadir `Pause()`/`PollNow()`, hacer que `PollOnceForTestsAsync()` delegue en `PollNow()`, tal cual Data Model.
7. **Tests de `UsagePollingCoordinator` (US-2, extendiendo `UsagePollingCoordinatorTests.cs`):** `Pause()` detiene el timer subyacente (`IsRunningForTests == false`) sin lanzar y sin llamar a `Dispose()` (la instancia sigue utilizable); `Start()` tras `Pause()` reanuda el timer y dispara un fetch inmediato (reutiliza el test ya existente de "Start dispara el primer fetch" como base); `PollNow()` dispara `GetUsageAsync` exactamente una vez y respeta el guard `_isPolling` si ya hay un poll en vuelo (extender el test parametrizado ya existente de `PollOnceForTestsAsync` con el nuevo nombre).
8. **`Polling/PollingControlService.cs` (US-2):** crear tal cual Data Model.
9. **Tests de `PollingControlService` (US-2, xUnit puro, nuevo `Polling/PollingControlServiceTests.cs`):** usando un `UsagePollingCoordinator` real respaldado por `FakeUsageDataSource` (mismo patrón que `UsagePollingCoordinatorTests`) — `AttachCoordinator` dejar `IsPaused == false`; `Pause()`/`Resume()`/`TogglePause()` alternan `IsPaused` y disparan `PauseStateChanged` exactamente una vez por llamada efectiva (una segunda `Pause()` consecutiva no dispara el evento de nuevo); `Pause()`/`Resume()`/`RequestReload()` sin coordinador adjunto no lanzan (no-op silencioso); `RequestReload()` invoca un poll adicional en el coordinador adjunto (`fake.CallCount` aumenta) sin tocar `IsPaused`.
10. **`Tray/TrayIconService.cs` (US-2):** crear tal cual Data Model.
11. **Tests de `TrayIconService` (US-2, xUnit puro, nuevo `Tray/TrayIconServiceTests.cs`):** construir con un `PollingControlService`+coordinador real (fake-backed) y un `ClickThroughService` reales; `PauseResumeItemForTests.PerformClick()` alterna `Text` entre `"Pausar"`/`"Reanudar"` y `pollingControl.IsPaused`; `ReloadItemForTests.PerformClick()` invoca un poll adicional en el coordinador (`fake.CallCount` aumenta) sin tocar `IsPaused`; disparar `clickThrough.SetEnabledForTests(true)` actualiza `ClickThroughItemForTests.Checked` a `true` (verifica la suscripción a `StateChanged` sin pasar por Win32 real); `ExitItemForTests` existe con `Text == "Salir"`, pero el test **no** invoca `PerformClick()` sobre él (dispararía `Application.Current.Shutdown()` real) — mismo criterio de exclusión que `WindowCloseService.RequestClose()`/`MainWindow`, cubierto por la validación manual heredada.
12. **`Pages/UsagePage.razor` (US-2):** inyectar `PollingControlService`/`WindowCloseService`, adjuntar/desadjuntar el coordinador, suscribir/desuscribir `OnPauseStateChanged`, registrar `claudeMeterClose.init`, añadir el botón de cierre — tal cual Data Model.
13. **`wwwroot/js/close.js` (US-2):** crear tal cual Data Model.
14. **`wwwroot/index.html` (US-2):** añadir `<script src="js/close.js"></script>`.
15. **`wwwroot/css/app.css` (US-1/US-2):** añadir `position: relative` a `.claudemeter-root` y las reglas de `.claudemeter-close` tal cual Data Model.
16. **Tests bUnit de `UsagePage` (US-2, extendiendo `UsagePageTests.cs`):** tras un snapshot exitoso seguido de invocar el `OnPauseStateChanged` interno (vía un `PollingControlService` de prueba inyectado con `Pause()` real sobre un coordinador fake-backed), `UsageBar` recibe `Stale="true"` y el marcador `"(desactualizado)"` aparece; `cut.Find("button.claudemeter-close")` existe tanto con snapshot `Success` como con `Unauthorized` (`ReauthNotice` visible) — verifica que el botón vive fuera del `@if/else`.
17. **`MainWindow.xaml.cs` (US-1/US-2):** adjuntar `ClickThroughService`/`WindowCloseService` tal cual Data Model. Sin test automatizado (mismo criterio ya aplicado a este fichero desde F2/Ciclo B).
18. **`App.xaml.cs` (US-1/US-2):** registrar los 4 servicios nuevos, llamar a `TrayIconService.Initialize()` al final de `OnStartup` y a `TrayIconService.Dispose()` al principio de `OnExit`, tal cual Data Model. Sin test automatizado (composition root, mismo criterio que el resto de este fichero).
19. **Validación de compilación:** `dotnet build ClaudeMeter.sln` en Debug y Release (analizadores + `TreatWarningsAsErrors` en Release), igual que en ciclos anteriores — prestar atención especial a advertencias de nulabilidad en el *shim* de `ClickThroughService` (conversión `IntPtr`↔`long`) y a que `UseWindowsForms=true` no introduzca advertencias de compatibilidad de plataforma nuevas en el resto del proyecto (no debería: el TFM del proyecto ya es `net8.0-windows`).

## Cross-Cutting Concerns

- **Seguridad:** ninguna superficie nueva — click-through/pausa/click de
  bandeja son controles puramente locales de UI, sin ninguna interacción con
  el token/credenciales ni con la red.
- **Rendimiento:** `ClickThroughService`/`WindowCloseService`/
  `PollingControlService`/`TrayIconService` son operaciones event-driven
  disparadas por una interacción explícita del usuario (nunca en bucle) —
  coste despreciable, ninguna de las cuatro añade trabajo por ciclo de poll.
  El `NotifyIcon`/`ContextMenuStrip` comparten el hilo de UI de WPF sin
  bucle de mensajes adicional (ver Technology Choices).
- **Manejo de errores:** `ClickThroughService.SetEnabled`/
  `WindowCloseService.RequestClose` nunca lanzan si `MainWindow` no está
  todavía adjunta (guard + `Warning`, mismo patrón que
  `WindowDragService`); `TrayIconService.Initialize()` no lanza si no puede
  extraer el icono del ejecutable (fallback a `SystemIcons.Application` +
  `Warning`).
- **Observabilidad/logging:** entradas `Information` nuevas para
  Pausar/Reanudar (`UsagePollingCoordinator.Pause/Start`, ya existente para
  `Start`), y `Warning` para los tres guards de "servicio invocado antes de
  adjuntar la ventana" — mismo nivel/formato que los ya existentes en
  `WindowDragService`/`WindowResizeService`.
- **Compatibilidad hacia atrás:** ningún cambio en `config.json`/
  `AppConfig`/`AppConfigStore` (pausa y click-through son estado de sesión,
  no persistido — decisión ya fijada por Requirements); un `config.json` de
  cualquier ciclo anterior sigue cargando exactamente igual. El arranque por
  defecto (sin interacción del usuario) es visual y funcionalmente idéntico
  al actual: sin icono de bandeja no había forma de pausar/click-through
  antes de este ciclo, y ahora ambos arrancan desactivados — cero regresión
  del comportamiento por defecto (AC explícita de US-1).

## Risks & Open Decisions

- **Decisión ya cerrada por este documento — mecanismo del icono de
  bandeja:** `System.Windows.Forms.NotifyIcon` + `UseWindowsForms=true`, en
  vez de `Hardcodet.NotifyIcon.Wpf` 2.0.1 — ver Technology Choices para el
  rationale completo (sin dependencia de terceros nueva, y es la vía que el
  propio historial de decisiones de este repositorio ya señalaba como
  destino natural de F3). No es una decisión abierta: no hay AC ni
  requisito que la deje pendiente de validación humana, y el trade-off
  (superficie de framework compartido más amplia, sin dependencia externa)
  es una decisión de implementación resoluble por Design, no un cambio de
  comportamiento observable por el usuario.
- **Gap de cobertura automática aceptado, consistente con precedentes ya
  establecidos (Impact: Low):** la llamada real a `user32.dll` con un
  `HWND` real (`ClickThroughService`), la llamada real a
  `Application.Current.Shutdown()` (`WindowCloseService`/ítem "Salir"), y el
  `NotifyIcon.Visible = true` real (`TrayIconService.Initialize()`) quedan
  fuera de cobertura xUnit/bUnit — exactamente el mismo criterio ya aceptado
  por este proyecto para `Win32ScreenInfo`/`MainWindow` desde F2/Ciclo B.
  Cubierto por la validación manual ya prevista en la Definition of Done
  heredada de Requirements (icono, menú, click-through y las tres vías de
  cierre verificados en Windows real).
- **No distinguir visualmente "pausado" de "stale por fallo transitorio"
  (heredado del Risk de Requirements, resuelto aquí — Impact: Low):**
  Requirements dejaba explícitamente a discreción de Design si merecía la
  pena una distinción visual nueva; este documento decide reutilizar
  `_isStale` sin un tercer estado (ver Technology Choices) por
  proporcionalidad — ninguna AC lo exige. Reconsiderable en un ciclo futuro
  si el usuario reporta confusión real entre ambos casos, sin que eso
  bloquee este ciclo.
- Sin decisiones de producto/arquitectura genuinamente abiertas que
  requieran validación humana antes de implementar: las tres ambigüedades
  que Requirements dejaba para Design (mecanismo del icono de bandeja,
  detalle del gesto de cierre, distinción visual pausado/stale) quedan
  resueltas arriba con su rationale.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados (xUnit puro para
      `ClickThroughService`/`WindowCloseService`/`PollingControlService`/
      `UsagePollingCoordinator.Pause`+`PollNow` y para la lógica de
      `TrayIconService` invocada vía `PerformClick()`/`SetEnabledForTests`;
      bUnit para que `UsagePage` refleje `_isStale` al pausar y para la
      presencia del botón de cierre) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada depende de una bandeja de sistema real ni
      de interacción de ratón real de Windows — los tests invocan
      `PerformClick()`/los métodos `[JSInvokable]`/`SetEnabledForTests`
      directamente, nunca simulan un clic físico sobre un icono real
- [ ] **Validación manual:** se confirma, ejecutando la aplicación real en
      Windows, que (a) el icono aparece en la bandeja y su menú responde a
      Pausar/Reanudar/Recargar/click-through/Salir con el efecto descrito,
      (b) el click-through activado deja pasar los clics a la ventana de
      debajo del escritorio real y el widget sigue actualizándose, (c) el
      botón de cierre directo (visible solo en hover) y "Salir" desde la
      bandeja terminan el proceso sin dejarlo corriendo en segundo plano
      (verificable en el Administrador de tareas), y (d) ningún icono de
      bandeja queda residual tras cerrar por cualquiera de las tres vías —
      ningún agente de este pipeline puede verificar interacción de ratón
      real, comportamiento real de la bandeja de Windows, ni procesos del
      sistema operativo de forma automática
