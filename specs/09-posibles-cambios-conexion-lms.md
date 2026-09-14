# Posibles cambios · Conexión AVACOM Biblioteca ↔ LMS ante el formato v2

Evaluación de los nueve documentos de `prototype-lms-v04/spec-driven` contra la
API local que hoy publica AVACOM Biblioteca y contra el formato de curso v2
analizado en [08-analisis-formato-v2.md](08-analisis-formato-v2.md).

Fecha: 14 de septiembre de 2026.
Todo lo que sigue se comprobó leyendo el código de la API local, el esquema del
manifiesto y la salida real de la herramienta de contenido.

---

## 1 · Respuesta corta

**Sí, el cambio de formato impacta la conexión, y el impacto es grave en un
punto concreto: la evaluación.**

El formato v2 mueve las preguntas desde un archivo de evaluación, que es un
elemento declarado del paquete, hacia dentro del guion de la lección, que se
empaqueta como un interactivo opaco. Para el LMS eso significa que desaparecen
`evaluacion_ref` y `pregunta_ref`, y con ellos el intento, la respuesta, el
veredicto, la nota, el consolidado del docente y el indicador en vivo del quiz.

El resto de la conexión, el transporte, el descubrimiento por la nota de enlace,
la degradación y el catálogo, no se ve afectado por el formato.

| Área | Impacto del formato v2 |
|---|---|
| Transporte y descubrimiento | Ninguno |
| Catálogo de elementos | Bajo |
| Lista de cursos | Medio |
| Árbol del curso | Alto |
| Evaluación, corrección y nota | **Crítico** |
| Capacidades | Alto, pero por un motivo distinto al formato |
| Sesión en vivo del quiz | Alto, como consecuencia de la evaluación |
| Instalador | Ninguno |

---

## 2 · Sobre `/capacidades`, que era lo que había que mirar con más cuidado

### 2.1 · No existe ningún endpoint `/capacidades`

Conviene decirlo antes que nada, porque cambia cómo se prueba.

`capacidades` es un **campo dentro de la respuesta de `/v1/salud`**, no una ruta.
Se comprobó sobre el código: las rutas que la API publica son `/v1/salud`,
`/v1/catalogo`, `/v1/cursos`, `/v1/curso/{ref}`, `/v1/taxonomia`,
`/v1/elemento/{ref}`, `/v1/mostrar`, y, solo cuando se inyecta una fuente de
contenido, `/v1/medio/{ref}`, `/v1/leccion/{ref}`, `/v1/evaluacion/{ref}`,
`/v1/comprobar` y `/v1/voz/{ref}`. No hay ninguna ruta que contenga la palabra
capacidades, ni `/v1/banco`, ni `/v1/repaso`.

Los documentos del LMS lo tratan bien: el documento 00 lo describe como campo de
salud y el 06 le dedica su sección 4. La aclaración es para que nadie escriba una
prueba contra una ruta que no existe.

### 2.2 · Qué declara la Biblioteca y qué espera el LMS

La declaración es una sola línea de código, y depende de si la aplicación
inyectó una fuente de contenido. Sin fuente declara solo `curso`. Con fuente
declara seis.

| Capacidad | La Biblioteca la declara | El LMS la usa | Estado |
|---|---|---|---|
| `curso` | Siempre | Sí | Correcto |
| `medio` | Con fuente | Sí | Correcto |
| `leccion` | Con fuente | Sí | Correcto |
| `evaluacion` | Con fuente | Sí, es crítica | Declarada, pero ver 2.4 |
| `comprobar` | Con fuente | Sí, es crítica | Declarada, pero ver 2.4 |
| `voz` | Con fuente | **No está en su tabla** | Se declara y nadie la consume |
| `banco` | **No** | Sí, tiene ruta cableada | **501 permanente** |
| `repaso` | **No** | Sí, tiene ruta cableada | **501 permanente** |

Las dos últimas filas son el hallazgo más concreto de esta sección. El documento
00, en su apartado 4.2, y el documento 06, en su sección 4, dan por previstas
`banco` y `repaso`, con rutas y todo. La Biblioteca no las declara ni las tiene
implementadas. El LMS se comporta bien, responde 501 con explicación en vez de
inventar un resultado, pero son dos funciones que nunca van a llegar mientras
nadie las escriba.

Hay que decidir una de dos cosas por cada una: implementarlas en la Biblioteca,
o retirarlas del cliente del LMS para que no figuren como algo pendiente.

### 2.3 · Las capacidades son todo o nada

Las seis capacidades cuelgan de la misma condición: que exista una fuente de
contenido. No hay forma de declarar `evaluacion` sin declarar `comprobar`, ni de
retirar `voz` conservando el resto.

