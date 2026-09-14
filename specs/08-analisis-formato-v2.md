# Formato v2 · Análisis del contenido recibido

Comparación entre el curso entregado (`guion_prueba.txt` y `curso-9.zip`) y el
formato que hoy documenta [05-resumen-formato-curso.md](05-resumen-formato-curso.md)
y que lee la herramienta del proyecto.

Fecha del análisis: 14 de septiembre de 2026.
El guion suelto y el que viene dentro del ZIP son **idénticos**, byte a byte.

---

## 1. Veredicto en una línea

El material recibido usa un formato **distinto del actual**. La herramienta lo
acepta sin errores, pero produce un curso roto: se armó de verdad y salieron
**13 avisos**, los cinco cuestionarios quedaron sin respuestas, todo el material
se colgó del nodo equivocado y solo se generó **1 de los 4** archivos de medios.

| | Resultado |
|---|---|
| Errores que bloquean | 0 |
| Avisos | 13 |
| Materiales detectados | 6 (4 interactivos, 2 lecciones de secuencia) |
| Medios que llegaron a existir | 1 de 4 |
| Cuestionarios utilizables | 0 de 5 |
| Veredicto de la herramienta | «Se puede armar» |

Ese «se puede armar» es lo peligroso: **nada impide publicar este curso**, y el
problema solo aparecería en el aula.

---

## 2. Comparación de árboles

A la izquierda, el modelo actual. A la derecha, lo que llegó.

### Modelo actual (v1, documentado)

```
contenido/
└── CO/
    └── secundaria/
        └── 09/
            └── humanidades/                     materia sin tildes
                ├── curso.txt                    titulo, materia, idioma, version, descripcion
                ├── indice.txt                   tipo | CÓDIGO | nombre
                │
                └── DBA-9-03 Romanticismo/       CÓDIGO + espacio + nombre
                    ├── leccion.txt              opcional
                    ├── 01-Guía de lectura.pdf   material suelto, numerado
                    ├── 02-Del Romanticismo al Modernismo/
                    │   ├── guion.txt            lección completa
                    │   ├── linea-tiempo.png     los medios, al lado del guion
                    │   └── valle.mp4
                    ├── 03-Comparador/
                    │   └── index.html           interactivo
                    ├── 99-evaluacion.txt        SÍ da nota
                    └── 90-banco.txt             alimenta el examen
```

### Lo recibido (v2)

```
contenido/
└── CO/
    └── primaria/
        └── 01/
            └── inglés/                          (1) materia CON tilde
                ├── curso.txt                    (2) sin descripcion, version "1.0"
                ├── indice.txt                   (3) SIN códigos, tipo "leccion"
                │
                ├── El verbo to be en presente/  (4) tema SIN código delante
                │   ├── 01-I am y you are/
                │   │   └── guion.txt            (5) gramática ampliada
                │   └── 02-He is, she is, it is/
                │       └── SIN-REDACTAR.txt     (6) marcador de borrador
                │
                └── Presente simple con acciones diarias/
                    ├── 01-I like y I have/
                    │   └── SIN-REDACTAR.txt
                    └── 02-Me presento en inglés/
                        └── SIN-REDACTAR.txt

                                                 (7) sin evaluación ni banco
                                                 (8) sin ningún medio: faltan
                                                     las 3 imágenes citadas
```

Seis archivos en total, 3.552 bytes. No hay ni una imagen, ni un audio, ni un
vídeo.

---

## 3. Los ocho cambios, uno a uno

### (1) La materia lleva tilde

| | |
|---|---|
| Modelo actual | Una palabra corta, sin tildes ni espacios: `humanidades` |
| v2 | `inglés` |
| Qué hace la herramienta | La acepta sin decir nada |

La materia de la carpeta se usa tal cual para formar la clave del paquete. El
resultado real fue `co-primaria-1-inglés`, y esa tilde se propagó a todo: al
nombre del archivo de especificación, a la carpeta de medios, a la referencia de
cada material y al nombre de cada archivo comprimido.

Esa clave es lo que el LMS guarda en el expediente de cada alumno y lo que viaja
en las direcciones de la interfaz local. Funciona por poco, pero es frágil.
**Recomendación: `ingles`, sin tilde.**

