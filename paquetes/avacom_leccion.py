#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · renderizador de lecciones

    py -3 avacom_leccion.py <carpeta_de_la_leccion>

Convierte un guion.txt en una leccion en HTML con el aspecto de AVACOM.

POR QUE EL EQUIPO DE CONTENIDO NO ESCRIBE EL HTML

Si les damos una plantilla de HTML para que la copien y la cambien, en tres
meses habra veinte lecciones con veinte diseños distintos: alguien tocara un
color, otro cambiara un tamaño de letra «solo un poco», y el estandar habra
durado exactamente una semana. Es lo que pasa siempre, y no es culpa de nadie.

Asi que escriben CONTENIDO y nosotros ponemos la FORMA. El guion.txt no tiene
ni una etiqueta de HTML; el aspecto sale de aqui y es el mismo para todos. Si
mañana hay que cambiar el diseño de todas las lecciones del catalogo, se cambia
este archivo y se vuelven a generar.

EL FORMATO DEL GUION

    titulo: El Romanticismo en la novela del siglo XIX
    objetivo: Reconoce rasgos del Romanticismo en una obra del siglo XIX
    duracion: 50

    ## De que va esto

    Un parrafo normal. Las lineas seguidas forman un parrafo; una linea en
    blanco empieza otro.

    > Una idea que hay que destacar.

    - primer punto de una lista
    - segundo punto

    IMAGEN: retrato.jpg | Jorge Isaacs hacia 1870
    VIDEO: valle.mp4 | El valle del Cauca
    AUDIO: nocturno.mp3 | Nocturno, leido en voz alta
    PDF: guia.pdf | Guia de lectura completa

    QUIZ
    P: En que año se publico Maria?
    - 1867 *
    - 1885
    - 1902
    retro: 1867, en plena consolidacion del Romanticismo.

    ## Siguiente seccion

