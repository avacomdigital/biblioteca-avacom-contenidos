#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · criptografía del contenido
===================================
Dos cosas distintas que la gente confunde, y aquí van separadas:

  FIRMA        demuestra que el paquete es auténtico y que nadie lo alteró.
               No oculta nada. Ed25519.
  CIFRADO      hace el contenido ilegible para quien no tenga la clave.
               AES-256-GCM.

Cadena de claves
----------------
  1. Cada paquete se cifra con una clave propia de 256 bits, K_pkg,
     generada al azar en el momento de empaquetar.
  2. K_pkg no viaja dentro del paquete. Nunca.
  3. K_pkg viaja dentro de la LICENCIA, envuelta para un nodo concreto
     con la clave pública de ese nodo (X25519). Solo ese nodo la abre.
  4. La licencia va firmada por AVACOM (Ed25519).

Consecuencia práctica: copiar la carpeta del paquete a una memoria USB
no sirve de nada. Sin la licencia de ese nodo es ruido.

Lo que esto NO resuelve, y conviene decirlo
-------------------------------------------
Si alguien controla el equipo del aula mientras está funcionando, puede
llegar a la clave en memoria. El cifrado en reposo sube muchísimo el
listón y no lo vuelve imposible. La defensa que lo acompaña es el cifrado
de disco y el arranque verificado del equipo, que están en SEC-007.
"""

import os, json, struct, hashlib

TAMANO_BLOQUE = 1024 * 1024          # 1 MB. Permite buscar dentro de un video
CABECERA = b"AVACOMENC1"             # marca de archivo cifrado

def _aead():
    from cryptography.hazmat.primitives.ciphers.aead import AESGCM
    return AESGCM

def _x25519():
    from cryptography.hazmat.primitives.asymmetric.x25519 import (
        X25519PrivateKey, X25519PublicKey)
    return X25519PrivateKey, X25519PublicKey

def _hkdf(clave: bytes, info: bytes, largo=32) -> bytes:
    from cryptography.hazmat.primitives.kdf.hkdf import HKDF
    from cryptography.hazmat.primitives import hashes
    return HKDF(algorithm=hashes.SHA256(), length=largo, salt=None, info=info).derive(clave)


# ------------------------------------------------------------------ claves

def nueva_clave_paquete() -> bytes:
    """K_pkg. 256 bits de aleatoriedad del sistema."""
    return os.urandom(32)


def nuevo_par_nodo():
    """Par de claves del nodo. La privada vive en su almacén seguro."""
    Priv, _ = _x25519()
    p = Priv.generate()
    from cryptography.hazmat.primitives import serialization as ser
    return (p.private_bytes(ser.Encoding.Raw, ser.PrivateFormat.Raw, ser.NoEncryption()),
            p.public_key().public_bytes(ser.Encoding.Raw, ser.PublicFormat.Raw))


def envolver_clave(k_pkg: bytes, nodo_publica: bytes) -> dict:
    """Envuelve K_pkg para un nodo concreto. Solo ese nodo la puede abrir."""
    Priv, Pub = _x25519()
    from cryptography.hazmat.primitives import serialization as ser
    efimera = Priv.generate()
    compartido = efimera.exchange(Pub.from_public_bytes(nodo_publica))
    kek = _hkdf(compartido, b"avacom-envoltura-clave-paquete")
    nonce = os.urandom(12)
    ct = _aead()(kek).encrypt(nonce, k_pkg, None)
    return {
        "efimera": efimera.public_key().public_bytes(ser.Encoding.Raw, ser.PublicFormat.Raw).hex(),
        "nonce": nonce.hex(),
        "clave_envuelta": ct.hex(),
    }


def abrir_clave(envoltura: dict, nodo_privada: bytes) -> bytes:
    Priv, Pub = _x25519()
    priv = Priv.from_private_bytes(nodo_privada)
    compartido = priv.exchange(Pub.from_public_bytes(bytes.fromhex(envoltura["efimera"])))
    kek = _hkdf(compartido, b"avacom-envoltura-clave-paquete")
    return _aead()(kek).decrypt(bytes.fromhex(envoltura["nonce"]),
                                bytes.fromhex(envoltura["clave_envuelta"]), None)


# ------------------------------------------------------------------ archivos

def _clave_archivo(k_pkg: bytes, etiqueta: str) -> bytes:
    """Cada archivo usa una clave derivada distinta. Un fallo no compromete al resto."""
    return _hkdf(k_pkg, b"avacom-archivo:" + etiqueta.encode())


def cifrar_archivo(datos: bytes, k_pkg: bytes, etiqueta: str) -> bytes:
    """Cifra por bloques de 1 MB, para poder buscar dentro de un video sin
    descifrarlo entero. Cada bloque lleva su propio sello de autenticidad."""
    k = _clave_archivo(k_pkg, etiqueta)
    aes = _aead()(k)
    base = os.urandom(8)
    salida = [CABECERA, struct.pack("<IQ", TAMANO_BLOQUE, len(datos)), base]
    for i in range(0, max(len(datos), 1), TAMANO_BLOQUE):
        bloque = datos[i:i + TAMANO_BLOQUE]
        nonce = base + struct.pack("<I", i // TAMANO_BLOQUE)
        ct = aes.encrypt(nonce, bloque, CABECERA)
        salida.append(struct.pack("<I", len(ct)))
        salida.append(ct)
    return b"".join(salida)


def descifrar_archivo(cifrado: bytes, k_pkg: bytes, etiqueta: str) -> bytes:
    if not cifrado.startswith(CABECERA):
        raise ValueError("no es un archivo cifrado de AVACOM")
    k = _clave_archivo(k_pkg, etiqueta)
    aes = _aead()(k)
    p = len(CABECERA)
    tam_bloque, largo = struct.unpack("<IQ", cifrado[p:p + 12]); p += 12
    base = cifrado[p:p + 8]; p += 8
    trozos, i = [], 0
    while p < len(cifrado):
        n = struct.unpack("<I", cifrado[p:p + 4])[0]; p += 4
        nonce = base + struct.pack("<I", i)
        trozos.append(aes.decrypt(nonce, cifrado[p:p + n], CABECERA))
        p += n; i += 1
    return b"".join(trozos)[:largo]


def descifrar_bloque(cifrado: bytes, k_pkg: bytes, etiqueta: str, indice: int) -> bytes:
    """Descifra un solo bloque. Es lo que permite adelantar un video sin
    descifrar los cien megabytes anteriores."""
    if not cifrado.startswith(CABECERA):
        raise ValueError("no es un archivo cifrado de AVACOM")
    aes = _aead()(_clave_archivo(k_pkg, etiqueta))
    p = len(CABECERA) + 12
    base = cifrado[p:p + 8]; p += 8
    i = 0
    while p < len(cifrado):
        n = struct.unpack("<I", cifrado[p:p + 4])[0]; p += 4
        if i == indice:
            return aes.decrypt(base + struct.pack("<I", i), cifrado[p:p + n], CABECERA)
        p += n; i += 1
    raise IndexError("bloque %d fuera de rango" % indice)


# ------------------------------------------------------------------ licencia

def emitir_licencia(nodo_publica: bytes, paquetes: dict, firmante_privada,
                    instalacion: str, vence_en: int) -> dict:
    """Emite la licencia de un nodo con las claves de los paquetes que compró.

    paquetes: {clave_paquete: (version, K_pkg)}
    """
    from cryptography.hazmat.primitives import serialization as ser
    cuerpo = {
        "instalacion": instalacion,
        "nodo_publica": nodo_publica.hex(),
        "vence_en": vence_en,
        "paquetes": {c: {"version": v, "envoltura": envolver_clave(k, nodo_publica)}
                     for c, (v, k) in paquetes.items()},
    }
    payload = json.dumps(cuerpo, sort_keys=True, separators=(",", ":")).encode()
    firma = firmante_privada.sign(payload)
    pub = firmante_privada.public_key().public_bytes(ser.Encoding.Raw, ser.PublicFormat.Raw)
    return {"cuerpo": cuerpo, "firma": firma.hex(), "emisor_publica": pub.hex()}


def verificar_licencia(lic: dict) -> bool:
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey
    pub = Ed25519PublicKey.from_public_bytes(bytes.fromhex(lic["emisor_publica"]))
    payload = json.dumps(lic["cuerpo"], sort_keys=True, separators=(",", ":")).encode()
    try:
        pub.verify(bytes.fromhex(lic["firma"]), payload)
        return True
    except Exception:
        return False


def clave_de_paquete(lic: dict, clave_paquete: str, nodo_privada: bytes) -> bytes:
    if not verificar_licencia(lic):
        raise ValueError("la licencia no esta firmada por un emisor valido")
    ent = lic["cuerpo"]["paquetes"].get(clave_paquete)
    if not ent:
        raise KeyError("la licencia no incluye el paquete %s" % clave_paquete)
    return abrir_clave(ent["envoltura"], nodo_privada)
