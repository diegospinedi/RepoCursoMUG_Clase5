using Optica.Domain.Comun;

namespace Optica.Domain.Acceso;

/// <summary>
/// Contraseña elegida por la operadora al crearla o cambiarla: de 8 a 128 caracteres (FR-04).
/// Los espacios cuentan como caracteres y el valor no se recorta.
/// </summary>
public sealed class ContrasenaNueva
{
    public const int LongitudMinima = 8;
    public const int LongitudMaxima = 128;
    public const string Campo = "contrasenaNueva";

    private ContrasenaNueva(string valor) => Valor = valor;

    public string Valor { get; }

    public static ContrasenaNueva Crear(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || valor.Length < LongitudMinima)
        {
            throw new ErrorValidacion(Campo, "La contraseña debe tener al menos 8 caracteres");
        }

        if (valor.Length > LongitudMaxima)
        {
            throw new ErrorValidacion(Campo, "La contraseña puede tener como máximo 128 caracteres");
        }

        return new ContrasenaNueva(valor);
    }

    // Evita que el valor aparezca en logs o mensajes por un ToString() implícito.
    public override string ToString() => "***";
}
