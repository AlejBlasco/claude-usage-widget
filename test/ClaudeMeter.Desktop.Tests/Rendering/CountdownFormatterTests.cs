using ClaudeMeter.Desktop.Rendering;

namespace ClaudeMeter.Desktop.Tests.Rendering;

/// <summary>
/// Pruebas xUnit puras de <see cref="CountdownFormatter"/> (F3/Ciclo A,
/// US-2): función pura, sin dependencias externas -- se cubren todos los
/// umbrales de formato descritos en su XMLDoc ("&gt;=1 día -&gt; "{d}d {h}h";
/// &gt;=1 hora -&gt; "{h}h {m}m"; en caso contrario -&gt; "{m}m"") y las
/// fronteras exactas entre unidades (59/60 minutos, 1439/1440 minutos).
/// </summary>
public sealed class CountdownFormatterTests
{
    [Fact]
    public void Format_ConCero_DevuelveCeroM()
    {
        // AC explícita de US-2 sobre el estado de cero.
        Assert.Equal("0m", CountdownFormatter.Format(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Format_ConValorNegativo_DevuelveCeroMDeFormaDefensiva(int minutesRemaining)
    {
        // El comentario XMLDoc documenta que el llamador solo invoca tras
        // desenvolver un int? no nulo, pero la guarda <= 0 debe seguir
        // siendo defensiva ante cualquier valor negativo inesperado.
        Assert.Equal("0m", CountdownFormatter.Format(minutesRemaining));
    }

    [Theory]
    [InlineData(1, "1m")]
    [InlineData(30, "30m")]
    [InlineData(59, "59m")] // frontera: el último minuto que aún no forma una hora completa
    public void Format_ConMenosDeUnaHora_DevuelveSoloMinutos(int minutesRemaining, string expected)
    {
        Assert.Equal(expected, CountdownFormatter.Format(minutesRemaining));
    }

    [Theory]
    [InlineData(60, "1h 0m")] // frontera: exactamente 1 hora
    [InlineData(61, "1h 1m")]
    [InlineData(119, "1h 59m")]
    [InlineData(90, "1h 30m")]
    [InlineData(1439, "23h 59m")] // frontera: el último minuto que aún no forma un día completo
    public void Format_ConUnaHoraOMasPeroMenosDeUnDia_DevuelveHorasYMinutos(int minutesRemaining, string expected)
    {
        Assert.Equal(expected, CountdownFormatter.Format(minutesRemaining));
    }

    [Theory]
    [InlineData(1440, "1d 0h")] // frontera: exactamente 1 día
    [InlineData(1441, "1d 0h")] // 1 minuto después de la frontera -- sigue sin sumar una hora completa
    [InlineData(1500, "1d 1h")]
    [InlineData(2880, "2d 0h")] // frontera: exactamente 2 días
    [InlineData(4319, "2d 23h")] // 1 minuto antes de 3 días completos
    public void Format_ConUnDiaOMas_DevuelveDiasYHorasSinMinutos(int minutesRemaining, string expected)
    {
        // A partir de 1 día, los minutos sueltos se descartan del texto
        // (solo "{d}d {h}h"), tal como documenta el XMLDoc de Format.
        Assert.Equal(expected, CountdownFormatter.Format(minutesRemaining));
    }
}
