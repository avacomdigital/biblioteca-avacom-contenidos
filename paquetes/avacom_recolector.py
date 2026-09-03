#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · recolector de contenido

    py -3 avacom_recolector.py revisar   <carpeta_del_curso>
    py -3 avacom_recolector.py armar     <carpeta_del_curso> [destino]

POR QUE EXISTE ESTO

El empaquetador necesita una especificacion en JSON. Un pedagogo no tiene por
que escribir JSON, y si le obligamos a hacerlo pasaran dos cosas: escribira
menos material del que podria, y el que escriba llegara con errores de coma.

Asi que la estructura de carpetas ES la especificacion. El equipo de contenido
trabaja como ya trabaja (carpetas, archivos, un par de archivos de texto) y
este programa deduce el resto.

    contenido/
      CO/                                   pais
        secundaria/                         nivel
          09/                               grado
            humanidades/                    materia
              curso.txt                     la ficha del curso
              indice.txt                    el indice curricular, con sangria
              DBA-9-03 Romanticismo/        un tema, con su codigo delante
                01-Linea de tiempo.png
                02-Guia de lectura.pdf
                03-Comparador/              carpeta con index.html
                04-evaluacion.txt
                leccion.txt                 opcional: notas de la secuencia

El pais, el nivel y el grado salen de la RUTA, no de un archivo. Asi no pueden
contradecirse, que es el error mas comun cuando se escriben dos veces.

La extension decide el tipo. El numero de delante decide el orden. El resto del
nombre es el titulo que vera el profesor.
"""

import json
import os
import re
import sys
import unicodedata
import zipfile

import avacom_leccion

AQUI = os.path.dirname(os.path.abspath(__file__))


# ---------------------------------------------------------------- tipos

# La extension manda. Es lo unico que el equipo de contenido no puede
# equivocarse al elegir, porque ya viene dada por el programa con el que hizo
# el material.
POR_EXTENSION = {
    ".png": "imagen", ".jpg": "imagen", ".jpeg": "imagen",
    ".webp": "imagen", ".svg": "imagen",
    ".pdf": "documento",
    ".mp4": "video", ".webm": "video",
    ".mp3": "audio", ".wav": "audio", ".m4a": "audio",
    ".zip": "interactivo",
}

EXTENSIONES_VALIDAS = set(POR_EXTENSION) | {".txt"}


def sin_tildes(t):
    return "".join(c for c in unicodedata.normalize("NFD", t)
                   if unicodedata.category(c) != "Mn")


def rebanada(t):
    """Convierte un titulo en una referencia estable y legible."""
    t = sin_tildes(t).lower()
    t = re.sub(r"[^a-z0-9]+", "-", t).strip("-")
    return re.sub(r"-{2,}", "-", t)[:48]


def partir_nombre(nombre):
    """De "01-Linea de tiempo.png" saca (1, "Linea de tiempo", ".png")."""
    base, ext = os.path.splitext(nombre)
    m = re.match(r"^\s*(\d+)\s*[-_. ]\s*(.+)$", base)
    if m:
        return int(m.group(1)), m.group(2).strip(), ext.lower()
    return 999, base.strip(), ext.lower()


# ---------------------------------------------------------------- lectura

def leer_claves(ruta):
    """Archivo de "clave: valor", una por linea. Se ignoran vacias y con #."""
    d = {}
    if not os.path.exists(ruta):
        return d
    for linea in abrir_texto(ruta).splitlines():
        linea = linea.strip()
        if not linea or linea.startswith("#"):
            continue
        if ":" in linea:
            k, v = linea.split(":", 1)
            d[k.strip().lower()] = v.strip()
    return d


def abrir_texto(ruta):
    """Lee un archivo de texto sin pelearse con la codificacion.

    Los editores de Windows guardan de tres formas distintas y el equipo de
    contenido no tiene por que saber cual. Se prueban en orden."""
    crudo = open(ruta, "rb").read()
    for cod in ("utf-8-sig", "utf-8", "cp1252", "latin-1"):
        try:
            return crudo.decode(cod)
        except UnicodeDecodeError:
            continue
    return crudo.decode("utf-8", errors="replace")


