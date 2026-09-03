using Avacom.Contenido.Cripto;
using Microsoft.Data.Sqlite;

namespace Avacom.Contenido.Uso;

/// <summary>
/// Lo único que deja el modo repaso: qué se abrió, cuánto tiempo y hasta dónde.
///
/// NUNCA crea intentos ni calificaciones. Si algún día alguien añade aquí una
/// escritura hacia el libro de calificaciones, está rompiendo DEC de repaso y
/// convirtiendo el espacio propio del alumno en una evaluación encubierta.
/// </summary>
public sealed class RegistroDeUso(SqliteConnection cn)
{
    public string AbrirSesion(string? personaId = null, string? dispositivoId = null)
    {
        var id = Identificador.Nuevo("RS");
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO m08_repaso_sesion(id,persona_id,dispositivo_id,iniciada_en,creado_en,secuencia)
            VALUES($id,$p,$d,$t,$t,(SELECT COALESCE(MAX(secuencia),0)+1 FROM m08_repaso_sesion))
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$p", (object?)personaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", (object?)dispositivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", Ahora);
        cmd.ExecuteNonQuery();
        return id;
    }

    public string RegistrarApertura(string sesionId, string elementoRef, string version)
    {
        var id = Identificador.Nuevo("RC");
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO m08_repaso_consumo(id,repaso_sesion_id,elemento_ref,version_elemento,abierto_en,creado_en,secuencia)
            VALUES($id,$s,$e,$v,$t,$t,(SELECT COALESCE(MAX(secuencia),0)+1 FROM m08_repaso_consumo))
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$s", sesionId);
        cmd.Parameters.AddWithValue("$e", elementoRef);
        cmd.Parameters.AddWithValue("$v", version);
        cmd.Parameters.AddWithValue("$t", Ahora);
        cmd.ExecuteNonQuery();
        return id;
    }

    public void RegistrarCierre(string consumoId, int? progresoPct = null)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            UPDATE m08_repaso_consumo
               SET cerrado_en = $t,
                   segundos = ($t - abierto_en) / 1000,
                   progreso_pct = COALESCE($pct, progreso_pct)
             WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", consumoId);
        cmd.Parameters.AddWithValue("$t", Ahora);
        cmd.Parameters.AddWithValue("$pct", (object?)progresoPct ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static long Ahora => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
