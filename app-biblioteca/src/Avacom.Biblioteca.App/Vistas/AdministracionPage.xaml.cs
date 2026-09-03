using Avacom.Biblioteca.App.ViewModels;

namespace Avacom.Biblioteca.App.Vistas;

public partial class AdministracionPage : ContentPage
{
    private readonly AdministracionViewModel _vm;

    public AdministracionPage(AdministracionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.CargarCommand.Execute(null);
    }

    /// <summary>
    /// Retirar un paquete se confirma. Es la unica accion de esta pantalla que
    /// quita algo, y en un aula se toca por error mas de lo que uno espera.
    /// </summary>
    private async void AlRetirar(object? emisor, EventArgs e)
    {
        if ((emisor as BindableObject)?.BindingContext is not PaqueteEnLista p) return;

        var si = await DisplayAlert(
            "Retirar contenido",
            $"Se va a retirar {p.Asignatura} ({p.Clave}). Deja de verse en el catalogo.\n\n" +
            "El registro de uso se conserva: lo que se consulto ocurrio y sigue siendo cierto.",
            "Retirar", "Cancelar");

        if (si) _vm.RetirarCommand.Execute(p);
    }
}
