# Design: F3 — UX + página Mascota, Ciclo C

## Requirements Reference

`docs/sdlc/requirements/f3-ux-mascota-ciclo-c.md` — cubre US-1 (issue #18,
`ScreenNavigator` + `IWidgetScreen`) y US-2 (issue #19, `MascotPage.razor`
con `MascotState` derivado únicamente del snapshot actual). Documento
cerrado, sin "Open question" pendientes. Con este ciclo se cierra el
milestone F3 completo (Ciclos A y B ya commiteados: `97206e7`, `acb13b6`).

Requirements deja dos puntos explícitamente para este documento (citados
literalmente en "Punto de partida verificado"):

1. Qué cambia exactamente al introducir `ScreenNavigator`: ¿pasa a ser el
   nuevo `RootComponent` de `BlazorWebView` (con `UsagePage` como una de las
   pantallas que renderiza internamente), o `MainWindow.xaml` sigue
   apuntando a otro componente? — **Resuelto en este documento: opción (a),
   `ScreenNavigator` es el nuevo `RootComponent` real.**
2. Dónde vive la gestión de ciclo de vida de los cuatro servicios que hoy
   gestiona `UsagePage` directamente (`WindowDragService`,
   `WindowResizeService`, `WindowCloseService`, `PollingControlService`):
   centralizada en `ScreenNavigator`, o replicada por cada `IWidgetScreen`.
   — **Resuelto en este documento: centralizada en `ScreenNavigator`,
   ver Architecture Overview para el rationale completo.**

## Architecture Overview

### Decisión central: `ScreenNavigator` es el nuevo `RootComponent`, no un componente más

Confirmado por inspección de `MainWindow.xaml` (líneas 34-39): hoy
`BlazorWebView.RootComponents` registra un único `RootComponent` con
`ComponentType="{x:Type pages:UsagePage}"`. Este documento cambia ese valor
a `ScreenNavigator`, que pasa a ser el único componente Razor garantizado
montado durante toda la vida de la aplicación, con independencia de qué
`IWidgetScreen` esté visible.

La razón no es estética — es la única forma de resolver correctamente,
sin regresión, los tres problemas concretos que Requirements deja
explícitamente abiertos:

1. **Consistencia de datos entre pantallas (Risk "Medium" de
   Requirements).** `MascotPage` necesita el mismo `UsageSnapshot`
   (parseado a `RateLimitWindow Session/Weekly`) que hoy consume
   `UsagePage`, y la AC de US-2 exige explícitamente que, al cambiar a
   `MascotPage`, ésta "refleje el `MascotState` correspondiente al último
   snapshot conocido... sin esperar a un nuevo ciclo de poll". Eso solo es
   posible si existe **una única fuente de verdad** para `(_session,
   _weekly, _status, _isStale)`, actualizada por **un único**
   `UsagePollingCoordinator`, que exista y seleccione con independencia de
   qué pantalla está visible en cada momento. Si cada `IWidgetScreen`
   creara su propio coordinador (como hace `UsagePage` hoy), `MascotPage`
   arrancaría "en blanco" hasta su primer poll propio — viola la AC citada
   — y además duplicaría llamadas HTTP a la API (Risk de Requirements).
   `PollingControlService` ya modela "un único coordinador adjuntado a la
   vez" (`AttachCoordinator`/`DetachCoordinator`) — reforzando que el
   diseño ya existente asume una única instancia activa, no una por
   pantalla.
2. **Gestos sobre la ventana completa, no sobre el contenido de una
   pantalla (Risk "Medium" de Requirements).** El arrastre
   (`WindowDragService`) y el cierre directo (`WindowCloseService`) actúan
   sobre `MainWindow` como un todo — no tiene sentido conceptual que
   dejen de funcionar (o se vuelvan a registrar, con el consiguiente riesgo
   de doble-registro de listeners JS) solo porque el usuario cambió de
   `UsagePage` a `MascotPage`. Lo mismo aplica a `WindowResizeService`:
   observa `.claudemeter-root` (confirmado en `wwwroot/js/resize.js`), un
   contenedor que en este diseño pertenece a `ScreenNavigator`, no a cada
   pantalla — se ajusta a la altura real de **cualquier** contenido que
   `ScreenNavigator` esté mostrando en ese momento, sin ningún cambio en
   `resize.js` ni en `WindowResizeService.cs`.
3. **Cero regresión de F3/Ciclos A y B (Risk "Medium" de Requirements,
   el más repetido).** Si estos cuatro servicios se centralizan en
   `ScreenNavigator` (que nunca se desmonta mientras la app viva), sus
   `OnAfterRenderAsync`/`Dispose` se ejecutan **exactamente una vez** en
   toda la vida de la aplicación — igual que hoy con `UsagePage` como único
   `RootComponent` — así que el arrastre, el auto-ajuste de altura, el
   cierre directo y el tema no cambian de comportamiento en absoluto, solo
   de ubicación en el código. La alternativa (cada `IWidgetScreen`
   registrando su propia copia) multiplicaría por N pantallas el número de
   veces que `claudeMeterDrag.init`/`claudeMeterResize.init`/
   `claudeMeterClose.init` se invocan a lo largo de la vida de la app
   (una vez por cada vez que esa pantalla se muestra, si se recrea al
   ciclar) — un riesgo real de listeners JS duplicados sobre
   `document.documentElement`, exactamente lo que Requirements advierte.

Consecuencia directa: **`UsagePage.razor` deja de gestionar su propio
ciclo de vida.** Dejar de ser el `RootComponent` significa que ya no es el
único componente garantizado vivo — así que toda la lógica que dependía de
eso (el coordinador, el chime, el tema, los tres `DotNetObjectReference`,
el botón de cierre) sube a `ScreenNavigator`. `UsagePage` pasa a ser un
componente **puramente de presentación**: recibe un único `[Parameter]`
(`WidgetUsageState State`, ver Data Model) con los datos ya calculados, y
solo decide cómo pintarlos (`UsageBar` x2 o `ReauthNotice`) — exactamente
el mismo criterio de separación que ya aplica `UsageThresholdClassifier`
(Domain, sin UI) frente a `UsageBar` (Desktop, todo lo visual). `MascotPage`
se construye desde cero con el mismo patrón: recibe el mismo `WidgetUsageState`
y solo decide su propia representación visual.

### Cómo se ciclan las pantallas sin que `ScreenNavigator` conozca su interior

`IWidgetScreen` se mantiene deliberadamente mínimo (tal como pide la AC):

```csharp
public interface IWidgetScreen
{
    string ScreenId { get; }
}
```

`ScreenNavigator` **no** necesita instanciar una pantalla para decidir
cuál va después: cicla sobre una lista de `Type` (cada uno, una clase Razor
concreta que implementa `IWidgetScreen`), y renderiza la actual con
`<DynamicComponent Type="..." Parameters="...">` (API estable de Blazor
desde .NET 6, confirmada contra la documentación oficial — ver Technology
Choices). `IWidgetScreen.ScreenId` no participa en la lógica de ciclado en
sí (que es aritmética pura sobre índices) — existe para que una pantalla
concreta pueda identificarse a sí misma en logs/tests una vez renderizada,
que es exactamente el "identificador/etiqueta de pantalla, si aplica" que
la AC de US-1 deja como opcional.

