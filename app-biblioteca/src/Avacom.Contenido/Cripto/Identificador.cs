namespace Avacom.Contenido.Cripto;

/// <summary>
/// Identificador de cinco segmentos, unico en el mundo sin coordinacion central.
/// Instalacion, nodo, emisor, contador y digito de verificacion.
///
/// En el componente de biblioteca la instalacion y el nodo salen de la
/// configuracion local. Cuando llegue el LMS completo, esto lo emite MOD-015.
/// </summary>
public static class Identificador
{
    public static string Instalacion { get; set; } = "INST001";
    public static string Nodo { get; set; } = "N01";

    /// <summary>
    /// El contador NO arranca en cero, y esa es la parte importante.
    ///
    /// Arrancando en cero, cada ejecucion del proceso volvia a emitir
    /// INST001-N01-RS-00000001-xx, porque los otros tres segmentos son
    /// constantes. La primera vez, con la base limpia, entraba; en el segundo
    /// arranque chocaba contra la clave primaria y la excepcion de SQLite
    /// tumbaba la aplicacion antes de pintar la ventana. Costo un dia
    /// encontrarlo porque el sintoma era "no abre y no dice nada".
    ///
    /// Sembrarlo con el reloj hace que dos ejecuciones distintas no empiecen
    /// en el mismo punto. Queda un resto conocido: dos procesos que arranquen
    /// dentro del MISMO SEGUNDO en el mismo nodo volverian a coincidir. Para un
    /// equipo maestro de aula, que corre un solo proceso, no ocurre. El dia que
    /// eso deje de ser cierto, la solucion no es ampliar este parche: es que
    /// Instalacion y Nodo dejen de estar fijos, que es lo que el comentario de
    /// arriba ya anticipa.
    ///
    /// Ocho digitos alcanzan para unos tres anos de segundos antes de dar la
    /// vuelta, y al dar la vuelta el riesgo vuelve a ser el de coincidir con
    /// una ejecucion de hace tres anos, no con la de al lado.
    /// </summary>
    private static long _contador = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100_000_000L;

    public static string Nuevo(string emisor)
    {
        var n = Interlocked.Increment(ref _contador);
        var b = $"{Instalacion}-{Nodo}-{emisor}-{n:D8}";
        int dv = b.Sum(c => (int)c) % 97;
        return $"{b}-{dv:D2}";
    }
}
