using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Avacom.Contenido.Medios;

/// <summary>
/// Un servidor diminuto que solo escucha en el propio equipo.
///
/// POR QUE EXISTE
///
/// El reproductor de video y el componente de navegacion no saben leer de un
/// flujo de bytes: quieren una ruta o una direccion. Las dos salidas obvias son
/// malas. Escribir el video descifrado en un archivo temporal deja el contenido
/// en claro en el disco, que es exactamente lo que el cifrado venia a evitar.
/// Cargarlo entero en memoria no sirve: un video de clase pesa cientos de
/// megabytes y la pantalla tiene que reproducirlo mientras lo descifra.
///
/// La salida es esta. Se entrega una direccion, el reproductor pide trozos por
/// rango, y cada trozo se descifra en el momento y se manda. En disco nunca hay
/// nada en claro, y adelantar un video no obliga a descifrar lo anterior.
///
/// LO QUE LO PROTEGE
///
///   Escucha solo en 127.0.0.1. Ninguna tableta del aula lo alcanza.
///   El puerto lo elige el sistema, distinto en cada arranque.
///   Cada material se sirve bajo una ficha aleatoria de 128 bits que se anula al
///   cerrar el visor. Sin la ficha exacta responde 404, sin decir por que.
///   Solo entiende GET y HEAD. No hay nada que escribir.
///
/// Es deliberadamente pequeño y sin dependencias: cuanto menos codigo escuche en
/// un puerto, menos superficie hay que revisar.
/// </summary>
public sealed class ServidorDeMedios : IDisposable
{
    private sealed record Entrada(Func<Stream> Abrir, string TipoContenido);

    private readonly TcpListener _escucha;
    private readonly ConcurrentDictionary<string, Entrada> _fichas = new();
    private readonly CancellationTokenSource _alto = new();

    public int Puerto { get; }
    public string Base => $"http://127.0.0.1:{Puerto}";

    public ServidorDeMedios()
    {
        _escucha = new TcpListener(IPAddress.Loopback, 0);      // 0 = el sistema elige
        _escucha.Start();
        Puerto = ((IPEndPoint)_escucha.LocalEndpoint).Port;
        _ = Task.Run(Atender);
    }

    /// <summary>Publica un material y devuelve su direccion. La ficha es de un solo uso logico.</summary>
    public string Publicar(Func<Stream> abrir, string tipoContenido)
    {
        var ficha = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _fichas[ficha] = new Entrada(abrir, tipoContenido);
        return $"{Base}/m/{ficha}";
    }

    /// <summary>
    /// Publica el contenido de un paquete interactivo comprimido. Cada archivo de
    /// dentro recibe su propia ficha, y se devuelve la del punto de entrada.
    /// </summary>
    public string PublicarComprimido(Stream zip, string entrada = "index.html")
    {
        var raiz = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        using var archivo = new ZipArchive(zip, ZipArchiveMode.Read);
        foreach (var e in archivo.Entries)
        {
            if (e.FullName.EndsWith('/')) continue;
            // se lee a memoria: un interactivo son kilobytes, no un video
            using var s = e.Open();
            var ms = new MemoryStream();
            s.CopyTo(ms);
            var datos = ms.ToArray();
            _fichas[$"{raiz}/{e.FullName}"] =
                new Entrada(() => new MemoryStream(datos, writable: false),
                            ResolutorDeMedios.TipoDeContenido(e.FullName));
        }
        return $"{Base}/m/{raiz}/{entrada}";
    }

    /// <summary>Anula una ficha. Se llama al cerrar el visor.</summary>
    public void Retirar(string direccion)
    {
        var i = direccion.IndexOf("/m/", StringComparison.Ordinal);
        if (i < 0) return;
        var ficha = direccion[(i + 3)..];
        _fichas.TryRemove(ficha, out _);
        var raiz = ficha.Split('/')[0];
        foreach (var k in _fichas.Keys.Where(k => k.StartsWith(raiz + "/", StringComparison.Ordinal)))
            _fichas.TryRemove(k, out _);
    }

    // ------------------------------------------------------------------ red

