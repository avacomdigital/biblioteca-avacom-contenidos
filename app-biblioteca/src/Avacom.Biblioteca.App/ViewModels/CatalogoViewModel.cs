using System.Collections.ObjectModel;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Uso;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avacom.Biblioteca.App.ViewModels;

/// <summary>
/// El catalogo. Se navega por taxonomia, que es la estructura curricular que
/// trae el propio contenido, no una que el componente invente.
///
/// Y solo lista lo que hay instalado. Un catalogo que enseña lo que no esta
/// convierte cada clase en una promesa incumplida delante de treinta alumnos.
/// </summary>
public partial class CatalogoViewModel(BaseDeIndice indice, RegistroDeUso uso, EstadoDelNodo nodo)
    : ObservableObject
{
    [ObservableProperty] private string? _nivelFiltro;
    [ObservableProperty] private string? _asignaturaFiltro;
    [ObservableProperty] private NodoTaxonomia? _nodoActual;
    [ObservableProperty] private string _rutaTexto = "Todo el contenido";
    [ObservableProperty] private bool _hayLicencia;
    [ObservableProperty] private string _aviso = "";

    public ObservableCollection<NodoTaxonomia> Ruta { get; } = new();
    public ObservableCollection<NodoTaxonomia> Hijos { get; } = new();
    public ObservableCollection<ElementoIndexado> Elementos { get; } = new();

    private string? _sesionRepaso;

    public bool PuedeVolver => Ruta.Count > 0;

    [RelayCommand]
    public void Cargar()
    {
        // ORDEN IMPORTANTE.
        //
        // Esta es la primera pantalla que ve la aplicacion al arrancar, y en un
        // equipo recien instalado el indice esta VACIO: no hay ni tablas, porque
        // el esquema lo aplica EstadoDelNodo.Cargar y todavia no se ha elegido
        // carpeta de trabajo. Cualquier consulta antes de comprobarlo revienta
        // con "no such table", y como esto lo llama OnAppearing, la excepcion se
        // lleva por delante el arranque entero: el profesor no llega a ver ni el
        // aviso que esta pantalla tiene preparado ni la pestaña de
        // Administracion, que es justo donde tendria que ir a arreglarlo.
        if (nodo.Carpeta is not null && !nodo.Listo) nodo.Cargar(nodo.Carpeta);
        HayLicencia = nodo.Listo;
        Aviso = HayLicencia ? "" : nodo.Resumen;

        if (!HayLicencia)
        {
            Ruta.Clear();
            Hijos.Clear();
            Elementos.Clear();
            NodoActual = null;
            RutaTexto = "Todo el contenido";
            OnPropertyChanged(nameof(PuedeVolver));
            return;
        }

        // Toda sesion de navegacion es modo repaso: deja rastro de uso y nada mas.
        // Ni intentos, ni calificaciones. Lo decidio producto y se cumple aqui.
        _sesionRepaso ??= uso.AbrirSesion();

        Refrescar(NodoActual?.TaxonomiaRef);
    }

    [RelayCommand]
    public void Entrar(NodoTaxonomia? n)
    {
        if (n is null) return;
        Ruta.Add(n);
        NodoActual = n;
        Refrescar(n.TaxonomiaRef);
    }

    [RelayCommand]
    public void Volver()
    {
        if (Ruta.Count == 0) return;
        Ruta.RemoveAt(Ruta.Count - 1);
        NodoActual = Ruta.LastOrDefault();
        Refrescar(NodoActual?.TaxonomiaRef);
    }

    /// <summary>Resuelve una referencia. La usa el puente cuando el LMS pide mostrar algo.</summary>
    public ElementoIndexado? Buscar(string elementoRef) => indice.Elemento(elementoRef);

    /// <summary>
    /// Se dispara cuando el profesor toca un material. El modelo no navega: no
    /// sabe que existe un visor ni una pila de paginas. Solo dice que se ha
    /// pedido abrir esto, y la pagina decide como se muestra.
    /// </summary>
    public event Action<ElementoIndexado>? SolicitaAbrir;

    [RelayCommand]
    public void Abrir(ElementoIndexado? e)
    {
        if (e is not null) SolicitaAbrir?.Invoke(e);
    }

    [RelayCommand]
    public void Inicio()
    {
        Ruta.Clear();
        NodoActual = null;
        Refrescar(null);
    }

    private void Refrescar(string? padre)
    {
        Hijos.Clear();
        foreach (var h in indice.Taxonomia(padre)) Hijos.Add(h);

        Elementos.Clear();
        // Disponibles ya aplica la politica del administrador. Nunca se lista
        // algo que la escuela desactivo, ni siquiera atenuado.
        foreach (var e in indice.Disponibles(NivelFiltro, AsignaturaFiltro))
            if (padre is null || e.TaxonomiaRef == padre) Elementos.Add(e);

        RutaTexto = Ruta.Count == 0
            ? "Todo el contenido"
            : "Todo el contenido  ›  " + string.Join("  ›  ", Ruta.Select(r => r.Nombre));

        OnPropertyChanged(nameof(PuedeVolver));
    }

    public string SesionRepaso => _sesionRepaso ?? "";
}
