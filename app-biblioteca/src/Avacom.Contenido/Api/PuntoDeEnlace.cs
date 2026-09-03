using System.Security.Cryptography;
using System.Text.Json;

namespace Avacom.Contenido.Api;

/// <summary>
/// Como encuentra el LMS a este componente.
///
/// EL PROBLEMA
///
/// La API local escucha en un puerto que elige el sistema, distinto en cada
/// arranque. Eso es deliberado: un puerto fijo y conocido es un punto que
/// sondear, y ademas choca cuando dos cosas quieren el mismo numero. Pero
/// entonces el LMS no sabe a donde llamar.
///
/// LA SOLUCION
///
/// Al arrancar, el componente deja una nota en un sitio acordado con el puerto
/// del dia y una ficha de acceso. El LMS lee esa nota y ya sabe a donde ir y
/// con que identificarse. Al cerrarse, el componente borra la nota.
///
/// La ficha no es para proteger secretos, que los medios ya van cifrados por su
/// cuenta. Es para que solo un programa que corre en este equipo, con permiso
/// para leer esa carpeta, pueda preguntar. Cualquier otra cosa que dispare al
/// puerto recibe un 401 y nada mas.
///
/// LO QUE ESTO NO ES
///
/// No es un mecanismo de autenticacion de personas. El LMS decide quien puede
/// ver que; este componente solo responde a quien tenga la ficha del equipo.
/// </summary>
public static class PuntoDeEnlace
{
    /// <summary>
    /// Version del contrato. Sube cuando cambia la forma de una respuesta de
    /// manera que rompa a quien ya la lee. El LMS debe comprobarla y negarse a
    /// hablar con un numero que no entienda, en vez de intentarlo y fallar raro.
    /// </summary>
    public const int Contrato = 1;

    /// <summary>
    /// Donde se deja la nota. En datos de programa y no en el perfil del
    /// usuario, porque el LMS puede correr con otra cuenta que el visor.
    /// </summary>
    public static string Ruta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AVACOM", "contenido", "enlace.json");

    public sealed record Nota(int Contrato, int Puerto, string Ficha, int Proceso);

    /// <summary>Deja la nota. Devuelve la ficha que hay que exigir en cada peticion.</summary>
    public static string Publicar(int puerto)
    {
        var ficha = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var carpeta = Path.GetDirectoryName(Ruta)!;
        Directory.CreateDirectory(carpeta);

        File.WriteAllText(Ruta, JsonSerializer.Serialize(
            new Nota(Contrato, puerto, ficha, Environment.ProcessId)));

        return ficha;
    }

    /// <summary>
    /// Lee la nota. Devuelve null si no hay componente escuchando, que es un
    /// estado normal y no un error: el LMS tiene que saber funcionar sin
    /// contenido instalado.
    /// </summary>
    public static Nota? Leer()
    {
        try
        {
            if (!File.Exists(Ruta)) return null;
            return JsonSerializer.Deserialize<Nota>(File.ReadAllText(Ruta));
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    /// <summary>Retira la nota al cerrar. Una nota que apunta a un puerto muerto confunde mas que su ausencia.</summary>
    public static void Retirar()
    {
        try { if (File.Exists(Ruta)) File.Delete(Ruta); }
        catch (IOException) { }
    }
}
