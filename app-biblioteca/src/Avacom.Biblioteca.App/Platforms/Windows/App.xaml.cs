using Microsoft.UI.Xaml;

// Para saber mas sobre WinUI, ver https://aka.ms/winui

namespace Avacom.Biblioteca.App.WinUI;

/// <summary>
/// Punto de entrada de la aplicacion. El destino es el equipo maestro que va
/// dentro de la pantalla interactiva de 86 pulgadas, que es un Windows.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
