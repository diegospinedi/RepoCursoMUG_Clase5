namespace Optica.Domain.Comun;

/// <summary>
/// Conjunto de errores de validación acumulados, a lo sumo uno por campo (FR-17).
/// </summary>
public sealed class ErroresValidacion : Exception
{
    public ErroresValidacion(IEnumerable<ErrorValidacion> errores)
        : base("Hay datos con errores de validación.")
    {
        ArgumentNullException.ThrowIfNull(errores);
        Errores = errores.ToArray();
        if (Errores.Count == 0)
        {
            throw new ArgumentException("Debe haber al menos un error de validación.", nameof(errores));
        }
    }

    public IReadOnlyList<ErrorValidacion> Errores { get; }
}
