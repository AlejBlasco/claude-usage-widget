using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Domain.Tests.Usage;

/// <summary>
/// Pruebas puras (sin E/S) de las cuatro fábricas estáticas de
/// <see cref="UsageSnapshot"/>: verifican que cada una produce el
/// <c>Status</c>/<c>Session</c>/<c>Weekly</c>/<c>IsSuccess</c> esperado, y
/// que el contrato "sin datos" nunca requiere lanzar ni capturar
/// excepciones.
/// </summary>
public sealed class UsageSnapshotTests
{
    [Fact]
    public void Success_ConAmbasVentanas_DevuelveStatusSuccessConSessionYWeeklyExactos()
    {
        var session = new RawRateLimitHeaders("allowed", "10%", "90%", "2026-09-16T12:00:00Z");
        var weekly = new RawRateLimitHeaders("allowed", "5%", "95%", "2026-09-20T00:00:00Z");

        var result = UsageSnapshot.Success(session, weekly);

        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.Same(session, result.Session);
        Assert.Same(weekly, result.Weekly);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TokenUnavailable_DevuelveStatusTokenUnavailableSinSessionNiWeekly()
    {
        var result = UsageSnapshot.TokenUnavailable();

        Assert.Equal(UsageSnapshotStatus.TokenUnavailable, result.Status);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Unauthorized_DevuelveStatusUnauthorizedSinSessionNiWeekly()
    {
        var result = UsageSnapshot.Unauthorized();

        Assert.Equal(UsageSnapshotStatus.Unauthorized, result.Status);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RequestFailed_DevuelveStatusRequestFailedSinSessionNiWeekly()
    {
        var result = UsageSnapshot.RequestFailed();

        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MalformedResponse_DevuelveStatusMalformedResponseSinSessionNiWeekly()
    {
        // F2/US-2: categoría 3 del modelo de reintento -- respuesta 2xx pero
        // sin las cabeceras anthropic-ratelimit-unified-* mínimas esperadas.
        // Distinto de RequestFailed (categoría 1, fallo transitorio):
        // RetryingUsageDataSource nunca reintenta este estado.
        var result = UsageSnapshot.MalformedResponse();

        Assert.Equal(UsageSnapshotStatus.MalformedResponse, result.Status);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MalformedResponse_EsUnEstadoDistintoDeRequestFailed()
    {
        // Ambos son "fallo" (IsSuccess == false), pero deben seguir siendo
        // estados explícitamente distintos: es justo la distinción que
        // introduce F2 para que el decorator de reintento pueda tratarlos
        // de forma diferente.
        Assert.NotEqual(UsageSnapshot.RequestFailed().Status, UsageSnapshot.MalformedResponse().Status);
    }

    [Theory]
    [InlineData(UsageSnapshotStatus.TokenUnavailable)]
    [InlineData(UsageSnapshotStatus.Unauthorized)]
    [InlineData(UsageSnapshotStatus.RequestFailed)]
    [InlineData(UsageSnapshotStatus.MalformedResponse)]
    public void IsSuccess_ParaCualquierEstadoDeFallo_EsSiempreFalse(UsageSnapshotStatus failureStatus)
    {
        var result = failureStatus switch
        {
            UsageSnapshotStatus.TokenUnavailable => UsageSnapshot.TokenUnavailable(),
            UsageSnapshotStatus.Unauthorized => UsageSnapshot.Unauthorized(),
            UsageSnapshotStatus.RequestFailed => UsageSnapshot.RequestFailed(),
            UsageSnapshotStatus.MalformedResponse => UsageSnapshot.MalformedResponse(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureStatus)),
        };

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RawRateLimitHeaders_ConTodosLosCamposNulos_SeConstruyeSinLanzar()
    {
        // Caso límite documentado en el diseño: cualquier sufijo puede ser
        // null si la cabecera concreta no vino en la respuesta.
        var headers = new RawRateLimitHeaders(Status: "allowed", Utilization: null, Remaining: null, Reset: null);

        Assert.Equal("allowed", headers.Status);
        Assert.Null(headers.Utilization);
        Assert.Null(headers.Remaining);
        Assert.Null(headers.Reset);
    }
}
