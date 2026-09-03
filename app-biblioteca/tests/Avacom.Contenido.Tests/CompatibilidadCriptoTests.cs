using System.Text;
using System.Text.Json.Nodes;
using Avacom.Contenido.Cripto;
using Xunit;

namespace Avacom.Contenido.Tests;

/// <summary>
/// La prueba mas importante del proyecto.
///
/// El empaquetador corre en Python y el componente en C#. Si las dos
/// implementaciones no coinciden byte a byte, nada funciona: las firmas no
/// validan y el contenido no se descifra. Estos vectores los genero la
/// implementacion de referencia, y aqui se comprueba que C# produce lo mismo.
///
/// Si alguna de estas pruebas falla despues de tocar el cifrado, el cambio
/// esta mal, por muy razonable que parezca.
/// </summary>
public class CompatibilidadCriptoTests
{
    private static JsonNode Vectores() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectores", "cripto.json")))!;

    [Fact]
    public void LaHuellaCoincideConLaReferencia()
    {
        var v = Vectores()["huella"]!;
        var entrada = Convert.FromBase64String(v["entrada_b64"]!.GetValue<string>());
        Assert.Equal(v["blake2b256_hex"]!.GetValue<string>(), Blake2b.Hex(entrada));
    }

    [Fact]
    public void LaSerializacionCanonicaCoincideConLaReferencia()
    {
        var v = Vectores()["canonico"]!;
        var esperado = Convert.FromBase64String(v["salida_utf8_b64"]!.GetValue<string>());
        Assert.Equal(esperado, Canonico.Serializar(v["entrada"]!));
    }

    [Fact]
    public void LaClaveDeArchivoSeDerivaIgual()
    {
        foreach (var c in Vectores()["cifrado"]!.AsArray())
        {
            var k = Convert.FromHexString(c!["clave_paquete_hex"]!.GetValue<string>());
            var esperada = c["clave_archivo_hex"]!.GetValue<string>();
            var obtenida = Convert.ToHexStringLower(
                CifradoArchivo.ClaveDeArchivo(k, c["etiqueta"]!.GetValue<string>()));
            Assert.Equal(esperada, obtenida);
        }
    }

    [Fact]
    public void DescifraLoQueCifroLaReferencia()
    {
        foreach (var c in Vectores()["cifrado"]!.AsArray())
        {
            var k = Convert.FromHexString(c!["clave_paquete_hex"]!.GetValue<string>());
            var etiqueta = c["etiqueta"]!.GetValue<string>();
            var claro = Convert.FromBase64String(c["claro_b64"]!.GetValue<string>());
            var cifrado = Convert.FromBase64String(c["cifrado_b64"]!.GetValue<string>());
            Assert.Equal(claro, CifradoArchivo.Descifrar(cifrado, k, etiqueta));
        }
    }

    [Fact]
    public void ElCifradoDeCSharpTambienSeDescifraEnCSharp()
    {
        var k = Convert.FromHexString(new string('a', 64));
        var claro = Encoding.UTF8.GetBytes(new string('x', 3_000_000));
        var cif = CifradoArchivo.Cifrar(claro, k, "prueba.bin");
        Assert.Equal(claro, CifradoArchivo.Descifrar(cif, k, "prueba.bin"));
    }

    [Fact]
    public void UnaClaveEquivocadaNoDescifra()
    {
        var k = Convert.FromHexString(new string('a', 64));
        var otra = Convert.FromHexString(new string('b', 64));
        var cif = CifradoArchivo.Cifrar(Encoding.UTF8.GetBytes("secreto"), k, "x");
        Assert.ThrowsAny<Exception>(() => CifradoArchivo.Descifrar(cif, otra, "x"));
    }

    [Fact]
    public void AlterarUnByteSeDetecta()
    {
        var k = Convert.FromHexString(new string('a', 64));
        var cif = CifradoArchivo.Cifrar(Encoding.UTF8.GetBytes("contenido educativo"), k, "x");
        cif[cif.Length / 2] ^= 1;
        Assert.ThrowsAny<Exception>(() => CifradoArchivo.Descifrar(cif, k, "x"));
    }

    [Fact]
    public void SePuedeLeerUnBloqueSueltoParaAdelantarUnVideo()
    {
        var k = Convert.FromHexString(new string('c', 64));
        var claro = new byte[CifradoArchivo.TamanoBloque * 3 + 500];
        Random.Shared.NextBytes(claro);
        var cif = CifradoArchivo.Cifrar(claro, k, "video.mp4");
        var bloque2 = CifradoArchivo.DescifrarBloque(cif, k, "video.mp4", 2);
        Assert.Equal(claro[(CifradoArchivo.TamanoBloque * 2)..(CifradoArchivo.TamanoBloque * 3)], bloque2);
    }
}