El asterisco marca la opcion correcta. Nada mas.
"""

import html
import os
import re
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))

EXT_IMAGEN = (".png", ".jpg", ".jpeg", ".webp", ".svg")
EXT_VIDEO = (".mp4", ".webm")
EXT_AUDIO = (".mp3", ".wav", ".m4a")


# ------------------------------------------------------------ lectura

def abrir_texto(ruta):
    crudo = open(ruta, "rb").read()
    for cod in ("utf-8-sig", "utf-8", "cp1252", "latin-1"):
        try:
            return crudo.decode(cod)
        except UnicodeDecodeError:
            continue
    return crudo.decode("utf-8", errors="replace")


def leer_guion(ruta, avisos):
    """Devuelve (ficha, secciones). Cada seccion es (titulo, [bloques])."""
    ficha, secciones = {}, []
    actual = None            # seccion en curso
    parrafo, lista = [], []
    quiz = None

    def cerrar_parrafo():
        nonlocal parrafo
        if parrafo:
            actual["bloques"].append(("parrafo", " ".join(parrafo)))
            parrafo = []

    def cerrar_lista():
        nonlocal lista
        if lista:
            actual["bloques"].append(("lista", lista))
            lista = []

    def cerrar_quiz():
        nonlocal quiz
        if quiz is None:
            return
        if not quiz["opciones"]:
            avisos.append("QUIZ «%s»: no tiene opciones debajo." % quiz["enunciado"][:40])
        elif not any(o["correcta"] for o in quiz["opciones"]):
            avisos.append("QUIZ «%s»: ninguna opcion lleva el asterisco que marca "
                          "la correcta." % quiz["enunciado"][:40])
        actual["bloques"].append(("quiz", quiz))
        quiz = None

    def nueva_seccion(titulo):
        nonlocal actual
        cerrar_parrafo(); cerrar_lista(); cerrar_quiz()
        actual = {"titulo": titulo, "bloques": []}
        secciones.append(actual)

    en_cabecera = True

    for cruda in abrir_texto(ruta).splitlines():
        linea = cruda.strip()

        if linea.startswith("#") and not linea.startswith("##"):
            continue                                    # comentario

        if linea.startswith("##"):
            nueva_seccion(linea.lstrip("#").strip())
            en_cabecera = False
            continue

        # cabecera: clave: valor, hasta la primera seccion
        if en_cabecera and ":" in linea and not linea.upper().startswith(
                ("IMAGEN:", "VIDEO:", "AUDIO:", "PDF:", "P:", "RETRO:")):
            k, v = linea.split(":", 1)
            ficha[k.strip().lower()] = v.strip()
            continue

        if actual is None:
            if not linea:
                continue
            nueva_seccion("")                           # contenido sin seccion
            en_cabecera = False

        if not linea:
            cerrar_parrafo(); cerrar_lista(); cerrar_quiz()
            continue

        arriba = linea.upper()

        if arriba == "QUIZ":
            cerrar_parrafo(); cerrar_lista(); cerrar_quiz()
            quiz = {"enunciado": "", "opciones": [], "retro": ""}
            continue

        if quiz is not None:
            if arriba.startswith(("P:", "PREGUNTA:")):
                quiz["enunciado"] = linea.split(":", 1)[1].strip()
            elif arriba.startswith(("RETRO:", "RETROALIMENTACION:")):
                quiz["retro"] = linea.split(":", 1)[1].strip()
            elif linea.startswith("- "):
                texto = linea[2:].strip()
                correcta = texto.endswith("*")
                quiz["opciones"].append({
                    "texto": texto.rstrip("*").strip(),
                    "correcta": correcta,
                })
            continue

        for etiqueta, clase in (("IMAGEN:", "imagen"), ("VIDEO:", "video"),
                                ("AUDIO:", "audio"), ("PDF:", "pdf")):
            if arriba.startswith(etiqueta):
                cerrar_parrafo(); cerrar_lista()
                resto = linea.split(":", 1)[1].strip()
                partes = [p.strip() for p in resto.split("|")]
                actual["bloques"].append(
                    (clase, {"archivo": partes[0],
                             "pie": partes[1] if len(partes) > 1 else ""}))
                break
        else:
            if linea.startswith("> "):
                cerrar_parrafo(); cerrar_lista()
                actual["bloques"].append(("destacado", linea[2:].strip()))
            elif linea.startswith("- "):
                cerrar_parrafo()
                lista.append(linea[2:].strip())
            else:
                cerrar_lista()
                parrafo.append(linea)

    cerrar_parrafo(); cerrar_lista(); cerrar_quiz()
    return ficha, secciones


# ------------------------------------------------------------ revision

def revisar_medios(carpeta, secciones, avisos):
    """Comprueba que cada medio citado exista de verdad y sea del tipo que dice."""
    for s in secciones:
        for clase, dato in s["bloques"]:
            if clase not in ("imagen", "video", "audio", "pdf"):
                continue
            arch = dato["archivo"]
            ruta = os.path.join(carpeta, arch)
            if not os.path.exists(ruta):
                avisos.append("Falta el archivo «%s», citado en la seccion «%s»."
                              % (arch, s["titulo"] or "sin titulo"))
                continue
            ext = os.path.splitext(arch)[1].lower()
            esperado = {"imagen": EXT_IMAGEN, "video": EXT_VIDEO,
                        "audio": EXT_AUDIO, "pdf": (".pdf",)}[clase]
            if ext not in esperado:
                avisos.append("«%s» esta puesto como %s pero su extension es %s. "
                              "Deberia ser una de: %s."
                              % (arch, clase.upper(), ext, " ".join(esperado)))
            if clase == "video" and os.path.getsize(ruta) > 300 * 1024 * 1024:
                avisos.append("«%s» pesa mas de 300 MB. Comprimelo o partelo: "
                              "un curso entero no puede pesar varios gigas." % arch)


# ------------------------------------------------------------ el aspecto

# Todo el diseño de AVACOM en un sitio. Cambiar esto cambia TODAS las lecciones
# del catalogo a la vez, que es exactamente por lo que el equipo de contenido no
# escribe HTML.
ESTILO = """
:root{
  --rojo:#E5262B; --rojo-oscuro:#C1191D;
  --papel:#E9EDF3; --hundido:#E3E7EE;
  --sombra:#C2C9D6; --luz:#FFFFFF;
  --tinta:#1D1D1F; --suave:#6E6E73; --borde:#D6DCE6;
  --bien:#1F8A4C; --mal:#E5262B;
}
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{
  margin:0;background:var(--papel);color:var(--tinta);
  font-family:'Segoe UI',system-ui,-apple-system,sans-serif;
  font-size:21px;line-height:1.65;
}

