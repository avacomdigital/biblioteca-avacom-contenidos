# Ejemplo · Matemáticas, grado 8

Cómo está registrado el curso de matemáticas que ya viene cargado. Sirve para
ver el formato general ([02-formato-de-curso.md](02-formato-de-curso.md)) aplicado a un caso real de
secundaria.

## 1. De un vistazo

| Dato | Valor |
|---|---|
| Clave del paquete | `co-secundaria-8-matematicas` |
| Versión | 1 |
| País · nivel · grado | CO · secundaria · 8 |
| Materia | Matemáticas |
| Idioma | es |
| Título | Matemáticas · Grado 8 |
| Descripción | Área obligatoria de la Ley 115 artículo 23. Estándares Básicos de Competencias, pensamiento variacional. |
| Emisor | AVACOM |
| Contenido | 7 nodos de índice · 5 materiales · 8 preguntas · rúbrica de 3 criterios |

Publicado, el paquete se llama `avacom-co-secundaria-8-matematicas-v1`.

## 2. Cómo está registrado

Este curso se escribió directamente como **especificación técnica**
(`paquetes\specs\spec_co_secundaria.json`), con los medios en
`paquetes\materiales`. Nació antes que el estándar de carpetas y sirve para
probar el sistema. Un curso nuevo de matemáticas se haría con carpetas, como se
muestra en la sección 7.

## 3. El índice curricular

Cadena de matemáticas: **area → pensamiento → estandar → tema**.

    area  L115-A8  Matemáticas
    ├─ pensamiento  EBC-VAR  Pensamiento variacional y sistemas algebraicos y analíticos
    │   └─ estandar  EBC-8-VAR-01  Identifico relaciones entre propiedades de las gráficas
    │       │                       y propiedades de las ecuaciones algebraicas
    │       ├─ tema  DBA-8-05  Función lineal y su representación gráfica
    │       └─ tema  DBA-8-06  Pendiente y razón de cambio
    └─ pensamiento  EBC-NUM  Pensamiento numérico y sistemas numéricos
        └─ estandar  EBC-8-NUM-01  Utilizo números reales en sus diferentes representaciones

Qué conviene notar:

- Los dos temas llevan **objetivo**: «Identifica y analiza la función lineal en
  distintas representaciones» e «Interpreta la pendiente como razón de cambio
  constante». Ese dato solo existe en la especificación técnica.
- El **orden** de cada nodo es el oficial, no el de escritura: el área es la 8
  de la Ley 115 y el pensamiento variacional es el 5 de los cinco pensamientos.
  Así el catálogo los muestra en el orden del marco.
- La rama de pensamiento numérico **no tiene material**. Es válido: el índice
  puede traer el marco completo y llenarse con el tiempo.

## 4. Los materiales

| # | Tipo | Título | Cuelga de | Archivo | Datos extra |
|---|---|---|---|---|---|
| 1 | documento | La función lineal | DBA-8-05 | funcion-lineal.pdf | 6 páginas. «Material de lectura con ejemplos resueltos» |
| 2 | video | Qué significa la pendiente | DBA-8-06 | pendiente.mp4 | 420 s declarados. Subtítulos y transcripción |
| 3 | interactivo | Explorador de rectas | DBA-8-05 | explorador-rectas.zip | «El alumno mueve la pendiente y el corte y ve la recta cambiar» |
| 4 | evaluacion | Evaluación · Función lineal | EBC-8-VAR-01 | — | 8 preguntas, rúbrica de 3 criterios |
| 5 | leccion | Función lineal y razón de cambio | EBC-8-VAR-01 | — | Secuencia de los 4 anteriores |

Fíjate en dónde cuelga cada cosa: los materiales van en el **tema** concreto, y
la evaluación y la lección en el **estándar** que agrupa a los dos temas. Así el
examen y la secuencia abarcan la unidad completa.

Las referencias internas se escribieron a mano y son legibles:
`co-sec-mat-doc-funcion`, `co-sec-mat-video-pendiente`, `co-sec-mat-int-grafica`,
`co-sec-mat-eval-funcion`, `co-sec-mat-lec-funcion`. Con carpetas se generan
solas a partir del título.

## 5. La evaluación

Ocho preguntas: seis que se corrigen solas y dos abiertas. Peso total: 11.

| # | Tipo | Enunciado | Respuesta | Peso | Dificultad |
|---|---|---|---|---|---|
| 1 | opción múltiple | ¿Cuál es la pendiente de la recta y = 3x − 5? | 3 | 1 | baja |
| 2 | opción múltiple | Si la pendiente es negativa, la recta: | desciende de izquierda a derecha | 1 | baja |
| 3 | numérica | Halla el corte con el eje y de y = −2x + 7 | 7 | 1 | media |
| 4 | numérica | Una recta pasa por (0,2) y (4,10). ¿Cuál es su pendiente? | 2 | 1,5 | media |
| 5 | emparejamiento | Empareja cada ecuación con su gráfica | A-2,B-1,C-3 | 1,5 | media |
| 6 | verdadero/falso | Toda función lineal pasa por el origen | falso | 1 | media |
| 7 | abierta | Explica con tus palabras qué representa la pendiente en una situación de la vida diaria | — | 2 | alta |
| 8 | abierta | Un taxi cobra 4000 de banderazo y 1500 por kilómetro. Escribe la función y explica qué es cada número | — | 2 | alta |

