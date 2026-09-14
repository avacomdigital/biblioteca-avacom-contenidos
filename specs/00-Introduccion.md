# 00 · Introducción · Cómo se conectan AVACOM Biblioteca y el LMS

| Campo | Valor |
|---|---|
| Estado | **Vigente.** Describe lo que existe en el código a la fecha de corte |
| Ámbito | La frontera entre AVACOM Biblioteca (este repositorio) y AVACOM OPS, el LMS |
| Audiencia | Desarrolladores de los dos productos que llegan nuevos |
| Fecha de corte | 14 de septiembre de 2026 |
| Detalle | El contrato completo está en [01-Contrato-Biblioteca-LMS.md](01-Contrato-Biblioteca-LMS.md) y cada ruta en [04-api-calls.md](04-api-calls.md) |

Este documento explica la conexión en su conjunto: qué es cada producto, cómo
se encuentran, qué se dicen y qué pasa cuando uno de los dos no está. No
repite la forma exacta de cada respuesta.

---

## 1 · Dos productos, una frontera

En el equipo maestro del aula, el que va dentro de la pantalla interactiva de
86 pulgadas, corren dos aplicaciones distintas. Se desarrollan por equipos
distintos, tienen bases de datos distintas y no comparten código.

| | AVACOM Biblioteca | AVACOM OPS (el LMS) |
|---|---|---|
| Qué es | La biblioteca de contenido educativo cifrado | El sistema que lleva alumnos, grupos, exámenes y calificaciones |
| Qué posee | Los cursos: su estructura, sus materiales, sus preguntas y las claves de respuesta | El expediente: inscripciones, avances, intentos, respuestas, veredictos y notas |
| Qué sabe hacer | Instalar paquetes, verificar firmas, descifrar al vuelo, mostrar material en la pantalla | Registrar lo que cada estudiante hace y mostrárselo al docente |
| Tecnología | .NET 10, MAUI, SQLite. Sin backend ni servicio | Django y DRF en el backend, MAUI en el escritorio y en las tabletas |
| Puertos | Solo `127.0.0.1`, puerto efímero | `0.0.0.0:8000` hacia la red del aula |

La regla que gobierna todo lo demás: **el contenido vive en la Biblioteca, el
expediente vive en el LMS, y ninguno reescribe lo del otro.** Un paquete de
contenido se puede reinstalar. Una nota perdida no se recupera. Por eso cada
dato vive donde ocurre y se escribe una sola vez.

---

## 2 · La topología

```
   Tableta (AVACOM Student) ──┐
                              │  HTTP en la red del aula, puerto 8000
   AVACOM OPS Master ─────────┼──────────────►  Backend de OPS
   (escritorio del docente)   │                       │
                              ┘                       │  HTTP solo por loopback
                                                      │  puerto efímero
                                                      │  cabecera X-Avacom-Ficha
                                                      ▼
                                          AVACOM Biblioteca · 127.0.0.1:{efímero}
                                          (este repositorio)
```

Tres consecuencias que no son negociables:

1. **El backend del LMS es el único cliente de la Biblioteca.** Ninguna tableta
   la alcanza. Lo que una tableta necesita lo pide al backend, y el backend lo
   pide a la Biblioteca y lo retransmite.
2. **La Biblioteca nunca abre un puerto hacia la red.** El puerto lo elige el
   sistema en cada arranque, solo escucha en el propio equipo y no hace falta
   ninguna regla de cortafuegos.
3. **El aula no tiene internet.** No «puede quedarse sin internet»: no lo tiene.
   Todo lo descrito aquí funciona con el cable de red desconectado.

---

## 3 · Los principios

Son los artículos de la constitución que gobierna la frontera. Cada uno tiene
una prueba que lo fija, y la lista completa de pruebas está en la sección 5
del contrato.

