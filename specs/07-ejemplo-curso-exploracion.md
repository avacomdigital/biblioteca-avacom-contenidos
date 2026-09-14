# Ejemplo · Exploración del medio, transición

Cómo está registrado el curso de preescolar que ya viene cargado. Es el otro
extremo del producto: sin asignaturas, sin texto escrito para el niño y con
instrucciones habladas. El formato general está en [02-formato-de-curso.md](02-formato-de-curso.md).

## 1. De un vistazo

| Dato | Valor |
|---|---|
| Clave del paquete | `co-preescolar-transicion-exploracion` |
| Versión | 1 |
| País · nivel · grado | CO · preescolar · transicion |
| Materia | Exploración del medio (es la actividad rectora: en preescolar no hay asignaturas) |
| Idioma | es |
| Título | Exploración del medio · Transición |
| Descripción | Bases Curriculares para la Educación Inicial y Preescolar. Propósito 3, actividad rectora exploración del medio. |
| Emisor | AVACOM |
| Contenido | 7 nodos de índice · 5 materiales · 3 preguntas · 4 audios de voz |

Publicado, el paquete se llama `avacom-co-preescolar-transicion-exploracion-v1`.

## 2. Cómo está registrado

Como **especificación técnica** (`paquetes\specs\spec_co_preescolar.json`), con
los medios en `paquetes\materiales`. Además de ser anterior al estándar de
carpetas, este curso **no se puede escribir completo con carpetas todavía**: usa
instrucción hablada y una actividad de tocar imágenes, que las carpetas aún no
expresan (sección 8).

## 3. El índice curricular

Cadena de preescolar: **proposito → actividad_rectora → experiencia → aprendizaje**.
No se parece a la de secundaria, y así debe ser: cada marco tiene la suya.

    proposito  P3  Las niñas y los niños disfrutan aprender; exploran y se relacionan
    │              con el mundo para comprenderlo y construirlo
    ├─ actividad_rectora  AR-EM  Exploración del medio
    │   ├─ experiencia  (sin código)  Seres vivos de mi entorno
    │   └─ experiencia  (sin código)  Dónde viven los animales
    │       └─ aprendizaje  DBA-T-08  Agrupa animales según dónde viven
    └─ actividad_rectora  AR-JU  Juego
        └─ experiencia  (sin código)  Juegos de clasificar

Qué conviene notar:

- Las experiencias **no tienen código** y se dejaron vacías. En la especificación
  técnica eso no impide colgarles material; desde carpetas sí haría falta uno.
- Las tres experiencias y el aprendizaje llevan **objetivo**, por ejemplo
  «Establece relaciones entre los animales y el lugar donde viven» e «Identifica
  y agrupa objetos y seres a partir de un criterio».
- La rama «Juego» está vacía: el marco se registra completo aunque todavía no
  haya material.

## 4. Los materiales

| # | Tipo | Título | Cuelga de | Archivo | Datos extra |
|---|---|---|---|---|---|
| 1 | imagen | Lámina de la granja | Seres vivos de mi entorno | lamina-granja.png | Descripción alterna y lectura en voz. «Ilustración a página completa, legible a cuatro metros» |
| 2 | imagen | Lámina del bosque | Seres vivos de mi entorno | lamina-bosque.png | Descripción alterna y lectura en voz |
| 3 | video | Sonidos de los animales | Seres vivos de mi entorno | sonidos-animales.mp4 | 95 s. Sin subtítulos, con lectura en voz |
| 4 | actividad | ¿Dónde vive cada animal? | DBA-T-08 | — | 3 preguntas de tocar imagen, sin texto, con voz |
| 5 | leccion | Los animales y dónde viven | Dónde viven los animales | — | «Secuencia de una sesión de treinta minutos» |

Las láminas son de 1920×1080 con tres figuras grandes cada una: es lo que
distingue un niño de cinco años a cuatro metros. El video declara «sin
subtítulos» a propósito, porque el niño no lee, y lo compensa con la voz.

## 5. La actividad

Es de tipo **actividad**, no evaluación: cierra la sesión, no da nota. Las tres
preguntas son de «opción por imagen»: el niño toca una de tres imágenes.

| # | Enunciado (también dicho en voz) | Respuesta | Retroalimentación |
|---|---|---|---|
| 1 | ¿Dónde vive la vaca? | granja | La vaca vive en la granja |
| 2 | ¿Dónde vive el oso? | bosque | El oso vive en el bosque |
| 3 | ¿Dónde vive el pez? | rio | El pez vive en el agua |

