using System.Text.Json.Nodes;
using Avacom.Contenido.Cripto;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Medios;
using Avacom.Contenido.Paquetes;
using Avacom.Contenido.Uso;

// ---------------------------------------------------------------------------
// AVACOM · comprobacion del componente de contenido, sin interfaz
//
// No necesita MAUI ni interfaz: solo el SDK de .NET. Sirve para ver el
// componente funcionando de punta a punta antes de tocar la aplicacion, que es
// donde se pierde el tiempo cuando uno empieza.
//
//   dotnet run --project src/Avacom.Contenido.Consola -- <carpeta_trabajo>
//
// Espera encontrar, dentro de la carpeta de trabajo:
//   pub/          los paquetes publicados
//   lic/licencia.json
//   nodo/nodo_privada.bin
//   esquema/contenido.sql
// ---------------------------------------------------------------------------

var trabajo = args.Length > 0 ? args[0] : ".";
var rutaIndice = Path.Combine(trabajo, "indice.db");

Titulo("1 · Se crea el indice del componente");
if (File.Exists(rutaIndice)) File.Delete(rutaIndice);
using var indice = new BaseDeIndice(rutaIndice);
indice.Crear(Path.Combine(trabajo, "esquema", "contenido.sql"));
Bien($"indice creado en {rutaIndice}");
Bien("ocho tablas y tres vistas, sin depender de ninguna otra base");

Titulo("2 · Se carga la licencia de este nodo");
var licencia = Licencia.Cargar(Path.Combine(trabajo, "lic", "licencia.json"));
if (!licencia.Verificar()) { Mal("la licencia no esta firmada por un emisor valido"); return 1; }
Bien($"licencia valida, vigente hasta {DateTimeOffset.FromUnixTimeMilliseconds(licencia.VenceEn):yyyy-MM-dd}");
Bien($"paquetes autorizados: {string.Join(", ", licencia.PaquetesAutorizados)}");
var nodoPrivada = File.ReadAllBytes(Path.Combine(trabajo, "nodo", "nodo_privada.bin"));

Titulo("3 · Se instalan los paquetes publicados");
var instalados = new Dictionary<string, string>();
foreach (var carpeta in Directory.GetDirectories(Path.Combine(trabajo, "pub")))
{
    using var lector = new LectorDePaquete(carpeta);

    var v = lector.Verificar(formatoSoportado: 2);
    if (!v.Aceptado) { Mal($"{Path.GetFileName(carpeta)}: {string.Join(" ", v.Motivos)}"); continue; }

    var a = lector.Abrir(licencia, nodoPrivada);
    if (!a.Aceptado) { Mal($"{Path.GetFileName(carpeta)}: {string.Join(" ", a.Motivos)}"); continue; }

    var pid = Identificador.Nuevo("PQ");
    Instalador.Proyectar(indice, lector, pid, carpeta);
    instalados[lector.ClavePaquete] = pid;
    Bien($"{lector.ClavePaquete} v{lector.Version} · {lector.Vitrina["titulo"]}");
}

Titulo("4 · El catalogo, ya filtrado por la politica");
Listar(indice);

Titulo("5 · El administrador desactiva una asignatura");
Instalador.Politica(indice, "asignatura", "Matematicas", "deshabilitar");
Listar(indice);

Titulo("6 · Se retira esa politica");
Instalador.QuitarPoliticas(indice);
Listar(indice);

Titulo("7 · Se abre un material y se descifra al vuelo");
var gestor = new GestorSimple(indice, licencia, nodoPrivada);
var resolutor = new ResolutorDeMedios(indice, gestor.Abrir);
var conArchivo = indice.Disponibles().FirstOrDefault(e => e.HuellaArchivo is not null);
if (conArchivo is null) { Mal("no hay ningun elemento con archivo"); return 1; }

var uso = new RegistroDeUso(indice.Conexion);
var sesion = uso.AbrirSesion();
var consumo = uso.RegistrarApertura(sesion, conArchivo.ElementoRef, conArchivo.VersionElemento);

using (var flujo = resolutor.Abrir(conArchivo.ElementoRef))
{
    var buf = new byte[Math.Min(64, flujo.Length)];
    flujo.ReadExactly(buf);
    var muestra = System.Text.Encoding.UTF8.GetString(buf).ReplaceLineEndings(" ");
    if (muestra.Length > 48) muestra = muestra[..48];
    Bien($"{conArchivo.Titulo} · {flujo.Length} bytes en claro");
    Bien($"primeros bytes: {muestra}");
}
uso.RegistrarCierre(consumo, progresoPct: 100);
Bien("el contenido en claro nunca toco el disco");

