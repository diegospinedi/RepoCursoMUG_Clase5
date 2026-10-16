using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Optica.Api.Tests;

public class ConfiguracionApiTests
{
    [Fact]
    public async Task Configuracion_BaseNueva_DevuelveValoresIniciales()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        var configuracion = await LeerAsync(cliente);

        Assert.Equal(21m, configuracion.GetProperty("alicuotaIva").GetDecimal());
        Assert.Equal("ResponsableInscripto", configuracion.GetProperty("condicionFiscal").GetString());
        Assert.Equal(JsonValueKind.Null, configuracion.GetProperty("topeIdentificacion").ValueKind);
        Assert.Equal(0.01m, configuracion.GetProperty("multiploRedondeo").GetDecimal());
        Assert.Equal(
            ["alicuotaIva", "condicionFiscal", "multiploRedondeo", "topeIdentificacion"],
            configuracion.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Configuracion_PutYGet_Devuelve4Valores()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        using (var respuesta = await cliente.ActualizarConfiguracionAsync(new
        {
            alicuotaIva = 10.5m,
            condicionFiscal = "Monotributo",
            topeIdentificacion = 100000m,
            multiploRedondeo = 50m,
        }))
        {
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            AssertValoresAc12(await respuesta.Content.ReadFromJsonAsync<JsonElement>());
        }

        AssertValoresAc12(await LeerAsync(cliente));

        // Al volver a abrir la pantalla en otra sesión se ven los mismos valores.
        using var otro = fabrica.CrearCliente();
        await otro.IniciarSesionAsync(AccesoApiTests.Contrasena);
        AssertValoresAc12(await LeerAsync(otro));
    }

    [Fact]
    public async Task Configuracion_Multiplo0001_Devuelve400()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        using var respuesta = await cliente.ActualizarConfiguracionAsync(Cuerpo(multiplo: 0.001m));

        var errores = await Problemas.ErroresAsync(respuesta);
        Assert.Equal(["El valor mínimo es 0,01"], errores["multiploRedondeo"]);
        Assert.Single(errores);
        await AssertSinCambiosAsync(cliente);
    }

    [Fact]
    public async Task Configuracion_Alicuota101_Devuelve400()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        foreach (var alicuota in new[] { 101m, -1m })
        {
            using var respuesta = await cliente.ActualizarConfiguracionAsync(Cuerpo(alicuota: alicuota));
            var errores = await Problemas.ErroresAsync(respuesta);
            Assert.Equal(["La alícuota de IVA debe estar entre 0 y 100"], errores["alicuotaIva"]);
        }

        await AssertSinCambiosAsync(cliente);
    }

    [Fact]
    public async Task Configuracion_TopeNegativo_Devuelve400()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        using var respuesta = await cliente.ActualizarConfiguracionAsync(Cuerpo(tope: -1m));

        var errores = await Problemas.ErroresAsync(respuesta);
        Assert.Equal(["El tope no puede ser negativo"], errores["topeIdentificacion"]);
        await AssertSinCambiosAsync(cliente);
    }

    [Fact]
    public async Task Configuracion_CampoFaltante_Devuelve400()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        // Falta un campo obligatorio.
        await AssertErrorEnCampoAsync(
            cliente,
            """{ "alicuotaIva": 10.5, "condicionFiscal": "Monotributo", "topeIdentificacion": null }""",
            "multiploRedondeo");

        // topeIdentificacion admite null, pero es obligatorio enviarlo.
        await AssertErrorEnCampoAsync(
            cliente,
            """{ "alicuotaIva": 10.5, "condicionFiscal": "Monotributo", "multiploRedondeo": 50 }""",
            "topeIdentificacion");

        // Tipo erróneo.
        await AssertErrorEnCampoAsync(
            cliente,
            """{ "alicuotaIva": "10,5", "condicionFiscal": "Monotributo", "topeIdentificacion": null, "multiploRedondeo": 50 }""",
            "alicuotaIva");

        // condicionFiscal: solo los dos nombres exactos (ni otra capitalización ni el número del enum).
        foreach (var condicion in new[] { "\"monotributo\"", "\"Otra\"", "1" })
        {
            await AssertErrorEnCampoAsync(
                cliente,
                $$"""{ "alicuotaIva": 10.5, "condicionFiscal": {{condicion}}, "topeIdentificacion": null, "multiploRedondeo": 50 }""",
                "condicionFiscal");
        }

        // Campo no previsto (mass assignment).
        await AssertErrorEnCampoAsync(
            cliente,
            """{ "id": 2, "alicuotaIva": 10.5, "condicionFiscal": "Monotributo", "topeIdentificacion": null, "multiploRedondeo": 50 }""",
            "id");

        // Cuerpo que no es JSON.
        using (var invalido = await cliente.ActualizarConfiguracionAsync("{ esto no es json"))
        {
            await Problemas.ErroresAsync(invalido);
        }

        await AssertSinCambiosAsync(cliente);

        // Con todos los campos (tope en null) se acepta.
        using var completo = await cliente.ActualizarConfiguracionAsync(
            """{ "alicuotaIva": 10.5, "condicionFiscal": "Monotributo", "topeIdentificacion": null, "multiploRedondeo": 50 }""");
        Assert.Equal(HttpStatusCode.OK, completo.StatusCode);
    }

    // ---- Ayudas ----

    private static object Cuerpo(decimal alicuota = 10.5m, decimal? tope = 100000m, decimal multiplo = 50m) => new
    {
        alicuotaIva = alicuota,
        condicionFiscal = "Monotributo",
        topeIdentificacion = tope,
        multiploRedondeo = multiplo,
    };

    private static async Task<JsonElement> LeerAsync(ClienteApi cliente)
    {
        using var respuesta = await cliente.ObtenerConfiguracionAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertValoresAc12(JsonElement configuracion)
    {
        Assert.Equal(10.5m, configuracion.GetProperty("alicuotaIva").GetDecimal());
        Assert.Equal("Monotributo", configuracion.GetProperty("condicionFiscal").GetString());
        Assert.Equal(100000m, configuracion.GetProperty("topeIdentificacion").GetDecimal());
        Assert.Equal(50m, configuracion.GetProperty("multiploRedondeo").GetDecimal());
    }

    private static async Task AssertSinCambiosAsync(ClienteApi cliente)
    {
        var configuracion = await LeerAsync(cliente);
        Assert.Equal(21m, configuracion.GetProperty("alicuotaIva").GetDecimal());
        Assert.Equal("ResponsableInscripto", configuracion.GetProperty("condicionFiscal").GetString());
        Assert.Equal(JsonValueKind.Null, configuracion.GetProperty("topeIdentificacion").ValueKind);
        Assert.Equal(0.01m, configuracion.GetProperty("multiploRedondeo").GetDecimal());
    }

    private static async Task AssertErrorEnCampoAsync(ClienteApi cliente, string json, string campo)
    {
        using var respuesta = await cliente.ActualizarConfiguracionAsync(json);
        var errores = await Problemas.ErroresAsync(respuesta);
        Assert.True(errores.ContainsKey(campo), $"El 400 de {json} no informa el campo '{campo}': {string.Join(", ", errores.Keys)}");
        Assert.All(errores[campo], mensaje => Assert.False(string.IsNullOrWhiteSpace(mensaje)));
    }
}