def leer_indice(ruta, avisos):
    """
    El indice curricular, escrito con sangria. Se parece a los documentos que
    el equipo de contenido ya tiene, y esa es toda la gracia:

        area | L115-A7 | Humanidades, lengua castellana e idiomas extranjeros
          factor | EBC-LIT | Literatura
            estandar | EBC-9-LIT-01 | Determino en las obras literarias...
              tema | DBA-9-03 | El Romanticismo en la novela colombiana

    La sangria marca de quien cuelga cada cosa. Las barras separan tipo,
    codigo y nombre. El codigo puede ir vacio.
    """
    if not os.path.exists(ruta):
        avisos.append("Falta indice.txt. Sin el no hay estructura curricular.")
        return [], {}

    nodos, por_codigo = [], {}
    pila = []          # (sangria, taxonomia_ref)
    orden_por_padre = {}

    for n, cruda in enumerate(abrir_texto(ruta).splitlines(), 1):
        if not cruda.strip() or cruda.strip().startswith("#"):
            continue

        sangria = len(cruda) - len(cruda.lstrip())
        partes = [p.strip() for p in cruda.strip().split("|")]

        if len(partes) < 2:
            avisos.append("indice.txt linea %d: hacen falta al menos "
                          "tipo | nombre, separados por barras." % n)
            continue

        if len(partes) == 2:
            tipo, codigo, nombre = partes[0], None, partes[1]
        else:
            tipo, codigo, nombre = partes[0], (partes[1] or None), partes[2]

        while pila and pila[-1][0] >= sangria:
            pila.pop()
        padre = pila[-1][1] if pila else None

        ref = rebanada(codigo) if codigo else rebanada(nombre)
        if any(x["taxonomia_ref"] == ref for x in nodos):
            ref = ref + "-" + str(n)

        orden_por_padre[padre] = orden_por_padre.get(padre, 0) + 1
        nodos.append({
            "taxonomia_ref": ref,
            "padre_ref": padre,
            "tipo_nodo": tipo,
            "codigo": codigo,
            "nombre": nombre,
            "orden": orden_por_padre[padre],
            "objetivo": None,
        })
        if codigo:
            por_codigo[codigo.upper()] = ref
        pila.append((sangria, ref))

    return nodos, por_codigo


def leer_banco(ruta, ref_base, taxonomia_ref, avisos):
    """
    Un banco de preguntas: el repertorio del que un examen saca reactivos.

    Se escribe igual que una evaluacion, pero con una cabecera que dice cuantas
    preguntas extraer y de que dificultad. La diferencia con una evaluacion no
    es de formato, es de proposito:

        evaluacion   se da entera, siempre las mismas preguntas, en la leccion
        banco        el examen saca N al azar, y dos alumnos no ven lo mismo

    Por eso el banco NO entra en la secuencia de la leccion.
    """
    ficha = leer_claves(ruta)
    elemento = leer_evaluacion(ruta, ref_base, taxonomia_ref, avisos)
    elemento["tipo"] = "banco"
    elemento["titulo"] = ficha.get("titulo", "Banco de preguntas")

    total = len(elemento["preguntas"])
    reglas = {}

    if ficha.get("extraer", "").isdigit():
        reglas["extraer"] = int(ficha["extraer"])

    # "por_dificultad: baja 3, media 5, alta 2"
    if ficha.get("por_dificultad"):
        mezcla = {}
        for trozo in ficha["por_dificultad"].split(","):
            partes = trozo.split()
            if len(partes) == 2 and partes[1].isdigit():
                mezcla[partes[0].lower()] = int(partes[1])
        if mezcla:
            reglas["por_dificultad"] = mezcla
            reglas.setdefault("extraer", sum(mezcla.values()))

            # que el banco tenga de verdad lo que la mezcla pide
            hay = {}
            for p in elemento["preguntas"]:
                d = (p.get("dificultad") or "media").lower()
                hay[d] = hay.get(d, 0) + 1
            for d, piden in mezcla.items():
                if hay.get(d, 0) < piden:
                    avisos.append("%s: la mezcla pide %d preguntas de dificultad "
                                  "«%s» y en el banco solo hay %d."
                                  % (os.path.basename(ruta), piden, d, hay.get(d, 0)))

    n = reglas.get("extraer")
    if n and total < n * 2:
        avisos.append("%s: el banco tiene %d preguntas y el examen saca %d. Con "
                      "tan pocas, dos alumnos veran casi lo mismo. Conviene al "
                      "menos el doble." % (os.path.basename(ruta), total, n))
    if not reglas:
        avisos.append("%s: es un banco y no dice cuantas preguntas extraer. "
                      "Añade «extraer: 10» en la cabecera."
                      % os.path.basename(ruta))

    elemento["reglas"] = reglas
    return elemento


