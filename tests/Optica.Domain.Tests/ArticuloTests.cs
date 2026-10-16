using Optica.Domain.Catalogo;
using Optica.Domain.Comun;
using Optica.Domain.Configuracion;

namespace Optica.Domain.Tests;

public class ArticuloTests
{
    private static ConfiguracionNegocio Configuracion(
        decimal alicuota = 21m,
        CondicionFiscal condicion = CondicionFiscal.ResponsableInscripto,
        decimal multiplo = 0.01m)
    {
        var configuracion = ConfiguracionNegocio.Inicial();
        configuracion.Actualizar(alicuota, condicion, null, multiplo);
        return configuracion;
    }

    private static Articulo ArticuloBase(ConfiguracionNegocio? configuracion = null) =>
        Articulo.Crear("LEN-001", "Lente orgánico 1.56", 1210m, 50m, configuracion ?? Configuracion());

    private static ErrorValidacion UnicoError(Action accion)
    {
        var errores = Assert.Throws<ErroresValidacion>(accion);
        return Assert.Single(errores.Errores);
    }

    [Fact]
    public void Articulo_Crear_NormalizaTextosYCalculaPrecio()
    {
        var articulo = Articulo.Crear("  ÁB-01 ", "  Lente Óptico  ", 1210m, 50m, Configuracion());

        Assert.Equal(0, articulo.Codigo);
        Assert.Equal("ÁB-01", articulo.CodigoProveedor);
        Assert.Equal("áb-01", articulo.CodigoProveedorClave);
        Assert.Equal("Lente Óptico", articulo.Descripcion);
        Assert.Equal("ab-01 lente optico", articulo.TextoBusqueda);
        Assert.Equal(1210m, articulo.PrecioCosto);
        Assert.Equal(50m, articulo.MargenUtilidad);
        Assert.Equal(1815.00m, articulo.PrecioVenta);
        Assert.True(articulo.Activo);
    }

    [Fact]
    public void Articulo_Crear_CodigoProveedorFormD_SeGuardaEnFormC()
    {
        var articulo = Articulo.Crear(" AN\u0303O-1 ", "Lente", 100m, 0m, Configuracion());

        Assert.Equal("A\u00D1O-1", articulo.CodigoProveedor);
        Assert.Equal("a\u00F1o-1", articulo.CodigoProveedorClave);
    }

