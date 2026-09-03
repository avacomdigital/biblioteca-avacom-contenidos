using System.Buffers.Binary;
using System.Security.Cryptography;
using Avacom.Contenido.Cripto;

namespace Avacom.Contenido.Medios;

/// <summary>
/// Un Stream que descifra al vuelo, bloque a bloque. Es lo que permite que el
/// reproductor adelante un video a la mitad sin descifrar lo anterior, y que el
/// contenido en claro nunca toque el disco.
///
/// Lee del archivo cifrado, no de una copia en memoria. La diferencia importa:
/// un video de clase puede pesar cientos de megabytes, el reproductor abre una
/// peticion por cada salto, y cargar el archivo entero en cada una dejaria el
/// equipo del aula sin memoria mientras treinta tabletas escriben respuestas.
/// Aqui solo vive un bloque de un megabyte a la vez.
///
/// Los desplazamientos de los bloques se van aprendiendo segun hacen falta. La
/// primera vez que se salta al minuto ocho hay que recorrer las longitudes de
/// los bloques anteriores, que son cuatro bytes cada una; a partir de ahi el
/// salto es directo.
/// </summary>
public sealed class StreamDeMedio : Stream
{
    private readonly Stream _cifrado;
    private readonly bool _cerrarOrigen;
    private readonly byte[] _clavePaquete;      // copia propia, ver la nota de abajo
    private readonly string _etiqueta;
    private readonly long _largo;
    private readonly int _tamanoBloque;
    private readonly byte[] _baseNonce;

    /// <summary>Desplazamiento del campo de longitud de cada bloque, segun se descubren.</summary>
    private readonly List<long> _desplazamientos = new();

    private long _posicion;
    private byte[]? _bloqueCache;
    private int _indiceCache = -1;

    /// <summary>
    /// El flujo se queda con SU PROPIA copia de la clave del paquete.
    ///
    /// No es celo de mas. El gestor cierra manifiestos cuando pasa de cuatro
    /// paquetes abiertos, y al cerrarlos borra la clave con ceros. Si este flujo
    /// guardara la misma referencia, un video que se estuviera reproduciendo se
    /// cortaria a la mitad con un error de autenticacion en cuanto un profesor
    /// abriera un quinto paquete.
    /// </summary>
    public StreamDeMedio(Stream cifrado, byte[] clavePaquete, string etiqueta, long largoClaro,
                         bool cerrarOrigen = true)
    {
        _cifrado = cifrado;
        _cerrarOrigen = cerrarOrigen;
        _clavePaquete = (byte[])clavePaquete.Clone();
        _etiqueta = etiqueta;
        _largo = largoClaro;

        var cab = new byte[CifradoArchivo.LargoEncabezado];
        _cifrado.Seek(0, SeekOrigin.Begin);
        _cifrado.ReadExactly(cab);
        var e = CifradoArchivo.LeerEncabezado(cab);
        _tamanoBloque = e.TamanoBloque;
        _baseNonce = e.BaseNonce;
        _desplazamientos.Add(e.Desplazamiento);
    }

    /// <summary>Version en memoria. Se usa en pruebas y para archivos pequeños.</summary>
    public StreamDeMedio(byte[] cifrado, byte[] clavePaquete, string etiqueta, long largoClaro)
        : this(new MemoryStream(cifrado, writable: false), clavePaquete, etiqueta, largoClaro) { }

    public override bool CanRead => true;
    public override bool CanSeek => true;          // esto es lo que hace posible adelantar
    public override bool CanWrite => false;
    public override long Length => _largo;
    public override long Position { get => _posicion; set => _posicion = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_posicion >= _largo) return 0;
        int leidos = 0;
        while (count > 0 && _posicion < _largo)
        {
            int indice = (int)(_posicion / _tamanoBloque);
            int dentro = (int)(_posicion % _tamanoBloque);

            if (indice != _indiceCache)
            {
                if (_bloqueCache is not null) CryptographicOperations.ZeroMemory(_bloqueCache);
                _bloqueCache = LeerBloque(indice);
                _indiceCache = indice;
            }

            int disponible = Math.Min(_bloqueCache!.Length - dentro, count);
            disponible = (int)Math.Min(disponible, _largo - _posicion);
            if (disponible <= 0) break;

            Array.Copy(_bloqueCache, dentro, buffer, offset, disponible);
            offset += disponible; count -= disponible;
            _posicion += disponible; leidos += disponible;
        }
        return leidos;
    }

    private byte[] LeerBloque(int indice)
    {
        var pos = Desplazamiento(indice);
        _cifrado.Seek(pos, SeekOrigin.Begin);

        Span<byte> largo = stackalloc byte[4];
        _cifrado.ReadExactly(largo);
        int n = (int)BinaryPrimitives.ReadUInt32LittleEndian(largo);

        var ct = new byte[n];
        _cifrado.ReadExactly(ct);
        return CifradoArchivo.DescifrarSuelto(ct, _clavePaquete, _etiqueta, _baseNonce, indice);
    }

    /// <summary>
    /// Donde empieza el bloque pedido. Los bloques no son de tamaño fijo en el
    /// archivo, porque cada uno lleva su etiqueta de autenticacion y el ultimo
    /// esta a medias, asi que hay que ir leyendo longitudes hasta llegar.
    /// </summary>
    private long Desplazamiento(int indice)
    {
        Span<byte> largo = stackalloc byte[4];
        while (_desplazamientos.Count <= indice)
        {
            var actual = _desplazamientos[^1];
            _cifrado.Seek(actual, SeekOrigin.Begin);
            _cifrado.ReadExactly(largo);
            var n = BinaryPrimitives.ReadUInt32LittleEndian(largo);
            _desplazamientos.Add(actual + 4 + n);
        }
        return _desplazamientos[indice];
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _posicion = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _posicion + offset,
            _ => _largo + offset
        };
        return _posicion;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_bloqueCache is not null) CryptographicOperations.ZeroMemory(_bloqueCache);
        CryptographicOperations.ZeroMemory(_clavePaquete);
        if (disposing && _cerrarOrigen) _cifrado.Dispose();
        base.Dispose(disposing);
    }
}
