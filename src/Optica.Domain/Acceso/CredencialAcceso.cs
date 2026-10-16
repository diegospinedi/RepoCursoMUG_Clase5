namespace Optica.Domain.Acceso;

/// <summary>
/// Credencial única del sistema (fila Id = 1): hash de la contraseña, sello de seguridad de las sesiones
/// y estado del bloqueo de ingreso (NFR-02).
/// </summary>
public sealed class CredencialAcceso
{
    public const int IdUnico = 1;
    public const int MaximoIntentosFallidos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);

    public CredencialAcceso(
        string? hash,
        Guid selloSeguridad,
        int intentosFallidos = 0,
        DateTimeOffset? bloqueadoHasta = null)
    {
        if (hash is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        }

        if (selloSeguridad == Guid.Empty)
        {
            throw new ArgumentException("El sello de seguridad no puede estar vacío.", nameof(selloSeguridad));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(intentosFallidos);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(intentosFallidos, MaximoIntentosFallidos);

        Id = IdUnico;
        Hash = hash;
        SelloSeguridad = selloSeguridad;
        IntentosFallidos = intentosFallidos;
        BloqueadoHasta = bloqueadoHasta;
    }

    public int Id { get; private set; }

    public string? Hash { get; private set; }

    public Guid SelloSeguridad { get; private set; }

    public int IntentosFallidos { get; private set; }

    public DateTimeOffset? BloqueadoHasta { get; private set; }

    public bool ContrasenaDefinida => Hash is not null;

    public bool EstaBloqueada(DateTimeOffset ahora) => BloqueadoHasta > ahora;

    public void RegistrarFallo(DateTimeOffset ahora)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= MaximoIntentosFallidos)
        {
            BloqueadoHasta = ahora + DuracionBloqueo;
            IntentosFallidos = 0;
        }
    }

    public void RegistrarIngresoExitoso()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public void CambiarHash(string nuevoHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoHash);
        Hash = nuevoHash;
        SelloSeguridad = Guid.NewGuid();
    }

    public void Blanquear()
    {
        Hash = null;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        SelloSeguridad = Guid.NewGuid();
    }
}
