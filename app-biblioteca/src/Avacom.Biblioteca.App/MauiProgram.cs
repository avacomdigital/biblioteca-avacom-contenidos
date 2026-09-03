using Avacom.Contenido.Indice;
using Avacom.Contenido.Medios;
using Avacom.Contenido.Uso;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Avacom.Biblioteca.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var b = MauiApp.CreateBuilder();
        b.UseMauiApp<App>()
         // El destino unico es Windows: no hace falta servicio en primer plano
         // de Android, pero la version 10.x de MediaElement exige el parametro.
         .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
         .ConfigureFonts(_ =>
         {
             // Se usa la tipografia del sistema a proposito, y por eso aqui no se
             // registra ninguna. Es la unica que esta garantizada en el equipo
             // maestro de cualquier pantalla, y una fuente que no carga en un aula
             // sin conexion deja la interfaz ilegible. Lo que de verdad importa a
             // cuatro metros son los tamaños, y esos estan en
             // Resources/Styles/Estilos.xaml.
             //
             // Si mas adelante se adopta una tipografia de marca, se pone el .ttf
             // en Resources/Fonts y se añade aqui su AddFont.
         });

        var carpeta = Path.Combine(FileSystem.AppDataDirectory, "avacom");
        Directory.CreateDirectory(carpeta);

        b.Services.AddSingleton(_ => new BaseDeIndice(Path.Combine(carpeta, "indice.db")));
        b.Services.AddSingleton<GestorDePaquetes>();
        b.Services.AddSingleton<EstadoDelNodo>();
        b.Services.AddSingleton<ResolutorDeMedios>(sp =>
        {
            var idx = sp.GetRequiredService<BaseDeIndice>();
            var gp = sp.GetRequiredService<GestorDePaquetes>();
            return new ResolutorDeMedios(idx, gp.Abrir);
        });
        b.Services.AddSingleton(sp => new RegistroDeUso(sp.GetRequiredService<BaseDeIndice>().Conexion));

        // Un solo servidor para toda la aplicacion. Escucha en 127.0.0.1 con un
        // puerto que elige el sistema, y es lo que permite reproducir un video
        // cifrado sin dejarlo nunca en claro en el disco.
        b.Services.AddSingleton<ServidorDeMedios>();

        // La puerta por la que el LMS pregunta que contenido hay. Se enciende
        // desde el catalogo, cuando ya hay indice: antes no habria nada que
        // responder y el LMS leeria un catalogo vacio creyendolo bueno.
        b.Services.AddSingleton<PuenteConElLms>();

        b.Services.AddSingleton<ViewModels.CatalogoViewModel>();
        b.Services.AddSingleton<ViewModels.AdministracionViewModel>();
        b.Services.AddTransient<ViewModels.VisorViewModel>();

        b.Services.AddSingleton<Vistas.CatalogoPage>();
        b.Services.AddSingleton<Vistas.AdministracionPage>();
        b.Services.AddSingleton<Vistas.ScormPage>();
        b.Services.AddSingleton<Vistas.PropioPage>();
        b.Services.AddTransient<Vistas.VisorPage>();


#if DEBUG
        b.Logging.AddDebug();
#endif
        return b.Build();
    }
}