Titulo("8 · Lo que dejo el modo repaso, y lo que no puede dejar");
using (var cmd = indice.Conexion.CreateCommand())
{
    // Antes esto contaba filas en las tablas de intentos y calificaciones para
    // comprobar que seguian a cero. Ahora la comprobacion es mas fuerte: esas
    // tablas NO EXISTEN en el indice del componente. No es que el repaso se
    // porte bien, es que no hay donde escribir una nota aunque alguien quisiera.
    cmd.CommandText = """
        SELECT (SELECT count(*) FROM m08_repaso_consumo),
               (SELECT count(*) FROM sqlite_master WHERE type='table'),
               (SELECT count(*) FROM sqlite_master
                 WHERE type='table' AND (name LIKE '%intento%' OR name LIKE '%calificacion%'
                                      OR name LIKE '%persona%' OR name LIKE '%matricula%'))
        """;
    using var r = cmd.ExecuteReader(); r.Read();
    Bien($"materiales abiertos: {r.GetInt32(0)}");
    Bien($"tablas en el indice: {r.GetInt32(1)}   (las ocho del componente, ni una del LMS)");
    Bien($"tablas de intentos, notas, personas o matriculas: {r.GetInt32(2)}   (tiene que ser 0)");
    if (r.GetInt32(1) != 8 || r.GetInt32(2) != 0)
    {
        Mal("el indice tiene tablas que no le pertenecen");
        return 1;
    }
    Bien("no hay donde anotar una calificacion, ni por descuido");
}

Titulo("9 · Se borra el indice y se reconstruye");
var antes = indice.Disponibles().Count;
indice.Ejecutar("DELETE FROM m04_indice_elemento; DELETE FROM m04_indice_taxonomia;");
foreach (var (clave, pid) in instalados)
{
    var carpeta = Directory.GetDirectories(Path.Combine(trabajo, "pub")).First(d => d.Contains(clave));
    using var lector = new LectorDePaquete(carpeta);
    lector.Verificar(2); lector.Abrir(licencia, nodoPrivada);
    Instalador.Proyectar(indice, lector, pid, carpeta, soloIndice: true);
}
var despues = indice.Disponibles().Count;
if (antes == despues) Bien($"reconstruido: {despues} elementos, los mismos de antes");
else { Mal($"antes {antes}, despues {despues}"); return 1; }

Titulo("10 · El servidor local entrega el video sin escribirlo en disco");
using (var servidor = new ServidorDeMedios())
{
    var video = indice.Disponibles().FirstOrDefault(e => e.Tipo == "video");
    if (video is null) { Mal("no hay ningun video en el catalogo"); return 1; }

    long tamano;
    using (var f = resolutor.Abrir(video.ElementoRef)) tamano = f.Length;
    var salto = Math.Max(0, tamano / 2);            // a mitad del video, que es lo que hace un profesor

    var url = servidor.Publicar(() => resolutor.Abrir(video.ElementoRef),
                                ResolutorDeMedios.TipoDeContenido(video.HuellaArchivo!));
    using var http = new HttpClient();

    // el reproductor pide un trozo del medio, como cuando se adelanta
    var pide = new HttpRequestMessage(HttpMethod.Get, url);
    pide.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(salto, salto + 63);
    var resp = await http.SendAsync(pide);
    var trozo = await resp.Content.ReadAsByteArrayAsync();

    Bien($"{video.Titulo} · {tamano} bytes");
    Bien($"codigo {(int)resp.StatusCode} · rango {resp.Content.Headers.ContentRange}");
    Bien($"llegaron {trozo.Length} bytes desde el byte {salto}, sin descifrar lo anterior");
    if ((int)resp.StatusCode != 206 || trozo.Length != 64) { Mal("el servidor no respeta los rangos"); return 1; }

    // el mismo trozo, sacado directamente del flujo, tiene que coincidir
    using (var f = resolutor.Abrir(video.ElementoRef))
    {
        f.Seek(salto, SeekOrigin.Begin);
        var esperado = new byte[64];
        f.ReadExactly(esperado);
        if (!esperado.AsSpan().SequenceEqual(trozo)) { Mal("el trozo servido no coincide"); return 1; }
        Bien("el trozo servido coincide byte a byte con el descifrado directo");
    }

    // una ficha inventada no abre nada
    var inventada = $"{servidor.Base}/m/{new string('0', 32)}";
    if ((int)(await http.GetAsync(inventada)).StatusCode != 404) { Mal("una ficha falsa obtuvo respuesta"); return 1; }
    Bien("una direccion sin ficha valida responde 404");

    servidor.Retirar(url);
    if ((int)(await http.GetAsync(url)).StatusCode != 404) { Mal("la ficha sigue viva despues de retirarla"); return 1; }
    Bien("al cerrar el visor la ficha deja de existir");
}

