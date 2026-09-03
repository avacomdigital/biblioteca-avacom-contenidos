using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Avacom.Contenido.Cripto;
using Microsoft.Data.Sqlite;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Avacom.Contenido.Paquetes;

public sealed record ResultadoVerificacion(bool Aceptado, IReadOnlyList<string> Motivos)
{
    public static ResultadoVerificacion Ok() => new(true, Array.Empty<string>());
    public static ResultadoVerificacion No(params string[] m) => new(false, m);
}

/// <summary>
/// Lee un paquete publicado. Hace las seis comprobaciones antes de tocar nada,
/// y solo después descifra el manifiesto usando la clave que saca de la licencia.
///
/// El manifiesto descifrado NUNCA se escribe en disco. Se abre en una base en
/// memoria y se descarta al cerrar. Es lo que evita que las claves de respuesta
/// de todos los exámenes acaben en un archivo temporal.
/// </summary>
public sealed class LectorDePaquete : IDisposable
{
    private readonly string _carpeta;
    private SqliteConnection? _manifiesto;

    public JsonNode Formato { get; }
    public string ClavePaquete => Formato["clave_paquete"]!.GetValue<string>();
    public string Version => Formato["version"]!.GetValue<string>();
    public int FormatoVersion => Formato["formato_version"]!.GetValue<int>();
    public JsonNode Vitrina => Formato["vitrina"]!;
    public byte[]? ClaveDelPaquete { get; private set; }

    public LectorDePaquete(string carpeta)
    {
        _carpeta = carpeta;
        Formato = JsonNode.Parse(File.ReadAllText(Path.Combine(carpeta, "formato.json")))!;
    }

    /// <summary>
    /// Las seis comprobaciones del contrato, en orden. Ninguna se salta y todas
    /// ocurren antes de copiar o descifrar un solo byte.
    /// </summary>
    public ResultadoVerificacion Verificar(int formatoSoportado, byte[]? emisorEsperado = null)
    {
        var motivos = new List<string>();

        // 1 · formato
        if (FormatoVersion != formatoSoportado)
            return ResultadoVerificacion.No($"Esta versión del componente no entiende el formato {FormatoVersion}.");

        // 2 · firma
        var pub = Convert.FromHexString(Formato["clave_publica"]!.GetValue<string>());
        if (emisorEsperado is not null && !pub.SequenceEqual(emisorEsperado))
            motivos.Add("El paquete está firmado por un emisor que no reconocemos.");
        var payload = Canonico.Serializar(Formato["payload_firmado"]!);
        var firma = File.ReadAllBytes(Path.Combine(_carpeta, "firma.sig"));
        var v = new Ed25519Signer();
        v.Init(false, new Ed25519PublicKeyParameters(pub, 0));
        v.BlockUpdate(payload, 0, payload.Length);
        if (!v.VerifySignature(firma)) motivos.Add("La firma del paquete no es válida.");

        // 3 · el manifiesto coincide con su huella firmada
        var manifCif = File.ReadAllBytes(Path.Combine(_carpeta, "manifiesto.enc"));
        var esperada = Formato["payload_firmado"]!["huella_manifiesto_cifrado"]!.GetValue<string>();
        if (Huella(manifCif) != esperada) motivos.Add("El manifiesto no coincide con su huella firmada.");

        // 4 · cada medio coincide con la suya
        foreach (var it in Formato["payload_firmado"]!["inventario"]!.AsArray())
        {
            var nombre = it!["archivo"]!.GetValue<string>();
            var ruta = Path.Combine(_carpeta, "medios", nombre);
            if (!File.Exists(ruta)) { motivos.Add($"Falta el medio {nombre}."); continue; }
            if (Huella(File.ReadAllBytes(ruta)) != it!["huella_cifrado"]!.GetValue<string>())
                motivos.Add($"El medio {nombre} no coincide con su huella.");
        }

        return motivos.Count == 0 ? ResultadoVerificacion.Ok() : new ResultadoVerificacion(false, motivos);
    }

    /// <summary>
    /// Abre el manifiesto en memoria con la clave que viene de la licencia.
    /// Las comprobaciones 5 y 6, de coherencia interna, se hacen aquí.
    /// </summary>
    public ResultadoVerificacion Abrir(Licencia licencia, byte[] nodoPrivada)
    {
        if (!licencia.Vigente) return ResultadoVerificacion.No("La licencia está vencida.");
        byte[] k;
        try { k = licencia.ClaveDePaquete(ClavePaquete, nodoPrivada); }
        catch (KeyNotFoundException) { return ResultadoVerificacion.No($"Este nodo no tiene licencia para {ClavePaquete}."); }
        catch (CryptographicException) { return ResultadoVerificacion.No("La licencia no se emitió para este nodo."); }

        var manifCif = File.ReadAllBytes(Path.Combine(_carpeta, "manifiesto.enc"));
        var claro = CifradoArchivo.Descifrar(manifCif, k, "manifiesto");

        _manifiesto = BaseEnMemoria.Desde(claro);
        ClaveDelPaquete = k;

        // 5 · los elementos declarados son los que hay
        var declarados = Escalar<long>("SELECT elementos FROM p_paquete");
        var reales = Escalar<long>("SELECT count(*) FROM p_elemento");
        if (declarados != reales)
            return ResultadoVerificacion.No($"El manifiesto declara {declarados} elementos y tiene {reales}.");

        // 6 · ningún elemento cuelga de una taxonomía inexistente
        var huerfanos = Escalar<long>(
            "SELECT count(*) FROM p_elemento e WHERE e.taxonomia_ref IS NOT NULL " +
            "AND NOT EXISTS(SELECT 1 FROM p_taxonomia t WHERE t.taxonomia_ref = e.taxonomia_ref)");
        if (huerfanos > 0)
            return ResultadoVerificacion.No($"{huerfanos} elementos apuntan a una taxonomía inexistente.");

        return ResultadoVerificacion.Ok();
    }

    public SqliteConnection Manifiesto =>
        _manifiesto ?? throw new InvalidOperationException("El paquete no está abierto. Llama a Abrir primero.");

    /// <summary>Ruta del medio cifrado. El componente de medios lo descifra al vuelo.</summary>
    public string RutaMedio(string huellaArchivo) =>
        Path.Combine(_carpeta, "medios", huellaArchivo + ".enc");

    private T Escalar<T>(string sql)
    {
        using var cmd = Manifiesto.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }

    private static string Huella(byte[] datos)
    {
        // blake2b de 256 bits, igual que la referencia. Ver NotaBlake2 en el LEEME.
        return Convert.ToHexStringLower(Blake2b.Hash256(datos));
    }

    public void Dispose()
    {
        _manifiesto?.Dispose();
        if (ClaveDelPaquete is not null) CryptographicOperations.ZeroMemory(ClaveDelPaquete);
    }
}
