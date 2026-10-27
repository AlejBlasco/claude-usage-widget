# Deployment Notes: F6 — Distribución

## Target Environment

Aplicación de escritorio **Windows** (WPF + `BlazorWebView`), distribuida
como instalador de usuario final vía **Velopack + GitHub Releases**. No es
un servidor propio ni una nube con infraestructura gestionada: el "entorno
de producción" de esta fase es la **máquina Windows de cada usuario final**
que instala ClaudeMeter. El build/empaquetado se ejecuta en runners
efímeros de **GitHub Actions** (`windows-latest`); no hay host remoto propio
que este agente pueda o deba tocar.

Esta fase ya fue implementada por `sdlc-development` (US-1 versionado, US-2
empaquetado, US-3 autoarranque/auto-actualización) y verificada por
`sdlc-testing` (206/206 tests en verde). Este documento es una **revisión**
de los artefactos de despliegue ya construidos (`version.json`, `ci.yml`,
`release.yml`) y el runbook operativo para publicar/revertir una release
real — no introduce infraestructura nueva.

## Files Changed

Ninguno. Esta fase revisa lo ya construido en `sdlc-development`, sin
encontrar ninguna corrección necesaria (ver verificación abajo). Ficheros
revisados, ya presentes en el árbol de trabajo:

- `version.json` — semilla `"1.0"`, `publicReleaseRefSpec` con
  `^refs/heads/master$` y tags `v\d+\.\d+`. Correcto para el flujo descrito
  más abajo.
- `.github/workflows/ci.yml` — `push` (todas las ramas) + `pull_request`
  contra `master` → `checkout --fetch-depth 0` → `setup-dotnet` 8.0.x →
  `restore` → `build -c Release` (warnings-as-errors ya vía
  `Directory.Build.props`) → `test`. `permissions: contents: read`
  (correcto, no necesita escribir nada).
- `.github/workflows/release.yml` — trigger `push: tags: v*` →
  `permissions: contents: write` → `checkout --fetch-depth 0` →
  `setup-dotnet` 8.0.x → instala `nbgv`/`vpk` como herramientas globales →
  `restore` → `build -c Release` → `test` → `dotnet publish ... -r win-x64
  --self-contained true` → `nbgv get-version -v NuGetPackageVersion` →
  `vpk pack --packId ClaudeMeter --packVersion <versión> --framework
  webview2 --runtime win-x64` → `vpk upload github --repoUrl
  https://github.com/AlejBlasco/claude-usage-widget --publish --tag
  <tag>`.

Verificado en esta revisión (lectura del YAML + `git remote -v` local, sin
ninguna llamada de red): el `--repoUrl` de `release.yml` coincide
exactamente con el remoto real del repositorio
(`https://github.com/AlejBlasco/claude-usage-widget.git`); cualquier fallo
en restore/build/test/publish/`vpk pack` detiene el job antes de llegar a
`vpk upload github --publish` (comportamiento por defecto de GitHub
Actions — ningún paso ignora errores), por lo que nunca se publica un
instalador roto; el trigger `tags: v*` no se dispara en un push normal, que
solo activa `ci.yml`. No se ha encontrado ninguna corrección pendiente en
ninguno de los dos workflows.

## Resumen del flujo de release (`release.yml`)

Se dispara **únicamente** con `git push` de un tag que empieza por `v`
(`on.push.tags: v*`) — nunca con un push normal ni con un PR. Pasos, en
orden:

1. **Checkout completo** (`fetch-depth: 0`): imprescindible para que
   Nerdbank.GitVersioning pueda calcular la altura de commits; un checkout
   superficial rompería el cálculo de versión en silencio.
2. **Instala `nbgv` y `vpk`** como herramientas globales de .NET en el
   runner (no vienen preinstaladas en `windows-latest`).
3. **`restore` → `build -c Release` → `test`**: misma secuencia que
   `ci.yml`, repetida aquí porque el job de release corre en un workflow
   distinto (sin compartir estado con `ci.yml`). Si falla cualquiera de los
   tres, el job se detiene y no se publica nada.
4. **`dotnet publish` self-contained `win-x64`** de `ClaudeMeter.Desktop` a
   `publish/`.
5. **`nbgv get-version -v NuGetPackageVersion`**: calcula la versión
   pública exacta para el tag empujado (p. ej. `1.0.0`) y la guarda en
   `GITHUB_OUTPUT` para el paso siguiente.
6. **`vpk pack`**: empaqueta `publish/` en un instalador Velopack
   (`ClaudeMeter-win-Setup.exe` en `Releases/`), con el bootstrapper de
   WebView2 Evergreen embebido (`--framework webview2`) y fijando la
   arquitectura (`--runtime win-x64`, necesario porque `vpk pack` asume
   x86 si no se especifica).
