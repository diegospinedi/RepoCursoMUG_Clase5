namespace Optica.Application.Comun;

/// <summary>Se intentó crear la contraseña inicial cuando ya había una definida (E3-1, FR-03).</summary>
public sealed class ContrasenaYaDefinida() : Exception("La contraseña ya está definida");

/// <summary>Se intentó ingresar antes de crear la contraseña (E3-2).</summary>
public sealed class ContrasenaNoDefinida() : Exception("Todavía no se creó la contraseña");

/// <summary>El ingreso está bloqueado por intentos fallidos hasta <see cref="Hasta"/> (E3-3, NFR-02).</summary>
public sealed class IngresoBloqueado(DateTimeOffset hasta) : Exception("Ingreso bloqueado")
{
    public DateTimeOffset Hasta { get; } = hasta;
}

/// <summary>Contraseña de ingreso incorrecta; informa cuántos intentos quedan antes del bloqueo (E3-4).</summary>
public sealed class ContrasenaIncorrecta(int intentosRestantes) : Exception("La contraseña no es correcta")
{
    public int IntentosRestantes { get; } = intentosRestantes;
}

/// <summary>Al cambiar la contraseña, la actual informada no coincide (E3-5, AC-06).</summary>
public sealed class ContrasenaActualIncorrecta() : Exception(MensajeError)
{
    public const string MensajeError = "La contraseña actual no es correcta";

    public const string Campo = "contrasenaActual";

    public string Mensaje => MensajeError;
}
