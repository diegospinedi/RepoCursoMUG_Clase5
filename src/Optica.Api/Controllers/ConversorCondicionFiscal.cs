using System.Text.Json;
using System.Text.Json.Serialization;
using Optica.Domain.Configuracion;

namespace Optica.Api.Controllers;

/// <summary>
/// <see cref="CondicionFiscal"/> como texto con el nombre exacto (<c>ResponsableInscripto</c> o
/// <c>Monotributo</c>). A diferencia de <see cref="JsonStringEnumConverter"/>, no acepta otra capitalización ni
/// el valor numérico del enum.
/// </summary>
public sealed class ConversorCondicionFiscal : JsonConverter<CondicionFiscal>
{
    public override CondicionFiscal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var valor = reader.GetString();
            foreach (var condicion in Enum.GetValues<CondicionFiscal>())
            {
                if (string.Equals(valor, condicion.ToString(), StringComparison.Ordinal))
                {
                    return condicion;
                }
            }
        }

        throw new JsonException("Condición fiscal no válida.");
    }

    public override void Write(Utf8JsonWriter writer, CondicionFiscal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