    [Fact]
    public void Articulo_CambiarMargen60_Recalcula1936()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);

        articulo.Modificar("LEN-001", "Lente orgánico 1.56", 1210m, 60m, configuracion);

        Assert.Equal(60m, articulo.MargenUtilidad);
        Assert.Equal(1936.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_CambiarCosto2420_Recalcula3630()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);

        articulo.Modificar("LEN-001", "Lente orgánico 1.56", 2420m, 50m, configuracion);

        Assert.Equal(2420m, articulo.PrecioCosto);
        Assert.Equal(3630.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_CambiarCosto_RecalculaYNoReactiva()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);
        articulo.Desactivar();

        articulo.CambiarCosto(2420m, configuracion);

        Assert.Equal(2420m, articulo.PrecioCosto);
        Assert.Equal(3630.00m, articulo.PrecioVenta);
        Assert.False(articulo.Activo);
    }

    [Fact]
    public void Articulo_CambiarCostoNegativo_AceptaYDejaPrecioNegativo()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);

        articulo.CambiarCosto(-100m, configuracion);

        Assert.Equal(-100m, articulo.PrecioCosto);
        Assert.Equal(-150.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_Desactivar_ConservaDatos()
    {
        var articulo = ArticuloBase();

        articulo.Desactivar();

        Assert.False(articulo.Activo);
        Assert.Equal("LEN-001", articulo.CodigoProveedor);
        Assert.Equal("Lente orgánico 1.56", articulo.Descripcion);
        Assert.Equal(1210m, articulo.PrecioCosto);
        Assert.Equal(50m, articulo.MargenUtilidad);
        Assert.Equal(1815.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_Reactivar_QuedaActivo()
    {
        var articulo = ArticuloBase();
        articulo.Desactivar();
        Assert.False(articulo.Activo);

        articulo.Reactivar();

        Assert.True(articulo.Activo);
        Assert.Equal(1815.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_Margen1000_Acepta()
    {
        var articulo = Articulo.Crear("X-1", "Armazón", 100m, 1000m, Configuracion());

        Assert.Equal(1000m, articulo.MargenUtilidad);
        Assert.Equal(1100.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_Margen100001_Rechaza()
    {
        var error = UnicoError(() => Articulo.Crear("X-1", "Armazón", 100m, 1000.01m, Configuracion()));

        Assert.Equal("margenUtilidad", error.Campo);
        Assert.Equal("El margen debe estar entre 0 y 1.000", error.Mensaje);
    }

    [Fact]
    public void Articulo_MargenNegativo_Rechaza()
    {
        var error = UnicoError(() => Articulo.Crear("X-1", "Armazón", 100m, -0.01m, Configuracion()));

        Assert.Equal("margenUtilidad", error.Campo);
        Assert.Equal("El margen debe estar entre 0 y 1.000", error.Mensaje);
    }

    [Fact]
    public void Articulo_Costo100000000_Acepta()
    {
        var articulo = Articulo.Crear("X-1", "Armazón", 100_000_000m, 50m, Configuracion());

        Assert.Equal(100_000_000m, articulo.PrecioCosto);
        Assert.Equal(150_000_000.00m, articulo.PrecioVenta);
    }

    [Theory]
    [InlineData("100000000.01")]
    [InlineData("-100000000.01")]
    public void Articulo_Costo10000000001_Rechaza(string costoTexto)
    {
        var costo = decimal.Parse(costoTexto, System.Globalization.CultureInfo.InvariantCulture);

        var error = UnicoError(() => Articulo.Crear("X-1", "Armazón", costo, 50m, Configuracion()));

        Assert.Equal("precioCosto", error.Campo);
        Assert.Equal("El precio de costo no puede superar $100.000.000", error.Mensaje);
    }

    [Fact]
    public void Articulo_MasDeDosDecimales_Rechaza()
    {
        var errores = Assert.Throws<ErroresValidacion>(
            () => Articulo.Crear("X-1", "Armazón", 1.234m, 50.555m, Configuracion()));

        Assert.Equal(2, errores.Errores.Count);
        Assert.Contains(errores.Errores, e => e.Campo == "precioCosto" && e.Mensaje == "Ingresá como máximo 2 decimales");
        Assert.Contains(errores.Errores, e => e.Campo == "margenUtilidad" && e.Mensaje == "Ingresá como máximo 2 decimales");
    }

    [Fact]
    public void Articulo_DecimalesConCerosALaDerecha_Acepta()
    {
        var articulo = Articulo.Crear("X-1", "Armazón", 1210.500m, 50.000m, Configuracion());

        Assert.Equal(1210.5m, articulo.PrecioCosto);
        Assert.Equal(1815.75m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_DescripcionVaciaOCodigoLargo_Rechaza()
    {
        var errores = Assert.Throws<ErroresValidacion>(
            () => Articulo.Crear(new string('A', 51), "   ", 100m, 50m, Configuracion()));

        Assert.Equal(2, errores.Errores.Count);
        Assert.Contains(errores.Errores, e => e.Campo == "codigoProveedor");
        Assert.Contains(errores.Errores, e => e.Campo == "descripcion");
    }

    [Fact]
    public void Articulo_TextosEnLosBordes_Acepta()
    {
        var articulo = Articulo.Crear(" " + new string('A', 50) + " ", new string('d', 200), 100m, 0m, Configuracion());

        Assert.Equal(50, articulo.CodigoProveedor.Length);
        Assert.Equal(200, articulo.Descripcion.Length);
    }

    [Fact]
    public void Articulo_ModificarInvalido_NoCambiaNada()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);

        Assert.Throws<ErroresValidacion>(
            () => articulo.Modificar("NUEVO", "Otra", 2420m, 1000.01m, configuracion));

        Assert.Equal("LEN-001", articulo.CodigoProveedor);
        Assert.Equal("len-001", articulo.CodigoProveedorClave);
        Assert.Equal("Lente orgánico 1.56", articulo.Descripcion);
        Assert.Equal("len-001 lente organico 1.56", articulo.TextoBusqueda);
        Assert.Equal(1210m, articulo.PrecioCosto);
        Assert.Equal(50m, articulo.MargenUtilidad);
        Assert.Equal(1815.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_CambiarCostoFueraDeRango_NoCambiaNada()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);

        var error = UnicoError(() => articulo.CambiarCosto(100_000_001m, configuracion));

        Assert.Equal("precioCosto", error.Campo);
        Assert.Equal(1210m, articulo.PrecioCosto);
        Assert.Equal(1815.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_CambiarCostoConMasDeDosDecimales_RechazaYNoCambiaNada()
    {
        var configuracion = Configuracion();
        var articulo = ArticuloBase(configuracion);

        var error = UnicoError(() => articulo.CambiarCosto(2420.001m, configuracion));

        Assert.Equal("precioCosto", error.Campo);
        Assert.Equal("Ingresá como máximo 2 decimales", error.Mensaje);
        Assert.Equal(1210m, articulo.PrecioCosto);
        Assert.Equal(1815.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_CostoMenos100000000_Acepta()
    {
        var articulo = Articulo.Crear("X-1", "Armazón", -100_000_000m, 50m, Configuracion());

        Assert.Equal(-100_000_000m, articulo.PrecioCosto);
        Assert.Equal(-150_000_000.00m, articulo.PrecioVenta);
    }

    [Theory]
    [InlineData(null, "Armazón", "codigoProveedor", "Ingresá el código en el proveedor")]
    [InlineData(" \t ", "Armazón", "codigoProveedor", "Ingresá el código en el proveedor")]
    [InlineData("X-1", null, "descripcion", "Ingresá la descripción")]
    [InlineData("X-1", "   ", "descripcion", "Ingresá la descripción")]
    public void Articulo_TextoNuloOSoloEspacios_Rechaza(
        string? codigoProveedor,
        string? descripcion,
        string campo,
        string mensaje)
    {
        var error = UnicoError(() => Articulo.Crear(codigoProveedor!, descripcion!, 100m, 50m, Configuracion()));

        Assert.Equal(campo, error.Campo);
        Assert.Equal(mensaje, error.Mensaje);
    }

    [Fact]
    public void Articulo_CrearSinConfiguracion_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Articulo.Crear("X-1", "Armazón", 100m, 50m, null!));
    }

    [Fact]
    public void RecalculoPrecios_Aplicar_ArgumentosNulos_LanzaArgumentNull()
    {
        var configuracion = Configuracion();

        Assert.Throws<ArgumentNullException>(() => RecalculoPrecios.Aplicar(null!, configuracion));
        Assert.Throws<ArgumentNullException>(() => RecalculoPrecios.Aplicar(new[] { ArticuloBase() }, null!));
    }

    [Fact]
    public void Articulo_RecalcularPrecio_UsaConfiguracionNueva()
    {
        var articulo = ArticuloBase();

        articulo.RecalcularPrecio(Configuracion(multiplo: 50m));

        Assert.Equal(1850.00m, articulo.PrecioVenta);
    }

    [Fact]
    public void Articulo_RecalcularSinConfiguracion_LanzaArgumentNull()
    {
        var articulo = ArticuloBase();

        Assert.Throws<ArgumentNullException>(() => articulo.RecalcularPrecio(null!));
    }

    [Fact]
    public void RecalculoPrecios_Aplicar_RecalculaActivosEInactivos()
    {
        var configuracion = Configuracion();
        var activo = ArticuloBase(configuracion);
        var inactivo = Articulo.Crear("LEN-002", "Lente mineral", 1210m, 60m, configuracion);
        inactivo.Desactivar();
        var nueva = Configuracion(multiplo: 50m);

        var cantidad = RecalculoPrecios.Aplicar(new[] { activo, inactivo }, nueva);

        Assert.Equal(2, cantidad);
        Assert.Equal(1850.00m, activo.PrecioVenta);
        Assert.Equal(1950.00m, inactivo.PrecioVenta);
        Assert.True(activo.Activo);
        Assert.False(inactivo.Activo);
    }
}
