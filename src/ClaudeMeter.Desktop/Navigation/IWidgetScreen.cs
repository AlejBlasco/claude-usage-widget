namespace ClaudeMeter.Desktop.Navigation;

/// <summary>
/// Contrato mínimo que debe implementar una página Razor para integrarse en
/// <see cref="ScreenNavigator"/> sin que éste conozca ningún detalle interno
/// de la página (US-1, issue #18). El propio tipo concreto (obtenido vía
/// <c>typeof(...)</c> en <see cref="ScreenNavigator"/>) ya es "lo mínimo" que
/// ScreenNavigator necesita para poder mostrarlo vía <c>DynamicComponent</c>
/// -- ScreenId es metadato adicional para logging/tests, no participa en la
/// lógica de ciclado en sí (ver <see cref="ScreenCycle"/>).
/// </summary>
public interface IWidgetScreen
{
    /// <summary>Identificador corto y estable de la pantalla (p. ej. "usage", "mascot"), nunca usado para lógica de negocio.</summary>
    string ScreenId { get; }
}