def leer_evaluacion(ruta, ref_base, taxonomia_ref, avisos):
    """
    Una evaluacion escrita como la escribiria un profesor:

        titulo: Evaluacion de la unidad

        P: En que año se publico Maria, de Jorge Isaacs?
        R: 1867
        peso: 1
        retro: 1867, en plena consolidacion del Romanticismo.

        P: Compara como tratan la naturaleza los dos movimientos.
        abierta
        peso: 3

        RUBRICA
        Uso de terminos tecnicos | Emplea con precision al menos dos | 2
        Evidencia textual | Sostiene lo que afirma citando el texto | 2
    """
    texto = abrir_texto(ruta)
    titulo = "Evaluacion"
    preguntas, rubrica = [], []
    actual = None
    en_rubrica = False

    def cerrar():
        if actual is None:
            return
        if actual["tipo"] != "abierta" and not actual["clave_respuesta"]:
            avisos.append("%s: la pregunta «%s» no tiene respuesta (R:). "
                          "Si es abierta, escribe «abierta» debajo."
                          % (os.path.basename(ruta), actual["enunciado"][:44]))
        preguntas.append(actual)

    for cruda in texto.splitlines():
        linea = cruda.strip()
        if not linea or linea.startswith("#"):
            continue

        if linea.upper() == "RUBRICA":
            cerrar(); actual = None
            en_rubrica = True
            continue

        if en_rubrica:
            partes = [p.strip() for p in linea.split("|")]
            if len(partes) >= 2:
                rubrica.append({
                    "rubrica_ref": "%s-r%d" % (ref_base, len(rubrica) + 1),
                    "criterio": partes[0],
                    "descriptor": partes[1],
                    "peso": int(partes[2]) if len(partes) > 2 and partes[2].isdigit() else 1,
                })
            continue

        bajo = linea.lower()
        if bajo.startswith("titulo:"):
            titulo = linea.split(":", 1)[1].strip()
        elif bajo.startswith(("p:", "pregunta:")):
            cerrar()
            actual = {
                "pregunta_ref": "%s-q%d" % (ref_base, len(preguntas) + 1),
                "tipo": "opcion_unica",
                "enunciado": linea.split(":", 1)[1].strip(),
                "clave_respuesta": None,
                "peso": 1,
                "dificultad": "media",
            }
        elif actual is None:
            continue
        elif bajo.startswith(("r:", "respuesta:")):
            actual["clave_respuesta"] = linea.split(":", 1)[1].strip()
        elif bajo == "abierta":
            actual["tipo"] = "abierta"
        elif bajo.startswith("peso:"):
            v = linea.split(":", 1)[1].strip()
            actual["peso"] = int(v) if v.isdigit() else 1
        elif bajo.startswith(("retro:", "retroalimentacion:")):
            actual["retroalimentacion"] = linea.split(":", 1)[1].strip()
        elif bajo.startswith("dificultad:"):
            actual["dificultad"] = linea.split(":", 1)[1].strip().lower()

    cerrar()

    if any(p["tipo"] == "abierta" for p in preguntas) and not rubrica:
        avisos.append("%s: hay preguntas abiertas y ninguna RUBRICA. Sin ella, "
                      "dos profesores daran notas distintas al mismo texto."
                      % os.path.basename(ruta))

    elemento = {
        "elemento_ref": ref_base,
        "tipo": "evaluacion",
        "titulo": titulo,
        "taxonomia_ref": taxonomia_ref,
        "preguntas": preguntas,
    }
    if rubrica:
        elemento["rubrica"] = rubrica
    return elemento


# ---------------------------------------------------------- interactivos

def sin_comentarios(texto, nombre):
    """Quita los comentarios antes de buscar problemas.

    Sin esto, la propia plantilla se suspende: en su cabecera explica, dentro de
    un comentario, que no se puede usar internet ni almacenamiento del
    navegador. Y el primer contacto del equipo de contenido con la herramienta
    seria un ejemplo oficial que incumple sus propias reglas."""
    if nombre.lower().endswith((".html", ".htm")):
        texto = re.sub(r"<!--.*?-->", " ", texto, flags=re.S)
    # los de bloque valen para html con estilo o guion dentro, y para js y css
    texto = re.sub(r"/\*.*?\*/", " ", texto, flags=re.S)
    texto = re.sub(r"^\s*//.*$", " ", texto, flags=re.M)
    return texto


