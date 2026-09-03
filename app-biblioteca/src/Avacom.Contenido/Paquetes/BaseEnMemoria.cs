using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Avacom.Contenido.Paquetes;

/// <summary>
/// Abre un manifiesto descifrado y lo deja SOLO en memoria.
///
/// Esto no es un detalle de estilo. El manifiesto lleva las claves de respuesta
/// de todos los reactivos: si quedara en un archivo temporal, un alumno con
/// acceso al equipo se llevaria las respuestas de todos los examenes del año.
///
/// Como se hace, y por que asi:
///
///   El camino ideal seria sqlite3_deserialize, que monta una base entera desde
///   un bloque de memoria. Esa llamada exige memoria no gestionada y reservada
///   por el propio SQLite, y no esta expuesta en Microsoft.Data.Sqlite.
///
///   Lo que se hace en su lugar: se escribe el manifiesto en un archivo temporal
///   con permisos solo para el usuario, se copia a una base en memoria con la
///   interfaz de respaldo de SQLite, y se sobrescribe y borra el temporal de
///   inmediato. La ventana de exposicion son milisegundos, el archivo solo lo
///   puede leer el usuario del servicio, y su contenido se machaca con ceros
///   antes de borrarlo.
///
///   Si algun dia el enlace de SQLitePCLRaw expone deserialize de forma comoda,
///   se sustituye este metodo y desaparece hasta esa ventana. El resto del
///   componente no se entera, porque solo ve la conexion que devuelve.
/// </summary>
public static class BaseEnMemoria
{
    public static SqliteConnection Desde(byte[] baseDeDatos)
    {
        var carpeta = CarpetaSegura();
        var temporal = Path.Combine(carpeta, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + ".tmp");

        var memoria = new SqliteConnection("Data Source=:memory:");
        memoria.Open();

        try
        {
            File.WriteAllBytes(temporal, baseDeDatos);
            SoloParaMi(temporal);

            // Pooling=False NO es opcional, y esta es la linea mas facil de
            // borrar por descuido de todo el archivo.
            //
            // La capa de SQLite reutiliza conexiones por defecto. Con el reparto
            // activado, al salir de este using la conexion vuelve al deposito y
            // el archivo SIGUE ABIERTO. Entonces Machacar no puede abrirlo en
            // exclusiva, salta una excepcion de entrada y salida que se captura
            // mas abajo, y el temporal se queda en el disco con el manifiesto en
            // claro dentro. Es decir: con las claves de respuesta de todos los
            // examenes del año, que es exactamente lo que esta clase existe para
            // evitar. Y sin ningun error visible.
            using (var origen = new SqliteConnection($"Data Source={temporal};Mode=ReadOnly;Pooling=False"))
            {
                origen.Open();
                origen.BackupDatabase(memoria);      // copia pagina a pagina, sin tocar disco de destino
            }
            // Se machaca aqui, y no en un finally, a proposito.
            //
            // Si esto falla en el camino bueno, hay que enterarse: significa que
            // algo dejo el archivo abierto y el manifiesto en claro sigue en el
            // disco. Desde un finally, la excepcion taparia la causa original
            // cuando el fallo viniera de mas arriba, y perderiamos el
            // diagnostico justo el dia que haga falta.
            Machacar(temporal, baseDeDatos.Length);
            return memoria;
        }
        catch
        {
            memoria.Dispose();
            // En el camino malo se limpia sin tapar el error que nos trajo aqui.
            try { Machacar(temporal, baseDeDatos.Length); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>Carpeta propia del proceso, no la temporal compartida del sistema.</summary>
    private static string CarpetaSegura()
    {
        var c = Path.Combine(Path.GetTempPath(), "avacom-" + Environment.ProcessId);
        Directory.CreateDirectory(c);
        SoloParaMi(c);
        return c;
    }

    /// <summary>
    /// Restringe el acceso al usuario que corre el servicio.
    ///
    /// En Windows no hace falta tocar los permisos: la carpeta temporal del
    /// perfil ya hereda una lista de control de acceso que solo deja entrar a
    /// ese usuario y a los administradores. La rama de permisos de estilo Unix
    /// se conserva porque la biblioteca se compila para una plataforma neutra y
    /// las pruebas pueden correr fuera de Windows; en el equipo del aula no se
    /// ejecuta nunca.
    /// </summary>
    private static void SoloParaMi(string ruta)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            File.SetUnixFileMode(ruta,
                Directory.Exists(ruta)
                    ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException) { }
    }

    /// <summary>
    /// Sobrescribe con ceros antes de borrar. Borrar sin mas deja los bytes en
    /// el disco hasta que alguien los pise.
    ///
    /// Si esto falla, tiene que verse. Un temporal que sobrevive con el
    /// manifiesto en claro dentro es la peor forma de fallar de todo el
    /// componente: no rompe nada, no sale ningun error, y deja las claves de
    /// respuesta al alcance de cualquiera con acceso al equipo.
    /// </summary>
    private static void Machacar(string ruta, int largo)
    {
        try
        {
            if (!File.Exists(ruta)) return;
            using (var f = new FileStream(ruta, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                var ceros = new byte[Math.Min(largo, 1024 * 1024)];
                long escrito = 0;
                while (escrito < largo)
                {
                    var n = (int)Math.Min(ceros.Length, largo - escrito);
                    f.Write(ceros, 0, n);
                    escrito += n;
                }
                f.Flush(true);
            }
            File.Delete(ruta);
        }
        catch (IOException e)
        {
            // No se traga en silencio. Si algun dia vuelve a quedarse un
            // archivo abierto, esto es lo unico que lo delata.
            throw new IOException(
                "No se pudo borrar el manifiesto temporal. Queda contenido en claro en disco: " + ruta, e);
        }
    }
}
