using System.Collections.ObjectModel;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Medios;
using Avacom.Contenido.Paquetes;
using Avacom.Contenido.Uso;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avacom.Biblioteca.App.ViewModels;

/// <summary>Una pregunta con el estado de lo que va contestando el alumno.</summary>
public partial class ReactivoViewModel(PreguntaVisible p, Func<string, string, bool> comprobar,
                                       Func<OpcionDeVoz, Task> escuchar) : ObservableObject
{
    [ObservableProperty] private string _respuesta = "";
    [ObservableProperty] private string _resultado = "";
    [ObservableProperty] private bool _acertado;
    [ObservableProperty] private bool _contestado;

    public string PreguntaRef => p.PreguntaRef;
    public string Enunciado => p.Enunciado;
    public string Numero => $"PREGUNTA {p.Orden}";
    public bool TieneVoz => p.Voz is not null;

    [RelayCommand]
    private void Comprobar()
    {
        if (string.IsNullOrWhiteSpace(Respuesta)) return;
        Acertado = comprobar(p.PreguntaRef, Respuesta);
        Contestado = true;
        // La retroalimentacion del manifiesto explica, no solo dice si esta bien.
        // Cuando el contenido no la trae, se dice lo minimo y no se inventa nada.
        Resultado = Acertado
            ? (p.Retroalimentacion ?? "Correcto")
            : "Todavia no. Vuelve a mirarlo y prueba otra vez.";
    }

    [RelayCommand]
    private async Task Escuchar()
    {
        if (p.Voz is not null) await escuchar(p.Voz);
    }
}