Eso funciona hoy porque las cinco rutas opcionales se implementaron juntas. En
cuanto una evolucione a otro ritmo que las demás, la lista mentirá. Y el propio
contrato dice que declarar una capacidad que no responde es peor que no
declararla, precisamente porque el LMS la usaría.

### 2.4 · El problema de fondo: la capacidad habla del componente, no del contenido

Este es el punto que conecta las capacidades con el formato v2, y es el que
conviene resolver antes que ningún otro.

`capacidades` describe **lo que el componente sabe hacer**, no **lo que el
catálogo instalado contiene**. Son cosas distintas y hoy se confunden.

Con un catálogo hecho solo de cursos v2, donde no hay ni un elemento de tipo
evaluación, la Biblioteca seguiría declarando `evaluacion` y `comprobar`, porque
las rutas existen y responden. El LMS leería esa lista, daría por hecho que puede
montar exámenes, y al pedir una evaluación no recibiría el 501 explicativo que
el contrato prevé: recibiría un 404 o una lista vacía.

El resultado es el peor de los posibles. El profesor ve un curso que se abre, el
alumno recorre el material, y no hay nota ni explicación de por qué no la hay.
La degradación diseñada en el documento 04 nunca se dispara, porque la condición
que la dispara es la ausencia de la capacidad, y la capacidad está presente.

Tres formas de arreglarlo, de menor a mayor esfuerzo:

| Opción | Qué se añade | Ventaja |
|---|---|---|
| A | Contadores en `/v1/salud`: `evaluaciones`, `bancos` | Una línea. El LMS ya lee contadores y sabe degradar con ellos |
| B | `calificable` por lección en `/v1/curso/{ref}` | Ya está pedido en el contrato. Resuelve el caso por lección, que es donde se nota |
| C | Separar `capacidades` de `capacidades_efectivas` | Explícito, pero obliga a versionar el contrato |

La recomendación es **A más B**. A es barato y honesto, B ya estaba comprometido
en el contrato y es lo que permite que la pantalla del alumno diga «esta lección
no tiene evaluación» en lugar de callarse.

---

## 3 · Lista de posibles cambios

Severidad: **C** crítica, **A** alta, **M** media, **B** baja.

### 3.1 · Capacidades

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-01 | Dejar escrito que `capacidades` es un campo de `/v1/salud` y no una ruta | Documentos 00 y 06 | Evita pruebas contra una ruta inexistente | B |
| PC-02 | Decidir sobre `banco` y `repaso`: implementarlas o retirarlas del cliente | Biblioteca o documento 06 | Hoy son dos 501 permanentes presentados como algo previsto | A |
| PC-03 | Añadir `voz` a la tabla de capacidades del LMS con el efecto de su ausencia | Documento 06 sección 4 | Se declara y no figura en el contrato del consumidor | M |
| PC-04 | Publicar contadores de evaluaciones y bancos en `/v1/salud` | API local | Distinguir «el componente sabe corregir» de «hay algo que corregir» | **C** |
| PC-05 | Poder declarar capacidades por separado, no todas atadas a la fuente | API local | Cuando una evolucione sola, la lista mentirá | M |

### 3.2 · Salud, contrato y versiones

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-06 | Cerrar el desacuerdo entre `generacion` y `huella_catalogo` | Documento 06 sección 3 | El LMS pide un contador monótono; la Biblioteca decidió publicar la huella y dejó escrito el motivo. Hoy funciona por el segundo nivel de preferencia, pero el contrato sigue pidiendo lo otro | M |
| PC-07 | Subir `contrato` a 2 si el árbol del curso cambia de forma | API local | Anidar lecciones dentro de secciones rompe a quien ya lee `items` directamente. Añadir campos no lo sube; cambiar la forma sí | A |
| PC-08 | Plan de despliegue para `formato_version` del paquete | Empaquetador e instalador | Un paquete construido con un formato nuevo es rechazado por una Biblioteca instalada antes de descifrar nada. Hay que actualizar el aula antes de publicar contenido v2 | A |

### 3.3 · Lista de cursos

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-09 | Normalizar la clave del paquete a caracteres ASCII | Recolector | El curso v2 produjo `co-primaria-1-inglés`. Esa referencia viaja en la ruta de varias URL del LMS, se guarda en cuatro tablas del expediente y se pinta en la pantalla del alumno | A |
| PC-10 | Decidir si se publica `marco` | Contrato y formato | El contrato lo trae en su ejemplo, la API no lo devuelve y v2 no declara ningún marco curricular | B |
| PC-11 | Fijar la forma de la versión | Formato | v2 usó `1.0` donde el modelo actual usa enteros. El LMS compara versiones como texto, así que `1` frente a `1.0` parece un cambio de versión sin serlo | M |
| PC-12 | Definir qué cuenta el campo `lecciones` | API local | Hoy cuenta elementos de tipo lección. Si v2 las convierte en nodos del índice, hay que decidir qué se cuenta | M |

