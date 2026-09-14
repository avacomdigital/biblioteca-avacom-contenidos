# Formato de un curso en AVACOM Biblioteca

Guía para el equipo que crea cursos. Explica qué datos lleva un curso, cómo se
escriben y qué comprueba la herramienta antes de aceptarlo. No hace falta saber
programar: se trabaja con carpetas, archivos de texto y los medios.

Los dos cursos que ya vienen cargados están explicados aparte:
[Matemáticas, grado 8](06-ejemplo-curso-matematicas.md) y
[Exploración del medio, transición](07-ejemplo-curso-exploracion.md).

---

## 1. Qué es un curso

Para la Biblioteca, un curso es un **paquete**: una unidad que se instala entera
en el equipo del aula. Cada paquete lleva cuatro partes:

| Parte | Qué contiene |
|---|---|
| **Ficha** | Título, país, nivel, grado, materia, idioma, versión y descripción |
| **Índice curricular** | El árbol del marco oficial (áreas, estándares, temas…) con sus códigos |
| **Materiales** | Láminas, PDF, videos, audios, juegos, evaluaciones y bancos de preguntas, cada uno colgado de un punto del índice |
| **Lecciones** | El orden en que se dan los materiales de cada tema |

Todo se identifica por referencias internas que se deducen de los títulos y los
códigos. Por eso los títulos no se cambian a la ligera (sección 11).

## 2. Dos maneras de registrar un curso

| | Con carpetas (la recomendada) | Con especificación técnica |
|---|---|---|
| Quién la usa | Equipo de contenido | Equipo técnico |
| Cómo se trabaja | Carpetas, dos archivos de texto y los medios | Un archivo JSON escrito a mano |
| Ejemplo en el proyecto | Literatura, grado 9 (`contenido\CO\secundaria\09\humanidades`) | Matemáticas grado 8 y Exploración del medio (`paquetes\specs`), los dos ya publicados |
| Cuándo | Siempre que se pueda | Solo si hace falta algo que las carpetas aún no expresan (sección 14) |

Las dos vías acaban en el mismo sitio: la herramienta convierte las carpetas en
una especificación y de ahí sale el paquete cifrado. Esta guía describe la vía
de carpetas y señala, cuando aparece, lo que solo existe en la técnica.

## 3. Dónde va el curso: la ruta

    contenido\<PAÍS>\<nivel>\<grado>\<materia>\

| Tramo | Valores aceptados | Ejemplo |
|---|---|---|
| País | Dos letras en mayúsculas | `CO`, `MX` |
| Nivel | `preescolar`, `primaria`, `secundaria`, `media` | `secundaria` |
| Grado | Preescolar: `prejardin`, `jardin`, `transicion`. Los demás: dos cifras | `08`, `transicion` |
| Materia | Una palabra corta, sin tildes ni espacios | `matematicas`, `exploracion` |

País, nivel y grado **no se escriben en ningún archivo**: salen de la ruta, para
que no puedan contradecirse. La materia de la carpeta es solo un nombre corto; el
nombre completo va en la ficha.

## 4. La ficha: `curso.txt`

Un archivo de texto con una línea por dato. Las líneas que empiezan por `#` no se leen.

    titulo: Matemáticas · Grado 8
    materia: Matemáticas
    idioma: es
    version: 1
    descripcion: Área obligatoria de la Ley 115 artículo 23. Estándares Básicos, pensamiento variacional.

| Campo | Obligatorio | Qué es |
|---|---|---|
| `titulo` | Sí | Lo que ve el profesor en el catálogo |
| `materia` | Sí | Nombre completo del área, como lo llama el ministerio. En preescolar, la actividad rectora |
| `idioma` | No (es) | Código de dos letras |
| `version` | No (1) | Sube a 2, 3… cuando se republica con cambios. El aula puede tener varias versiones |
| `descripcion` | Recomendado | Una o dos frases: qué cubre y a qué marco curricular responde |
| `clave` | No | Identificador del paquete. Si no se escribe se forma solo (`co-secundaria-8-matematicas`). **Nunca se cambia** después de publicar |
| `emisor` | No (AVACOM) | Quién firma el paquete |

## 5. El índice curricular: `indice.txt`

El marco curricular del área, escrito con sangría. Cada línea es un nodo con
tres datos separados por barras: `tipo | código | nombre`.

    area | L115-A8 | Matemáticas
      pensamiento | EBC-VAR | Pensamiento variacional y sistemas algebraicos y analíticos
        estandar | EBC-8-VAR-01 | Identifico relaciones entre propiedades de las gráficas y de las ecuaciones
          tema | DBA-8-05 | Función lineal y su representación gráfica
          tema | DBA-8-06 | Pendiente y razón de cambio

