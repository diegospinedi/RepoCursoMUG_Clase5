using System.Text;
using Optica.Domain.Comun;
using Optica.Domain.Configuracion;

namespace Optica.Domain.Catalogo;

/// <summary>
/// Artículo del catálogo con su Precio de Venta calculado (FR-01 a FR-09, FR-19 a FR-24).
/// El precio de venta nunca se asigna desde afuera: siempre sale de <see cref="CalculadoraPrecioVenta"/>.
/// </summary>
public sealed class Articulo
{
    public const int CodigoProveedorLongitudMaxima = 50;
    public const int DescripcionLongitudMaxima = 200;
    public const decimal CostoMaximoAbsoluto = 100_000_000m;
    public const decimal MargenMinimo = 0m;
    public const decimal MargenMaximo = 1_000m;
    public const int DecimalesMaximos = 2;

    private const string MensajeDecimales = "Ingresá como máximo 2 decimales";

    private Articulo()
    {
    }

    /// <summary>Código autonumérico; 0 hasta que el repositorio lo asigna (FR-02).</summary>
    public int Codigo { get; private set; }

    public string CodigoProveedor { get; private set; } = string.Empty;

    /// <summary>Clave de coincidencia con la planilla (<see cref="TextoNormalizado.ClaveCodigoProveedor"/>).</summary>
    public string CodigoProveedorClave { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    /// <summary>Código en el proveedor y descripción normalizados para la búsqueda (FR-22).</summary>
    public string TextoBusqueda { get; private set; } = string.Empty;

    public decimal PrecioCosto { get; private set; }

    public decimal MargenUtilidad { get; private set; }

    public decimal PrecioVenta { get; private set; }

    public bool Activo { get; private set; }

    /// <summary>
    /// Crea un artículo activo con su precio calculado. Si algún dato es inválido lanza
    /// <see cref="ErroresValidacion"/> con un error por campo.
    /// </summary>
    public static Articulo Crear(
        string codigoProveedor,
        string descripcion,
        decimal costo,
        decimal margen,
        ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        Validar(codigoProveedor, descripcion, costo, margen);

        var articulo = new Articulo { Activo = true };
        articulo.Asignar(codigoProveedor, descripcion, costo, margen, configuracion);
        return articulo;
    }

    /// <summary>
    /// Modifica los datos editables y recalcula el precio (FR-09). Si hay errores no modifica nada.
    /// No cambia <see cref="Activo"/>.
    /// </summary>
    public void Modificar(
        string codigoProveedor,
        string descripcion,
        decimal costo,
        decimal margen,
        ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        Validar(codigoProveedor, descripcion, costo, margen);
        Asignar(codigoProveedor, descripcion, costo, margen, configuracion);
    }

    /// <summary>
    /// Cambia solo el precio de costo (planilla, FR-12) y recalcula el precio. Acepta negativos y
    /// cero dentro del rango (FR-16) y no toca <see cref="Activo"/> (FR-21).
    /// </summary>
    public void CambiarCosto(decimal costo, ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        LanzarSiHay(ValidarCosto(costo));

        var precioVenta = CalculadoraPrecioVenta.Calcular(costo, MargenUtilidad, configuracion);

        PrecioCosto = costo;
        PrecioVenta = precioVenta;
    }

    /// <summary>Recalcula el precio de venta con la configuración indicada (FR-10).</summary>
    public void RecalcularPrecio(ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        PrecioVenta = CalculadoraPrecioVenta.Calcular(PrecioCosto, MargenUtilidad, configuracion);
    }

    /// <summary>Marca el artículo como inactivo y conserva todos sus datos (FR-19).</summary>
    public void Desactivar() => Activo = false;

    /// <summary>Vuelve a marcar el artículo como activo (FR-20).</summary>
    public void Reactivar() => Activo = true;

    private void Asignar(
        string codigoProveedor,
        string descripcion,
        decimal costo,
        decimal margen,
        ConfiguracionNegocio configuracion)
    {
        // Primero se calcula todo en variables locales y al final se asigna, para que una
        // excepción no deje el artículo a medio modificar.
        var codigo = TextoNormalizado.CodigoProveedor(codigoProveedor);
        var clave = TextoNormalizado.ClaveCodigoProveedor(codigo);
        var descripcionRecortada = descripcion.Trim();
        var textoBusqueda = TextoNormalizado.ParaBusqueda(codigo + " " + descripcionRecortada);
        var precioVenta = CalculadoraPrecioVenta.Calcular(costo, margen, configuracion);

        CodigoProveedor = codigo;
        CodigoProveedorClave = clave;
        Descripcion = descripcionRecortada;
        TextoBusqueda = textoBusqueda;
        PrecioCosto = costo;
        MargenUtilidad = margen;
        PrecioVenta = precioVenta;
    }

    private static void Validar(string codigoProveedor, string descripcion, decimal costo, decimal margen) =>
        LanzarSiHay(
            ValidarTexto(
                "codigoProveedor",
                codigoProveedor?.Normalize(NormalizationForm.FormC),
                CodigoProveedorLongitudMaxima,
                "Ingresá el código en el proveedor",
                "El código en el proveedor admite hasta 50 caracteres"),
            ValidarTexto(
                "descripcion",
                descripcion,
                DescripcionLongitudMaxima,
                "Ingresá la descripción",
                "La descripción admite hasta 200 caracteres"),
            ValidarCosto(costo),
            ValidarMargen(margen));

    private static ErrorValidacion? ValidarTexto(
        string campo,
        string? valor,
        int longitudMaxima,
        string mensajeVacio,
        string mensajeLargo)
    {
        var recortado = valor?.Trim() ?? string.Empty;
        if (recortado.Length == 0)
        {
            return new ErrorValidacion(campo, mensajeVacio);
        }

        return recortado.Length > longitudMaxima ? new ErrorValidacion(campo, mensajeLargo) : null;
    }

    private static ErrorValidacion? ValidarCosto(decimal valor)
    {
        const string campo = "precioCosto";
        if (Math.Abs(valor) > CostoMaximoAbsoluto)
        {
            return new ErrorValidacion(campo, "El precio de costo no puede superar $100.000.000");
        }

        return TieneMasDeDosDecimales(valor) ? new ErrorValidacion(campo, MensajeDecimales) : null;
    }

    private static ErrorValidacion? ValidarMargen(decimal valor)
    {
        const string campo = "margenUtilidad";
        if (valor < MargenMinimo || valor > MargenMaximo)
        {
            return new ErrorValidacion(campo, "El margen debe estar entre 0 y 1.000");
        }

        return TieneMasDeDosDecimales(valor) ? new ErrorValidacion(campo, MensajeDecimales) : null;
    }

    // Compara por valor, no por escala: 10.50m y 10.500m tienen 2 decimales significativos.
    private static bool TieneMasDeDosDecimales(decimal valor) =>
        decimal.Round(valor, DecimalesMaximos) != valor;

    private static void LanzarSiHay(params ErrorValidacion?[] posibles)
    {
        var errores = posibles.OfType<ErrorValidacion>().ToList();
        if (errores.Count > 0)
        {
            throw new ErroresValidacion(errores);
        }
    }
}