7. **`vpk upload github --publish --tag <tag>`**: sube el instalador (y el
   feed de actualización que `AppUpdateService`/`GithubSource` consulta
   después) como asset de la GitHub Release correspondiente a ese tag, y la
   publica (no la deja en borrador). Usa el `GITHUB_TOKEN` por defecto de
   la Action, con `permissions: contents: write` declarado explícitamente
   en el workflow.

## Checklist pre-release

Antes de empujar un tag real, confirmar:

- [ ] `master` actualizado localmente (`git pull origin master`) y el
      commit que se va a etiquetar es el correcto (`git log -1 --oneline`).
- [ ] `dotnet build ClaudeMeter.sln -c Release` — 0 advertencias / 0
      errores (warnings-as-errors ya configurado).
- [ ] `dotnet test ClaudeMeter.sln -c Release` — todos los tests en verde
      (última cifra conocida: 206/206, ver
      `docs/sdlc/testing/f6-distribucion.md`).
- [ ] CI (`ci.yml`) en verde para ese mismo commit en GitHub Actions (no
      basta con verificar en local; confirmar en la pestaña Actions del
      repositorio o con `gh run list --branch master --limit 1`).
- [ ] `nbgv get-version` ejecutado **desde un checkout de `master`** en el
      commit a etiquetar, confirmando que `NuGetPackageVersion` es la
      versión pública esperada (sin sufijo de commit/dirty) y que coincide
      con el tag que se va a empujar (p. ej. versión `1.0.0` → tag
      `v1.0.0`).
- [ ] `version.json` sigue teniendo `v\d+\.\d+` en `publicReleaseRefSpec` —
      si el tag no encaja con ese patrón, `nbgv` no lo tratará como release
      pública y la versión embebida en el instalador quedará con sufijo
      "no oficial".
- [ ] `git status` limpio (sin cambios sin commitear) antes de etiquetar.
- [ ] Recordar que el instalador **no está firmado digitalmente** (riesgo
      ya documentado en requisitos/diseño): el usuario final verá un aviso
      de Windows SmartScreen/Defender ("editor desconocido") la primera
      vez. No es una regresión de esta release; es un riesgo aceptado y
      conocido.

## Procedimiento paso a paso para publicar una release real

**Nota:** estos comandos son para que el usuario los ejecute — este agente
no los ejecuta ni se conecta a GitHub.

```bash
# 1. Asegurarse de estar en master y actualizado
git checkout master
git pull origin master
git log -1 --oneline

# 2. Verificación local (checklist pre-release)
dotnet restore ClaudeMeter.sln
dotnet build ClaudeMeter.sln -c Release
dotnet test ClaudeMeter.sln -c Release
nbgv get-version

# 3. Crear el tag anotado con la versión pública confirmada en el paso 2
#    (sustituir v1.0.0 por la versión real)
git tag -a v1.0.0 -m "ClaudeMeter 1.0.0"

# 4. Empujar el tag -- esto es lo que dispara release.yml
git push origin v1.0.0
```

Monitorizar la ejecución y el resultado (lectura/consulta, no una acción de
despliegue en sí):

```bash
gh run list --workflow=release.yml --limit 1
gh run watch <run-id>              # ver salida en vivo del job
gh release view v1.0.0             # confirmar que el asset quedó adjunto y la release está publicada (no en borrador)
```

Confirmar manualmente en la página de la release de GitHub que el asset
`ClaudeMeter-win-Setup.exe` está presente y descargable antes de anunciar
la release a usuarios.

## Manual Setup Required (one-time)

- **Ninguna secret/PAT nueva.** `vpk upload github --publish` usa el
  `GITHUB_TOKEN` por defecto que la Action inyecta en cada ejecución;
  `release.yml` ya declara `permissions: contents: write` a nivel de
  workflow, que es lo que le da a ese token permiso para crear/publicar la
  release y adjuntar el asset. No hace falta crear un Personal Access Token
  ni un secret adicional en el repositorio.
- **Verificar una vez (no ejecutado por este agente, requiere acceso a la
  configuración del repositorio en github.com):** en *Settings → Actions →
  General → Workflow permissions*, confirmar que la organización/cuenta no
  tiene forzado un tope de solo-lectura que impida que el `permissions:`
  explícito de `release.yml` eleve el token a `contents: write` en tiempo
  de ejecución. En un repositorio personal normal esto no suele bloquear
  nada (el `permissions:` del propio workflow ya tiene prioridad sobre el
  valor por defecto del repositorio), pero es la única pieza de
  configuración de GitHub que, si estuviera mal, haría fallar
  silenciosamente el paso `vpk upload github --publish` con un 403 la
  primera vez que se empuje un tag real — y ningún agente de este pipeline
  puede comprobarlo sin acceso de red a GitHub.
