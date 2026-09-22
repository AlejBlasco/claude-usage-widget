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
