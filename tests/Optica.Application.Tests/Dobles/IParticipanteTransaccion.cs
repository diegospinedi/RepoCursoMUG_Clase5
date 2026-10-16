namespace Optica.Application.Tests.Dobles;

/// <summary>
/// Repositorio en memoria que puede tomar una instantánea de lo "persistido" y volver a ella,
/// para que <see cref="UnidadDeTrabajoInmediata"/> simule el rollback de una transacción.
/// </summary>
public interface IParticipanteTransaccion
{
    object TomarInstantanea();

    void Restaurar(object instantanea);
}
