# Implementation Summary: Migración de .NET 8 a .NET 10 (LTS)

## Design Reference
Issue de GitHub #37 "[Chore] Migrar la solución de .NET 8 a .NET 10 (LTS)". No hay documento de diseño: el issue hace de requisitos y diseño.

## Files Changed
- `Directory.Build.props` — TFM `net8.0` -> `net10.0` (único sitio para los proyectos no-Windows).
- `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — TFM `net10.0-windows10.0.17763.0` (ver Corrección posterior); paquetes subidos (ver tabla); comentarios XML de los paquetes reescritos para no quedar falsos.
- `src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj` — `Microsoft.Extensions.Logging.Abstractions` a 10.0.14 y comentario actualizado.
- `test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj` — TFM `net10.0-windows10.0.17763.0` y `StaticWebAssetsEnabled=false` (ver Deviations). Ningún `.cs` de test tocado.
- `.github/workflows/ci.yml`, `.github/workflows/release.yml` — `dotnet-version: '10.0.x'`, nombre del step ("Configurar el SDK de .NET 10") y comentarios `net10.0-windows`.
- `README.md` — badge .NET 10 y enlace/requisito del SDK 10.
- `CLAUDE.md` — línea "Stack: .NET 10, ...".
- `version.json` — `1.1.0` -> `1.2.0`.

## Paquetes (antes -> después)
| Paquete | Antes | Después | Justificación |
|---|---|---|---|
| Microsoft.AspNetCore.Components.WebView.Wpf | 8.0.100 | 10.0.120 | Última estable de la serie 10; la 8.x no se alinea con net10 |
| Microsoft.Extensions.Logging | 8.0.1 | 10.0.14 | Alineación con .NET 10 (última estable 10.0.x) |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | 10.0.14 | Idem |
| Serilog.Extensions.Logging | 8.0.0 | 10.0.0 | Última estable; acompaña a Microsoft.Extensions.* 10 |
| Velopack | 1.2.158 | sin cambio | Compila y publica; existe 1.2.161, pero se deja para no desalinear con la CLI `vpk` usada en release.yml |
| Serilog 4.4.0, Serilog.Sinks.File 7.0.0, Nerdbank.GitVersioning 3.10.94, BannedApiAnalyzers 5.6.0 | — | sin cambio | Ya son la última estable y compilan |
| xUnit 2.9.2, bUnit 2.11.3, Microsoft.NET.Test.Sdk 17.12.0, coverlet 6.0.2, xunit.runner.visualstudio 2.8.2 | — | sin cambio | Compilan con net10; no era necesario subirlos (candidatos a revisión por `sdlc-testing`) |

Versiones verificadas contra `api.nuget.org/v3-flatcontainer`.

## Deviations from the Design
- **Añadido `StaticWebAssetsEnabled=false` en el .csproj de tests de Desktop.** No estaba en el issue. Con el SDK 10, el build Release fallaba con `error : Conflicting assets with the same target path '_framework/blazor.modules#[.{fingerprint}]?.json'` (el proyecto Razor de test descubría el asset del paquete `Microsoft.AspNetCore.Components.WebView` 10.0.0 por dos vías). Los tests bUnit no sirven contenido web, así que desactivarlo es el cambio mínimo. Es un cambio solo de .csproj; `sdlc-testing` debe confirmar que los tests siguen en verde.
- Velopack no subido (ver tabla).

## Warnings/errores nuevos
- Único problema: el conflicto de static web assets descrito arriba (resuelto).
- Analizadores (`AnalysisLevel=latest`) y APIs obsoletas: 0 warnings, 0 errores. No fue necesario tocar código ni suprimir reglas.

## Resultados
- `dotnet build ClaudeMeter.sln -c Release`: correcto, 0 advertencias, 0 errores.
- `dotnet publish src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj -c Release -r win-x64 --self-contained true -o <scratchpad>/pub`: correcto; runtime incluido 10.0.11, salida 178 MB (carpeta temporal fuera del repo; no se compara con net8 por no haberlo medido). Sin artefactos dentro del repo.

## How to Verify
1. `dotnet build ClaudeMeter.sln -c Release` sin warnings.
2. `dotnet test ClaudeMeter.sln` (lo ejecuta `sdlc-testing`): deben pasar los 359 tests.
3. En Windows real: arranque del widget, arrastre, cambio barras/mascota, tema claro/oscuro, click-through, icono y menú de bandeja.

## Follow-ups / Known Limitations
Pendientes (fuera de mi alcance):
- Ejecución de `dotnet test` (359 tests) y verificación en CI del job `build-and-test`.
- Validación manual en Windows real (WPF + WebView2) listada arriba.
- `vpk pack` del publish net10, comprobación del tamaño del instalador y que funcione en instalación limpia.
- Prueba de auto-actualización desde la última release net8 a la nueva (1.2.0) y etiquetado.
- Revisión opcional de paquetes de test y de Velopack 1.2.161.
- Los `.gitattributes`/EOL: git avisa de LF->CRLF en varios ficheros editados (aviso habitual de Windows, no es un cambio funcional).

## Corrección posterior (validación manual)
Al arrancar la app con `dotnet run --project src/ClaudeMeter.Desktop -c Release` la primera vez, el proceso terminaba con una excepción no controlada:

```
FileNotFoundException: Could not load file or assembly 'Microsoft.Windows.SDK.NET, Version=10.0.17763.10'
   at Microsoft.Web.WebView2.Wpf.WebView2CompositionControl.TryInitializeD3DImage()
```

**Causa:** la subida de `Microsoft.AspNetCore.Components.WebView.Wpf` de 8.0.100 a 10.0.120 arrastra un WebView2 más nuevo, cuyo `WebView2CompositionControl` necesita las proyecciones de WinRT (`Microsoft.Windows.SDK.NET`), que solo se incluyen cuando el TFM lleva una versión de Windows. Con `net10.0-windows` a secas no están. Ni el build ni los 359 tests podían detectarlo, porque no instancian un `BlazorWebView` real.

**Arreglo:** TFM `net10.0-windows10.0.17763.0` en `ClaudeMeter.Desktop.csproj` y en `ClaudeMeter.Desktop.Tests.csproj` (este debe coincidir con Desktop). Efecto secundario: el TFM fija Windows 10 1809 (build 17763) como versión mínima de sistema operativo.

**Verificación:** build Release sin warnings, 359/359 tests en verde y arranque y recorrido manual del widget correctos.

**Lección:** `dotnet publish` terminando bien no demuestra que la app publicada arranque; hay que ejecutarla antes de etiquetar una release.

