using System.Text.Json.Nodes;
using Avacom.Contenido.Paquetes;

namespace Avacom.Contenido.Medios;

/// <summary>
/// Convierte una referencia de elemento en algo que se puede reproducir.
/// Es el único punto por el que sale contenido descifrado, y por eso es donde
/// se comprueba la política del administrador antes de entregar nada.
/// </summary>
public sealed class ResolutorDeMedios(Indice.BaseDeIndice indice, Func<string, LectorDePaquete> abrirPaquete)
{
    /// <summary>
    /// Un cerrojo, y no por costumbre.
    ///
    /// El servidor local atiende cada peticion de rango en su propio hilo, y un
    /// reproductor abre varias a la vez cuando el profesor adelanta un video.
    /// Todas acaban aqui, tocando la MISMA conexion de SQLite que usa la
    /// interfaz y el mismo diccionario de paquetes abiertos. Ninguno de los dos
    /// es seguro para hilos, y el sintoma seria un fallo intermitente que
    /// aparece justo cuando alguien esta usando la pantalla delante de treinta
    /// alumnos. Es reentrante, asi que Abrir puede llamar a AbrirPorHuella.
    ///
    /// Lo que se protege es la resolucion, que dura microsegundos. La lectura
    /// del video ya sale de aqui con su propio flujo y no bloquea a nadie.
    /// </summary>
    private readonly object _cerrojo = new();

    public Stream Abrir(string elementoRef)
    {
        lock (_cerrojo)
        {
            var e = indice.Elemento(elementoRef)
                ?? throw new FileNotFoundException($"La referencia {elementoRef} no está en el índice.");

            if (!indice.Politica.Permite(e))
                throw new UnauthorizedAccessException("La política de esta instalación no permite abrir este material.");

            if (e.HuellaArchivo is null)
                throw new InvalidOperationException("Este elemento es estructura, no tiene archivo. Es el caso de las lecciones.");

            return AbrirPorHuella(e.PaqueteId, e.HuellaArchivo);
        }
    }

    /// <summary>
    /// Abre un archivo del paquete por su huella. Hace falta para los audios de
    /// instruccion hablada, que no son elementos del catalogo: cuelgan de una
    /// pregunta y solo existen dentro del manifiesto.
    /// </summary>
    public Stream AbrirPorHuella(string paqueteId, string huellaArchivo)
    {
        lock (_cerrojo)
        {
            var lector = abrirPaquete(paqueteId);
            var largo = LargoClaro(lector, huellaArchivo);
            var clave = lector.ClaveDelPaquete
                ?? throw new InvalidOperationException(
                    "El paquete esta verificado pero no abierto: no hay clave para descifrar sus medios.");

            // Se abre el archivo, no se lee entero. Un video de clase pesa cientos
            // de megabytes y el reproductor abre una peticion por cada salto:
            // cargarlo completo cada vez dejaria sin memoria al equipo del aula.
            var archivo = new FileStream(lector.RutaMedio(huellaArchivo), FileMode.Open,
                                         FileAccess.Read, FileShare.Read);
            try
            {
                return new StreamDeMedio(archivo, clave, Etiqueta(huellaArchivo), largo);
            }
            catch
            {
                // Si la cabecera no valida, el archivo NO puede quedarse abierto.
                // Esto se llama una vez por cada peticion de rango, asi que una
                // fuga aqui se acumula rapido y acaba bloqueando el archivo.
                archivo.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// La etiqueta con la que se derivo la clave de este archivo.
    ///
    /// Es la huella CON su extension original y SIN el .enc: al publicar, el
    /// empaquetador cifra usando el nombre del archivo en claro y solo despues
    /// le añade .enc al guardarlo. Poner aqui el .enc deriva otra clave y todos
    /// los medios fallan con error de autenticacion, que es un sintoma que no
    /// se parece en nada a su causa. Por eso la regla vive en un solo sitio.
    /// </summary>
    public static string Etiqueta(string huellaArchivo) =>
        huellaArchivo.EndsWith(".enc", StringComparison.Ordinal)
            ? huellaArchivo[..^4]
            : huellaArchivo;

    /// <summary>Tipo de medio a partir de la extension que quedo en la huella.</summary>
    public static string TipoDeContenido(string huellaArchivo) =>
        Path.GetExtension(huellaArchivo).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".html" or ".htm" => "text/html; charset=utf-8",
            // los que siguen solo aparecen dentro de un interactivo comprimido,
            // pero si no se declaran bien el navegador no aplica el estilo ni
            // ejecuta el guion, y el material se ve roto sin decir por que
            ".css" => "text/css; charset=utf-8",
            ".js" or ".mjs" => "text/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".ttf" => "font/ttf",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };

    private static long LargoClaro(LectorDePaquete lector, string huella)
    {
        foreach (var it in lector.Formato["payload_firmado"]!["inventario"]!.AsArray())
            if (it!["archivo"]!.GetValue<string>() == huella + ".enc")
                return it!["bytes_claro"]!.GetValue<long>();
        throw new FileNotFoundException($"El inventario no declara {huella}.");
    }
}
