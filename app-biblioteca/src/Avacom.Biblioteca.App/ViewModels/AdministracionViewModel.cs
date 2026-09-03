using System.Collections.ObjectModel;
using Avacom.Contenido.Indice;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;

namespace Avacom.Biblioteca.App.ViewModels;

public sealed record PaqueteEnLista(string Id, string Clave, string Version, string Asignatura,
                                    string Nivel, string Grado, int Elementos)
{
    public string Resumen => $"{Elementos} elementos  ·  {Nivel} {Grado}".Trim();
}

public sealed record PoliticaEnLista(string Id, string Ambito, string Valor, string Accion);

/// <summary>
/// La consola del administrador. Tres cosas y ninguna mas: de donde sale el
/// contenido, que hay instalado, y que se ha desactivado.
///
/// Deliberadamente no hay nada de alumnos ni de calificaciones. Este componente
/// es la biblioteca; lo academico es del LMS. Mezclarlos aqui seria empezar a
/// construir un segundo LMS por accidente.
/// </summary>
public partial class AdministracionViewModel(BaseDeIndice indice, EstadoDelNodo nodo) : ObservableObject
{
    [ObservableProperty] private string _carpeta = "";
    [ObservableProperty] private string _estado = "";
    [ObservableProperty] private bool _listo;
    [ObservableProperty] private string _registro = "";
    [ObservableProperty] private string _ambitoNuevo = "asignatura";
    [ObservableProperty] private string _valorNuevo = "";

    public ObservableCollection<PaqueteEnLista> Paquetes { get; } = new();
    public ObservableCollection<PoliticaEnLista> Politicas { get; } = new();
    // Estos valores NO son libres: son exactamente los que compara
    // Politica.Permite. Poner "nivel_clave" en vez de "nivel" haria que la
    // regla se guardara, se listara en pantalla y no filtrara nada.
    public string[] Ambitos { get; } = ["asignatura", "nivel", "grado", "paquete", "elemento", "taxonomia"];

    [RelayCommand]
    public void Cargar()
    {
        Carpeta = nodo.Carpeta ?? "";
        if (!string.IsNullOrEmpty(Carpeta) && !nodo.Listo) nodo.Cargar(Carpeta);
        Refrescar();
    }

    [RelayCommand]
    private async Task ElegirCarpeta()
    {
        // Se pide el archivo de licencia y de ahi se deduce la carpeta de trabajo.
        // Es mas fiable que pedir una carpeta: si el archivo esta, la estructura
        // que hay alrededor tambien.
        try
        {
            var r = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Elige el archivo licencia.json de este equipo",
            });
            if (r is null) return;

            var lic = Path.GetDirectoryName(r.FullPath);
            var trabajo = Path.GetDirectoryName(lic);
            if (trabajo is null) { Estado = "No se pudo deducir la carpeta de trabajo."; return; }

            Aplicar(trabajo);
        }
        catch (Exception e)
        {
            Estado = $"No se pudo elegir: {e.Message}";
        }
    }

    [RelayCommand]
    private void UsarRuta()
    {
        if (string.IsNullOrWhiteSpace(Carpeta)) return;
        Aplicar(Carpeta.Trim());
    }

    private void Aplicar(string trabajo)
    {
        Carpeta = trabajo;
        Listo = nodo.Cargar(trabajo);
        Estado = nodo.Resumen;
        Refrescar();
    }

    [RelayCommand]
    private void Instalar()
    {
        if (string.IsNullOrWhiteSpace(Carpeta)) { Estado = "Primero elige la carpeta de trabajo."; return; }

        var pub = Path.Combine(Carpeta, "pub");
        var r = nodo.InstalarDesde(pub);
        Registro = r.Count == 0
            ? $"No hay ningun paquete en {pub}"
            : string.Join("\n", r.Select(x => (x.Aceptado ? "instalado   " : "rechazado   ") + x.Carpeta + "\n              " + x.Detalle));
        Refrescar();
    }

    [RelayCommand]
    private void Desactivar()
    {
        if (string.IsNullOrWhiteSpace(ValorNuevo)) return;
        Instalador.Politica(indice, AmbitoNuevo, ValorNuevo.Trim(), "deshabilitar");
        ValorNuevo = "";
        Refrescar();
    }

    [RelayCommand]
    private void QuitarPoliticas()
    {
        Instalador.QuitarPoliticas(indice);
        Refrescar();
    }

    [RelayCommand]
    private void Retirar(PaqueteEnLista? p)
    {
        if (p is null) return;
        Instalador.Desinstalar(indice, p.Id);
        Registro = $"Retirado {p.Clave}. El registro de uso se conserva: lo que se consulto ocurrio.";
        Refrescar();
    }

    private void Refrescar()
    {
        Listo = nodo.Listo;
        Estado = nodo.Resumen;

        Paquetes.Clear();
        Politicas.Clear();
        if (!Existe("m04_paquete_instalado")) return;

        using (var c = indice.Conexion.CreateCommand())
        {
            c.CommandText = """
                SELECT p.id, p.clave_paquete, p.version, COALESCE(p.asignatura,''),
                       COALESCE(p.nivel_clave,''), COALESCE(p.grado,''),
                       (SELECT count(*) FROM m04_indice_elemento e WHERE e.paquete_id = p.id)
                FROM m04_paquete_instalado p WHERE p.estado='activo' ORDER BY p.secuencia
                """;
            using var r = c.ExecuteReader();
            while (r.Read())
                Paquetes.Add(new PaqueteEnLista(r.GetString(0), r.GetString(1), r.GetString(2),
                    r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6)));
        }

        using (var c = indice.Conexion.CreateCommand())
        {
            c.CommandText = "SELECT id,ambito,ambito_valor,accion FROM m04_politica ORDER BY secuencia";
            using var r = c.ExecuteReader();
            while (r.Read())
                Politicas.Add(new PoliticaEnLista(r.GetString(0), r.GetString(1),
                    r.GetString(2), r.GetString(3)));
        }
    }

    private bool Existe(string tabla)
    {
        using var c = indice.Conexion.CreateCommand();
        c.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$n";
        c.Parameters.AddWithValue("$n", tabla);
        return Convert.ToInt64(c.ExecuteScalar()) > 0;
    }
}
