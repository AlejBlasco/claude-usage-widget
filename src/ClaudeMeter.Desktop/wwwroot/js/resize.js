// Ajusta la altura real de la ventana WPF al contenido, para que el "Tamaño
// de texto" de Accesibilidad de Windows (TextScaleFactor, que WebView2
// aplica como zoom incluso sobre font-size en px fijos) nunca recorte
// contenido con overflow:hidden -- ver rationale completo en
// WindowResizeService.cs. Observa .claudemeter-root, que en
// wwwroot/css/app.css deliberadamente NO tiene una altura fija (a
// diferencia de antes de este fix): si estuviera clavado al 100% del
// viewport, su caja jamás podría crecer más allá del tamaño de ventana
// actual y ResizeObserver nunca dispararía nada -- un problema circular.
window.claudeMeterResize = {
    init: function (dotNetResizeService) {
        const target = document.querySelector('.claudemeter-root');
        if (!target) {
            return;
        }

        new ResizeObserver(function (entries) {
            const height = entries[0].contentRect.height;
            dotNetResizeService.invokeMethodAsync('SetContentHeight', height * window.devicePixelRatio);
        }).observe(target);
    }
};
