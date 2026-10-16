using Microsoft.Extensions.Time.Testing;
using Optica.Application.Acceso;
using Optica.Application.Comun;
using Optica.Application.Tests.Dobles;
using Optica.Domain.Comun;

namespace Optica.Application.Tests;

public class AccesoTests
{
    private const string Actual = "clave-actual-123";
    private const string Nueva = "clave-nueva-456";

    private static readonly DateTimeOffset Inicio = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _reloj = new(Inicio);
    private readonly HasherFalso _hasher = new();

    private static RepositorioCredencialEnMemoria ConContrasena(int intentosFallidos = 0, DateTimeOffset? bloqueadoHasta = null) =>
        new(HasherFalso.HashDe(Actual), intentosFallidos, bloqueadoHasta);

    private IniciarSesion Ingreso(RepositorioCredencialEnMemoria repositorio) =>
        new(repositorio, _hasher, new UnidadDeTrabajoInmediata(repositorio), _reloj);

    private CambiarContrasena Cambio(RepositorioCredencialEnMemoria repositorio) =>
        new(repositorio, _hasher, new UnidadDeTrabajoInmediata(repositorio));

    // ---- Estado ----

    [Fact]
    public async Task ObtenerEstado_SinContrasena_InformaNoDefinida()
    {
        var estado = await new ObtenerEstadoAcceso(new RepositorioCredencialEnMemoria(), _reloj).EjecutarAsync();

        Assert.False(estado.ContrasenaDefinida);
        Assert.Null(estado.BloqueadoHasta);
    }

    [Fact]
    public async Task ObtenerEstado_BloqueoVencido_NoInformaBloqueo()
    {
        var vigente = await new ObtenerEstadoAcceso(ConContrasena(bloqueadoHasta: Inicio.AddMinutes(2)), _reloj).EjecutarAsync();
        var vencido = await new ObtenerEstadoAcceso(ConContrasena(bloqueadoHasta: Inicio), _reloj).EjecutarAsync();

        Assert.True(vigente.ContrasenaDefinida);
        Assert.Equal(Inicio.AddMinutes(2), vigente.BloqueadoHasta);
        Assert.True(vencido.ContrasenaDefinida);
        Assert.Null(vencido.BloqueadoHasta);
    }

    // ---- Creación inicial ----

    [Fact]
    public async Task CrearInicial_Valida_DefineLaContrasena()
    {
        var repositorio = new RepositorioCredencialEnMemoria();
        var selloPrevio = repositorio.SelloSeguridad;

        await new CrearContrasenaInicial(repositorio, _hasher).EjecutarAsync("primera-clave");

        Assert.Equal(HasherFalso.HashDe("primera-clave"), repositorio.Hash);
        Assert.NotEqual(selloPrevio, repositorio.SelloSeguridad);
        Assert.NotEqual(Guid.Empty, repositorio.SelloSeguridad);
    }

    [Fact]
    public async Task CrearInicial_YaDefinida_LanzaContrasenaYaDefinida()
    {
        var repositorio = ConContrasena();

        await Assert.ThrowsAsync<ContrasenaYaDefinida>(
            () => new CrearContrasenaInicial(repositorio, _hasher).EjecutarAsync("otra-clave-1"));

        Assert.Equal(HasherFalso.HashDe(Actual), repositorio.Hash);
    }

    [Fact]
    public async Task CrearInicial_Corta_LanzaErrorValidacion()
    {
        var repositorio = new RepositorioCredencialEnMemoria();

        var error = await Assert.ThrowsAsync<ErrorValidacion>(
            () => new CrearContrasenaInicial(repositorio, _hasher).EjecutarAsync("1234567"));

        Assert.Equal("contrasenaNueva", error.Campo);
        Assert.Null(repositorio.Hash);
    }

    // ---- Ingreso ----

    [Fact]
    public async Task Ingreso_SinContrasena_LanzaContrasenaNoDefinida()
    {
        var repositorio = new RepositorioCredencialEnMemoria();

        await Assert.ThrowsAsync<ContrasenaNoDefinida>(() => Ingreso(repositorio).EjecutarAsync("cualquier-cosa"));

        Assert.Equal(0, _hasher.LlamadasAVerificar);
    }

    [Fact]
    public async Task Ingreso_Correcta_DevuelveSelloYReiniciaContador()
    {
        var repositorio = ConContrasena(intentosFallidos: 3);

        var sello = await Ingreso(repositorio).EjecutarAsync(Actual);

        Assert.Equal(repositorio.SelloSeguridad, sello);
        Assert.Equal(0, repositorio.IntentosFallidos);
        Assert.Null(repositorio.BloqueadoHasta);
    }

