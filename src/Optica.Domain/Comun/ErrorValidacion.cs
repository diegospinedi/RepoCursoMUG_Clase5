namespace Optica.Domain.Comun;

/// <summary>
/// Error de validación de un único campo: indica el campo afectado y la acción correctiva (FR-17).
/// </summary>
public sealed class ErrorValidacion : Exception
{
    public ErrorValidacion(string campo, string mensaje) : base(mensaje)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campo);
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);
        Campo = campo;
        Mensaje = mensaje;
    }

    public string Campo { get; }

    public string Mensaje { get; }
}
