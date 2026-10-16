using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace Optica.Api.Errores;

/// <summary>
/// Construcción de respuestas <c>application/problem+json</c> (RFC 9457) con el formato común de la API:
/// <c>title</c>, <c>status</c> y, en los 400 de validación, <c>errors: { "campo": ["mensaje"] }</c> (FR-17).
/// </summary>
public static class Problemas
{
    public const string TipoContenido = "application/problem+json";
    public const string TituloValidacion = "Revisá los datos ingresados";

    public static ProblemDetails Crear(int estado, string titulo, IEnumerable<KeyValuePair<string, object?>>? extensiones = null)
    {
        var problema = new ProblemDetails { Status = estado, Title = titulo };
        foreach (var (clave, valor) in extensiones ?? [])
        {
            problema.Extensions[clave] = valor;
        }

        return problema;
    }

    /// <summary>400 con un mensaje por campo; un mismo campo puede traer varios mensajes.</summary>
    public static HttpValidationProblemDetails Validacion(IEnumerable<(string Campo, string Mensaje)> errores)
    {
        var porCampo = errores
            .GroupBy(e => e.Campo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mensaje).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        return new HttpValidationProblemDetails(porCampo)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = TituloValidacion,
        };
    }

    public static ObjectResult Resultado(ProblemDetails problema)
    {
        ArgumentNullException.ThrowIfNull(problema);

        var resultado = new ObjectResult(problema) { StatusCode = problema.Status };
        resultado.ContentTypes.Add(TipoContenido);
        return resultado;
    }

    /// <summary>Fecha en ISO 8601 UTC (<c>2026-10-16T12:05:00.0000000Z</c>).</summary>
    public static string FechaUtc(DateTimeOffset fecha) => fecha.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
