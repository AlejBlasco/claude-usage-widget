# Requirements: F6 — Distribución

## Source
Milestone **F6 — Distribución** (GitHub milestone #7,
https://github.com/AlejBlasco/claude-usage-widget/milestone/7),
issues #27, #25 y #26 —
https://github.com/AlejBlasco/claude-usage-widget/issues/27,
/issues/25, /issues/26.

Corresponde a la fase F6 del roadmap en `CLAUDE.md`: *"distribution: Velopack
or MSIX installer, autostart + auto-update, versioning via
Nerdbank.GitVersioning"*. Siguiendo el mismo patrón usado en F0-F3 de este
repositorio (un PR por fase), este documento cubre las 3 issues del
milestone como una sola entrega, con una cadena de dependencias interna
explícita: **US-1 (#27) → US-2 (#25) → US-3 (#26)**. El trabajo de esta fase
es de build/CI/empaquetado y no requiere tocar Domain/Application (según
`CLAUDE.md`).

## US-1: Versionado automático con Nerdbank.GitVersioning (issue #27)

**As a** desarrollador del proyecto,
**I want** versionado automático de la solución con Nerdbank.GitVersioning,
**so that** no tenga que llevar el número de versión a mano en cada release.

### Acceptance Criteria

- **GIVEN** el repositorio con Nerdbank.GitVersioning integrado y un fichero
  `version.json` en la raíz de la solución
  **WHEN** se compila cualquiera de los proyectos de `src/`
  (Domain/Application/Infrastructure/Desktop)
  **THEN** el ensamblado generado incluye una versión (`AssemblyVersion` /
  `AssemblyInformationalVersion`) calculada a partir de `version.json` y del
  commit de git actual, sin ningún número de versión hardcodeado a mano

- **GIVEN** un commit nuevo en la rama por encima del último tag de versión
  **WHEN** se compila la solución
  **THEN** el número de versión calculado incrementa de forma determinista
  (altura de git desde el tag), sin intervención manual del desarrollador

- **GIVEN** un directorio de trabajo con cambios locales sin commitear
  ("dirty")
  **WHEN** se compila localmente
  **THEN** el build no falla, pero el número de versión resultante deja
  constancia de que el build no es reproducible/oficial (p. ej. sufijo de
  commit-id), para no confundirlo con un build de release

- **GIVEN** el instalador generado por el job de CI de empaquetado (US-2)
  **WHEN** se consulta su versión (metadata del instalador o del ejecutable
  instalado)
  **THEN** coincide exactamente con el número de versión calculado por
  Nerdbank.GitVersioning para el commit/tag que originó ese build

- **GIVEN** un `version.json` ausente o con un formato inválido
  **WHEN** se intenta compilar la solución
  **THEN** el build falla con un error claro indicando el problema de
  configuración, en vez de compilar silenciosamente con una versión
  incorrecta o por defecto

### Fuentes verificadas
[Nerdbank.GitVersioning en NuGet](https://www.nuget.org/packages/Nerdbank.GitVersioning) —
paquete `Nerdbank.GitVersioning`, versión actual 3.10.85.
[Nerdbank.GitVersioning — Getting Started (dotnet.github.io)](https://dotnet.github.io/Nerdbank.GitVersioning/docs/getting-started.html) —
instalación recomendada vía herramienta global `nbgv` (`dotnet tool install -g nbgv`
seguido de `nbgv install`), que añade la referencia al paquete y el
`version.json` al repositorio.
[version.json file (dotnet.github.io)](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html) —
el nombre del fichero debe ser `version.json` en minúsculas.

## US-2: Empaquetado del instalador (issue #25)

**As a** usuario del widget,
**I want** un instalador generado automáticamente en cada release,
**so that** pueda instalar ClaudeMeter sin pasar por Visual Studio ni
compilar nada.

### Acceptance Criteria

- **GIVEN** un tag de release empujado al repositorio (p. ej. `v1.0.0`)
  **WHEN** se dispara el job de CI de empaquetado
  **THEN** se genera un instalador de Windows para `ClaudeMeter.Desktop`,
  con el número de versión calculado en US-1 embebido, sin intervención
  manual

- **GIVEN** el instalador generado por el job de CI
  **WHEN** se ejecuta en una máquina Windows limpia (sin Visual Studio, sin
  SDK de .NET ni ninguna otra herramienta de desarrollo instalada)
  **THEN** la instalación se completa correctamente y la aplicación queda
  operativa (arranca y muestra el widget)

- **GIVEN** el job de CI de empaquetado completado con éxito para un tag de
  release
  **WHEN** el job termina
  **THEN** el instalador queda publicado como asset descargable en la
  página de esa release de GitHub

- **GIVEN** un push que no es un tag/release (p. ej. a una rama de feature)
  **WHEN** se ejecuta el pipeline de CI
  **THEN** el job de empaquetado del instalador NO se dispara — solo se
  ejecuta el flujo ya planeado de restore→build→tests→analyzers descrito en
  `CLAUDE.md`, evitando generar un instalador por cada commit

- **GIVEN** que el build en modo Release trata los warnings de los
  analizadores Roslyn como errores (según `CLAUDE.md`)
  **WHEN** el job de empaquetado falla por esa causa o por cualquier otra
  **THEN** no se publica ningún instalador incompleto o roto como asset de
  la release — el job falla de forma visible en vez de dejar un artefacto
  corrupto disponible para descarga

### Fuentes verificadas
[Velopack (sitio oficial)](https://velopack.io/) y
[Velopack en GitHub](https://github.com/velopack/velopack) —
framework de instalación y auto-actualización para apps de escritorio
multiplataforma (Windows/macOS/Linux), sin configuración previa necesaria;
la CLI `vpk` empaqueta el output del compilador en un instalador, delta
packages y un paquete portable auto-actualizable en un solo comando, y
puede publicar/descargar releases directamente contra GitHub Releases.

## US-3: Autoarranque y auto-actualización (issue #26)

**As a** usuario del widget,
**I want** que ClaudeMeter arranque solo con Windows y se actualice
automáticamente,
**so that** no tenga que acordarme de abrirlo ni de reinstalarlo cuando
salga una versión nueva.

### Acceptance Criteria

- **GIVEN** ClaudeMeter instalado vía el instalador de US-2
  **WHEN** el usuario activa la opción de "iniciar con Windows" (p. ej.
  desde el menú de bandeja ya existente de F3)
  **THEN** la aplicación queda registrada para arrancar automáticamente al
  iniciar sesión en Windows

- **GIVEN** la opción de autoarranque activada
  **WHEN** el usuario reinicia o inicia sesión en Windows
  **THEN** ClaudeMeter se abre automáticamente, sin que el usuario tenga
  que ejecutarlo manualmente

- **GIVEN** la opción de autoarranque activada previamente
  **WHEN** el usuario la desactiva desde el menú de bandeja
  **THEN** ClaudeMeter deja de arrancar automáticamente en el siguiente
  inicio de sesión

- **GIVEN** ClaudeMeter en ejecución y una versión más reciente publicada
  como release de GitHub (empaquetada según US-2)
  **WHEN** la aplicación comprueba si hay actualizaciones disponibles
  **THEN** detecta la nueva versión, la descarga y la aplica sin que el
  usuario tenga que descargar ni ejecutar nada manualmente

- **GIVEN** una comprobación de actualizaciones sin conexión a internet o
  con el feed de releases inaccesible
  **WHEN** se ejecuta esa comprobación
  **THEN** la aplicación sigue funcionando con la versión actual sin
  bloquearse ni mostrar un error intrusivo — mismo patrón de degradación
  silenciosa ante fallos de red ya adoptado en F2

- **GIVEN** una actualización descargada y aplicada
  **WHEN** la aplicación se reinicia tras aplicarla
  **THEN** el usuario conserva su configuración existente (`config.json` de
  F2) y el estado de la opción de autoarranque, sin tener que reconfigurar
  nada

### Fuentes verificadas
[Integrating Overview (docs.velopack.io)](https://docs.velopack.io/integrating/overview) —
`VelopackApp...Run()` debe invocarse una única vez, como primera línea de
`Main()`.
[UpdateManager.CheckForUpdatesAsync (docs.velopack.io)](https://docs.velopack.io/reference/cs/Velopack/UpdateManager/methods/CheckForUpdatesAsync) —
flujo `CheckForUpdatesAsync` → `DownloadUpdatesAsync` →
`ApplyUpdatesAndRestart`, con la opción de separar cada paso para no forzar
una comprobación en cada arranque.
[SetAutoApplyOnStartup (docs.velopack.io)](https://docs.velopack.io/reference/cs/Velopack/VelopackApp/methods/SetAutoApplyOnStartup) —
Velopack puede aplicar automáticamente actualizaciones ya descargadas al
arrancar (activado por defecto).

## Technical Notes

- **US-1:** instalar Nerdbank.GitVersioning con la herramienta `nbgv`
  (`nbgv install`) añade tanto el `version.json` como la referencia al
  paquete a los proyectos — se deja como sugerencia de Design, no como
  decisión tomada aquí.
- **US-2 / elección de tecnología de empaquetado:** decidido — **Velopack**
  (ver Dependencies). MSIX queda descartado para esta fase; Design debe
  integrar la CLI `vpk` de Velopack en el job de CI de empaquetado, no
  evaluar alternativas.
- **US-3 / autoarranque no es una función de Velopack:** se ha verificado
  que Velopack no incluye un mecanismo propio de "arrancar con el login de
  Windows" — solo gestiona instalación/actualización. El autoarranque es un
  mecanismo estándar de Windows (clave de registro
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` o acceso directo en
  la carpeta de inicio) independiente del empaquetador elegido; queda a
  criterio de Design cuál de los dos usar. Se asume que el toggle vive en
  el menú de bandeja ya construido en F3, por ser el único punto de control
  del widget fuera de la propia ventana.
- **US-3 / frecuencia de comprobación de actualizaciones:** ninguna de las
  issues originales especifica cada cuánto se comprueba. Se asume por
  defecto una comprobación al arrancar la aplicación más una comprobación
  periódica de baja frecuencia (p. ej. cada pocas horas), evitando tanto
  dejar al usuario sin actualizar durante sesiones largas como generar
  tráfico innecesario contra GitHub Releases; es una decisión de
  implementación delegada a Design, no un open question de producto.
- **CI/CD aún no existe en el repositorio:** `CLAUDE.md` describe el
  pipeline de CI/CD (incluido el job de empaquetado en tags/releases) como
  "planeado" — esta fase F6 incluye construir ese job de empaquetado como
  parte de la propia entrega, no como un prerrequisito ya satisfecho.

## Dependencies

- **Cadena de dependencias interna del milestone (secuencial):** US-1
  (#27) → US-2 (#25) → US-3 (#26). US-2 consume el número de versión que
  produce US-1 (necesita existir antes de poder embeberse en el
  instalador). US-3 se apoya en el mecanismo de empaquetado/actualización
  de Velopack ya integrado en US-2 (necesita el instalador/canal de
  releases funcionando antes de poder comprobar y aplicar actualizaciones).
  No se espera desarrollo en paralelo entre las tres.
- **Depends on (externo, ya satisfecho):** scaffolding inicial de la
  solución (F0, ya mergeado); menú de bandeja del icono de F3 (issue de F3
  ya cerrada) como punto de control sugerido para el toggle de autoarranque
  de US-3.
- **Related to:** pipeline de CI/CD de restore→build→tests→analyzers en
  cada push (descrito como "planeado" en `CLAUDE.md`) — el job de
  empaquetado de US-2 es un job adicional sobre ese mismo pipeline,
  disparado solo en tags/releases.
- **Decisión de producto confirmada — tecnología de empaquetado: Velopack.**
  Las issues originales dejaban abierto *"Velopack (o MSIX)"*; el usuario ha
  resuelto la ambigüedad eligiendo **Velopack**, descartando MSIX. Razones
  dadas: integración directa con GitHub Releases (la CLI `vpk` puede
  publicar/descargar releases contra GitHub Releases sin infraestructura
  adicional, encajando con el flujo de publicar el instalador como asset de
  release de GitHub ya descrito en `CLAUDE.md`); no requiere firma de código
  para funcionar (aunque, sin firmar, sigue disparando avisos de Windows
  SmartScreen/Defender — ver Risks). Esta decisión ya no es un open question:
  Design (`sdlc-design`) debe asumir Velopack como la tecnología de
  empaquetado y auto-actualización, no evaluarla como alternativa.

## Risks

- **Seguridad/confianza (US-2):** un instalador sin firmar puede disparar
  avisos de Windows SmartScreen / Microsoft Defender ("editor
  desconocido") durante la instalación limpia que pide el AC de US-2,
  degradando la experiencia de un usuario sin herramientas de desarrollo.
  Impact: Medium — no bloquea la instalación (el usuario puede continuar
  tras el aviso), pero puede hacerla parecer insegura; la decisión de
  invertir en un certificado de firma de código queda fuera del alcance de
  este documento y se traslada como consideración a Design.
- **Adopción/recursos (US-3):** el autoarranque hace que el poll de 60s y
  el proceso WebView2 embebido (ver riesgo ya documentado en F1) consuman
  recursos desde el inicio de sesión de Windows, incluso si el usuario no
  interactúa con el widget en esa sesión. Impact: Low — mitigado porque es
  una opción explícitamente opt-in (AC de US-3), no un comportamiento por
  defecto.
- **Continuidad del servicio (US-3):** una actualización aplicada a mitad
  de un ciclo de poll, o un fallo durante `ApplyUpdatesAndRestart`, podría
  dejar al usuario sin el widget corriendo hasta el siguiente inicio de
  sesión. Impact: Low — Velopack gestiona el reinicio de la aplicación tras
  aplicar la actualización como parte de su flujo estándar.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1, US-2 y US-3 anteriores se
      cumplen
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
