using System.Net;
using Microsoft.AspNetCore.Http;
using Optica.Api.Seguridad;

namespace Optica.Api.Tests;

public class GuardaLoopbackTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5080")]
    [InlineData("http://localhost:5080")]
    [InlineData("http://LOCALHOST:5080")]
    [InlineData("http://[::1]:5080")]
    [InlineData("http://127.0.0.2:5080")]
    [InlineData("http://[::ffff:127.0.0.1]:5080")]
    public void GuardaLoopback_DireccionLoopback_Acepta(string direccion)
    {
        Assert.Empty(GuardaLoopback.DireccionesAjenas([direccion]));
    }

    [Theory]
    [InlineData("http://[::]:5080")]
    [InlineData("http://0.0.0.0:5080")]
    [InlineData("http://*:5080")]
    [InlineData("http://+:5080")]
    [InlineData("http://localhost.:5080")]
    [InlineData("http://localhost.evil.com:5080")]
    [InlineData("http://[fe80::1]:5080")]
    [InlineData("fe80::1")]
    [InlineData("http://servidor-optica:5080")]
    [InlineData("http://192.168.1.10:5080")]
    [InlineData("texto basura")]
    [InlineData("")]
    public void GuardaLoopback_DireccionNoLoopback_Rechaza(string direccion)
    {
        Assert.Equal([direccion], GuardaLoopback.DireccionesAjenas([direccion]));
    }

    [Fact]
    public void GuardaLoopback_SinDireccionesFueraDeTestServer_FallaElArranque()
    {
        // Kestrel siempre informa sus direcciones: si no hay ninguna, no se puede comprobar que sea loopback.
        Assert.NotNull(GuardaLoopback.Problema(null, esServidorDePruebas: false));
        Assert.NotNull(GuardaLoopback.Problema([], esServidorDePruebas: false));
        Assert.Contains("0.0.0.0", GuardaLoopback.Problema(["http://127.0.0.1:5080", "http://0.0.0.0:5080"], esServidorDePruebas: false));

        // TestServer no escucha en ninguna interfaz: no aplica.
        Assert.Null(GuardaLoopback.Problema(null, esServidorDePruebas: true));
        Assert.Null(GuardaLoopback.Problema(["http://127.0.0.1:5080"], esServidorDePruebas: false));
    }

    [Fact]
    public async Task PedidoPorInterfazNoLoopback_SeRechazaSinProcesar()
    {
        await using var fabrica = new FabricaApi();
        _ = fabrica.Services;

        var externo = await EnviarDesdeAsync(fabrica, IPAddress.Parse("192.168.1.10"));
        Assert.Equal(StatusCodes.Status403Forbidden, externo.Response.StatusCode);
        Assert.False(externo.Response.Headers.ContainsKey("Set-Cookie"), "El pedido llegó al endpoint (emitió cookies)");

        var mapeado = await EnviarDesdeAsync(fabrica, IPAddress.Parse("::ffff:192.168.1.10"));
        Assert.Equal(StatusCodes.Status403Forbidden, mapeado.Response.StatusCode);

        foreach (var local in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback, IPAddress.Parse("::ffff:127.0.0.1") })
        {
            var respuesta = await EnviarDesdeAsync(fabrica, local);
            Assert.Equal(StatusCodes.Status200OK, respuesta.Response.StatusCode);
        }
    }

    private static Task<HttpContext> EnviarDesdeAsync(FabricaApi fabrica, IPAddress local) =>
        fabrica.Server.SendAsync(contexto =>
        {
            contexto.Request.Method = HttpMethods.Get;
            contexto.Request.Host = new HostString("localhost");
            contexto.Request.Path = "/api/acceso/ingreso";
            contexto.Connection.LocalIpAddress = local;
        });
}