Todas con peso 1 y dificultad baja. No hay rúbrica porque no hay abiertas.

**Lo que hoy falta.** El paquete guarda la respuesta («granja») pero **no las
tres imágenes entre las que se elige**. Mientras el formato no las defina, el
visor pide la respuesta escrita, que funciona pero no es la interacción pensada
para preescolar.

## 6. La voz

Cuatro audios WAV grabados, uno por instrucción:

| Acompaña a | Texto que dice | Archivo |
|---|---|---|
| La actividad entera | Toca el lugar donde vive cada animal | voz-1.wav |
| Pregunta 1 | ¿Dónde vive la vaca? | voz-2.wav |
| Pregunta 2 | ¿Dónde vive el oso? | voz-3.wav |
| Pregunta 3 | ¿Dónde vive el pez? | voz-4.wav |

Cada voz lleva cuatro datos: a qué material o pregunta acompaña, idioma, el
texto dicho y el archivo. La duración se lee del propio audio. En el aula
aparece un botón para escucharla junto a cada pregunta.

## 7. La lección

| Paso | Material | Nota para el docente |
|---|---|---|
| 1 | Lámina de la granja | Se proyecta y se conversa |
| 2 | Lámina del bosque | Se compara con la anterior |
| 3 | Sonidos de los animales | Se escucha y se adivina |
| 4 | ¿Dónde vive cada animal? | Cierre de la sesión |

La lección cuelga de la experiencia «Dónde viven los animales», pero reúne
materiales de la otra experiencia (las láminas y el video) y del aprendizaje.
Una lección puede tomar material de cualquier punto del índice.

## 8. Qué se podría hacer hoy con carpetas, y qué no

    contenido\CO\preescolar\transicion\exploracion\
        curso.txt
        indice.txt
        DBA-T-08 Dónde viven los animales\
            01-Lámina de la granja.png
            02-Lámina del bosque.png
            03-Sonidos de los animales.mp4
            04-evaluacion.txt
            leccion.txt

`curso.txt`:

    titulo: Exploración del medio · Transición
    materia: Exploración del medio
    idioma: es
    version: 1
    descripcion: Bases Curriculares para la Educación Inicial y Preescolar. Propósito 3, actividad rectora exploración del medio.

`indice.txt` (hay que dar un código a cada experiencia para poder colgarle carpetas):

    proposito | P3 | Las niñas y los niños disfrutan aprender; exploran y se relacionan con el mundo para comprenderlo y construirlo
      actividad_rectora | AR-EM | Exploración del medio
        experiencia | EXP-SERES | Seres vivos de mi entorno
        experiencia | EXP-HABITAT | Dónde viven los animales
          aprendizaje | DBA-T-08 | Agrupa animales según dónde viven
      actividad_rectora | AR-JU | Juego
        experiencia | EXP-CLASIF | Juegos de clasificar

`04-evaluacion.txt`:

    titulo: ¿Dónde vive cada animal?

    P: ¿Dónde vive la vaca?
    R: granja
    dificultad: baja
    retro: La vaca vive en la granja

    P: ¿Dónde vive el oso?
    R: bosque
    dificultad: baja
    retro: El oso vive en el bosque

    P: ¿Dónde vive el pez?
    R: rio
    dificultad: baja
    retro: El pez vive en el agua

Con eso se registra casi todo: ficha, índice, láminas, video, preguntas y
secuencia. Lo que se pierde:

- La **voz**: desde carpetas no hay forma de asociar un audio a una pregunta.
- El tipo **actividad** y la **opción por imagen**: el archivo de texto sale
  como evaluación con respuesta escrita.
- Los datos de accesibilidad, la duración del video, los objetivos de los nodos
  y las notas por paso de la lección.

Para un curso de preescolar nuevo, hoy conviene preparar las carpetas con todo
lo que sí se expresa y pedir al equipo técnico que añada la voz y la actividad
en la especificación. La alternativa es ampliar el estándar de carpetas, que es
justo lo que este ejemplo deja pendiente.

## 9. Detalles que conviene notar

- El grado `transicion` va como palabra, no como número. Los otros dos de
  preescolar son `prejardin` y `jardin`.
- La «materia» es la actividad rectora. El LMS no debe esperar una asignatura
  en preescolar.
- Los códigos oficiales existen solo en algunos niveles del marco (P3, AR-EM,
  DBA-T-08). Es normal, y el índice lo admite.
