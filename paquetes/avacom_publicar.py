#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · publicación de paquetes
================================
Segunda etapa de la tubería. La primera, el empaquetador, arma el paquete
en claro para poder revisarlo. Esta lo deja listo para salir al mundo:

    construir   ->   revisar   ->   PUBLICAR   ->   distribuir

Qué hace publicar:
  1. Genera la clave del paquete, K_pkg.
  2. Cifra el manifiesto entero y cada archivo de medios.
  3. Firma el resultado.
  4. Emite la licencia del nodo destino, con K_pkg envuelta para él.

El paquete publicado es ilegible sin la licencia de ese nodo. Copiarlo a
una memoria USB no sirve de nada.

Uso:
    py -3 avacom_publicar.py nodo <carpeta_nodo>              genera el par del nodo
    py -3 avacom_publicar.py publicar <pkg_claro> <destino>
    py -3 avacom_publicar.py licencia <carpeta_nodo> <destino> <pkg_publicado>...
    py -3 avacom_publicar.py abrir <pkg_publicado> <licencia.json> <carpeta_nodo>
"""

import os, sys, json, shutil, sqlite3, tempfile, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import avacom_cripto as C
from avacom_empaquetador import huella, ahora_ms, _cargar_privada, _ed25519

FORMATO_VERSION = 2          # el formato 1 era en claro; el 2 va cifrado


# ------------------------------------------------------------------ nodo

def nodo(carpeta):
    """El nodo genera su par al provisionarse. La privada va a su almacén seguro."""
    os.makedirs(carpeta, exist_ok=True)
    priv, pub = C.nuevo_par_nodo()
    open(os.path.join(carpeta, "nodo_privada.bin"), "wb").write(priv)
    open(os.path.join(carpeta, "nodo_publica.hex"), "w").write(pub.hex())
    print("Par del nodo generado en", carpeta)
    print("  publica:", pub.hex())
    print("  la privada NUNCA sale de este equipo")
    return pub


# ------------------------------------------------------------------ publicar

def publicar(pkg_claro, destino_base):
    manif_claro = os.path.join(pkg_claro, "manifiesto.db")
    cn = sqlite3.connect("file:%s?mode=ro" % manif_claro, uri=True)
    p = dict(zip([d[0] for d in cn.execute("SELECT * FROM p_paquete").description],
                 cn.execute("SELECT * FROM p_paquete").fetchone()))
    cn.close()

    nombre = "avacom-%s-v%s" % (p["clave_paquete"], p["version"])
    destino = os.path.join(destino_base, nombre)
    if os.path.exists(destino):
        shutil.rmtree(destino)
    os.makedirs(os.path.join(destino, "medios"))

    k_pkg = C.nueva_clave_paquete()

    # --- manifiesto cifrado entero ---
    datos = open(manif_claro, "rb").read()
    cif = C.cifrar_archivo(datos, k_pkg, "manifiesto")
    open(os.path.join(destino, "manifiesto.enc"), "wb").write(cif)

    # --- cada medio, cifrado por bloques ---
    inventario = []
    origen_medios = os.path.join(pkg_claro, "medios")
    for f in sorted(os.listdir(origen_medios)):
        d = open(os.path.join(origen_medios, f), "rb").read()
        cf = C.cifrar_archivo(d, k_pkg, f)
        open(os.path.join(destino, "medios", f + ".enc"), "wb").write(cf)
        inventario.append({"archivo": f + ".enc", "huella_cifrado": huella(cf),
                           "bytes_claro": len(d)})

    # --- firma sobre lo cifrado ---
    priv = _cargar_privada()
    payload = json.dumps({
        "clave_paquete": p["clave_paquete"], "version": p["version"],
        "formato_version": FORMATO_VERSION,
        "huella_manifiesto_cifrado": huella(cif),
        "inventario": inventario,
    }, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    open(os.path.join(destino, "firma.sig"), "wb").write(priv.sign(payload))

    _, _, ser = _ed25519()
    pub_hex = priv.public_key().public_bytes(ser.Encoding.Raw, ser.PublicFormat.Raw).hex()
    json.dump({
        "formato_version": FORMATO_VERSION,
        "clave_paquete": p["clave_paquete"], "version": p["version"],
        "cifrado": {"algoritmo": "AES-256-GCM", "bloque_bytes": C.TAMANO_BLOQUE,
                    "derivacion": "HKDF-SHA256 por archivo"},
        "emisor": p["emisor"], "clave_publica": pub_hex,
        # metadatos visibles sin licencia, para que el tecnico sepa qué tiene en la mano
        "vitrina": {"pais": p["pais"], "nivel_clave": p["nivel_clave"], "grado": p["grado"],
                    "asignatura": p["asignatura"], "idioma": p["idioma"],
                    "titulo": p["titulo"], "elementos": p["elementos"]},
        "payload_firmado": json.loads(payload.decode()),
    }, open(os.path.join(destino, "formato.json"), "w", encoding="utf-8"),
        ensure_ascii=False, indent=2)

    open(os.path.join(destino, "K_PKG_NO_DISTRIBUIR.hex"), "w").write(k_pkg.hex())
    print("Publicado: %s" % destino)
    print("  manifiesto cifrado · %d medios cifrados" % len(inventario))
    print("  K_pkg guardada aparte. Va a la licencia, nunca al paquete.")
    return destino, k_pkg


# ------------------------------------------------------------------ licencia

def licencia(carpeta_nodo, destino, paquetes):
    pub = bytes.fromhex(open(os.path.join(carpeta_nodo, "nodo_publica.hex")).read().strip())
    entradas = {}
    for pk in paquetes:
        fmt = json.load(open(os.path.join(pk, "formato.json"), encoding="utf-8"))
        k = bytes.fromhex(open(os.path.join(pk, "K_PKG_NO_DISTRIBUIR.hex")).read().strip())
        entradas[fmt["clave_paquete"]] = (fmt["version"], k)
    lic = C.emitir_licencia(pub, entradas, _cargar_privada(), "INST001",
                            ahora_ms() + 365 * 24 * 3600 * 1000)
    ruta = os.path.join(destino, "licencia.json")
    os.makedirs(destino, exist_ok=True)
    json.dump(lic, open(ruta, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("Licencia emitida:", ruta)
    print("  %d paquetes autorizados para este nodo" % len(entradas))
    return ruta


# ------------------------------------------------------------------ abrir

def abrir(pkg, ruta_lic, carpeta_nodo, mostrar=True):
    """Lo que hace el nodo: verifica, saca la clave de la licencia y descifra."""
    fmt = json.load(open(os.path.join(pkg, "formato.json"), encoding="utf-8"))
    if fmt["formato_version"] != FORMATO_VERSION:
        print("RECHAZADO · formato %s no soportado" % fmt["formato_version"]); return None

    _, Pub, _ = _ed25519()
    pubk = Pub.from_public_bytes(bytes.fromhex(fmt["clave_publica"]))
    payload = json.dumps(fmt["payload_firmado"], ensure_ascii=False,
                         sort_keys=True, separators=(",", ":")).encode()
    try:
        pubk.verify(open(os.path.join(pkg, "firma.sig"), "rb").read(), payload)
    except Exception:
        print("RECHAZADO · firma invalida"); return None

    cif = open(os.path.join(pkg, "manifiesto.enc"), "rb").read()
    if huella(cif) != fmt["payload_firmado"]["huella_manifiesto_cifrado"]:
        print("RECHAZADO · el manifiesto no coincide con su huella firmada"); return None
    for it in fmt["payload_firmado"]["inventario"]:
        r = os.path.join(pkg, "medios", it["archivo"])
        if not os.path.exists(r) or huella(open(r, "rb").read()) != it["huella_cifrado"]:
            print("RECHAZADO · medio alterado o ausente: %s" % it["archivo"]); return None

    lic = json.load(open(ruta_lic, encoding="utf-8"))
    priv = open(os.path.join(carpeta_nodo, "nodo_privada.bin"), "rb").read()
    try:
        k = C.clave_de_paquete(lic, fmt["clave_paquete"], priv)
    except Exception as e:
        print("RECHAZADO · este nodo no tiene licencia para %s (%s)"
              % (fmt["clave_paquete"], type(e).__name__)); return None

    claro = C.descifrar_archivo(cif, k, "manifiesto")
    # Carpeta temporal del sistema, no una ruta escrita a mano: esto tiene que
    # funcionar igual en cualquier equipo. Quien llama es responsable de borrar
    # el archivo, que es lo que devuelve como tercer valor.
    fd, tmp = tempfile.mkstemp(prefix="avacom_manif_", suffix=".db")
    with os.fdopen(fd, "wb") as f:
        f.write(claro)
    cn = sqlite3.connect("file:%s?mode=ro" % tmp, uri=True)
    if mostrar:
        t = cn.execute("SELECT titulo FROM p_paquete").fetchone()[0]
        n = cn.execute("SELECT count(*) FROM p_elemento").fetchone()[0]
        q = cn.execute("SELECT count(*) FROM p_pregunta").fetchone()[0]
        print("ABIERTO · %s · %d elementos · %d reactivos" % (t, n, q))
    return cn, k, tmp


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__); sys.exit(1)
    c = sys.argv[1]
    if c == "nodo": nodo(sys.argv[2])
    elif c == "publicar": publicar(sys.argv[2], sys.argv[3])
    elif c == "licencia": licencia(sys.argv[2], sys.argv[3], sys.argv[4:])
    elif c == "abrir":
        # Se cierra y se borra. El manifiesto en claro lleva las claves de
        # respuesta: dejarlo en la carpeta temporal es justo lo que todo este
        # esquema existe para evitar. En Windows ademas queda bloqueado si no se
        # cierra la conexion antes.
        r = abrir(sys.argv[2], sys.argv[3], sys.argv[4])
        if r:
            r[0].close()
            try:
                os.unlink(r[2])
            except OSError:
                pass
    else: print(__doc__); sys.exit(1)
