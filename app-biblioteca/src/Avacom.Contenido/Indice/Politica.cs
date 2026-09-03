using Microsoft.Data.Sqlite;

namespace Avacom.Contenido.Indice;

/// <summary>
/// La consola del administrador, del lado de la lógica. Se aplica ENCIMA del
/// índice sin modificarlo, para que una actualización de contenido nunca pise
/// una decisión de la escuela.
///
/// Seis ámbitos, de más grueso a más fino: paquete, nivel, grado, asignatura,
/// rama de la taxonomía y elemento concreto.
/// </summary>
public sealed class Politica(SqliteConnection cn)
{
    public bool Permite(ElementoIndexado e)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT count(*) FROM m04_politica p
            WHERE p.accion = 'deshabilitar'
              AND (p.vigente_hasta IS NULL OR p.vigente_hasta > $ahora)
              AND ( (p.ambito='elemento'   AND p.ambito_valor = $ref)
                 OR (p.ambito='paquete'    AND p.ambito_valor = $paq)
                 OR (p.ambito='nivel'      AND p.ambito_valor = $niv)
                 OR (p.ambito='grado'      AND p.ambito_valor = $gra)
                 OR (p.ambito='asignatura' AND p.ambito_valor = $asi)
                 OR (p.ambito='taxonomia'  AND p.ambito_valor = $tax) )
            """;
        cmd.Parameters.AddWithValue("$ahora", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$ref", e.ElementoRef);
        cmd.Parameters.AddWithValue("$paq", (object?)e.ClavePaquete ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$niv", (object?)e.Nivel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$gra", (object?)e.Grado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$asi", (object?)e.Asignatura ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tax", (object?)e.TaxonomiaRef ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar()) == 0;
    }

    public string? VersionFijada(string clavePaquete)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT version_fijada FROM m04_politica WHERE accion='fijar_version' AND ambito='paquete' AND ambito_valor=$p LIMIT 1";
        cmd.Parameters.AddWithValue("$p", clavePaquete);
        return cmd.ExecuteScalar() as string;
    }
}