### (2) La ficha del curso

```
titulo: ygtfiuy
materia: inglés
idioma: es
version: 1.0
```

| Campo | Situación |
|---|---|
| `titulo` | Texto de relleno. Es lo que vería el profesor en el catálogo |
| `version` | `1.0` en vez de un número entero. El modelo actual sube de 1 a 2, a 3 |
| `descripcion` | No está. Nadie avisa de ello |

La descripción es el único sitio donde se dice a qué marco curricular responde
el curso. Sin ella no hay forma de auditarlo después.

### (3) El índice, sin códigos y con un tipo nuevo

Lo recibido:

```
tema |  | El verbo to be en presente
    leccion | | I am y you are
    leccion | | He is, she is, it is
tema |  | Presente simple con acciones diarias
    leccion | | I like y I have
    leccion | | Me presento en inglés
```

Dos diferencias de fondo:

**Los códigos están vacíos.** El modelo actual permite dejarlos vacíos, pero
entonces ese nodo **no puede recibir material**, porque el enganche entre una
carpeta de tema y su nodo se hace por el código. Al estar todos vacíos, ningún
tema se pudo enganchar.

**`leccion` aparece como tipo de nodo.** En el modelo actual el índice es el
marco curricular oficial y nada más: área, factor, pensamiento, estándar, tema.
La lección no es un nodo del currículo, es un material que se genera solo a
partir de la carpeta del tema. En v2 el índice declara además la lista de
lecciones, que es un cambio de concepto, no de sintaxis.

Tampoco hay nodo raíz de área ni ninguna referencia al marco colombiano. Para
inglés en primaria eso serían los Estándares Básicos de Competencias en Lenguas
Extranjeras, con sus códigos.

### (4) Las carpetas de tema no llevan código delante

| | |
|---|---|
| Modelo actual | `DBA-9-03 Romanticismo` |
| v2 | `El verbo to be en presente` |

Esto produjo el peor efecto de todos, y conviene verlo con el resultado real.
La herramienta toma la primera palabra del nombre como si fuera el código: leyó
`EL` y `PRESENTE`. Como ninguno existe en el índice, avisó y colgó **todo** del
primer nodo. El resultado:

| Material | Debería colgar de | Colgó de |
|---|---|---|
| I am y you are | El verbo to be en presente | El verbo to be en presente ✓ |
| He is, she is, it is | El verbo to be en presente | El verbo to be en presente ✓ |
| I like y I have | Presente simple con acciones diarias | **El verbo to be en presente** ✗ |
| Me presento en inglés | Presente simple con acciones diarias | **El verbo to be en presente** ✗ |

Los cuatro nodos de tipo `leccion` se quedaron vacíos, y el segundo tema
completo quedó colgado del primero.

Además, al comerse la primera palabra, los títulos de las dos secuencias que la
herramienta genera sola salieron mutilados: **«verbo to be en presente»** y
**«simple con acciones diarias»**. Eso es lo que leería el profesor.

### (5) La gramática del guion, ampliada

Aquí está el cambio más grande. El modelo actual admite ocho cosas y ninguna
más. El guion recibido usa seis construcciones que no existen.

| Construcción v2 | Ejemplo | Qué hace hoy la herramienta |
|---|---|---|
| `PROFESOR:` | `PROFESOR: Pregunta quién jugó a la lleva...` | **Lo imprime como párrafo visible para el alumno** |
| `VOCABULARIO:` | `VOCABULARIO: I\|Palabra que significa "yo".` | Lo imprime como párrafo, con la barra a la vista |
| `ACTIVIDAD:` | `ACTIVIDAD: Arrastra la imagen correcta...` | Lo imprime como párrafo. No genera ninguna actividad |
| `A)` `B)` `C)` | `A) You` | **Lo descarta en silencio** |
| `R:` dentro del cuestionario | `R: B` | **Lo descarta en silencio** |
| `E:` | `E: I significa "yo"...` | **Lo descarta en silencio** |
| `FIN QUIZ` | `FIN QUIZ` | Lo descarta. El cuestionario se cierra igual, por la línea en blanco |
| Emoji en el título de sección | `## 🎮 El juego de la lleva` | Lo acepta y lo muestra tal cual |