| # | Principio | Qué impide |
|---|---|---|
| 1 | El contenido nunca sale del equipo maestro sin descifrar | Que una copia de un paquete sirva en otro equipo |
| 2 | La clave de respuesta no sale de la Biblioteca | Que el LMS, un volcado de memoria o una pantalla la muestren. Solo se compara |
| 3 | El LMS no lee la base de datos de la Biblioteca | Que un cambio de esquema en un producto rompa al otro |
| 4 | La Biblioteca no sabe nada del LMS | Que la Biblioteca dependa de tablas de personas o de grupos que no le pertenecen |
| 5 | Se guardan referencias más versión, nunca títulos | Que el expediente deje de poder explicarse cuando se corrija una errata |
| 6 | Lo que no llega, no existe | Que el LMS muestre material que la escuela desactivó |
| 7 | El LMS no guarda un catálogo propio | Que exista un segundo catálogo que alguien tenga que sincronizar a mano |
| 8 | Sin contenido, el LMS sigue funcionando | Que un aula recién montada, o con la Biblioteca cerrada, se quede en blanco |
| 9 | La versión del contrato se comprueba antes de hablar | Que un cambio de forma falle de manera rara semanas después |
| 10 | La Biblioteca no instala, no desinstala ni cambia políticas por orden del LMS | Que el catálogo cambie debajo de los pies del profesor a mitad de clase |

---

## 4 · El ciclo de vida de una conexión

Es lo mismo cada vez, y conviene tenerlo entero en la cabeza.

```
   1 · La Biblioteca arranca y abre la pestaña «Contenido AVACOM»
       └─► escribe %ProgramData%\AVACOM\contenido\enlace.json
           { Contrato, Puerto, Ficha, Proceso }

   2 · El LMS lee la nota, en cada petición, sin cachearla
       ├─ no existe ──────────────► «sin contenido»: estado normal, degradar
       ├─ Contrato distinto de 1 ─► negarse a hablar y avisar
       └─ bien ───────────────────► seguir

   3 · GET /v1/salud
       └─► guardar huella_catalogo · leer capacidades

   4 · GET /v1/cursos ─► GET /v1/curso/{curso_ref}
       └─► pintar asignaturas y secciones, guardando solo referencias

   5 · Según capacidades:
       GET /v1/medio/{ref}       bytes descifrados, por rangos
       GET /v1/leccion/{ref}     pasos de una lección
       GET /v1/evaluacion/{ref}  preguntas sin clave
       POST /v1/comprobar        veredicto de una respuesta
       GET /v1/voz/{ref}         instrucción hablada

   6 · Cada pocos segundos: GET /v1/salud
       └─ huella distinta ─► volver al paso 4

   7 · La Biblioteca se cierra ─► borra la nota. El LMS vuelve al paso 2
```

Cuatro detalles del ciclo que se aprenden a golpes si nadie los cuenta:

- **La nota se relee siempre.** Guardarla en memoria es la forma segura de
  seguir hablando con un puerto que ya murió.
- **La huella se compara por igualdad y nada más.** No es un contador, no
  crece, y vuelve al valor anterior si se deshace un cambio.
- **Las capacidades se leen antes de usar cualquier ruta opcional.** Lo que no
  esté en la lista no se simula: se degrada con una explicación.
- **El tiempo de espera es corto.** Es loopback. Si no contesta en tres
  segundos no va a contestar, y un tiempo largo congela la pantalla del
  docente delante de la clase.

---

## 5 · Qué posee cada lado

| Dato | Dueño | Cómo lo ve el otro |
|---|---|---|
| Paquetes, licencia, claves del equipo | Biblioteca | No los ve. Pide bytes descifrados por la API |
| Estructura curricular y de cada curso | Biblioteca | La resuelve en vivo por referencia, no la guarda |
| Materiales, preguntas y rúbricas | Biblioteca | Preguntas sin clave; materiales por `/v1/medio` |
| Claves de respuesta | Biblioteca | Nunca. Envía una respuesta y recibe un veredicto |
| Política de la escuela (lo desactivado) | Biblioteca | No la ve: el catálogo ya viene filtrado |
| Inscripciones, progreso, intentos, notas | LMS | La Biblioteca no los conoce ni los necesita |
| Reparto de material a la clase | LMS | Guarda `elemento_ref` y versión; la Biblioteca solo muestra |
| Memoria de la última revisión de disponibilidad | LMS | Fechas, nunca contenido. Existe para explicar una ausencia con la Biblioteca cerrada |
| Registro de repaso (qué se abrió y cuánto tiempo) | Biblioteca | Dos tablas propias, con una columna de persona que admite nulo |

La única costura entre las dos bases es esa columna de persona, que admite
nulo a propósito: la Biblioteca se puede consultar sin identificarse, y en
preescolar directamente no hay con qué identificarse.

---

## 6 · Cómo se degrada

La conexión está diseñada para fallar bien. Cada situación tiene una respuesta
prevista y ninguna deja una pantalla en blanco.