/// <summary>
/// El visor. Recibe una referencia de elemento y monta lo que haga falta segun
/// su tipo.
///
/// Lo importante de esta clase es lo que NO hace: nunca escribe el contenido
/// descifrado en un archivo. El video y el documento se sirven por el servidor
/// local, la imagen va directa a un flujo, y la actividad solo puede comparar
/// respuestas, no leerlas.
/// </summary>
public partial class VisorViewModel(
    BaseDeIndice indice, ResolutorDeMedios resolutor, ServidorDeMedios servidor,
    GestorDePaquetes gestor, RegistroDeUso uso) : ObservableObject
{
    private ElementoIndexado? _elemento;
    private string? _sesion;
    private string? _consumo;
    private readonly List<string> _fichas = new();

    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _tipo = "";
    [ObservableProperty] private string _detalle = "";
    [ObservableProperty] private string _problema = "";
    [ObservableProperty] private bool _cargando = true;

    [ObservableProperty] private ImageSource? _imagen;
    [ObservableProperty] private string? _urlMedio;      // video y audio
    [ObservableProperty] private string? _urlWeb;        // documento e interactivo

    public ObservableCollection<ReactivoViewModel> Reactivos { get; } = new();
    public ObservableCollection<PasoDeLeccion> Pasos { get; } = new();

    public bool EsImagen => Tipo == "imagen";
    public bool EsMedio => Tipo is "video" or "audio";
    public bool EsWeb => Tipo is "documento" or "interactivo";
    public bool EsActividad => Tipo is "actividad" or "evaluacion";
    public bool EsLeccion => Tipo == "leccion";
    public bool HayProblema => !string.IsNullOrEmpty(Problema);

    public void Preparar(ElementoIndexado e, string sesion)
    {
        _elemento = e;
        _sesion = string.IsNullOrEmpty(sesion) ? uso.AbrirSesion() : sesion;
        Titulo = e.Titulo;
        Tipo = e.Tipo;
        Detalle = string.Join("  ·  ", new[] { e.Asignatura, e.Grado, e.Nivel }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    [RelayCommand]
    public async Task Cargar()
    {
        if (_elemento is null) return;
        Cargando = true;
        Problema = "";
        try
        {
            // Queda constancia de que se abrio. Es lo unico que deja el repaso:
            // ni intento ni calificacion.
            _consumo = uso.RegistrarApertura(_sesion!, _elemento.ElementoRef, _elemento.VersionElemento);

            switch (_elemento.Tipo)
            {
                case "imagen":
                    // ImageSource pide una fabrica de flujos, no un flujo: MAUI puede
                    // volver a pedirlo si la vista se recicla.
                    var er = _elemento.ElementoRef;
                    Imagen = ImageSource.FromStream(() => resolutor.Abrir(er));
                    break;

                case "video":
                case "audio":
                    UrlMedio = Publicar(_elemento);
                    break;

                case "documento":
                    UrlWeb = Publicar(_elemento);
                    break;

                case "interactivo":
                {
                    // El interactivo viene comprimido. Se despliega en memoria y se
                    // sirve por el mismo servidor local, nunca a un disco.
                    using var zip = resolutor.Abrir(_elemento.ElementoRef);
                    var url = servidor.PublicarComprimido(zip);
                    _fichas.Add(url);
                    UrlWeb = url;
                    break;
                }

                case "actividad":
                case "evaluacion":
                    CargarReactivos(_elemento);
                    break;

                case "leccion":
                {
                    var lectura = new LecturaDeManifiesto(gestor.Abrir(_elemento.PaqueteId));
                    Pasos.Clear();
                    foreach (var p in lectura.Leccion(_elemento.ElementoRef)) Pasos.Add(p);
                    break;
                }

                default:
                    Problema = $"Todavia no hay visor para el tipo \"{_elemento.Tipo}\".";
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            Problema = "La politica de esta instalacion no permite abrir este material.";
        }
        catch (Exception e)
        {
            Problema = $"No se pudo abrir: {e.Message}";
        }
        finally
        {
            Cargando = false;
            Avisar();
        }
        await Task.CompletedTask;
    }

    private string Publicar(ElementoIndexado e)
    {
        var url = servidor.Publicar(() => resolutor.Abrir(e.ElementoRef),
                                    ResolutorDeMedios.TipoDeContenido(e.HuellaArchivo ?? ""));
        _fichas.Add(url);
        return url;
    }

    private void CargarReactivos(ElementoIndexado e)
    {
        var lector = gestor.Abrir(e.PaqueteId);
        var lectura = new LecturaDeManifiesto(lector);
        Reactivos.Clear();
        foreach (var p in lectura.Preguntas(e.ElementoRef))
            Reactivos.Add(new ReactivoViewModel(p, lectura.Acierta,
                v => EscucharVoz(e.PaqueteId, v)));
    }

    private Task EscucharVoz(string paqueteId, OpcionDeVoz voz)
    {
        var url = servidor.Publicar(() => resolutor.AbrirPorHuella(paqueteId, voz.HuellaArchivo),
                                    ResolutorDeMedios.TipoDeContenido(voz.HuellaArchivo));
        _fichas.Add(url);
        UrlVoz = url;
        return Task.CompletedTask;
    }

    [ObservableProperty] private string? _urlVoz;

    /// <summary>
    /// Cierre. Se anulan las fichas del servidor y se cierra el consumo.
    /// Si esto no se llamara, el material seguiria alcanzable en el puerto local
    /// despues de haber salido de la pantalla.
    /// </summary>
    public void Cerrar(int progreso = 100)
    {
        foreach (var f in _fichas) servidor.Retirar(f);
        _fichas.Clear();
        if (_consumo is not null) uso.RegistrarCierre(_consumo, progreso);
        _consumo = null;
        Imagen = null;
        UrlMedio = UrlWeb = UrlVoz = null;
    }

    private void Avisar()
    {
        OnPropertyChanged(nameof(EsImagen));
        OnPropertyChanged(nameof(EsMedio));
        OnPropertyChanged(nameof(EsWeb));
        OnPropertyChanged(nameof(EsActividad));
        OnPropertyChanged(nameof(EsLeccion));
        OnPropertyChanged(nameof(HayProblema));
    }
}
