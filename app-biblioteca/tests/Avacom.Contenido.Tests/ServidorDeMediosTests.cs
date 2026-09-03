using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Avacom.Contenido.Medios;
using Xunit;

namespace Avacom.Contenido.Tests;

/// <summary>
/// El servidor local es lo unico de este componente que abre un puerto, asi que
/// es lo que mas hay que apretar. Estas pruebas comprueban dos cosas distintas:
/// que sirve bien lo que debe, y que no sirve nada de lo que no debe.
/// </summary>
public class ServidorDeMediosTests
{
    private static byte[] Ruido(int n) => RandomNumberGenerator.GetBytes(n);

    [Fact]
    public async Task Entrega_el_archivo_entero()
    {
        var datos = Ruido(300_000);
        using var s = new ServidorDeMedios();
        var url = s.Publicar(() => new MemoryStream(datos, writable: false), "video/mp4");

        using var http = new HttpClient();
        var r = await http.GetAsync(url);
        var salida = await r.Content.ReadAsByteArrayAsync();

        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal(datos, salida);
        Assert.Equal("video/mp4", r.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Respeta_los_rangos_que_pide_el_reproductor()
    {
        var datos = Ruido(500_000);
        using var s = new ServidorDeMedios();
        var url = s.Publicar(() => new MemoryStream(datos, writable: false), "video/mp4");

        using var http = new HttpClient();
        var p = new HttpRequestMessage(HttpMethod.Get, url);
        p.Headers.Range = new RangeHeaderValue(400_000, 400_999);
        var r = await http.SendAsync(p);
        var salida = await r.Content.ReadAsByteArrayAsync();

        Assert.Equal(206, (int)r.StatusCode);
        Assert.Equal(1000, salida.Length);
        Assert.Equal(datos.AsSpan(400_000, 1000).ToArray(), salida);
        Assert.Equal(500_000, r.Content.Headers.ContentRange?.Length);
    }

    /// <summary>
    /// "bytes=-N" son los ULTIMOS N bytes. Es lo primero que pide un reproductor
    /// cuando abre un MP4 que trae su indice al final, que es la mayoria de los
    /// que no han pasado por faststart. Si se le sirve el principio, el video no
    /// arranca nunca y no dice por que.
    /// </summary>
    [Fact]
    public async Task Un_rango_de_sufijo_devuelve_el_final_del_archivo()
    {
        var datos = Ruido(200_000);
        using var s = new ServidorDeMedios();
        var url = s.Publicar(() => new MemoryStream(datos, writable: false), "video/mp4");

        using var http = new HttpClient();
        var p = new HttpRequestMessage(HttpMethod.Get, url);
        p.Headers.Range = new RangeHeaderValue(null, 500);       // bytes=-500
        var r = await http.SendAsync(p);
        var salida = await r.Content.ReadAsByteArrayAsync();

        Assert.Equal(206, (int)r.StatusCode);
        Assert.Equal(500, salida.Length);
        Assert.Equal(datos.AsSpan(datos.Length - 500).ToArray(), salida);
        Assert.Equal(datos.Length - 500, r.Content.Headers.ContentRange?.From);
    }

    [Fact]
    public async Task Una_ficha_inventada_no_abre_nada()
    {
        using var s = new ServidorDeMedios();
        s.Publicar(() => new MemoryStream(Ruido(1000)), "video/mp4");

        using var http = new HttpClient();
        var r = await http.GetAsync($"{s.Base}/m/{new string('a', 32)}");
        Assert.Equal(404, (int)r.StatusCode);
    }

    [Fact]
    public async Task Al_retirar_la_ficha_el_material_deja_de_estar()
    {
        using var s = new ServidorDeMedios();
        var url = s.Publicar(() => new MemoryStream(Ruido(1000)), "image/png");

        using var http = new HttpClient();
        Assert.Equal(200, (int)(await http.GetAsync(url)).StatusCode);

        s.Retirar(url);
        Assert.Equal(404, (int)(await http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task No_acepta_escrituras()
    {
        using var s = new ServidorDeMedios();
        var url = s.Publicar(() => new MemoryStream(Ruido(100)), "image/png");

        using var http = new HttpClient();
        var r = await http.PostAsync(url, new ByteArrayContent([1, 2, 3]));
        Assert.Equal(405, (int)r.StatusCode);
    }

    [Fact]
    public async Task Un_interactivo_comprimido_se_sirve_archivo_por_archivo()
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var e = new StreamWriter(z.CreateEntry("index.html").Open()))
                e.Write("<html><body>hola</body></html>");
            using (var e = new StreamWriter(z.CreateEntry("estilo.css").Open()))
                e.Write("body{color:#1d1d1f}");
        }
        ms.Position = 0;

        using var s = new ServidorDeMedios();
        var url = s.PublicarComprimido(ms);

        using var http = new HttpClient();
        var entrada = await http.GetAsync(url);
        Assert.Equal(200, (int)entrada.StatusCode);
        Assert.Contains("hola", await entrada.Content.ReadAsStringAsync());
        Assert.Equal("text/html", entrada.Content.Headers.ContentType?.MediaType);

        var css = await http.GetAsync(url.Replace("index.html", "estilo.css"));
        Assert.Equal(200, (int)css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);

        // y al cerrarlo se va todo, no solo el punto de entrada
        s.Retirar(url);
        Assert.Equal(404, (int)(await http.GetAsync(url.Replace("index.html", "estilo.css"))).StatusCode);
    }

    [Fact]
    public void Solo_escucha_en_el_propio_equipo()
    {
        using var s = new ServidorDeMedios();
        Assert.StartsWith("http://127.0.0.1:", s.Base);

        // el puerto no puede ser fijo: dos aplicaciones en el mismo equipo
        // chocarian, y un puerto conocido es un punto que sondear
        using var otro = new ServidorDeMedios();
        Assert.NotEqual(s.Puerto, otro.Puerto);
    }

    /// <summary>
    /// Esta prueba existe por un fallo real. La etiqueta con la que se deriva la
    /// clave de cada medio es el nombre del archivo EN CLARO; el .enc se le
    /// añade despues, al guardarlo. Cuando el componente usaba el nombre con
    /// .enc derivaba otra clave y todos los medios fallaban con un error de
    /// autenticacion, que no se parece en nada a su causa.
    /// </summary>
    [Theory]
    [InlineData("abc123.png.enc", "abc123.png")]
    [InlineData("abc123.png", "abc123.png")]
    [InlineData("def456.mp4.enc", "def456.mp4")]
    [InlineData("ghi789.wav.enc", "ghi789.wav")]
    public void La_etiqueta_de_un_medio_nunca_lleva_el_enc(string entra, string sale)
        => Assert.Equal(sale, ResolutorDeMedios.Etiqueta(entra));

    [Fact]
    public async Task Pide_un_rango_fuera_del_archivo()
    {
        var datos = Ruido(1000);
        using var s = new ServidorDeMedios();
        var url = s.Publicar(() => new MemoryStream(datos, writable: false), "video/mp4");

        using var http = new HttpClient();
        var p = new HttpRequestMessage(HttpMethod.Get, url);
        p.Headers.Range = new RangeHeaderValue(5000, 6000);
        var r = await http.SendAsync(p);

        Assert.Equal(416, (int)r.StatusCode);
    }
}