Solo la pregunta 6 lleva retroalimentación: «Solo pasa por el origen cuando el
corte con el eje y es cero».

Rúbrica para las dos abiertas:

| Criterio | Qué se espera | Peso |
|---|---|---|
| Interpretación | Relaciona la pendiente con una razón de cambio real | 2 |
| Representación | Escribe correctamente la función pedida | 2 |
| Argumentación | Justifica con lenguaje matemático apropiado | 1 |

**Cómo se ve en el aula.** El paquete guarda la respuesta correcta pero no las
opciones. Hoy el visor muestra el enunciado, el alumno escribe y el sistema
compara sin distinguir mayúsculas. La pregunta 2 solo se acierta escribiendo
exactamente «desciende de izquierda a derecha», y la 5 exige «A-2,B-1,C-3». Al
redactar, las respuestas cortas y sin ambigüedad funcionan mejor.

## 6. La lección

Una sesión que recorre los cuatro materiales. Cada paso lleva una nota para el
docente:

| Paso | Material | Nota |
|---|---|---|
| 1 | La función lineal (PDF) | Lectura previa |
| 2 | Qué significa la pendiente (video) | Explicación |
| 3 | Explorador de rectas (interactivo) | Exploración guiada |
| 4 | Evaluación · Función lineal | Evaluación de cierre |

## 7. Cómo se registraría hoy con carpetas

    contenido\CO\secundaria\08\matematicas\
        curso.txt
        indice.txt
        EBC-8-VAR-01 Función lineal\
            01-La función lineal.pdf
            02-Qué significa la pendiente.mp4
            03-Explorador de rectas\
                index.html
            04-evaluacion.txt
            leccion.txt

`curso.txt`:

    titulo: Matemáticas · Grado 8
    materia: Matemáticas
    idioma: es
    version: 1
    descripcion: Área obligatoria de la Ley 115 artículo 23. Estándares Básicos de Competencias, pensamiento variacional.

`indice.txt`:

    area | L115-A8 | Matemáticas
      pensamiento | EBC-VAR | Pensamiento variacional y sistemas algebraicos y analíticos
        estandar | EBC-8-VAR-01 | Identifico relaciones entre propiedades de las gráficas y propiedades de las ecuaciones algebraicas
          tema | DBA-8-05 | Función lineal y su representación gráfica
          tema | DBA-8-06 | Pendiente y razón de cambio
      pensamiento | EBC-NUM | Pensamiento numérico y sistemas numéricos
        estandar | EBC-8-NUM-01 | Utilizo números reales en sus diferentes representaciones

`04-evaluacion.txt` (tres de las ocho preguntas y la rúbrica; las demás siguen igual):

    titulo: Evaluación · Función lineal

    P: ¿Cuál es la pendiente de la recta y = 3x - 5?
    R: 3
    peso: 1
    dificultad: baja

    P: Toda función lineal pasa por el origen
    R: falso
    dificultad: media
    retro: Solo pasa por el origen cuando el corte con el eje y es cero

    P: Explica con tus palabras qué representa la pendiente en una situación de la vida diaria
    abierta
    peso: 2
    dificultad: alta

    RUBRICA
    Interpretación | Relaciona la pendiente con una razón de cambio real | 2
    Representación | Escribe correctamente la función pedida | 2
    Argumentación | Justifica con lenguaje matemático apropiado | 1

`leccion.txt`:

    titulo: Función lineal y razón de cambio
    descripcion: Una sesión: lectura, video, exploración guiada y evaluación de cierre.

Diferencias con la versión cargada:

- Con una sola carpeta todo cuelga del estándar. Para que el PDF y el
  interactivo cuelguen de DBA-8-05 y el video de DBA-8-06 harían falta tres
  carpetas, y saldrían tres lecciones en vez de una.
- Los pesos 1,5 no caben en el formato de texto, que solo admite enteros: un
  peso con decimales se toma como 1. Habría que decidir entre 1 y 2.
- Se pierden las páginas del PDF, la duración del video, los datos de
  accesibilidad, los objetivos de los temas y las notas por paso de la lección.

## 8. Detalles que conviene notar

- La duración del video se **declara**, no se mide: la especificación dice 420
  segundos y el archivo de muestra dura 30. Nada lo comprueba.
- La evaluación es la misma para todos y sí da nota, que registra el LMS. Este
  curso no tiene banco de preguntas.
- Los tipos de pregunta (opción múltiple, numérica, emparejamiento…) hoy son
  descriptivos: el visor trata todas igual y pide la respuesta escrita.
