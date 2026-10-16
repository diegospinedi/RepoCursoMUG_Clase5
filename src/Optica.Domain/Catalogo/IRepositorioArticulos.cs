namespace Optica.Domain.Catalogo;

public interface IRepositorioArticulos
{
    /// <summary>Devuelve el artículo con ese código, o <c>null</c> si no existe.</summary>
    Task<Articulo?> Obtener(int codigo);

    /// <summary>Agrega un artículo nuevo y devuelve el Código autonumérico asignado (FR-02).</summary>
    Task<int> Agregar(Articulo articulo);

    Task Guardar(Articulo articulo);

    /// <summary>Devuelve todos los artículos, activos e inactivos.</summary>
    Task<IReadOnlyList<Articulo>> ObtenerTodos();

    /// <summary>Guarda un conjunto de artículos modificados dentro de la transacción en curso.</summary>
    Task GuardarLote(IReadOnlyCollection<Articulo> articulos);

    /// <summary>
    /// Busca por texto ya normalizado con <see cref="TextoNormalizado.ParaBusqueda"/> y devuelve
    /// como máximo <paramref name="maximo"/> artículos, activos e inactivos.
    /// </summary>
    Task<IReadOnlyList<Articulo>> Buscar(string textoBusqueda, int maximo);

    /// <summary>
    /// Devuelve todos los artículos cuya <see cref="Articulo.CodigoProveedorClave"/> esté en el
    /// conjunto. Varios artículos pueden compartir la misma clave.
    /// </summary>
    Task<IReadOnlyList<Articulo>> ObtenerPorClaves(IReadOnlySet<string> claves);
}
