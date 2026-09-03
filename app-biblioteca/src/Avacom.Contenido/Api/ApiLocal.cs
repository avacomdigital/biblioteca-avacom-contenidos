using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avacom.Contenido.Indice;

namespace Avacom.Contenido.Api;

/// <summary>
/// La puerta por la que el LMS pregunta que contenido hay.
///
/// POR QUE UNA API Y NO LEER LA BASE DIRECTAMENTE
///
/// Lo facil seria que el LMS abriera indice.db y consultara. Y seria un error,
/// por dos motivos que se pagan a los seis meses:
///
///   El LMS quedaria atado al esquema interno de este componente. Renombrar una
///   columna aqui romperia el LMS alli, y ninguno de los dos equipos sabria por
///   que hasta que un aula se quedara sin catalogo.
///
///   El indice es una proyeccion reconstruible. Mientras se reconstruye esta a
///   medias. Un lector externo veria un catalogo incompleto y lo creeria bueno.
///
/// Con una API, el contrato es la forma del JSON, no la forma de las tablas.
/// Los dos equipos publican versiones sin coordinarse.
///
/// QUE PUEDE Y QUE NO PUEDE HACER EL LMS
///
///   Leer: el catalogo, el arbol curricular y un elemento suelto.
///   Pedir: que se muestre un material en la pantalla.
///   Nada mas. No puede instalar, ni desinstalar, ni cambiar politicas, ni
///   tocar el indice. Todo eso es del administrador y se hace en la aplicacion.
///
/// LO QUE LO PROTEGE
///
///   Escucha solo en 127.0.0.1. Ninguna tableta del aula lo alcanza.
///   El puerto lo elige el sistema, distinto en cada arranque.
///   Cada peticion tiene que traer la ficha del equipo. Sin ella, 401.
///   Solo GET y un POST. No hay forma de escribir nada.
/// </summary>
public sealed class ApiLocal : IDisposable
{
    private readonly BaseDeIndice _indice;
    private readonly Func<string, string?> _mostrar;
    private readonly TcpListener _escucha;
    private readonly CancellationTokenSource _alto = new();
    private readonly string _ficha;
    private readonly bool _publicoEnlace;

    public int Puerto { get; }
    public string Base => $"http://127.0.0.1:{Puerto}";

    /// <summary>La ficha, para las pruebas. En produccion la lee el LMS del punto de enlace.</summary>
    public string Ficha => _ficha;

    /// <summary>
    /// <paramref name="mostrar"/> recibe una referencia de elemento y devuelve
    /// null si se mostro, o el motivo si no se pudo. Se inyecta porque mostrar
    /// algo en pantalla es cosa de la aplicacion, no de la biblioteca: aqui no
    /// sabemos que hay una pantalla.
    /// </summary>
    public ApiLocal(BaseDeIndice indice, Func<string, string?> mostrar, bool publicarEnlace = true)
    {
        _indice = indice;
        _mostrar = mostrar;

        _escucha = new TcpListener(IPAddress.Loopback, 0);
        _escucha.Start();
        Puerto = ((IPEndPoint)_escucha.LocalEndpoint).Port;

        // En las pruebas no se publica la nota: si se publicara, cada prueba
        // pisaria el punto de enlace del componente que este corriendo de verdad
        // en esa maquina, y el LMS acabaria llamando a un puerto que ya murio.
        _publicoEnlace = publicarEnlace;
        _ficha = publicarEnlace
            ? PuntoDeEnlace.Publicar(Puerto)
            : Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        _ = Task.Run(Atender);
    }

    // ------------------------------------------------------------------ red