    [Fact]
    public async Task Ingreso_Incorrecta_InformaIntentosRestantes()
    {
        var repositorio = ConContrasena();
        var ingreso = Ingreso(repositorio);

        var primero = await Assert.ThrowsAsync<ContrasenaIncorrecta>(() => ingreso.EjecutarAsync("incorrecta-1"));
        var segundo = await Assert.ThrowsAsync<ContrasenaIncorrecta>(() => ingreso.EjecutarAsync("incorrecta-2"));

        Assert.Equal(4, primero.IntentosRestantes);
        Assert.Equal(3, segundo.IntentosRestantes);
        // El fallo queda persistido aunque el caso de uso termine con una excepción.
        Assert.Equal(2, repositorio.IntentosFallidos);
    }

    [Fact]
    public async Task Ingreso_QuintoFallo_LanzaIngresoBloqueado()
    {
        var repositorio = ConContrasena(intentosFallidos: 4);

        var error = await Assert.ThrowsAsync<IngresoBloqueado>(() => Ingreso(repositorio).EjecutarAsync("incorrecta"));

        Assert.Equal(Inicio.AddMinutes(5), error.Hasta);
        Assert.Equal(Inicio.AddMinutes(5), repositorio.BloqueadoHasta);
        Assert.Equal(0, repositorio.IntentosFallidos);
    }

    [Fact]
    public async Task Ingreso_Bloqueado_NoVerificaHash()
    {
        var hasta = Inicio.AddMinutes(3);
        var repositorio = ConContrasena(bloqueadoHasta: hasta);

        var error = await Assert.ThrowsAsync<IngresoBloqueado>(() => Ingreso(repositorio).EjecutarAsync(Actual));

        Assert.Equal(hasta, error.Hasta);
        Assert.Equal(0, _hasher.LlamadasAVerificar);
        // No se extiende el bloqueo ni se cuenta el intento.
        Assert.Equal(hasta, repositorio.BloqueadoHasta);
        Assert.Equal(0, repositorio.IntentosFallidos);
        Assert.Equal(0, repositorio.CantidadGuardados);
    }

    [Fact]
    public async Task Ingreso_BloqueoVencido_AceptaCorrecta()
    {
        var repositorio = ConContrasena();
        var ingreso = Ingreso(repositorio);
        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<ContrasenaIncorrecta>(() => ingreso.EjecutarAsync("incorrecta"));
        }

        await Assert.ThrowsAsync<IngresoBloqueado>(() => ingreso.EjecutarAsync("incorrecta"));

        _reloj.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<IngresoBloqueado>(() => ingreso.EjecutarAsync(Actual));

        _reloj.Advance(TimeSpan.FromSeconds(1));
        var sello = await ingreso.EjecutarAsync(Actual);