/* --- barra de progreso, siempre visible --- */
#progreso{position:fixed;top:0;left:0;right:0;height:8px;background:var(--hundido);z-index:100}
#barra{height:100%;width:0;background:var(--rojo);transition:width .35s ease}
#etiqueta{
  position:fixed;top:18px;right:24px;z-index:100;
  background:var(--papel);border-radius:999px;padding:8px 20px;font-size:16px;
  box-shadow:5px 5px 12px var(--sombra),-5px -5px 12px var(--luz);
}

.marco{max-width:1080px;margin:0 auto;padding:64px 40px 120px}

/* --- portada --- */
.portada{margin-bottom:56px}
.cinta{display:inline-block;background:var(--rojo);color:#fff;font-size:14px;
  letter-spacing:1.5px;padding:6px 18px;border-radius:999px;margin-bottom:18px}
h1{font-size:46px;line-height:1.2;margin:0 0 14px;letter-spacing:-.02em}
.objetivo{font-size:23px;color:var(--suave);margin:0 0 8px;max-width:44ch}
.duracion{font-size:17px;color:var(--suave)}

/* --- indice --- */
.indice{
  background:var(--hundido);border-radius:22px;padding:26px 32px;margin-bottom:56px;
  box-shadow:inset 4px 4px 10px var(--sombra),inset -4px -4px 10px var(--luz);
}
.indice h2{font-size:15px;letter-spacing:1.4px;color:var(--rojo-oscuro);margin:0 0 14px}
.indice ol{margin:0;padding-left:26px}
.indice li{padding:5px 0;font-size:19px}
.indice a{color:var(--tinta);text-decoration:none}
.indice a:hover{color:var(--rojo)}

/* --- secciones --- */
section{margin-bottom:64px;scroll-margin-top:60px}
h2.sec{font-size:33px;margin:0 0 22px;display:flex;align-items:center;gap:14px}
h2.sec::before{content:'';width:6px;height:32px;background:var(--rojo);border-radius:3px;flex:0 0 6px}
p{margin:0 0 22px;max-width:66ch}
ul.puntos{margin:0 0 24px;padding:0;list-style:none;max-width:66ch}
ul.puntos li{position:relative;padding:8px 0 8px 30px}
ul.puntos li::before{content:'';position:absolute;left:8px;top:19px;width:8px;height:8px;
  border-radius:50%;background:var(--rojo)}
.destacado{
  border-left:5px solid var(--rojo);background:var(--hundido);
  border-radius:0 18px 18px 0;padding:22px 28px;margin:0 0 26px;
  font-size:23px;max-width:66ch;
}

/* --- medios --- */
figure{margin:0 0 30px}
figure img,figure video{width:100%;border-radius:20px;display:block;
  box-shadow:8px 8px 18px var(--sombra),-8px -8px 18px var(--luz)}
