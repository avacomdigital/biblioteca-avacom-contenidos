# Resumen del formato de un curso · AVACOM Biblioteca

Guía corta para el equipo que crea cursos. Todo lo que dice aquí está
comprobado contra la herramienta que revisa y arma los cursos y contra la
aplicación del aula (AVACOM Contenido). La guía completa está en
[02-formato-de-curso.md](02-formato-de-curso.md) y el estándar en [ESTANDAR-CONTENIDO.txt](../ESTANDAR-CONTENIDO.txt).

---

## 1. Resumen

Un curso es una **carpeta**. No hay que escribir código ni JSON: la
estructura de carpetas y dos archivos de texto son el formato. Una herramienta
recorre la carpeta, deduce el tipo de cada material por su extensión, cuelga
cada tema del índice curricular y produce un **paquete** cifrado y firmado.
Ese paquete se instala entero en el equipo del aula, y desde ahí lo abren la
Biblioteca y el LMS.

Para el aula, **un curso es un paquete instalado**: su referencia estable es
la clave del paquete, que se forma sola con país, nivel, grado y materia, y
que nunca cambia al republicar. Lo que sube es la versión.

| Pieza | Dónde va | Qué contiene |
|---|---|---|
| Ruta | `contenido\PAÍS\nivel\grado\materia\` | País, nivel y grado. **No se escriben en ningún archivo** |
| Ficha | `curso.txt` | Título, materia, idioma, versión y descripción |
| Índice curricular | `indice.txt` | El marco oficial con sangría: `tipo \| código \| nombre` |
| Temas | Una carpeta por tema, con su código delante | Los materiales numerados en el orden en que se dan |
| Materiales | Archivos dentro del tema | Láminas, PDF, videos, audios, lecciones, juegos, evaluaciones y bancos |
| Lección del tema | Se genera sola por cada carpeta de tema | La secuencia de los materiales. `leccion.txt` le da título y descripción |

Tipos que la Biblioteca sabe mostrar hoy: imagen, documento (PDF), video,
audio, interactivo (incluye la lección completa), evaluación y lección
(secuencia). El banco de preguntas se registra pero no se muestra: alimenta el
examen. SCORM está declarado pero no tiene visor.

Los pasos son cuatro: copiar la plantilla de `contenido\PLANTILLA`, rellenar
los dos archivos, crear las carpetas de tema con su material, y revisar hasta
que salga limpio:

```bash
py -3 paquetes\avacom_recolector.py revisar "contenido\CO\secundaria\09\humanidades"
```

Cuando no hay errores, se arma con `armar` en vez de `revisar` y se entrega
al equipo técnico, que construye, verifica, cifra, firma y emite la licencia.

---

## 2. Estructura de ejemplo de un curso completo

```
contenido\
└── CO\                                        país: dos letras en mayúsculas
    └── secundaria\                            nivel: preescolar, primaria, secundaria o media
        └── 09\                                grado: dos cifras (en preescolar: prejardin, jardin, transicion)
            └── humanidades\                   materia: una palabra corta, sin tildes
                ├── curso.txt                  la ficha
                ├── indice.txt                 el índice curricular
                │
                ├── DBA-9-03 Romanticismo\     tema: código del índice + espacio + nombre
                │   ├── leccion.txt            opcional: título y descripción de la secuencia
                │   ├── 01-Guía de lectura del Romanticismo.pdf
                │   ├── 02-Línea de tiempo del Romanticismo.png
                │   ├── 03-Del Romanticismo al Modernismo\     lección completa
                │   │   ├── guion.txt                          el texto de la lección
                │   │   ├── linea-tiempo.png                   medios citados en el guion
                │   │   ├── valle.mp4
                │   │   ├── nocturno.wav
                │   │   └── guia-lectura.pdf
                │   └── 99-evaluacion.txt
                │
                ├── DBA-9-04 Modernismo\
                │   ├── 01-Lectura en voz alta del Nocturno.wav
                │   ├── 02-El Nocturno explicado.mp4
                │   └── 99-evaluacion.txt
                │
                └── EBC-9-LIT-01 Sintesis\     un tema puede colgar de un estándar
                    ├── 01-Comparador de movimientos\          juego o animación
                    │   ├── index.html                         punto de entrada, siempre este nombre
                    │   ├── estilos.css
                    │   └── juego.js
                    ├── 02-evaluacion.txt
                    └── 90-banco.txt                           alimenta el examen; no se da en clase