def revisar_interactivo(carpeta, avisos):
    """
    Comprueba un interactivo SIN construir nada.

    Va aparte de la compresion a proposito: este es el aviso mas importante de
    todo el programa y tiene que salir en «revisar», no solo al armar. Si el
    equipo de contenido se entera de que su juego pide algo por internet
    cuando ya lo dio por terminado, la correccion cuesta el triple.

    En el aula NO HAY internet. Un <script src="https://..."> deja la pantalla
    en blanco delante de treinta alumnos, sin ningun mensaje de error.
    """
    if not os.path.exists(os.path.join(carpeta, "index.html")):
        avisos.append("%s: es una carpeta de interactivo y no tiene index.html. "
                      "El punto de entrada tiene que llamarse asi."
                      % os.path.basename(carpeta))
        return

    fuera, pesados = [], []
    for raiz, _, archivos in os.walk(carpeta):
        for a in archivos:
            completo = os.path.join(raiz, a)
            if a.lower().endswith((".html", ".htm", ".js", ".css")):
                texto = sin_comentarios(abrir_texto(completo), a)
                for m in re.finditer(r'(?:src|href)\s*=\s*["\'](https?://[^"\']+)', texto):
                    fuera.append("%s pide %s" % (a, m.group(1)[:52]))
                if "localStorage" in texto or "sessionStorage" in texto:
                    avisos.append("%s: %s usa almacenamiento del navegador. El "
                                  "interactivo se sirve desde una direccion que "
                                  "cambia en cada arranque, asi que lo guardado se "
                                  "pierde. Guarda el estado en variables."
                                  % (os.path.basename(carpeta), a))
            if os.path.getsize(completo) > 40 * 1024 * 1024:
                pesados.append(a)

    for f in fuera:
        avisos.append("SIN INTERNET · %s: %s. En el aula no hay conexion y esto "
                      "se queda en blanco. Descarga el archivo y metelo dentro "
                      "de la carpeta." % (os.path.basename(carpeta), f))
    for p in pesados:
        avisos.append("%s: %s pesa mas de 40 MB dentro de un interactivo. "
                      "Sacalo como video suelto." % (os.path.basename(carpeta), p))


def comprimir_interactivo(carpeta, destino, avisos):
    """Empaqueta la carpeta. La revision ya se hizo antes, en revisar_interactivo."""
    if not os.path.exists(os.path.join(carpeta, "index.html")):
        return None
    with zipfile.ZipFile(destino, "w", zipfile.ZIP_DEFLATED) as z:
        for raiz, _, archivos in os.walk(carpeta):
            for a in sorted(archivos):
                completo = os.path.join(raiz, a)
                z.write(completo, os.path.relpath(completo, carpeta).replace(os.sep, "/"))
    return destino


# ---------------------------------------------------------------- recorrido