Lo que sí es compatible y funciona bien: la cabecera con `titulo`, `objetivo` y
`duracion`; las secciones con `##`; los párrafos; las ideas destacadas con `>`;
y `IMAGEN:` con la barra vertical, que funciona con o sin espacios alrededor.

**Cómo se escribe hoy un cuestionario y cómo llegó:**

```
   MODELO ACTUAL                      LO RECIBIDO (v2)

   QUIZ                               QUIZ
   P: ¿Qué palabra usas...?           P: ¿Qué palabra usas...?
   - You                              A) You
   - I *          ← asterisco         B) I
   - He                               C) He
   retro: I significa "yo".           R: B        ← respuesta por letra
                                      E: I significa "yo".
                                      FIN QUIZ
```

Se abrió la página generada para comprobarlo. Los cinco cuestionarios salen con
el enunciado y **cero opciones**. El alumno ve la pregunta y no puede responder.

Hay un efecto secundario que conviene conocer. La barra de progreso cuenta seis
secciones más cinco cuestionarios, once cosas en total. Como ningún cuestionario
se puede contestar, el progreso se queda en un máximo del 55 %, la tarjeta de
cierre no aparece nunca y la lección nunca avisa de que terminó.

Sobre la respuesta por letra: el modelo actual guarda el texto de la respuesta,
no su posición. Una clave como `R: B` depende del orden de las opciones, así que
reordenarlas cambia la respuesta correcta sin que nadie lo note. Si v2 se adopta,
conviene marcar la correcta en la propia opción, no aparte.

### (6) `SIN-REDACTAR.txt`, un estado de borrador

Tres de las cuatro lecciones traen un archivo con el texto «La leccion X aun no
esta redactada».

El modelo actual **no tiene estados de borrador ni de aprobado**, y está
documentado como una carencia conocida. La convención es razonable, pero hoy no
significa nada para la herramienta: esas tres carpetas se tratan como
interactivos rotos, avisan de que les falta el punto de entrada, y aun así se
registran como material del curso.

El resultado real: la especificación declara cuatro archivos comprimidos y solo
se creó **uno**. Los otros tres se referencian y no existen. Al construir el
paquete, el empaquetador pondría un marcador de posición en su lugar. Eso nunca
se publica.

### (7) No hay evaluación ni banco

En el modelo actual la nota sale de la evaluación o del examen que se monta con
el banco. El cuestionario de la lección **no da nota**: sirve para que el alumno
sepa si conviene repasar.

El curso recibido no trae ni evaluación ni banco. Toda la comprobación está
dentro del guion, en cuestionarios que por definición no califican. Tal como
está, **este curso no puede producir ninguna nota**, y el LMS no tendría nada
que registrar en el expediente.

### (8) No hay ningún medio

El guion cita tres imágenes y ninguna de las tres viene en el ZIP:

- `nino-senalando-su-pecho.png`
- `dos-ninos-senalandose-entre-si.png`
- `pantalla-arrastrar-fotos-ninos.png`

La herramienta avisa de las tres, una por una, con el nombre del archivo y la
sección donde se citan.

---

## 4. Lo que dijo la herramienta

Salida real de la revisión, sin recortar nada:

```
  ygtfiuy
  CO · primaria · grado 1 · inglés
  clave: co-primaria-1-inglés   version: 1.0

  6 nodos de indice curricular
  6 materiales:  interactivo 4  leccion 2

  Avisos (13) — se puede armar, pero conviene mirarlos:
    · La carpeta «El verbo to be en presente» no coincide con ningun codigo
      de indice.txt. Su material quedara colgando de la raiz.
    · QUIZ «¿Qué palabra usas para hablar de ti mism»: no tiene opciones debajo.
    · QUIZ «¿Qué gesto haces cuando escuchas you are»: no tiene opciones debajo.
    · QUIZ «¿Cómo se pronuncia I am?»: no tiene opciones debajo.
    · QUIZ «¿Qué palabra va siempre con la forma am?»: no tiene opciones debajo.
    · QUIZ «Si señalas a tu compañero, ¿qué frase us»: no tiene opciones debajo.
    · Falta el archivo «nino-senalando-su-pecho.png», citado en la seccion
      «I am: hablo de mí».
    · Falta el archivo «dos-ninos-senalandose-entre-si.png», citado en la
      seccion «You are: hablo de ti».
    · Falta el archivo «pantalla-arrastrar-fotos-ninos.png», citado en la
      seccion «⚡ A jugar con las frases».
    · 02-He is, she is, it is: es una carpeta de interactivo y no tiene
      index.html. El punto de entrada tiene que llamarse asi.
    · La carpeta «Presente simple con acciones diarias» no coincide con
      ningun codigo de indice.txt. Su material quedara colgando de la raiz.
    · 01-I like y I have: es una carpeta de interactivo y no tiene index.html.
    · 02-Me presento en inglés: es una carpeta de interactivo y no tiene
      index.html.

  Se puede armar.
```

