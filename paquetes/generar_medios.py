#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AVACOM · genera los medios de muestra de los dos paquetes de ejemplo

Produce material de verdad, no marcadores de posicion, para poder probar los
visores: imagenes, un documento, dos videos, un interactivo y los audios.

No sustituye al trabajo del equipo de contenido. Es material de prueba con la
forma correcta: proporciones de la pantalla interactiva, texto legible a cuatro
metros y duraciones realistas.

    py -3 generar_medios.py [destino]
"""

import os, sys, math, wave, struct, zipfile, subprocess, shutil

AQUI = os.path.dirname(os.path.abspath(__file__))
ROJO = (229, 38, 43)
TINTA = (29, 29, 31)
PAPEL = (233, 237, 243)


# ---------------------------------------------------------------- imagenes

def lamina(ruta, titulo, subtitulo, emojis, fondo):
    from PIL import Image, ImageDraw, ImageFont
    # 16:9 a la resolucion de la pantalla interactiva
    W, H = 1920, 1080
    img = Image.new("RGB", (W, H), fondo)
    d = ImageDraw.Draw(img)

    def fuente(tam):
        for f in [r"C:\Windows\Fonts\segoeuib.ttf",
                  r"C:\Windows\Fonts\arialbd.ttf",
                  r"C:\Windows\Fonts\calibrib.ttf"]:
            if os.path.exists(f):
                try: return ImageFont.truetype(f, tam)
                except Exception: pass
        # Solo se llega aqui si no hay ni una fuente del sistema, que no deberia
        # pasar. El tamaño en la de reserva pide una version reciente de la
        # biblioteca de imagen, asi que se prueba y se cae hacia atras.
        try: return ImageFont.load_default(tam)
        except TypeError: return ImageFont.load_default()

    # banda de marca
    d.rectangle([0, 0, W, 12], fill=ROJO)

    d.text((90, 90), titulo, font=fuente(96), fill=TINTA)
    d.text((90, 210), subtitulo, font=fuente(44), fill=(110, 110, 115))

    # figuras grandes, que es lo que ve un nino de cinco anos
    x = 180
    for e in emojis:
        d.rounded_rectangle([x, 380, x + 400, 860], radius=48, fill=(255, 255, 255),
                            outline=(200, 205, 215), width=3)
        d.text((x + 200, 560), e[0], font=fuente(150), fill=TINTA, anchor="mm")
        d.text((x + 200, 760), e[1], font=fuente(52), fill=(110, 110, 115), anchor="mm")
        x += 480

    d.text((90, 990), "AVACOM · material de prueba", font=fuente(28), fill=(150, 150, 158))
    img.save(ruta, "PNG", optimize=True)
    return ruta


# ---------------------------------------------------------------- documento

def documento(ruta):
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.units import cm
    from reportlab.lib import colors
    from reportlab.pdfgen import canvas

    c = canvas.Canvas(ruta, pagesize=A4)
    W, H = A4

    def cabecera(titulo, pagina):
        c.setFillColorRGB(*[v / 255 for v in ROJO])
        c.rect(0, H - 0.5 * cm, W, 0.5 * cm, fill=1, stroke=0)
        c.setFillColorRGB(*[v / 255 for v in TINTA])
        c.setFont("Helvetica-Bold", 20)
        c.drawString(2.5 * cm, H - 3 * cm, titulo)
        c.setFont("Helvetica", 9)
        c.setFillColor(colors.grey)
        c.drawRightString(W - 2.5 * cm, 1.5 * cm, f"Matematicas grado 8 · pagina {pagina}")

    cabecera("La funcion lineal", 1)
    c.setFillColorRGB(*[v / 255 for v in TINTA])
    c.setFont("Helvetica", 12)
    y = H - 4.5 * cm
    for linea in [
        "Una funcion lineal relaciona dos cantidades de manera que, cuando una",
        "cambia, la otra cambia siempre en la misma proporcion.",
        "",
        "Se escribe asi:        y = m x + b",
        "",
        "donde m es la pendiente y b es el corte con el eje vertical.",
        "",
        "La pendiente dice cuanto cambia y cada vez que x aumenta en uno. Si la",
        "pendiente es 3, cada paso a la derecha sube tres. Si es negativa, baja.",
        "",
        "Un ejemplo de todos los dias:",
        "",
        "Un taxi cobra 4000 de banderazo y 1500 por kilometro recorrido.",
        "El costo total es:      y = 1500 x + 4000",
        "",
        "Aqui 1500 es la pendiente, porque es lo que sube el precio por cada",
        "kilometro, y 4000 es el corte, porque es lo que se paga antes de",
        "avanzar un solo metro.",
    ]:
        c.drawString(2.5 * cm, y, linea)
        y -= 0.65 * cm

    # una grafica sencilla, dibujada de verdad
    ox, oy, esc = 3 * cm, 4 * cm, 0.9 * cm
    c.setStrokeColor(colors.lightgrey); c.setLineWidth(0.5)
    for i in range(9):
        c.line(ox + i * esc, oy, ox + i * esc, oy + 7 * esc)
        c.line(ox, oy + i * esc if i < 8 else oy, ox + 8 * esc, oy + i * esc if i < 8 else oy)
    c.setStrokeColorRGB(*[v / 255 for v in TINTA]); c.setLineWidth(1.2)
    c.line(ox, oy, ox + 8 * esc, oy)
    c.line(ox, oy, ox, oy + 7 * esc)
    c.setStrokeColorRGB(*[v / 255 for v in ROJO]); c.setLineWidth(2)
    c.line(ox, oy + 1 * esc, ox + 6 * esc, oy + 7 * esc)
    c.setFillColorRGB(*[v / 255 for v in ROJO]); c.setFont("Helvetica-Bold", 10)
    c.drawString(ox + 6.2 * esc, oy + 6.8 * esc, "y = x + 1")

    c.showPage()
    cabecera("Ejercicios", 2)
    c.setFillColorRGB(*[v / 255 for v in TINTA]); c.setFont("Helvetica", 12)
    y = H - 4.5 * cm
    for linea in [
        "1. Cual es la pendiente de y = 3x - 5?",
        "2. Halla el corte con el eje vertical de y = -2x + 7",
        "3. Una recta pasa por (0,2) y (4,10). Cual es su pendiente?",
        "4. Escribe la funcion del taxi si el banderazo sube a 5000.",
        "5. Explica con tus palabras que representa la pendiente.",
    ]:
        c.drawString(2.5 * cm, y, linea); y -= 1.1 * cm
    c.showPage()
    c.save()
    return ruta


# ---------------------------------------------------------------- audio

def tono(ruta, notas, seg_por_nota=0.45, hz_muestreo=22050):
    """Genera un audio real y audible. En produccion aqui va la voz generada
    al empaquetar; esto es material de prueba con la forma correcta."""
    with wave.open(ruta, "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(hz_muestreo)
        marcos = bytearray()
        for f in notas:
            n = int(hz_muestreo * seg_por_nota)
            for i in range(n):
                # envolvente suave, para que no chasquee
                env = min(1.0, i / (hz_muestreo * 0.02), (n - i) / (hz_muestreo * 0.05))
                v = int(12000 * env * math.sin(2 * math.pi * f * i / hz_muestreo))
                marcos += struct.pack("<h", v)
        w.writeframes(bytes(marcos))
    return ruta


# ---------------------------------------------------------------- video

def video(ruta, titulo, subtitulo, segundos, color):
    """Video real con ffmpeg. Si no esta, se deja un audio en su lugar."""
    if not shutil.which("ffmpeg"):
        return None
    filtro = (
        f"drawtext=text='{titulo}':fontcolor=0x1d1d1f:fontsize=90:"
        f"x=(w-text_w)/2:y=(h-text_h)/2-60,"
        f"drawtext=text='{subtitulo}':fontcolor=0x6e6e73:fontsize=44:"
        f"x=(w-text_w)/2:y=(h-text_h)/2+80,"
        f"drawbox=x=0:y=0:w=iw:h=10:color=0xe5262b:t=fill"
    )
    cmd = ["ffmpeg", "-y", "-loglevel", "error",
           "-f", "lavfi", "-i", f"color=c={color}:s=1280x720:d={segundos}:r=25",
           "-f", "lavfi", "-i", f"sine=frequency=440:duration={segundos}",
           "-vf", filtro, "-c:v", "libx264", "-pix_fmt", "yuv420p",
           "-c:a", "aac", "-shortest", ruta]
    try:
        subprocess.run(cmd, check=True, capture_output=True)
        return ruta
    except subprocess.CalledProcessError:
        # algunas compilaciones de ffmpeg no traen drawtext
        cmd = ["ffmpeg", "-y", "-loglevel", "error",
               "-f", "lavfi", "-i", f"color=c={color}:s=1280x720:d={segundos}:r=25",
               "-f", "lavfi", "-i", f"sine=frequency=440:duration={segundos}",
               "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", ruta]
        subprocess.run(cmd, check=True, capture_output=True)
        return ruta


# ---------------------------------------------------------------- interactivo

def interactivo(ruta):
    """Un paquete interactivo es un zip con su punto de entrada. El visor lo
    abre en un componente de navegacion embebido."""
    html = """<!doctype html>
