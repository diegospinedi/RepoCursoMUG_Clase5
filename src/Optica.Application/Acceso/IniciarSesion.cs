using Optica.Application.Comun;
using Optica.Domain.Acceso;

namespace Optica.Application.Acceso;

/// <summary>
/// Verifica la contraseña de ingreso y devuelve el sello de seguridad vigente para la sesión (FR-01).
/// Aplica el bloqueo de 5 intentos / 5 minutos (NFR-02).
/// </summary>
public sealed class IniciarSesion(
    IRepositorioCredencial repositorio,
    IHasherContrasena hasher,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    // Serializa los ingresos del proceso: pedidos simultáneos no pueden leer el mismo contador
    // de fallos y superar los 5 intentos (amenaza de fuerza bruta, C2 del threat model).
    private static readonly SemaphoreSlim Serializador = new(1, 1);

    public async Task<Guid> EjecutarAsync(string? contrasena)
    {
        await Serializador.WaitAsync();
        try
        {
            Exception? rechazo = null;
            var sello = Guid.Empty;

            await unidadDeTrabajo.EjecutarAsync(async () =>
            {
                var credencial = await repositorio.Obtener();
                if (credencial.Hash is not { } hash)
                {
                    rechazo = new ContrasenaNoDefinida();
                    return;
                }

                var ahora = reloj.GetUtcNow();
                if (credencial.EstaBloqueada(ahora))
                {
                    // Bloqueado: no se verifica el hash ni se extiende el bloqueo.
                    rechazo = new IngresoBloqueado(credencial.BloqueadoHasta!.Value);
                    return;
                }

                if (!hasher.Verificar(contrasena ?? string.Empty, hash))
                {
                    credencial.RegistrarFallo(ahora);
                    await repositorio.Guardar(credencial);
                    rechazo = credencial.EstaBloqueada(ahora)
                        ? new IngresoBloqueado(credencial.BloqueadoHasta!.Value)
                        : new ContrasenaIncorrecta(CredencialAcceso.MaximoIntentosFallidos - credencial.IntentosFallidos);
                    return;
                }

                credencial.RegistrarIngresoExitoso();
                await repositorio.Guardar(credencial);
                sello = credencial.SelloSeguridad;
            });

            // El rechazo se lanza fuera de la unidad de trabajo: si se lanzara adentro, la transacción
            // se revertiría y el fallo registrado no quedaría persistido.
            if (rechazo is not null)
            {
                throw rechazo;
            }

            return sello;
        }
        finally
        {
            Serializador.Release();
        }
    }
}
