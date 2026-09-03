using Avacom.Contenido.Cripto;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Paquetes;

namespace Avacom.Biblioteca.App;

public sealed record ResultadoInstalacion(string Carpeta, bool Aceptado, string Detalle);

/// <summary>
/// Lo que este equipo es y lo que este equipo puede abrir.
///
/// Un nodo tiene tres cosas: su clave privada, que no sale nunca de aqui; la
/// licencia que le emitieron, que le da las claves de los paquetes que puede
/// abrir; y el indice, que es la proyeccion de lo que tiene instalado.
///
/// La carpeta de trabajo se recuerda entre arranques. En el aula esto lo deja
/// puesto el instalador y el profesor no lo ve nunca; aqui es visible porque
/// durante el desarrollo hay que poder cambiarla.
/// </summary>
public sealed class EstadoDelNodo(BaseDeIndice indice, GestorDePaquetes gestor)
{
    private const string ClavePreferencia = "avacom.carpeta_trabajo";

    public string? Carpeta { get; private set; } = Preferences.Default.Get<string?>(ClavePreferencia, null);
    public Licencia? Licencia { get; private set; }
    public string? Problema { get; private set; }

    public bool Listo => Licencia is not null && Problema is null;

    public string Resumen => Problema
        ?? (Licencia is null
            ? "Todavia no se ha cargado la licencia de este equipo."
            : $"Licencia valida hasta {DateTimeOffset.FromUnixTimeMilliseconds(Licencia.VenceEn):yyyy-MM-dd} · "
              + $"{Licencia.PaquetesAutorizados.Count()} paquetes autorizados");

    /// <summary>
    /// Carga licencia y clave privada desde una carpeta de trabajo. Devuelve
    /// false y deja el motivo en Problema; nunca lanza, porque esto lo llama la
    /// pantalla al arrancar y un fallo aqui no puede tumbar la aplicacion.
    /// </summary>
    public bool Cargar(string carpeta)
    {
        Problema = null;
        Licencia = null;
        try
        {
            var rutaLic = Path.Combine(carpeta, "lic", "licencia.json");
            var rutaPriv = Path.Combine(carpeta, "nodo", "nodo_privada.bin");

            if (!File.Exists(rutaLic)) { Problema = "No hay lic/licencia.json en esa carpeta."; return false; }
            if (!File.Exists(rutaPriv)) { Problema = "No hay nodo/nodo_privada.bin en esa carpeta."; return false; }

            // se escribe el nombre completo a proposito: en esta clase hay una
            // propiedad que se llama igual que el tipo
            var lic = Avacom.Contenido.Cripto.Licencia.Cargar(rutaLic);
            if (!lic.Verificar()) { Problema = "La licencia no esta firmada por un emisor que reconozcamos."; return false; }
            if (!lic.Vigente) { Problema = "La licencia esta vencida."; return false; }

            AsegurarEsquema(carpeta);

            Licencia = lic;
            gestor.Licencia = lic;
            gestor.NodoPrivada = File.ReadAllBytes(rutaPriv);

            Carpeta = carpeta;
            Preferences.Default.Set(ClavePreferencia, carpeta);
            return true;
        }
        catch (Exception e)
        {
            Problema = $"No se pudo cargar: {e.Message}";
            return false;
        }
    }

    /// <summary>
    /// Recorre pub/ e instala lo que verifique. Un paquete que falla no detiene
    /// a los demas: en un aula, que un material venga corrupto no puede dejar al
    /// profesor sin los otros veinte.
    /// </summary>
    public IReadOnlyList<ResultadoInstalacion> InstalarDesde(string carpetaPub)
    {
        var salida = new List<ResultadoInstalacion>();
        if (!Directory.Exists(carpetaPub)) return salida;
        if (Licencia is null || gestor.NodoPrivada is null)
        {
            salida.Add(new ResultadoInstalacion(carpetaPub, false, "Falta cargar la licencia de este equipo."));
            return salida;
        }

        foreach (var c in Directory.GetDirectories(carpetaPub).OrderBy(x => x))
        {
            var nombre = Path.GetFileName(c);
            if (!File.Exists(Path.Combine(c, "formato.json"))) continue;
            try
            {
                using var lector = new LectorDePaquete(c);

                var v = lector.Verificar(formatoSoportado: 2);
                if (!v.Aceptado) { salida.Add(new ResultadoInstalacion(nombre, false, string.Join(" ", v.Motivos))); continue; }

                var a = lector.Abrir(Licencia, gestor.NodoPrivada);
                if (!a.Aceptado) { salida.Add(new ResultadoInstalacion(nombre, false, string.Join(" ", a.Motivos))); continue; }

                Instalador.Instalar(indice, lector, c);
                salida.Add(new ResultadoInstalacion(nombre, true,
                    $"{lector.Vitrina["titulo"]} · version {lector.Version}"));
            }
            catch (Exception e)
            {
                salida.Add(new ResultadoInstalacion(nombre, false, e.Message));
            }
        }
        return salida;
    }

    /// <summary>
    /// Aplica el esquema del componente si el indice esta vacio.
    ///
    /// OJO: el guion NO es idempotente. Sus CREATE TABLE no llevan IF NOT
    /// EXISTS, asi que ejecutarlo dos veces falla con "table already exists".
    /// Lo unico que lo evita es la comprobacion de aqui abajo.
    ///
    /// Si se queda a medias, el indice queda inservible y el siguiente arranque
    /// da un error que no dice nada. Por eso se comprueba al final que estan
    /// todas las tablas, y si no, se dice que hay que borrar indice.db.
    /// </summary>
    private void AsegurarEsquema(string carpeta)
    {
        using var c = indice.Conexion.CreateCommand();
        c.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='m04_indice_elemento'";
        if (Convert.ToInt64(c.ExecuteScalar()) > 0) return;

        // Un solo guion, el del componente. Antes se cargaban tambien los del
        // LMS, casi dos mil lineas y noventa y dos tablas ajenas, y todo por una
        // clave foranea hacia la tabla de personas que este componente ni
        // siquiera necesita: aqui se puede consultar sin identificarse.
        var guion = Path.Combine(carpeta, "esquema", "contenido.sql");
        if (!File.Exists(guion))
            throw new FileNotFoundException(
                "Falta el esquema del componente. Deberia estar en " + guion, guion);
        indice.Crear(guion);

        // Se comprueba que quedo entero. Un esquema a medias es peor que
        // ninguno: la aplicacion arranca, parece que va, y falla al primer
        // material que se abra.
        using var v = indice.Conexion.CreateCommand();
        v.CommandText = """
            SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN
              ('m04_paquete_instalado','m04_indice_elemento','m04_indice_taxonomia',
               'm04_politica','m08_repaso_sesion','m08_repaso_consumo')
            """;
        if (Convert.ToInt64(v.ExecuteScalar()) < 6)
            throw new InvalidOperationException(
                "El esquema quedo incompleto. Cierra la aplicacion, borra el archivo indice.db y vuelve a intentarlo.");
    }
}