        Assert.Equal(repositorio.SelloSeguridad, sello);
        Assert.Null(repositorio.BloqueadoHasta);
        Assert.Equal(0, repositorio.IntentosFallidos);
    }

    // ---- Sesión ----

    [Fact]
    public async Task ValidarSesion_SelloAnterior_DevuelveFalse()
    {
        var repositorio = ConContrasena();
        var validar = new ValidarSesion(repositorio);
        var selloAnterior = await Ingreso(repositorio).EjecutarAsync(Actual);
        Assert.True(await validar.EjecutarAsync(selloAnterior));

        var selloNuevo = await Cambio(repositorio).EjecutarAsync(Actual, Nueva);

        Assert.False(await validar.EjecutarAsync(selloAnterior));
        Assert.True(await validar.EjecutarAsync(selloNuevo));

        await new BlanquearContrasena(repositorio, new UnidadDeTrabajoInmediata(repositorio)).EjecutarAsync();

        // Sin contraseña definida ninguna sesión es válida, ni siquiera con el sello vigente.
        Assert.False(await validar.EjecutarAsync(selloNuevo));
        Assert.False(await validar.EjecutarAsync(repositorio.SelloSeguridad));
    }

    // ---- Cambio de contraseña ----

    [Fact]
    public async Task Cambiar_ConActualCorrecta_AceptaSoloLaNueva()
    {
        var repositorio = ConContrasena();
        var selloPrevio = repositorio.SelloSeguridad;

        var sello = await Cambio(repositorio).EjecutarAsync(Actual, Nueva);

        Assert.Equal(repositorio.SelloSeguridad, sello);
        Assert.NotEqual(selloPrevio, sello);
        var ingreso = Ingreso(repositorio);
        await Assert.ThrowsAsync<ContrasenaIncorrecta>(() => ingreso.EjecutarAsync(Actual));
        Assert.Equal(repositorio.SelloSeguridad, await ingreso.EjecutarAsync(Nueva));
    }

    [Fact]
    public async Task Cambiar_ConActualIncorrecta_LanzaYMantieneLaAnterior()
    {
        var repositorio = ConContrasena();
        var selloPrevio = repositorio.SelloSeguridad;

        var error = await Assert.ThrowsAsync<ContrasenaActualIncorrecta>(
            () => Cambio(repositorio).EjecutarAsync("no-es-la-actual", Nueva));

        Assert.Equal("contrasenaActual", ContrasenaActualIncorrecta.Campo);
        Assert.Equal("La contraseña actual no es correcta", error.Mensaje);
        Assert.Equal(HasherFalso.HashDe(Actual), repositorio.Hash);
        Assert.Equal(selloPrevio, repositorio.SelloSeguridad);
    }

    [Fact]
    public async Task Cambiar_FallosNoCuentanParaElBloqueo()
    {
        var repositorio = ConContrasena();
        var cambio = Cambio(repositorio);

        for (var i = 0; i < 6; i++)
        {
            await Assert.ThrowsAsync<ContrasenaActualIncorrecta>(() => cambio.EjecutarAsync("no-es-la-actual", Nueva));
        }

        Assert.Equal(0, repositorio.IntentosFallidos);
        Assert.Null(repositorio.BloqueadoHasta);
        Assert.Equal(repositorio.SelloSeguridad, await Ingreso(repositorio).EjecutarAsync(Actual));
    }

    [Fact]
    public async Task Cambiar_NuevaCorta_LanzaErrorValidacion()
    {
        var repositorio = ConContrasena();
        var selloPrevio = repositorio.SelloSeguridad;

        var error = await Assert.ThrowsAsync<ErrorValidacion>(() => Cambio(repositorio).EjecutarAsync(Actual, "1234567"));

        Assert.Equal("contrasenaNueva", error.Campo);
        Assert.Equal(HasherFalso.HashDe(Actual), repositorio.Hash);
        Assert.Equal(selloPrevio, repositorio.SelloSeguridad);
    }

    [Fact]
    public async Task Cambiar_ActualIncorrectaYNuevaCorta_InformaAmbosCampos()
    {
        var repositorio = ConContrasena();
        var selloPrevio = repositorio.SelloSeguridad;

        var errores = await Assert.ThrowsAsync<ErroresValidacion>(
            () => Cambio(repositorio).EjecutarAsync("no-es-la-actual", "1234567"));

        Assert.Collection(
            errores.Errores.OrderBy(e => e.Campo, StringComparer.Ordinal),
            e => { Assert.Equal("contrasenaActual", e.Campo); Assert.Equal("La contraseña actual no es correcta", e.Mensaje); },
            e => { Assert.Equal("contrasenaNueva", e.Campo); Assert.Equal("La contraseña debe tener al menos 8 caracteres", e.Mensaje); });
        Assert.Equal(HasherFalso.HashDe(Actual), repositorio.Hash);
        Assert.Equal(selloPrevio, repositorio.SelloSeguridad);
    }

    [Fact]
    public async Task Cambiar_SinContrasenaDefinida_LanzaContrasenaNoDefinida()
    {
        var repositorio = new RepositorioCredencialEnMemoria();
        var selloPrevio = repositorio.SelloSeguridad;

        await Assert.ThrowsAsync<ContrasenaNoDefinida>(() => Cambio(repositorio).EjecutarAsync(Actual, Nueva));

        Assert.Null(repositorio.Hash);
        Assert.Equal(selloPrevio, repositorio.SelloSeguridad);
        Assert.Equal(0, repositorio.CantidadGuardados);
    }

    // ---- Blanqueo ----

    [Fact]
    public async Task Blanquear_DejaSinContrasenaYRegeneraSello()
    {
        var repositorio = ConContrasena(intentosFallidos: 2, bloqueadoHasta: Inicio.AddMinutes(1));
        var selloPrevio = repositorio.SelloSeguridad;

        var habiaContrasena = await new BlanquearContrasena(repositorio, new UnidadDeTrabajoInmediata(repositorio)).EjecutarAsync();

        Assert.True(habiaContrasena);
        Assert.Null(repositorio.Hash);
        Assert.NotEqual(selloPrevio, repositorio.SelloSeguridad);
        Assert.Equal(0, repositorio.IntentosFallidos);
        Assert.Null(repositorio.BloqueadoHasta);
        var estado = await new ObtenerEstadoAcceso(repositorio, _reloj).EjecutarAsync();
        Assert.False(estado.ContrasenaDefinida);
    }

    [Fact]
    public async Task Blanquear_SinContrasena_EsIdempotente()
    {
        var repositorio = new RepositorioCredencialEnMemoria();
        var blanqueo = new BlanquearContrasena(repositorio, new UnidadDeTrabajoInmediata(repositorio));

        var primera = await blanqueo.EjecutarAsync();
        var segunda = await blanqueo.EjecutarAsync();

        Assert.False(primera);
        Assert.False(segunda);
        Assert.Null(repositorio.Hash);
        // Después del blanqueo se puede volver a crear la contraseña inicial.
        await new CrearContrasenaInicial(repositorio, _hasher).EjecutarAsync("nueva-inicial");
        Assert.Equal(HasherFalso.HashDe("nueva-inicial"), repositorio.Hash);
    }
}
