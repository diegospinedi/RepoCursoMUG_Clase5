using Optica.Domain.Acceso;

namespace Optica.Application.Tests.Dobles;

/// <summary>
/// Guarda los valores de la credencial, no la instancia: cada <see cref="Obtener"/> devuelve un objeto
/// nuevo, como haría la base, así que un cambio sin <see cref="Guardar"/> no queda persistido.
/// </summary>
public sealed class RepositorioCredencialEnMemoria : IRepositorioCredencial, IParticipanteTransaccion
{
    private Datos _datos;

    public RepositorioCredencialEnMemoria(
        string? hash = null,
        int intentosFallidos = 0,
        DateTimeOffset? bloqueadoHasta = null)
    {
        _datos = new Datos(hash, Guid.NewGuid(), intentosFallidos, bloqueadoHasta);
    }

    public string? Hash => _datos.Hash;

    public Guid SelloSeguridad => _datos.Sello;

    public int IntentosFallidos => _datos.IntentosFallidos;

    public DateTimeOffset? BloqueadoHasta => _datos.BloqueadoHasta;

    public int CantidadGuardados { get; private set; }

    public Task<CredencialAcceso> Obtener() =>
        Task.FromResult(new CredencialAcceso(_datos.Hash, _datos.Sello, _datos.IntentosFallidos, _datos.BloqueadoHasta));

    public Task<bool> CrearSiNoExiste(string hash, Guid sello)
    {
        if (_datos.Hash is not null)
        {
            return Task.FromResult(false);
        }

        _datos = _datos with { Hash = hash, Sello = sello };
        return Task.FromResult(true);
    }

    public Task Guardar(CredencialAcceso credencial)
    {
        _datos = new Datos(credencial.Hash, credencial.SelloSeguridad, credencial.IntentosFallidos, credencial.BloqueadoHasta);
        CantidadGuardados++;
        return Task.CompletedTask;
    }

    public object TomarInstantanea() => _datos;

    public void Restaurar(object instantanea) => _datos = (Datos)instantanea;

    private sealed record Datos(string? Hash, Guid Sello, int IntentosFallidos, DateTimeOffset? BloqueadoHasta);
}