Ningún aviso menciona `PROFESOR:`, `VOCABULARIO:`, `ACTIVIDAD:`, `A)`, `R:`,
`E:` ni `FIN QUIZ`. La herramienta no sabe que existen, así que no puede
quejarse de ellos. **Ese es el riesgo de fondo: los cambios de v2 son
silenciosos.**

---

## 5. Qué hacer

### Si se decide adoptar v2

Hay que implementarlo, porque hoy no existe. Por orden de urgencia:

| # | Qué | Por qué |
|---|---|---|
| 1 | Cuestionario con opciones `A)` `B)` `C)`, `R:` y `E:` | Sin esto ningún cuestionario de v2 funciona. Conviene marcar la correcta en la opción, no por letra |
| 2 | `PROFESOR:` como nota que **no ve el alumno** | Hoy las instrucciones del docente se le muestran al niño |
| 3 | `VOCABULARIO:` como glosario con su propio aspecto | Hoy sale como párrafo con una barra suelta |
| 4 | `ACTIVIDAD:` como actividad de verdad | Hoy es solo un párrafo que describe algo que no ocurre |
| 5 | Enganche del tema por nombre, no solo por código | Es lo que rompe el reparto del material entre nodos |
| 6 | `leccion` como tipo válido de nodo, o quitarlo del índice | Decidir si el índice declara lecciones o solo el currículo |
| 7 | `SIN-REDACTAR.txt` como estado de borrador reconocido | Que no se cuente como material ni genere medios fantasma |
| 8 | Avisar de las etiquetas desconocidas | Que un `PROFESOR:` en una herramienta que no lo entiende dé un aviso, no un párrafo |

También hay que decidir de dónde sale la nota en v2, porque hoy no sale de
ningún sitio.

### Si hay que publicar este curso con el formato actual

| Qué | Cómo |
|---|---|
| Renombrar la carpeta de materia | `inglés` → `ingles` |
| Rellenar la ficha | Título real en vez de `ygtfiuy`, `version: 1`, y una descripción con el marco curricular |
| Poner códigos en el índice | Los oficiales de lenguas extranjeras, y quitar los nodos `leccion` |
| Renombrar las carpetas de tema | Código, un espacio, y luego el nombre |
| Reescribir los cinco cuestionarios | Opciones con guion y un asterisco en la correcta; `E:` pasa a `retro:` |
| Mover las instrucciones del docente | Fuera del guion: hoy las lee el alumno |
| Convertir el vocabulario | En lista con guiones o en una lámina |
| Aportar las tres imágenes | 1920×1080, fondo gris papel |
| Redactar las otras tres lecciones | O sacarlas del entregable hasta que existan |
| Añadir evaluación o banco | Sin ellos el curso no produce nota |

---

## 6. Cómo se comprobó

Todo lo anterior se ejecutó de verdad sobre una copia del ZIP, fuera del
repositorio. Nada del proyecto se modificó.

| Paso | Qué se hizo |
|---|---|
| Extracción | El ZIP trae 6 archivos y 3.552 bytes. El guion suelto es idéntico al del ZIP |
| Revisión | Se ejecutó la revisión del proyecto. Salida completa en la sección 4 |
| Armado | Se armó en una carpeta temporal para ver la especificación real |
| Lectura de la especificación | De ahí salen la clave con tilde, el reparto de nodos y los títulos mutilados |
| Lectura de la página generada | De ahí sale que las notas del docente se muestran al alumno y que los cinco cuestionarios salen sin opciones |
