using Optica.Domain.Comun;

namespace Optica.Domain.Configuracion;

/// <summary>
/// Parámetros del negocio (fila única Id = 1): alícuota de IVA, condición fiscal,
/// tope de identificación del receptor y múltiplo de redondeo comercial (FR-07 a FR-15).
/// </summary>
public sealed class ConfiguracionNegocio
{
    public const int IdUnico = 1;

    public const decimal AlicuotaMinima = 0m;
    public const decimal AlicuotaMaxima = 100m;
    public const decimal TopeMinimo = 0m;
    public const decimal TopeMaximo = 1_000_000_000m;
    public const decimal MultiploMinimo = 0.01m;
    public const decimal MultiploMaximo = 1_000_000m;
    public const int DecimalesMaximos = 2;

    private const string MensajeDecimales = "Ingresá como máximo 2 decimales";

    private ConfiguracionNegocio()
    {
        Id = IdUnico;
    }

    public int Id { get; private set; }

    public decimal AlicuotaIva { get; private set; }

    public CondicionFiscal CondicionFiscal { get; private set; }

    public decimal? TopeIdentificacion { get; private set; }

    public decimal MultiploRedondeo { get; private set; }

    /// <summary>Valores de una base nueva (FR-15).</summary>
    public static ConfiguracionNegocio Inicial() => new()
    {
        AlicuotaIva = 21m,
        CondicionFiscal = CondicionFiscal.ResponsableInscripto,
        TopeIdentificacion = null,
        MultiploRedondeo = 0.01m,
    };

    /// <summary>
    /// Valida los cuatro valores y solo si todos son válidos los asigna. Si hay errores, lanza
    /// <see cref="ErroresValidacion"/> con un error por cada campo inválido y no modifica nada.
    /// </summary>
    public void Actualizar(
        decimal alicuotaIva,
        CondicionFiscal condicionFiscal,
        decimal? topeIdentificacion,
        decimal multiploRedondeo)
    {
        var errores = new List<ErrorValidacion>();

        AgregarSiHay(errores, ValidarAlicuota(alicuotaIva));
        AgregarSiHay(errores, ValidarCondicion(condicionFiscal));
        AgregarSiHay(errores, ValidarTope(topeIdentificacion));
        AgregarSiHay(errores, ValidarMultiplo(multiploRedondeo));

        if (errores.Count > 0)
        {
            throw new ErroresValidacion(errores);
        }

        AlicuotaIva = alicuotaIva;
        CondicionFiscal = condicionFiscal;
        TopeIdentificacion = topeIdentificacion;
        MultiploRedondeo = multiploRedondeo;
    }

    private static ErrorValidacion? ValidarAlicuota(decimal valor)
    {
        const string campo = "alicuotaIva";
        if (valor < AlicuotaMinima || valor > AlicuotaMaxima)
        {
            return new ErrorValidacion(campo, "La alícuota de IVA debe estar entre 0 y 100");
        }

        return TieneMasDeDosDecimales(valor) ? new ErrorValidacion(campo, MensajeDecimales) : null;
    }

    private static ErrorValidacion? ValidarCondicion(CondicionFiscal valor) =>
        Enum.IsDefined(valor) ? null : new ErrorValidacion("condicionFiscal", "Elegí una condición fiscal válida");

    private static ErrorValidacion? ValidarTope(decimal? valor)
    {
        const string campo = "topeIdentificacion";
        if (valor is not { } tope)
        {
            return null;
        }

        if (tope < TopeMinimo)
        {
            return new ErrorValidacion(campo, "El tope no puede ser negativo");
        }

        if (tope > TopeMaximo)
        {
            return new ErrorValidacion(campo, "El tope no puede superar 1.000.000.000");
        }

        return TieneMasDeDosDecimales(tope) ? new ErrorValidacion(campo, MensajeDecimales) : null;
    }

    private static ErrorValidacion? ValidarMultiplo(decimal valor)
    {
        const string campo = "multiploRedondeo";
        if (valor < MultiploMinimo)
        {
            return new ErrorValidacion(campo, "El valor mínimo es 0,01");
        }

        if (valor > MultiploMaximo)
        {
            return new ErrorValidacion(campo, "El valor máximo es 1.000.000");
        }

        return TieneMasDeDosDecimales(valor) ? new ErrorValidacion(campo, MensajeDecimales) : null;
    }

    // Compara por valor, no por escala: 10.50m y 10.500m tienen 2 decimales significativos.
    private static bool TieneMasDeDosDecimales(decimal valor) =>
        decimal.Round(valor, DecimalesMaximos) != valor;

    private static void AgregarSiHay(List<ErrorValidacion> errores, ErrorValidacion? error)
    {
        if (error is not null)
        {
            errores.Add(error);
        }
    }
}
