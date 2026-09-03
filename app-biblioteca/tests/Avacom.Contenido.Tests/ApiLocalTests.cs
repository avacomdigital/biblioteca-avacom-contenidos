using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Avacom.Contenido.Api;
using Avacom.Contenido.Indice;
using Xunit;

namespace Avacom.Contenido.Tests;

/// <summary>
/// Estas pruebas SON el contrato con el equipo de LMS.
///
/// Cada nombre de campo que se comprueba aqui es un compromiso: si cambia,
/// el LMS deja de funcionar en las aulas donde ya esta instalado. Cambiar
/// cualquiera de estos nombres obliga a subir la version del contrato, no a
/// tocar la prueba para que vuelva a pasar.
/// </summary>
public class ApiLocalTests : IDisposable
{
    private readonly string _carpeta;
    private readonly BaseDeIndice _indice;
    private readonly ApiLocal _api;
    private readonly HttpClient _http = new();
    private string _ultimoMostrado = "";

    public ApiLocalTests()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "avacom-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        _indice = new BaseDeIndice(Path.Combine(_carpeta, "indice.db"));
        _indice.Crear(RutaEsquema());
        Sembrar();

        // publicarEnlace: false, para no pisar el punto de enlace del componente
        // que pueda estar corriendo de verdad en esta maquina.
        _api = new ApiLocal(_indice, r => { _ultimoMostrado = r; return null; }, publicarEnlace: false);
        _http.DefaultRequestHeaders.Add("X-Avacom-Ficha", _api.Ficha);
    }

    private static string RutaEsquema()
    {
        // Se sube hasta encontrar la carpeta del esquema. Asi la prueba funciona
        // igual desde la carpeta de compilacion que desde la raiz del proyecto.
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "esquema", "contenido.sql");
            if (File.Exists(r)) return r;
            d = d.Parent;
        }
        throw new FileNotFoundException("No se encontro esquema/contenido.sql desde " + AppContext.BaseDirectory);
    }

    private void Sembrar()
    {
        _indice.Ejecutar("""
            INSERT INTO m04_paquete_instalado(id,clave_paquete,version,formato_version,origen,pais,
              nivel_clave,grado,asignatura,idioma,ruta_paquete,huella_manifiesto,firma_verificada,
              instalado_en,estado,creado_en,secuencia)
            VALUES('PQ1','co-sec-8-mat','1',2,'avacom','CO','secundaria','8','Matematicas','es',
              'C:\\paquetes\\co-sec-8-mat','hh',1,1,'activo',1,1);

            INSERT INTO m04_indice_taxonomia(taxonomia_ref,paquete_id,padre_ref,tipo_nodo,codigo,
              nombre,orden,pais,nivel_clave)
            VALUES('co-sec-mat','PQ1',NULL,'area','MAT','Matematicas',1,'CO','secundaria'),
                  ('co-sec-mat-var','PQ1','co-sec-mat','pensamiento','VAR','Variacional',1,'CO','secundaria');

            INSERT INTO m04_indice_elemento(elemento_ref,paquete_id,version_elemento,tipo,titulo,
              taxonomia_ref,nivel_clave,grado,asignatura,idioma,huella_archivo,duracion_seg,estado)
            VALUES('el-doc','PQ1','1','documento','La funcion lineal','co-sec-mat-var','secundaria','8',
                   'Matematicas','es','aaa.pdf',NULL,'vigente'),
                  ('el-vid','PQ1','1','video','Que significa la pendiente','co-sec-mat-var','secundaria','8',
                   'Matematicas','es','bbb.mp4',30,'vigente');
            """);
    }

    private JsonElement Get(string ruta)
    {
        var r = _http.GetAsync(_api.Base + ruta).Result;
        Assert.Equal(200, (int)r.StatusCode);
        return JsonDocument.Parse(r.Content.ReadAsStringAsync().Result).RootElement.Clone();
    }

    // ---------------------------------------------------------------- salud

    [Fact]
    public void Salud_dice_quien_es_y_que_contrato_habla()
    {
        var d = Get("/v1/salud");
        Assert.Equal("avacom-contenido", d.GetProperty("componente").GetString());
        Assert.Equal(PuntoDeEnlace.Contrato, d.GetProperty("contrato").GetInt32());
        Assert.Equal(2, d.GetProperty("elementos").GetInt32());
        Assert.Equal(1, d.GetProperty("paquetes").GetInt32());
    }

    // -------------------------------------------------------------- catalogo

    [Fact]
    public void El_catalogo_trae_los_campos_que_el_LMS_necesita()
    {
        var e = Get("/v1/catalogo").GetProperty("elementos").EnumerateArray().First();

        // Estos son los nombres del contrato. Cambiar uno rompe el LMS.
        foreach (var campo in new[] { "ref", "tipo", "titulo", "nivel", "grado", "asignatura",
                                      "idioma", "taxonomia_ref", "version", "duracion_seg",
                                      "paquete", "huella" })
            Assert.True(e.TryGetProperty(campo, out _), $"falta el campo {campo}");

        Assert.Equal("secundaria", e.GetProperty("nivel").GetString());
        Assert.Equal("8", e.GetProperty("grado").GetString());
        Assert.Equal("Matematicas", e.GetProperty("asignatura").GetString());
    }

    [Fact]
    public void El_catalogo_NO_dice_donde_vive_el_archivo()
    {
        var texto = _http.GetStringAsync(_api.Base + "/v1/catalogo").Result;

        // Si el LMS supiera la ruta del paquete, acabaria abriendo el archivo por
        // su cuenta, saltandose el cifrado y la politica. No sale, y no debe salir.
        Assert.DoesNotContain("ruta", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paquetes\\\\", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PQ1", texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?nivel=secundaria", 2)]
    [InlineData("?nivel=preescolar", 0)]
    [InlineData("?asignatura=Matematicas", 2)]
    [InlineData("?grado=8", 2)]
    [InlineData("?grado=9", 0)]
    [InlineData("?tipo=video", 1)]
    [InlineData("?tipo=documento", 1)]
    [InlineData("?taxonomia_ref=co-sec-mat-var", 2)]
    [InlineData("?nivel=secundaria&grado=8&asignatura=Matematicas&tipo=video", 1)]
    public void Los_filtros_funcionan_y_se_combinan(string consulta, int esperados)
        => Assert.Equal(esperados, Get("/v1/catalogo" + consulta).GetProperty("elementos").GetArrayLength());

    // ------------------------------------------------------------ taxonomia

    [Fact]
    public void La_taxonomia_se_recorre_de_arriba_abajo()
    {
        var raiz = Get("/v1/taxonomia").GetProperty("nodos").EnumerateArray().Single();
        Assert.Equal("co-sec-mat", raiz.GetProperty("ref").GetString());
        Assert.Equal("area", raiz.GetProperty("tipo").GetString());
        Assert.Equal("Matematicas", raiz.GetProperty("nombre").GetString());

        var hijo = Get("/v1/taxonomia?padre=co-sec-mat").GetProperty("nodos").EnumerateArray().Single();
        Assert.Equal("co-sec-mat-var", hijo.GetProperty("ref").GetString());
        Assert.Equal("pensamiento", hijo.GetProperty("tipo").GetString());
    }

    // -------------------------------------------------------------- elemento

    [Fact]
    public void Un_elemento_suelto_se_resuelve_por_referencia()
    {
        var d = Get("/v1/elemento/el-vid");
        Assert.Equal("el-vid", d.GetProperty("ref").GetString());
        Assert.Equal(30, d.GetProperty("duracion_seg").GetInt32());
    }

    [Fact]
    public void Una_referencia_que_no_existe_da_404()
        => Assert.Equal(404, (int)_http.GetAsync(_api.Base + "/v1/elemento/no-existe").Result.StatusCode);

    // -------------------------------------------------------------- politica

    [Fact]
    public void Lo_que_la_escuela_desactivo_desaparece_para_el_LMS()
    {
        Assert.Equal(2, Get("/v1/catalogo").GetProperty("elementos").GetArrayLength());

        Instalador.Politica(_indice, "asignatura", "Matematicas", "deshabilitar");

        // No llega atenuado ni con una marca: no esta. Si el LMS pudiera verlo,
        // acabaria mostrandolo.
        Assert.Equal(0, Get("/v1/catalogo").GetProperty("elementos").GetArrayLength());

        // Y tampoco se puede sacar pidiendolo por referencia directa.
        Assert.Equal(403, (int)_http.GetAsync(_api.Base + "/v1/elemento/el-vid").Result.StatusCode);

        Instalador.QuitarPoliticas(_indice);
        Assert.Equal(2, Get("/v1/catalogo").GetProperty("elementos").GetArrayLength());
    }

    // --------------------------------------------------------------- mostrar

    [Fact]
    public void El_LMS_puede_pedir_que_se_muestre_un_material()
    {
        var r = _http.PostAsync(_api.Base + "/v1/mostrar",
            new StringContent("""{"elemento_ref":"el-vid"}""", Encoding.UTF8, "application/json")).Result;

        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal("el-vid", _ultimoMostrado);
    }

    [Fact]
    public void Mostrar_sin_referencia_da_400()
    {
        var r = _http.PostAsync(_api.Base + "/v1/mostrar",
            new StringContent("{}", Encoding.UTF8, "application/json")).Result;
        Assert.Equal(400, (int)r.StatusCode);
    }

    // -------------------------------------------------- retirar contenido

    /// <summary>
    /// El caso que pide el LMS: el administrador retira un paquete en la
    /// aplicacion y el LMS deja de verlo en la MISMA peticion siguiente, sin
    /// reiniciar nada y sin esperar a que caduque ninguna cache.
    /// </summary>
    [Fact]
    public void Retirar_un_paquete_lo_quita_del_catalogo_de_inmediato()
    {
        Assert.Equal(2, Get("/v1/catalogo").GetProperty("elementos").GetArrayLength());
        Assert.Equal(200, (int)_http.GetAsync(_api.Base + "/v1/elemento/el-vid").Result.StatusCode);

        Instalador.Desinstalar(_indice, "PQ1");

        // Sin reiniciar la API, sin limpiar nada: la siguiente peticion ya no lo trae.
        Assert.Equal(0, Get("/v1/catalogo").GetProperty("elementos").GetArrayLength());

        // Y tampoco se puede sacar pidiendolo por referencia directa.
        Assert.Equal(404, (int)_http.GetAsync(_api.Base + "/v1/elemento/el-vid").Result.StatusCode);

        // La taxonomia del paquete retirado tambien se va: si quedara, el LMS
        // pintaria ramas del arbol que ya no llevan a ningun material.
        Assert.Empty(Get("/v1/taxonomia").GetProperty("nodos").EnumerateArray());
    }

    [Fact]
    public void Retirar_deja_la_salud_coherente_con_el_catalogo()
    {
        Instalador.Desinstalar(_indice, "PQ1");

        var s = Get("/v1/salud");
        Assert.Equal(0, s.GetProperty("elementos").GetInt32());
        Assert.Equal(0, s.GetProperty("paquetes").GetInt32());
    }

    // ------------------------------------------------- huella del catalogo

    /// <summary>
    /// La huella es lo que permite al LMS no cachear a ciegas. Estas pruebas
    /// fijan las dos unicas propiedades de las que el LMS puede depender:
    /// si nada cambio es la misma, y si cambio lo que el LMS ve es distinta.
    /// </summary>
    [Fact]
    public void La_huella_no_cambia_si_no_cambia_nada()
    {
        var a = Get("/v1/salud").GetProperty("huella_catalogo").GetString();
        var b = Get("/v1/salud").GetProperty("huella_catalogo").GetString();
        Assert.False(string.IsNullOrWhiteSpace(a));
        Assert.Equal(a, b);
    }

    [Fact]
    public void La_huella_cambia_al_retirar_un_paquete()
    {
        var antes = Get("/v1/salud").GetProperty("huella_catalogo").GetString();
        Instalador.Desinstalar(_indice, "PQ1");
        var despues = Get("/v1/salud").GetProperty("huella_catalogo").GetString();

        Assert.NotEqual(antes, despues);
    }

    [Fact]
    public void La_huella_cambia_al_desactivar_por_politica()
    {
        // Desactivar no desinstala nada, pero cambia lo que el LMS ve, y por eso
        // tiene que cambiar la huella igual que si se hubiera retirado.
        var antes = Get("/v1/salud").GetProperty("huella_catalogo").GetString();

        Instalador.Politica(_indice, "asignatura", "Matematicas", "deshabilitar");
        var conPolitica = Get("/v1/salud").GetProperty("huella_catalogo").GetString();
        Assert.NotEqual(antes, conPolitica);

        // Y al retirarla vuelve a ser la de antes: la huella describe el estado,
        // no cuenta cuantas veces se toco.
        Instalador.QuitarPoliticas(_indice);
        Assert.Equal(antes, Get("/v1/salud").GetProperty("huella_catalogo").GetString());
    }

    [Fact]
    public void Las_respuestas_prohiben_cachear()
    {
        // Si una capa intermedia cacheara el catalogo, el LMS mostraria material
        // retirado por mucho que la huella sea correcta.
        foreach (var ruta in new[] { "/v1/salud", "/v1/catalogo", "/v1/taxonomia" })
        {
            var r = _http.GetAsync(_api.Base + ruta).Result;
            Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
        }
    }

    // ------------------------------------------------------------ seguridad

    [Fact]
    public void Sin_ficha_no_se_responde_nada()
    {
        using var sinFicha = new HttpClient();
        foreach (var ruta in new[] { "/v1/salud", "/v1/catalogo", "/v1/taxonomia", "/v1/elemento/el-vid" })
            Assert.Equal(401, (int)sinFicha.GetAsync(_api.Base + ruta).Result.StatusCode);
    }

    [Fact]
    public void Con_una_ficha_inventada_tampoco()
    {
        using var otra = new HttpClient();
        otra.DefaultRequestHeaders.Add("X-Avacom-Ficha", new string('0', 64));
        Assert.Equal(401, (int)otra.GetAsync(_api.Base + "/v1/salud").Result.StatusCode);
    }

    [Fact]
    public void El_LMS_no_puede_escribir_nada()
    {
        var vacio = new StringContent("{}", Encoding.UTF8, "application/json");

        // No hay punto de enlace para instalar, desinstalar ni cambiar politicas.
        // Eso es del administrador y se hace en la aplicacion, no por aqui.
        foreach (var ruta in new[] { "/v1/instalar", "/v1/politica", "/v1/desinstalar" })
            Assert.Equal(404, (int)_http.PostAsync(_api.Base + ruta, vacio).Result.StatusCode);

        Assert.Equal(404, (int)_http.PutAsync(_api.Base + "/v1/catalogo", vacio).Result.StatusCode);
        Assert.Equal(404, (int)_http.DeleteAsync(_api.Base + "/v1/elemento/el-vid").Result.StatusCode);
    }

    [Fact]
    public void Solo_escucha_en_el_propio_equipo()
        => Assert.StartsWith("http://127.0.0.1:", _api.Base);

    public void Dispose()
    {
        _http.Dispose();
        _api.Dispose();
        _indice.Dispose();
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }
}
