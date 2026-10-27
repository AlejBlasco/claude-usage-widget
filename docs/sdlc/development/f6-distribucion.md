# Implementation Summary: F6 — Distribución

## Design Reference

`docs/sdlc/design/f6-distribucion.md` (Requirements: `docs/sdlc/requirements/f6-distribucion.md`).
Cubre el milestone F6 — Distribución (GitHub milestone #7), 3 historias
encadenadas US-1 (#27, versionado) → US-2 (#25, empaquetado) → US-3 (#26,
autoarranque + auto-actualización). Se ha seguido el Implementation Plan del
documento de diseño paso a paso (1.1-1.4, CI/CD base, 3.1-3.4, 4.1-4.5, 5),
con una desviación significativa en el paso 1.3 (ver Deviations).

## Files Changed

### US-1 — Versionado (Nerdbank.GitVersioning)

- `version.json` (nuevo) — generado con `nbgv install` y ajustado a la
  semilla/refspec fijadas por el diseño: `"version": "1.0"`,
  `publicReleaseRefSpec` con `master` + tags `v\d+\.\d+`.
- `src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj`,
  `src/ClaudeMeter.Application/ClaudeMeter.Application.csproj`,
  `src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj`,
  `src/ClaudeMeter.Console/ClaudeMeter.Console.csproj`,
  `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — `PackageReference`
  a `Nerdbank.GitVersioning` 3.10.94 (`PrivateAssets="all"`) añadido en cada
  proyecto de `src/` individualmente, **no** centralizado en
  `Directory.Build.props` como proponía el diseño (ver Deviations).
  `Directory.Build.props` queda sin cambios.

### US-2 — Empaquetado (Velopack)

- `.github/workflows/ci.yml` (nuevo) — `restore` → `build -c Release` →
  `test`, en cada push y en cada PR contra `master`, `windows-latest`,
  `fetch-depth: 0` (obligatorio para que Nerdbank.GitVersioning calcule la
  altura de commits). `actions/checkout@v7` / `actions/setup-dotnet@v6`
  (últimas versiones mayores verificadas en GitHub al implementar).
- `.github/workflows/release.yml` (nuevo) — trigger `tags: v*`; instala
  `nbgv`/`vpk`, `restore` → `build -c Release` → `test` →
  `dotnet publish ... -r win-x64 --self-contained true` → calcula la
  versión con `nbgv get-version -v NuGetPackageVersion` → `vpk pack`
  (`--framework webview2`, `--runtime win-x64`) → `vpk upload github
  --publish --tag`. `permissions: contents: write` explícito, usa el
  `GITHUB_TOKEN` por defecto de la Action.
- `src/ClaudeMeter.Desktop/Program.cs` (nuevo) — `Main()` explícito con
  `[STAThread]`; `Velopack.VelopackApp.Build().Run()` como primera línea,
  antes de construir `App` (técnica oficial de Velopack para WPF).
- `src/ClaudeMeter.Desktop/App.xaml.cs` — sin cambios de contenido salvo
  los de US-3 (ver abajo); sigue derivando de `System.Windows.Application`
  con `OnStartup`/`OnExit`, ahora invocado desde `Program.cs` en vez de un
  `Main()` autogenerado.
- `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — `<StartupObject>`
  apuntando a `Program`; `ItemGroup` que quita `App.xaml` de
  `ApplicationDefinition` y lo vuelve a añadir como `Page` (con
  `Generator=MSBuild:Compile`); `PackageReference` a `Velopack` 1.2.158.
  `App.xaml` en sí no se modificó (el cambio de Build Action se hace por
  csproj, no tocando el fichero `.xaml`).

### US-3 — Autoarranque + auto-actualización

- `src/ClaudeMeter.Desktop/Configuration/AppConfig.cs` — nuevo parámetro
  `AutoStartEnabled` (por defecto `false`, mismo criterio opt-in que
  `ChimeEnabled`), añadido al final del `record` (compatibilidad con
  construcciones posicionales existentes, mismo patrón que `Theme` en F3).
- `src/ClaudeMeter.Desktop/Configuration/AppConfigStore.cs` — DTO con
  `autoStartEnabled` (`bool?`), resuelto con `?? AppConfig.Default.AutoStartEnabled`
  en `Load()` y serializado en `Save()`; sin función `Resolve*` propia (un
  `bool` no tiene rango inválido que detectar, igual que `ChimeEnabled`).
- `src/ClaudeMeter.Desktop/Startup/AutoStartService.cs` (nuevo) — registra/
  retira `ClaudeMeter` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
  Resuelve la ruta estable vía `Velopack.Locators.VelopackLocator.Current`
  (`RootAppDir` + `AppId`), nunca `Environment.ProcessPath`. Guarda
  explícitamente ante `VelopackLocator.IsCurrentSet == false` (proceso sin
  contexto de instalación Velopack real, p. ej. `dotnet run` en desarrollo
  local) y ante `RootAppDir == null`, registrando un `Warning` y omitiendo
  la escritura en el registro en ambos casos en vez de lanzar — no estaba
  explícito en el esqueleto del diseño, pero es necesario porque
  `RootAppDir` es de tipo `string?` (ver Deviations). Persiste el toggle en
  `config.json` vía `AppConfigStore` en cada `SetEnabled`.
- `src/ClaudeMeter.Desktop/Updates/AppUpdateService.cs` (nuevo) — envuelve
  `Velopack.UpdateManager` + `Velopack.Sources.GithubSource` apuntando al
  repo; comprobación al arrancar + `System.Timers.Timer` cada 4h; guarda
  `!_updateManager.IsInstalled` para omitir la comprobación fuera de una
  instalación real; `catch (Exception ex)` deliberadamente genérico
  (degradación silenciosa, AC de US-3). `ApplyUpdatesAndRestart(toApply: null)`
  aplica la última versión descargada (la API real recibe un
  `VelopackAsset?`, no el `UpdateInfo` que asumía el esqueleto del diseño —
  ver Deviations).
- `src/ClaudeMeter.Desktop/Tray/TrayIconService.cs` — nuevo
  `ToolStripMenuItem _autoStartItem` ("Iniciar con Windows"), mismo patrón
  que `_clickThroughItem` (`CheckOnClick = false`, toggle manual +
  suscripción a `StateChanged`); a diferencia de pausa/click-through, el
  `Checked` inicial en `Initialize()` sí refleja `_autoStart.IsEnabled`
  (estado ya persistido). Constructor gana el parámetro `AutoStartService`.
- `src/ClaudeMeter.Desktop/App.xaml.cs` — registra `AutoStartService` y
  `AppUpdateService` como singletons; llama a `AutoStartService.Initialize()`
  y `AppUpdateService.Start()` al final de `OnStartup`; `AppUpdateService.Dispose()`
  en `OnExit`, junto al `Dispose()` ya existente de `TrayIconService`.

## Deviations from the Design

- **`PackageReference` de Nerdbank.GitVersioning repetido por proyecto en
  vez de centralizado en `Directory.Build.props`.** El documento de diseño
  pedía centralizarlo ahí, siguiendo el patrón ya usado para TFM/Nullable/
  analizadores. Al implementarlo se detectó y reprodujo de forma
  determinista (cache de NuGet limpia, `obj`/`bin` limpios, probado con
  varios paquetes distintos para descartar que fuera específico de nbgv)
  que un `PackageReference` sin condición **o condicionado** en
  `Directory.Build.props` rompe `dotnet build`/`dotnet restore` a nivel de
  solución en este entorno (.NET SDK 9.0.317/10.0.400) con
  `NETSDK1013: Identificador de marco de trabajo no válido ""`: MSBuild
  genera un metaproyecto sintético para la propia `ClaudeMeter.sln` que
  también hereda `Directory.Build.props` por ubicación, sin tener contexto
  de restauración de paquetes ni `TargetFramework` real, y NuGet intenta
  resolver igualmente ese `PackageReference` para él. Probar
  `Condition="'$(TargetFramework)' != ''"` no lo evitaba; sí lo evitaba
  `Condition="'$(MSBuildProjectExtension)' == '.csproj'"`, pero se optó por
  la alternativa más simple y menos frágil (repetir el `PackageReference`
  en los 5 `.csproj` de `src/`, como habría hecho `nbgv install` por
  defecto si no se hubiese consolidado) para no depender de una condición
  MSBuild cuyo comportamiento exacto en el metaproyecto de la `.sln` no
  está documentado por Microsoft/NuGet. Verificado: `dotnet build` de cada
  proyecto de `src/` y de `ClaudeMeter.sln` completo en Release (warnings
  como errores) compila sin advertencias con este enfoque.
- **`AutoStartService` añade guardas de nulabilidad no explícitas en el
  esqueleto del diseño**: `VelopackLocator.IsCurrentSet` (el proceso puede
  no correr desde una instalación Velopack real, p. ej. en desarrollo
  local) y `RootAppDir` como `string?` (el compilador lo exige con
  `Nullable` habilitado). Sin estas guardas el build fallaría en Release
  (warning `CS8604` como error). Documentado también en el propio XMLDoc
  del método.
- **`AppUpdateService.ApplyUpdatesAndRestart`** recibe un `VelopackAsset?`
  (no un `UpdateInfo` como sugería el pseudocódigo del diseño); se pasa
  `toApply: null`, que la propia documentación de Velopack indica que
  aplica automáticamente la última versión ya descargada — comportamiento
  equivalente al que buscaba el diseño.
- **API exacta de Velopack confirmada al implementar** (tareas 3.4/4.2 del
  Implementation Plan, dejadas como verificación mecánica pendiente por el
  propio diseño): `Velopack.Locators.VelopackLocator.Current`/`IsCurrentSet`
  (no un método estático distinto) para obtener el locator activo; `vpk
  pack` acepta el bootstrapper de WebView2 vía `--framework webview2`
  (confirmado contra el código fuente de Velopack en GitHub, no solo su
  documentación); se añadió también `--runtime win-x64` a `vpk pack`, no
  mencionado en el diseño, tras comprobar con un empaquetado real de prueba
  que `vpk pack` asume x86 por defecto si no se especifica.
- **`Nerdbank.GitVersioning` 3.10.94, no 3.10.85** — última estable real de
  la serie 3.10.x en `api.nuget.org` al implementar (el diseño ya preveía
  esta confirmación como mecánica, no como decisión abierta).
- **`TrayIconServiceTests.cs` queda sin actualizar y `dotnet build
  ClaudeMeter.sln`/`dotnet test` fallarán hasta que se corrija** (ver
  Follow-ups) — consecuencia directa y necesaria de la Regla dura nº 5 de
  este agente (nunca crear/modificar/ejecutar ficheros de test), en
  tensión con el hecho de que `TrayIconService` gana un parámetro de
  constructor obligatorio (`AutoStartService`) por diseño explícito de
  US-3. Los 5 proyectos de `src/` compilan limpios de forma aislada (con
  warnings como errores en Release); solo el proyecto de test
  `ClaudeMeter.Desktop.Tests` no compila por este motivo.

## How to Verify

1. `dotnet build src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj src/ClaudeMeter.Application/ClaudeMeter.Application.csproj src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj src/ClaudeMeter.Console/ClaudeMeter.Console.csproj src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj -c Release` (uno a uno, o vía `dotnet build ClaudeMeter.sln -c Release` que compilará también `src/`, deteniéndose después en el error conocido de `ClaudeMeter.Desktop.Tests` — ver Deviations/Follow-ups): los 5 deben compilar en 0 advertencias/0 errores.
2. `nbgv get-version` desde la raíz del repo: debe mostrar una versión `1.0.0+<hash>` (no pública, por estar en `features/f6`, no en `master` ni en un tag `v*`).
3. `dotnet publish src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj -c Release -r win-x64 --self-contained true -o publish/` seguido de `vpk pack --packId ClaudeMeter --packVersion 1.0.0 --packDir publish --mainExe ClaudeMeter.Desktop.exe --packTitle "ClaudeMeter" --framework webview2 --runtime win-x64 --outputDir Releases`: debe completar y loguear "Verified VelopackApp.Run() in ... ClaudeMeter.Desktop.Program::Main()." (ambos pasos ya se ejecutaron localmente durante esta implementación con éxito).
4. Revisar `.github/workflows/ci.yml` y `.github/workflows/release.yml` manualmente (no se han podido ejecutar en GitHub Actions real desde este agente).
5. Validaciones manuales que exigen una máquina Windows limpia/un ciclo real de release (instalación limpia, autoarranque tras reinicio, auto-actualización entre dos releases, WebView2 no preinstalado) quedan fuera del alcance de este agente, tal como ya anticipaba la Definition of Done del documento de diseño.

## Follow-ups / Known Limitations

- **Bloqueante para `sdlc-testing`**: `test/ClaudeMeter.Desktop.Tests/Tray/TrayIconServiceTests.cs` no compila (`CS7036`) porque `TrayIconService` ahora requiere un `AutoStartService` en su constructor. El único call site afectado es `CreateService()` (línea 41): necesita construir un `AutoStartService` (p. ej. `new AutoStartService(AppConfig.Default, new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<AutoStartService>.Instance)`, mismo patrón que el resto de dependencias de ese test) y pasarlo al constructor de `TrayIconService`. El test `Constructor_..._YConstruyeLosCuatroItemsDelMenu` también debería actualizarse (ahora son 5 ítems de menú, no 4) y añadirse cobertura para `AutoStartItemForTests`/`OnAutoStartStateChanged`, siguiendo el mismo patrón que `ClickThroughItemForTests`.
- Sin tests nuevos para `AutoStartService`/`AppUpdateService`/los cambios de `AppConfig`/`AppConfigStore` — íntegramente responsabilidad de `sdlc-testing`, como en cada ciclo anterior.
- La firma de código del instalador sigue fuera de alcance (ya señalada como riesgo en requirements/design).
- `dotnet-ef`/`dotnet-reportgenerator-globaltool` ya estaban instalados como herramientas globales en este entorno; se instalaron además `nbgv` 3.10.94 y `vpk` 1.2.158 (necesarias para implementar y verificar US-1/US-2) — el usuario deberá tenerlas disponibles en el runner de `release.yml` (ya cubierto por el paso `dotnet tool install --global` del propio workflow) y, si quiere reproducir los pasos de verificación manual localmente, instalarlas también en su máquina.
