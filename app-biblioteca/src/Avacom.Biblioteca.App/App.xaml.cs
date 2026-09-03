namespace Avacom.Biblioteca.App;

public partial class App : Application
{
    public App(IServiceProvider servicios)
    {
        InitializeComponent();
        Servicios = servicios;

        // El aula siempre es clara. El tema oscuro no aplica en una pantalla que
        // se mira a plena luz, y dejarlo automatico haria que la interfaz
        // cambiara de color segun la configuracion del equipo de cada colegio.
        UserAppTheme = AppTheme.Light;
    }

    /// <summary>
    /// El contenedor, para las pantallas que se abren fuera de una ruta de Shell.
    /// El visor se crea asi porque necesita servicios y se destruye al cerrarse.
    /// </summary>
    public static IServiceProvider Servicios { get; private set; } = null!;

    protected override Window CreateWindow(IActivationState? estado)
    {
        var v = new Window(new AppShell())
        {
            Title = "AVACOM Biblioteca",
        };

        // Tamaño de partida para trabajar en un monitor de escritorio. En la
        // pantalla interactiva la ventana va maximizada y esto no se aplica.
        v.Width = 1440;
        v.Height = 900;
        return v;
    }
}