```

Los archivos de texto, en pocas palabras:

| Archivo | Cómo se escribe |
|---|---|
| `curso.txt` | Una línea por dato: `titulo:`, `materia:`, `idioma:`, `version:`, `descripcion:`. Las líneas con `#` no se leen |
| `indice.txt` | Una línea por nodo: `tipo \| código \| nombre`. La sangría dice de quién cuelga. El tipo es el real del marco (area, factor, pensamiento, estandar, tema…) |
| `guion.txt` | Cabecera `titulo:`, `objetivo:`, `duracion:`. Secciones con `##`, párrafos, `>` para destacar, `-` para listas, `IMAGEN:`/`VIDEO:`/`AUDIO:`/`PDF:` con `archivo \| pie`, y `QUIZ` con `P:`, opciones `- ` y asterisco en la correcta |
| `evaluacion.txt` | `titulo:`, luego por pregunta `P:`, `R:` o la palabra `abierta`, `peso:`, `dificultad:`, `retro:`. Al final `RUBRICA` con `criterio \| qué se espera \| peso` |
| `banco.txt` | Igual que la evaluación, más `extraer:` y `por_dificultad: baja 1, media 2, alta 1` en la cabecera |
| `leccion.txt` | Opcional: `titulo:` y `descripcion:` de la secuencia del tema |

---

## 3. Lo más importante

```
   carpetas y texto  ──►  revisar  ──►  armar  ──►  construir · verificar · cifrar · firmar  ──►  aula
   (equipo de contenido)   ▲                         (equipo técnico)
                           └── se repite hasta que salga limpio
```

| # | Regla | Por qué importa |
|---|---|---|
| 1 | **La extensión decide el tipo.** `.png .jpg .jpeg .webp .svg` imagen · `.pdf` documento · `.mp4 .webm` video · `.mp3 .wav .m4a` audio · carpeta con `index.html` interactivo · carpeta con `guion.txt` lección completa | No hay nada que elegir ni declarar. Lo que no está en la lista se ignora |
| 2 | **El nombre manda:** `NN-Título.extensión` | El número da el orden dentro de la lección del tema. Sin número, el material va al final |
| 3 | **El título es la referencia.** Se convierte en un identificador y **debe ser único en todo el curso** | Dos títulos iguales dan la misma referencia y la herramienta no deja armar. Cambiar un título crea un material nuevo |
| 4 | **La carpeta del tema empieza por un código de `indice.txt`** seguido de un espacio | Así el material se cuelga del nodo correcto. Un nodo sin código no puede recibir material |
| 5 | **País, nivel y grado salen de la ruta** | Nunca se escriben en `curso.txt`. Un dato escrito dos veces acaba contradiciéndose |
| 6 | **La clave del paquete no cambia; la versión sube** | Es lo que el LMS guarda en el expediente de cada alumno |
| 7 | **Las evaluaciones guardan la respuesta, no las opciones.** El alumno escribe y el sistema compara sin distinguir mayúsculas ni espacios sobrantes | Respuestas cortas y sin ambigüedad: un número, una palabra, «falso». Las opciones para elegir solo existen en el quiz del guion |
| 8 | **Pregunta abierta, rúbrica obligatoria** | Sin ella dos profesores dan notas distintas al mismo texto |
| 9 | **Nada de internet en los interactivos** | El aula no tiene conexión. Una dirección `https://` deja la pantalla en blanco |
| 10 | **Se escribe para leerse a cuatro metros** | Textos de 20 px o más, botones de 64 px, una idea por lámina, fondo gris papel |

Cómo lo ve el LMS: pide la lista de cursos y, por cada uno, sus secciones. Las
secciones son los nodos del propio índice curricular, con su código, y dentro
van los materiales visibles. El orden pedagógico va dentro de la lección del
tema; en la lista de una sección el LMS ordena los materiales por título. Una
sección sin material no aparece, y un curso sin nada visible tampoco.

---

## 4. Errores que hay que evitar

Los marcados con **Bloquea** impiden armar. El resto son avisos: se puede
armar, pero conviene arreglarlos antes.

