using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Avacom.Contenido.Api;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Paquetes;
using Xunit;

namespace Avacom.Contenido.Tests;

/// <summary>
/// Las capacidades opcionales de la API local (medio, leccion, evaluacion,
/// comprobar, voz). Igual que ApiLocalTests, estos nombres de campo SON el
/// contrato con el LMS: cambiarlos obliga a subir la version, no a tocar la
/// prueba.
///
/// Lo que mas se aprieta aqui es lo que NO debe salir: la clave de respuesta.
/// </summary>
public class ApiLocalCapacidadesTests : IDisposable
{
    private readonly string _carpeta;
    private readonly BaseDeIndice _indice;
    private readonly ApiLocal _api;
    private readonly HttpClient _http = new();
    private readonly FuenteDePruebas _fuente = new();

    public ApiLocalCapacidadesTests()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "avacom-api-cap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        _indice = new BaseDeIndice(Path.Combine(_carpeta, "indice.db"));
        _indice.Crear(RutaEsquema());
        Sembrar();
        _api = new ApiLocal(_indice, _ => null, publicarEnlace: false, fuente: _fuente);
        _http.DefaultRequestHeaders.Add("X-Avacom-Ficha", _api.Ficha);
    }

    private static string RutaEsquema()
    {
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
            VALUES('co-sec-mat','PQ1',NULL,'area','MAT','Matematicas',1,'CO','secundaria');

            INSERT INTO m04_indice_elemento(elemento_ref,paquete_id,version_elemento,tipo,titulo,
              taxonomia_ref,nivel_clave,grado,asignatura,idioma,huella_archivo,duracion_seg,estado)
            VALUES('el-doc','PQ1','1','documento','La funcion lineal','co-sec-mat','secundaria','8','Matematicas','es','aaa.pdf',NULL,'vigente'),
                  ('el-vid','PQ1','1','video','Que significa la pendiente','co-sec-mat','secundaria','8','Matematicas','es','bbb.mp4',30,'vigente'),
                  ('el-int','PQ1','1','interactivo','Explorador de rectas','co-sec-mat','secundaria','8','Matematicas','es','ccc.zip',NULL,'vigente'),
                  ('el-eval','PQ1','1','evaluacion','Evaluacion · Funcion lineal','co-sec-mat','secundaria','8','Matematicas','es',NULL,NULL,'vigente'),
                  ('el-lec','PQ1','1','leccion','Funcion lineal y razon de cambio','co-sec-mat','secundaria','8','Matematicas','es',NULL,NULL,'vigente');
            """);
    }

    private HttpResponseMessage Get(string ruta, RangeHeaderValue? rango = null)
    {
        var p = new HttpRequestMessage(HttpMethod.Get, _api.Base + ruta);
        if (rango is not null) p.Headers.Range = rango;
        return _http.SendAsync(p).Result;
    }

    private JsonElement GetJson(string ruta)
    {
        var r = Get(ruta);
        Assert.Equal(200, (int)r.StatusCode);
        return JsonDocument.Parse(r.Content.ReadAsStringAsync().Result).RootElement.Clone();
    }

    private HttpResponseMessage Post(string ruta, string json) =>
        _http.PostAsync(_api.Base + ruta, new StringContent(json, Encoding.UTF8, "application/json")).Result;

    // ----------------------------------------------------------- capacidades

    [Fact]
    public void Con_fuente_la_salud_declara_las_capacidades_nuevas()
    {
        var caps = GetJson("/v1/salud").GetProperty("capacidades").EnumerateArray().Select(x => x.GetString()).ToList();
        foreach (var c in new[] { "curso", "medio", "leccion", "evaluacion", "comprobar", "voz" })
            Assert.Contains(c, caps);
    }

    [Fact]
    public void Sin_fuente_las_rutas_nuevas_no_existen_y_la_salud_no_las_declara()
    {
        using var sinFuente = new ApiLocal(_indice, _ => null, publicarEnlace: false);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-Avacom-Ficha", sinFuente.Ficha);
        var salud = JsonDocument.Parse(http.GetStringAsync(sinFuente.Base + "/v1/salud").Result).RootElement;
        Assert.Equal(new[] { "curso" }, salud.GetProperty("capacidades").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(404, (int)http.GetAsync(sinFuente.Base + "/v1/medio/el-doc").Result.StatusCode);
        Assert.Equal(404, (int)http.GetAsync(sinFuente.Base + "/v1/evaluacion/el-eval").Result.StatusCode);
    }

    // ----------------------------------------------------------------- medio

    [Fact]
    public void El_medio_entrega_los_bytes_con_su_tipo()
    {
        var r = Get("/v1/medio/el-doc");
        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(_fuente.Pdf, r.Content.ReadAsByteArrayAsync().Result);
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
    }

    [Fact]
    public void El_medio_respeta_los_rangos_del_reproductor()
    {
        var r = Get("/v1/medio/el-vid", new RangeHeaderValue(1000, 1999));
        Assert.Equal(206, (int)r.StatusCode);
        var salida = r.Content.ReadAsByteArrayAsync().Result;
        Assert.Equal(1000, salida.Length);
        Assert.Equal(_fuente.Video.AsSpan(1000, 1000).ToArray(), salida);
        Assert.Equal(_fuente.Video.Length, r.Content.Headers.ContentRange?.Length);

        var sufijo = Get("/v1/medio/el-vid", new RangeHeaderValue(null, 500));
        Assert.Equal(206, (int)sufijo.StatusCode);
        Assert.Equal(_fuente.Video.AsSpan(_fuente.Video.Length - 500).ToArray(), sufijo.Content.ReadAsByteArrayAsync().Result);
    }

    [Fact]
    public void HEAD_da_las_cabeceras_sin_cuerpo()
    {
        var r = _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, _api.Base + "/v1/medio/el-vid")).Result;
        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal(_fuente.Video.Length, r.Content.Headers.ContentLength);
    }

    [Fact]
    public void Un_interactivo_sirve_index_por_defecto_y_sus_archivos_internos()
    {
        var indice = Get("/v1/medio/el-int");
        Assert.Equal(200, (int)indice.StatusCode);
        Assert.Contains("Explorador", indice.Content.ReadAsStringAsync().Result);
        Assert.StartsWith("text/html", indice.Content.Headers.ContentType?.ToString());

        var js = Get("/v1/medio/el-int/app.js");
        Assert.Equal(200, (int)js.StatusCode);
        Assert.StartsWith("text/javascript", js.Content.Headers.ContentType?.ToString());

        Assert.Equal(404, (int)Get("/v1/medio/el-int/no-existe.css").StatusCode);
    }

    [Fact]
    public void Una_leccion_no_tiene_archivo_409_y_lo_desconocido_404()
    {
        Assert.Equal(409, (int)Get("/v1/medio/el-lec").StatusCode);
        Assert.Equal(404, (int)Get("/v1/medio/no-existe").StatusCode);
    }

    [Fact]
    public void La_politica_tambien_protege_los_bytes()
    {
        Instalador.Politica(_indice, "asignatura", "Matematicas", "deshabilitar");
        Assert.Equal(403, (int)Get("/v1/medio/el-doc").StatusCode);
        Assert.Equal(403, (int)Get("/v1/evaluacion/el-eval").StatusCode);
        Assert.Equal(403, (int)Get("/v1/leccion/el-lec").StatusCode);
        Instalador.QuitarPoliticas(_indice);
        Assert.Equal(200, (int)Get("/v1/medio/el-doc").StatusCode);
    }

    // --------------------------------------------------------------- leccion

    [Fact]
    public void La_leccion_trae_sus_pasos_en_orden()
    {
        var d = GetJson("/v1/leccion/el-lec");
        Assert.Equal("el-lec", d.GetProperty("elemento_ref").GetString());
        var pasos = d.GetProperty("pasos").EnumerateArray().ToList();
        Assert.Equal(2, pasos.Count);
        Assert.Equal("el-doc", pasos[0].GetProperty("elemento_ref").GetString());
        Assert.Equal("Lectura previa", pasos[0].GetProperty("nota").GetString());
        Assert.True(pasos[0].GetProperty("disponible").GetBoolean());
        foreach (var campo in new[] { "orden", "elemento_ref", "titulo", "tipo", "nota", "disponible" })
            Assert.True(pasos[0].TryGetProperty(campo, out _), $"falta el campo {campo}");
    }

    // ------------------------------------------------------------ evaluacion

    [Fact]
    public void La_evaluacion_trae_las_preguntas_SIN_clave_ni_retroalimentacion()
    {
        var r = Get("/v1/evaluacion/el-eval");
        Assert.Equal(200, (int)r.StatusCode);
        var texto = r.Content.ReadAsStringAsync().Result;
        Assert.DoesNotContain("clave", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("retroalimentacion", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"3\"", texto, StringComparison.Ordinal);   // la respuesta de la primera pregunta

        var d = JsonDocument.Parse(texto).RootElement;
        var preguntas = d.GetProperty("preguntas").EnumerateArray().ToList();
        Assert.Equal(2, preguntas.Count);
        foreach (var campo in new[] { "ref", "orden", "tipo", "enunciado", "peso", "dificultad", "corregible", "voz" })
            Assert.True(preguntas[0].TryGetProperty(campo, out _), $"falta el campo {campo}");
        Assert.True(preguntas[0].GetProperty("corregible").GetBoolean());
        Assert.False(preguntas[1].GetProperty("corregible").GetBoolean());   // la abierta
    }

    [Fact]
    public void Un_documento_no_es_una_evaluacion()
        => Assert.Equal(409, (int)Get("/v1/evaluacion/el-doc").StatusCode);

    // ------------------------------------------------------------- comprobar

    [Fact]
    public void Comprobar_devuelve_el_veredicto_y_la_retroalimentacion_solo_al_acertar()
    {
        var bien = Post("/v1/comprobar", """{"elemento_ref":"el-eval","pregunta_ref":"p1","respuesta":" 3 "}""");
        Assert.Equal(200, (int)bien.StatusCode);
        var d = JsonDocument.Parse(bien.Content.ReadAsStringAsync().Result).RootElement;
        Assert.True(d.GetProperty("acierta").GetBoolean());
        Assert.Equal("La pendiente multiplica a x.", d.GetProperty("retroalimentacion").GetString());

        var mal = Post("/v1/comprobar", """{"elemento_ref":"el-eval","pregunta_ref":"p1","respuesta":"7"}""");
        var m = JsonDocument.Parse(mal.Content.ReadAsStringAsync().Result).RootElement;
        Assert.False(m.GetProperty("acierta").GetBoolean());
        Assert.Equal(JsonValueKind.Null, m.GetProperty("retroalimentacion").ValueKind);
        Assert.DoesNotContain("3", mal.Content.ReadAsStringAsync().Result);
    }

    [Fact]
    public void Una_pregunta_abierta_no_se_comprueba()
    {
        var r = Post("/v1/comprobar", """{"elemento_ref":"el-eval","pregunta_ref":"p2","respuesta":"lo que sea"}""");
        Assert.Equal(409, (int)r.StatusCode);
    }

    [Fact]
    public void Comprobar_sin_datos_da_400_y_con_pregunta_desconocida_404()
    {
        Assert.Equal(400, (int)Post("/v1/comprobar", "{}").StatusCode);
        Assert.Equal(404, (int)Post("/v1/comprobar", """{"elemento_ref":"el-eval","pregunta_ref":"px","respuesta":"3"}""").StatusCode);
    }

    // ------------------------------------------------------------------- voz

    [Fact]
    public void La_voz_de_una_pregunta_se_sirve_como_audio()
    {
        var r = Get("/v1/voz/el-eval/p1");
        Assert.Equal(200, (int)r.StatusCode);
        Assert.Equal("audio/wav", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, (int)Get("/v1/voz/el-eval/p2").StatusCode);
        Assert.Equal(404, (int)Get("/v1/voz/el-eval").StatusCode);
    }

    [Fact]
    public void Las_capacidades_siguen_exigiendo_la_ficha()
    {
        using var sinFicha = new HttpClient();
        foreach (var ruta in new[] { "/v1/medio/el-doc", "/v1/leccion/el-lec", "/v1/evaluacion/el-eval", "/v1/voz/el-eval/p1" })
            Assert.Equal(401, (int)sinFicha.GetAsync(_api.Base + ruta).Result.StatusCode);
        Assert.Equal(401, (int)sinFicha.PostAsync(_api.Base + "/v1/comprobar",
            new StringContent("{}", Encoding.UTF8, "application/json")).Result.StatusCode);
    }

    public void Dispose()
    {
        _http.Dispose();
        _api.Dispose();
        _indice.Dispose();
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }

    /// <summary>
    /// Una fuente en memoria. Tiene las claves porque las necesita para
    /// comparar, y las pruebas comprueban que ninguna sale por la API.
    /// </summary>
    private sealed class FuenteDePruebas : IFuenteDeContenido
    {
        public readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 prueba");
        public readonly byte[] Video = Enumerable.Range(0, 50_000).Select(i => (byte)(i * 7)).ToArray();
        private readonly byte[] _zip;
        private readonly Dictionary<string, string> _claves = new() { ["p1"] = "3" };

        public FuenteDePruebas()
        {
            using var ms = new MemoryStream();
            using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var w = new StreamWriter(z.CreateEntry("index.html").Open()))
                    w.Write("<html><body><h1>Explorador</h1><script src='app.js'></script></body></html>");
                using (var w = new StreamWriter(z.CreateEntry("app.js").Open()))
                    w.Write("console.log('hola')");
            }
            _zip = ms.ToArray();
        }

        public MedioAbierto AbrirMedio(ElementoIndexado e) => e.ElementoRef switch
        {
            "el-doc" => new(new MemoryStream(Pdf, false), "application/pdf"),
            "el-vid" => new(new MemoryStream(Video, false), "video/mp4"),
            "el-int" => new(new MemoryStream(_zip, false), "application/zip"),
            _ => throw new FileNotFoundException("no hay archivo"),
        };

        public MedioAbierto? AbrirArchivoInterno(ElementoIndexado e, string rutaInterna)
        {
            using var z = new ZipArchive(new MemoryStream(_zip, false), ZipArchiveMode.Read);
            var entrada = z.GetEntry(rutaInterna);
            if (entrada is null) return null;
            var ms = new MemoryStream();
            using (var s = entrada.Open()) s.CopyTo(ms);
            ms.Position = 0;
            return new MedioAbierto(ms, Medios.ResolutorDeMedios.TipoDeContenido(rutaInterna));
        }

        public IReadOnlyList<PreguntaVisible> Preguntas(ElementoIndexado e) =>
        [
            new("p1", 1, "opcion_multiple", "Cual es la pendiente de y = 3x - 5?", 1, "baja", "La pendiente multiplica a x.",
                new OpcionDeVoz("v1", "voz1.wav", 1200)),
            new("p2", 2, "abierta", "Explica que representa la pendiente", 2, "alta", null, null),
        ];

        public ISet<string> Corregibles(ElementoIndexado e) => new HashSet<string>(_claves.Keys);

        public bool Acierta(ElementoIndexado e, string preguntaRef, string respuesta) =>
            _claves.TryGetValue(preguntaRef, out var clave) &&
            string.Equals(clave.Trim(), respuesta.Trim(), StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<PasoDeLeccion> Leccion(ElementoIndexado e) =>
        [
            new(1, "el-doc", "Lectura previa", "La funcion lineal", "documento"),
            new(2, "el-vid", "Explicacion", "Que significa la pendiente", "video"),
        ];

        public OpcionDeVoz? Voz(ElementoIndexado e, string? preguntaRef) =>
            preguntaRef == "p1" ? new OpcionDeVoz("v1", "voz1.wav", 1200) : null;

        public MedioAbierto AbrirVoz(ElementoIndexado e, OpcionDeVoz voz) =>
            new(new MemoryStream(Encoding.ASCII.GetBytes("RIFF....WAVE"), false), "audio/wav");
    }
}