| Situación | Lo que ve el backend del LMS | Lo que hace |
|---|---|---|
| No hay nota de enlace | Nada que leer | «Sin contenido». Muestra el expediente y sigue |
| Hay nota pero nadie escucha en el puerto | Conexión rechazada | Igual que sin nota: la aplicación se cerró de golpe |
| El proceso de la nota ya no existe | Comprobación de PID | Igual que sin nota |
| `Contrato` mayor que el soportado | Número desconocido | Avisa de que hay que actualizar el LMS y no llama |
| Falta la ficha o es incorrecta | 401 | Relee la nota y reintenta una vez |
| La ruta no existe en esta versión | 404 | Comprueba `capacidades`: es una capacidad no publicada |
| Lo desactivó la escuela | 403 | De cara al docente, igual que 404 |
| No se pudo mostrar | 409 con motivo | Enseña el motivo tal cual: está escrito para una persona |
| Capacidad no declarada | No llama | 501 hacia arriba, con la lista de capacidades |

Hacia arriba, el backend traduce todo esto a tres códigos: 503 cuando la
Biblioteca no está, 501 cuando falta una capacidad y 502 cuando la Biblioteca
contestó con error. El escritorio y las tabletas convierten esos códigos en una
lista vacía o en un aviso, nunca en una excepción que tumbe la pantalla.

La regla que hay que hacer explícita en todo el código del LMS: **«no se pudo
comprobar» no es «no está»**. Afirmar que un curso desapareció cuando solo
está cerrada la Biblioteca es peor que no decir nada.

---

## 7 · Mapa de la documentación

Los documentos de esta carpeta se leen en orden. Cada uno dice en su cabecera
si describe lo que existe o lo que se propone.

| Documento | Para quién | Qué contiene |
|---|---|---|
| **00** · este | Todos | La conexión en su conjunto |
| [01 · Contrato Biblioteca ↔ LMS](01-Contrato-Biblioteca-LMS.md) | Equipo del LMS y de la Biblioteca | Cifrado, licencia, el contrato y cómo condiciona el consumo |
| [02 · Formato de curso](02-formato-de-curso.md) | Equipo de contenido | La guía completa del formato de carpetas y archivos |
| [03 · Archivos JSON](03-archivos-JSON.md) | Equipo técnico | **Propuesta, no implementada**: pasar los archivos de texto a JSON |
| [04 · Llamadas a la API](04-api-calls.md) | Equipo del LMS | Cada ruta, su forma y sus códigos, con la lista de capacidades |
| [05 · Resumen del formato](05-resumen-formato-curso.md) | Equipo de contenido | Versión corta del 02, con árbol de ejemplo y errores que evitar |
| [06 · Ejemplo: Matemáticas](06-ejemplo-curso-matematicas.md) | Equipo de contenido | Un curso real de secundaria explicado |
| [07 · Ejemplo: Exploración](07-ejemplo-curso-exploracion.md) | Equipo de contenido | Un curso real de preescolar explicado |
| [08 · Análisis del formato v2](08-analisis-formato-v2.md) | Ambos equipos | Qué cambia un curso recibido en formato nuevo, comprobado con la herramienta |
| [09 · Posibles cambios](09-posibles-cambios-conexion-lms.md) | Ambos equipos | Cómo afecta el formato v2 a la conexión, con foco en las capacidades |
| [10 · Instalador](10-instalador.md) | Quien distribuye | Especificación del instalador, ya implementado |
| [11 · Integración con el LMS v03](11-spec-driven-integracion-lms-v03.md) | Histórico | La especificación original de la integración. Parcialmente superada; su cabecera dice qué sigue vigente |

---

## 8 · Estado y pendientes

Lo que hoy funciona y está fijado por pruebas: el descubrimiento, la salud con
capacidades, el catálogo, la taxonomía, los cursos con sus secciones, el
elemento suelto, mostrar en pantalla, y las cinco capacidades opcionales.

Lo que el LMS espera y la Biblioteca todavía no publica está en
[09-posibles-cambios-conexion-lms.md](09-posibles-cambios-conexion-lms.md). Lo
más urgente de esa lista, en orden:

1. Decidir dónde vive la evaluación en el formato v2.
2. Exigir códigos estables en los nodos de lección del índice.
3. Publicar en la salud contadores de evaluaciones y bancos, para que la
   capacidad describa también el contenido y no solo el componente.
4. Completar el árbol del curso: tres niveles, padre de cada nodo y orden real.
5. Guardar y publicar las opciones de cada pregunta.
