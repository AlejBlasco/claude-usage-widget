using Bunit;
using ClaudeMeter.Desktop.Pages;

namespace ClaudeMeter.Desktop.Tests.Pages;

/// <summary>
/// Pruebas bUnit de <see cref="ReauthNotice"/> (US-1, F2): componente sin
/// parámetros que renderiza siempre el mismo aviso de reautenticación,
/// visual y textualmente distinto de "No disponible" (<see cref="UsageBar"/>)
/// y sin el sufijo "(desactualizado)". Usa la API v2 de bUnit
/// (<see cref="BunitContext"/> + <c>Render&lt;T&gt;</c>), igual que el resto
/// de la suite.
/// </summary>
public sealed class ReauthNoticeTests : BunitContext
{
    [Fact]
    public void ReauthNotice_AlRenderizar_MuestraElMensajeDeReautenticacion()
    {
        var cut = Render<ReauthNotice>();

        var notice = cut.Find("div.usage-reauth");
        Assert.Contains("Vuelve a iniciar sesión", notice.TextContent);
    }

    [Fact]
    public void ReauthNotice_AlRenderizar_NuncaMuestraElSufijoDesactualizado()
    {
        // AC 3 de US-1: un 401/403 no es un dato desactualizado, es una
        // credencial inválida -- el mensaje nunca debe llevar ese sufijo,
        // a diferencia de UsageBar con Stale=true.
        var cut = Render<ReauthNotice>();

        Assert.DoesNotContain("desactualizado", cut.Markup);
    }

    [Fact]
    public void ReauthNotice_AlRenderizar_NoContieneNingunaUsageBar()
    {
        // El componente sustituye por completo las barras -- no coexiste
        // con ellas (ver UsagePage).
        var cut = Render<ReauthNotice>();

        Assert.Empty(cut.FindAll("div.usage-bar"));
    }
}
