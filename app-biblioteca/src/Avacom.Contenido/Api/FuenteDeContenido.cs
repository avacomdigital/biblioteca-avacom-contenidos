using System.IO.Compression;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Medios;
using Avacom.Contenido.Paquetes;

namespace Avacom.Contenido.Api;

/// <summary>
/// La fuente real: el resolutor de medios (que descifra al vuelo y aplica la
/// politica) y el gestor de paquetes (que abre el manifiesto en memoria).
///
/// SOBRE EL CERROJO
///
/// La API atiende cada peticion en su propio hilo y un reproductor abre varias
/// a la vez cuando adelanta un video. El manifiesto es UNA conexion de SQLite y
/// el gestor UN diccionario; ninguno es seguro para hilos. La resolucion dura
/// microsegundos y se serializa aqui; la lectura del medio sale con su propio
/// flujo y no bloquea a nadie.
/// </summary>
public sealed class FuenteDeContenido(ResolutorDeMedios resolutor, Func<string, LectorDePaquete> abrirPaquete)
    : IFuenteDeContenido
{
    private readonly object _cerrojo = new();

    /// <summary>
    /// Interactivos ya desplegados en memoria, por elemento y version. Un
    /// interactivo son kilobytes y el navegador pide sus archivos uno a uno;
    /// abrir y recorrer el zip cifrado en cada peticion seria absurdo. Se
    /// conservan pocos y se descarta el mas antiguo.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, byte[]>> _interactivos = new();
    private readonly Queue<string> _ordenInteractivos = new();
    private const int MaximoInteractivos = 8;

    public MedioAbierto AbrirMedio(ElementoIndexado e) =>
        new(resolutor.Abrir(e.ElementoRef), ResolutorDeMedios.TipoDeContenido(e.HuellaArchivo ?? ""));

    public MedioAbierto? AbrirArchivoInterno(ElementoIndexado e, string rutaInterna)
    {
        var nombre = rutaInterna.Replace('\\', '/').TrimStart('/');
        if (nombre.Length == 0) nombre = "index.html";

        Dictionary<string, byte[]> archivos;
        lock (_cerrojo)
        {
            var clave = $"{e.ElementoRef}|{e.VersionElemento}|{e.HuellaArchivo}";
            if (!_interactivos.TryGetValue(clave, out archivos!))
            {
                archivos = Desplegar(e);
                _interactivos[clave] = archivos;
                _ordenInteractivos.Enqueue(clave);
                while (_ordenInteractivos.Count > MaximoInteractivos)
                    _interactivos.Remove(_ordenInteractivos.Dequeue());
            }
        }

        return archivos.TryGetValue(nombre, out var datos)
            ? new MedioAbierto(new MemoryStream(datos, writable: false), ResolutorDeMedios.TipoDeContenido(nombre))
            : null;
    }

    private Dictionary<string, byte[]> Desplegar(ElementoIndexado e)
    {
        var salida = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var zip = resolutor.Abrir(e.ElementoRef);
        using var archivo = new ZipArchive(zip, ZipArchiveMode.Read);
        foreach (var entrada in archivo.Entries)
        {
            if (entrada.FullName.EndsWith('/')) continue;
            using var s = entrada.Open();
            var ms = new MemoryStream();
            s.CopyTo(ms);
            salida[entrada.FullName] = ms.ToArray();
        }
        return salida;
    }

    public IReadOnlyList<PreguntaVisible> Preguntas(ElementoIndexado e)
    {
        lock (_cerrojo) return Lectura(e).Preguntas(e.ElementoRef);
    }

    public ISet<string> Corregibles(ElementoIndexado e)
    {
        lock (_cerrojo) return Lectura(e).Corregibles(e.ElementoRef);
    }

    public bool Acierta(ElementoIndexado e, string preguntaRef, string respuesta)
    {
        lock (_cerrojo) return Lectura(e).Acierta(preguntaRef, respuesta);
    }

    public IReadOnlyList<PasoDeLeccion> Leccion(ElementoIndexado e)
    {
        lock (_cerrojo) return Lectura(e).Leccion(e.ElementoRef);
    }

    public OpcionDeVoz? Voz(ElementoIndexado e, string? preguntaRef)
    {
        lock (_cerrojo)
        {
            var lectura = Lectura(e);
            if (preguntaRef is null) return lectura.VozDeElemento(e.ElementoRef);
            return lectura.Preguntas(e.ElementoRef).FirstOrDefault(p => p.PreguntaRef == preguntaRef)?.Voz;
        }
    }

    public MedioAbierto AbrirVoz(ElementoIndexado e, OpcionDeVoz voz) =>
        new(resolutor.AbrirPorHuella(e.PaqueteId, voz.HuellaArchivo), ResolutorDeMedios.TipoDeContenido(voz.HuellaArchivo));

    private LecturaDeManifiesto Lectura(ElementoIndexado e) => new(abrirPaquete(e.PaqueteId));
}
