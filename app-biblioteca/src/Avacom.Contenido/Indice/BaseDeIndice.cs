using Microsoft.Data.Sqlite;

namespace Avacom.Contenido.Indice;

public sealed record ElementoIndexado(
    string ElementoRef, string PaqueteId, string ClavePaquete, string VersionElemento,
    string Tipo, string Titulo, string? TaxonomiaRef, string? Nivel, string? Grado,
    string? Asignatura, string? Idioma, string? HuellaArchivo, int? DuracionSeg,
    string Estado, string? SucesorRef);

public sealed record NodoTaxonomia(
    string TaxonomiaRef, string? PadreRef, string TipoNodo, string? Codigo,
    string Nombre, int Orden, string? Pais, string? Nivel);

/// <summary>
/// El índice del componente. Es una PROYECCIÓN de los manifiestos instalados,
/// no un catálogo propio. Si se pierde, se reconstruye escaneando los paquetes
/// y da exactamente lo mismo.
///
/// Contiene metadatos y nada más: referencia, título, tipo, nivel, materia,
/// versión y huella. El material vive fuera y cifrado.
/// </summary>
public sealed class BaseDeIndice : IDisposable
{
    private readonly SqliteConnection _cn;
    public Politica Politica { get; }

    public BaseDeIndice(string ruta)
    {
        _cn = new SqliteConnection($"Data Source={ruta}");
        _cn.Open();
        Ejecutar("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON;");
        Politica = new Politica(_cn);
    }

    public SqliteConnection Conexion => _cn;

    public void Ejecutar(string sql)
    {
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Aplica el esquema. Son las tablas m04 del contrato, más el registro de uso.</summary>
    public void Crear(string rutaGuion) => Ejecutar(File.ReadAllText(rutaGuion));

    public ElementoIndexado? Elemento(string elementoRef)
    {
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = """
            SELECT i.elemento_ref, i.paquete_id, p.clave_paquete, i.version_elemento, i.tipo,
                   i.titulo, i.taxonomia_ref, i.nivel_clave, i.grado, i.asignatura, i.idioma,
                   i.huella_archivo, i.duracion_seg, i.estado, i.sucesor_ref
            FROM m04_indice_elemento i
            JOIN m04_paquete_instalado p ON p.id = i.paquete_id AND p.estado='activo'
            WHERE i.elemento_ref = $r
            """;
        cmd.Parameters.AddWithValue("$r", elementoRef);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Leer(r) : null;
    }

    /// <summary>Lo que se lista en pantalla: ya filtrado por estado y por política.</summary>
    public IReadOnlyList<ElementoIndexado> Disponibles(string? nivel = null, string? asignatura = null, string? tipo = null)
    {
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = """
            SELECT i.elemento_ref, i.paquete_id, p.clave_paquete, i.version_elemento, i.tipo,
                   i.titulo, i.taxonomia_ref, i.nivel_clave, i.grado, i.asignatura, i.idioma,
                   i.huella_archivo, i.duracion_seg, i.estado, i.sucesor_ref
            FROM m04_indice_elemento i
            JOIN m04_paquete_instalado p ON p.id = i.paquete_id AND p.estado='activo'
            WHERE i.estado='vigente'
              AND ($niv IS NULL OR i.nivel_clave = $niv)
              AND ($asi IS NULL OR i.asignatura = $asi)
              AND ($tip IS NULL OR i.tipo = $tip)
            ORDER BY i.nivel_clave, i.asignatura, i.titulo
            """;
        cmd.Parameters.AddWithValue("$niv", (object?)nivel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$asi", (object?)asignatura ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tip", (object?)tipo ?? DBNull.Value);
        var lista = new List<ElementoIndexado>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(Leer(r));
        return lista.Where(Politica.Permite).ToList();   // la política se aplica encima
    }

    public IReadOnlyList<NodoTaxonomia> Taxonomia(string? padre)
    {
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = padre is null
            ? "SELECT taxonomia_ref,padre_ref,tipo_nodo,codigo,nombre,orden,pais,nivel_clave FROM m04_indice_taxonomia WHERE padre_ref IS NULL ORDER BY orden"
            : "SELECT taxonomia_ref,padre_ref,tipo_nodo,codigo,nombre,orden,pais,nivel_clave FROM m04_indice_taxonomia WHERE padre_ref=$p ORDER BY orden";
        if (padre is not null) cmd.Parameters.AddWithValue("$p", padre);
        var lista = new List<NodoTaxonomia>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(new NodoTaxonomia(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4),
                r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)));
        return lista;
    }

    private static ElementoIndexado Leer(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
        r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7),
        r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9),
        r.IsDBNull(10) ? null : r.GetString(10), r.IsDBNull(11) ? null : r.GetString(11),
        r.IsDBNull(12) ? null : r.GetInt32(12), r.GetString(13), r.IsDBNull(14) ? null : r.GetString(14));

    public void Dispose() => _cn.Dispose();
}