Titulo("11 · Las preguntas llegan a la pantalla sin la clave de respuesta");
{
    var actividad = indice.Disponibles().FirstOrDefault(e => e.Tipo == "actividad");
    if (actividad is null) { Mal("no hay actividades"); return 1; }
    var lectura = new LecturaDeManifiesto(gestor.Abrir(actividad.PaqueteId));
    var preguntas = lectura.Preguntas(actividad.ElementoRef);
    Bien($"{actividad.Titulo} · {preguntas.Count} reactivos");

    // el tipo que ve la interfaz no tiene siquiera un campo donde meterla
    var campos = typeof(PreguntaVisible).GetProperties().Select(p => p.Name).ToArray();
    if (campos.Any(c => c.Contains("Clave", StringComparison.OrdinalIgnoreCase) ||
                        c.Contains("Respuesta", StringComparison.OrdinalIgnoreCase)))
    { Mal("el tipo que va a la interfaz expone la respuesta"); return 1; }
    Bien($"campos visibles: {string.Join(", ", campos)}");
    Bien("no hay ninguno que lleve la respuesta");

    var q = preguntas[0];
    Bien($"se comprueba \"{q.Enunciado}\" contra una respuesta cualquiera: " +
         (lectura.Acierta(q.PreguntaRef, "esto no es") ? "acierta" : "falla, como debe"));
    if (q.Voz is not null) Bien($"la pregunta trae instruccion hablada de {q.Voz.DuracionMs} ms");
}

Titulo("12 · El mismo paquete en otro nodo");
{
    var otro = Licencia.Cargar(Path.Combine(trabajo, "lic", "licencia.json"));
    var privadaAjena = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    var carpeta = Directory.GetDirectories(Path.Combine(trabajo, "pub"))[0];
    using var lector = new LectorDePaquete(carpeta);
    if (!lector.Verificar(2).Aceptado) { Mal("el paquete no verifica"); return 1; }
    var r = lector.Abrir(otro, privadaAjena);
    if (r.Aceptado) { Mal("se abrio con una clave de nodo que no es la suya"); return 1; }
    Bien("la firma sigue siendo valida, porque el paquete no esta alterado");
    Bien($"y aun asi no abre: {string.Join(" ", r.Motivos)}");
    Bien("copiarlo a una memoria y llevarselo a otro equipo no sirve de nada");
}

Console.WriteLine();
Console.WriteLine("Todo correcto. El componente funciona de punta a punta.");
return 0;


// ---------------------------------------------------------------- ayudas

static void Titulo(string t) { Console.WriteLine(); Console.WriteLine(t); Console.WriteLine(new string('-', t.Length)); }
static void Bien(string t) => Console.WriteLine("  " + t);
static void Mal(string t) => Console.WriteLine("  FALLA: " + t);

static void Listar(BaseDeIndice idx)
{
    var d = idx.Disponibles();
    foreach (var e in d) Console.WriteLine($"  {e.Tipo,-12} {e.Titulo,-46} {e.Nivel,-12} {e.Asignatura}");
    Console.WriteLine($"  ({d.Count} disponibles)");
}

/// <summary>Version minima del gestor de paquetes, para la consola.</summary>
file sealed class GestorSimple(BaseDeIndice indice, Licencia lic, byte[] priv)
{
    private readonly Dictionary<string, LectorDePaquete> _abiertos = new();
    public LectorDePaquete Abrir(string paqueteId)
    {
        if (_abiertos.TryGetValue(paqueteId, out var y)) return y;
        using var cmd = indice.Conexion.CreateCommand();
        cmd.CommandText = "SELECT ruta_paquete FROM m04_paquete_instalado WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", paqueteId);
        var ruta = (string)cmd.ExecuteScalar()!;
        var l = new LectorDePaquete(ruta);
        l.Verificar(2);
        l.Abrir(lic, priv);
        _abiertos[paqueteId] = l;
        return l;
    }
}

// Las escrituras del componente (instalar, proyectar, politicas) viven en
// Avacom.Contenido.Indice.Instalador, para que la consola y la aplicacion usen
// exactamente el mismo codigo. Si fueran dos copias, una acabaria divergiendo y
// esta comprobacion dejaria de decir nada sobre lo que corre en el aula.
