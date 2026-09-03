using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Avacom.Contenido.Cripto;

/// <summary>
/// Cifrado de archivos de contenido. Compatible byte a byte con la
/// implementación de referencia en Python (avacom_cripto.py).
///
/// Formato del archivo cifrado:
///   [10]  "AVACOMENC1"
///   [4]   tamaño de bloque, entero sin signo, orden de bytes pequeño
///   [8]   longitud del contenido en claro, entero sin signo
///   [8]   base del nonce, aleatoria por archivo
///   luego, por cada bloque:
///     [4]  longitud del bloque cifrado
///     [n]  bloque cifrado con AES-256-GCM
///
/// El nonce de cada bloque es base(8) + indice(4). El dato asociado
/// autenticado es la cabecera, para que nadie pueda cambiar el formato.
///
/// Se cifra por bloques de 1 MB a propósito: permite adelantar un video
/// sin descifrar los cien megabytes anteriores.
/// </summary>
public static class CifradoArchivo
{
    public static readonly byte[] Cabecera = Encoding.ASCII.GetBytes("AVACOMENC1");
    public const int TamanoBloque = 1024 * 1024;
    private const int TagBytes = 16;

    /// <summary>
    /// Deriva la clave de un archivo concreto a partir de la clave del paquete.
    /// Cada archivo usa una clave distinta: comprometer una no compromete al resto.
    /// </summary>
    public static byte[] ClaveDeArchivo(ReadOnlySpan<byte> clavePaquete, string etiqueta)
    {
        var info = Encoding.UTF8.GetBytes("avacom-archivo:" + etiqueta);
        // La sobrecarga que devuelve byte[] solo acepta byte[]. Con ReadOnlySpan
        // hay que usar la que escribe en un Span. Sal vacia equivale a sal de
        // ceros segun el RFC 5869, que es lo mismo que hace salt=None en Python.
        var salida = new byte[32];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, clavePaquete, salida, ReadOnlySpan<byte>.Empty, info);
        return salida;
    }

    public static byte[] Cifrar(ReadOnlySpan<byte> claro, ReadOnlySpan<byte> clavePaquete, string etiqueta)
    {
        var clave = ClaveDeArchivo(clavePaquete, etiqueta);
        using var aes = new AesGcm(clave, TagBytes);

        var baseNonce = RandomNumberGenerator.GetBytes(8);
        using var salida = new MemoryStream();
        salida.Write(Cabecera);
        Span<byte> enc = stackalloc byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(enc[..4], TamanoBloque);
        salida.Write(enc[..4]);
        BinaryPrimitives.WriteUInt64LittleEndian(enc[..8], (ulong)claro.Length);
        salida.Write(enc[..8]);
        salida.Write(baseNonce);

        int indice = 0;
        for (int i = 0; i < Math.Max(claro.Length, 1); i += TamanoBloque)
        {
            var bloque = claro.Slice(i, Math.Min(TamanoBloque, claro.Length - i));
            var ct = new byte[bloque.Length + TagBytes];
            aes.Encrypt(Nonce(baseNonce, indice), bloque, ct.AsSpan(0, bloque.Length),
                        ct.AsSpan(bloque.Length), Cabecera);
            BinaryPrimitives.WriteUInt32LittleEndian(enc[..4], (uint)ct.Length);
            salida.Write(enc[..4]);
            salida.Write(ct);
            indice++;
        }
        return salida.ToArray();
    }

    public static byte[] Descifrar(ReadOnlySpan<byte> cifrado, ReadOnlySpan<byte> clavePaquete, string etiqueta)
    {
        var (baseNonce, largo, primero) = LeerCabecera(cifrado);
        var clave = ClaveDeArchivo(clavePaquete, etiqueta);
        using var aes = new AesGcm(clave, TagBytes);

        using var salida = new MemoryStream(checked((int)largo));
        int p = primero, indice = 0;
        while (p < cifrado.Length)
        {
            int n = (int)BinaryPrimitives.ReadUInt32LittleEndian(cifrado.Slice(p, 4)); p += 4;
            var ct = cifrado.Slice(p, n); p += n;
            var claro = new byte[n - TagBytes];
            aes.Decrypt(Nonce(baseNonce, indice), ct[..^TagBytes], ct[^TagBytes..], claro, Cabecera);
            salida.Write(claro);
            indice++;
        }
        var todo = salida.ToArray();
        return todo.Length == (int)largo ? todo : todo[..(int)largo];
    }

    /// <summary>Descifra un solo bloque. Es lo que hace posible buscar dentro de un video.</summary>
    public static byte[] DescifrarBloque(ReadOnlySpan<byte> cifrado, ReadOnlySpan<byte> clavePaquete,
                                         string etiqueta, int indiceBuscado)
    {
        var (baseNonce, _, primero) = LeerCabecera(cifrado);
        using var aes = new AesGcm(ClaveDeArchivo(clavePaquete, etiqueta), TagBytes);
        int p = primero, indice = 0;
        while (p < cifrado.Length)
        {
            int n = (int)BinaryPrimitives.ReadUInt32LittleEndian(cifrado.Slice(p, 4)); p += 4;
            if (indice == indiceBuscado)
            {
                var ct = cifrado.Slice(p, n);
                var claro = new byte[n - TagBytes];
                aes.Decrypt(Nonce(baseNonce, indice), ct[..^TagBytes], ct[^TagBytes..], claro, Cabecera);
                return claro;
            }
            p += n; indice++;
        }
        throw new ArgumentOutOfRangeException(nameof(indiceBuscado));
    }

    /// <summary>Lo que hay en la cabecera de un archivo cifrado.</summary>
    public readonly record struct Encabezado(byte[] BaseNonce, long LargoClaro, int TamanoBloque, int Desplazamiento);

    /// <summary>
    /// Tamaño de la cabecera. Es fijo: marca(10) + tamaño de bloque(4) +
    /// longitud en claro(8) + base del nonce(8).
    /// </summary>
    public const int LargoEncabezado = 10 + 4 + 8 + 8;

    /// <summary>
    /// Lee la cabecera sin tocar el resto. Hace falta para poder descifrar un
    /// archivo desde el disco bloque a bloque, sin cargarlo entero en memoria:
    /// un video de clase pesa mas de lo que conviene tener en RAM mientras
    /// treinta tabletas escriben respuestas.
    /// </summary>
    public static Encabezado LeerEncabezado(ReadOnlySpan<byte> c)
    {
        if (c.Length < LargoEncabezado || !c[..Cabecera.Length].SequenceEqual(Cabecera))
            throw new InvalidDataException("No es un archivo cifrado de AVACOM.");
        int p = Cabecera.Length;
        int bloque = (int)BinaryPrimitives.ReadUInt32LittleEndian(c.Slice(p, 4)); p += 4;
        long largo = (long)BinaryPrimitives.ReadUInt64LittleEndian(c.Slice(p, 8)); p += 8;
        var baseNonce = c.Slice(p, 8).ToArray(); p += 8;
        return new Encabezado(baseNonce, largo, bloque, p);
    }

    /// <summary>
    /// Descifra un bloque suelto del que ya se tienen los bytes. El nonce sale
    /// de la base y del indice, y el dato asociado es la cabecera, asi que un
    /// bloque no se puede mover de sitio dentro del archivo sin que se note.
    /// </summary>
    public static byte[] DescifrarSuelto(ReadOnlySpan<byte> bloqueCifrado, ReadOnlySpan<byte> clavePaquete,
                                         string etiqueta, byte[] baseNonce, int indice)
    {
        using var aes = new AesGcm(ClaveDeArchivo(clavePaquete, etiqueta), TagBytes);
        var claro = new byte[bloqueCifrado.Length - TagBytes];
        aes.Decrypt(Nonce(baseNonce, indice), bloqueCifrado[..^TagBytes], bloqueCifrado[^TagBytes..],
                    claro, Cabecera);
        return claro;
    }

    private static (byte[] baseNonce, ulong largo, int primero) LeerCabecera(ReadOnlySpan<byte> c)
    {
        if (c.Length < Cabecera.Length + 20 || !c[..Cabecera.Length].SequenceEqual(Cabecera))
            throw new InvalidDataException("No es un archivo cifrado de AVACOM.");
        int p = Cabecera.Length;
        p += 4;                                                       // tamaño de bloque
        ulong largo = BinaryPrimitives.ReadUInt64LittleEndian(c.Slice(p, 8)); p += 8;
        var baseNonce = c.Slice(p, 8).ToArray(); p += 8;
        return (baseNonce, largo, p);
    }

    private static byte[] Nonce(byte[] baseNonce, int indice)
    {
        var n = new byte[12];
        baseNonce.CopyTo(n, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(n.AsSpan(8), (uint)indice);
        return n;
    }
}