    private async Task Atender()
    {
        while (!_alto.IsCancellationRequested)
        {
            TcpClient cliente;
            try { cliente = await _escucha.AcceptTcpClientAsync(_alto.Token); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { return; }
            _ = Task.Run(() => Servir(cliente));
        }
    }

    private void Servir(TcpClient cliente)
    {
        try
        {
            using (cliente)
            using (var red = cliente.GetStream())
            {
                var p = LeerPeticion(red);
                if (p is null) return;
                var (metodo, ruta, ficha, cuerpo) = p.Value;

                // Comparacion en tiempo constante: si tardara mas cuando los
                // primeros caracteres coinciden, se podria adivinar la ficha
                // midiendo el tiempo de respuesta.
                if (!FichaValida(ficha)) { Responder(red, 401, """{"error":"ficha no valida"}"""); return; }

                var (codigo, json) = Despachar(metodo, ruta, cuerpo);
                Responder(red, codigo, json);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private bool FichaValida(string? recibida)
    {
        if (recibida is null) return false;
        var a = Encoding.ASCII.GetBytes(_ficha);
        var b = Encoding.ASCII.GetBytes(recibida);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    // ------------------------------------------------------------- despacho

    private (int, string) Despachar(string metodo, string ruta, string cuerpo)
    {
        var (camino, consulta) = Partir(ruta);

        if (metodo == "GET" && camino == "/v1/salud")      return (200, Salud());
        if (metodo == "GET" && camino == "/v1/catalogo")   return (200, Catalogo(consulta));
        if (metodo == "GET" && camino == "/v1/taxonomia")  return (200, Taxonomia(consulta));
        if (metodo == "GET" && camino.StartsWith("/v1/elemento/", StringComparison.Ordinal))
            return Elemento(Uri.UnescapeDataString(camino["/v1/elemento/".Length..]));
        if (metodo == "POST" && camino == "/v1/mostrar")   return Mostrar(cuerpo);

        return (404, """{"error":"no existe ese punto de enlace"}""");
    }

    private string Salud()
    {
        var d = _indice.Disponibles();
        return Json(new
        {
            componente = "avacom-contenido",
            contrato = PuntoDeEnlace.Contrato,
            elementos = d.Count,
            paquetes = Escalar("SELECT count(*) FROM m04_paquete_instalado WHERE estado='activo'"),
            politicas = Escalar("SELECT count(*) FROM m04_politica WHERE accion='deshabilitar'"),
            huella_catalogo = HuellaDeCatalogo(d),
        });
    }

    /// <summary>
    /// La huella de lo que el LMS veria AHORA MISMO si pidiera el catalogo.
    ///
    /// POR QUE EXISTE
    ///
    /// Al LMS se le pide que no guarde un catalogo propio, pero sin una señal de
    /// cambio la unica alternativa es recargarlo entero cada pocos segundos, que
    /// es caro, o cachearlo y quedarse desactualizado, que es peor: el profesor
    /// ofrece a la clase un material que el administrador acaba de retirar.
    ///
    /// Con esto el LMS pide /v1/salud, que son doscientos bytes, y solo recarga
    /// el catalogo cuando la huella cambia.
    ///
    /// POR QUE ES UNA HUELLA Y NO UN CONTADOR
    ///
    /// Un contador hay que acordarse de incrementarlo en cada sitio que toca el
    /// catalogo: instalar, desinstalar, cambiar politica, reconstruir. El dia
    /// que alguien añada una via nueva y olvide incrementarlo, el LMS se queda
    /// desactualizado y nadie se entera hasta que pasa en un aula.
    ///
    /// Esta huella se DERIVA del catalogo, asi que no hay nada que recordar
    /// actualizar: si lo que el LMS veria cambio, la huella cambio. Es la misma
    /// idea que gobierna el indice entero, que es una proyeccion reconstruible y
    /// no una fuente de verdad paralela.
    ///
    /// Se calcula sobre la lista YA FILTRADA por la politica, que es justo lo
    /// que el LMS puede ver. Desactivar una asignatura cambia la huella aunque
    /// no se haya desinstalado nada.
    /// </summary>
    private static string HuellaDeCatalogo(IReadOnlyList<ElementoIndexado> disponibles)
    {
        var sb = new StringBuilder();
        foreach (var e in disponibles.OrderBy(x => x.ElementoRef, StringComparer.Ordinal))
            sb.Append(e.ElementoRef).Append('|').Append(e.VersionElemento).Append('|')
              .Append(e.Estado).Append('\n');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        // Diecisseis caracteres bastan para comparar por igualdad, que es lo
        // unico que el LMS hace con esto.
        return Convert.ToHexStringLower(bytes)[..16];
    }

    private string Catalogo(Dictionary<string, string> q)
    {
        // Disponibles ya aplica la politica del administrador. El LMS NUNCA ve
        // lo que la escuela desactivo: no le llega atenuado ni con una marca,
        // simplemente no esta. Si el LMS pudiera verlo, acabaria mostrandolo.
        var lista = _indice.Disponibles(
            q.GetValueOrDefault("nivel"),
            q.GetValueOrDefault("asignatura"),
            q.GetValueOrDefault("tipo"));

        var grado = q.GetValueOrDefault("grado");
        var taxonomia = q.GetValueOrDefault("taxonomia_ref");

        var filtrada = lista
            .Where(e => grado is null || e.Grado == grado)
            .Where(e => taxonomia is null || e.TaxonomiaRef == taxonomia)
            .Select(Vista);

        return Json(new { elementos = filtrada });
    }

    private string Taxonomia(Dictionary<string, string> q)
    {
        // Sin padre devuelve la raiz. Asi el LMS recorre el arbol de arriba
        // abajo sin saber de antemano cuantos niveles tiene, que es lo que
        // permite que preescolar y secundaria tengan formas distintas.
        var nodos = _indice.Taxonomia(q.GetValueOrDefault("padre")).Select(n => new
        {
            @ref = n.TaxonomiaRef,
            padre = n.PadreRef,
            tipo = n.TipoNodo,
            codigo = n.Codigo,
            nombre = n.Nombre,
            orden = n.Orden,
            pais = n.Pais,
            nivel = n.Nivel,
        });
        return Json(new { nodos });
    }

    private (int, string) Elemento(string elementoRef)
    {
        var e = _indice.Elemento(elementoRef);
        if (e is null) return (404, """{"error":"no esta en el indice"}""");

        // Se comprueba la politica tambien aqui. Sin esto, el LMS podria pedir
        // por referencia directa algo que el catalogo le oculta.
        if (!_indice.Politica.Permite(e))
            return (403, """{"error":"la politica de esta instalacion no lo permite"}""");

        return (200, Json(Vista(e)));
    }

    private (int, string) Mostrar(string cuerpo)
    {
        string? elementoRef;
        try
        {
            using var d = JsonDocument.Parse(string.IsNullOrWhiteSpace(cuerpo) ? "{}" : cuerpo);
            elementoRef = d.RootElement.TryGetProperty("elemento_ref", out var v) ? v.GetString() : null;
        }
        catch (JsonException) { return (400, """{"error":"el cuerpo no es JSON valido"}"""); }

        if (string.IsNullOrWhiteSpace(elementoRef))
            return (400, """{"error":"falta elemento_ref"}""");

        var motivo = _mostrar(elementoRef);
        return motivo is null
            ? (200, """{"aceptado":true}""")
            : (409, Json(new { aceptado = false, motivo }));
    }

    /// <summary>
    /// Lo que ve el LMS de un elemento.
    ///
    /// Fijate en lo que NO sale: la ruta del paquete en disco ni el
    /// identificador interno de instalacion. El LMS no tiene por que saber
    /// donde vive un archivo, y si lo supiera acabaria abriendolo por su cuenta
    /// saltandose el cifrado y la politica.
    /// </summary>
    private static object Vista(ElementoIndexado e) => new
    {
        @ref = e.ElementoRef,
        tipo = e.Tipo,
        titulo = e.Titulo,
        nivel = e.Nivel,
        grado = e.Grado,
        asignatura = e.Asignatura,
        idioma = e.Idioma,
        taxonomia_ref = e.TaxonomiaRef,
        version = e.VersionElemento,
        duracion_seg = e.DuracionSeg,
        paquete = e.ClavePaquete,
        huella = e.HuellaArchivo,
    };

    private long Escalar(string sql)
    {
        using var cmd = _indice.Conexion.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static string Json(object o) =>
        JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = false });

    // -------------------------------------------------------------- ayudas

    private static (string camino, Dictionary<string, string> consulta) Partir(string ruta)
    {
        var i = ruta.IndexOf('?', StringComparison.Ordinal);
        if (i < 0) return (ruta, new Dictionary<string, string>());

        var q = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var par in ruta[(i + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var j = par.IndexOf('=', StringComparison.Ordinal);
            if (j <= 0) continue;
            var valor = Uri.UnescapeDataString(par[(j + 1)..]);
            if (valor.Length > 0) q[Uri.UnescapeDataString(par[..j])] = valor;
        }
        return (ruta[..i], q);
    }

    private static (string metodo, string ruta, string? ficha, string cuerpo)? LeerPeticion(NetworkStream red)
    {
        var cabecera = new StringBuilder();
        var uno = new byte[1];
        while (!cabecera.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (red.Read(uno, 0, 1) <= 0) return null;
            cabecera.Append((char)uno[0]);
            if (cabecera.Length > 16384) return null;      // nada legitimo pide tanto
        }

        var lineas = cabecera.ToString().Split("\r\n");
        var partes = lineas[0].Split(' ');
        if (partes.Length < 2) return null;

        string? ficha = null;
        int largo = 0;
        foreach (var l in lineas.Skip(1))
        {
            if (l.StartsWith("X-Avacom-Ficha:", StringComparison.OrdinalIgnoreCase))
                ficha = l[15..].Trim();
            else if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(l[15..].Trim(), out largo);
        }

        var cuerpo = "";
        if (largo is > 0 and <= 65536)
        {
            var buf = new byte[largo];
            int leidos = 0;
            while (leidos < largo)
            {
                var n = red.Read(buf, leidos, largo - leidos);
                if (n <= 0) break;
                leidos += n;
            }
            cuerpo = Encoding.UTF8.GetString(buf, 0, leidos);
        }

        return (partes[0], partes[1], ficha, cuerpo);
    }

    private static void Responder(NetworkStream red, int codigo, string json)
    {
        var razon = codigo switch
        {
            200 => "OK", 400 => "Bad Request", 401 => "Unauthorized",
            403 => "Forbidden", 404 => "Not Found", 409 => "Conflict", _ => "OK",
        };
        var cuerpo = Encoding.UTF8.GetBytes(json);
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {codigo} {razon}\r\n");
        sb.Append("Content-Type: application/json; charset=utf-8\r\n");
        sb.Append($"Content-Length: {cuerpo.Length}\r\n");
        sb.Append("Cache-Control: no-store\r\n");
        sb.Append("Connection: close\r\n\r\n");
        var enc = Encoding.ASCII.GetBytes(sb.ToString());
        red.Write(enc, 0, enc.Length);
        red.Write(cuerpo, 0, cuerpo.Length);
    }

    public void Dispose()
    {
        _alto.Cancel();
        if (_publicoEnlace) PuntoDeEnlace.Retirar();
        try { _escucha.Stop(); } catch (SocketException) { }
        _alto.Dispose();
    }
}
