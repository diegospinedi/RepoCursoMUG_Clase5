using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace Optica.Api.Errores;

/// <summary>
/// Respuesta 400 para cuerpos JSON que no se pudieron leer (campo faltante, tipo erróneo, campo no previsto o
/// JSON mal formado). Informa el campo afectado con un mensaje propio en español; nunca devuelve el mensaje
/// crudo del deserializador, que puede incluir nombres de tipos internos o fragmentos del cuerpo enviado.
/// </summary>
public static partial class RespuestaValidacion
{
    public const string CampoCuerpo = "cuerpo";
    public const string MensajeFaltante = "Completá este campo";
    public const string MensajeNoAdmitido = "Este campo no se admite";
    public const string MensajeFormato = "El valor no tiene el formato esperado";
    public const string MensajeCondicionFiscal = "Elegí una condición fiscal válida";
    public const string MensajeCuerpo = "El cuerpo del pedido no es un JSON válido";

    public static IActionResult Crear(ActionContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var errores = new List<(string Campo, string Mensaje)>();
        foreach (var (clave, entrada) in contexto.ModelState)
        {
            foreach (var error in entrada.Errors)
            {
                errores.AddRange(Traducir(clave, error.ErrorMessage));
            }
        }

        if (errores.Count == 0)
        {
            errores.Add((CampoCuerpo, MensajeCuerpo));
        }

        return Problemas.Resultado(Problemas.Validacion(errores));
    }

    private static IEnumerable<(string Campo, string Mensaje)> Traducir(string clave, string mensaje)
    {
        var faltantes = CamposFaltantes().Match(mensaje);
        if (faltantes.Success)
        {
            foreach (Match nombre in NombreEntreComillas().Matches(faltantes.Groups["campos"].Value))
            {
                yield return (nombre.Groups["nombre"].Value, MensajeFaltante);
            }

            yield break;
        }

        var noPrevisto = CampoNoPrevisto().Match(mensaje);
        if (noPrevisto.Success)
        {
            yield return (noPrevisto.Groups["nombre"].Value, MensajeNoAdmitido);
            yield break;
        }

        // Errores de un campo concreto: la clave es la ruta JSON ("$.alicuotaIva").
        if (clave.StartsWith("$.", StringComparison.Ordinal))
        {
            var campo = clave[2..].Split('.', '[')[0];
            // "...doesn't allow null values": un campo obligatorio enviado en null.
            if (mensaje.Contains("allow null", StringComparison.OrdinalIgnoreCase))
            {
                yield return (campo, MensajeFaltante);
            }
            else
            {
                yield return (campo, campo == "condicionFiscal" ? MensajeCondicionFiscal : MensajeFormato);
            }
        }

        // Cualquier otro error (cuerpo vacío, JSON mal formado, parámetro requerido) se informa en "cuerpo" solo si
        // no hubo errores de campo: lo decide Crear.
    }

    [GeneratedRegex(@"missing required properties including: (?<campos>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex CamposFaltantes();

    [GeneratedRegex(@"'(?<nombre>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex NombreEntreComillas();

    [GeneratedRegex(@"The JSON property '(?<nombre>[^']+)' could not be mapped", RegexOptions.CultureInvariant)]
    private static partial Regex CampoNoPrevisto();
}