| Dato del nodo | Cómo se da | Notas |
|---|---|---|
| Tipo | Primera columna | El nombre **real** del nivel en el marco oficial. No se inventa ni se unifica entre áreas |
| Código | Segunda columna | El oficial (`DBA-8-05`). Puede quedar vacío, pero un nodo sin código **no puede recibir material** desde carpetas |
| Nombre | Tercera columna | Tal como aparece en el documento del ministerio |
| De quién cuelga | La sangría | Más sangría que su padre; lo habitual son dos espacios por nivel |
| Orden | La posición entre hermanos | Se numera solo |
| Objetivo | — | Solo en la especificación técnica |

Las cadenas de tipos cambian por área y por nivel, y está bien que no coincidan:

| Área | Cadena real |
|---|---|
| Preescolar | proposito → actividad_rectora → experiencia → aprendizaje |
| Matemáticas | area → pensamiento → estandar → tema |
| Lenguaje | area → factor → estandar → tema |

Puede haber nodos sin material: el índice puede traer el marco completo y
llenarse con el tiempo. Nada comprueba que un código exista de verdad en el
marco; esa responsabilidad es de quien lo escribe.

## 6. Los materiales

Cada tema es una carpeta cuyo nombre empieza por un código del índice. Dentro
van los materiales, numerados en el orden en que se dan:

    DBA-8-05 Función lineal\
        01-La función lineal.pdf
        02-Qué significa la pendiente.mp4
        03-Explorador de rectas\           (carpeta con index.html: juego o animación)
        04-Repaso de la función lineal\    (carpeta con guion.txt: lección completa)
        90-banco.txt
        99-evaluacion.txt
        leccion.txt                        (opcional)

Regla de nombres: `NN-Título del material.extensión`

| Parte | Regla |
|---|---|
| Número | Manda el orden. Dejar huecos (10, 20, 30) para poder intercalar. Sin número, va al final |
| Título | Lo que ve el profesor. Con tildes y espacios. **Único en todo el curso** |
| Extensión | Decide el tipo. No se elige nada más |

| Tipo | Cómo se registra | Datos que lleva | ¿Se ve en el aula? |
|---|---|---|---|
| imagen | `.png .jpg .jpeg .webp .svg` | título | Sí |
| documento | `.pdf` | título, páginas* | Sí |
| video | `.mp4 .webm` | título, duración en segundos*, accesibilidad* | Sí |
| audio | `.mp3 .wav .m4a` | título, duración* | Sí |
| interactivo | Carpeta con `index.html` | título | Sí |
| lección completa | Carpeta con `guion.txt` y sus medios | título, objetivo, duración | Sí (se guarda como interactivo) |
| evaluacion | Archivo `.txt` con «evaluacion» en el nombre | preguntas, rúbrica | Sí |
| banco | Archivo `.txt` con «banco» en el nombre | preguntas, reglas de extracción | No: alimenta los exámenes |
| actividad | Solo en especificación técnica | preguntas, instrucción hablada | Sí |
| leccion (secuencia) | Se genera sola por cada carpeta de tema | orden de los materiales | Sí |
| scorm | Declarado; todavía sin visor | — | No |

`*` Solo se declaran en la especificación técnica y son informativos: nadie los
mide automáticamente.

Además del título y el tipo, cada material queda registrado con su referencia
(sale del título), el nodo del que cuelga, la versión del paquete y su estado
(vigente o retirado). La descripción por material solo existe en la técnica.

Lo que no esté en la tabla se ignora y el revisor lo dice por su nombre. Un
`.docx` o un `.pptx` no se puede mostrar: hay que exportarlo a PDF.

## 7. La lección: el orden del tema

Hay dos cosas con ese nombre y conviene distinguirlas.

**La secuencia del tema.** Se genera sola por cada carpeta de tema: todos los
materiales numerados, en orden, salvo el banco. Si existe `leccion.txt`, le da
título y descripción; si no, toma el nombre de la carpeta.

    titulo: Función lineal y razón de cambio
    descripcion: Una sesión de cincuenta minutos.

En la especificación técnica cada paso puede llevar una nota para el docente
(«Lectura previa», «Cierre de la sesión»). Desde carpetas todavía no.

**La lección completa.** Una carpeta con `guion.txt` y sus medios al lado. Sale
en pantalla como una sola página con barra de progreso, índice, medios integrados
y preguntas de comprobación. El guion admite ocho cosas y ninguna más:

