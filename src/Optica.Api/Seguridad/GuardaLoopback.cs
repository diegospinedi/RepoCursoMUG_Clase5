using System.Net;

namespace Optica.Api.Seguridad;

/// <summary>
/// El servidor solo puede escuchar en la interfaz loopback: la ausencia de TLS y la cookie sin <c>Secure</c> están
/// aceptadas únicamente porque el tráfico no sale de la PC (AR-01). Si la configuración (<c>Urls</c>, argumentos o
/// variables de entorno) lo hace escuchar en otra interfaz, el proceso se detiene.
/// </summary>
public static class GuardaLoopback
{
    /// <summary>Devuelve las direcciones que no son loopback. Una dirección que no se puede interpretar cuenta como ajena.</summary>
    public static IReadOnlyList<string> DireccionesAjenas(IEnumerable<string> direcciones)
    {
        ArgumentNullException.ThrowIfNull(direcciones);
        return direcciones.Where(d => !EsLoopback(d)).ToList();
    }

    /// <summary>
    /// Motivo por el que el servidor no puede seguir corriendo, o <c>null</c> si está bien. Sin direcciones
    /// informadas (feature nula o vacía) no se puede comprobar que escuche solo en loopback: falla, salvo con
    /// TestServer, que no escucha en ninguna interfaz.
    /// </summary>
    public static string? Problema(ICollection<string>? direcciones, bool esServidorDePruebas)
    {
        if (direcciones is null || direcciones.Count == 0)
        {
            return esServidorDePruebas
                ? null
                : "el servidor no informó en qué direcciones escucha, así que no se puede comprobar que sea solo loopback";
        }

        var ajenas = DireccionesAjenas(direcciones);
        return ajenas.Count == 0
            ? null
            : $"solo puede escuchar en loopback (127.0.0.1), pero se configuró {string.Join(", ", ajenas)}";
    }

    /// <summary>TestServer (pruebas en memoria) se reconoce por nombre, para no referenciar el paquete de pruebas.</summary>
    public static bool EsServidorDePruebas(object servidor)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        return servidor.GetType().FullName == "Microsoft.AspNetCore.TestHost.TestServer";
    }

    /// <summary>Loopback IPv4 (127.0.0.0/8), IPv6 (<c>::1</c>) o IPv4 loopback mapeada en IPv6.</summary>
    public static bool EsIpLoopback(IPAddress ip)
    {
        ArgumentNullException.ThrowIfNull(ip);
        return IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
    }

    private static bool EsLoopback(string direccion)
    {
        if (!Uri.TryCreate(direccion, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && EsIpLoopback(ip);
    }
}
