// Arrastre implementado íntegramente aquí (no vía Window.DragMove(), ver
// rationale en WindowDragService.cs). Usa Pointer Events +
// setPointerCapture en vez de Mouse Events normales: con mousemove/mouseup
// "a secas", en cuanto el puntero sale del área de 280x140px del widget
// (algo casi inmediato: la ventana no se ha movido todavía cuando el
// usuario ya está moviendo el ratón) WebView2 deja de recibir esos eventos
// -- el SO ya no se los entrega a esa ventana -- y el arrastre se corta al
// primer píxel. setPointerCapture engancha la captura de ratón a nivel de
// SO sobre el HWND real de WebView2, así que sigue recibiendo pointermove
// aunque el cursor salga de los límites visibles del widget.
window.claudeMeterDrag = {
    init: function (dotNetDragService) {
        const root = document.documentElement;

        root.addEventListener('pointerdown', function (e) {
            if (e.button === 0) {
                root.setPointerCapture(e.pointerId);
                dotNetDragService.invokeMethodAsync('BeginDrag');
            }
        });

        root.addEventListener('pointermove', function (e) {
            if (root.hasPointerCapture(e.pointerId)) {
                // devicePixelRatio: WPF espera el delta en píxeles de
                // dispositivo, no en píxeles CSS del contenido web.
                dotNetDragService.invokeMethodAsync(
                    'DragDelta',
                    e.movementX * window.devicePixelRatio,
                    e.movementY * window.devicePixelRatio);
            }
        });

        root.addEventListener('pointerup', function (e) {
            if (root.hasPointerCapture(e.pointerId)) {
                root.releasePointerCapture(e.pointerId);
                dotNetDragService.invokeMethodAsync('EndDrag');
            }
        });
    }
};