| Qué | Cómo se escribe |
|---|---|
| Cabecera | `titulo:`, `objetivo:`, `duracion:` (minutos) |
| Sección | `## Nombre de la sección` |
| Párrafo | Texto normal; una línea en blanco separa párrafos |
| Idea destacada | `> texto` |
| Lista | `- punto` |
| Medio | `IMAGEN:`, `VIDEO:`, `AUDIO:` o `PDF:` seguido de `archivo | pie de foto` |
| Quiz | `QUIZ`, luego `P:`, opciones con `- ` y asterisco en la correcta, `retro:` |

El quiz de la lección **no da nota**: sirve para que el alumno sepa si conviene
repasar. La nota sale de la evaluación o del examen que se monta con el banco.

## 8. Evaluaciones, actividades y bancos

Se escriben como las escribiría un profesor:

    titulo: Evaluación · Función lineal

    P: ¿Cuál es la pendiente de la recta y = 3x - 5?
    R: 3
    peso: 1
    dificultad: baja
    retro: La pendiente es el número que multiplica a x.

    P: Explica qué representa la pendiente en una situación de la vida diaria
    abierta
    peso: 2
    dificultad: alta

    RUBRICA
    Interpretación | Relaciona la pendiente con una razón de cambio real | 2
    Argumentación | Justifica con lenguaje matemático apropiado | 1

| Dato | Cómo se escribe | Obligatorio | Notas |
|---|---|---|---|
| Enunciado | `P:` | Sí | Empieza cada pregunta |
| Respuesta | `R:` | Sí, salvo abiertas | Se compara tal cual, sin distinguir mayúsculas ni espacios sobrantes |
| Abierta | la palabra `abierta` | — | La califica el profesor con la rúbrica |
| Peso | `peso:` | No (1) | Número entero. Si se escribe con decimales se toma como 1; solo la técnica admite decimales |
| Dificultad | `dificultad:` | No (media) | `baja`, `media` o `alta` |
| Retroalimentación | `retro:` | No | Lo que se explica al alumno después de responder |
| Tipo | — | — | En texto se deduce: con respuesta o abierta. En la técnica se declara: opción múltiple, numérica, verdadero/falso, emparejamiento, abierta, opción por imagen |

**Importante:** el paquete guarda la respuesta correcta pero **no las opciones**.
Hoy el visor muestra el enunciado, el alumno escribe la respuesta y el sistema
la compara. Por eso las respuestas deben ser cortas y sin ambigüedad: un número,
una palabra, «falso». Las opciones para elegir solo existen en el quiz de la
lección completa.

**Rúbrica.** Una línea por criterio: `criterio | qué se espera | peso`. Es
obligatoria si hay al menos una pregunta abierta; sin ella, dos profesores dan
notas distintas al mismo texto.

**Banco de preguntas.** Mismo formato, con una cabecera que dice cuántas
preguntas saca el examen y con qué mezcla:

    titulo: Banco · Función lineal
    extraer: 4
    por_dificultad: baja 1, media 2, alta 1

El banco debe tener al menos el doble de preguntas de las que extrae, y de cada
dificultad tantas como pide la mezcla. No entra en la secuencia del tema.

| | Dónde vive | ¿Se da en clase? | ¿Da nota? |
|---|---|---|---|
| Quiz | Dentro de `guion.txt` | Sí | No |
| Evaluación | Archivo con «evaluacion» | Sí, entera y siempre igual | Sí (la registra el LMS) |
| Banco | Archivo con «banco» | No | Alimenta el examen: N preguntas al azar por alumno |

## 9. Instrucción hablada (voz)

Para preescolar, donde el niño no lee. Un audio grabado (WAV) acompaña a un
material entero o a cada pregunta, y en el aula aparece un botón para
escucharlo. Cada voz lleva: a qué material o pregunta acompaña, idioma, el texto
que dice y el archivo. La duración se lee del propio audio.

Hoy solo se puede registrar en la especificación técnica. La voz sintética sin
conexión no existe: los audios se graban o se traen hechos.

## 10. Requisitos de los medios

| Medio | Requisito |
|---|---|
| Imágenes | 1920×1080 (16:9). Figuras grandes, poco texto y fondo gris papel, no blanco: en 86 pulgadas el blanco deslumbra. Una idea por lámina |
| Video | `.mp4` o `.webm`, máximo 300 MB, a 1280×720 o 1920×1080 |
| Audio | `.mp3`, `.wav` o `.m4a`. Voz grabada |
| PDF | Exportado desde el original. Poco texto por página: se lee a cuatro metros |
| Interactivos | Entrada `index.html`. Sin internet, sin almacenamiento del navegador. Texto de 20 px o más, botones de 64 px o más, funciona a 1920×1080 y encogido. Ningún archivo interno pasa de 40 MB |
| Derechos | Resueltos antes. El sistema cifra y firma, no comprueba licencias de uso |

