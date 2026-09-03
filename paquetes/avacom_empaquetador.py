#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · empaquetador de contenido educativo
============================================
Implementación de referencia del contrato entre el producto de contenido
y el producto LMS. El equipo de contenido puede usarlo tal cual o
reescribirlo en otro lenguaje: lo que importa es que el resultado sea
byte a byte compatible con lo que el nodo espera.

Produce una carpeta de paquete:

    avacom-co-preescolar-transicion-exploracion-v1/
      manifiesto.db      catálogo del paquete, SQLite de solo lectura
      medios/            archivos nombrados por su huella de contenido
      firma.sig          firma Ed25519 del manifiesto y del inventario
      formato.json       versión de formato del contrato y clave pública

Uso:
    py -3 avacom_empaquetador.py claves          genera el par de claves
    py -3 avacom_empaquetador.py construir <spec.json> <destino>
    py -3 avacom_empaquetador.py verificar <carpeta_paquete>
    py -3 avacom_empaquetador.py ejemplos <destino>
"""

import json, os, sqlite3, sys, hashlib, shutil, datetime

FORMATO_VERSION = 1
CLAVES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "claves")

# ---------------------------------------------------------------- utilidades

def huella(datos: bytes) -> str:
    """Huella de contenido. blake2b de 256 bits, en hexadecimal."""
    return hashlib.blake2b(datos, digest_size=32).hexdigest()


def ahora_ms() -> int:
    return int(datetime.datetime.now(datetime.timezone.utc).timestamp() * 1000)


def duracion_wav_ms(datos: bytes):
    """Duracion real de un WAV, leida de su cabecera. Devuelve None si no lo es.

    Importa porque el nodo programa la lectura en voz con este dato: si dice
    dos segundos y el audio dura cinco, la instruccion se corta a la mitad."""
    try:
        import io, wave
        with wave.open(io.BytesIO(datos)) as w:
            return int(w.getnframes() * 1000 / w.getframerate())
    except Exception:
        return None


def _ed25519():
    from cryptography.hazmat.primitives.asymmetric.ed25519 import (
        Ed25519PrivateKey, Ed25519PublicKey)
    from cryptography.hazmat.primitives import serialization
    return Ed25519PrivateKey, Ed25519PublicKey, serialization


# ---------------------------------------------------------------- claves

def generar_claves(destino=CLAVES):
    """Genera el par de claves del emisor. La privada NUNCA sale de aquí."""
    Priv, _, ser = _ed25519()
    os.makedirs(destino, exist_ok=True)
    priv = Priv.generate()
    with open(os.path.join(destino, "emisor_privada.pem"), "wb") as f:
        f.write(priv.private_bytes(
            ser.Encoding.PEM, ser.PrivateFormat.PKCS8, ser.NoEncryption()))
    pub_hex = priv.public_key().public_bytes(
        ser.Encoding.Raw, ser.PublicFormat.Raw).hex()
    with open(os.path.join(destino, "emisor_publica.hex"), "w") as f:
        f.write(pub_hex)
    print("Claves generadas en", destino)
    print("Pública (va dentro de cada paquete y en el nodo):", pub_hex)
    return pub_hex


def _cargar_privada(destino=CLAVES):
    _, _, ser = _ed25519()
    ruta = os.path.join(destino, "emisor_privada.pem")
    if not os.path.exists(ruta):
        raise SystemExit("No hay clave privada. Ejecuta primero: claves")
    with open(ruta, "rb") as f:
        return ser.load_pem_private_key(f.read(), password=None)


# ---------------------------------------------------------------- esquema

ESQUEMA = """
CREATE TABLE p_paquete (
  clave_paquete TEXT PRIMARY KEY, version TEXT NOT NULL, formato_version INTEGER NOT NULL,
  pais TEXT NOT NULL, nivel_clave TEXT NOT NULL, grado TEXT, asignatura TEXT,
  idioma TEXT NOT NULL, titulo TEXT NOT NULL, descripcion TEXT,
  emitido_en INTEGER NOT NULL, emisor TEXT NOT NULL,
  elementos INTEGER NOT NULL, tamano_medios_bytes INTEGER NOT NULL,
  huella_manifiesto TEXT NOT NULL
);
CREATE TABLE p_taxonomia (
  taxonomia_ref TEXT PRIMARY KEY, padre_ref TEXT, tipo_nodo TEXT NOT NULL,
  codigo TEXT, nombre TEXT NOT NULL, orden INTEGER NOT NULL, objetivo TEXT
);
CREATE INDEX ix_p_tax ON p_taxonomia(padre_ref, orden);
CREATE TABLE p_elemento (
  elemento_ref TEXT PRIMARY KEY, version_elemento TEXT NOT NULL,
  -- 'banco' es un repertorio de preguntas del que el examen extrae. No se da
  -- en clase: por eso no entra en ninguna secuencia de leccion.
  tipo TEXT NOT NULL CHECK(tipo IN ('documento','imagen','video','audio','leccion','actividad','evaluacion','banco','interactivo','scorm')),
  titulo TEXT NOT NULL, descripcion TEXT, taxonomia_ref TEXT,
  duracion_seg INTEGER, paginas INTEGER,
  huella_archivo TEXT, tamano_bytes INTEGER,
  estado TEXT NOT NULL DEFAULT 'vigente' CHECK(estado IN ('vigente','retirado')),
  sucesor_ref TEXT, accesibilidad TEXT,
  -- reglas de extraccion de un banco, en JSON: cuantas preguntas saca el
  -- examen y con que mezcla de dificultad
  reglas TEXT,
  FOREIGN KEY(taxonomia_ref) REFERENCES p_taxonomia(taxonomia_ref)
);
CREATE INDEX ix_p_elem ON p_elemento(taxonomia_ref, tipo, estado);
CREATE TABLE p_leccion_item (
  elemento_ref TEXT NOT NULL, orden INTEGER NOT NULL, item_ref TEXT NOT NULL, nota TEXT,
  PRIMARY KEY(elemento_ref, orden),
  FOREIGN KEY(elemento_ref) REFERENCES p_elemento(elemento_ref)
);
CREATE TABLE p_pregunta (
  pregunta_ref TEXT PRIMARY KEY, elemento_ref TEXT NOT NULL, orden INTEGER NOT NULL,
  tipo TEXT NOT NULL, enunciado TEXT NOT NULL, clave_respuesta TEXT,
  peso REAL NOT NULL DEFAULT 1, dificultad TEXT, version_pregunta TEXT NOT NULL,
  retroalimentacion TEXT,
  FOREIGN KEY(elemento_ref) REFERENCES p_elemento(elemento_ref)
);
CREATE INDEX ix_p_preg ON p_pregunta(elemento_ref, orden);
CREATE TABLE p_rubrica (
  rubrica_ref TEXT PRIMARY KEY, elemento_ref TEXT NOT NULL, criterio TEXT NOT NULL,
  descriptor TEXT, peso REAL NOT NULL DEFAULT 1, orden INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY(elemento_ref) REFERENCES p_elemento(elemento_ref)
);
CREATE TABLE p_voz (
  voz_ref TEXT PRIMARY KEY, elemento_ref TEXT, pregunta_ref TEXT,
  idioma TEXT NOT NULL, huella_archivo TEXT NOT NULL, duracion_ms INTEGER
);
"""


# ---------------------------------------------------------------- construir

def construir(spec_ruta, destino_base):
    with open(spec_ruta, encoding="utf-8") as f:
        spec = json.load(f)

    p = spec["paquete"]
    nombre = "avacom-%s-v%s" % (p["clave_paquete"], p["version"])
    destino = os.path.join(destino_base, nombre)
    if os.path.exists(destino):
        shutil.rmtree(destino)
    medios = os.path.join(destino, "medios")
    os.makedirs(medios)

    # --- medios: se copian nombrados por su huella ---
    # medios_origen se resuelve respecto a la carpeta de la especificacion, para
    # que la especificacion se pueda mover de maquina sin tocar rutas.
    origen_medios = spec.get("medios_origen")
    if origen_medios and not os.path.isabs(origen_medios):
        origen_medios = os.path.normpath(
            os.path.join(os.path.dirname(os.path.abspath(spec_ruta)), origen_medios))
    faltantes = []
    huellas = {}
    total = 0
    for el in spec.get("elementos", []):
        arch = el.get("archivo")
        if not arch:
            continue
        ruta_o = os.path.join(origen_medios, arch) if origen_medios else arch
        if os.path.exists(ruta_o):
            datos = open(ruta_o, "rb").read()
        else:
            # marcador de posición, para poder ejercitar el contrato sin medios reales
            datos = ("MARCADOR DE POSICION · %s · %s\n" % (el["elemento_ref"], arch)).encode()
            faltantes.append(arch)
        h = huella(datos)
        ext = os.path.splitext(arch)[1] or ".bin"
        with open(os.path.join(medios, h + ext), "wb") as f:
            f.write(datos)
        huellas[el["elemento_ref"]] = (h + ext, len(datos))
        total += len(datos)

    # --- manifiesto ---
    manif = os.path.join(destino, "manifiesto.db")
    cn = sqlite3.connect(manif)
    cn.executescript(ESQUEMA)

    for t in spec.get("taxonomia", []):
        cn.execute(
            "INSERT INTO p_taxonomia(taxonomia_ref,padre_ref,tipo_nodo,codigo,nombre,orden,objetivo)"
            " VALUES(?,?,?,?,?,?,?)",
            (t["taxonomia_ref"], t.get("padre_ref"), t["tipo_nodo"], t.get("codigo"),
             t["nombre"], t["orden"], t.get("objetivo")))

    for el in spec.get("elementos", []):
        ha, tam = huellas.get(el["elemento_ref"], (None, None))
        cn.execute(
            "INSERT INTO p_elemento(elemento_ref,version_elemento,tipo,titulo,descripcion,"
            "taxonomia_ref,duracion_seg,paginas,huella_archivo,tamano_bytes,estado,sucesor_ref,"
            "accesibilidad,reglas)"
            " VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (el["elemento_ref"], el.get("version_elemento", p["version"]), el["tipo"],
             el["titulo"], el.get("descripcion"), el.get("taxonomia_ref"),
             el.get("duracion_seg"), el.get("paginas"), ha, tam,
             el.get("estado", "vigente"), el.get("sucesor_ref"),
             json.dumps(el["accesibilidad"], ensure_ascii=False) if el.get("accesibilidad") else None,
             json.dumps(el["reglas"], ensure_ascii=False) if el.get("reglas") else None))
        for i, it in enumerate(el.get("secuencia", []), 1):
            cn.execute("INSERT INTO p_leccion_item(elemento_ref,orden,item_ref,nota) VALUES(?,?,?,?)",
                       (el["elemento_ref"], i, it["item_ref"], it.get("nota")))
        for i, q in enumerate(el.get("preguntas", []), 1):
            cn.execute(
                "INSERT INTO p_pregunta(pregunta_ref,elemento_ref,orden,tipo,enunciado,"
                "clave_respuesta,peso,dificultad,version_pregunta,retroalimentacion)"
                " VALUES(?,?,?,?,?,?,?,?,?,?)",
                (q["pregunta_ref"], el["elemento_ref"], i, q["tipo"], q["enunciado"],
                 q.get("clave_respuesta"), q.get("peso", 1), q.get("dificultad"),
                 q.get("version_pregunta", p["version"]), q.get("retroalimentacion")))
        for i, r in enumerate(el.get("rubrica", []), 1):
            cn.execute("INSERT INTO p_rubrica(rubrica_ref,elemento_ref,criterio,descriptor,peso,orden)"
                       " VALUES(?,?,?,?,?,?)",
                       (r["rubrica_ref"], el["elemento_ref"], r["criterio"],
                        r.get("descriptor"), r.get("peso", 1), i))

    for v in spec.get("voz", []):
        texto = v.get("texto", "")
        arch = v.get("archivo")
        ruta_o = os.path.join(origen_medios, arch) if (arch and origen_medios) else arch
        if ruta_o and os.path.exists(ruta_o):
            datos = open(ruta_o, "rb").read()
            ms = duracion_wav_ms(datos) or v.get("duracion_ms", len(texto) * 70)
        else:
            if arch:
                faltantes.append(arch)
            datos = ("AUDIO GENERADO AL EMPAQUETAR\n%s\n" % texto).encode()
            ms = v.get("duracion_ms", len(texto) * 70)
        h = huella(datos)
        with open(os.path.join(medios, h + ".wav"), "wb") as f:
            f.write(datos)
        total += len(datos)
        cn.execute("INSERT INTO p_voz(voz_ref,elemento_ref,pregunta_ref,idioma,huella_archivo,duracion_ms)"
                   " VALUES(?,?,?,?,?,?)",
                   (v["voz_ref"], v.get("elemento_ref"), v.get("pregunta_ref"),
                    v.get("idioma", "es"), h + ".wav", ms))

    n_elem = cn.execute("SELECT count(*) FROM p_elemento").fetchone()[0]
    cn.execute(
        "INSERT INTO p_paquete(clave_paquete,version,formato_version,pais,nivel_clave,grado,"
        "asignatura,idioma,titulo,descripcion,emitido_en,emisor,elementos,tamano_medios_bytes,huella_manifiesto)"
        " VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        (p["clave_paquete"], p["version"], FORMATO_VERSION, p["pais"], p["nivel_clave"],
         p.get("grado"), p.get("asignatura"), p.get("idioma", "es"), p["titulo"],
         p.get("descripcion"), ahora_ms(), p.get("emisor", "AVACOM"), n_elem, total, "pendiente"))
    cn.commit()
    cn.close()

    # la huella del manifiesto se calcula sobre el archivo ya cerrado
    h_manif = huella(open(manif, "rb").read())
    cn = sqlite3.connect(manif)
    cn.execute("UPDATE p_paquete SET huella_manifiesto = ?", (h_manif,))
    cn.commit(); cn.close()

    # --- inventario de medios, para que el nodo pueda verificar cada archivo ---
    inventario = []
    for f in sorted(os.listdir(medios)):
        datos = open(os.path.join(medios, f), "rb").read()
        inventario.append({"archivo": f, "huella": huella(datos), "bytes": len(datos)})

    # --- firma sobre lo que de verdad importa ---
    priv = _cargar_privada()
    payload = json.dumps({
        "clave_paquete": p["clave_paquete"], "version": p["version"],
        "formato_version": FORMATO_VERSION,
        "huella_manifiesto": huella(open(manif, "rb").read()),
        "inventario": inventario,
    }, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    firma = priv.sign(payload)

    with open(os.path.join(destino, "firma.sig"), "wb") as f:
        f.write(firma)
    _, _, ser = _ed25519()
    pub_hex = priv.public_key().public_bytes(ser.Encoding.Raw, ser.PublicFormat.Raw).hex()
    with open(os.path.join(destino, "formato.json"), "w", encoding="utf-8") as f:
        json.dump({
            "formato_version": FORMATO_VERSION,
            "clave_paquete": p["clave_paquete"], "version": p["version"],
            "emisor": p.get("emisor", "AVACOM"), "clave_publica": pub_hex,
            "payload_firmado": json.loads(payload.decode()),
        }, f, ensure_ascii=False, indent=2)

    print("Paquete construido: %s" % destino)
    print("  elementos %d · medios %d archivos · %d bytes" % (n_elem, len(inventario), total))
    if faltantes:
        print("  AVISO · %d medios no se encontraron y van como marcador de posicion:" % len(faltantes))
        for f in faltantes:
            print("      %s" % f)
        print("  Un paquete con marcadores no se publica. Es solo para probar el contrato.")
    return destino


# ---------------------------------------------------------------- verificar

def verificar(carpeta):
    """Lo mismo que hace el nodo antes de tocar nada."""
    _, Pub, _ = _ed25519()
    fmt = json.load(open(os.path.join(carpeta, "formato.json"), encoding="utf-8"))
    fallos = []

    if fmt["formato_version"] != FORMATO_VERSION:
        fallos.append("formato %s no soportado" % fmt["formato_version"])

    pub = Pub.from_public_bytes(bytes.fromhex(fmt["clave_publica"]))
    payload = json.dumps(fmt["payload_firmado"], ensure_ascii=False,
                         sort_keys=True, separators=(",", ":")).encode()
    firma = open(os.path.join(carpeta, "firma.sig"), "rb").read()
    try:
        pub.verify(firma, payload)
    except Exception:
        fallos.append("firma invalida")

    manif = os.path.join(carpeta, "manifiesto.db")
    h_real = huella(open(manif, "rb").read())
    if h_real != fmt["payload_firmado"]["huella_manifiesto"]:
        fallos.append("el manifiesto no coincide con su huella firmada")

    medios = os.path.join(carpeta, "medios")
    for it in fmt["payload_firmado"]["inventario"]:
        ruta = os.path.join(medios, it["archivo"])
        if not os.path.exists(ruta):
            fallos.append("falta el medio %s" % it["archivo"]); continue
        if huella(open(ruta, "rb").read()) != it["huella"]:
            fallos.append("el medio %s no coincide con su huella" % it["archivo"])

    cn = sqlite3.connect("file:%s?mode=ro" % manif, uri=True)
    n = cn.execute("SELECT elementos FROM p_paquete").fetchone()[0]
    real = cn.execute("SELECT count(*) FROM p_elemento").fetchone()[0]
    if n != real:
        fallos.append("el manifiesto declara %d elementos y tiene %d" % (n, real))
    huerfanos = cn.execute(
        "SELECT count(*) FROM p_elemento e WHERE e.taxonomia_ref IS NOT NULL"
        " AND NOT EXISTS(SELECT 1 FROM p_taxonomia t WHERE t.taxonomia_ref = e.taxonomia_ref)"
    ).fetchone()[0]
    if huerfanos:
        fallos.append("%d elementos apuntan a una taxonomia inexistente" % huerfanos)
    cn.close()

    if fallos:
        print("RECHAZADO ·", os.path.basename(carpeta))
        for f in fallos:
            print("   ·", f)
        return False
    print("ACEPTADO ·", os.path.basename(carpeta), "· %d elementos" % real)
    return True


# ---------------------------------------------------------------- ejemplos

def ejemplos(destino):
    """Construye TODAS las especificaciones de specs/, sin lista fija.

    Antes los nombres estaban escritos aqui, y añadir un area nueva obligaba a
    tocar este archivo. Ahora se deja caer la especificacion en specs/ y ya
    entra. Es la diferencia entre que el equipo de contenido dependa del equipo
    de LMS o no dependa."""
    aqui = os.path.dirname(os.path.abspath(__file__))
    carpeta = os.path.join(aqui, "specs")
    hechos = []
    for spec in sorted(os.listdir(carpeta)):
        if not spec.endswith(".json"):
            continue
        hechos.append(construir(os.path.join(carpeta, spec), destino))
    print()
    for c in hechos:
        verificar(c)
    return hechos


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(1)
    cmd = sys.argv[1]
    if cmd == "claves":
        generar_claves()
    elif cmd == "construir":
        construir(sys.argv[2], sys.argv[3])
    elif cmd == "verificar":
        sys.exit(0 if verificar(sys.argv[2]) else 1)
    elif cmd == "ejemplos":
        ejemplos(sys.argv[2] if len(sys.argv) > 2 else ".")
    else:
        print(__doc__); sys.exit(1)
