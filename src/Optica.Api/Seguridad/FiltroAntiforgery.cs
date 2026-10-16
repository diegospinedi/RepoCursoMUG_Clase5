using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;
using Optica.Api.Errores;

namespace Optica.Api.Seguridad;

/// <summary>
/// Exige el token antiforgery (header <c>X-XSRF-TOKEN</c>) en todo pedido que no sea GET, HEAD, OPTIONS ni TRACE,
/// incluidos el ingreso y la creación inicial, que son anónimos (E4-3). Corre como filtro de autorización:
/// antes de leer el cuerpo y, por lo tanto, antes de verificar cualquier contraseña.
/// </summary>
public sealed class FiltroAntiforgery(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public const string Titulo = "Token antiforgery inválido";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var metodo = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo) || HttpMethods.IsTrace(metodo))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = Problemas.Resultado(Problemas.Crear(StatusCodes.Status400BadRequest, Titulo));
        }
    }
}
