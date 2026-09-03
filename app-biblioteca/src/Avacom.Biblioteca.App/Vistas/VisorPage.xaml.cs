using System.ComponentModel;
using Avacom.Biblioteca.App.ViewModels;
using Avacom.Contenido.Indice;
using CommunityToolkit.Maui.Views;

namespace Avacom.Biblioteca.App.Vistas;

public partial class VisorPage : ContentPage
{
    private readonly VisorViewModel _vm;

    public VisorPage(VisorViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += AlCambiarModelo;
    }

    public void Preparar(ElementoIndexado e, string sesion) => _vm.Preparar(e, sesion);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CargarCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        // Primero se paran los reproductores y solo despues se anulan las fichas.
        // Al reves, el reproductor pediria el siguiente trozo de algo que ya no
        // existe y dejaria un error en pantalla al cerrar.
        Reproductor.Stop();
        Reproductor.Source = null;
        Voz.Stop();
        Voz.Source = null;
        Navegador.Source = new HtmlWebViewSource { Html = "<html><body></body></html>" };

        _vm.Cerrar();
        base.OnDisappearing();
    }

    /// <summary>
    /// Las direcciones se aplican desde codigo porque MediaSource y
    /// UrlWebViewSource no son texto: hay que construirlos.
    /// </summary>
    private void AlCambiarModelo(object? emisor, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VisorViewModel.UrlMedio) when _vm.UrlMedio is not null:
                Reproductor.Source = MediaSource.FromUri(_vm.UrlMedio);
                break;

            case nameof(VisorViewModel.UrlWeb) when _vm.UrlWeb is not null:
                Navegador.Source = new UrlWebViewSource { Url = _vm.UrlWeb };
                break;

            case nameof(VisorViewModel.UrlVoz) when _vm.UrlVoz is not null:
                Voz.Stop();
                Voz.Source = MediaSource.FromUri(_vm.UrlVoz);
                Voz.Play();
                break;
        }
    }

    private async void AlCerrar(object? emisor, EventArgs e) => await Navigation.PopAsync();
}