def recolectar(carpeta_curso):
    """Recorre el arbol y devuelve (especificacion, avisos, errores)."""
    carpeta_curso = os.path.abspath(carpeta_curso.rstrip(os.sep))
    avisos, errores = [], []

    # pais / nivel / grado / materia salen de la ruta
    partes = carpeta_curso.split(os.sep)
    if len(partes) < 4:
        errores.append("La ruta no tiene la forma PAIS/nivel/grado/materia.")
        return None, avisos, errores

    materia_slug, grado, nivel, pais = partes[-1], partes[-2], partes[-3], partes[-4]

    # Comprobacion de cordura de la ruta. Sin esto, una carpeta suelta produce
    # un curso con pais "TMP" y grado "contenido", que es peor que un error:
    # parece que funciono.
    NIVELES = ("preescolar", "primaria", "secundaria", "media")
    if len(pais) != 2 or not pais.isalpha() or nivel.lower() not in NIVELES:
        errores.append(
            "Esta carpeta no esta en el sitio correcto. Tiene que colgar de\n"
            "      contenido\\<PAIS>\\<nivel>\\<grado>\\<materia>\n"
            "    con el pais en dos letras y el nivel entre: %s.\n"
            "    Por ejemplo:  contenido\\CO\\secundaria\\09\\humanidades"
            % ", ".join(NIVELES))
        return None, avisos, errores

    grado = grado.lstrip("0") or grado

    ficha = leer_claves(os.path.join(carpeta_curso, "curso.txt"))
    if not ficha:
        errores.append("Falta curso.txt en %s" % carpeta_curso)
        return None, avisos, errores

    clave = ficha.get("clave") or "%s-%s-%s-%s" % (
        pais.lower(), nivel.lower(), grado, materia_slug.lower())

    if "titulo" not in ficha:
        avisos.append("curso.txt no tiene titulo. Se usa el nombre de la carpeta.")

    nodos, por_codigo = leer_indice(os.path.join(carpeta_curso, "indice.txt"), avisos)

    spec = {
        "paquete": {
            "clave_paquete": clave,
            "version": ficha.get("version", "1"),
            "pais": pais.upper(),
            "nivel_clave": nivel.lower(),
            "grado": grado,
            "asignatura": ficha.get("materia", materia_slug),
            "idioma": ficha.get("idioma", "es"),
            "titulo": ficha.get("titulo", materia_slug),
            "descripcion": ficha.get("descripcion", ""),
            "emisor": ficha.get("emisor", "AVACOM"),
        },
        "medios_origen": None,      # se rellena al armar
        "taxonomia": nodos,
        "elementos": [],
        "voz": [],
    }

    medios = {}       # nombre destino -> ruta origen
    temas = sorted(d for d in os.listdir(carpeta_curso)
                   if os.path.isdir(os.path.join(carpeta_curso, d)))
    if not temas:
        errores.append("No hay ninguna carpeta de tema dentro del curso.")

    for tema in temas:
        ruta_tema = os.path.join(carpeta_curso, tema)

        # el codigo va delante del nombre de la carpeta: "DBA-9-03 Romanticismo"
        m = re.match(r"^\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s+(.*)$", tema)
        codigo = m.group(1).upper() if m else None
        tax_ref = por_codigo.get(codigo or "")

        if tax_ref is None:
            avisos.append("La carpeta «%s» no coincide con ningun codigo de "
                          "indice.txt. Su material quedara colgando de la raiz."
                          % tema)
            tax_ref = nodos[0]["taxonomia_ref"] if nodos else None

        secuencia = []
        entradas = sorted(os.listdir(ruta_tema), key=lambda x: partir_nombre(x)[0])

        for nombre in entradas:
            completo = os.path.join(ruta_tema, nombre)
            orden, titulo, ext = partir_nombre(nombre)

            if os.path.isdir(completo):
                # Una carpeta con guion.txt es una leccion: se genera su
                # index.html con el aspecto de AVACOM antes de seguir. Una
                # carpeta que ya trae index.html es un interactivo suelto.
                if os.path.exists(os.path.join(completo, "guion.txt")):
                    avacom_leccion.construir(completo, avisos)
                revisar_interactivo(completo, avisos)
                ref = "%s-%s" % (clave, rebanada(titulo))
                destino = ref + ".zip"
                medios[destino] = ("__carpeta__", completo)
                spec["elementos"].append({
                    "elemento_ref": ref, "tipo": "interactivo", "titulo": titulo,
                    "taxonomia_ref": tax_ref, "archivo": destino,
                })
                secuencia.append(ref)
                continue

            if nombre.lower() == "leccion.txt":
                continue

            if ext == ".txt":
                if "banco" in nombre.lower():
                    ref = "%s-%s" % (clave, rebanada(titulo))
                    spec["elementos"].append(
                        leer_banco(completo, ref, tax_ref, avisos))
                    # el banco NO entra en la secuencia de la leccion: no se
                    # "da" en clase, es de donde el examen saca preguntas
                    continue
                if "evalua" in nombre.lower():
                    ref = "%s-%s" % (clave, rebanada(titulo))
                    spec["elementos"].append(
                        leer_evaluacion(completo, ref, tax_ref, avisos))
                    secuencia.append(ref)
                else:
                    avisos.append("Se ignora %s: solo se leen leccion.txt y los "
                                  "archivos con «evaluacion» en el nombre." % nombre)
                continue

            if ext not in POR_EXTENSION:
                avisos.append("Se ignora %s: la extension %s no es de las que "
                              "el componente sabe mostrar." % (nombre, ext or "(ninguna)"))
                continue

            ref = "%s-%s" % (clave, rebanada(titulo))
            destino = ref + ext
            medios[destino] = ("__archivo__", completo)
            spec["elementos"].append({
                "elemento_ref": ref, "tipo": POR_EXTENSION[ext], "titulo": titulo,
                "taxonomia_ref": tax_ref, "archivo": destino,
            })
            secuencia.append(ref)

        # la leccion cose el tema en el orden en que se ha de dar
        if secuencia:
            notas = leer_claves(os.path.join(ruta_tema, "leccion.txt"))
            titulo_leccion = notas.get("titulo") or (m.group(2) if m else tema)
            spec["elementos"].append({
                "elemento_ref": "%s-leccion-%s" % (clave, rebanada(titulo_leccion)),
                "tipo": "leccion",
                "titulo": titulo_leccion,
                "taxonomia_ref": tax_ref,
                "descripcion": notas.get("descripcion", ""),
                "secuencia": [{"item_ref": r, "nota": None} for r in secuencia],
            })

    # referencias repetidas: dos titulos iguales dan la misma referencia
    vistos = {}
    for e in spec["elementos"]:
        vistos.setdefault(e["elemento_ref"], []).append(e["titulo"])
    for ref, titulos in vistos.items():
        if len(titulos) > 1:
            errores.append("Dos materiales dan la misma referencia (%s): %s. "
                           "Cambia uno de los titulos." % (ref, " · ".join(titulos)))

    if not spec["elementos"]:
        errores.append("El curso no tiene ni un material.")

    return (spec, medios), avisos, errores


