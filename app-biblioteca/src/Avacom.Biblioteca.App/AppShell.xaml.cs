namespace Avacom.Biblioteca.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // El visor no es una pestaña: se abre encima de lo que haya y se cierra
        // devolviendo al sitio exacto del que se salio. Un profesor que abre una
        // lamina y vuelve no puede perder el punto del arbol donde estaba.
        Routing.RegisterRoute("visor", typeof(Vistas.VisorPage));
    }
}
