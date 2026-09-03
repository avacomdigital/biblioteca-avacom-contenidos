using Avacom.Contenido.Api;
using Avacom.Contenido.Indice;

namespace Avacom.Biblioteca.App;

/// <summary>
/// Levanta la API local y conecta el comando "mostrar" con la pantalla.
///
/// Existe porque la biblioteca no sabe que hay una pantalla. ApiLocal recibe una
/// funcion y la llama cuando el LMS pide mostrar algo; quien sabe abrir un visor
/// es la aplicacion, y ese cable se ata aqui.
///
/// SOBRE EL HILO
///
/// La peticion del LMS llega en un hilo del servidor, y tocar la interfaz desde
/// ahi cuelga la aplicacion o la tumba, segun el dia. Por eso el trabajo se
/// devuelve al hilo de la interfaz antes de abrir nada.
///
/// La respuesta al LMS se da en cuanto se acepta la peticion, no cuando el
/// material termina de cargarse. Un video de trescientos megabytes tarda, y
/// dejar al LMS esperando por eso lo bloquearia sin motivo.
/// </summary>
public sealed class PuenteConElLms : IDisposable
{
    private readonly BaseDeIndice _indice;
    private ApiLocal? _api;

    public PuenteConElLms(BaseDeIndice indice) => _indice = indice;

    public Action<string>? AlPedirMostrar { get; set; }

    public int Puerto => _api?.Puerto ?? 0;
    public bool Encendida => _api is not null;

    public void Encender()
    {
        if (_api is not null) return;
        _api = new ApiLocal(_indice, Mostrar);
    }

    /// <summary>
    /// Devuelve null si se acepta, o el motivo si no. El LMS recibe ese texto
    /// tal cual, asi que tiene que servirle a una persona: "no esta instalado"
    /// es util, "referencia nula" no.
    /// </summary>
    private string? Mostrar(string elementoRef)
    {
        var e = _indice.Elemento(elementoRef);
        if (e is null) return "Ese material no esta instalado en este equipo.";
        if (!_indice.Politica.Permite(e)) return "La politica de esta escuela no permite abrirlo.";
        if (AlPedirMostrar is null) return "La biblioteca todavia no esta lista para mostrar nada.";

        // Al hilo de la interfaz. Y sin esperar: se acepta la peticion y el
        // material se abre por su cuenta.
        MainThread.BeginInvokeOnMainThread(() => AlPedirMostrar?.Invoke(elementoRef));
        return null;
    }

    public void Dispose()
    {
        _api?.Dispose();
        _api = null;
    }
}
