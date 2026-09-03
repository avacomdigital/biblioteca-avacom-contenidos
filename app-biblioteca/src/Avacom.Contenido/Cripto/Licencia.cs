using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Avacom.Contenido.Cripto;

/// <summary>
/// La licencia del nodo. Lleva, para cada paquete autorizado, la clave de ese
/// paquete envuelta con la clave pública de este nodo concreto. Ningún otro
/// nodo la puede abrir, y por eso copiar un paquete a una memoria no sirve.
///
/// La licencia va firmada por AVACOM con Ed25519. Manipular cualquier campo,
/// incluida la fecha de vencimiento, invalida la firma.
///
/// Nota sobre dependencias: .NET no trae Ed25519 ni X25519 en la biblioteca
/// estándar, así que se usa BouncyCastle. Es la única dependencia externa de
/// criptografía del proyecto, y conviene que siga siendo la única.
/// </summary>
public sealed class Licencia
{
    private readonly JsonNode _cuerpo;
    private readonly byte[] _firma;
    private readonly byte[] _emisorPublica;

    private Licencia(JsonNode cuerpo, byte[] firma, byte[] emisorPublica)
        => (_cuerpo, _firma, _emisorPublica) = (cuerpo, firma, emisorPublica);

    public static Licencia Cargar(string ruta)
    {
        var raiz = JsonNode.Parse(File.ReadAllText(ruta))!;
        return new Licencia(
            raiz["cuerpo"]!,
            Convert.FromHexString(raiz["firma"]!.GetValue<string>()),
            Convert.FromHexString(raiz["emisor_publica"]!.GetValue<string>()));
    }

    /// <summary>Verifica la firma del emisor. Si falla, la licencia no vale nada.</summary>
    public bool Verificar(byte[]? emisorEsperado = null)
    {
        if (emisorEsperado is not null && !_emisorPublica.SequenceEqual(emisorEsperado))
            return false;
        var payload = Canonico(_cuerpo);
        var verificador = new Ed25519Signer();
        verificador.Init(false, new Ed25519PublicKeyParameters(_emisorPublica, 0));
        verificador.BlockUpdate(payload, 0, payload.Length);
        return verificador.VerifySignature(_firma);
    }

    public long VenceEn => _cuerpo["vence_en"]!.GetValue<long>();
    public bool Vigente => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < VenceEn;
    public IEnumerable<string> PaquetesAutorizados =>
        _cuerpo["paquetes"]!.AsObject().Select(p => p.Key);

    /// <summary>
    /// Saca la clave de un paquete. Falla si la licencia no está firmada,
    /// si no incluye ese paquete, o si este no es el nodo para el que se emitió.
    /// </summary>
    public byte[] ClaveDePaquete(string clavePaquete, byte[] nodoPrivada)
    {
        if (!Verificar()) throw new InvalidOperationException("La licencia no está firmada por un emisor válido.");
        var ent = _cuerpo["paquetes"]![clavePaquete]
            ?? throw new KeyNotFoundException($"La licencia no incluye el paquete {clavePaquete}.");
        var env = ent["envoltura"]!;

        var efimera = Convert.FromHexString(env["efimera"]!.GetValue<string>());
        var nonce = Convert.FromHexString(env["nonce"]!.GetValue<string>());
        var envuelta = Convert.FromHexString(env["clave_envuelta"]!.GetValue<string>());

        var acuerdo = new X25519Agreement();
        acuerdo.Init(new X25519PrivateKeyParameters(nodoPrivada, 0));
        var compartido = new byte[acuerdo.AgreementSize];
        acuerdo.CalculateAgreement(new X25519PublicKeyParameters(efimera, 0), compartido, 0);

        var kek = HKDF.DeriveKey(HashAlgorithmName.SHA256, compartido, 32, salt: null,
                                 info: Encoding.UTF8.GetBytes("avacom-envoltura-clave-paquete"));

        using var aes = new AesGcm(kek, 16);
        var clave = new byte[envuelta.Length - 16];
        aes.Decrypt(nonce, envuelta.AsSpan(0, clave.Length), envuelta.AsSpan(clave.Length), clave);
        return clave;
    }

    /// <summary>
    /// Serialización canónica, idéntica a la de Python: claves ordenadas y sin
    /// espacios. Si esto no coincide byte a byte, la firma nunca valida.
    /// </summary>
    private static byte[] Canonico(JsonNode nodo)
    {
        var opciones = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return Encoding.UTF8.GetBytes(Ordenar(nodo).ToJsonString(opciones));
    }

    private static JsonNode Ordenar(JsonNode n)
    {
        switch (n)
        {
            case JsonObject o:
                var r = new JsonObject();
                foreach (var k in o.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
                    r[k] = o[k] is null ? null : Ordenar(o[k]!.DeepClone());
                return r;
            case JsonArray a:
                var arr = new JsonArray();
                foreach (var x in a) arr.Add(x is null ? null : Ordenar(x.DeepClone()));
                return arr;
            default:
                return n.DeepClone();
        }
    }
}
