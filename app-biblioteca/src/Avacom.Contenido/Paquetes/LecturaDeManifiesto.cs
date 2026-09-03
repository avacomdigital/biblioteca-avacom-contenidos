using System.Security.Cryptography;
using System.Text;

namespace Avacom.Contenido.Paquetes;

public sealed record OpcionDeVoz(string VozRef, string HuellaArchivo, int DuracionMs);

/// <summary>
/// Una pregunta tal como la puede ver la interfaz. Fijate en lo que NO trae:
/// la clave de respuesta. Ese campo existe en el manifiesto y no sale de aqui.
/// </summary>
public sealed record PreguntaVisible(
    string PreguntaRef, int Orden, string Tipo, string Enunciado,
    int Peso, string? Dificultad, string? Retroalimentacion, OpcionDeVoz? Voz);

public sealed record PasoDeLeccion(int Orden, string ItemRef, string? Nota, string? Titulo, string? Tipo);

/// <summary>
/// La cara publica del manifiesto.
///
/// Existe por una razon concreta: el manifiesto tiene la columna clave_respuesta,
/// y si la interfaz tuviera acceso directo a la conexion, tarde o temprano alguien
/// escribiria un SELECT * y las respuestas acabarian en un binding de pantalla, en
/// un log o en un volcado de memoria. Aqui las respuestas solo se pueden comparar,
/// nunca leer, y la comparacion es en tiempo constante.
/// </summary>
public sealed class LecturaDeManifiesto(LectorDePaquete lector)
{
    public IReadOnlyList<PreguntaVisible> Preguntas(string elementoRef)
    {
        var voces = VocesPorPregunta();
        var lista = new List<PreguntaVisible>();
        using var c = lector.Manifiesto.CreateCommand();
        c.CommandText = "SELECT pregunta_ref,orden,tipo,enunciado,peso,dificultad,retroalimentacion " +
                        "FROM p_pregunta WHERE elemento_ref=$e ORDER BY orden";
        c.Parameters.AddWithValue("$e", elementoRef);
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            var pref = r.GetString(0);
            lista.Add(new PreguntaVisible(pref, r.GetInt32(1), r.GetString(2), r.GetString(3),
                r.GetInt32(4), r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                voces.GetValueOrDefault(pref)));
        }
        return lista;
    }

    /// <summary>
    /// Comprueba una respuesta sin revelarla. Comparacion en tiempo constante:
    /// si tardara mas cuando los primeros caracteres coinciden, se podria sacar
    /// la respuesta letra a letra midiendo el tiempo.
    /// </summary>
    public bool Acierta(string preguntaRef, string respuesta)
    {
        using var c = lector.Manifiesto.CreateCommand();
        c.CommandText = "SELECT clave_respuesta FROM p_pregunta WHERE pregunta_ref=$p";
        c.Parameters.AddWithValue("$p", preguntaRef);
        if (c.ExecuteScalar() is not string clave) return false;
        var a = Encoding.UTF8.GetBytes(clave.Trim().ToLowerInvariant());
        var b = Encoding.UTF8.GetBytes(respuesta.Trim().ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public IReadOnlyList<PasoDeLeccion> Leccion(string elementoRef)
    {
        var lista = new List<PasoDeLeccion>();
        using var c = lector.Manifiesto.CreateCommand();
        c.CommandText = """
            SELECT li.orden, li.item_ref, li.nota, e.titulo, e.tipo
            FROM p_leccion_item li
            LEFT JOIN p_elemento e ON e.elemento_ref = li.item_ref
            WHERE li.elemento_ref = $e ORDER BY li.orden
            """;
        c.Parameters.AddWithValue("$e", elementoRef);
        using var r = c.ExecuteReader();
        while (r.Read())
            lista.Add(new PasoDeLeccion(r.GetInt32(0), r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4)));
        return lista;
    }

    /// <summary>La instruccion hablada del elemento entero, si la trae.</summary>
    public OpcionDeVoz? VozDeElemento(string elementoRef)
    {
        using var c = lector.Manifiesto.CreateCommand();
        c.CommandText = "SELECT voz_ref,huella_archivo,duracion_ms FROM p_voz WHERE elemento_ref=$e LIMIT 1";
        c.Parameters.AddWithValue("$e", elementoRef);
        using var r = c.ExecuteReader();
        return r.Read() ? new OpcionDeVoz(r.GetString(0), r.GetString(1), r.GetInt32(2)) : null;
    }

    private Dictionary<string, OpcionDeVoz> VocesPorPregunta()
    {
        var d = new Dictionary<string, OpcionDeVoz>();
        using var c = lector.Manifiesto.CreateCommand();
        c.CommandText = "SELECT pregunta_ref,voz_ref,huella_archivo,duracion_ms FROM p_voz WHERE pregunta_ref IS NOT NULL";
        using var r = c.ExecuteReader();
        while (r.Read())
            d[r.GetString(0)] = new OpcionDeVoz(r.GetString(1), r.GetString(2), r.GetInt32(3));
        return d;
    }
}