### 3.4 · Árbol del curso

Aquí está la mayor distancia entre lo que el contrato pide y lo que la API
devuelve, y el formato v2 la cambia en las dos direcciones: agrava unas cosas y
abre la puerta a resolver otras.

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-13 | Añadir el nivel de lección: curso, secciones, lecciones, ítems | API local | El contrato pide tres niveles y la API devuelve dos. El progreso del alumno se escribe por lección, así que sin ese nivel no hay dónde anclarlo. **v2 lo facilita**: su índice ya declara nodos de tipo lección | **C** |
| PC-14 | Devolver el padre de cada sección | API local | La respuesta trae código, título, tipo, orden e ítems, y no dice de quién cuelga cada nodo. El LMS no puede reconstruir el árbol con esta ruta sola | A |
| PC-15 | Corregir el orden de las secciones | API local | El orden se numera por padre y la lista se devuelve plana, así que dos nodos de ramas distintas comparten número. En el curso v2 los dos temas y sus cuatro lecciones se entremezclan | A |
| PC-16 | Ordenar los ítems por su orden real, no por el título | API local | Hoy se ordenan alfabéticamente y el número de orden se inventa a partir de esa lista. En el curso v2 la lección 02 sale antes que la 01, porque su título empieza por una letra anterior | A |
| PC-17 | Publicar `calificable` por lección | API local y formato | Es lo que permite decirle al alumno que una lección no tiene evaluación, en vez de callarse | A |
| PC-18 | Distinguir `evaluacion_ref` de `elemento_ref` en los ítems | API local | El contrato los separa. La API devuelve solo `elemento_ref` y el tipo | M |
| PC-19 | Añadir `/v1/curso/{ref}/manifiesto` | API local | Respuesta diminuta para poder preguntar a menudo por un curso. Hoy hay que traerse el árbol entero | M |
| PC-20 | Revisar la regla de omitir secciones vacías | API local | Es correcta, pero con el curso v2 aplastó el curso entero a una sola sección, porque las carpetas no enganchaban con el índice y todo acabó colgando del primer nodo | M |
| PC-21 | Exigir código estable en los nodos de lección | Formato | El código lógico es lo único que hace que el progreso sobreviva a una republicación. En v2 los códigos venían vacíos, así que el código se deduce del nombre: renombrar una lección deja huérfano el progreso de todos los alumnos | **C** |

### 3.5 · Evaluación, corrección y nota

Este bloque es el que contesta la pregunta de fondo.

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-22 | Guardar y publicar las opciones de cada pregunta | Formato, empaquetador, manifiesto y API | El contrato exige opciones con referencia y texto, sin ningún indicador de corrección. Hoy no se publican porque **no se guardan**: el manifiesto no tiene tabla de opciones, solo la clave de respuesta. v2 escribe opciones en el guion y el recolector las descarta. Hay que tocar la cadena entera | **C** |
| PC-23 | Decidir dónde vive la evaluación en v2 | Formato | v2 no produce ningún elemento de tipo evaluación: los quiz viven dentro del guion y se empaquetan como interactivo. Sin evaluación no hay referencia de evaluación ni de pregunta, y sin ellas no hay intento, ni respuesta, ni veredicto, ni nota, ni consolidado | **C** |
| PC-24 | Definir el puntaje máximo de una evaluación | Formato y API | El contrato lo pide por ítem y el LMS lo necesita para calcular la nota. No existe en ningún sitio: lo más parecido es la suma de los pesos de las preguntas | A |
| PC-25 | Definir cómo vuelve el resultado de un interactivo | Contrato | La página generada avisa de su resultado por una función del anfitrión. Ese aviso lo recoge la aplicación de la Biblioteca, y no hay ninguna ruta por la que llegue al LMS. Si la evaluación de v2 se queda dentro del interactivo, esto es obligatorio | A |
| PC-26 | No aceptar la respuesta correcta por letra | Formato | v2 marca la correcta con la letra de la opción. Reordenar las opciones cambia la respuesta sin que nadie lo note. Debe marcarse en la propia opción | M |

### 3.6 · Aula, tabletas y sesión en vivo

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-27 | Reconsiderar la clave de la sesión en vivo | Documento 07 | El canal se identifica con la referencia de la evaluación. Con v2 no hay evaluaciones, así que el indicador de cuántos alumnos están viendo el quiz se queda sin clave | A |
| PC-28 | Codificar las referencias en las URL de los clientes | Clientes del LMS | Consecuencia de PC-09. Una referencia con tilde viaja en la ruta de las peticiones de la tableta y en el visor de Android | M |

### 3.7 · Instalador

