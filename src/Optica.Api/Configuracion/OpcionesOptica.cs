namespace Optica.Api.Configuracion;

/// <summary>
/// Rutas de datos del sistema (sección <c>Optica</c>): la base SQLite, las claves de Data Protection y la
/// auditoría del blanqueo. Ninguna puede caer dentro del árbol de fuentes (E4-11, R-12 del threat model):
/// así una base, una clave o un log nunca terminan commiteados.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Cargar"/> solo lee y valida; no crea nada en disco. Tiene que correr <b>antes</b> de
/// <c>AddInfraestructura</c>, que crea el directorio de la base: si la validación corriera después, el
/// directorio ya se habría creado dentro del repo.
/// </para>
/// <para>
/// Las rutas admiten <c>%ProgramData%</c> y <c>%LocalAppData%</c> en cualquier sistema operativo (se resuelven con
/// la variable de entorno del mismo nombre o, si no está definida, con
/// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>) y cualquier otra variable de entorno
/// con la sintaxis <c>%VARIABLE%</c>. Después de expandir tienen que ser absolutas.
/// </para>
/// </remarks>
public sealed class OpcionesOptica
{
    public const string Seccion = "Optica";

    private static readonly string[] MarcasDelRepo = ["Optica.sln", ".git"];

    private static readonly (string Variable, Environment.SpecialFolder Carpeta)[] CarpetasConocidas =
    [
        ("ProgramData", Environment.SpecialFolder.CommonApplicationData),
        ("LocalAppData", Environment.SpecialFolder.LocalApplicationData),
    ];

    private OpcionesOptica(string rutaBase, string directorioClaves, string directorioAuditoria)
    {
        RutaBase = rutaBase;
        DirectorioClaves = directorioClaves;
        DirectorioAuditoria = directorioAuditoria;
    }

    /// <summary>Directorio del archivo <c>optica.db</c> (ruta absoluta).</summary>
    public string RutaBase { get; }

    /// <summary>Directorio del anillo de claves de Data Protection (ruta absoluta).</summary>
    public string DirectorioClaves { get; }

    /// <summary>Directorio del log de auditoría del blanqueo (ruta absoluta).</summary>
    public string DirectorioAuditoria { get; }

    /// <summary>
    /// Lee las tres rutas y las valida. Si alguna falta, no es absoluta o cae dentro de un directorio que
    /// contiene <c>Optica.sln</c> o <c>.git</c>, lanza <see cref="OpcionesOpticaInvalidas"/> con todos los
    /// problemas encontrados. No crea directorios.
    /// </summary>
    public static OpcionesOptica Cargar(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var errores = new List<string>();
        var rutaBase = Resolver(configuracion, nameof(RutaBase), errores);
        var directorioClaves = Resolver(configuracion, nameof(DirectorioClaves), errores);
        var directorioAuditoria = Resolver(configuracion, nameof(DirectorioAuditoria), errores);

        if (errores.Count > 0)
        {
            throw new OpcionesOpticaInvalidas(errores);
        }

        return new OpcionesOptica(rutaBase!, directorioClaves!, directorioAuditoria!);
    }

    /// <summary>Crea los directorios de claves y de auditoría. El de la base lo crea <c>AddInfraestructura</c>.</summary>
    public void CrearDirectoriosAuxiliares()
    {
        Directory.CreateDirectory(DirectorioClaves);
        Directory.CreateDirectory(DirectorioAuditoria);
    }

    private static string? Resolver(IConfiguration configuracion, string nombre, List<string> errores)
    {
        var clave = $"{Seccion}:{nombre}";
        var valor = configuracion[clave];
        if (string.IsNullOrWhiteSpace(valor))
        {
            errores.Add($"Falta configurar {clave}.");
            return null;
        }

        var expandida = Expandir(valor.Trim());
        if (!Path.IsPathFullyQualified(expandida))
        {
            errores.Add($"{clave} tiene que ser una ruta absoluta (valor configurado: {expandida}).");
            return null;
        }

        var completa = Path.GetFullPath(expandida);
        if (BuscarRepoQueLaContiene(completa) is { } repo)
        {
            errores.Add(
                $"{clave} ({completa}) está dentro del árbol de fuentes ({repo}). " +
                "Configurá una ruta fuera del repo para que la base, las claves y los logs nunca se commiteen.");
            return null;
        }

        return completa;
    }

    private static string Expandir(string ruta)
    {
        // La variable de entorno del mismo nombre tiene prioridad (en Windows existe y coincide con la carpeta
        // conocida); si no está definida, se usa la carpeta conocida del sistema operativo.
        foreach (var (variable, carpeta) in CarpetasConocidas)
        {
            var valor = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(valor))
            {
                valor = Environment.GetFolderPath(carpeta);
            }

            if (!string.IsNullOrEmpty(valor))
            {
                ruta = ruta.Replace($"%{variable}%", valor, StringComparison.OrdinalIgnoreCase);
            }
        }

        return Environment.ExpandEnvironmentVariables(ruta);
    }

    // Recorre la ruta y todos sus ancestros (existan o no) buscando la marca de un repo.
    private static string? BuscarRepoQueLaContiene(string ruta)
    {
        for (var directorio = new DirectoryInfo(ruta); directorio is not null; directorio = directorio.Parent)
        {
            foreach (var marca in MarcasDelRepo)
            {
                var candidata = Path.Combine(directorio.FullName, marca);
                if (File.Exists(candidata) || Directory.Exists(candidata))
                {
                    return directorio.FullName;
                }
            }
        }

        return null;
    }
}

/// <summary>La configuración de rutas de <see cref="OpcionesOptica"/> no es válida: el proceso no arranca.</summary>
public sealed class OpcionesOpticaInvalidas(IReadOnlyList<string> errores)
    : Exception(string.Join(Environment.NewLine, errores))
{
    public IReadOnlyList<string> Errores { get; } = errores;
}