La lógica de ciclado en sí se extrae a una clase pura sin ninguna
dependencia de Blazor/WPF, **`ScreenCycle`**, mismo criterio ya aplicado en
este proyecto para separar lógica de decisión real de su envoltorio de UI
(`WindowPositionResolver` frente a `MainWindow`): esto es lo que permite
cumplir literalmente la exigencia de la Definition of Done heredada de
Requirements ("xUnit puro... para cualquier lógica de ciclo de
`ScreenNavigator` que no dependa de WPF real" + "bUnit para... `ScreenNavigator`
alternando entre `IWidgetScreen` simuladas") con dos suites de test
distintas y genuinamente aisladas, en vez de una sola prueba bUnit que
tuviera que arrastrar las dependencias completas de `UsagePage`/`MascotPage`
para probar algo tan simple como "el índice avanza cíclicamente".
`ScreenNavigator` expone `[Parameter] IReadOnlyList<Type> ScreenTypes`
(con el valor de producción `{ typeof(UsagePage), typeof(MascotPage) }`
como default) precisamente para que los tests bUnit puedan sustituirlo por
tipos de pantalla simulados sin tocar producción.

### Corrección de un caso límite descubierto por este ciclo (no una regresión, una pieza que faltaba)

Al diseñar `MascotState` como función pura de `(RateLimitWindow Session,
RateLimitWindow Weekly)` se descubrió, por inspección línea a línea de
`UsagePage.Apply()` (F2), que la rama `Unauthorized` **no** resetea
`_session`/`_weekly` a `RateLimitWindow.Unavailable` — los deja con el
último valor parseado con éxito antes del 401/403, porque hasta ahora daba
igual: `UsagePage` sustituye las dos `UsageBar` por `ReauthNotice` en ese
estado, así que esos campos nunca se leen visualmente. Si `MascotPage`
derivase su estado de esos mismos campos sin corregir esto, mostraría la
severidad de un consumo **anterior a la pérdida de credencial** en vez del
cuarto estado "sin datos" que exige la AC de US-2 para `Unauthorized` — un
bug real, aunque invisible hasta hoy. Este documento corrige la rama
`Unauthorized` de `Apply()` (ahora en `ScreenNavigator`) para que también
resetee ambos campos a `RateLimitWindow.Unavailable`, sin cambiar ningún
comportamiento observable de `UsagePage` (que ya ignoraba esos campos en
ese estado) — ver Technology Choices y Risks.

Piezas nuevas/modificadas, de fuera hacia dentro:

1. **`ClaudeMeter.Domain.Usage.MascotState` / `MascotStateClassifier`**
   (Domain, nuevo) — enum de 4 valores + función pura de clasificación
   sobre `(RateLimitWindow, RateLimitWindow)`, sin ninguna noción de
   color/texto — mismo estándar que `UsageThreshold`/`UsageThresholdClassifier`.
2. **`ClaudeMeter.Desktop.Navigation.IWidgetScreen`** (Desktop, nuevo) —
   contrato mínimo (issue #18).
3. **`ClaudeMeter.Desktop.Navigation.ScreenCycle`** (Desktop, nuevo) —
   lógica pura de ciclado sobre una lista de `Type`, sin Blazor/WPF.
4. **`ClaudeMeter.Desktop.Navigation.WidgetUsageState`** (Desktop, nuevo) —
   DTO compartido que `ScreenNavigator` pasa como `[Parameter]` a la
   pantalla activa (`Session`, `Weekly`, `Status`, `IsStale`).
5. **`ClaudeMeter.Desktop.Navigation.ScreenNavigator`** (Desktop, nuevo,
   `.razor`) — nuevo `RootComponent` real. Absorbe íntegramente el `@code`
   de polling/chime/tema/JS-interop que hoy tiene `UsagePage` (coordinador,
   `PollingControlService`, los tres `DotNetObjectReference` de
   drag/resize/close, el botón de cierre, el contenedor `.claudemeter-root`
   con la clase de tema) y añade el botón de cambio de pantalla + el
   `DynamicComponent` que renderiza la pantalla activa.
6. **`Pages/UsagePage.razor`** (Desktop, modificado en profundidad) — pasa
   de "dueño de su ciclo de vida" a componente de presentación puro:
   `[Parameter, EditorRequired] WidgetUsageState State`, `@implements
   IWidgetScreen`, renderiza `UsageBar`x2/`ReauthNotice` según
   `State.Status`. Pierde toda inyección de servicios de polling/ventana.
7. **`Pages/MascotPage.razor`** (Desktop, nuevo) — issue #19. `[Parameter,
   EditorRequired] WidgetUsageState State`, `@implements IWidgetScreen`,
   deriva `MascotState` vía `MascotStateClassifier.Classify(State.Session,
   State.Weekly)` y renderiza una de 4 representaciones visuales.
8. **`MainWindow.xaml`** (Desktop, modificado — una línea + un `xmlns`) —
   `RootComponent.ComponentType` pasa de `{x:Type pages:UsagePage}` a
   `{x:Type navigation:ScreenNavigator}`.
9. **`wwwroot/css/app.css`** (Desktop, modificado) — añade
   `.claudemeter-cycle` (botón de cambio de pantalla, mismo tratamiento
   visual que `.claudemeter-close`) y las clases `.mascot`/`.mascot--*`
   (US-2).

Sin cambios en `App.xaml.cs`: los cuatro servicios (`WindowDragService`,
`WindowResizeService`, `WindowCloseService`, `PollingControlService`) ya
están registrados como singleton desde Ciclos A/B — este ciclo solo mueve
**quién los inyecta y los usa** (de `UsagePage` a `ScreenNavigator`), no
quién los registra. Tampoco hay cambios en `MainWindow.xaml.cs`: sigue
llamando a `AttachWindow(this)` sobre los cuatro servicios de `Windowing`
exactamente igual, con independencia de qué Razor `RootComponent` haya
configurado (el `AttachWindow` no depende de Blazor en absoluto). Domain
(salvo `MascotState` nuevo) / Application / Infrastructure: sin cambios.

Diagrama de dependencias resultante (piezas nuevas/modificadas resaltadas):

```
Domain
 └── Usage/
      └── MascotState.cs (nuevo) ──> UsageThresholdClassifier (reutilizado, sin duplicar umbrales)

Desktop
 ├── Navigation/                                    (nuevo namespace)
 │    ├── IWidgetScreen.cs (nuevo)
 │    ├── ScreenCycle.cs (nuevo, lógica pura)
 │    ├── WidgetUsageState.cs (nuevo, DTO compartido)
 │    └── ScreenNavigator.razor (nuevo) ──> RootComponent real; posee
 │           UsagePollingCoordinator, PollingControlService, WindowDragService,
 │           WindowResizeService, WindowCloseService, IChimePlayer, AppConfig.Theme
 ├── Pages/
 │    ├── UsagePage.razor (modificado) ──> puramente presentacional, [Parameter] State
 │    ├── MascotPage.razor (nuevo) ──> puramente presentacional, [Parameter] State
 │    └── UsageBar.razor / ReauthNotice.razor (sin cambios)
 ├── MainWindow.xaml (modificado) ──> RootComponent = ScreenNavigator
 ├── MainWindow.xaml.cs (sin cambios)
 ├── App.xaml.cs (sin cambios)
 └── wwwroot/css/app.css (modificado) ──> .claudemeter-cycle, .mascot*

Application / Infrastructure: sin cambios.
```

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| **`ScreenNavigator` pasa a ser el `RootComponent` real de `BlazorWebView` (opción (a) de las dos que dejaba abiertas Requirements)** | Es la única opción que resuelve sin ambigüedad los tres riesgos "Medium" de Requirements (desincronización de datos, gestos sobre toda la ventana, regresión de F3/Ciclos A y B) con una única fuente de verdad y un único punto de registro de JS interop — ver Architecture Overview para el análisis completo. | Opción (b): `MainWindow.xaml` sigue apuntando a otro componente y `ScreenNavigator` vive dentro de él — rechazada por ser una capa de indirección sin beneficio real: ese "otro componente" tendría que ser, de nuevo, el único punto estable donde vivir el coordinador/servicios compartidos, es decir, jugaría exactamente el papel que aquí juega `ScreenNavigator` directamente. Añadir un nivel más solo aplazaría la misma decisión sin resolverla. |
| **Ciclado de pantallas vía `<DynamicComponent Type="..." Parameters="...">` sobre una lista de `Type`, en vez de un `@if`/`@switch` explícito por pantalla** | `DynamicComponent` es una API estable de Blazor desde .NET 6 (confirmado contra la documentación oficial de Microsoft Learn, `Microsoft.AspNetCore.Components.DynamicComponent`: expone `Type` y `Parameters` (`IDictionary<string, object>`), ya incluida en el SDK compartido de ASP.NET Core que este proyecto ya referencia vía `Microsoft.AspNetCore.Components.WebView.Wpf` — sin paquete NuGet nuevo). Permite que `ScreenNavigator` cicle sobre cualquier lista de `IWidgetScreen` sin un bloque condicional que crezca con cada pantalla nueva (F4 añadirá `PeakOffPeakPage` a la misma lista sin tocar la lógica de ciclado) — cumple literalmente el "so that" de la US-1 ("añadir una página nueva no requiera modificar el resto de la navegación"). | Un `@switch (_current)` explícito con un `case` por tipo de pantalla — rechazada: aunque más simple de leer con solo dos pantallas, reintroduce exactamente el acoplamiento que la US-1 quiere evitar (cada pantalla nueva exige tocar el cuerpo del `switch`), y no deja una superficie limpia para que `ScreenCycle` (lógica pura) se pruebe con tipos simulados sin arrastrar componentes Blazor reales. |
| **Lógica de ciclado extraída a `ScreenCycle` (clase pura, sin `IComponent`/Blazor), en vez de mantener el índice y el `% Count` directamente en el `@code` de `ScreenNavigator`** | Mismo criterio de separación ya aplicado por este proyecto entre `WindowPositionResolver` (puro) y `MainWindow` (WPF): permite xUnit puro con tipos cualesquiera (`typeof(object)`, `typeof(string)` como sustitutos triviales) para cubrir el caso límite "una sola pantalla registrada → no lanza, siempre devuelve la misma" (AC de US-1) sin montar ningún componente Razor real — exactamente lo que pide la Definition of Done heredada ("lógica de ciclo de `ScreenNavigator` que no dependa de WPF real"). | Mantener el índice como campo privado de `ScreenNavigator` y probarlo solo vía bUnit — rechazada: mezclaría en una sola prueba bUnit (más lenta, requiere `BunitContext`) una aserción que es pura aritmética de índices; Requirements pide explícitamente ambas coberturas (xUnit para la lógica, bUnit para el comportamiento observable de `ScreenNavigator` con pantallas simuladas) como pruebas distintas. |
| **Gesto de cambio de pantalla: botón discreto `<button class="claudemeter-cycle" @onclick="..." @onpointerdown:stopPropagation="true">`, resuelto íntegramente en Blazor, SIN interop JS nuevo — se aparta de la sugerencia literal de Requirements ("JS interop, mismo patrón que el botón de cierre")** | El botón de cierre y el arrastre necesitan JS interop porque actúan sobre el `HWND` real de WPF (mover la ventana, cerrar el proceso) — algo que vive **fuera** del árbol de Blazor y que WebView2 no puede tocar directamente. Cambiar de pantalla, en cambio, es una mutación de estado **dentro** del propio árbol de Blazor (qué `Type` renderiza `DynamicComponent`) — Blazor ya gestiona sus propios eventos DOM (`@onclick`) de forma nativa, sin necesitar salir al proceso WPF. El modificador `@onpointerdown:stopPropagation="true"` (confirmado contra la documentación oficial de ASP.NET Core Blazor, "event handling": los modificadores `:stopPropagation`/`:preventDefault` invocan la API nativa del navegador antes de despachar el evento a .NET) resuelve exactamente el mismo problema que `close.js` resuelve a mano con `e.stopPropagation()` — evitar que el mismo clic burbujee hasta `document.documentElement`, donde `drag.js` interpretaría el mismo gesto como un arrastre de longitud cero — sin necesitar un fichero `.js` nuevo ni un `DotNetObjectReference` adicional. Efecto colateral positivo: es la superficie más fácil de testear de todo el ciclo (bUnit puede invocar `cut.Find("button.claudemeter-cycle").Click()` de forma completamente síncrona, sin `JSInterop.Mode = Loose` ni simular ninguna promesa de interop). | Reutilizar literalmente el patrón de `close.js` (JS interop → `[JSInvokable] CycleScreen()` en un nuevo servicio de `Windowing`) tal como sugería Requirements — rechazada: añadiría un fichero JS, un `DotNetObjectReference` y un guard "sin ventana adjunta" para resolver un problema (acceso al `HWND` real) que el gesto de cambio de pantalla no tiene, ya que no toca WPF en absoluto. Requirements deja este gesto explícitamente como sugerencia, no como decisión cerrada. |
| **`MascotStateClassifier.Classify(RateLimitWindow session, RateLimitWindow weekly)`, en vez de la firma sugerida por Requirements (`MascotState.From(UsageSnapshot, DateTimeOffset)`)** | `ScreenNavigator` ya parsea el `UsageSnapshot` a `(RateLimitWindow Session, RateLimitWindow Weekly)` una única vez por ciclo de poll (lo necesita para las propias `UsageBar` de `UsagePage`) — reutilizar esos mismos valores ya parseados evita volver a parsear el mismo `UsageSnapshot` una segunda vez para `MascotPage`, y mantiene a `MascotStateClassifier` con la misma forma de firma que `UsageThresholdClassifier.Classify(double? percentageUsed)`: entrada ya normalizada, sin fecha de referencia (no la necesita, a diferencia de `RateLimitWindowParser.Parse`, que sí calcula un countdown dependiente del reloj). Requirements deja esta firma explícitamente como "forma sugerida, no decisión cerrada". | La firma sugerida por Requirements (`UsageSnapshot` + `DateTimeOffset`) — rechazada por la duplicación de parseo explicada arriba; se reconsideraría solo si en el futuro `MascotState` necesitase datos crudos que `RateLimitWindow` no expone (ninguna AC de este ciclo lo pide). |
| **Corrección de `Apply()` (ahora en `ScreenNavigator`): la rama `Unauthorized` resetea `_session`/`_weekly` a `RateLimitWindow.Unavailable`, algo que la implementación de F2 no hacía** | Necesario para que `MascotStateClassifier.Classify` reciba `(null, null)` y resuelva `NoData` en `Unauthorized`, tal como exige la AC de US-2 ("no hay datos disponibles... o el snapshot está en Unauthorized... se muestra un cuarto estado explícito de sin datos"). Verificado que es invisible para `UsagePage`: su rama `Unauthorized` ya sustituye las dos `UsageBar` por `ReauthNotice` sin leer `_session`/`_weekly` en absoluto (confirmado por inspección de `UsagePage.razor` línea 39-49 y de los tests existentes de `UsagePageTests.cs`, que solo verifican markup, nunca los campos internos) — cero regresión observable en `UsagePage`. | Dejar `_session`/`_weekly` sin resetear y añadir un chequeo explícito de `Status == Unauthorized` dentro de `MascotStateClassifier`/`MascotPage` para forzar `NoData` — rechazada: acoplaría el clasificador de Domain (que debe permanecer puro, sin ninguna noción de `UsageSnapshotStatus`) a un concepto de Infrastructure/red, y duplicaría en dos sitios (Domain y donde sea que se comprobara `Status`) la definición de "sin datos". Corregir el dato en el origen (una única vez, en `ScreenNavigator.Apply()`) es más simple y evita esa duplicación. |
| **`WidgetUsageState` como único `[Parameter]` compartido (`Session`, `Weekly`, `Status`, `IsStale`) pasado tanto a `UsagePage` como a `MascotPage`, en vez de 4 `[Parameter]` sueltos por pantalla** | Ambas pantallas consumen exactamente el mismo conjunto de datos calculado una sola vez por `ScreenNavigator` — un único parámetro evita que `DynamicComponent.Parameters` (un `IDictionary<string, object>` por nombre de propiedad) tenga que mantener sincronizados 4 nombres de clave distintos en dos ficheros distintos; un cambio futuro (p. ej. añadir un quinto dato compartido en F4) se hace en un único `record`. | 4 parámetros sueltos (`Session`, `Weekly`, `Status`, `IsStale`) — rechazada por el riesgo de desincronización de nombres de clave explicado arriba, sin ningún beneficio real ya que ambas pantallas siempre necesitan el conjunto completo (ninguna pantalla de este ciclo consume un subconjunto parcial). |

## Data Model / Interfaces

### `Domain/Usage/MascotState.cs` (Domain, nuevo)

```csharp
// src/ClaudeMeter.Domain/Usage/MascotState.cs
namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Estado visual de la mascota ("Clawd", issue #19), derivado exclusivamente
/// del snapshot de uso ACTUAL — sin ninguna noción de color/texto/UI (mismo
/// estándar que <see cref="UsageThreshold"/>) y sin ningún dato histórico
/// (restricción explícita de CLAUDE.md: "MascotPage.razor driven only by
/// the current snapshot, no history dependency").
/// </summary>
public enum MascotState
{
    /// <summary>Ambas ventanas en <see cref="UsageThreshold.Normal"/> (o una Normal y la otra sin dato individual).</summary>
    Calm,

    /// <summary>Al menos una ventana en <see cref="UsageThreshold.Warning"/>, ninguna en Critical.</summary>
    Alert,

    /// <summary>Al menos una ventana en <see cref="UsageThreshold.Critical"/>, con independencia de la otra.</summary>
    NearLimit,

    /// <summary>Ninguna ventana tiene un porcentaje interpretable (nunca hubo éxito, o el snapshot es Unauthorized).</summary>
    NoData
}

/// <summary>
/// Deriva <see cref="MascotState"/> de las dos ventanas ya parseadas.
/// Función pura, sin E/S, sin reloj propio — mismo estándar que
/// <see cref="UsageThresholdClassifier"/>/<see cref="RateLimitWindowParser"/>.
/// Reutiliza <see cref="UsageThresholdClassifier"/> como única fuente de
/// verdad del umbral (evita que la mascota y las <c>UsageBar</c> "cuenten
/// historias distintas" sobre el mismo porcentaje).
/// </summary>
public static class MascotStateClassifier
{
    /// <summary>
    /// "Peor caso" (Technical Notes de Requirements): si <paramref name="session"/>
    /// y <paramref name="weekly"/> clasifican en umbrales distintos, se usa el
    /// más severo. Si una de las dos no tiene dato individual (percentage
    /// null dentro de un snapshot por lo demás exitoso), se usa la otra sin
    /// penalizar. Solo si NINGUNA de las dos tiene dato se resuelve
    /// <see cref="MascotState.NoData"/> (AC de US-2: nunca "Calm" por
    /// defecto cuando en realidad no hay dato real).
    /// </summary>
    public static MascotState Classify(RateLimitWindow session, RateLimitWindow weekly)
    {
        var sessionThreshold = UsageThresholdClassifier.Classify(session.PercentageUsed);
        var weeklyThreshold = UsageThresholdClassifier.Classify(weekly.PercentageUsed);

        if (sessionThreshold is null && weeklyThreshold is null)
        {
            return MascotState.NoData;
        }

        return Worse(sessionThreshold, weeklyThreshold) switch
        {
            UsageThreshold.Critical => MascotState.NearLimit,
            UsageThreshold.Warning => MascotState.Alert,
            _ => MascotState.Calm,
        };
    }

    // UsageThreshold se declara Normal=0 < Warning=1 < Critical=2 (orden
    // ascendente de severidad) -- Math.Max sobre los valores enteros ya
    // expresa "el más severo de los dos" sin una tabla de comparación aparte.
    private static UsageThreshold Worse(UsageThreshold? a, UsageThreshold? b)
    {
        if (a is null) return b!.Value;
        if (b is null) return a.Value;
        return (UsageThreshold)Math.Max((int)a.Value, (int)b.Value);
    }
}
```

### `Desktop/Navigation/IWidgetScreen.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Navigation/IWidgetScreen.cs
namespace ClaudeMeter.Desktop.Navigation;

/// <summary>
/// Contrato mínimo que debe implementar una página Razor para integrarse en
/// <see cref="ScreenNavigator"/> sin que éste conozca ningún detalle interno
/// de la página (US-1, issue #18). El propio tipo concreto (obtenido vía
/// <c>typeof(...)</c> en <see cref="ScreenNavigator.DefaultScreens"/>) ya es
/// "lo mínimo" que ScreenNavigator necesita para poder mostrarlo vía
/// <c>DynamicComponent</c> -- ScreenId es metadato adicional para
/// logging/tests, no participa en la lógica de ciclado en sí (ver
/// <see cref="ScreenCycle"/>).
/// </summary>
public interface IWidgetScreen
{
    /// <summary>Identificador corto y estable de la pantalla (p. ej. "usage", "mascot"), nunca usado para lógica de negocio.</summary>
    string ScreenId { get; }
}
```

### `Desktop/Navigation/ScreenCycle.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Navigation/ScreenCycle.cs
namespace ClaudeMeter.Desktop.Navigation;

/// <summary>
/// Ciclado por índice sobre una lista fija de tipos de pantalla. Sin
/// ninguna dependencia de Blazor/WPF -- mismo criterio de separación que
/// <c>Windowing.WindowPositionResolver</c> frente a <c>MainWindow</c> -- para
/// poder probarse con xUnit puro usando tipos cualesquiera como sustitutos
/// (AC de US-1: una única pantalla registrada nunca lanza al ciclar).
/// </summary>
public sealed class ScreenCycle
{
    private readonly IReadOnlyList<Type> _screens;

    public ScreenCycle(IReadOnlyList<Type> screens)
    {
        if (screens.Count == 0)
        {
            throw new ArgumentException("ScreenCycle requiere al menos una pantalla registrada.", nameof(screens));
        }

        _screens = screens;
    }

    public Type Current { get; private set; } = default!;

    /// <summary>Avanza al siguiente tipo en orden cíclico (tras el último, vuelve al primero) y lo devuelve. Con una única pantalla registrada, siempre devuelve la misma sin lanzar (AC de caso límite de US-1).</summary>
    public Type Next()
    {
        var currentIndex = _screens.IndexOf(Current) is var i && i >= 0 ? i : -1;
        Current = _screens[(currentIndex + 1) % _screens.Count];
        return Current;
    }
}
```

*Nota de implementación:* el constructor debe fijar `Current = screens[0]`
antes de que `Next()` se invoque por primera vez (omitido arriba por
brevedad de firma — el Software Developer debe inicializar `Current` en el
constructor, no solo declararlo con `default!`).

### `Desktop/Navigation/WidgetUsageState.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Navigation/WidgetUsageState.cs
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Desktop.Navigation;

/// <summary>
/// Datos ya calculados por <see cref="ScreenNavigator"/> en cada ciclo de
/// poll, pasados como único <c>[Parameter]</c> compartido a la pantalla
/// activa (<c>UsagePage</c>/<c>MascotPage</c>) -- evita que ambas dependan
/// de nombres de clave sueltos en <c>DynamicComponent.Parameters</c>.
/// </summary>
public sealed record WidgetUsageState(
    RateLimitWindow Session,
    RateLimitWindow Weekly,
    UsageSnapshotStatus? Status,
    bool IsStale)
{
    /// <summary>Estado antes de que se complete el primer ciclo de poll: ambas ventanas sin dato, sin estado, no desactualizado.</summary>
    public static WidgetUsageState Initial { get; } =
        new(RateLimitWindow.Unavailable, RateLimitWindow.Unavailable, Status: null, IsStale: false);
}
```

### `Desktop/Navigation/ScreenNavigator.razor` (Desktop, nuevo)

```razor
@* src/ClaudeMeter.Desktop/Navigation/ScreenNavigator.razor *@
@* RootComponent real de BlazorWebView (US-1, issue #18) -- único componente
   Razor garantizado vivo durante toda la vida de la aplicación. Absorbe
   íntegramente el ciclo de vida que hasta F3/Ciclo B gestionaba
   UsagePage.razor (coordinador de polling, PollingControlService, chime,
   tema, y los tres DotNetObjectReference de drag/resize/close vía JS
   interop) para que sea independiente de qué IWidgetScreen esté visible en
   cada momento -- ver Architecture Overview del documento de diseño de F3
   Ciclo C para el rationale completo. UsagePage/MascotPage pasan a ser
   puramente presentacionales, dirigidas por el [Parameter] WidgetUsageState
   que este componente les pasa. *@
@implements IDisposable
@inject IUsageDataSource UsageDataSource
@inject ILogger<UsagePollingCoordinator> Logger
@inject AppConfig Config
@inject IChimePlayer ChimePlayer
@inject WindowDragService DragService
@inject WindowResizeService ResizeService
@inject PollingControlService PollingControl
@inject WindowCloseService CloseService
@inject IJSRuntime JS
@inject ILogger<ScreenNavigator> PageLogger

<div class="claudemeter-root @Config.Theme.ToCssClass()">
    <button type="button" class="claudemeter-close" aria-label="Cerrar ClaudeMeter">✕</button>

    @* US-1 (issue #18): gesto de cambio de pantalla resuelto íntegramente
       en Blazor -- @onpointerdown:stopPropagation evita que el mismo clic
       dispare también WindowDragService.BeginDrag()/EndDrag() (drag.js
       escucha en document.documentElement), sin necesitar JS interop
       nuevo (ver Technology Choices). Solo se muestra si hay más de una
       pantalla registrada -- con una única pantalla, el AC de caso límite
       exige que ciclar no falle, pero no exige mostrar el control. *@
    @if (_cycle.ScreenCount > 1)
    {
        <button type="button" class="claudemeter-cycle" @onclick="CycleScreen"
                @onpointerdown:stopPropagation="true" aria-label="Cambiar pantalla">⇄</button>
    }

    <DynamicComponent Type="@_cycle.Current" Parameters="@_screenParameters" />
</div>

@code {
    /// <summary>Registro único de pantallas -- añadir una pantalla nueva (p. ej. PeakOffPeakPage en F4) solo exige extender este array, sin tocar ScreenCycle ni el resto de este fichero.</summary>
    private static readonly Type[] DefaultScreens = { typeof(UsagePage), typeof(MascotPage) };

    [Parameter]
    public IReadOnlyList<Type> ScreenTypes { get; set; } = DefaultScreens;

    private ScreenCycle _cycle = null!;
    private WidgetUsageState _state = WidgetUsageState.Initial;
    private IReadOnlyDictionary<string, object> _screenParameters = new Dictionary<string, object>();

    private UsagePollingCoordinator? _coordinator;
    private DotNetObjectReference<WindowDragService>? _dragServiceRef;
    private DotNetObjectReference<WindowResizeService>? _resizeServiceRef;
    private DotNetObjectReference<WindowCloseService>? _closeServiceRef;
    private RateLimitWindow _session = RateLimitWindow.Unavailable;
    private RateLimitWindow _weekly = RateLimitWindow.Unavailable;
    private UsageSnapshotStatus? _status;
    private UsageThreshold? _previousSessionThreshold;
    private UsageThreshold? _previousWeeklyThreshold;
    private bool _hasEverSucceeded;
    private bool _isStale;

    protected override void OnInitialized()
    {
        _cycle = new ScreenCycle(ScreenTypes);
        UpdateScreenParameters();

        _coordinator = new UsagePollingCoordinator(UsageDataSource, Config.PollingInterval, Logger);
        _coordinator.SnapshotReceived += OnSnapshotReceived;
        _coordinator.Start();

        PollingControl.AttachCoordinator(_coordinator);
        PollingControl.PauseStateChanged += OnPauseStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Idéntico a lo que hacía UsagePage hasta F3/Ciclo B -- se
            // ejecuta ahora una única vez en toda la vida de la app (antes
            // también una única vez, porque UsagePage era el único
            // RootComponent) -- cero cambio de comportamiento observable.
            _dragServiceRef = DotNetObjectReference.Create(DragService);
            try { await JS.InvokeVoidAsync("claudeMeterDrag.init", _dragServiceRef); }
            catch (Exception ex) { PageLogger.LogError(ex, "No se pudo registrar el listener de arrastre (claudeMeterDrag.init)"); }

            _resizeServiceRef = DotNetObjectReference.Create(ResizeService);
            try { await JS.InvokeVoidAsync("claudeMeterResize.init", _resizeServiceRef); }
            catch (Exception ex) { PageLogger.LogError(ex, "No se pudo registrar el ajuste automático de altura (claudeMeterResize.init)"); }

            _closeServiceRef = DotNetObjectReference.Create(CloseService);
            try { await JS.InvokeVoidAsync("claudeMeterClose.init", _closeServiceRef); }
            catch (Exception ex) { PageLogger.LogError(ex, "No se pudo registrar el listener de cierre directo (claudeMeterClose.init)"); }
        }
    }

    /// <summary>US-1: avanza ScreenCycle y refresca el DynamicComponent -- sin recargar ni perder el snapshot ya calculado (AC de US-1: el polling no se reinicia al cambiar de pantalla).</summary>
    private void CycleScreen()
    {
        _cycle.Next();
        UpdateScreenParameters();
    }

    private void UpdateScreenParameters() =>
        _screenParameters = new Dictionary<string, object> { ["State"] = _state };

    private void OnSnapshotReceived(UsageSnapshot snapshot, DateTimeOffset now)
    {
        _ = InvokeAsync(() =>
        {
            try
            {
                Apply(snapshot, now);
                UpdateScreenParameters();
                StateHasChanged();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                // Carrera benigna: MainWindow se cerró mientras esta
                // respuesta HTTP estaba en vuelo -- ver rationale idéntico
                // heredado de UsagePage (F1).
            }
        });
    }

    private void OnPauseStateChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (PollingControl.IsPaused)
            {
                _isStale = true;
            }
            _state = _state with { IsStale = _isStale };
            UpdateScreenParameters();
            StateHasChanged();
        });
    }

    /// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: permite a bUnit simular un ciclo de refresco concreto sin depender del timer real (mismo patrón heredado de <c>UsagePage.ApplyForTests</c>).</summary>
    internal void ApplyForTests(UsageSnapshot snapshot, DateTimeOffset now)
    {
        Apply(snapshot, now);
        UpdateScreenParameters();
    }

    internal bool IsPollingActiveForTests => _coordinator?.IsRunningForTests ?? false;

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
            // CORRECCIÓN de F3/Ciclo C respecto a F2: además de no marcar
            // "(desactualizado)", ahora también se resetean _session/_weekly
            // a Unavailable -- necesario para que MascotStateClassifier
            // resuelva NoData (AC de US-2). Invisible para UsagePage, que ya
            // ignoraba estos campos en este estado (ver Technology Choices).
            _isStale = false;
            _session = RateLimitWindow.Unavailable;
            _weekly = RateLimitWindow.Unavailable;
        }
        else if (_hasEverSucceeded)
        {
            _isStale = true;
        }
        else
        {
            _session = session; // == RateLimitWindow.Unavailable
            _weekly = weekly;
        }

        _state = new WidgetUsageState(_session, _weekly, _status, _isStale);
    }

    public void Dispose()
    {
        PollingControl.PauseStateChanged -= OnPauseStateChanged;
        _dragServiceRef?.Dispose();
        _resizeServiceRef?.Dispose();
        _closeServiceRef?.Dispose();
        if (_coordinator is not null)
        {
            PollingControl.DetachCoordinator(_coordinator);
            _coordinator.SnapshotReceived -= OnSnapshotReceived;
            _coordinator.Dispose();
        }
    }
}
```

*Nota:* `ScreenCycle.ScreenCount` (usado arriba en `@if (_cycle.ScreenCount
> 1)`) es una propiedad trivial (`_screens.Count`) omitida del snippet de
`ScreenCycle.cs` por brevedad — el Software Developer debe añadirla.

### `Pages/UsagePage.razor` (Desktop, modificado — versión completa tras el ciclo)

```razor
@* src/ClaudeMeter.Desktop/Pages/UsagePage.razor *@
@* Puramente presentacional desde F3/Ciclo C: ya no gestiona polling, tema,
   chime ni JS interop (todo eso vive ahora en ScreenNavigator, el
   RootComponent real) -- solo decide cómo pintar el WidgetUsageState que
   recibe. Ver documento de diseño de F3 Ciclo C, Architecture Overview. *@