| ID | Cambio | Dónde | Por qué | Sev |
|---|---|---|---|---|
| PC-29 | Ninguno propio, salvo acompañar PC-08 | Documento 08 | Los dos productos solo comparten la nota de enlace, y el formato no la toca. Lo único que hay que coordinar es que el aula tenga una Biblioteca capaz de abrir un paquete v2 antes de que llegue el primero | B |

---

## 4 · Lo que el formato v2 no rompe

Conviene decirlo para no rehacer lo que ya funciona.

| Pieza | Por qué aguanta |
|---|---|
| La nota de enlace, el puerto efímero y la ficha | No dependen del contenido |
| La comprobación de que el proceso sigue vivo | Ídem |
| El tiempo de espera corto y la traducción de fallos | Ídem |
| La degradación 503, 501, 502 y 404 | Ídem. Es la parte mejor resuelta de la conexión |
| La regla de que el LMS no guarda títulos ni estructura | v2 no la afecta; si acaso la refuerza |
| La regla de que la clave se compara donde vive | Se conserva: la API solo dice si acierta |
| El catálogo plano de elementos | Sigue funcionando: v2 produce interactivos y lecciones, que son tipos conocidos |
| La política de la escuela aplicada antes de responder | No depende del formato |
| El instalador y la convivencia de los dos productos | No se tocan |

---

## 5 · Por dónde empezar

El orden importa, porque unos cambios bloquean a otros.

| Orden | Qué | Bloquea a |
|---|---|---|
| 1 | **PC-23**: decidir si en v2 la evaluación sigue siendo un elemento propio o pasa a vivir dentro del interactivo | PC-22, PC-24, PC-25, PC-27 y toda la cadena de nota |
| 2 | **PC-21**: exigir código estable en los nodos de lección | PC-13 y el progreso del alumno |
| 3 | **PC-04**: contadores de contenido en salud | Que el LMS pueda degradar con una explicación |
| 4 | **PC-13** con **PC-14**, **PC-15** y **PC-16**: el árbol del curso completo y bien ordenado | La pantalla del alumno |
| 5 | **PC-22**: las opciones, por toda la cadena | Las evaluaciones de opción múltiple |
| 6 | **PC-07** y **PC-08**: versionado del contrato y del formato del paquete | El despliegue en aulas ya instaladas |
| 7 | **PC-09**: normalizar la clave | Lo antes posible: cada curso publicado con una clave con tilde es una clave que ya no se puede cambiar |

PC-09 merece un comentario aparte. La clave del paquete no se puede cambiar
después de publicar, porque es lo que el LMS guarda en el expediente de cada
alumno. Cada curso que se publique con una tilde en la clave es un curso con el
que habrá que convivir. Conviene arreglarlo antes del siguiente paquete, no
después.

---

## 6 · Preguntas que hay que cerrar

Se proponen como continuación de la tabla de decisiones del documento 03.

| ID | Pregunta | Por qué bloquea |
|---|---|---|
| Q-39 | ¿La evaluación en v2 es un elemento propio o vive dentro del interactivo? | Es la pregunta de la que cuelga toda la nota |
| Q-40 | ¿El código de la lección es obligatorio en el índice? | Sin él, el progreso no sobrevive a una republicación |
| Q-41 | ¿Quién declara el puntaje máximo, y cómo se deriva de los pesos? | La nota necesita una escala |
| Q-42 | ¿Se implementan `banco` y `repaso` o se retiran del cliente? | Dos capacidades previstas que nunca llegan |
| Q-43 | ¿Cómo se distingue lo que el componente sabe hacer de lo que el catálogo contiene? | Es lo que hoy impide degradar con una explicación |
| Q-44 | ¿Qué versión de contrato y de formato de paquete acompaña a v2, y en qué orden se despliega? | Un paquete nuevo en un aula antigua no se abre |

---

## 7 · Cómo se comprobó

| Afirmación | Cómo se verificó |
|---|---|
| No existe `/v1/capacidades`, ni `/v1/banco`, ni `/v1/repaso` | Búsqueda sobre el despachador de rutas de la API local |
| La lista de capacidades y su condición | Lectura de la propiedad que la construye |
| La evaluación no publica opciones | Lectura del endpoint de evaluación y del esquema del manifiesto, que no tiene tabla de opciones |
| El árbol del curso tiene dos niveles y no devuelve el padre | Lectura del endpoint de curso |
| Los ítems se ordenan por título y el orden se sintetiza | Lectura del mismo endpoint |
| El curso v2 se aplasta a una sola sección | Se armó el curso del ZIP y se leyó la especificación resultante |
| La clave del paquete sale con tilde | Misma especificación: `co-primaria-1-inglés` |
| v2 no produce ningún elemento de tipo evaluación | Misma especificación: cuatro interactivos y dos lecciones de secuencia |
