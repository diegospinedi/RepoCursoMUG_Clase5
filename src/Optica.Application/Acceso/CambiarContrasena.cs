using System.Runtime.ExceptionServices;
using Optica.Application.Comun;
using Optica.Domain.Acceso;
using Optica.Domain.Comun;

namespace Optica.Application.Acceso;

/// <summary>
/// Cambia la contraseña verificando la actual (FR-05). Se usa con sesión iniciada, por eso los fallos
/// de la contraseña actual no cuentan para el bloqueo de ingreso. Devuelve el sello nuevo.
/// </summary>
public sealed class CambiarContrasena(
    IRepositorioCredencial repositorio,
    IHasherContrasena hasher,
    IUnidadDeTrabajo unidadDeTrabajo)
{
    public async Task<Guid> EjecutarAsync(string? contrasenaActual, string? contrasenaNueva)
    {
        ContrasenaNueva? nueva = null;
        ErrorValidacion? errorNueva = null;
        try
        {
            nueva = ContrasenaNueva.Crear(contrasenaNueva);
        }
        catch (ErrorValidacion error)
        {
            errorNueva = error;
        }

        var sello = Guid.Empty;
        await unidadDeTrabajo.EjecutarAsync(async () =>
        {
            var credencial = await repositorio.Obtener();
            if (credencial.Hash is not { } hash)
            {
                throw new ContrasenaNoDefinida();
            }

            var actualCorrecta = hasher.Verificar(contrasenaActual ?? string.Empty, hash);

            // Si fallan las dos, se informan ambos campos juntos (FR-17).
            if (!actualCorrecta && errorNueva is not null)
            {
                throw new ErroresValidacion(
                [
                    new ErrorValidacion(ContrasenaActualIncorrecta.Campo, ContrasenaActualIncorrecta.MensajeError),
                    errorNueva,
                ]);
            }

            if (!actualCorrecta)
            {
                throw new ContrasenaActualIncorrecta();
            }

            if (errorNueva is not null)
            {
                // Relanza conservando el stack trace original de la validación.
                ExceptionDispatchInfo.Throw(errorNueva);
            }

            credencial.CambiarHash(hasher.Hashear(nueva!));
            await repositorio.Guardar(credencial);
            sello = credencial.SelloSeguridad;
        });

        return sello;
    }
}