@implements ClaudeMeter.Desktop.Navigation.IWidgetScreen
@using ClaudeMeter.Desktop.Navigation

@if (State.Status == UsageSnapshotStatus.Unauthorized)
{
    <ReauthNotice />
}
else
{
    <div class="usage-widget">
        <UsageBar Title="Sesión" Window="@State.Session" Stale="@State.IsStale" />
        <UsageBar Title="Semana" Window="@State.Weekly" Stale="@State.IsStale" />
    </div>
}

@code {
    [Parameter, EditorRequired]
    public WidgetUsageState State { get; set; } = WidgetUsageState.Initial;

    public string ScreenId => "usage";
}
```

### `Pages/MascotPage.razor` (Desktop, nuevo)

```razor
@* src/ClaudeMeter.Desktop/Pages/MascotPage.razor *@
@* Página Mascota ("Clawd", issue #19). Puramente presentacional: deriva
   MascotState del WidgetUsageState recibido (que a su vez procede
   directamente del último UsageSnapshot -- nunca de historial, restricción
   explícita de CLAUDE.md) en cada render, sin estado propio entre ciclos. *@
@implements ClaudeMeter.Desktop.Navigation.IWidgetScreen
@using ClaudeMeter.Desktop.Navigation

<div class="mascot mascot--@CssModifier">
    <span class="mascot__icon" aria-hidden="true">@Icon</span>
    <span class="mascot__label">@Label</span>
</div>

@code {
    [Parameter, EditorRequired]
    public WidgetUsageState State { get; set; } = WidgetUsageState.Initial;

    public string ScreenId => "mascot";

    // Recalculado en cada render a partir de State -- nunca se conserva
    // entre ciclos (AC de US-2: siempre función del snapshot actual).
    private MascotState CurrentState => MascotStateClassifier.Classify(State.Session, State.Weekly);

    private string CssModifier => CurrentState switch
    {
        MascotState.Calm => "calm",
        MascotState.Alert => "alert",
        MascotState.NearLimit => "near-limit",
        _ => "no-data",
    };

    private string Icon => CurrentState switch
    {
        MascotState.Calm => "😌",
        MascotState.Alert => "😬",
        MascotState.NearLimit => "😱",
        _ => "😶",
    };

    private string Label => CurrentState switch
    {
        MascotState.Calm => "Todo tranquilo",
        MascotState.Alert => "Cerca del aviso",
        MascotState.NearLimit => "Cerca del límite",
        _ => "Sin datos",
    };
}
```

### `MainWindow.xaml` (Desktop, modificado — excerpt)

```xml
<Window ...
        xmlns:pages="clr-namespace:ClaudeMeter.Desktop.Pages"
        xmlns:navigation="clr-namespace:ClaudeMeter.Desktop.Navigation"
        ...>
    <Grid>
        <Rectangle Fill="#01000000" />
        <blazor:BlazorWebView x:Name="BlazorWebViewHost" HostPage="wwwroot\index.html">
            <blazor:BlazorWebView.RootComponents>
                <blazor:RootComponent Selector="#app" ComponentType="{x:Type navigation:ScreenNavigator}" />
            </blazor:BlazorWebView.RootComponents>
        </blazor:BlazorWebView>
    </Grid>
</Window>
```

`xmlns:pages` se mantiene aunque este fichero ya no lo use directamente
(no hay ninguna otra referencia a `pages:` en `MainWindow.xaml`) — el
Software Developer puede eliminarlo si el analizador de XAML lo marca como
`using` no utilizado, sin que sea un requisito de este documento.

### `wwwroot/css/app.css` (Desktop, modificado — excerpt)

```css
/* F3/Ciclo C (US-1): mismo tratamiento visual que .claudemeter-close,
   posicionado en la esquina opuesta para no solaparse. */
.claudemeter-cycle {
    position: absolute;
    top: 4px;
    left: 4px;
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

.claudemeter-root:hover .claudemeter-cycle,
.claudemeter-cycle:focus-visible {
    opacity: 1;
}

.claudemeter-cycle:hover {
    background-color: var(--track-bg);
    color: var(--text-color);
}

/* F3/Ciclo C (US-2): MascotPage -- reutiliza las mismas custom properties
   de umbral que .usage-bar--{green,amber,red,neutral} para que la mascota
   y las barras nunca "cuenten historias distintas" sobre el mismo dato. */
.mascot {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 6px;
    padding: 16px;
    box-sizing: border-box;
    text-align: center;
}

.mascot__icon {
    font-size: 40px;
    line-height: 1;
}

.mascot__label {
    font-size: 13px;
    color: var(--text-color);
}

.mascot--calm .mascot__icon { color: var(--color-normal); }
.mascot--alert .mascot__icon { color: var(--color-warning); }
.mascot--near-limit .mascot__icon { color: var(--color-critical); }
.mascot--no-data .mascot__icon { color: var(--color-neutral); }
```

## Implementation Plan

1. **`Domain/Usage/MascotState.cs` (US-2):** crear `MascotState` +
   `MascotStateClassifier` tal cual Data Model.
2. **Tests de `MascotStateClassifier` (US-2, xUnit puro, nuevo
   `test/ClaudeMeter.Domain.Tests/Usage/MascotStateClassifierTests.cs`):**
   Normal/Normal → `Calm`; una ventana Warning y la otra Normal → `Alert`;
   una ventana Critical y la otra en cualquier otro estado (Normal,
   Warning, o incluso Critical también) → `NearLimit`; ambas `null` →
   `NoData`; una `null` y la otra con dato real (Normal/Warning/Critical) →
   se usa el umbral de la que sí tiene dato, nunca `NoData` (caso de
   snapshot parcialmente parseable, distinto del "ninguna tiene dato").
3. **`Desktop/Navigation/IWidgetScreen.cs` (US-1):** crear tal cual Data
   Model.
4. **`Desktop/Navigation/ScreenCycle.cs` (US-1):** crear tal cual Data
   Model, incluyendo `ScreenCount` (trivial, omitida del snippet).
5. **Tests de `ScreenCycle` (US-1, xUnit puro, nuevo
   `test/ClaudeMeter.Desktop.Tests/Navigation/ScreenCycleTests.cs`):**
   constructor lanza `ArgumentException` con lista vacía; `Current` es el
   primer elemento antes de cualquier `Next()`; `Next()` avanza en orden y
   vuelve al primero tras el último (probar con 2 y con 3 tipos
   cualesquiera, p. ej. `typeof(object)`/`typeof(string)`/`typeof(int)`
   como sustitutos triviales — no hace falta ningún componente Razor real
   para esta prueba); con una única pantalla registrada, `Next()` no lanza
   y siempre devuelve el mismo tipo (AC de caso límite de US-1).
6. **`Desktop/Navigation/WidgetUsageState.cs` (US-1/US-2):** crear tal cual
   Data Model.
7. **`Desktop/Navigation/ScreenNavigator.razor` (US-1):** crear tal cual
   Data Model — mover íntegramente a este fichero el `@code` de polling,
   chime, `PollingControlService`, y los tres registros de JS interop hoy
   en `UsagePage.razor`, aplicando la corrección de la rama `Unauthorized`
   señalada en Technology Choices.
8. **`Pages/UsagePage.razor` (US-1, modificación en profundidad):**
   sustituir su `@code` completo por la versión puramente presentacional de
   Data Model. **Este paso rompe la compilación de
   `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` tal como existe
   hoy** (monta `UsagePage` directamente y usa `ApplyForTests`/
   `IsPollingActiveForTests`/`StatusForTests`, que ya no existen en
   `UsagePage`) — ver paso 12.
9. **`Pages/MascotPage.razor` (US-2):** crear tal cual Data Model.
10. **`MainWindow.xaml` (US-1):** cambiar `RootComponent.ComponentType` a
    `ScreenNavigator` y añadir el `xmlns:navigation`, tal cual Data Model.
    Sin cambios en `MainWindow.xaml.cs`/`App.xaml.cs` (ver Architecture
    Overview).
11. **`wwwroot/css/app.css` (US-1/US-2):** añadir `.claudemeter-cycle` y
    `.mascot`/`.mascot--*` tal cual Data Model.
12. **Migración de `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`
    (US-1, obligatorio para que el build siga verde):** el fichero actual
    (26 tests: primer fetch, colores por umbral, stale, Unauthorized,
    chime en sus 6 variantes, tema, botón de cierre, pausa,
    desmontaje/desadjunte de `PollingControlService`) se divide en dos:
    - **Nuevo `test/ClaudeMeter.Desktop.Tests/Navigation/ScreenNavigatorTests.cs`:**
      recibe literalmente los tests que ejercitan polling/chime/tema/botón
      de cierre/pausa/desmontaje (todo lo que dependía de `ApplyForTests`/
      `IsPollingActiveForTests`), montando ahora `ScreenNavigator` en vez de
      `UsagePage` — el propio `RegisterCoreServices` ya existente se
      reutiliza casi sin cambios (las mismas dependencias siguen siendo
      necesarias, ahora las exige `ScreenNavigator`). Añadir además: un
      test de que la rama `Unauthorized` deja `_session`/`_weekly` en
      `Unavailable` (verificable indirectamente montando `ScreenNavigator`
      con `ScreenTypes` apuntando a un `MascotPage` real tras un ciclo
      Success→Unauthorized, y comprobando que se resuelve `NoData`, no la
      severidad del ciclo anterior).
    - **`UsagePageTests.cs` reescrito, mucho más simple:** monta `UsagePage`
      directamente pasando `WidgetUsageState` vía `Render<UsagePage>(p =>
      p.Add(x => x.State, new WidgetUsageState(...)))` — sin
      `RegisterCoreServices` en absoluto (ya no tiene dependencias
      inyectadas). Cubre: dos `UsageBar` con sus colores/valores cuando
      `Status != Unauthorized`; `ReauthNotice` cuando `Status ==
      Unauthorized`; sufijo "(desactualizado)" cuando `IsStale == true`.
      Los tests de tema/botón de cierre/chime/polling desaparecen de este
      fichero (ya no aplican a un componente puramente presentacional) —
      quedan cubiertos por `ScreenNavigatorTests.cs`.
13. **Tests de `ScreenNavigator` alternando pantallas simuladas (US-1, bUnit,
    dentro de `ScreenNavigatorTests.cs` o un fichero dedicado
    `Navigation/ScreenNavigatorCyclingTests.cs`):** definir 2-3 componentes
    Razor mínimos de prueba (`DummyScreenA`/`DummyScreenB`, cada uno
    `@implements IWidgetScreen` + `[Parameter] WidgetUsageState State` +
    un `<span>` con su propio `ScreenId` en el markup para poder
    localizarlos), montar `ScreenNavigator` con `ScreenTypes = new[]
    {typeof(DummyScreenA), typeof(DummyScreenB)}`, y verificar: se
    renderiza `DummyScreenA` primero; `cut.Find("button.claudemeter-cycle").Click()`
    hace que se renderice `DummyScreenB` en su lugar (y de vuelta a
    `DummyScreenA` en el siguiente clic); con una única pantalla en
    `ScreenTypes`, el botón `.claudemeter-cycle` NO se renderiza (ver
    Data Model) y el `DynamicComponent` sigue mostrando la única pantalla
    sin error.
14. **Tests bUnit de `MascotPage` (US-2, nuevo
    `test/ClaudeMeter.Desktop.Tests/Pages/MascotPageTests.cs`):** un test
    por cada uno de los 4 estados (`Calm`/`Alert`/`NearLimit`/`NoData`),
    montando `MascotPage` con un `WidgetUsageState` construido a mano
    (`RateLimitWindow` con los `PercentageUsed` deseados) y verificando la
    clase CSS `mascot--*` resultante; un test adicional para el caso
    "sesión Critical, semana Normal" y su simétrico "sesión Normal, semana
    Critical" → ambos `NearLimit` (AC de "gana el más severo,
    independientemente de cuál sea"); un test para `Status ==
    Unauthorized` con `Session`/`Weekly` en `Unavailable` (tal como los
    dejará `ScreenNavigator.Apply()` corregido) → `NoData`.
15. **Validación de compilación:** `dotnet build ClaudeMeter.sln` en Debug
    y Release (analizadores + `TreatWarningsAsErrors` en Release) — prestar
    atención especial a que `UsagePage`/`MascotPage` no queden con
    `@inject` huérfanos tras retirar las dependencias de polling/ventana
    (advertencia de Roslyn por servicio inyectado y no usado), y a que
    `DynamicComponent` no genere advertencias de trimming/AOT (no
    aplicable a este proyecto, que no publica con trimming — solo
    verificar que compila sin advertencias nuevas).

## Cross-Cutting Concerns

- **Seguridad:** ninguna superficie nueva — cambio de pantalla y
  representación de la mascota son controles puramente locales de UI, sin
  ninguna llamada de red ni dato de credenciales nuevo.
- **Rendimiento:** `ScreenCycle.Next()` es aritmética de índice O(1);
  `DynamicComponent` recrea el componente de la pantalla saliente al
  cambiar (coste de montar un componente Razor pequeño, sin temporizador ni
  E/S propios — ni `UsagePage` ni `MascotPage` tienen estado interno que
  perder al recrearse, ya que ambas son puras funciones de `State`);
  `MascotStateClassifier.Classify` es una función pura O(1), invocada una
  vez por render de `MascotPage`. Ningún cambio en la cadencia de poll
  (sigue siendo un único `UsagePollingCoordinator`, ahora en
  `ScreenNavigator`).
- **Manejo de errores:** el registro de JS interop (drag/resize/close)
  conserva exactamente los mismos `try/catch` + `LogError` ya existentes,
  solo reubicados; `MascotStateClassifier` nunca lanza (mismo estándar que
  `UsageThresholdClassifier`); `ScreenCycle` lanza `ArgumentException` solo
  en construcción con lista vacía (error de programación, no un caso de
  runtime alcanzable con `DefaultScreens` no vacío).
- **Observabilidad/logging:** sin cambios de nivel/formato respecto a
  Ciclos A/B — las mismas líneas `Information`/`Warning`/`Error` que ya
  existían en `UsagePage` se conservan tal cual en `ScreenNavigator`.
- **Compatibilidad hacia atrás:** ningún cambio en `config.json`/
  `AppConfig`/`AppConfigStore` — el cambio de pantalla y el estado de la
  mascota son estado de sesión, no persistido. El arranque por defecto
  (sin interacción del usuario) muestra `UsagePage` primero
  (`DefaultScreens[0]`), idéntico al comportamiento actual — cero regresión
  del estado inicial.

## Risks & Open Decisions

- **Refactor no trivial de `UsagePage.razor` y de su suite de tests ya
  commiteada (Impact: Medium, mitigado por el Implementation Plan):** este
  documento traslada la práctica totalidad del `@code` de `UsagePage` (26
  tests existentes en `UsagePageTests.cs`) a `ScreenNavigator`. Es un
  cambio mecánico (mover código, no reescribir lógica) pero de superficie
  amplia — el paso 12 del Implementation Plan detalla exactamente qué test
  va a qué fichero nuevo para que no se pierda cobertura por el camino. Es
  la consecuencia directa, ya anunciada por el propio Risk de Requirements,
  de resolver correctamente la centralización del ciclo de vida — no una
  regresión del ciclo de vida — no una regresión, es el coste ya aceptado
  de la decisión tomada en Architecture Overview.
- **Corrección de comportamiento en `Apply()`/rama `Unauthorized` (Impact:
  Low, ya resuelta en este documento):** ver Technology Choices — resetear
  `_session`/`_weekly` a `Unavailable` en `Unauthorized` es necesario para
  `MascotPage` y verificado como invisible para `UsagePage` (que ya
  ignoraba esos campos en ese estado). Sin decisión pendiente; documentado
  aquí solo para que el Software Developer y el QA Engineer lo tengan
  presente al migrar los tests del paso 12.
- **Botón de cambio de pantalla oculto con una única pantalla registrada
  (decisión de este documento, no un requisito explícito):** con
  `ScreenTypes.Count == 1`, `ScreenNavigator` no renderiza
  `.claudemeter-cycle` en absoluto (en vez de mostrarlo deshabilitado). Es
  una decisión de UX menor no exigida por ninguna AC (que solo pide que
  ciclar con una pantalla no falle, no que el control exista o no) —
  reconsiderable sin impacto arquitectónico si en el futuro se prefiriera
  mostrarlo siempre.
- **Emoji/etiquetas textuales exactas de `MascotPage` (Impact: Low):**
  Requirements deja explícitamente la redacción textual/visual final fuera
  de alcance funcional ("no es una decisión funcional de este documento").
  Los emoji/textos de este documento (Data Model) son un placeholder
  razonable, sustituible sin ningún cambio estructural si el usuario pide
  otra ilustración — no bloquea la implementación de este ciclo.
- **Cierre de milestone (heredado de Requirements, Impact: Medium):**
  este es el último ciclo de F3; la Definition of Done exige validación
  manual explícita de que el arrastre, el auto-ajuste de altura, el
  click-through, la bandeja y el cierre directo de Ciclos A/B siguen
  funcionando igual tras introducir `ScreenNavigator` como nuevo
  `RootComponent` — ningún agente de este pipeline puede verificar
  renderizado real de WPF/WebView2.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`): xUnit puro para
      `MascotStateClassifier` (Domain, incluyendo el caso de combinación
      `Session`/`Weekly` en umbrales distintos y el caso "sin datos") y
      para `ScreenCycle` (Desktop, lógica de ciclo sin WPF real); bUnit
      para `MascotPage.razor` cubriendo al menos los tres estados de
      consumo más el estado "sin datos", y para el comportamiento de
      `ScreenNavigator` alternando entre `IWidgetScreen` simuladas
- [ ] Ninguna prueba automatizada depende de un `UsageSnapshot` real de la
      API ni de interacción real de ratón/WebView2 — los tests usan
      snapshots simulados y, para `ScreenNavigator`, invocan directamente
      el método de cambio de pantalla (`cut.Find("button.claudemeter-cycle").Click()`,
      una interacción DOM simulada por bUnit, no un gesto de ratón real) en
      vez de simular el gesto de UI real
- [ ] **Validación manual:** se confirma, ejecutando la aplicación real en
      Windows, que (a) se puede alternar entre `UsagePage` y `MascotPage`
      con el botón de cambio de pantalla, (b) `MascotPage` refleja
      visualmente el estado correcto (tranquilo/alerta/cerca del
      límite/sin datos) en un escenario real de consumo, y (c) ninguna
      funcionalidad ya entregada en F3/Ciclos A y B (tema claro/oscuro,
      countdown animado, click-through, icono de bandeja con
      pausar/reanudar/recargar/salir, cierre directo) sufre una regresión
      al introducir `ScreenNavigator` como nuevo `RootComponent` — ningún
      agente de este pipeline puede verificar renderizado real de
      WPF/WebView2 ni interacción real de ratón
