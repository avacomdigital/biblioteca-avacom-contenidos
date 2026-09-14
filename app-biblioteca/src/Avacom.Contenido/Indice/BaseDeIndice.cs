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
/// Un curso, que en este componente ES un paquete instalado.
///
/// No hay una entidad "curso" aparte, y no hace falta inventarla: un paquete ya
/// se publica con un titulo, una version, un pais, un nivel, un grado y una
/// asignatura, que es exactamente lo que describe un curso. Añadir una tabla de
/// cursos encima seria un segundo catalogo que hay que mantener sincronizado a
/// mano, y de esos ya sabemos como acaban.
///
/// CursoRef es la clave del paquete. Es estable entre versiones: cuando se
/// republica el mismo curso corregido, la clave no cambia y la version sube. Es
/// lo que el LMS guarda en cada fila de expediente.
/// </summary>
public sealed record CursoInstalado(
    string CursoRef, string Titulo, string Version, string? Pais, string? Nivel,
    string? Grado, string? Asignatura, string? Idioma, int Lecciones, int Elementos,
    long ActualizadoEn);

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

    /// <summary>
    /// Los cursos ofrecidos a este equipo, con la politica ya aplicada.
    ///
    /// Un curso cuyos elementos estan TODOS desactivados por la politica de la
    /// escuela no aparece. Es la misma regla del catalogo: lo que no llega, no
    /// existe. Si apareciera vacio, el profesor lo abriria y encontraria una
    /// lista en blanco sin ninguna explicacion.
    ///
    /// El titulo sale de formato.json, la ficha en claro del paquete. No esta en
    /// el indice y no se añade: ese archivo existe precisamente para poder leer
    /// de que va un paquete sin licencia y sin descifrar nada.
    /// </summary>
    public IReadOnlyList<CursoInstalado> Cursos()
    {
        // Los elementos disponibles ya vienen filtrados por politica. Se agrupan
        // por paquete para saber cuantos sobreviven de cada curso.
        var porPaquete = Disponibles()
            .GroupBy(e => e.ClavePaquete)
            .ToDictionary(g => g.Key, g => g.ToList());

        var lista = new List<CursoInstalado>();
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = """
            SELECT clave_paquete, version, pais, nivel_clave, grado, asignatura, idioma,
                   ruta_paquete, instalado_en
            FROM m04_paquete_instalado
            WHERE estado='activo'
            ORDER BY nivel_clave, asignatura, clave_paquete
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var clave = r.GetString(0);
            if (!porPaquete.TryGetValue(clave, out var elementos)) continue;   // todo desactivado

            lista.Add(new CursoInstalado(
                CursoRef:     clave,
                Titulo:       TituloDeLaVitrina(r.IsDBNull(7) ? null : r.GetString(7)) ?? clave,
                Version:      r.GetString(1),
                Pais:         r.IsDBNull(2) ? null : r.GetString(2),
                Nivel:        r.IsDBNull(3) ? null : r.GetString(3),
                Grado:        r.IsDBNull(4) ? null : r.GetString(4),
                Asignatura:   r.IsDBNull(5) ? null : r.GetString(5),
                Idioma:       r.IsDBNull(6) ? null : r.GetString(6),
                Lecciones:    elementos.Count(e => e.Tipo == "leccion"),
                Elementos:    elementos.Count,
                ActualizadoEn: r.IsDBNull(8) ? 0 : r.GetInt64(8)));
        }
        return lista;
    }

    /// <summary>
    /// Lee el titulo de formato.json. Devuelve null y no lanza si el paquete ya
    /// no esta en disco: un curso sin titulo se puede seguir mostrando por su
    /// referencia, pero una excepcion aqui dejaria al LMS sin catalogo entero.
    /// </summary>
    private static string? TituloDeLaVitrina(string? rutaPaquete)
    {
        if (string.IsNullOrWhiteSpace(rutaPaquete)) return null;
        try
        {
            var ficha = Path.Combine(rutaPaquete, "formato.json");
            if (!File.Exists(ficha)) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ficha));
            return doc.RootElement.TryGetProperty("vitrina", out var v)
                && v.TryGetProperty("titulo", out var t)
                ? t.GetString()
                : null;
        }
        catch (IOException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Los nodos del arbol curricular que trae un curso concreto.</summary>
    public IReadOnlyList<NodoTaxonomia> TaxonomiaDe(string cursoRef)
    {
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = """
            SELECT t.taxonomia_ref, t.padre_ref, t.tipo_nodo, t.codigo, t.nombre,
                   t.orden, t.pais, t.nivel_clave
            FROM m04_indice_taxonomia t
            JOIN m04_paquete_instalado p ON p.id = t.paquete_id AND p.estado='activo'
            WHERE p.clave_paquete = $c
            ORDER BY t.orden
            """;
        cmd.Parameters.AddWithValue("$c", cursoRef);
        var lista = new List<NodoTaxonomia>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(new NodoTaxonomia(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4),
                r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7)));
        return lista;
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