figure audio{width:100%;height:56px}
figcaption{font-size:17px;color:var(--suave);margin-top:12px;text-align:center}
.medio-caja{
  background:var(--papel);border-radius:22px;padding:20px;margin-bottom:30px;
  box-shadow:8px 8px 18px var(--sombra),-8px -8px 18px var(--luz);
}
.pdf-enlace{
  display:flex;align-items:center;gap:18px;text-decoration:none;color:var(--tinta);
  background:var(--papel);border-radius:20px;padding:24px 28px;margin-bottom:30px;
  box-shadow:7px 7px 16px var(--sombra),-7px -7px 16px var(--luz);
}
.pdf-enlace .icono{
  width:56px;height:56px;flex:0 0 56px;border-radius:14px;background:var(--rojo);
  color:#fff;display:grid;place-items:center;font-size:15px;font-weight:700;
}
.pdf-enlace b{display:block;font-size:21px}
.pdf-enlace span{font-size:16px;color:var(--suave)}

/* --- quiz --- */
.quiz{
  background:var(--papel);border-radius:24px;padding:32px;margin:0 0 34px;
  box-shadow:9px 9px 20px var(--sombra),-9px -9px 20px var(--luz);
}
.quiz .rotulo{font-size:14px;letter-spacing:1.4px;color:var(--rojo-oscuro);
  font-weight:700;margin-bottom:12px}
.quiz .enunciado{font-size:26px;font-weight:600;margin-bottom:24px;line-height:1.4}
.opcion{
  display:block;width:100%;text-align:left;min-height:72px;
  padding:18px 26px;margin-bottom:14px;font-size:21px;font-family:inherit;
  color:var(--tinta);background:var(--papel);
  border:3px solid transparent;border-radius:18px;cursor:pointer;
  box-shadow:6px 6px 14px var(--sombra),-6px -6px 14px var(--luz);
  transition:transform .12s;
}
.opcion:active{transform:translateY(2px)}
.opcion.bien{border-color:var(--bien)}
.opcion.mal{border-color:var(--mal)}
.opcion[disabled]{cursor:default;opacity:.75}
.opcion.bien[disabled]{opacity:1}
.respuesta{font-size:19px;margin-top:16px;padding:16px 22px;border-radius:16px;
  background:var(--hundido);display:none}
.respuesta.vista{display:block}

/* --- cierre --- */
.cierre{
  background:var(--hundido);border-radius:26px;padding:44px;text-align:center;
  box-shadow:inset 5px 5px 12px var(--sombra),inset -5px -5px 12px var(--luz);
}
.cierre h2{font-size:32px;margin:0 0 10px}
.cierre .marca{font-size:52px;font-weight:700;color:var(--rojo);margin:18px 0 6px}
.cierre p{color:var(--suave);margin:0 auto;max-width:52ch}

@media (max-width:820px){
  body{font-size:19px}
  .marco{padding:48px 22px 90px}
  h1{font-size:34px} h2.sec{font-size:27px}
  .quiz .enunciado{font-size:22px}
}
"""

GUION_JS = """
// Progreso: cuenta las secciones que ya se han visto y los quiz contestados.
// No usa almacenamiento del navegador a proposito: esto se sirve desde una
// direccion que cambia en cada arranque y lo guardado se perderia igual.
const TOTAL = window.__TOTAL__;
const vistas = new Set();
let contestados = 0, aciertos = 0;

function pintarProgreso(){
  const hecho = vistas.size + contestados;
  const pct = TOTAL ? Math.min(100, Math.round(hecho * 100 / TOTAL)) : 0;
  document.getElementById('barra').style.width = pct + '%';
  document.getElementById('etiqueta').textContent = pct + '% de la leccion';
  if (window.avacom && window.avacom.progreso) {
    window.avacom.progreso({ porcentaje: pct, aciertos: aciertos, quiz: contestados });
  }
  if (pct === 100) mostrarCierre();
}

const observador = new IntersectionObserver(es => {
  es.forEach(e => {
    if (e.isIntersecting) { vistas.add(e.target.id); pintarProgreso(); }
  });
}, { rootMargin: '0px 0px -55% 0px' });

