using Avacom.Contenido.Cripto;
using Avacom.Contenido.Indice;
using Avacom.Contenido.Paquetes;

namespace Avacom.Biblioteca.App;

/// <summary>
/// Mantiene abiertos los paquetes que hacen falta y ni uno mas.
///
/// Importante: SQLite admite diez bases adjuntas a la vez, y un despliegue
/// universitario puede tener cuarenta paquetes instalados. Por eso los
/// manifiestos se abren A DEMANDA y se cierran al soltarlos. Navegar y buscar
/// se resuelven contra el indice, sin abrir ningun paquete.
/// </summary>
public sealed class GestorDePaquetes(BaseDeIndice indice) : IDisposable
{
    private readonly Dictionary<string, LectorDePaquete> _abiertos = new();
    private const int MaximoAbiertos = 4;

    public Licencia? Licencia { get; set; }
    public byte[]? NodoPrivada { get; set; }

    public LectorDePaquete Abrir(string paqueteId)
    {
        if (_abiertos.TryGetValue(paqueteId, out var y)) return y;

        if (_abiertos.Count >= MaximoAbiertos)
        {
            var viejo = _abiertos.Keys.First();
            _abiertos[viejo].Dispose();
            _abiertos.Remove(viejo);
        }

        var lector = new LectorDePaquete(RutaDe(paqueteId));
        var v = lector.Verificar(formatoSoportado: 2);
        if (!v.Aceptado) throw new InvalidOperationException(string.Join(" ", v.Motivos));

        var a = lector.Abrir(Licencia ?? throw new InvalidOperationException("No hay licencia instalada."),
                             NodoPrivada ?? throw new InvalidOperationException("No hay clave de nodo."));
        if (!a.Aceptado) throw new InvalidOperationException(string.Join(" ", a.Motivos));

        _abiertos[paqueteId] = lector;
        return lector;
    }

    private string RutaDe(string paqueteId)
    {
        using var cmd = indice.Conexion.CreateCommand();
        cmd.CommandText = "SELECT ruta_paquete FROM m04_paquete_instalado WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", paqueteId);
        return (string)(cmd.ExecuteScalar() ?? throw new FileNotFoundException($"Paquete {paqueteId} no instalado."));
    }

    public void Dispose()
    {
        foreach (var l in _abiertos.Values) l.Dispose();
        _abiertos.Clear();
        if (NodoPrivada is not null)
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(NodoPrivada);
    }
}
