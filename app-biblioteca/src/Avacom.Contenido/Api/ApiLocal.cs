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
///   Leer: el catalogo, el arbol curricular, un elemento suelto, los cursos.
///   Pedir: que se muestre un material en la pantalla.
///   Con las capacidades opcionales (si la aplicacion enchufa una fuente de
///   contenido): recibir los bytes descifrados de un material, los pasos de una
///   leccion, las preguntas de una evaluacion SIN clave, y pedir que se
///   compruebe una respuesta. La clave sigue sin salir de aqui: se compara.
///   Nada mas. No puede instalar, ni desinstalar, ni cambiar politicas, ni
///   tocar el indice. Todo eso es del administrador y se hace en la aplicacion.
///
/// LO QUE LO PROTEGE
///
///   Escucha solo en 127.0.0.1. Ninguna tableta del aula lo alcanza.
///   El puerto lo elige el sistema, distinto en cada arranque.
///   Cada peticion tiene que traer la ficha del equipo. Sin ella, 401.
///   Solo GET, HEAD y dos POST que no escriben nada.
///   La politica del administrador se comprueba antes de entregar un solo byte.
/// </summary>
public sealed class ApiLocal : IDisposable
{
    private readonly BaseDeIndice _indice;
    private readonly Func<string, string?> _mostrar;
    private readonly IFuenteDeContenido? _fuente;
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
    ///
    /// <paramref name="fuente"/> es opcional. Sin ella la API publica lo mismo
    /// que en el contrato original; con ella declara y responde las capacidades
    /// medio, leccion, evaluacion, comprobar y voz.
    /// </summary>
    public ApiLocal(BaseDeIndice indice, Func<string, string?> mostrar, bool publicarEnlace = true,
                    IFuenteDeContenido? fuente = null)
    {
        _indice = indice;
        _mostrar = mostrar;
        _fuente = fuente;

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
                var (metodo, ruta, ficha, cuerpo, rango) = p.Value;

                // Comparacion en tiempo constante: si tardara mas cuando los
                // primeros caracteres coinciden, se podria adivinar la ficha
                // midiendo el tiempo de respuesta.
                if (!FichaValida(ficha)) { Responder(red, 401, """{"error":"ficha no valida"}"""); return; }

                var respuesta = Despachar(metodo, ruta, cuerpo);
                if (respuesta.Medio is not null)
                {
                    using var flujo = respuesta.Medio.Flujo;
                    ResponderMedio(red, metodo, flujo, respuesta.Medio.TipoContenido, rango);
                }
                else
                {
                    Responder(red, respuesta.Codigo, respuesta.Json ?? "{}", sinCuerpo: metodo == "HEAD");
                }
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

    private readonly record struct Respuesta(int Codigo, string? Json, MedioAbierto? Medio = null)
    {
        public static implicit operator Respuesta((int, string) par) => new(par.Item1, par.Item2);
    }

    private Respuesta Despachar(string metodo, string ruta, string cuerpo)
    {
        var (camino, consulta) = Partir(ruta);
        var lectura = metodo is "GET" or "HEAD";

        if (lectura && camino == "/v1/salud")      return (200, Salud());
        if (lectura && camino == "/v1/catalogo")   return (200, Catalogo(consulta));
        if (lectura && camino == "/v1/cursos")     return (200, Cursos());
        if (lectura && camino.StartsWith("/v1/curso/", StringComparison.Ordinal))
            return Curso(Uri.UnescapeDataString(camino["/v1/curso/".Length..]));
        if (lectura && camino == "/v1/taxonomia")  return (200, Taxonomia(consulta));
        if (lectura && camino.StartsWith("/v1/elemento/", StringComparison.Ordinal))
            return Elemento(Uri.UnescapeDataString(camino["/v1/elemento/".Length..]));
        if (metodo == "POST" && camino == "/v1/mostrar")   return Mostrar(cuerpo);

        // Capacidades opcionales. Sin fuente, la ruta no existe: el LMS lo sabe
        // de antemano porque /v1/salud no la declaro.
        if (_fuente is not null)
        {
            if (lectura && camino.StartsWith("/v1/medio/", StringComparison.Ordinal))
            {
                var (referencia, interno) = PartirReferencia(camino["/v1/medio/".Length..]);
                return Medio(referencia, interno);
            }
            if (lectura && camino.StartsWith("/v1/leccion/", StringComparison.Ordinal))
                return Leccion(Uri.UnescapeDataString(camino["/v1/leccion/".Length..]));
            if (lectura && camino.StartsWith("/v1/evaluacion/", StringComparison.Ordinal))
                return Evaluacion(Uri.UnescapeDataString(camino["/v1/evaluacion/".Length..]));
            if (metodo == "POST" && camino == "/v1/comprobar") return Comprobar(cuerpo);
            if (lectura && camino.StartsWith("/v1/voz/", StringComparison.Ordinal))
            {
                var (referencia, pregunta) = PartirReferencia(camino["/v1/voz/".Length..]);
                return Voz(referencia, pregunta);
            }
        }

        return (404, """{"error":"no existe ese punto de enlace"}""");
    }

    /// <summary>"ref/lo/que/sigue" → ("ref", "lo/que/sigue"); "ref" → ("ref", null).</summary>
    private static (string referencia, string? resto) PartirReferencia(string texto)
    {
        var i = texto.IndexOf('/', StringComparison.Ordinal);
        if (i < 0) return (Uri.UnescapeDataString(texto), null);
        var resto = texto[(i + 1)..];
        return (Uri.UnescapeDataString(texto[..i]), resto.Length == 0 ? null : Uri.UnescapeDataString(resto));
    }

    /// <summary>
    /// Lo que este componente sabe hacer HOY.
    ///
    /// El LMS consulta esta lista antes de usar cualquier punto de enlace que no
    /// sea de los originales, y si algo no esta, no lo simula: sin "evaluacion"
    /// no entrega preguntas, y sin "comprobar" no produce nota. Un componente
    /// antiguo que no declare nada equivale a la lista vacia, y el LMS degrada
    /// en vez de romperse.
    ///
    /// Aqui solo va lo que esta implementado y probado. Declarar una capacidad
    /// que todavia no responde es peor que no declararla: el LMS la usaria.
    /// </summary>
    private string[] Capacidades => _fuente is null
        ? ["curso"]
        : ["curso", "medio", "leccion", "evaluacion", "comprobar", "voz"];

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
            cursos = _indice.Cursos().Count,
            capacidades = Capacidades,
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

    /// <summary>
    /// Los cursos ofrecidos a este equipo. Es lo que el LMS pinta en su seccion
    /// de asignaturas.
    ///
    /// curso_ref es la referencia estable: la emite este componente, no cambia
    /// cuando se republica el curso corregido, y es lo que el LMS guarda. El
    /// titulo NO sirve para eso: cambia entre versiones, y un expediente que
    /// apunte a un titulo deja de poder explicarse en cuanto se corrija una
    /// errata.
    /// </summary>
    private string Cursos()
    {
        var cursos = _indice.Cursos().Select(c => new
        {
            curso_ref = c.CursoRef,
            titulo = c.Titulo,
            version_vigente = c.Version,
            pais = c.Pais,
            nivel = c.Nivel,
            grado = c.Grado,
            asignatura = c.Asignatura,
            idioma = c.Idioma,
            lecciones = c.Lecciones,
            elementos = c.Elementos,
            actualizado_en = c.ActualizadoEn,
        });
        // El contrato propuesto por OPS pide aqui un contador "generacion".
        // Se publica la huella en su lugar, que es la señal que este componente
        // ya tiene y que se compara igual de bien. Un contador monotono exige
        // guardarlo en el esquema y acordarse de incrementarlo en cada sitio que
        // toca el catalogo; la huella se deriva de lo que el LMS veria, asi que
        // no hay nada que recordar mantener. Si OPS necesita de verdad un numero
        // que crezca, es una decision con esquema detras, no un renombre.
        return Json(new { huella_catalogo = HuellaDeCatalogo(_indice.Disponibles()), cursos });
    }

    /// <summary>
    /// Un curso concreto con los elementos que lo componen, agrupados por su
    /// nodo de taxonomia.
    ///
    /// Las secciones salen del arbol curricular que trae el propio contenido, no
    /// de una estructura inventada aqui: el codigo de cada seccion es su
    /// taxonomia_ref, que es estable entre versiones y es lo que permite que el
    /// progreso de un alumno sobreviva a una republicacion.
    /// </summary>
    private (int, string) Curso(string cursoRef)
    {
        var curso = _indice.Cursos().FirstOrDefault(c => c.CursoRef == cursoRef);
        if (curso is null) return (404, """{"error":"no hay ningun curso con esa referencia"}""");

        var elementos = _indice.Disponibles().Where(e => e.ClavePaquete == cursoRef).ToList();
        var nodos = _indice.TaxonomiaDe(cursoRef);

        var secciones = nodos
            .Select(n => new
            {
                codigo = n.TaxonomiaRef,
                titulo = n.Nombre,
                tipo = n.TipoNodo,
                orden = n.Orden,
                items = elementos
                    .Where(e => e.TaxonomiaRef == n.TaxonomiaRef)
                    .OrderBy(e => e.Titulo, StringComparer.Ordinal)
                    .Select((e, i) => new
                    {
                        orden = i + 1,
                        tipo = e.Tipo,
                        elemento_ref = e.ElementoRef,
                        titulo = e.Titulo,
                        version = e.VersionElemento,
                        duracion_seg = e.DuracionSeg,
                    }),
            })
            .Where(s => s.items.Any())     // una seccion vacia no se pinta
            .OrderBy(s => s.orden);

        return (200, Json(new
        {
            curso_ref = curso.CursoRef,
            titulo = curso.Titulo,
            version = curso.Version,
            nivel = curso.Nivel,
            grado = curso.Grado,
            asignatura = curso.Asignatura,
            idioma = curso.Idioma,
            huella = HuellaDeCatalogo(elementos),
            secciones,
        }));
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

    // ------------------------------------------------ capacidades opcionales

    /// <summary>
    /// Resuelve una referencia con las mismas dos comprobaciones que /v1/elemento:
    /// que exista y que la politica lo permita. Ninguna capacidad se salta esto.
    /// </summary>
    private (ElementoIndexado? elemento, Respuesta error) Resolver(string elementoRef)
    {
        var e = _indice.Elemento(elementoRef);
        if (e is null) return (null, (404, """{"error":"no esta en el indice"}"""));
        if (!_indice.Politica.Permite(e))
            return (null, (403, """{"error":"la politica de esta instalacion no lo permite"}"""));
        return (e, default);
    }

    /// <summary>
    /// Los bytes descifrados de un material. Es la unica via por la que el LMS
    /// recibe contenido, y por eso pasa por el mismo resolutor que el visor:
    /// politica, cifrado y clave se aplican igual venga de donde venga.
    ///
    /// Para un interactivo (zip) se sirve el archivo interno pedido, o
    /// index.html si no se pide ninguno, para que el navegador del LMS cargue
    /// sus recursos con rutas relativas.
    /// </summary>
    private Respuesta Medio(string elementoRef, string? rutaInterna)
    {
        var (e, error) = Resolver(elementoRef);
        if (e is null) return error;
        if (e.HuellaArchivo is null)
            return (409, """{"error":"este elemento es estructura, no tiene archivo"}""");

        try
        {
            if (e.Tipo == "interactivo")
            {
                var interno = _fuente!.AbrirArchivoInterno(e, rutaInterna ?? "index.html");
                return interno is null
                    ? new Respuesta(404, """{"error":"no existe dentro del interactivo"}""")
                    : new Respuesta(200, null, interno);
            }
            if (rutaInterna is not null)
                return (404, """{"error":"solo un interactivo tiene archivos internos"}""");
            return new Respuesta(200, null, _fuente!.AbrirMedio(e));
        }
        catch (Exception ex) { return Fallo(ex); }
    }

    private Respuesta Leccion(string elementoRef)
    {
        var (e, error) = Resolver(elementoRef);
        if (e is null) return error;
        if (e.Tipo != "leccion")
            return (409, """{"error":"este elemento no es una leccion"}""");
        try
        {
            var pasos = _fuente!.Leccion(e).Select(p =>
            {
                var item = _indice.Elemento(p.ItemRef);
                return new
                {
                    orden = p.Orden,
                    elemento_ref = p.ItemRef,
                    titulo = p.Titulo ?? item?.Titulo,
                    tipo = p.Tipo ?? item?.Tipo,
                    nota = p.Nota,
                    // Un paso puede apuntar a algo que la escuela desactivo o que
                    // ya no esta: se dice, para que el LMS no lo ofrezca.
                    disponible = item is not null && _indice.Politica.Permite(item),
                };
            }).ToList();
            return (200, Json(new
            {
                elemento_ref = e.ElementoRef,
                titulo = e.Titulo,
                tipo = e.Tipo,
                version = e.VersionElemento,
                duracion_seg = e.DuracionSeg,
                pasos,
            }));
        }
        catch (Exception ex) { return Fallo(ex); }
    }

    /// <summary>
    /// Las preguntas de una evaluacion o actividad. Fijate en lo que NO sale:
    /// la clave y la retroalimentacion. La primera no sale nunca; la segunda
    /// llega con el veredicto, igual que en el visor de la pantalla, porque
    /// antes de responder es una pista.
    /// </summary>
    private Respuesta Evaluacion(string elementoRef)
    {
        var (e, error) = Resolver(elementoRef);
        if (e is null) return error;
        if (e.Tipo is not ("evaluacion" or "actividad"))
            return (409, """{"error":"este elemento no es una evaluacion ni una actividad"}""");
        try
        {
            var corregibles = _fuente!.Corregibles(e);
            var preguntas = _fuente.Preguntas(e).Select(p => new
            {
                @ref = p.PreguntaRef,
                orden = p.Orden,
                tipo = p.Tipo,
                enunciado = p.Enunciado,
                peso = p.Peso,
                dificultad = p.Dificultad,
                corregible = corregibles.Contains(p.PreguntaRef),
                voz = p.Voz is not null,
            }).ToList();
            return (200, Json(new
            {
                elemento_ref = e.ElementoRef,
                titulo = e.Titulo,
                tipo = e.Tipo,
                version = e.VersionElemento,
                voz = _fuente.Voz(e, null) is not null,
                preguntas,
            }));
        }
        catch (Exception ex) { return Fallo(ex); }
    }

    /// <summary>
    /// Comprueba una respuesta sin revelar la clave. El LMS guarda el veredicto;
    /// la retroalimentacion solo acompaña al acierto, como en la pantalla.
    /// </summary>
    private Respuesta Comprobar(string cuerpo)
    {
        string? elementoRef, preguntaRef, respuesta;
        try
        {
            using var d = JsonDocument.Parse(string.IsNullOrWhiteSpace(cuerpo) ? "{}" : cuerpo);
            elementoRef = d.RootElement.TryGetProperty("elemento_ref", out var a) ? a.GetString() : null;
            preguntaRef = d.RootElement.TryGetProperty("pregunta_ref", out var b) ? b.GetString() : null;
            respuesta = d.RootElement.TryGetProperty("respuesta", out var c)
                ? (c.ValueKind == JsonValueKind.String ? c.GetString() : c.GetRawText())
                : null;
        }
        catch (JsonException) { return (400, """{"error":"el cuerpo no es JSON valido"}"""); }

        if (string.IsNullOrWhiteSpace(elementoRef) || string.IsNullOrWhiteSpace(preguntaRef))
            return (400, """{"error":"faltan elemento_ref o pregunta_ref"}""");

        var (e, error) = Resolver(elementoRef);
        if (e is null) return error;
        try
        {
            var pregunta = _fuente!.Preguntas(e).FirstOrDefault(p => p.PreguntaRef == preguntaRef);
            if (pregunta is null) return (404, """{"error":"no existe esa pregunta en ese elemento"}""");
            if (!_fuente.Corregibles(e).Contains(preguntaRef))
                return (409, """{"error":"pregunta abierta: la califica el docente con la rubrica"}""");

            var acierta = _fuente.Acierta(e, preguntaRef, respuesta ?? "");
            return (200, Json(new { acierta, retroalimentacion = acierta ? pregunta.Retroalimentacion : null }));
        }
        catch (Exception ex) { return Fallo(ex); }
    }

    private Respuesta Voz(string elementoRef, string? preguntaRef)
    {
        var (e, error) = Resolver(elementoRef);
        if (e is null) return error;
        try
        {
            var voz = _fuente!.Voz(e, preguntaRef);
            if (voz is null) return (404, """{"error":"no hay instruccion hablada para eso"}""");
            return new Respuesta(200, null, _fuente.AbrirVoz(e, voz));
        }
        catch (Exception ex) { return Fallo(ex); }
    }

    /// <summary>Las excepciones de la fuente, traducidas a codigos que el LMS ya entiende.</summary>
    private static Respuesta Fallo(Exception ex) => ex switch
    {
        UnauthorizedAccessException => (403, """{"error":"la politica de esta instalacion no lo permite"}"""),
        FileNotFoundException => (404, Json(new { error = ex.Message })),
        InvalidOperationException => (409, Json(new { error = ex.Message })),
        _ => (500, Json(new { error = "No se pudo abrir el contenido: " + ex.Message })),
    };

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

    private static (string metodo, string ruta, string? ficha, string cuerpo, string? rango)? LeerPeticion(NetworkStream red)
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
        string? rango = null;
        int largo = 0;
        foreach (var l in lineas.Skip(1))
        {
            if (l.StartsWith("X-Avacom-Ficha:", StringComparison.OrdinalIgnoreCase))
                ficha = l[15..].Trim();
            else if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(l[15..].Trim(), out largo);
            else if (l.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                rango = l[6..].Trim();
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

        return (partes[0], partes[1], ficha, cuerpo, rango);
    }

    private static string Razon(int codigo) => codigo switch
    {
        200 => "OK", 206 => "Partial Content", 400 => "Bad Request", 401 => "Unauthorized",
        403 => "Forbidden", 404 => "Not Found", 409 => "Conflict", 416 => "Range Not Satisfiable",
        500 => "Internal Server Error", _ => "OK",
    };

    private static void Responder(NetworkStream red, int codigo, string json, bool sinCuerpo = false)
    {
        var cuerpo = Encoding.UTF8.GetBytes(json);
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {codigo} {Razon(codigo)}\r\n");
        sb.Append("Content-Type: application/json; charset=utf-8\r\n");
        sb.Append($"Content-Length: {cuerpo.Length}\r\n");
        sb.Append("Cache-Control: no-store\r\n");
        sb.Append("Connection: close\r\n\r\n");
        var enc = Encoding.ASCII.GetBytes(sb.ToString());
        red.Write(enc, 0, enc.Length);
        if (!sinCuerpo) red.Write(cuerpo, 0, cuerpo.Length);
    }

    /// <summary>
    /// Sirve un flujo descifrado por rangos, como hace el servidor de medios del
    /// visor: el reproductor del LMS pide trozos y cada trozo se descifra en el
    /// momento. "bytes=-N" son los ULTIMOS N bytes, que es lo primero que pide un
    /// reproductor con un MP4 que trae el indice al final.
    /// </summary>
    private static void ResponderMedio(NetworkStream red, string metodo, Stream flujo, string tipo, string? rango)
    {
        var largo = flujo.Length;
        long desde = 0; long? hasta = null; var sufijo = false;
        if (rango is not null && rango.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            var r = rango[6..].Split('-');
            if (r.Length == 2)
            {
                if (r[0].Length == 0)
                {
                    if (long.TryParse(r[1], out var ultimos)) { desde = Math.Max(0, largo - ultimos); sufijo = true; }
                }
                else
                {
                    if (long.TryParse(r[0], out var d)) desde = d;
                    if (long.TryParse(r[1], out var h)) hasta = h;
                }
            }
        }
        var fin = hasta ?? largo - 1;
        if (desde >= largo && largo > 0)
        {
            CabeceraMedio(red, 416, tipo, 0, largo: largo);
            return;
        }
        if (fin >= largo) fin = largo - 1;
        var cuantos = Math.Max(0, fin - desde + 1);
        var parcial = hasta is not null || desde > 0 || sufijo;

        CabeceraMedio(red, parcial ? 206 : 200, tipo, cuantos,
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

    private static void CabeceraMedio(NetworkStream red, int codigo, string tipo, long cuantos,
                                      long? desde = null, long? fin = null, long? largo = null)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {codigo} {Razon(codigo)}\r\n");
        sb.Append($"Content-Type: {tipo}\r\n");
        sb.Append($"Content-Length: {cuantos}\r\n");
        sb.Append("Accept-Ranges: bytes\r\n");
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
        if (_publicoEnlace) PuntoDeEnlace.Retirar();
        try { _escucha.Stop(); } catch (SocketException) { }
        _alto.Dispose();
    }
}