<html lang="es"><head><meta charset="utf-8">
<title>Explorador de rectas</title>
<style>
 body{font-family:'Segoe UI',system-ui,sans-serif;background:#e9edf3;color:#1d1d1f;
      margin:0;display:grid;place-items:center;height:100vh}
 .caja{background:#e9edf3;border-radius:28px;padding:40px 48px;
       box-shadow:10px 10px 22px #c2c9d6,-10px -10px 22px #fff;text-align:center}
 h1{font-size:28px;margin:0 0 6px} p{color:#6e6e73;margin:0 0 26px}
 canvas{background:#fff;border-radius:16px}
 label{display:block;margin:18px 0 6px;font-size:15px}
 input{width:340px}
 b{color:#c1191d;font-variant-numeric:tabular-nums}
</style></head><body>
<div class="caja">
  <h1>Explorador de rectas</h1>
  <p>Mueve la pendiente y el corte, y mira que le pasa a la recta</p>
  <canvas id="c" width="440" height="320"></canvas>
  <label>Pendiente m = <b id="vm">1.0</b></label>
  <input id="m" type="range" min="-4" max="4" step="0.1" value="1">
  <label>Corte b = <b id="vb">0</b></label>
  <input id="b" type="range" min="-6" max="6" step="1" value="0">
</div>
<script>
const c=document.getElementById('c'),x=c.getContext('2d');
function dibuja(){
  const m=+document.getElementById('m').value, b=+document.getElementById('b').value;
  document.getElementById('vm').textContent=m.toFixed(1);
  document.getElementById('vb').textContent=b;
  x.clearRect(0,0,440,320);
  x.strokeStyle='#e3e7ee'; x.lineWidth=1;
  for(let i=0;i<=440;i+=40){x.beginPath();x.moveTo(i,0);x.lineTo(i,320);x.stroke();}
  for(let j=0;j<=320;j+=40){x.beginPath();x.moveTo(0,j);x.lineTo(440,j);x.stroke();}
  x.strokeStyle='#1d1d1f'; x.lineWidth=1.5;
  x.beginPath();x.moveTo(0,160);x.lineTo(440,160);x.moveTo(220,0);x.lineTo(220,320);x.stroke();
  x.strokeStyle='#e5262b'; x.lineWidth=3;
  x.beginPath();
  x.moveTo(0,160-(m*(-220/40)+b)*40);
  x.lineTo(440,160-(m*(220/40)+b)*40);
  x.stroke();
}
document.getElementById('m').oninput=dibuja;
document.getElementById('b').oninput=dibuja;
dibuja();
</script></body></html>"""
    with zipfile.ZipFile(ruta, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("index.html", html)
    return ruta


# ---------------------------------------------------------- literatura

def lit_linea_tiempo(ruta):
    """Lamina de linea de tiempo. Un area distinta pide una forma distinta de
    lamina: aqui no hay tres figuras grandes, hay una linea con hitos."""
    from PIL import Image, ImageDraw, ImageFont
    W, H = 1920, 1080
    img = Image.new("RGB", (W, H), PAPEL)
    d = ImageDraw.Draw(img)

    def fuente(tam, negrita=True):
        cands = [r"C:\Windows\Fonts\segoeui%s.ttf" % ("b" if negrita else ""),
                 r"C:\Windows\Fonts\arial%s.ttf" % ("bd" if negrita else "")]
        for f in cands:
            if os.path.exists(f):
                try: return ImageFont.truetype(f, tam)
                except Exception: pass
        try: return ImageFont.load_default(tam)
        except TypeError: return ImageFont.load_default()

    d.rectangle([0, 0, W, 12], fill=ROJO)
    d.text((90, 80), "Del Romanticismo al Modernismo", font=fuente(84), fill=TINTA)
    d.text((90, 190), "Literatura colombiana del siglo XIX", font=fuente(42, False), fill=(110, 110, 115))

    y = 560
    d.line([(150, y), (1790, y)], fill=(180, 186, 198), width=6)

    hitos = [
        (250, "1837", "Nace Jorge Isaacs", ROJO),
        (620, "1867", "Se publica Maria", ROJO),
        (1080, "1865", "Nace J. A. Silva", (40, 90, 160)),
        (1500, "1894", "Nocturno", (40, 90, 160)),
    ]
    for x, año, texto, color in hitos:
        d.ellipse([x - 18, y - 18, x + 18, y + 18], fill=color)
        d.line([(x, y - 18), (x, y - 120)], fill=color, width=4)
        d.text((x, y - 190), año, font=fuente(64), fill=color, anchor="mm")
        d.text((x, y + 90), texto, font=fuente(34, False), fill=TINTA, anchor="mm")

    d.rounded_rectangle([150, 760, 940, 960], radius=28, fill=(255, 255, 255), outline=(200, 205, 215), width=3)
    d.text((190, 800), "ROMANTICISMO", font=fuente(34), fill=ROJO)
    d.text((190, 860), "El sentimiento y la naturaleza", font=fuente(32, False), fill=TINTA)
    d.text((190, 905), "por delante del argumento", font=fuente(32, False), fill=TINTA)

    d.rounded_rectangle([1000, 760, 1790, 960], radius=28, fill=(255, 255, 255), outline=(200, 205, 215), width=3)
    d.text((1040, 800), "MODERNISMO", font=fuente(34), fill=(40, 90, 160))
    d.text((1040, 860), "La musica de la palabra", font=fuente(32, False), fill=TINTA)
    d.text((1040, 905), "y la imagen sugerida", font=fuente(32, False), fill=TINTA)

    d.text((90, 1020), "AVACOM · material de prueba · obras en dominio publico",
           font=fuente(26, False), fill=(150, 150, 158))
    img.save(ruta, "PNG", optimize=True)
    return ruta


def lit_documento(ruta):
    """Guia de lectura. Material didactico propio: se explica el movimiento y se
    dan preguntas de lectura, sin reproducir la obra."""
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.units import cm
    from reportlab.lib import colors
    from reportlab.pdfgen import canvas

    c = canvas.Canvas(ruta, pagesize=A4)
    W, H = A4

    def pagina(titulo, n, lineas, salto=0.62):
        c.setFillColorRGB(*[v / 255 for v in ROJO])
        c.rect(0, H - 0.5 * cm, W, 0.5 * cm, fill=1, stroke=0)
        c.setFillColorRGB(*[v / 255 for v in TINTA])
        c.setFont("Helvetica-Bold", 19)
        c.drawString(2.5 * cm, H - 3 * cm, titulo)
        c.setFont("Helvetica", 11.5)
        y = H - 4.4 * cm
        for l in lineas:
            if l.startswith("##"):
                c.setFont("Helvetica-Bold", 13)
                y -= 0.3 * cm
                c.drawString(2.5 * cm, y, l[2:].strip())
                c.setFont("Helvetica", 11.5)
            else:
                c.drawString(2.5 * cm, y, l)
            y -= salto * cm
        c.setFont("Helvetica", 9)
        c.setFillColor(colors.grey)
        c.drawRightString(W - 2.5 * cm, 1.5 * cm, "Humanidades · Lengua Castellana · grado 9 · pagina %d" % n)
        c.showPage()

    pagina("El Romanticismo en la novela del siglo XIX", 1, [
        "El Romanticismo llega a America Latina cuando las republicas nuevas",
        "buscan contarse a si mismas. No es solo una moda literaria: es una forma",
        "de mirar el propio pais.",
        "",
        "## Tres rasgos que se reconocen al leer",
        "",
        "1. El sentimiento manda sobre el argumento. Lo que importa no es tanto",
        "   que pasa, sino como lo vive quien lo cuenta.",
        "",
        "2. El paisaje no decora: acompaña. Llueve cuando el personaje sufre, y el",
        "   valle se abre cuando respira. Se le llama paisaje como espejo del alma.",
        "",
        "3. Hay una perdida en el centro. Casi siempre alguien recuerda algo que",
        "   ya no puede volver.",
        "",
        "## Por que importa el tercero",
        "",
        "Si el alumno reconoce esa perdida, entiende por que el narrador cuenta",
        "en pasado y por que se detiene tanto en los detalles pequeños. No es",
        "lentitud: es alguien tratando de retener lo que se le fue.",
    ])

    pagina("Guia de lectura · Maria, de Jorge Isaacs (1867)", 2, [
        "Obra en dominio publico. La edicion que se use da igual; las preguntas",
        "funcionan con cualquiera.",
        "",
        "## Antes de leer",
        "",
        "Situar el año: 1867. Colombia lleva pocas decadas de vida republicana.",
        "El valle del Cauca que aparece en la novela es un lugar real.",
        "",
        "## Mientras se lee, marcar",
        "",
        "· Cada vez que el narrador describe el paisaje justo antes o despues de",
        "  una emocion fuerte. Anotar cual va primero.",
        "",
        "· Los objetos que se repiten. En esta novela un objeto pequeño puede",
        "  cargar mas peso que un capitulo entero.",
        "",
        "· El momento en que se sabe como va a terminar. Casi nunca es el final.",
        "",
        "## Despues de leer",
        "",
        "Escribir un parrafo respondiendo: que cambia si la historia se contara",
        "en presente y en orden. Se pierde algo? Que exactamente?",
    ])

    pagina("El Modernismo y la renovacion del lenguaje", 3, [
        "Hacia el final del siglo aparece una generacion que ya no quiere contar",
        "sentimientos: quiere construir un objeto sonoro. El poema empieza a",
        "escribirse casi como una partitura.",
        "",
        "## Que cambia",
        "",
        "· El ritmo se vuelve tema. La longitud de los versos deja de ser fija y",
        "  se estira o se corta segun lo que el poema quiere hacer sentir.",
        "",
        "· La repeticion no es descuido. Volver sobre una palabra es una decision,",
        "  y suele marcar el punto donde el poema insiste.",
        "",
        "· La imagen sugiere en vez de describir. Se nombra una sombra, no una",
        "  tristeza.",
        "",
        "## Jose Asuncion Silva (1865 - 1896)",
        "",
        "Su Nocturno, de 1894, es el caso que mejor se oye. Antes de analizarlo",
        "conviene escucharlo en voz alta: el alumno percibe el vaiven del ritmo",
        "mucho antes de poder explicarlo, y esa es la puerta de entrada.",
        "",
        "Obra en dominio publico.",
    ])

    pagina("Ejercicios", 4, [
        "1. Busca en la novela un pasaje donde el paisaje anuncie lo que va a",
        "   pasar. Copialo y explica que anuncia.",
        "",
        "2. El narrador cuenta en pasado. Reescribe cinco lineas en presente y di",
        "   que se pierde.",
        "",
        "3. Escucha el Nocturno sin leerlo. Anota, sin usar terminos tecnicos, que",
        "   te hace sentir el ritmo. Despues ponle nombre tecnico a eso mismo.",
        "",
        "4. Haz una tabla de dos columnas, Romanticismo y Modernismo, y coloca en",
        "   cada una: que papel tiene la naturaleza, que papel tiene el sonido, y",
        "   quien habla.",
        "",
        "5. Elige uno de los dos movimientos y defiende por que se lee mejor hoy.",
        "   Vale cualquiera de los dos: lo que se califica es el argumento.",
    ])
    c.save()
    return ruta


def lit_comparador(ruta):
    """Interactivo de clasificacion. Distinta area, distinta interaccion: aqui no
    se mueven parametros de una recta, se clasifican rasgos."""
    html = """<!doctype html>
<html lang="es"><head><meta charset="utf-8">
<title>Comparador de movimientos</title>
<style>
 body{font-family:'Segoe UI',system-ui,sans-serif;background:#e9edf3;color:#1d1d1f;
      margin:0;padding:28px;display:flex;flex-direction:column;align-items:center}
 h1{font-size:26px;margin:0 0 4px} p.g{color:#6e6e73;margin:0 0 22px;font-size:15px}
 .zona{display:flex;gap:22px;width:100%;max-width:900px}
 .col{flex:1;background:#e9edf3;border-radius:22px;padding:18px;min-height:250px;
      box-shadow:inset 5px 5px 12px #c2c9d6,inset -5px -5px 12px #fff}
 .col h2{font-size:17px;margin:0 0 12px;text-align:center}
 .rom h2{color:#c1191d} .mod h2{color:#285aa0}
 .rasgo{background:#e9edf3;border-radius:14px;padding:12px 16px;margin:8px 0;
        font-size:15px;cursor:pointer;box-shadow:5px 5px 11px #c2c9d6,-5px -5px 11px #fff;
        transition:.15s;border:2px solid transparent}
 .rasgo:hover{transform:translateY(-2px)}
 .rasgo.bien{border-color:#1f8a4c} .rasgo.mal{border-color:#e5262b}
 #banco{width:100%;max-width:900px;margin-bottom:20px}
 #marcador{margin-top:22px;font-size:17px;font-weight:600}
</style></head><body>
<h1>Comparador de movimientos</h1>
<p class="g">Toca cada rasgo para enviarlo al movimiento al que pertenece</p>
<div id="banco"></div>
<div class="zona">
  <div class="col rom"><h2>Romanticismo</h2><div id="rom"></div></div>
  <div class="col mod"><h2>Modernismo</h2><div id="mod"></div></div>
</div>
<div id="marcador"></div>
<script>
const rasgos=[
 {t:"El paisaje refleja el estado de animo",d:"rom"},
 {t:"El ritmo del verso es un tema en si mismo",d:"mod"},
 {t:"Predomina el sentimiento sobre el argumento",d:"rom"},
 {t:"La imagen sugiere en vez de describir",d:"mod"},
 {t:"Hay una perdida en el centro del relato",d:"rom"},
 {t:"La repeticion sonora es una decision, no un descuido",d:"mod"}
];
let aciertos=0, hechos=0;
const banco=document.getElementById('banco');
rasgos.sort(()=>Math.random()-.5).forEach((r,i)=>{
  const e=document.createElement('div');
  e.className='rasgo'; e.textContent=r.t; e.dataset.d=r.d; e.dataset.i=i;
  e.onclick=()=>elegir(e);
  banco.appendChild(e);
});
let pendiente=null;
function elegir(e){
  if(e.dataset.listo) return;
  if(!pendiente){ pendiente=e; e.style.opacity=.55; return; }
  pendiente.style.opacity=1; pendiente=null;
}
document.querySelectorAll('.col').forEach(c=>{
  c.onclick=ev=>{
    if(!pendiente) return;
    const destino=c.classList.contains('rom')?'rom':'mod';
    const ok=pendiente.dataset.d===destino;
    pendiente.style.opacity=1;
    pendiente.classList.add(ok?'bien':'mal');
    pendiente.dataset.listo=1;
    document.getElementById(destino).appendChild(pendiente);
    aciertos+=ok?1:0; hechos++;
    document.getElementById('marcador').textContent=
      hechos<rasgos.length ? aciertos+' de '+hechos+' bien'
                           : 'Terminado: '+aciertos+' de '+rasgos.length;
    pendiente=null;
    ev.stopPropagation();
  };
});
</script></body></html>"""
    with zipfile.ZipFile(ruta, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("index.html", html)
    return ruta


# ---------------------------------------------------------------- todo

def main(destino=None):
    d = destino or os.path.join(AQUI, "materiales")
    os.makedirs(d, exist_ok=True)
    hechos = []

    hechos.append(lamina(os.path.join(d, "lamina-granja.png"), "La granja",
                         "Animales que viven cerca de las personas",
                         [("V", "vaca"), ("G", "gallina"), ("C", "caballo")], PAPEL))
    hechos.append(lamina(os.path.join(d, "lamina-bosque.png"), "El bosque",
                         "Animales que viven entre los arboles",
                         [("O", "oso"), ("A", "ardilla"), ("B", "buho")], (226, 235, 228)))

    hechos.append(documento(os.path.join(d, "funcion-lineal.pdf")))
    hechos.append(interactivo(os.path.join(d, "explorador-rectas.zip")))

    v1 = video(os.path.join(d, "sonidos-animales.mp4"), "Sonidos de los animales",
               "Escucha y adivina cual es", 95, "0xe9edf3")
    v2 = video(os.path.join(d, "pendiente.mp4"), "Que significa la pendiente",
               "Cuanto sube por cada paso", 30, "0xe9edf3")
    hechos += [v for v in (v1, v2) if v]

    if not v1:
        print("  aviso: no hay ffmpeg, los videos se omiten")

    for i, notas in enumerate([[523, 659], [392, 523, 659], [330, 415], [440, 554]], 1):
        hechos.append(tono(os.path.join(d, f"voz-{i}.wav"), notas))

    # --- literatura, grado 9 ---------------------------------------------
    # Se añade despues y sin tocar nada de lo anterior. Es la prueba de que
    # meter un area nueva no obliga a cambiar el componente: solo hace falta
    # una especificacion con su propia taxonomia y sus propios medios.
    hechos.append(lit_linea_tiempo(os.path.join(d, "lit-linea-tiempo.png")))
    hechos.append(lit_documento(os.path.join(d, "lit-romanticismo.pdf")))
    hechos.append(lit_comparador(os.path.join(d, "lit-comparador.zip")))
    hechos.append(tono(os.path.join(d, "lit-nocturno.wav"),
                       [294, 330, 262, 294, 247, 262, 220], seg_por_nota=0.62))
    hechos.append(tono(os.path.join(d, "voz-5.wav"), [349, 440]))

    print(f"Medios generados en {d}")
    for h in hechos:
        print(f"  {os.path.basename(h):<28} {os.path.getsize(h) / 1024:>9.1f} KB")
    return d


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