## 11. Reglas que se pagan caro si se rompen

1. **Los títulos son referencias.** Cambiar un título convierte el material en
   uno nuevo, y el examen que apuntaba al viejo deja de poder explicarse.
   Corregir una errata: sí, avisando al equipo técnico.
2. **Dos materiales no pueden llamarse igual**, ni siquiera en temas distintos.
   Mayúsculas, tildes y signos no cuentan: «Lámina» y «lamina» chocan. «Lámina»
   no es un título; «Lámina de la célula animal» sí.
3. **La clave del paquete no cambia.** Lo que sube es la versión.
4. **Nada de internet en los interactivos.** El aula no tiene conexión; una
   dirección externa deja la pantalla en blanco delante de treinta alumnos.
5. **Los códigos del índice son los oficiales** y los tipos, los del marco.
6. **Pregunta abierta, rúbrica obligatoria.**
7. **Se escribe para leerse a cuatro metros.** Una idea por lámina.

## 12. Pasos para registrar un curso nuevo

1. Copiar `contenido\PLANTILLA` a la ruta que toque (sección 3).
2. Rellenar `curso.txt` e `indice.txt`. Los dos traen las instrucciones dentro.
3. Crear una carpeta por tema, con su código delante, y meter el material numerado.
4. Revisar tantas veces como haga falta. No toca el material; solo regenera la
   página de las lecciones completas y avisa de lo que falta:

       py -3 paquetes\avacom_recolector.py revisar "contenido\CO\secundaria\08\matematicas"

5. Cuando salga limpio, armar. Genera la especificación y copia los medios:

       py -3 paquetes\avacom_recolector.py armar "contenido\CO\secundaria\08\matematicas"

6. Entregar al equipo técnico, que construye, verifica, cifra, firma y emite la
   licencia del aula. El paquete resultante se llama
   `avacom-<clave>-v<versión>`. La verificación es el **último momento** en que
   se puede leer el contenido en claro: ahí se hace la revisión pedagógica final.

Los dos comandos se ejecutan desde la carpeta raíz del proyecto, la que contiene
`contenido` y `paquetes`.

## 13. Lista de comprobación antes de entregar

- [ ] La ruta tiene la forma país, nivel, grado, materia.
- [ ] `curso.txt` con título, materia y descripción.
- [ ] `indice.txt` con los tipos reales del marco y los códigos oficiales.
- [ ] Cada carpeta de tema empieza por un código que está en `indice.txt`.
- [ ] Materiales numerados, títulos únicos y extensiones de la tabla.
- [ ] Cada pregunta tiene `R:` o `abierta`; hay `RUBRICA` si hay abiertas.
- [ ] Cada banco dice `extraer:` y tiene al menos el doble de preguntas.
- [ ] Interactivos con `index.html`, sin direcciones externas ni almacenamiento del navegador.
- [ ] Videos de menos de 300 MB. Derechos resueltos.
- [ ] `revisar` termina sin errores ni avisos.

## 14. Límites actuales y hallazgos

- **Las carpetas no expresan todo.** Voz, actividades por imagen, descripción
  por material, duración y páginas, objetivo por nodo y notas por paso de la
  lección solo se registran en la especificación técnica. Es lo que impide hoy
  hacer el curso de preescolar solo con carpetas.
- **Las evaluaciones no guardan opciones**, solo la respuesta. El visor pide
  respuesta escrita. Las actividades de preescolar necesitan que el formato
  defina las tres imágenes entre las que se elige.
- **Los nombres de tipo de pregunta difieren entre las dos vías** (por ejemplo
  «opción única» y «abierta» desde carpetas frente a «opción múltiple» y
  «respuesta abierta» en la técnica). Hoy el aula trata todas igual, pero
  conviene unificar el vocabulario antes de que el LMS dependa de él.
- **La duración de un video se declara, no se mide.** Nada la comprueba.
- **SCORM** no tiene visor; **banco** no lo tiene a propósito.
- **No hay editor visual** ni estados de borrador y aprobado. La revisión ocurre
  con `revisar` y con la verificación en claro, y depende de que alguien la haga.
- **Nadie valida que un código curricular exista.** Un código inventado se
  empaqueta igual de bien que uno correcto.
