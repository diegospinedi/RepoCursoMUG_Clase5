using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// Guarda un <see cref="decimal"/> como entero escalado ×10.000 (ADR-001): exacto, independiente de la
/// cultura y comparable, ordenable y sumable en SQL. Un valor con más de 4 decimales no se trunca: lanza
/// <see cref="InvalidOperationException"/>, porque es un error de programación (el dominio redondea antes).
/// Un valor que escalado no entra en un <see cref="long"/> también lanza <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class ConversorDecimalEscalado() : ValueConverter<decimal, long>(
    valor => AEntero(valor),
    entero => ADecimal(entero))
{
    public const long Escala = 10_000;

    public static long AEntero(decimal valor)
    {
        decimal escalado;
        try
        {
            escalado = valor * Escala;
        }
        catch (OverflowException error)
        {
            throw FueraDeRango(valor, error);
        }

        if (escalado != decimal.Truncate(escalado))
        {
            throw new InvalidOperationException(
                $"El valor {Texto(valor)} tiene más de 4 decimales y no se puede persistir sin pérdida.");
        }

        if (escalado < long.MinValue || escalado > long.MaxValue)
        {
            throw FueraDeRango(valor, null);
        }

        return decimal.ToInt64(escalado);
    }

    public static decimal ADecimal(long entero) => entero / (decimal)Escala;

    private static InvalidOperationException FueraDeRango(decimal valor, Exception? causa) =>
        new($"El valor {Texto(valor)} está fuera del rango que se puede persistir (×10.000 en un entero de 64 bits).", causa);

    private static string Texto(decimal valor) => valor.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