document.querySelectorAll('section[id]').forEach(s => observador.observe(s));

function responder(boton, correcta, idQuiz){
  const caja = boton.closest('.quiz');
  if (caja.dataset.hecho) return;
  caja.dataset.hecho = '1';

  caja.querySelectorAll('.opcion').forEach(b => {
    b.disabled = true;
    if (b.dataset.correcta === '1') b.classList.add('bien');
  });
  if (!correcta) boton.classList.add('mal');

  const r = caja.querySelector('.respuesta');
  if (r) r.classList.add('vista');

  contestados++;
  if (correcta) aciertos++;
  pintarProgreso();
}

function mostrarCierre(){
  const c = document.getElementById('cierre');
  if (!c || c.dataset.visto) return;
  c.dataset.visto = '1';
  const m = document.getElementById('resultado');
  if (m && window.__QUIZ__ > 0) {
    m.textContent = aciertos + ' de ' + window.__QUIZ__;
  }
  if (window.avacom && window.avacom.terminado) {
    window.avacom.terminado({ aciertos: aciertos, total: window.__QUIZ__ });
  }
}

pintarProgreso();
"""


def esc(t):
    return html.escape(str(t), quote=True)


def render(ficha, secciones):
    titulo = ficha.get("titulo", "Leccion")
    objetivo = ficha.get("objetivo", "")
    duracion = ficha.get("duracion", "")

    n_quiz = sum(1 for s in secciones for c, _ in s["bloques"] if c == "quiz")
    n_sec = len([s for s in secciones if s["titulo"]])

    partes = []
    indice = []

    for i, s in enumerate(secciones, 1):
        sid = "s%d" % i
        if s["titulo"]:
            indice.append('<li><a href="#%s">%s</a></li>' % (sid, esc(s["titulo"])))

        cuerpo = ['<section id="%s">' % sid]
        if s["titulo"]:
            cuerpo.append('<h2 class="sec">%s</h2>' % esc(s["titulo"]))

        for clase, dato in s["bloques"]:
            if clase == "parrafo":
                cuerpo.append("<p>%s</p>" % esc(dato))
            elif clase == "destacado":
                cuerpo.append('<div class="destacado">%s</div>' % esc(dato))
            elif clase == "lista":
                cuerpo.append('<ul class="puntos">%s</ul>'
                              % "".join("<li>%s</li>" % esc(x) for x in dato))
            elif clase == "imagen":
                cuerpo.append(
                    '<figure><img src="%s" alt="%s" loading="lazy">%s</figure>'
                    % (esc(dato["archivo"]), esc(dato["pie"] or titulo),
                       '<figcaption>%s</figcaption>' % esc(dato["pie"]) if dato["pie"] else ""))
            elif clase == "video":
                cuerpo.append(
                    '<figure><video controls preload="metadata" src="%s"></video>%s</figure>'
                    % (esc(dato["archivo"]),
                       '<figcaption>%s</figcaption>' % esc(dato["pie"]) if dato["pie"] else ""))
            elif clase == "audio":
                cuerpo.append(
                    '<div class="medio-caja"><audio controls preload="metadata" src="%s"></audio>%s</div>'
                    % (esc(dato["archivo"]),
                       '<figcaption>%s</figcaption>' % esc(dato["pie"]) if dato["pie"] else ""))
            elif clase == "pdf":
                cuerpo.append(
                    '<a class="pdf-enlace" href="%s" target="_blank" rel="noopener">'
                    '<span class="icono">PDF</span><span><b>%s</b>'
                    '<span>Se abre en una ventana aparte</span></span></a>'
                    % (esc(dato["archivo"]), esc(dato["pie"] or dato["archivo"])))
            elif clase == "quiz":
                opciones = "".join(
                    '<button class="opcion" data-correcta="%d" onclick="responder(this,%s)">%s</button>'
                    % (1 if o["correcta"] else 0,
                       "true" if o["correcta"] else "false", esc(o["texto"]))
                    for o in dato["opciones"])
                retro = ('<div class="respuesta">%s</div>' % esc(dato["retro"])) if dato["retro"] else ""
                cuerpo.append(
                    '<div class="quiz"><div class="rotulo">COMPRUEBA SI LO ENTENDISTE</div>'
                    '<div class="enunciado">%s</div>%s%s</div>'
                    % (esc(dato["enunciado"]), opciones, retro))

        cuerpo.append("</section>")
        partes.append("\n".join(cuerpo))

    cierre = (
        '<div class="cierre" id="cierre">'
        '<h2>Terminaste la leccion</h2>'
        + ('<div class="marca" id="resultado">—</div>'
           '<p>Respuestas correctas de las preguntas de comprobacion. '
           'No cuenta como nota: sirve para saber si conviene repasar algo.</p>'
           if n_quiz else
           '<p>Ya puedes pasar a la siguiente, o volver sobre lo que quieras.</p>')
        + "</div>")

    return """<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>%(titulo)s</title>