- **Nada que configurar en Velopack/`vpk`** más allá de lo ya presente en
  `release.yml` (`--repoUrl` ya coincide con el remoto real del
  repositorio, confirmado en esta revisión).

## Deployment Flow

1. El desarrollador fusiona los cambios a `master` (vía PR, fuera del
   alcance de este agente) y pasa el checklist pre-release de arriba.
2. Empuja un tag `vX.Y.Z` (procedimiento de arriba) → dispara
   `release.yml` en `windows-latest`.
3. El job calcula la versión con `nbgv`, publica el build self-contained,
   empaqueta con `vpk pack` y sube/publica el instalador como asset de la
   GitHub Release del tag con `vpk upload github --publish`.
4. Los usuarios con una versión anterior instalada reciben la actualización
   sin intervención manual: `AppUpdateService` (ya implementado en US-3)
   comprueba actualizaciones al arrancar la app y cada 4 horas
   (`System.Timers.Timer`) vía `Velopack.UpdateManager` +
   `Velopack.Sources.GithubSource` apuntando al mismo repositorio;
   `ApplyUpdatesAndRestart(toApply: null)` aplica la última versión ya
   descargada y reinicia el proceso, conservando `config.json` (incluido el
   estado de "Iniciar con Windows").
5. Un usuario nuevo descarga el instalador directamente de la página de la
   release de GitHub y lo ejecuta — no requiere SDK ni Visual Studio (self-
   contained), y el bootstrapper de WebView2 embebido (`--framework
   webview2`) lo instala si la máquina no lo trae ya.

## Rollback

**Cambio con impacto en entorno real/producción — leer con atención antes
de actuar.** Velopack/GitHub Releases no tienen un mecanismo de "downgrade
automático" para los usuarios que ya se hayan actualizado: la única vía
hacia delante es publicar una nueva versión corregida.

Si una release sale rota (el instalador no instala, la app no arranca tras
actualizar, etc.):

1. **Contener la difusión inmediatamente:**
   - Despublicar o borrar la GitHub Release rota
     (`gh release delete vX.Y.Z` o editarla a borrador desde la web),
     para que ningún usuario nuevo la descargue y para que
     `GithubSource`/`AppUpdateService` deje de ofrecerla como "última
     versión disponible" en la próxima comprobación de los clientes que
     aún no se hayan actualizado.
   - Si el tag en sí está mal (versión incorrecta, etc.), también se puede
     borrar el tag remoto: `git push origin :refs/tags/vX.Y.Z` (y
     `git tag -d vX.Y.Z` en local) — esto **no** retira por sí solo la
     GitHub Release ya creada por `vpk upload github`; hay que borrarla
     aparte como en el punto anterior.
2. **Usuarios que ya se actualizaron a la versión rota:** no hay forma de
   "empujarles" un downgrade — seguirán en la versión rota hasta que se
   publique una versión nueva y superior que `AppUpdateService` detecte en
   su siguiente comprobación (al arrancar la app, o dentro de la ventana de
   4 horas del timer periódico). Comunicar esto como limitación real a
   cualquier usuario afectado si la rotura es grave (p. ej. la app no
   arranca: en ese caso ni siquiera podrá autoactualizarse sola, y
   necesitará reinstalar manualmente desde una release corregida).
3. **Publicar el fix:** corregir el problema, subir la versión
   (`version.json`/altura de commits ya lo hace solo), y repetir el
   procedimiento de arriba con un tag nuevo (p. ej. `v1.0.1`). Esta es la
   única vía de "rollback" real disponible con la arquitectura actual.
4. **Usuarios que no llegaron a actualizarse** (siguen en la versión
   anterior, buena) no se ven afectados, siempre que no se haya tocado ni
   borrado el asset de esa release anterior.

## Follow-ups / Known Limitations

- **Sin firma de código del instalador** — riesgo ya documentado en
  requisitos/diseño (impacto: aviso de SmartScreen/Defender en la primera
  instalación, no bloqueante). Añadir un certificado de firma y un paso de
  firma antes de `vpk pack` en `release.yml` queda como mejora futura, sin
  bloquear esta fase.
- **Sin mecanismo de downgrade/rollback real para usuarios ya
  actualizados** (ver sección Rollback) — limitación arquitectónica de
  Velopack/GitHub Releases tal como está integrado hoy, no un defecto de
  esta implementación.