| Error | Efecto | Cómo evitarlo |
|---|---|---|
| Carpeta del curso fuera de `contenido\PAÍS\nivel\grado\materia` | **Bloquea** | País de dos letras, nivel entre los cuatro válidos, grado de dos cifras |
| Falta `curso.txt` | **Bloquea** | Copiar la plantilla y rellenarla |
| Dos materiales con el mismo título, aunque estén en temas distintos | **Bloquea** | Títulos completos y distintos. «Lámina» no sirve; «Lámina de la célula animal» sí. Mayúsculas y tildes no cuentan para distinguir |
| Dos títulos muy largos que coinciden en sus primeros 48 caracteres | **Bloquea** | La referencia se recorta a 48 caracteres. Diferenciar los títulos al principio, no al final |
| Curso sin ninguna carpeta de tema o sin ningún material | **Bloquea** | Al menos un tema con un material reconocido |
| Falta `indice.txt` | Aviso. Todo cuelga de la raíz | Escribir el índice antes que los temas |
| Carpeta de tema cuyo código no está en `indice.txt` | Aviso. El material cuelga del primer nodo | Copiar el código exacto del índice. El nombre de la carpeta lleva el código, un espacio y luego el nombre |
| Carpeta suelta en la raíz del curso («borradores», «fuentes») | Se trata como un tema y cuelga de la raíz | Guardar el material de trabajo fuera de la carpeta del curso |
| Subcarpeta dentro de un tema sin `index.html` ni `guion.txt` | Se trata como interactivo roto | Los medios sueltos van directamente en la carpeta del tema, no en subcarpetas |
| Material `.docx`, `.pptx`, `.gif` u otra extensión no reconocida | Se ignora | Exportar a PDF o a un formato de la lista |
| Archivo `.txt` sin «evaluacion» ni «banco» en el nombre | Se ignora | Las notas de trabajo no van dentro del tema |
| Escribir país, nivel o grado dentro de `curso.txt` | Se ignoran; manda la ruta | No escribirlos. Solo título, materia, idioma, versión y descripción |
| Inventar o unificar los tipos del índice para que «quede parejo» | El contenido deja de corresponder al currículo | Usar el nombre real de cada nivel del marco: si el ministerio dice «factor», se escribe factor |
| Poner un código curricular inventado | Se empaqueta igual; nadie lo comprueba | Copiarlo del documento oficial. El error aparece meses después, en una auditoría |
| Pregunta `P:` sin `R:` ni la palabra `abierta` | Aviso. Se guarda sin respuesta y no se puede corregir | Cada pregunta lleva `R:` o `abierta` |
| Pregunta abierta sin `RUBRICA` | Aviso | Añadir la rúbrica: `criterio \| qué se espera \| peso` |
| Respuesta larga o con varias formas válidas («1867» frente a «en 1867») | El alumno falla aunque sepa | Respuestas de una palabra o un número. Lo que necesite opciones va al quiz del guion |
| `peso:` con decimales | Se toma como 1 | Pesos enteros |
| Banco sin `extraer:` | Aviso | Escribir cuántas preguntas saca el examen |
| Banco con menos del doble de preguntas de las que extrae, o sin suficientes de una dificultad | Aviso. Dos alumnos verían casi lo mismo | Al menos el doble de preguntas y de cada dificultad tantas como pide la mezcla |
| Interactivo sin `index.html` | Aviso. No se puede abrir | El punto de entrada se llama siempre así |
| Interactivo con `src` o `href` a `https://` | Aviso. Pantalla en blanco en el aula | Descargar la biblioteca y meterla en la misma carpeta |
| Interactivo que usa `localStorage` o `sessionStorage` | Aviso. Lo guardado se pierde en cada arranque | Guardar el estado en variables normales |
| Archivo de más de 40 MB dentro de un interactivo | Aviso | Sacarlo como video suelto |
| Video de más de 300 MB en una lección completa | Aviso | Comprimirlo o partirlo |
| Guion sin `titulo:` o sin ninguna sección `##` | Aviso. Lección vacía o sin nombre | Cabecera completa y al menos una sección |
| Medio citado en el guion que no existe o con extensión que no corresponde (`VIDEO:` con un `.png`) | Aviso. El medio no se muestra | El archivo va al lado del guion, con el nombre exacto y la etiqueta correcta |
| `QUIZ` sin opciones o sin asterisco en la correcta | Aviso. El quiz no corrige | Opciones con `- ` y un `*` al final de la correcta |
| Cambiar el título de un material ya publicado | Para el sistema es material nuevo; el examen antiguo deja de explicarse | Corregir erratas avisando al equipo técnico. Un cambio de título es material nuevo |
| Cambiar la `clave` del paquete al republicar | El LMS pierde la relación con los expedientes | No escribir `clave:` en `curso.txt`, o no tocarla nunca. Subir `version:` |
| Usar material con derechos sin resolver | El sistema cifra y firma, no comprueba licencias | Resolverlo antes. Ante la duda, material propio |

Antes de entregar, `revisar` debe terminar con «Todo correcto. Listo para
armar». Si dice «Se puede armar» con avisos, leerlos uno a uno: un aviso
ignorado hoy es un material que no se ve en el aula mañana.
