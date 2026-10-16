using Optica.Application.Comun;

namespace Optica.Application.Tests.Dobles;

/// <summary>
/// Ejecuta la operación en el momento. Si lanza, devuelve a los participantes al estado previo
/// (como un rollback) y propaga la excepción.
/// </summary>
public sealed class UnidadDeTrabajoInmediata(params IParticipanteTransaccion[] participantes) : IUnidadDeTrabajo
{
    public async Task EjecutarAsync(Func<Task> operacion)
    {
        var instantaneas = participantes.Select(p => p.TomarInstantanea()).ToArray();
        try
        {
            await operacion();
        }
        catch
        {
            for (var i = 0; i < participantes.Length; i++)
            {
                participantes[i].Restaurar(instantaneas[i]);
            }

            throw;
        }
    }
}
