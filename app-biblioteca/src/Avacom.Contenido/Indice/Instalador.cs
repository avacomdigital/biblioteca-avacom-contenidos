using System.Text.Json.Nodes;
using Avacom.Contenido.Cripto;
using Avacom.Contenido.Paquetes;

namespace Avacom.Contenido.Indice;

/// <summary>
/// Las escrituras del componente: instalar un paquete, proyectar su manifiesto
/// al indice, y las politicas del administrador.
///
/// Proyectar es la unica operacion que escribe el indice, y esta escrita para
/// poder ejecutarse dos veces sin dejar duplicados. Eso es lo que permite
/// borrar el indice entero y reconstruirlo: si el resultado no fuera identico,
/// el indice habria dejado de ser una proyeccion y seria una fuente de verdad
/// paralela, que es justo lo que el contrato prohibe.
/// </summary>
public static class Instalador
{
    /// <summary>
    /// Instala un paquete ya verificado y abierto. Devuelve el identificador
    /// que le queda asignado.
    /// </summary>
    public static string Instalar(BaseDeIndice idx, LectorDePaquete lector, string carpeta)
    {
        var pid = YaInstalado(idx, lector.ClavePaquete, lector.Version) ?? Identificador.Nuevo("PQ");
        Proyectar(idx, lector, pid, carpeta);
        return pid;
    }

    /// <summary>Si ya esta esa clave y version, devuelve su id. Reinstalar no duplica.</summary>
    public static string? YaInstalado(BaseDeIndice idx, string clavePaquete, string version)
    {
        using var c = idx.Conexion.CreateCommand();
        c.CommandText = "SELECT id FROM m04_paquete_instalado WHERE clave_paquete=$c AND version=$v";
        c.Parameters.AddWithValue("$c", clavePaquete);
        c.Parameters.AddWithValue("$v", version);
        return c.ExecuteScalar() as string;
    }

    public static void Proyectar(BaseDeIndice idx, LectorDePaquete lector, string pid,
                                 string carpeta, bool soloIndice = false)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using var tx = idx.Conexion.BeginTransaction();

        if (!soloIndice)
        {
            var v = lector.Vitrina;
            using var c = idx.Conexion.CreateCommand();
            c.Transaction = tx;
            c.CommandText = """
                INSERT OR REPLACE INTO m04_paquete_instalado(id,clave_paquete,version,formato_version,origen,
                  pais,nivel_clave,grado,asignatura,idioma,ruta_paquete,huella_manifiesto,
                  firma_verificada,instalado_en,estado,creado_en,secuencia)
                VALUES($id,$cp,$v,2,'avacom',$pa,$ni,$gr,$as,$id2,$ru,$hu,1,$t,'activo',$t,
                  (SELECT COALESCE(MAX(secuencia),0)+1 FROM m04_paquete_instalado))
                """;
            c.Parameters.AddWithValue("$id", pid);
            c.Parameters.AddWithValue("$cp", lector.ClavePaquete);
            c.Parameters.AddWithValue("$v", lector.Version);
            c.Parameters.AddWithValue("$pa", Str(v, "pais"));
            c.Parameters.AddWithValue("$ni", Str(v, "nivel_clave"));
            c.Parameters.AddWithValue("$gr", Str(v, "grado"));
            c.Parameters.AddWithValue("$as", Str(v, "asignatura"));
            c.Parameters.AddWithValue("$id2", Str(v, "idioma"));
            c.Parameters.AddWithValue("$ru", Path.GetFullPath(carpeta));
            c.Parameters.AddWithValue("$hu", lector.Formato["payload_firmado"]!["huella_manifiesto_cifrado"]!.GetValue<string>());
            c.Parameters.AddWithValue("$t", t);
            c.ExecuteNonQuery();
        }

        using (var lee = lector.Manifiesto.CreateCommand())
        {
            lee.CommandText = "SELECT taxonomia_ref,padre_ref,tipo_nodo,codigo,nombre,orden FROM p_taxonomia";
            using var r = lee.ExecuteReader();
            while (r.Read())
            {
                using var w = idx.Conexion.CreateCommand(); w.Transaction = tx;
                w.CommandText = """
                    INSERT OR REPLACE INTO m04_indice_taxonomia(taxonomia_ref,paquete_id,padre_ref,
                      tipo_nodo,codigo,nombre,orden,pais,nivel_clave)
                    VALUES($tr,$p,$pa,$tn,$co,$no,$or,$pai,$niv)
                    """;
                w.Parameters.AddWithValue("$tr", r.GetString(0));
                w.Parameters.AddWithValue("$p", pid);
                w.Parameters.AddWithValue("$pa", r.IsDBNull(1) ? DBNull.Value : r.GetString(1));
                w.Parameters.AddWithValue("$tn", r.GetString(2));
                w.Parameters.AddWithValue("$co", r.IsDBNull(3) ? DBNull.Value : r.GetString(3));
                w.Parameters.AddWithValue("$no", r.GetString(4));
                w.Parameters.AddWithValue("$or", r.GetInt32(5));
                w.Parameters.AddWithValue("$pai", Str(lector.Vitrina, "pais"));
                w.Parameters.AddWithValue("$niv", Str(lector.Vitrina, "nivel_clave"));
                w.ExecuteNonQuery();
            }
        }

