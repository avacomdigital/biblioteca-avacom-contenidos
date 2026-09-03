using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Avacom.Contenido.Cripto;

/// <summary>
/// Serialización canónica de JSON: claves ordenadas por orden de bytes y sin
/// espacios. Tiene que coincidir byte a byte con json.dumps(sort_keys=True,
/// separators=(",",":")) de Python, o ninguna firma valida jamás.
///
/// Es el punto donde más fallan estas integraciones, así que hay una prueba
/// dedicada en Avacom.Contenido.Tests que compara contra vectores generados
/// por la implementación de referencia.
/// </summary>
public static class Canonico
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static byte[] Serializar(JsonNode nodo) =>
        Encoding.UTF8.GetBytes(Ordenar(nodo).ToJsonString(Opciones));

    private static JsonNode Ordenar(JsonNode n) => n switch
    {
        JsonObject o => OrdenarObjeto(o),
        JsonArray a => OrdenarArreglo(a),
        _ => n.DeepClone()
    };

    private static JsonObject OrdenarObjeto(JsonObject o)
    {
        var r = new JsonObject();
        foreach (var k in o.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
            r[k] = o[k] is null ? null : Ordenar(o[k]!.DeepClone());
        return r;
    }

    private static JsonArray OrdenarArreglo(JsonArray a)
    {
        var r = new JsonArray();
        foreach (var x in a) r.Add(x is null ? null : Ordenar(x.DeepClone()));
        return r;
    }
}
