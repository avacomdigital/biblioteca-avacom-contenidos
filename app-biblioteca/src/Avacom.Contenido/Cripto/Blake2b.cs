using Org.BouncyCastle.Crypto.Digests;

namespace Avacom.Contenido.Cripto;

/// <summary>
/// Huella de contenido. blake2b de 256 bits, la misma que usa el empaquetador.
/// Es la identidad real de un archivo: cambiar un byte cambia el nombre.
/// </summary>
public static class Blake2b
{
    public static byte[] Hash256(ReadOnlySpan<byte> datos)
    {
        var d = new Blake2bDigest(256);
        d.BlockUpdate(datos);
        var salida = new byte[32];
        d.DoFinal(salida);
        return salida;
    }

    public static string Hex(ReadOnlySpan<byte> datos) => Convert.ToHexStringLower(Hash256(datos));
}
