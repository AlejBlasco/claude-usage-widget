# Implementation Summary: F1 — Widget visual base

## Design Reference

`docs/sdlc/design/f1-widget-visual-base.md` (Requirements: `docs/sdlc/requirements/f1-widget-visual-base.md`).
Cubre las 4 issues del milestone F1 como un único ciclo SDLC: US-1 (#6),
US-2 (#7), US-3 (#8) y US-4 (#9), con cadena de dependencia interna
#6 → #7 → #8 → #9. Se ha seguido el Implementation Plan del documento de
diseño paso a paso, sin cambios de arquitectura respecto a lo acordado.

## Files Changed

- `src/ClaudeMeter.Domain/Usage/UsageThreshold.cs` (nuevo) — enum
  `UsageThreshold` (`Normal`/`Warning`/`Critical`) y clase estática
  `UsageThresholdClassifier.Classify(double? percentageUsed)`, función pura
  sin dependencias externas. Umbrales: `<70` → Normal, `[70,90)` → Warning,
  `>=90` → Critical, `null` → `null` ("sin datos"). Único cambio fuera de
  `ClaudeMeter.Desktop` (US-3).
- `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — `Sdk="Microsoft.NET.Sdk"`
  → `Sdk="Microsoft.NET.Sdk.Razor"`; añadido `<RootNamespace>ClaudeMeter.Desktop</RootNamespace>`
  explícito; añadido `<PackageReference Include="Microsoft.AspNetCore.Components.WebView.Wpf" Version="8.0.100" />`
  (versión confirmada como la última estable de la serie 8.x en
  `api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.components.webview.wpf/index.json`
  en el momento de implementar — coincide con la ya verificada en Design);
  añadido `<InternalsVisibleTo Include="ClaudeMeter.Desktop.Tests" />`.
  `OutputType=WinExe`, `TargetFramework=net8.0-windows` y `UseWPF=true` sin
  cambios.
- `src/ClaudeMeter.Desktop/_Imports.razor` (nuevo) — `@using` de
  `Microsoft.AspNetCore.Components.Web`, `ClaudeMeter.Application.Abstractions`
  (namespace hijo, nunca el raíz `ClaudeMeter.Application`), `ClaudeMeter.Domain.Usage`,
  `ClaudeMeter.Desktop.Polling` y `ClaudeMeter.Desktop.Pages`.
- `src/ClaudeMeter.Desktop/wwwroot/index.html` (nuevo) — host page mínima
  del `BlazorWebView`, sin Bootstrap ni CSS de terceros (decisión ya
  documentada en Design).
- `src/ClaudeMeter.Desktop/wwwroot/css/app.css` (nuevo) — clases
  `.usage-widget`, `.usage-bar`, `.usage-bar--green/amber/red/neutral`,
  `.usage-bar__*`; fondo oscuro opaco intencional (`#20232aee`) dentro del
  `BlazorWebView` (limitación de "airspace" de WebView2 ya documentada en
  Design: la transparencia real de la ventana solo se aprecia alrededor del
  control, nunca dentro).
- `src/ClaudeMeter.Desktop/App.xaml.cs` — composition root real (antes
  vacío): en `OnStartup` construye el `HttpClient` compartido, un
  `IServiceCollection` con `AddWpfBlazorWebView()` + `ITokenProvider`
  (`CredentialsFileTokenProvider`) + `IUsageDataSource`
  (`AnthropicApiUsageDataSource`) como singletons, y expone
  `IServiceProvider Services`; en `OnExit` libera el `HttpClient`. La
  declaración de clase mantiene `App : System.Windows.Application` (mismo
  patrón ya presente antes de este cambio, mitigación de la colisión
  conocida con `ClaudeMeter.Application`).
- `src/ClaudeMeter.Desktop/MainWindow.xaml` — reemplazada la ventana
  scaffold por defecto: `WindowStyle="None"`, `AllowsTransparency="True"`,
  `Background="Transparent"`, `Topmost="True"`, `ResizeMode="NoResize"`,
  `ShowInTaskbar="False"`, `Width="280" Height="140"`,
  `WindowStartupLocation="Manual"`; aloja un único `BlazorWebView` cuyo
  `RootComponent` es `Pages.UsagePage` sobre el selector `#app`.
- `src/ClaudeMeter.Desktop/MainWindow.xaml.cs` — en el constructor asigna
  `BlazorWebViewHost.Services` desde `((App)System.Windows.Application.Current).Services`
  (siempre cualificado, norma defensiva de Design) y posiciona la ventana en
  la esquina inferior derecha del área de trabajo (`SystemParameters.WorkArea`)
  con un margen de 16px.
- `src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs` (nuevo) —
  clase C# plana `IDisposable` que envuelve un `System.Timers.Timer`
  (siempre cualificado, sin `using System.Timers;`) de intervalo
  configurable sobre `IUsageDataSource.GetUsageAsync`, con guard
  `_isPolling` contra solapes y evento `Action<UsageSnapshot, DateTimeOffset> SnapshotReceived`.
  Miembros `internal` (`PollOnceForTestsAsync`, `IsRunningForTests`) visibles
  vía `InternalsVisibleTo` para el futuro test bUnit.
- `src/ClaudeMeter.Desktop/Pages/UsageBar.razor` (nuevo) — componente hijo
  reutilizado para sesión y semana: barra `<div>`+`width` dinámico (sin
  `<progress>` nativo, decisión ya tomada en Design), etiqueta de porcentaje
  con marca "(desactualizado)" si `Stale`, o "No disponible" si
  `PercentageUsed` es `null`. Calcula el color exclusivamente vía
  `UsageThresholdClassifier.Classify`, la misma función para ambas barras
  (US-3).
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` (nuevo) — `RootComponent`
  del `BlazorWebView`. Crea su propio `UsagePollingCoordinator` de 60s en
  `OnInitialized`, aplica cada `UsageSnapshot` recibido con
  `RateLimitWindowParser.ParseSnapshot` (reutilizado tal cual de F0, sin
  reimplementar parseo), conserva el último valor válido marcado
  "desactualizado" si un ciclo posterior falla, envuelve
  `InvokeAsync`/`StateHasChanged` en un `try/catch` que ignora
  `ObjectDisposedException`/`InvalidOperationException` (carrera benigna de
  cierre documentada en Design) y libera el coordinador en `Dispose()`.

## Deviations from the Design

- **`using System.Net.Http;` añadido en `App.xaml.cs`** — el snippet del
  documento de diseño no lo incluía. Necesario porque, a diferencia de
  `Microsoft.NET.Sdk.Web`, el conjunto de *implicit usings* de
  `Microsoft.NET.Sdk.Razor` no incluye `System.Net.Http`; sin el `using`
  explícito el tipo `HttpClient` no se resuelve (`CS0246`). Es una
  corrección mecánica de compilación, no un cambio de diseño — se detectó
  en el primer intento de build y se corrigió antes de continuar.
- Ninguna otra desviación: el resto del código (`UsageThreshold`/
  `UsageThresholdClassifier`, `UsagePollingCoordinator`, `UsageBar.razor`,
  `UsagePage.razor`, XAML de `MainWindow`, csproj) se implementó tal cual la
  sección Data Model / Implementation Plan del documento de diseño.
- **`test/ClaudeMeter.Desktop.Tests`**: no se ha tocado ningún fichero de
  este proyecto (ni su `.csproj`, que sigue en `Sdk="Microsoft.NET.Sdk"` sin
  migrar a `Microsoft.NET.Sdk.Razor`, ni se ha creado ningún test/`TestDoubles`).
  Los pasos 9-12 del Implementation Plan del diseño (tests de Domain,
  migración del csproj de test a Razor, `FakeUsageDataSource` reconfigurable,
  tests bUnit) quedan explícitamente fuera de esta fase — son responsabilidad
  de `sdlc-testing`, según la regla dura de este agente de no tocar ningún
  proyecto `*.Tests`. Se confirma que el build en Debug y Release compila
  igualmente el proyecto de test sin cambios (sigue vacío de tests, sin
  referencias a bUnit `BunitContext` todavía).

## How to Verify

1. `dotnet build ClaudeMeter.sln -c Debug` y `-c Release` — ambos deben
   compilar sin advertencias ni errores (verificado en esta fase, ver
   sección de resultado de build en el informe del agente).
2. Ejecutar `ClaudeMeter.Desktop` en un equipo Windows real (no automatizable
   por este pipeline): confirmar visualmente que `MainWindow` aparece sin
   bordes/barra de título, permanece topmost sobre otras ventanas, se
   posiciona en la esquina inferior derecha de la pantalla, y que el área
   fuera del `BlazorWebView` es realmente transparente (US-1, Definition of
   Done de Design/Requirements).
3. Con un `.credentials.json` válido, confirmar que las dos barras
   (Sesión/Semana) muestran porcentaje y color acorde al umbral (verde
   `<70`, ámbar `[70,90)`, rojo `>=90`) y que sin token/con 401/403 ambas
   barras muestran "No disponible" (US-2/US-3).
4. Dejar la aplicación corriendo varios minutos y confirmar en el Task
   Manager que no crece el número de handles/threads entre ciclos de 60s, y
   que al cerrar la ventana el proceso termina limpiamente sin llamadas HTTP
   pendientes (US-4; la validación de 30-60 minutos de la Definition of Done
   queda pendiente de validación manual del usuario, como ya marca el
   documento de diseño).
5. Cuando `sdlc-testing` migre `test/ClaudeMeter.Desktop.Tests` a
   `Microsoft.NET.Sdk.Razor`, los miembros `internal` ya expuestos
   (`UsagePollingCoordinator.PollOnceForTestsAsync`/`IsRunningForTests`,
   `UsagePage.ApplyForTests`/`IsPollingActiveForTests`) permiten escribir los
   tests bUnit descritos en el documento de diseño sin esperar temporizadores
   reales.

## Follow-ups / Known Limitations

- `test/ClaudeMeter.Desktop.Tests` sigue sin migrar a `Microsoft.NET.Sdk.Razor`
  y sin ningún test — pendiente de `sdlc-testing` (pasos 9-12 del
  Implementation Plan de Design).
- Ningún logging estructurado (Serilog) ni `config.json` todavía — alcance
  explícito de F2, no de F1.
- Posición/tamaño de `MainWindow` siguen fijos en código (280×140, esquina
  inferior derecha con margen de 16px); la configurabilidad es F2.
- El riesgo de "airspace" entre la transparencia de `MainWindow` y el
  rectángulo opaco de WebView2 es una limitación de plataforma aceptada
  desde Requirements/Design, sin mitigación adicional en F1.
- Validaciones manuales pendientes de un equipo Windows real (no
  automatizables por este pipeline): comportamiento visual de US-1 y
  estabilidad de memoria/handles de US-4 en una ejecución prolongada — ya
  marcadas como tales en la Definition of Done de Requirements/Design.
