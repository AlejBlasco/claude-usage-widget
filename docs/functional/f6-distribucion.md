# F6 — Distribución — Documentación Funcional

## Qué hace esto

Hasta ahora, usar ClaudeMeter significaba compilarlo desde el código fuente.
Esta fase lo convierte en una aplicación instalable de verdad, con tres
novedades:

1. **Un instalador de un clic.** ClaudeMeter ya se puede descargar e
   instalar como cualquier otro programa de Windows, sin necesidad de
   tener herramientas de programación instaladas.
2. **Autoarranque opcional.** El widget puede abrirse solo cada vez que se
   inicia sesión en Windows, si el usuario lo decide así.
3. **Actualizaciones automáticas.** Cuando sale una versión nueva,
   ClaudeMeter la detecta, la descarga y la aplica solo, sin que el
   usuario tenga que ir a buscarla ni reinstalar nada.

Cada versión instalable lleva además un número de versión que se calcula
solo, a partir del propio historial del proyecto — nadie tiene que
acordarse de actualizarlo a mano.

## Por qué importa

Antes de esta fase, ClaudeMeter solo podía usarlo quien supiera compilar
proyectos .NET. Con el instalador, cualquiera puede empezar a usarlo en
un par de clics. El autoarranque evita el gesto repetitivo de abrirlo cada
vez que se encienda el ordenador, para quien lo quiera siempre disponible.
Y las actualizaciones automáticas significan que las mejoras futuras
llegan solas, sin que el usuario tenga que acordarse de revisar si hay una
versión nueva ni de reinstalar manualmente.

## Cómo funciona (perspectiva del usuario)

```mermaid
flowchart TD
    A[Descargar el instalador\ndesde la página de releases] --> B[Ejecutar el instalador]
    B --> C[ClaudeMeter queda instalado\ny arranca]
    C --> D[Pasar el ratón por el widget\ny abrir el menú de la bandeja]
    D --> E{Activar "Iniciar con Windows"?}
    E -->|Sí| F[ClaudeMeter se abrirá solo\ncada vez que se inicie sesión]
    E -->|No, prefiero abrirlo yo| G[Todo sigue igual que antes]
    F --> H[Mientras está abierto,\ncomprueba solo si hay una\nversión nueva]
    G --> H
    H -->|Hay actualización| I[La descarga y la aplica\nsin pedir nada al usuario]
    H -->|No hay actualización\no sin conexión| J[Sigue funcionando igual,\nsin avisos ni errores]
```

- **Instalación:** se descarga el instalador desde la página de releases
  del proyecto en GitHub y se ejecuta como cualquier instalador de
  Windows. Al terminar, ClaudeMeter queda listo para usarse, con el mismo
  comportamiento (widget, barras, mascota, bandeja) que en las fases
  anteriores.
- **Activar o desactivar el autoarranque:** desde el menú de la bandeja
  del sistema (el mismo menú con "Pausar"/"Recargar"/"Ignorar clics" de
  fases anteriores) hay una nueva opción, "Iniciar con Windows". Un clic
  la activa; otro clic la desactiva. El estado se recuerda aunque se
  cierre y reabra el widget, y aunque se instale una actualización nueva.
  Por defecto viene desactivada — es una opción que el usuario elige, no
  un comportamiento impuesto.
- **Actualizaciones automáticas:** mientras ClaudeMeter está abierto,
  comprueba de vez en cuando (al arrancar, y después cada pocas horas) si
  hay una versión más reciente publicada. Si la hay, la descarga y la
  aplica reiniciando el widget automáticamente, sin pedir confirmación ni
  mostrar diálogos. Si no hay conexión a internet o la comprobación falla
  por cualquier motivo, ClaudeMeter sigue funcionando con normalidad con
  la versión que ya tiene instalada — nunca se bloquea ni muestra un
  error por esto.
- **Todo lo demás sigue igual:** las barras de consumo, la mascota, el
  tema claro/oscuro, el arrastre del widget, el modo "ignorar clics" y el
  resto del menú de bandeja funcionan exactamente igual que en las fases
  anteriores.

## Qué queda pendiente de validar

Algunas comprobaciones solo se pueden hacer con un ciclo real de
publicación de versiones en GitHub, o con un ordenador Windows "limpio"
(sin herramientas de programación instaladas), y están fuera del alcance
de lo que un agente automatizado puede verificar por sí mismo:

- Instalar ClaudeMeter en un ordenador Windows recién instalado, sin
  Visual Studio ni el kit de desarrollo de .NET.
- Confirmar que, tras publicar una versión nueva, un ClaudeMeter ya
  instalado la detecta, la descarga y la aplica solo, de principio a fin.
- Confirmar el autoarranque tras un reinicio real de Windows (ya
  comprobado el toggle del menú y que queda guardado en el registro de
  Windows; falta el reinicio real).

Lo que **ya se ha comprobado** en esta fase: el instalador se genera
correctamente, se instaló en un ordenador real y la aplicación arrancó y
funcionó con normalidad; la opción "Iniciar con Windows" del menú de
bandeja se activa/desactiva correctamente y queda registrada en Windows.

## Preguntas frecuentes

**¿Tengo que desinstalar la versión anterior antes de instalar una
nueva?**
No. Las actualizaciones se aplican automáticamente sobre la instalación
existente; no hay que desinstalar ni volver a descargar nada a mano.

**¿Qué pasa si activo "Iniciar con Windows" y luego cambio de opinión?**
Se desactiva con otro clic en la misma opción del menú de bandeja.
ClaudeMeter deja de abrirse solo a partir de la siguiente vez que se
inicie sesión en Windows.

**¿El autoarranque consume recursos si no uso el widget?**
Solo si lo activas explícitamente — es una opción desactivada por
defecto. Si la activas, el widget consume los mismos recursos ligeros que
ya consumía en fases anteriores (actualización cada 60 segundos), ahora
simplemente desde el inicio de sesión en vez de tener que abrirlo a mano.

**¿Qué pasa si no tengo conexión a internet cuando ClaudeMeter comprueba
si hay actualizaciones?**
Nada visible. Sigue funcionando con la versión que ya tiene instalada, sin
avisos ni interrupciones, y lo volverá a intentar más adelante.

**¿Pierdo mi configuración (tema, posición del widget, etc.) al
actualizar?**
No. La configuración guardada y el estado del autoarranque se conservan
tal cual tras aplicar una actualización.
