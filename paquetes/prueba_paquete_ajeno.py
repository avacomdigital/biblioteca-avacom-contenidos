#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · que pasa si alguien se lleva un paquete

    py -3 prueba_paquete_ajeno.py <carpeta_trabajo>

Comprueba las dos cosas que sostienen todo el esquema de proteccion:

  1. Un paquete copiado a otro equipo no abre, aunque se lleve la licencia.
     La licencia envuelve la clave del paquete para la clave publica de UN
     equipo concreto. Sin la privada de ese equipo, la clave no se puede
     desenvolver, y sin la clave del paquete los medios son ruido.

  2. Un solo bit cambiado y el paquete se rechaza. La firma cubre la huella
     de cada archivo, asi que no hay forma de retocar un video ni de meter
     uno nuevo sin la clave privada del emisor.

Devuelve 0 si las dos se cumplen. Cualquier otra cosa es un fallo grave.
"""

import os
import shutil
import subprocess
import sys
import tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

from avacom_publicar import abrir            # noqa: E402


def main(trabajo):
    pub = os.path.join(trabajo, "pub")
    if not os.path.isdir(pub):
        print("      no hay carpeta pub en %s" % trabajo)
        return 1

    nombre = sorted(os.listdir(pub))[0]
    origen = os.path.join(pub, nombre)
    licencia = os.path.join(trabajo, "lic", "licencia.json")

    with tempfile.TemporaryDirectory() as usb:
        copia = os.path.join(usb, nombre)
        shutil.copytree(origen, copia)
        print("      el paquete se copia entero, con firma y todo")

        # --- 1 · otro equipo, con su propio par de claves ---
        subprocess.run(
            [sys.executable, os.path.join(AQUI, "avacom_publicar.py"),
             "nodo", os.path.join(usb, "nodo")],
            check=True, capture_output=True)

        if abrir(copia, licencia, os.path.join(usb, "nodo"), mostrar=False) is not None:
            print("      FALLA: se abrio en un equipo que no es el suyo")
            return 1
        print("      no abre: la licencia envuelve la clave para un solo equipo")

        # --- 2 · un bit cambiado ---
        medios = os.path.join(copia, "medios")
        med = os.path.join(medios, sorted(os.listdir(medios))[0])
        datos = bytearray(open(med, "rb").read())
        datos[-1] ^= 0x01
        with open(med, "wb") as f:
            f.write(bytes(datos))

        if abrir(copia, licencia, os.path.join(trabajo, "nodo"), mostrar=False) is not None:
            print("      FALLA: se acepto un paquete con un byte cambiado")
            return 1
        print("      un solo bit cambiado y el paquete se rechaza")

    return 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    sys.exit(main(sys.argv[1]))