- `dotnet publish` en `release.yml` no usa `--no-restore`/`--no-build`
  tras el `build`/`test` previos del mismo job, por lo que repite
  restore/build internamente antes de publicar — no es incorrecto, solo
  ligeramente redundante en tiempo de CI; no se ha tocado porque no afecta
  a la corrección del resultado y modificarlo no estaba dentro del alcance
  pedido (revisar, no rediseñar).

## Definition of Done — estado final

Copiado literalmente de `docs/sdlc/requirements/f6-distribucion.md` +
el ítem añadido por `docs/sdlc/design/f6-distribucion.md` (ambos ya
idénticos salvo ese quinto punto):

- [ ] PENDIENTE — DEL USUARIO. Todas las Acceptance Criteria de US-1, US-2
      y US-3 anteriores se cumplen. Estado real tras `sdlc-testing`: 3 ACs
      cubiertas por completo por tests automáticos, 3 parcialmente
      (toggle/persistencia sin la escritura real en el registro ni la
      aplicación real de una actualización), y las 10 restantes son de
      build/CI/empaquetado o exigen un entorno Windows real + un ciclo de
      release real — ver tabla de trazabilidad en
      `docs/sdlc/testing/f6-distribucion.md`. No se marca cumplida sin esa
      evidencia real.
- [ ] PENDIENTE — DEL USUARIO. **Validación manual (US-2):** instalación
      limpia del instalador generado por CI en una máquina Windows real
      sin Visual Studio ni SDK de .NET, confirmando que la aplicación
      arranca y funciona. Ningún agente de este pipeline dispone de una
      máquina Windows limpia real para ejecutarla. **Responsable: el
      usuario (Alex), en una VM o máquina física sin herramientas de
      desarrollo instaladas, después de tener al menos una release real
      publicada (punto siguiente).**
- [ ] PENDIENTE — DEL USUARIO. **Validación manual (US-2):** confirmar que
      el instalador queda publicado como asset en una release real de
      GitHub tras empujar un tag real. Requiere credenciales/permiso de
      publicación de releases que ningún agente de este pipeline tiene.
      **Responsable: el usuario (Alex), siguiendo el "Procedimiento paso a
      paso" de este documento; puede hacerse en cuanto el checklist
      pre-release esté en verde.**
- [ ] PENDIENTE — DEL USUARIO. **Validación manual (US-3):** confirmar el
      autoarranque real tras cerrar sesión/reiniciar Windows, y la
      aplicación real de una actualización entre dos versiones publicadas
      consecutivas. Requiere un ciclo real de publicación de al menos dos
      releases y una máquina Windows real, fuera del alcance de cualquier
      agente automatizado de este pipeline. **Responsable: el usuario
      (Alex); requiere haber completado ya las dos validaciones manuales
      anteriores y esperar a publicar una segunda release (p. ej. `v1.0.1`)
      tras la primera (`v1.0.0`) para poder observar la auto-actualización
      entre ambas.**
- [ ] PENDIENTE — DEL USUARIO. **Validación manual añadida por el
      documento de diseño (US-2):** confirmar en una máquina Windows real
      sin el WebView2 Runtime preinstalado (p. ej. Windows 10 limpio, o
      Windows 11 con el componente deshabilitado) que el bootstrapper
      embebido lo instala correctamente antes de que el widget intente
      arrancar `BlazorWebView`. Mismo motivo que las anteriores: requiere
      una máquina Windows real fuera del alcance de este pipeline.
      **Responsable: el usuario (Alex); puede combinarse con la validación
      de instalación limpia de arriba, en la misma máquina/VM.**

Esta es la última fase del pipeline SDLC para F6: los cinco puntos de la
Definition of Done siguen abiertos de forma genuina y real, no por
formalismo — ninguno de ellos es ejecutable por un agente sin acceso a una
máquina Windows real ni a credenciales de publicación de GitHub. No se
marca ninguno como cumplido sin esa evidencia.

---

**Nada se ha confirmado (`commit`), enviado (`push`) ni desplegado** por
este agente: no se ha creado ni empujado ningún tag, no se ha ejecutado
`vpk upload github`, y no se ha llamado a ninguna API de GitHub. Todo lo
anterior (procedimiento de release, comandos de tag, verificación de
permisos, rollback) está documentado para que el usuario lo ejecute
cuando decida publicar la primera release real; ninguno de esos pasos se
ha disparado desde esta sesión. El único cambio aplicado por este agente es
la creación de este propio documento (`docs/sdlc/deployment/f6-distribucion.md`),
que queda sin confirmar en el árbol de trabajo junto con el resto de
cambios de F6 ya presentes y sin commitear.
