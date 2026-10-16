namespace Optica.Application.Comun;

/// <summary>
/// Ejecuta una operación dentro de una transacción: si la operación termina bien se confirma;
/// si lanza, se revierte y la excepción se propaga sin cambios (E3-7).
/// </summary>
public interface IUnidadDeTrabajo
{
    Task EjecutarAsync(Func<Task> operacion);
}