    private async Task Atender()
    {
        while (!_alto.IsCancellationRequested)
        {
            TcpClient cliente;
            try { cliente = await _escucha.AcceptTcpClientAsync(_alto.Token); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }   // se cerro mientras esperabamos
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
                var peticion = LeerPeticion(red);
                if (peticion is null) return;
                var (metodo, ruta, desde, hasta) = peticion.Value;

                if (metodo is not ("GET" or "HEAD")) { Cabecera(red, 405, "text/plain", 0); return; }
                if (!ruta.StartsWith("/m/", StringComparison.Ordinal)) { Cabecera(red, 404, "text/plain", 0); return; }

                // Se quita la cadena de consulta. Los interactivos suelen pedir
                // sus recursos con algo como estilo.css?v=2 para saltarse la
                // cache del navegador, y sin esto no encontrarian nada.
                var interrogante = ruta.IndexOf('?', StringComparison.Ordinal);
                if (interrogante >= 0) ruta = ruta[..interrogante];

                if (!_fichas.TryGetValue(Uri.UnescapeDataString(ruta[3..]), out var e))
                {
                    Cabecera(red, 404, "text/plain", 0);
                    return;
                }

                using var flujo = e.Abrir();
                var largo = flujo.Length;
                var sufijo = desde < 0;
                if (sufijo) desde = Math.Max(0, largo + desde);
                var fin = hasta ?? largo - 1;
                if (desde >= largo) { Cabecera(red, 416, e.TipoContenido, 0, largo: largo); return; }
                if (fin >= largo) fin = largo - 1;
                var cuantos = fin - desde + 1;

                var parcial = hasta is not null || desde > 0 || sufijo;
                Cabecera(red, parcial ? 206 : 200, e.TipoContenido, cuantos,
                         desde: parcial ? desde : null, fin: parcial ? fin : null, largo: largo);
                if (metodo == "HEAD") return;

                flujo.Seek(desde, SeekOrigin.Begin);
                var buf = new byte[64 * 1024];
                long faltan = cuantos;
                while (faltan > 0)
                {
                    var n = flujo.Read(buf, 0, (int)Math.Min(buf.Length, faltan));
                    if (n <= 0) break;
                    red.Write(buf, 0, n);
                    faltan -= n;
                }
            }
        }
        catch (IOException) { /* el reproductor corto la conexion al adelantar; es normal */ }
        catch (ObjectDisposedException) { }
    }

    private static (string metodo, string ruta, long desde, long? hasta)? LeerPeticion(NetworkStream red)
    {
        var texto = new StringBuilder();
        var uno = new byte[1];
        while (!texto.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (red.Read(uno, 0, 1) <= 0) return null;
            texto.Append((char)uno[0]);
            if (texto.Length > 8192) return null;              // nada legitimo pide tanto
        }
        var lineas = texto.ToString().Split("\r\n");
        var partes = lineas[0].Split(' ');
        if (partes.Length < 2) return null;

        long desde = 0; long? hasta = null;
        foreach (var l in lineas.Skip(1))
        {
            if (!l.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) continue;
            var v = l[6..].Trim();
            if (!v.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) continue;
            var r = v[6..].Split('-');
            if (r.Length == 2)
            {
                if (r[0].Length == 0)
                {
                    // "bytes=-500" son los ULTIMOS 500 bytes, no los primeros.
                    // Es justo lo que pide un reproductor para leer el indice de
                    // un MP4 que lo trae al final del archivo. Servirle el
                    // principio hace que el video no arranque nunca.
                    // Se marca con un desde negativo y se resuelve al servir,
                    // que es donde ya se conoce el tamaño real.
                    if (long.TryParse(r[1], out var ultimos)) desde = -ultimos;
                }
                else
                {
                    if (long.TryParse(r[0], out var d)) desde = d;
                    if (long.TryParse(r[1], out var h)) hasta = h;
                }
            }
            break;
        }
        return (partes[0], partes[1], desde, hasta);
    }

    private static void Cabecera(NetworkStream red, int codigo, string tipo, long cuantos,
                                 long? desde = null, long? fin = null, long? largo = null)
    {
        var razon = codigo switch
        {
            200 => "OK", 206 => "Partial Content", 404 => "Not Found",
            405 => "Method Not Allowed", 416 => "Range Not Satisfiable", _ => "OK",
        };
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {codigo} {razon}\r\n");
        sb.Append($"Content-Type: {tipo}\r\n");
        sb.Append($"Content-Length: {cuantos}\r\n");
        sb.Append("Accept-Ranges: bytes\r\n");
        // el contenido no se guarda en ninguna cache: cuando el visor se cierra,
        // no debe quedar rastro reutilizable
        sb.Append("Cache-Control: no-store\r\n");
        if (codigo == 206) sb.Append($"Content-Range: bytes {desde}-{fin}/{largo}\r\n");
        if (codigo == 416) sb.Append($"Content-Range: bytes */{largo}\r\n");
        sb.Append("Connection: close\r\n\r\n");
        var b = Encoding.ASCII.GetBytes(sb.ToString());
        red.Write(b, 0, b.Length);
    }

    public void Dispose()
    {
        _alto.Cancel();
        _fichas.Clear();
        try { _escucha.Stop(); } catch (SocketException) { }
        _alto.Dispose();
    }
}