<style>%(estilo)s</style>
</head>
<body>

<div id="progreso"><div id="barra"></div></div>
<div id="etiqueta">0%%</div>

<div class="marco">

  <div class="portada">
    <div class="cinta">LECCION</div>
    <h1>%(titulo)s</h1>
    %(objetivo)s
    %(duracion)s
  </div>

  %(indice)s

  %(cuerpo)s

  %(cierre)s
</div>

<script>
window.__TOTAL__ = %(total)d;
window.__QUIZ__  = %(nquiz)d;
%(js)s
</script>
</body>
</html>
""" % {
        "titulo": esc(titulo),
        "estilo": ESTILO,
        "objetivo": '<p class="objetivo">%s</p>' % esc(objetivo) if objetivo else "",
        "duracion": '<div class="duracion">%s minutos</div>' % esc(duracion) if duracion else "",
        "indice": ('<nav class="indice"><h2>EN ESTA LECCION</h2><ol>%s</ol></nav>'
                   % "".join(indice)) if len(indice) > 1 else "",
        "cuerpo": "\n\n".join(partes),
        "cierre": cierre,
        "total": max(1, n_sec + n_quiz),
        "nquiz": n_quiz,
        "js": GUION_JS,
    }


# ------------------------------------------------------------ orden

def construir(carpeta, avisos=None):
    """Genera index.html dentro de la carpeta. Devuelve (titulo, avisos)."""
    propios = avisos is None
    avisos = [] if propios else avisos

    guion = os.path.join(carpeta, "guion.txt")
    if not os.path.exists(guion):
        return None, avisos

    ficha, secciones = leer_guion(guion, avisos)
    if not ficha.get("titulo"):
        avisos.append("El guion de «%s» no tiene «titulo:» en la cabecera."
                      % os.path.basename(carpeta))
    if not secciones:
        avisos.append("El guion de «%s» no tiene ninguna seccion (##)."
                      % os.path.basename(carpeta))

    revisar_medios(carpeta, secciones, avisos)

    with open(os.path.join(carpeta, "index.html"), "w", encoding="utf-8") as f:
        f.write(render(ficha, secciones))

    if propios:
        n_quiz = sum(1 for s in secciones for c, _ in s["bloques"] if c == "quiz")
        print()
        print("  %s" % ficha.get("titulo", "(sin titulo)"))
        print("  %d secciones · %d preguntas de comprobacion" % (len(secciones), n_quiz))
        print("  generado: %s" % os.path.join(carpeta, "index.html"))
        if avisos:
            print()
            for a in avisos:
                print("    · %s" % a)
        print()

    return ficha.get("titulo"), avisos


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    titulo, avisos = construir(sys.argv[1])
    sys.exit(0 if titulo else 1)
