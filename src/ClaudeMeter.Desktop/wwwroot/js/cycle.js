// Botón de cambio de pantalla (US-1, issue #18). Análogo a close.js:
// stopPropagation() en el pointerdown NATIVO del propio botón evita que el
// mismo gesto burbujee hasta document.documentElement y dispare también
// WindowDragService.BeginDrag() (drag.js escucha ahí).
//
// El modificador de Blazor @onpointerdown:stopPropagation NO sirve para
// esto: su listener delegado vive en document, que en el burbujeo del
// evento nativo se alcanza DESPUÉS que document.documentElement -- drag.js
// ya habría capturado el puntero (root.setPointerCapture) y arrancado el
// arrastre antes de que Blazor tuviera ocasión de llamar a
// event.stopPropagation(). Registrando el listener aquí, directamente
// sobre el botón, stopPropagation() se ejecuta en cuanto el evento sale del
// propio botón -- antes de llegar a documentElement -- igual que hace
// close.js para el botón de cierre. No hace falta ningún JSInvokable: el
// @onclick de Blazor sobre el botón sigue disparando CycleScreen()
// normalmente una vez que drag.js deja de robarle el gesto.
window.claudeMeterCycle = {
    init: function () {
        const button = document.querySelector('.claudemeter-cycle');
        if (!button) {
            return;
        }

        button.addEventListener('pointerdown', function (e) {
            e.stopPropagation();
        });
    }
};