# ---------------------------------------------------------------- ordenes

def revisar(carpeta):
    resultado, avisos, errores = recolectar(carpeta)

    print()
    print("Revision de %s" % carpeta)
    print("=" * 60)

    if resultado:
        (spec, medios) = resultado
        p = spec["paquete"]
        print()
        print("  %s" % p["titulo"])
        print("  %s · %s · grado %s · %s" % (p["pais"], p["nivel_clave"], p["grado"], p["asignatura"]))
        print("  clave: %s   version: %s" % (p["clave_paquete"], p["version"]))
        print()
        print("  %d nodos de indice curricular" % len(spec["taxonomia"]))
        cuenta = {}
        for e in spec["elementos"]:
            cuenta[e["tipo"]] = cuenta.get(e["tipo"], 0) + 1
        print("  %d materiales:  %s" % (
            len(spec["elementos"]),
            "  ".join("%s %d" % (t, n) for t, n in sorted(cuenta.items()))))
        reactivos = sum(len(e.get("preguntas", [])) for e in spec["elementos"])
        if reactivos:
            print("  %d reactivos" % reactivos)

    if errores:
        print()
        print("  NO SE PUEDE ARMAR TODAVIA")
        for e in errores:
            print("    · %s" % e)

    if avisos:
        print()
        print("  Avisos (%d) — se puede armar, pero conviene mirarlos:" % len(avisos))
        for a in avisos:
            print("    · %s" % a)

    print()
    if not errores and not avisos:
        print("  Todo correcto. Listo para armar.")
    elif not errores:
        print("  Se puede armar.")
    print()
    return 1 if errores else 0


def armar(carpeta, destino=None):
    resultado, avisos, errores = recolectar(carpeta)
    if errores:
        revisar(carpeta)
        return 1

    (spec, medios) = resultado
    destino = destino or os.path.join(AQUI, "specs")
    os.makedirs(destino, exist_ok=True)

    # los medios se juntan en una carpeta propia del curso, con los nombres
    # que espera la especificacion
    carpeta_medios = os.path.join(destino, "medios-" + spec["paquete"]["clave_paquete"])
    os.makedirs(carpeta_medios, exist_ok=True)
    for nombre, (clase, origen) in medios.items():
        salida = os.path.join(carpeta_medios, nombre)
        if clase == "__carpeta__":
            comprimir_interactivo(origen, salida, avisos)
        else:
            with open(origen, "rb") as f, open(salida, "wb") as g:
                g.write(f.read())

    spec["medios_origen"] = os.path.relpath(carpeta_medios, destino).replace(os.sep, "/")

    ruta_spec = os.path.join(destino, "spec_%s.json" % spec["paquete"]["clave_paquete"].replace("-", "_"))
    with open(ruta_spec, "w", encoding="utf-8") as f:
        json.dump(spec, f, ensure_ascii=False, indent=2)

    print()
    print("Especificacion generada:")
    print("  %s" % ruta_spec)
    print("  %d materiales · %d medios copiados" % (len(spec["elementos"]), len(medios)))
    if avisos:
        print()
        print("  Avisos (%d):" % len(avisos))
        for a in avisos:
            print("    · %s" % a)
    print()
    print("Ahora, para construir el paquete:")
    print("  py -3 avacom_empaquetador.py ejemplos C:\\avacom\\claro")
    print()
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    orden = sys.argv[1]
    if orden == "revisar":
        sys.exit(revisar(sys.argv[2]))
    elif orden == "armar":
        sys.exit(armar(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None))
    else:
        print(__doc__)
        sys.exit(1)
