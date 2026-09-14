using Avacom.Contenido.Indice;
using Avacom.Contenido.Paquetes;

namespace Avacom.Contenido.Api;

/// <summary>Un flujo descifrado listo para servir, con su tipo de contenido.</summary>
public sealed record MedioAbierto(Stream Flujo, string TipoContenido);

/// <summary>
/// Lo que la API local necesita del contenido descifrado para publicar las
/// capacidades opcionales (medio, leccion, evaluacion, comprobar, voz), sin
/// saber como se abre un paquete ni donde vive la clave.
///
/// POR QUE UNA INTERFAZ
///
/// ApiLocal vive en la biblioteca sin interfaz y se prueba sola. Abrir un
/// paquete de verdad exige licencia, clave de nodo y archivos cifrados en disco;
/// eso lo tiene la aplicacion, no la prueba. Con esta frontera la aplicacion
/// enchufa el resolutor de medios y el gestor de paquetes, y las pruebas
/// enchufan una fuente en memoria. La API no distingue una de otra.
///
/// LO QUE NUNCA SALE POR AQUI
///
/// La clave de respuesta. Solo se puede COMPARAR (Acierta), nunca leer. Es la
/// misma regla que gobierna LecturaDeManifiesto, y por eso esta interfaz la
/// reutiliza en vez de exponer la conexion del manifiesto.
/// </summary>
public interface IFuenteDeContenido
{
    /// <summary>El archivo de un elemento (imagen, video, audio, documento o zip de interactivo).</summary>
    MedioAbierto AbrirMedio(ElementoIndexado e);

    /// <summary>Un archivo de dentro de un interactivo comprimido. Null si no existe.</summary>
    MedioAbierto? AbrirArchivoInterno(ElementoIndexado e, string rutaInterna);

    /// <summary>Las preguntas de una evaluacion o actividad, sin clave.</summary>
    IReadOnlyList<PreguntaVisible> Preguntas(ElementoIndexado e);

    /// <summary>Referencias de las preguntas que tienen clave y por tanto se pueden comprobar.</summary>
    ISet<string> Corregibles(ElementoIndexado e);

    /// <summary>Compara sin revelar. Tiempo constante.</summary>
    bool Acierta(ElementoIndexado e, string preguntaRef, string respuesta);

    /// <summary>Los pasos de una leccion (secuencia del tema).</summary>
    IReadOnlyList<PasoDeLeccion> Leccion(ElementoIndexado e);

    /// <summary>La instruccion hablada del elemento entero o de una pregunta, si la trae.</summary>
    OpcionDeVoz? Voz(ElementoIndexado e, string? preguntaRef);

    MedioAbierto AbrirVoz(ElementoIndexado e, OpcionDeVoz voz);
}
