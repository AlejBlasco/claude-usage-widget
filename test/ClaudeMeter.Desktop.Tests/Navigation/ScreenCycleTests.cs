using ClaudeMeter.Desktop.Navigation;

namespace ClaudeMeter.Desktop.Tests.Navigation;

/// <summary>
/// Pruebas xUnit puras (sin Blazor/WPF) de <see cref="ScreenCycle"/>: cubre
/// el AC de US-1 (issue #18) sobre el ciclado por índice, incluyendo el
/// caso límite explícito de una única pantalla registrada. Usa tipos
/// cualesquiera (<c>typeof(object)</c>/<c>typeof(string)</c>/<c>typeof(int)</c>)
/// como sustitutos triviales -- ningún componente Razor real hace falta
/// para esta lógica, tal como especifica el documento de diseño (paso 5 del
/// Implementation Plan).
/// </summary>
public sealed class ScreenCycleTests
{
    [Fact]
    public void Constructor_ConListaVacia_LanzaArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ScreenCycle(Array.Empty<Type>()));

        Assert.Equal("screens", exception.ParamName);
    }

    [Fact]
    public void Current_AntesDeCualquierNext_EsElPrimerElementoDeLaLista()
    {
        var cycle = new ScreenCycle(new[] { typeof(string), typeof(int) });

        Assert.Equal(typeof(string), cycle.Current);
    }

    [Fact]
    public void ScreenCount_ReflejaElNumeroDePantallasRegistradas()
    {
        var cycle = new ScreenCycle(new[] { typeof(object), typeof(string), typeof(int) });

        Assert.Equal(3, cycle.ScreenCount);
    }

    [Fact]
    public void Next_ConDosPantallas_AvanzaYVuelveALaPrimeraTrasLaUltima()
    {
        var cycle = new ScreenCycle(new[] { typeof(string), typeof(int) });

        var second = cycle.Next();
        Assert.Equal(typeof(int), second);
        Assert.Equal(typeof(int), cycle.Current);

        var backToFirst = cycle.Next();
        Assert.Equal(typeof(string), backToFirst);
        Assert.Equal(typeof(string), cycle.Current);
    }

    [Fact]
    public void Next_ConTresPantallas_AvanzaEnOrdenYVuelveALaPrimeraTrasLaUltima()
    {
        var cycle = new ScreenCycle(new[] { typeof(object), typeof(string), typeof(int) });

        Assert.Equal(typeof(object), cycle.Current);
        Assert.Equal(typeof(string), cycle.Next());
        Assert.Equal(typeof(int), cycle.Next());
        Assert.Equal(typeof(object), cycle.Next()); // vuelve a la primera tras la última
    }

    [Fact]
    public void Next_ConUnaUnicaPantallaRegistrada_NoLanzaYSiempreDevuelveLaMisma()
    {
        // AC de caso límite de US-1: el ciclo trivial "vuelve a la misma
        // pantalla" es un comportamiento válido, no un caso a rechazar.
        var cycle = new ScreenCycle(new[] { typeof(string) });

        var exception = Record.Exception(() =>
        {
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(typeof(string), cycle.Next());
            }
        });

        Assert.Null(exception);
        Assert.Equal(typeof(string), cycle.Current);
    }
}