        using (var lee = lector.Manifiesto.CreateCommand())
        {
            lee.CommandText = "SELECT elemento_ref,version_elemento,tipo,titulo,taxonomia_ref," +
                              "huella_archivo,duracion_seg,estado,sucesor_ref FROM p_elemento";
            using var r = lee.ExecuteReader();
            while (r.Read())
            {
                using var w = idx.Conexion.CreateCommand(); w.Transaction = tx;
                w.CommandText = """
                    INSERT OR REPLACE INTO m04_indice_elemento(elemento_ref,paquete_id,version_elemento,
                      tipo,titulo,taxonomia_ref,nivel_clave,grado,asignatura,idioma,huella_archivo,
                      duracion_seg,estado,sucesor_ref)
                    VALUES($er,$p,$ve,$ti,$tt,$tx,$ni,$gr,$as,$idi,$hu,$du,$es,$su)
                    """;
                w.Parameters.AddWithValue("$er", r.GetString(0));
                w.Parameters.AddWithValue("$p", pid);
                w.Parameters.AddWithValue("$ve", r.GetString(1));
                w.Parameters.AddWithValue("$ti", r.GetString(2));
                w.Parameters.AddWithValue("$tt", r.GetString(3));
                w.Parameters.AddWithValue("$tx", r.IsDBNull(4) ? DBNull.Value : r.GetString(4));
                w.Parameters.AddWithValue("$ni", Str(lector.Vitrina, "nivel_clave"));
                w.Parameters.AddWithValue("$gr", Str(lector.Vitrina, "grado"));
                w.Parameters.AddWithValue("$as", Str(lector.Vitrina, "asignatura"));
                w.Parameters.AddWithValue("$idi", Str(lector.Vitrina, "idioma"));
                w.Parameters.AddWithValue("$hu", r.IsDBNull(5) ? DBNull.Value : r.GetString(5));
                w.Parameters.AddWithValue("$du", r.IsDBNull(6) ? DBNull.Value : r.GetInt32(6));
                w.Parameters.AddWithValue("$es", r.GetString(7));
                w.Parameters.AddWithValue("$su", r.IsDBNull(8) ? DBNull.Value : r.GetString(8));
                w.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    /// <summary>
    /// Retira un paquete. Se borra del indice y de la lista de instalados, pero
    /// NO se toca el registro de uso: lo que un alumno consulto ocurrio, y sigue
    /// siendo cierto aunque el material ya no este.
    /// </summary>
    public static void Desinstalar(BaseDeIndice idx, string paqueteId)
    {
        using var tx = idx.Conexion.BeginTransaction();
        foreach (var sql in new[]
        {
            "DELETE FROM m04_indice_elemento WHERE paquete_id=$p",
            "DELETE FROM m04_indice_taxonomia WHERE paquete_id=$p",
            "DELETE FROM m04_paquete_instalado WHERE id=$p",
        })
        {
            using var c = idx.Conexion.CreateCommand();
            c.Transaction = tx; c.CommandText = sql;
            c.Parameters.AddWithValue("$p", paqueteId);
            c.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static void Politica(BaseDeIndice idx, string ambito, string valor, string accion,
                                string motivo = "Definido por el administrador")
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using var c = idx.Conexion.CreateCommand();
        c.CommandText = """
            INSERT INTO m04_politica(id,ambito,ambito_valor,accion,motivo,vigente_desde,creado_en,secuencia)
            VALUES($id,$a,$v,$ac,$mo,$t,$t,
              (SELECT COALESCE(MAX(secuencia),0)+1 FROM m04_politica))
            """;
        c.Parameters.AddWithValue("$id", Identificador.Nuevo("PL"));
        c.Parameters.AddWithValue("$a", ambito);
        c.Parameters.AddWithValue("$v", valor);
        c.Parameters.AddWithValue("$ac", accion);
        c.Parameters.AddWithValue("$mo", motivo);
        c.Parameters.AddWithValue("$t", t);
        c.ExecuteNonQuery();
    }

    public static void QuitarPoliticas(BaseDeIndice idx) => idx.Ejecutar("DELETE FROM m04_politica");

    private static object Str(JsonNode v, string clave) =>
        v[clave] is null ? DBNull.Value : v[clave]!.GetValue<string>();
}
