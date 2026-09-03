using Avacom.Biblioteca.App.ViewModels;
using Avacom.Contenido.Indice;

namespace Avacom.Biblioteca.App.Vistas;

public partial class CatalogoPage : ContentPage
{
    private readonly CatalogoViewModel _vm;
    private bool _abriendo;

    private readonly PuenteConElLms _puente;

    public CatalogoPage(CatalogoViewModel vm, PuenteConElLms puente)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _puente = puente;
        _vm.SolicitaAbrir += Abrir;

        // Cuando el LMS pide mostrar algo, llega aqui. Es la misma via que un
        // toque del profesor, asi que el material se abre igual y deja el mismo
        // rastro de uso: al componente le da igual quien lo pidio.
        _puente.AlPedirMostrar = r =>
        {
            var e = _vm.Buscar(r);
            if (e is not null) Abrir(e);
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _abriendo = false;
        _vm.CargarCommand.Execute(null);

        // Se enciende una vez, y solo cuando hay licencia: sin ella el indice
        // esta vacio y el LMS se llevaria un catalogo en blanco creyendolo real.
        if (_vm.HayLicencia && !_puente.Encendida) _puente.Encender();
    }

    // Los toques dentro de una plantilla llegan aqui en vez de por enlace. El
    // objeto de datos de la tarjeta viaja en el contexto del gesto, que lo
    // hereda del borde que lo contiene.
    private void AlTocarRama(object? emisor, TappedEventArgs e)
    {
        if (Contexto<NodoTaxonomia>(emisor) is { } n) _vm.EntrarCommand.Execute(n);
    }

    private void AlTocarMaterial(object? emisor, TappedEventArgs e)
    {
        if (Contexto<ElementoIndexado>(emisor) is { } m) _vm.AbrirCommand.Execute(m);
    }

    private static T? Contexto<T>(object? emisor) where T : class =>
        (emisor as BindableObject)?.BindingContext as T;

    /// <summary>
    /// Navegar es cosa de la vista, no del modelo. El modelo solo avisa de que
    /// se ha pedido abrir un material.
    /// </summary>
    private async void Abrir(ElementoIndexado e)
    {
        // En una pantalla tactil grande un toque se registra dos veces con mas
        // facilidad de la que parece. Sin este cerrojo se abren dos visores.
        if (_abriendo) return;
        _abriendo = true;
        try
        {
            var pagina = App.Servicios.GetRequiredService<VisorPage>();
            pagina.Preparar(e, _vm.SesionRepaso);
            await Navigation.PushAsync(pagina);
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo abrir", ex.Message, "Entendido");
        }
        finally
        {
            _abriendo = false;
        }
    }
}
