# AVACOM · SPEC-DRIVEN-DEV
## Integración del LMS (`prototype-lms-v03`) con `AVACOM-Contenido`

> **Para quién es este documento.** Para el agente de IA que va a escribir el
> código de integración en `prototype-lms-v03`, y para el que va a añadir los
> puntos de enlace que faltan en `AVACOM_CONTENIDO_VERSION02`. No hace falta
> haber leído el código de ninguna de las dos aplicaciones antes de empezar:
> todo lo que se necesita saber está aquí o referenciado con archivo y símbolo.

> **Carpeta raíz de este proyecto:** `AVACOM_CONTENIDO_VERSION02`
> Todas las rutas de este documento son relativas a esa carpeta salvo que se
> diga lo contrario.

---

### Cómo se lee este documento

El desarrollo dirigido por especificación invierte el orden habitual: en vez de
escribir código y documentarlo después, se escribe la intención primero y el
código se deriva de ella. La especificación deja de ser un documento muerto y
pasa a ser la fuente que gobierna la implementación.

Seis fases, cada una con su comando:

| # | Fase | Comando | Qué produce | Qué **no** hace |
|---|---|---|---|---|
| 1 | **Constitución** | `/speckit.constitution` | Reglas no negociables del proyecto | No describe ninguna función |
| 2 | **Especificación** | `/speckit.specify` | Qué se construye y qué problema resuelve | **No** habla de tecnología |
| 3 | **Clarificación** | — | Preguntas que resuelven ambigüedades | No inventa respuestas |
| 4 | **Plan** | `/speckit.plan` | Diseño técnico: arquitectura y dependencias | No parte en tareas |
| 5 | **Tareas** | `/speckit.tasks` | Lista ordenada y atómica | No escribe código |
| 6 | **Implementación** | `/speckit.implement` | El código, paso a paso | No redefine el alcance |

**Regla de tránsito entre fases:** no se avanza a la fase siguiente con una
pregunta de la fase 3 sin responder. Una ambigüedad que se cruza a la fase 4 se
convierte en una decisión de arquitectura tomada por accidente.

---

## Mapa de las piezas

Hay **cinco** procesos, y conviene tenerlos claros antes de leer nada más:
la mitad de los errores de integración vienen de confundir dos de ellos.

```
                 EQUIPO MAESTRO DEL AULA (dentro de la pantalla de 86")
 ┌──────────────────────────────────────────────────────────────────────────┐
 │                                                                          │
 │   ┌─────────────────────────┐         ┌──────────────────────────┐       │
 │   │  AVACOM OPS Master      │         │  AVACOM-Contenido        │       │
 │   │  (MAUI .NET, C#)        │         │  ESTE PROYECTO           │       │
 │   │  El frontend del        │         │  (MAUI .NET 10, C#)      │       │
 │   │  profesor               │         │  La biblioteca cifrada   │       │
 │   └───────────┬─────────────┘         └────────────┬─────────────┘       │
 │               │                                    │                     │
 │   ┌───────────┴─────────────┐   API local          │                     │
 │   │  Backend OPS Master     │◄─────────────────────┘                     │
 │   │  (API REST + WebSocket) │   127.0.0.1 : puerto efímero               │
 │   │  ÚNICO cliente del      │   cabecera X-Avacom-Ficha                  │
 │   │  componente de contenido│   solo lectura                             │
 │   └───────────┬─────────────┘                                            │
 └───────────────┼──────────────────────────────────────────────────────────┘
                 │  LAN del aula · HTTP + WebSocket
     ┌───────────┴────────────┬────────────────────┐
     ▼                        ▼                    ▼
┌──────────┐            ┌───────────┐        ┌──────────┐
│ AVACOM   │            │ AVACOM    │        │ AVACOM   │
│ Student  │            │ Student   │  ...   │ Student  │
│ (tablet) │            │ (portátil)│        │ (tablet) │
└──────────┘            └───────────┘        └──────────┘
```

**La frase que resume toda la integración:**
`AVACOM-Contenido` habla **solo** con el Backend de OPS Master, **solo** por
`127.0.0.1`, y **solo** en modo lectura. Ninguna tableta lo alcanza nunca, ni
directa ni indirectamente sin pasar por el backend.

### El aula no tiene internet

No es que «pueda quedarse sin internet»: **no lo tiene**. Toda decisión de este
documento está subordinada a eso. Nada de CDNs, nada de tipografías remotas,
nada de telemetría, nada de validar licencia contra un servidor.

---

## Qué existe HOY, verificado en el código

Antes de especificar nada, el inventario real. Esto se leyó del código fuente,
no de la documentación.

### Ya funciona y está fijado por pruebas

| Capacidad | Dónde vive | Estado |
|---|---|---|
| Descubrimiento por `enlace.json` | [PuntoDeEnlace.cs](app-biblioteca/src/Avacom.Contenido/Api/PuntoDeEnlace.cs) | Estable |
| `GET /v1/salud` | [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs) | Estable |
| `GET /v1/catalogo` + 5 filtros combinables | [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs) | Estable |
| `GET /v1/taxonomia[?padre=]` | [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs) | Estable |
| `GET /v1/elemento/{ref}` | [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs) | Estable |
| `POST /v1/mostrar` | [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs) | Estable |
| Ficha en `X-Avacom-Ficha`; 401 sin ella; comparación en tiempo constante | [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs) | Estable |
| Política del administrador aplicada al catálogo **y** al elemento suelto | [BaseDeIndice.cs](app-biblioteca/src/Avacom.Contenido/Indice/BaseDeIndice.cs) | Estable |
| Servidor de medios cifrado, servido por rangos | [ServidorDeMedios.cs](app-biblioteca/src/Avacom.Contenido/Medios/ServidorDeMedios.cs) | Estable, **solo loopback** |
| El contrato, fijado como prueba ejecutable | [ApiLocalTests.cs](app-biblioteca/tests/Avacom.Contenido.Tests/ApiLocalTests.cs) | Estable |

### Existe en la biblioteca pero **NO** está expuesto por la API

Este es el hallazgo central de la revisión. La capacidad ya está escrita; lo
único que falta es la puerta.

| Capacidad | Firma | Archivo |
|---|---|---|
| Listar preguntas **sin** la clave de respuesta | `Preguntas(elementoRef) → IReadOnlyList<PreguntaVisible>` | [LecturaDeManifiesto.cs](app-biblioteca/src/Avacom.Contenido/Paquetes/LecturaDeManifiesto.cs) |
| Comprobar una respuesta sin revelarla, en tiempo constante | `Acierta(preguntaRef, respuesta) → bool` | [LecturaDeManifiesto.cs](app-biblioteca/src/Avacom.Contenido/Paquetes/LecturaDeManifiesto.cs) |
| Leer la secuencia de una lección | `Leccion(elementoRef) → IReadOnlyList<PasoDeLeccion>` | [LecturaDeManifiesto.cs](app-biblioteca/src/Avacom.Contenido/Paquetes/LecturaDeManifiesto.cs) |
| Instrucción hablada de un elemento o de una pregunta | `VozDeElemento(...)` y `PreguntaVisible.Voz` | [LecturaDeManifiesto.cs](app-biblioteca/src/Avacom.Contenido/Paquetes/LecturaDeManifiesto.cs) |
| Reglas de extracción de un banco | columna `reglas` (JSON) de `p_elemento` | [avacom_empaquetador.py](paquetes/avacom_empaquetador.py) |

`PreguntaVisible` **no tiene un campo donde meter la clave de respuesta**. No es
una omisión: es la garantía. La respuesta correcta solo se puede *comparar*,
nunca *leer*.

### No existe, y el LMS lo necesita

1. **No hay forma de montar un examen.** Las preguntas viven en el manifiesto
   cifrado y ningún punto de enlace las devuelve.
2. **No hay forma de calificar.** `Acierta()` está en la biblioteca; el LMS no
   puede llamarla.
3. **No hay forma de que una tableta reciba contenido.** El servidor de medios
   escucha en `127.0.0.1` a propósito y hay una prueba que lo fija.
4. **No hay señal barata de «el catálogo cambió».** Al LMS se le prohíbe cachear,
   pero no se le da con qué detectar un cambio salvo recontar en `/v1/salud`.
5. **No hay forma de leer la secuencia de una lección.** El tipo `leccion` llega
   en el catálogo; sus pasos, no.
6. **No hay registro de repaso desde el LMS.** Las tablas `m08_repaso_*` existen
   en [contenido.sql](esquema/contenido.sql) y nadie las escribe desde fuera.

### Incoherencias reales detectadas

Son de verdad, no hipotéticas, y se resuelven en la fase 3:

- **`CONTRATO-LMS.txt` lista ocho tipos**: `leccion, video, audio, imagen,
  documento, interactivo, actividad, evaluacion`.
  **El manifiesto acepta diez**: los ocho anteriores más `banco` y `scorm`
  (ver el `CHECK` de `p_elemento` en [avacom_empaquetador.py](paquetes/avacom_empaquetador.py)).
  Un LMS que haga `switch` exhaustivo sobre el tipo se rompe el día que llegue
  un `banco`.
- **SCORM está declarado sin hacer**, y el motivo es exactamente este proyecto:
  SCORM registra tiempo y calificación por su cuenta, y en AVACOM eso pertenece
  al LMS. Mientras esta integración no cierre ese punto, un curso SCORM crearía
  un expediente académico paralelo al del nodo.

---
---

# FASE 1 · CONSTITUCIÓN

```
/speckit.constitution
```

**Qué produce:** las reglas no negociables. **Qué no hace:** describir ninguna
función.

Cada artículo se escribe en imperativo y con su motivo. Un artículo sin motivo
se discute cada seis meses; un artículo con motivo se acata.

---

### Artículo 1 · El aula no tiene internet

Ninguna pieza de esta integración puede depender de una conexión externa: ni
para arrancar, ni para validar, ni para degradar con gracia. Si algo necesita
internet, no entra.

**Por qué.** No es una contingencia, es la condición del producto. Un `<script
src="https://…">` deja la pantalla en blanco delante de treinta alumnos, y sin
ningún mensaje que explique por qué.

---

### Artículo 2 · El contenido nunca sale del equipo maestro sin descifrar

`AVACOM-Contenido` escucha **exclusivamente** en `127.0.0.1`. No se le añade una
interfaz de red, no se le pone detrás de un proxy transparente, no se le
cambia el `IPAddress.Loopback` por `IPAddress.Any`. Si una tableta necesita ver
un vídeo, lo re-sirve el Backend de OPS Master bajo su propia sesión.

**Por qué.** El material está cifrado en disco y solo el componente tiene la
clave. El único sitio donde se comprueba la política antes de entregar nada es
ese componente. Abrirlo a la LAN convierte cada tableta del aula en un cliente
capaz de descargarse el catálogo entero en claro.

**Cómo se hace cumplir.** La prueba `Solo_escucha_en_el_propio_equipo` de
[ApiLocalTests.cs](app-biblioteca/tests/Avacom.Contenido.Tests/ApiLocalTests.cs)
falla si alguien lo cambia. Esa prueba no se toca.

---

### Artículo 3 · La clave de respuesta no sale del componente

Ningún punto de enlace, ningún campo, ningún registro y ningún mensaje de error
devuelve nunca `clave_respuesta`. El LMS **envía** la respuesta del alumno y
**recibe** un booleano. Nada más.

**Por qué.** Un manifiesto lleva las claves de todos los exámenes del año. En
cuanto exista un camino que las devuelva, aparecerán en un log, en un volcado de
memoria o en una pantalla de depuración. El tipo `PreguntaVisible` no tiene
dónde meterlas, y así se queda.

**Cómo se hace cumplir.** Una prueba que recorra por reflexión el DTO de
pregunta y falle si aparece cualquier propiedad cuyo nombre contenga `clave`,
`respuesta_correcta` o `solucion`.

---

### Artículo 4 · El LMS no lee la base de datos del componente

Ni `indice.db`, ni el manifiesto, ni los archivos del paquete. Solo la API.

**Por qué.** Dos motivos que se pagan a los seis meses. Primero: el LMS quedaría
atado al esquema interno del componente, y renombrar una columna aquí rompería
el LMS allí sin que ninguno de los dos equipos supiera por qué hasta que un aula
se quedara sin catálogo un lunes por la mañana. Segundo: el índice es una
proyección reconstruible; mientras se reconstruye está a medias, y un lector
externo vería un catálogo incompleto y lo creería bueno.

---

### Artículo 5 · El componente no sabe nada del LMS

`AVACOM-Contenido` no tiene alumnos, ni grupos, ni matrículas, ni
calificaciones, ni notas. No se le añaden. La única columna que roza al LMS es
`persona_id` en `m08_repaso_sesion`, y **admite nulo a propósito**.

**Por qué.** La biblioteca es abierta: alguien puede sentarse en la pantalla y
consultar material sin identificarse, y en preescolar directamente no hay con
qué identificarse. Si aquí se exigiera una persona, el componente no podría
funcionar solo, que es justo lo que tiene que poder hacer.

---

### Artículo 6 · Se guarda `ref` + `version`, nunca el título

Cuando el LMS asocie material a una unidad, guarda `elemento_ref` y `version`, y
`taxonomia_ref` para colgar de un nodo curricular. El título es texto de
pantalla y cambia entre versiones del paquete.

**Por qué.** La referencia de cada material se deriva de su título en el momento
de recolectarlo. Cambiar el título produce un material **nuevo**. Un examen de
hace seis meses que apuntara al título viejo dejaría de poder explicarse.

---

### Artículo 7 · Lo que no llega, no existe

El catálogo llega ya filtrado por la política del administrador de la escuela.
Lo desactivado no llega atenuado, ni con una marca, ni en una lista aparte:
simplemente no está. El LMS no intenta mostrarlo de otra forma, ni guarda una
copia de lo que vio ayer para enseñarlo hoy.

**Por qué.** Si el LMS pudiera verlo, acabaría enseñándolo. El único sitio donde
se decide qué puede ver un aula es la consola del administrador.

---

### Artículo 8 · El LMS no guarda un catálogo propio

Se pregunta cuando haga falta. Si el LMS cachea, que sea por minutos y con una
señal de invalidación explícita del componente.

**Por qué.** Un catálogo copiado se convierte en un segundo catálogo que hay que
mantener sincronizado a mano, y siempre acaba mintiendo.

---

### Artículo 9 · Sin contenido, el LMS sigue funcionando

Que `enlace.json` no exista es un estado **normal**, no un error. Un aula recién
montada está exactamente así. El LMS muestra «no hay contenido instalado» y
continúa: alumnos, grupos y asistencia funcionan igual.

---

### Artículo 10 · La versión del contrato se comprueba antes de hablar

El LMS lee `Contrato` de `enlace.json`. Si no es un número que entienda, se
niega a hablar y lo dice. No lo intenta «por si acaso».

**Regla de evolución.** El número sube **solo** cuando cambia la forma de una
respuesta de manera que rompa a quien ya la lee. Añadir un campo nuevo **no**
rompe a nadie y **no** sube la versión: el LMS ignora los campos que no conozca.
Añadir un punto de enlace nuevo tampoco rompe a nadie — por eso esta integración
**no sube el contrato a 2**, y en su lugar introduce descubrimiento de
capacidades (ver fase 4, decisión D-3).

---

### Artículo 11 · El componente no instala, no desinstala, no cambia políticas — y el LMS tampoco se lo pide

No hay punto de enlace para eso, y no se añade.

**Por qué.** No es desconfianza: instalar un paquete implica verificar su firma,
comprobar la licencia del equipo y proyectar el índice. Si eso se pudiera
disparar desde fuera, tarde o temprano se dispararía a mitad de una clase y el
catálogo cambiaría debajo de los pies del profesor.

---

### Artículo 12 · El repaso no genera nota

Que un alumno abra un material en su tableta deja constancia de que lo abrió y
de cuánto tiempo. Ni intento, ni calificación, ni dominio. La nota sale del
examen, y el examen la calcula el LMS.

---

### Artículo 13 · Se lee a cuatro metros y se toca con el dedo

Todo lo que se muestre en la pantalla de 86 pulgadas: nada por debajo de 20
píxeles, áreas táctiles de 64 píxeles de alto mínimo, y nada importante colgando
de `:hover`, que en táctil no existe.

---

### Artículo 14 · El texto de error es para una persona

Todo `motivo` que cruce la frontera entre las dos aplicaciones se escribe para
que el profesor lo lea tal cual en la pantalla. «Ese material no está instalado
en este equipo» sirve; «referencia nula» no.

---

### Artículo 15 · Las pruebas SON el contrato

Los nombres de campo están fijados por
[ApiLocalTests.cs](app-biblioteca/tests/Avacom.Contenido.Tests/ApiLocalTests.cs).
Si alguien los cambia, esas pruebas fallan, y el arreglo es subir la versión del
contrato — **no** tocar la prueba para que vuelva a pasar.

---
---

# FASE 2 · ESPECIFICACIÓN

```
/speckit.specify
```

**Qué produce:** qué se construye y qué problema resuelve.
**Qué NO hace:** hablar de tecnología. Aquí no aparece HTTP, ni JSON, ni C#, ni
SQLite. Si aparece, va en la fase 4.

---

## 2.1 · El problema

Un profesor entra al aula con su plan de clase. En la pantalla de 86 pulgadas
tiene abierto AVACOM OPS Master, que sabe qué grupo tiene delante, qué unidad
toca hoy y qué alumnos están conectados desde sus tabletas.

Lo que **no** sabe es qué material educativo hay disponible en ese equipo. Ese
material está en la biblioteca cifrada del aula, y hoy solo se puede recorrer
abriendo la aplicación de la biblioteca aparte, a mano, en otra ventana.

Eso obliga al profesor a salir de su clase para buscar contenido, y hace
imposible tres cosas que son el motivo de que el LMS exista:

- **Planificar.** Colgar de una unidad del curso los materiales que se van a
  usar, y que sigan colgando la semana que viene.
- **Repartir.** Que el material que el profesor elige llegue a las tabletas de
  los alumnos, no solo a la pantalla grande.
- **Evaluar.** Montar un examen con las preguntas que el contenido ya trae, y
  calificarlo sin que el profesor tenga que teclear la respuesta correcta.

---

## 2.2 · Qué se construye

Un puente entre las dos aplicaciones que permita al LMS:

**A · Descubrir el contenido**
Saber si hay biblioteca en este equipo, recorrer la estructura curricular tal y
como la define el contenido, y listar el material disponible filtrando por
nivel, grado, asignatura, tipo y nodo curricular.

**B · Planificar con él**
Anclar material a las unidades del curso del LMS de forma que el vínculo
sobreviva a una actualización del paquete de contenido y a un cambio de título.

**C · Proyectarlo en la pantalla del aula**
Pedir que un material aparezca en la pantalla de 86 pulgadas, y saber si la
petición se aceptó o por qué no.

**D · Repartirlo a las tabletas**
Que un material que el profesor elige llegue a las tabletas de los alumnos
conectados, sin que el material salga descifrado del equipo maestro y sin que
las tabletas alcancen nunca la biblioteca.

**E · Evaluar con él**
Leer las preguntas de una evaluación o extraer reactivos de un banco, mostrarlas
al alumno, y comprobar sus respuestas sin que la respuesta correcta llegue nunca
al LMS ni a la tableta.

**F · Dejar constancia del repaso**
Registrar que un alumno abrió un material y cuánto tiempo, sin que eso genere
nota.

---

## 2.3 · Qué NO se construye

Decirlo explícitamente ahorra la mitad de las discusiones de la fase 6.

| No se construye | Por qué |
|---|---|
| Un editor de contenido dentro del LMS | El contenido se produce con el estándar de carpetas y el recolector. Ver [ESTANDAR-CONTENIDO.txt](ESTANDAR-CONTENIDO.txt) |
| Instalación o desinstalación de paquetes desde el LMS | Artículo 11 |
| Cambio de políticas desde el LMS | Artículo 11 |
| Un segundo catálogo dentro del LMS | Artículo 8 |
| Acceso directo de las tabletas a la biblioteca | Artículo 2 |
| Sincronización con un servidor central | Artículo 1 |
| Soporte SCORM | Declarado pendiente. Ver pregunta C-9 |
| Calificación de preguntas abiertas | El contenido trae la rúbrica; la nota la pone una persona |

---

## 2.4 · Historias de usuario

Cada una con su criterio de aceptación observable. «Observable» significa que se
puede comprobar mirando la pantalla o el registro, sin leer código.

---

**HU-01 · El aula sin biblioteca**
*Como* profesor en un aula recién montada,
*quiero* que el LMS funcione aunque no haya contenido instalado,
*para* poder pasar lista y organizar grupos desde el primer día.

- **CA-01.1** Con el componente de contenido apagado, el LMS arranca sin errores.
- **CA-01.2** La sección de contenido muestra un mensaje legible: no hay
  biblioteca instalada en este equipo.
- **CA-01.3** El resto del LMS (alumnos, grupos, asistencia) funciona igual.
- **CA-01.4** Si el componente arranca después, el LMS lo detecta sin reiniciarse.

---

**HU-02 · Versión de contrato desconocida**
*Como* responsable técnico,
*quiero* que el LMS se niegue a hablar con una versión de contrato que no
entiende,
*para* que un fallo de despliegue se vea el primer día y no en forma de datos
raros el tercer mes.

- **CA-02.1** Con un contrato mayor que el soportado, el LMS no llama a ningún
  punto de enlace.
- **CA-02.2** Muestra un mensaje que nombra las dos versiones: la que espera y la
  que encontró.
- **CA-02.3** El resto del LMS sigue funcionando (equivale a «no hay contenido»).

---

**HU-03 · Recorrer el currículo**
*Como* profesor,
*quiero* recorrer el árbol curricular tal y como lo define el contenido,
*para* encontrar material por el mismo camino que uso al planear.

- **CA-03.1** El LMS muestra los nodos raíz sin saber de antemano cuántos hay.
- **CA-03.2** Al abrir un nodo, muestra sus hijos; se puede bajar hasta que un
  nodo no tenga hijos, **sin límite fijo de profundidad**.
- **CA-03.3** La etiqueta de tipo de cada nodo (`area`, `pensamiento`,
  `proposito`, `factor`, `estandar`…) se muestra tal cual llega, y **no se usa
  para decidir nada**.
- **CA-03.4** Un currículo de preescolar (`proposito → actividad_rectora →
  experiencia → aprendizaje`, sin asignatura) y uno de secundaria (`area →
  pensamiento → estandar → tema`) se recorren con el mismo código, sin ramas.
- **CA-03.5** Un nodo sin `asignatura` no rompe la pantalla ni muestra hueco.

---

**HU-04 · Buscar material**
*Como* profesor de grado 8,
*quiero* filtrar el material por nivel, grado, asignatura, tipo y nodo,
*para* llegar a lo que sirve hoy sin recorrer todo el catálogo.

- **CA-04.1** Los cinco filtros son opcionales y se combinan entre sí.
- **CA-04.2** Sin filtros llega todo lo disponible.
- **CA-04.3** Un filtro que no casa con nada devuelve una lista vacía, no un error.
- **CA-04.4** Cada resultado trae al menos: referencia, tipo, título, nivel,
  grado, asignatura, idioma, nodo curricular, versión y duración cuando aplique.
- **CA-04.5** Un tipo que el LMS no conoce se muestra con un icono genérico y su
  nombre literal. **No se descarta y no lanza excepción.**

---

**HU-05 · Anclar material a una unidad**
*Como* profesor que planea la semana,
*quiero* colgar materiales de una unidad de mi curso,
*para* que sigan ahí cuando vuelva.

- **CA-05.1** El LMS guarda referencia + versión + nodo curricular. **No guarda
  el título.**
- **CA-05.2** Al abrir la unidad, el título se pide de nuevo y se muestra el actual.
- **CA-05.3** Si la referencia ya no está instalada, la unidad lo indica sin
  romperse y ofrece quitar el ancla.
- **CA-05.4** Si la política de la escuela desactivó ese material, la unidad lo
  indica con el mismo tratamiento que el caso anterior. **No se ofrece ninguna
  vía alternativa para abrirlo.**

---

**HU-06 · Proyectar en la pantalla del aula**
*Como* profesor,
*quiero* mandar un material a la pantalla de 86 pulgadas desde el LMS,
*para* no cambiar de aplicación delante de la clase.

- **CA-06.1** La respuesta llega en menos de un segundo, **incluso para un vídeo
  de 300 MB**: se responde al aceptar la petición, no al terminar de cargar.
- **CA-06.2** Si no se puede, el motivo es un texto que el profesor puede leer
  tal cual.
- **CA-06.3** Un material desactivado por política da el mismo resultado que uno
  no instalado desde el punto de vista del profesor.

---

**HU-07 · Repartir a las tabletas**
*Como* profesor,
*quiero* que el material que elijo llegue a las tabletas de mis alumnos,
*para* que trabajen sobre él a la vez.

- **CA-07.1** El material llega a las tabletas de los alumnos conectados del
  grupo, y **solo** a esas.
- **CA-07.2** Una tableta que se conecta después recibe lo que está activo en ese
  momento.
- **CA-07.3** El material **no** queda accesible en la tableta cuando el profesor
  lo retira ni cuando la clase termina.
- **CA-07.4** Ninguna tableta puede pedir contenido que el profesor no haya
  repartido, ni adivinando referencias ni manipulando direcciones.
- **CA-07.5** Con treinta tabletas pidiendo el mismo vídeo, la reproducción es
  fluida en todas.
- **CA-07.6** En ningún momento existe una copia descifrada del material en el
  disco de ninguna de las máquinas.

---

**HU-08 · Montar un examen**
*Como* profesor,
*quiero* montar un examen a partir de una evaluación o de un banco de preguntas
del contenido,
*para* no volver a escribir preguntas que ya están hechas.

- **CA-08.1** El LMS lista las preguntas de una evaluación con enunciado, tipo,
  peso y dificultad.
- **CA-08.2** De un banco, el LMS obtiene una selección que respeta las reglas de
  extracción del propio banco (cuántas y con qué mezcla de dificultad).
- **CA-08.3** Dos alumnos que hacen el examen del mismo banco reciben
  selecciones distintas.
- **CA-08.4** **En ningún punto llega la respuesta correcta al LMS.**
- **CA-08.5** Las preguntas abiertas llegan con su rúbrica; si una evaluación
  tiene preguntas abiertas y no trae rúbrica, el LMS lo avisa al profesor.

---

**HU-09 · Calificar**
*Como* profesor,
*quiero* que el examen se califique solo en lo que sea autocalificable,
*para* dedicar el tiempo a las respuestas abiertas.

- **CA-09.1** El LMS envía la respuesta del alumno y recibe acierto o fallo.
- **CA-09.2** La comprobación es indiferente a mayúsculas y a espacios sobrantes.
- **CA-09.3** El tiempo de respuesta **no** depende de cuántos caracteres
  acertó: no se puede sacar la respuesta letra a letra midiendo el reloj.
- **CA-09.4** Las preguntas abiertas quedan marcadas como pendientes de una
  persona, con su rúbrica a la vista.
- **CA-09.5** La nota la calcula y la guarda el LMS. El componente de contenido
  **no** almacena ninguna calificación.

---

**HU-10 · Repaso del alumno**
*Como* coordinador académico,
*quiero* saber qué material se ha consultado en modo repaso,
*para* medir uso sin convertirlo en calificación.

- **CA-10.1** Se registra qué se abrió, cuándo y cuánto tiempo.
- **CA-10.2** **No** se genera intento, ni calificación, ni dominio.
- **CA-10.3** Un alumno no identificado también puede repasar; el registro
  admite no saber quién es.

---

**HU-11 · El catálogo cambia a mitad de jornada**
*Como* profesor,
*quiero* que el LMS refleje una instalación o una desactivación reciente,
*para* no ofrecer a la clase algo que ya no está.

- **CA-11.1** El LMS detecta el cambio en menos de un minuto sin recargar el
  catálogo entero cada vez.
- **CA-11.2** Cualquier lista de contenido que el profesor tenga abierta se
  marca como desactualizada y se puede refrescar.
- **CA-11.3** Un material repartido a las tabletas que deja de estar disponible
  se retira de las tabletas.

---

## 2.5 · Reglas de negocio

| # | Regla |
|---|---|
| RN-01 | El identificador estable de un material es `elemento_ref`. El título es texto de pantalla. |
| RN-02 | El identificador estable de un nodo curricular es `taxonomia_ref`. |
| RN-03 | La profundidad del árbol curricular es desconocida y variable. Se recorre, no se asume. |
| RN-04 | `asignatura` puede estar vacía (preescolar). |
| RN-05 | El tipo de nodo es una etiqueta de pantalla, nunca una condición. |
| RN-06 | El conjunto de tipos de elemento es **abierto**: el LMS tolera valores que no conoce. |
| RN-07 | Un `banco` **no** se «da» en clase: no entra en la secuencia de una lección; es de donde el examen extrae. |
| RN-08 | Una `evaluacion` se da entera y siempre con las mismas preguntas. |
| RN-09 | Las preguntas de comprobación dentro de una lección **no son nota**. |
| RN-10 | La regla práctica de un banco: al menos el doble de preguntas de las que extrae el examen. |

---
---

# FASE 3 · CLARIFICACIÓN

**Qué produce:** las preguntas que resuelven ambigüedades.
**Qué NO hace:** inventar respuestas.

Este es el documento vivo de la integración. Cada pregunta tiene un
**bloqueante**: qué no se puede empezar hasta responderla. Las marcadas
`[PROPUESTA]` traen una recomendación razonada; las marcadas `[ABIERTA]` no
tienen respuesta por defecto y **hay que preguntar**.

---

### C-1 · ¿Cómo llega el contenido a las tabletas? `[PROPUESTA]`

**El conflicto.** HU-07 pide que el material llegue a las tabletas. El artículo 2
prohíbe que la biblioteca escuche fuera de `127.0.0.1`.

**Propuesta: retransmisión por el Backend de OPS Master.**
El Backend pide el material al componente por loopback y lo re-sirve a las
tabletas bajo su propia sesión, con su propia autorización por alumno y grupo.
El componente sigue siendo loopback puro y no se entera de que existen tabletas.

Consecuencias que hay que aceptar antes de decir que sí:

- El Backend necesita **obtener bytes**, y hoy `/v1/mostrar` solo proyecta en la
  pantalla local. Hace falta un punto de enlace nuevo (ver fase 4, D-2).
- El Backend se convierte en el lugar donde se decide qué alumno puede ver qué.
  Esa responsabilidad hay que escribirla, no heredarla por accidente.
- Treinta tabletas pidiendo el mismo vídeo pasan por un solo proceso. Hay que
  medirlo (ver T-27).

**Alternativa descartada:** abrir el servidor de medios a la LAN. Rompe el
artículo 2 y hace fallar una prueba existente.

**Bloquea:** HU-07 entera, D-2, T-14 a T-18.

---

### C-2 · ¿Qué pasa con los tipos `banco` y `scorm`? `[PROPUESTA]`

**El conflicto.** `CONTRATO-LMS.txt` documenta ocho tipos; el manifiesto acepta
diez. `banco` y `scorm` pueden llegar hoy al catálogo y no están documentados.

**Propuesta.**
1. Corregir `CONTRATO-LMS.txt` para listar los diez tipos reales. Es una
   corrección de documentación, **no** sube la versión del contrato.
2. Fijar RN-06 con una prueba: el LMS recibe un tipo inventado y lo muestra sin
   romperse.
3. `banco` se **excluye** de las listas de «material para dar en clase» y
   aparece solo en el montador de exámenes.

**Bloquea:** CA-04.5, T-08.

---

### C-3 · ¿Un banco se extrae en el componente o en el LMS? `[PROPUESTA]`

**El conflicto.** CA-08.2 pide respetar las reglas de extracción del banco, que
viven en la columna `reglas` del manifiesto cifrado. CA-08.3 pide que dos
alumnos vean selecciones distintas.

**Propuesta: la extracción la hace el componente.**
El LMS pide «dame una extracción de este banco» y recibe una selección ya hecha
que cumple las reglas. El LMS guarda **qué preguntas le tocaron a qué alumno**,
porque eso es expediente académico y pertenece al LMS.

**Por qué así.** Si extrajera el LMS, tendría que leer las reglas y el repertorio
completo de preguntas para elegir. Eso significa sacar del componente **todas**
las preguntas del banco cada vez que se monta un examen, incluidas las que no se
usan. Extraer dentro reduce lo que cruza la frontera a lo mínimo.

**Bloquea:** HU-08, D-2, T-11.

---

### C-4 · ¿Con qué se detecta que el catálogo cambió? `[PROPUESTA]`

**El conflicto.** El artículo 8 prohíbe cachear; CA-11.1 pide detectar cambios en
menos de un minuto sin recargar todo.

**Propuesta: un contador de generación.**
`/v1/salud` devuelve un entero que sube cada vez que cambia algo que afecte al
catálogo: instalar, desinstalar, cambiar una política, reconstruir el índice. El
LMS consulta salud cada 30 segundos — es una respuesta de doscientos bytes — y
solo recarga cuando el número cambia.

**Por qué no comparar los conteos actuales.** `elementos` y `paquetes` ya están
en salud, pero desactivar diez elementos y activar otros diez deja el conteo
igual. Un contador monótono no tiene ese problema.

**Bloquea:** HU-11, D-3, T-09.

---

### C-5 · ¿Qué pasa si el componente se reinicia a mitad de clase? `[ABIERTA]`

El puerto y la ficha cambian en cada arranque, y al cerrarse el componente borra
`enlace.json`.

**Lo que hay que decidir:**
- ¿El LMS revalida `enlace.json` en cada petición, cada N segundos, o solo al
  recibir un fallo de conexión?
- Un material repartido a las tabletas cuando el componente cae: ¿se retira de
  las tabletas, se congela con un aviso, o se deja terminar?
- ¿Hay que avisar al profesor, o se reconecta en silencio?

**Recomendación de partida** (no es respuesta, es punto de arranque): revalidar
al fallar, más una comprobación periódica ligada al sondeo de C-4; y avisar al
profesor solo si la reconexión no ocurre en diez segundos.

**Bloquea:** T-05, T-19.

---

### C-6 · ¿Quién autoriza a un alumno concreto a ver un material concreto? `[ABIERTA]`

El componente decide qué está disponible **en este equipo** (política de la
escuela). No sabe nada de alumnos ni de grupos.

**Lo que hay que decidir, y es del LMS entero, no solo de esta integración:**
- ¿Un alumno de otro grupo conectado a la misma LAN puede recibir el reparto?
- ¿La autorización es por grupo, por sesión de clase, o por alumno?
- ¿Cuánto dura? ¿Termina con la clase, con la sesión, o con un temporizador?

**Bloquea:** HU-07, CA-07.4, T-16.

---

### C-7 · ¿Qué se registra en el componente y qué en el LMS? `[PROPUESTA]`

Las tablas `m08_repaso_sesion` y `m08_repaso_consumo` existen y nadie las
escribe desde fuera.

**Propuesta.**
- **En el componente:** que se abrió una referencia, cuándo, cuánto tiempo, y
  opcionalmente quién (`persona_id` admite nulo). Sirve para explicar qué se
  mostró en una clase aunque el paquete se desinstale después.
- **En el LMS:** todo lo académico. Intentos, respuestas, notas, progreso de la
  unidad.
- **Nada se duplica.** Si un dato está en los dos sitios, uno de los dos miente
  al cabo de un mes.

**Bloquea:** HU-10, D-2, T-13.

---

### C-8 · ¿Qué es exactamente una `leccion` para el LMS? `[PROPUESTA]`

Hay dos cosas distintas con el mismo nombre, y confundirlas cuesta caro:

1. **`leccion` como secuencia** — el elemento de tipo `leccion` del manifiesto,
   con una lista ordenada de `item_ref`. Es «el orden en que se da el tema».
   Se lee con `LecturaDeManifiesto.Leccion()`.
2. **La lección generada en HTML** — lo que produce
   [avacom_leccion.py](paquetes/avacom_leccion.py) a partir de un `guion.txt`:
   una página autónoma con barra de progreso, índice, medios integrados y quiz.
   Se empaqueta como un **interactivo**, es decir, una carpeta con `index.html`.

**Propuesta.** El LMS trata (1) como un plan de clase que puede recorrer paso a
paso, y (2) como cualquier otro interactivo. Los ganchos `window.avacom.progreso`
y `window.avacom.terminado` que la página HTML ya emite (ver el ejemplo de
lección adjunto) son la vía por la que el progreso llega al LMS, y van dentro de
un `if` a propósito: la página tiene que funcionar igual abierta suelta en un
navegador para probarla.

**Bloquea:** D-2, T-12, T-20.

---

### C-9 · ¿Entra SCORM en este alcance? `[ABIERTA]`

SCORM está declarado sin hacer, y el motivo es este proyecto: SCORM registra
tiempo y calificación por su cuenta, y en AVACOM eso pertenece al LMS. Hasta que
ese punto se cierre, un curso SCORM crearía un expediente académico paralelo.

**Lo que hay que decidir:** ¿el LMS absorbe el registro de SCORM en esta
entrega, o `scorm` se queda fuera del catálogo que ve el LMS hasta una entrega
posterior?

**Recomendación:** dejarlo fuera. El alcance ya es grande y SCORM arrastra un
modelo de datos propio.

**Bloquea:** nada crítico si se decide dejarlo fuera. Bloquea T-08 si entra.

---

### C-10 · ¿El LMS y el componente comparten identidad de máquina? `[ABIERTA]`

La licencia se emite para un equipo concreto, y sin la clave privada de ese
equipo los medios son ruido.

**Lo que hay que decidir:** ¿el LMS necesita saber para qué equipo está emitida
la licencia, o le basta con que el contenido llegue o no llegue? Afecta a qué se
le puede explicar al profesor cuando algo no abre.

**Bloquea:** CA-06.2 en su caso más raro.

---

### Resumen de bloqueos

| Pregunta | Estado | Bloquea |
|---|---|---|
| C-1 Reparto a tabletas | `[PROPUESTA]` | HU-07, D-2, T-14…T-18 |
| C-2 Tipos `banco` / `scorm` | `[PROPUESTA]` | CA-04.5, T-08 |
| C-3 Dónde se extrae el banco | `[PROPUESTA]` | HU-08, D-2, T-11 |
| C-4 Señal de cambio | `[PROPUESTA]` | HU-11, D-3, T-09 |
| C-5 Reinicio del componente | **`[ABIERTA]`** | T-05, T-19 |
| C-6 Autorización por alumno | **`[ABIERTA]`** | HU-07, T-16 |
| C-7 Reparto del registro | `[PROPUESTA]` | HU-10, T-13 |
| C-8 Qué es una lección | `[PROPUESTA]` | T-12, T-20 |
| C-9 SCORM | **`[ABIERTA]`** | T-08 si entra |
| C-10 Identidad de máquina | **`[ABIERTA]`** | CA-06.2 |

**Las cuatro `[ABIERTA]` hay que preguntarlas antes de la fase 4.** Las
`[PROPUESTA]` se pueden dar por buenas si nadie objeta, y la fase 4 las asume
como decididas.

---
---

# FASE 4 · PLAN

```
/speckit.plan
```

**Qué produce:** diseño técnico, arquitectura y dependencias.
**Qué NO hace:** partir en tareas. Eso es la fase 5.

Esta fase asume decididas las `[PROPUESTA]` de la fase 3.

---

## 4.1 · Decisiones de arquitectura

### D-1 · Tres capas, y la del medio es la única que cruza

```
  AVACOM Student  ──┐
  AVACOM Student  ──┼─►  Backend OPS Master  ──►  AVACOM-Contenido
  AVACOM Student  ──┘         (LAN)                   (loopback)
        ▲                        │
        └──── WebSocket ─────────┘
```

- **Contenido** no sabe que existen tabletas. No se toca su modelo de red.
- **Backend OPS Master** es el único cliente del componente. Concentra la
  autorización por alumno, el reparto y la retransmisión.
- **Student** no conoce el componente. Solo habla con el Backend.

Consecuencia práctica: **todo el código de cliente del contenido vive en un solo
proyecto** del lado del LMS. Si aparece un segundo sitio que lea `enlace.json`,
está mal.

---

### D-2 · Puntos de enlace nuevos en `AVACOM-Contenido`

Se añaden a [ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs).
Los cinco existentes **no se tocan**.

| Método y ruta | Para qué | Resuelve |
|---|---|---|
| `GET /v1/leccion/{ref}` | Pasos de una lección, en orden | C-8, HU-08 |
| `GET /v1/evaluacion/{ref}` | Preguntas visibles + rúbrica | HU-08 |
| `POST /v1/banco/{ref}/extraer` | Selección que cumple las reglas del banco | C-3 |
| `POST /v1/comprobar` | `{pregunta_ref, respuesta}` → `{acierta: bool}` | HU-09 |
| `GET /v1/medio/{ref}` | Abre una sesión de bytes para retransmitir | C-1 |
| `POST /v1/repaso` | Registra apertura y tiempo | C-7 |

**Reglas que gobiernan estos seis:**

1. Ninguno devuelve `clave_respuesta`. La respuesta de `/v1/comprobar` es un
   booleano y el `retroalimentacion` que la pregunta ya trae. Nada más.
2. Todos aplican la política **antes** de responder, igual que hace hoy
   `/v1/elemento/{ref}`. Un `403` para lo desactivado, un `404` para lo que no
   está.
3. Todos exigen `X-Avacom-Ficha`. Sin ella, `401` y nada más.
4. Todos escuchan en `127.0.0.1`. Ninguno abre una interfaz nueva.
5. `POST /v1/comprobar` y `POST /v1/repaso` son los dos únicos que reciben datos.
   `/v1/comprobar` **no escribe nada**: compara y contesta. `/v1/repaso` escribe
   solo en `m08_repaso_*`.

**Sobre `GET /v1/medio/{ref}`.** Es el punto delicado. Devuelve una dirección de
un solo uso del servidor de medios existente
([ServidorDeMedios.cs](app-biblioteca/src/Avacom.Contenido/Medios/ServidorDeMedios.cs)),
que ya sabe descifrar por bloques y servir por rangos. El Backend consume esa
dirección **por loopback** y retransmite. La dirección se anula al cerrar la
sesión. No se añade capacidad de red nueva: se reutiliza la que ya existe.

---

### D-3 · Descubrimiento de capacidades, en vez de subir el contrato

`/v1/salud` gana dos campos:

```json
{
  "componente": "avacom-contenido",
  "contrato": 1,
  "generacion": 47,
  "capacidades": ["leccion", "evaluacion", "banco", "comprobar", "medio", "repaso"],
  "elementos": 10,
  "paquetes": 2,
  "politicas": 0
}
```

- `generacion` — entero monótono, sube con cada cambio que afecte al catálogo.
  Resuelve C-4.
- `capacidades` — qué puntos de enlace nuevos entiende esta versión.

**Por qué no subir el contrato a 2.** El artículo 10 lo dice: el número sube
cuando cambia la forma de una respuesta de manera que **rompa a quien ya la
lee**. Añadir campos y añadir puntos de enlace no rompe a nadie. Subir el número
obligaría a desplegar los dos lados a la vez en todas las aulas, que es
exactamente lo que el contrato está diseñado para evitar.

**Cómo lo usa el LMS.** Comprueba `contrato`, y luego consulta `capacidades`
antes de usar cualquier punto nuevo. Un componente viejo sin `capacidades`
equivale a la lista vacía: el LMS muestra catálogo y proyección, y esconde los
exámenes. Degrada, no se rompe.

---

### D-4 · Componentes nuevos del lado del LMS

```
prototype-lms-v03/
  Avacom.Ops.Backend/
    Contenido/
      DescubridorDeContenido        lee enlace.json, valida contrato, reconecta
      ClienteDeContenido            el único que llama a la API. Tipado.
      ObservadorDeGeneracion        sondea /v1/salud, emite "el catálogo cambió"
      RetransmisorDeMedios          loopback → LAN, con autorización
      RepartoDeClase                qué está activo, para qué grupo, ahora
    Examenes/
      MontadorDeExamen              evaluación o banco → examen del LMS
      CalificadorAutomatico         respuesta → /v1/comprobar → acierto
```

**`ClienteDeContenido` es el único que conoce HTTP.** Si aparece un `HttpClient`
en otro sitio del LMS apuntando a `127.0.0.1`, está mal. Concentrarlo permite
que la política de reintentos, la revalidación de ficha y el manejo de «no hay
contenido» se escriban una sola vez.

---

### D-5 · Modelo de datos del lado del LMS

Solo lo que la integración añade. El resto del LMS no se toca.

```
unidad_material              qué material cuelga de qué unidad
  unidad_id                  FK a la unidad del LMS
  elemento_ref               TEXT   ← estable
  version_elemento           TEXT   ← estable
  taxonomia_ref              TEXT   ← estable
  orden                      INTEGER
  -- NO hay columna titulo. Artículo 6.

examen_pregunta              qué preguntas le tocaron a qué alumno
  examen_id
  persona_id
  pregunta_ref               TEXT
  elemento_ref               TEXT   ← de qué evaluación o banco salió
  orden                      INTEGER
  -- NO hay columna clave_respuesta. Artículo 3.

reparto_activo               qué está repartido ahora mismo
  sesion_clase_id
  elemento_ref
  grupo_id
  abierto_en / cerrado_en
```

**La ausencia de columnas es parte del diseño.** Un `titulo` en
`unidad_material` parece cómodo y convierte el LMS en un catálogo paralelo que
miente en cuanto el paquete se actualice. Una `clave_respuesta` en
`examen_pregunta` es la fuga que el artículo 3 existe para evitar.

---

### D-6 · Protocolo con las tabletas

- **Control** — WebSocket. El Backend empuja «se repartió esto», «se retiró
  aquello», «el catálogo cambió».
- **Bytes** — HTTP con soporte de rangos. Un vídeo se pide por trozos, igual que
  hace hoy el reproductor de la pantalla grande.
- **Autorización** — cada tableta lleva un vale de sesión emitido por el
  Backend, ligado a alumno y grupo. Una dirección de medio **no** es adivinable
  y **no** funciona sin el vale. Depende de C-6.

---

### D-7 · Qué se rompe si se hace mal

| Atajo tentador | Qué pasa a los seis meses |
|---|---|
| Abrir el servidor de medios a la LAN | Cualquier tableta se descarga el catálogo en claro. Falla `Solo_escucha_en_el_propio_equipo`. |
| Guardar el título en `unidad_material` | El catálogo del LMS y el real divergen tras la primera actualización. |
| Cachear el catálogo «para que vaya rápido» | El LMS ofrece material que la escuela desactivó. |
| Devolver `clave_respuesta` «solo para depurar» | Aparece en un log, y de ahí a un correo. |
| Hacer `switch` exhaustivo sobre `tipo` | Excepción el día que llegue un `banco`. |
| Asumir tres niveles de taxonomía | Preescolar de Colombia tiene cuatro y no tiene asignatura. |
| Leer `indice.db` directamente | Catálogo a medias durante una reconstrucción. |

---

## 4.2 · Dependencias

**No se añade ninguna dependencia externa a `AVACOM-Contenido`.** Los seis
puntos de enlace nuevos se escriben con lo que ya hay: el `TcpListener` de
`ApiLocal`, `LecturaDeManifiesto`, `BaseDeIndice` y `ServidorDeMedios`. El
artículo 1 lo exige y el componente ya está diseñado así: cuanto menos código
escuche en un puerto, menos superficie hay que revisar.

Del lado del LMS, cualquier dependencia tiene que poder resolverse sin red en el
aula. Eso significa restaurada en tiempo de compilación y empaquetada con la
aplicación.

---

## 4.3 · Orden de construcción

Cada etapa deja algo demostrable. No se pasa a la siguiente sin la anterior en
verde.

| Etapa | Qué deja | Depende de |
|---|---|---|
| **E1 · Enlace** | El LMS encuentra el componente, valida contrato, degrada sin él | C-5 |
| **E2 · Catálogo** | Recorrer taxonomía y listar material en el LMS | E1 |
| **E3 · Planificación** | Anclar material a unidades | E2 |
| **E4 · Proyección** | Mandar a la pantalla de 86" | E2 |
| **E5 · Generación** | Detectar cambios del catálogo | E1, D-3 |
| **E6 · Exámenes** | Montar y calificar | E2, C-3 |
| **E7 · Reparto** | El material llega a las tabletas | E2, C-1, C-6 |
| **E8 · Repaso** | Registro sin nota | E7, C-7 |

**E7 es la etapa cara.** Se deja para el final a propósito: E1 a E6 entregan un
LMS que ya sirve a un profesor con la pantalla grande, y eso es un producto
utilizable aunque E7 se retrase.

---
---

# FASE 5 · TAREAS

```
/speckit.tasks
```

**Qué produce:** lista ordenada y atómica.
**Qué NO hace:** escribir código.

Cada tarea es **atómica** (una sola cosa), **verificable** (con criterio de
hecho observable) y **trazable** (dice qué historia o decisión la origina).

Leyenda de lado: **[C]** en `AVACOM_CONTENIDO_VERSION02` · **[L]** en
`prototype-lms-v03` · **[A]** en ambos.

---

## E1 · Enlace

**T-01** `[L]` Modelo tipado de `enlace.json` con los cuatro campos:
`Contrato`, `Puerto`, `Ficha`, `Proceso`.
*Hecho cuando:* se deserializa el archivo real que escribe
[PuntoDeEnlace.cs](app-biblioteca/src/Avacom.Contenido/Api/PuntoDeEnlace.cs).
*Origen:* HU-01.

**T-02** `[L]` `DescubridorDeContenido`: leer el archivo de
`%ProgramData%\AVACOM\contenido\enlace.json`, devolver «no hay» cuando falte.
*Hecho cuando:* con el archivo borrado devuelve ausencia y **no** lanza
excepción.
*Origen:* CA-01.1, artículo 9.

**T-03** `[L]` Validar `Contrato` contra el conjunto de versiones soportadas.
*Hecho cuando:* con `Contrato: 99` no se emite ninguna petición y el mensaje
nombra las dos versiones.
*Origen:* HU-02, artículo 10.

**T-04** `[L]` `ClienteDeContenido`: `HttpClient` con `BaseAddress` en
`http://127.0.0.1:{Puerto}` y `X-Avacom-Ficha` por omisión.
*Hecho cuando:* `GET /v1/salud` responde 200 contra un componente vivo, y 401 si
se quita la cabecera.
*Origen:* D-4.

**T-05** `[L]` Revalidación del enlace al fallar la conexión, con reintento.
*Hecho cuando:* se reinicia el componente (puerto y ficha nuevos) y la siguiente
llamada del LMS funciona sin reiniciar el LMS.
*Origen:* CA-01.4. **Bloqueada por C-5.**

**T-06** `[L]` Estado «sin biblioteca» en la interfaz del LMS.
*Hecho cuando:* CA-01.2 y CA-01.3 se comprueban a ojo con el componente apagado.
*Origen:* HU-01.

---

## E2 · Catálogo

**T-07** `[L]` DTOs de `elemento` y `nodo` con **exactamente** los nombres del
contrato: `ref, tipo, titulo, nivel, grado, asignatura, idioma, taxonomia_ref,
version, duracion_seg, paquete, huella` y `ref, padre, tipo, codigo, nombre,
orden, pais, nivel`.
*Hecho cuando:* una prueba deserializa las respuestas de ejemplo de
[CONTRATO-LMS.txt](CONTRATO-LMS.txt) sin perder ningún campo, y campos
desconocidos se ignoran sin error.
*Origen:* artículo 15.

**T-08** `[L]` Tolerancia a tipos desconocidos: icono genérico y nombre literal.
*Hecho cuando:* un elemento con `tipo: "inventado"` se lista sin excepción, y un
`banco` no aparece en «material para dar en clase».
*Origen:* CA-04.5, RN-06, RN-07. **Depende de C-2.**

**T-09** `[C]` Añadir `generacion` y `capacidades` a `/v1/salud`.
*Hecho cuando:* `generacion` sube al instalar, al desinstalar, al cambiar una
política y al reconstruir el índice; y no sube al consultar el catálogo.
*Origen:* D-3, C-4.

**T-10** `[L]` Recorrido de la taxonomía sin profundidad fija.
*Hecho cuando:* se recorre un árbol de secundaria (4 niveles) y uno de
preescolar (4 niveles, sin asignatura) **con el mismo código y sin ramas por
`tipo`**.
*Origen:* CA-03.4, RN-03, RN-05.

**T-11** `[L]` Pantalla de catálogo con los cinco filtros combinables.
*Hecho cuando:* las nueve combinaciones de
[ApiLocalTests.cs](app-biblioteca/tests/Avacom.Contenido.Tests/ApiLocalTests.cs)
(`Los_filtros_funcionan_y_se_combinan`) dan los mismos conteos desde el LMS.
*Origen:* HU-04.

**T-12** `[C]` `GET /v1/leccion/{ref}` sobre `LecturaDeManifiesto.Leccion()`.
*Hecho cuando:* devuelve los pasos en orden con `item_ref`, `nota`, `titulo` y
`tipo`; `403` si la política lo tapa; `404` si no está.
*Origen:* C-8, D-2.

---

## E3 · Planificación

**T-13** `[L]` Tabla `unidad_material` **sin columna de título**.
*Hecho cuando:* una revisión del esquema confirma que no existe, y el título se
resuelve en cada pintado.
*Origen:* CA-05.1, artículo 6, D-5.

**T-14** `[L]` Anclar y desanclar material en una unidad.
*Hecho cuando:* el ancla sobrevive a reiniciar el LMS.
*Origen:* HU-05.

**T-15** `[L]` Ancla rota: referencia desinstalada o desactivada por política.
*Hecho cuando:* CA-05.3 y CA-05.4 se comprueban desactivando una asignatura con
`Instalador.Politica(...)` y viendo que la unidad lo indica **sin ofrecer
ninguna vía alternativa de apertura**.
*Origen:* CA-05.3, CA-05.4, artículo 7.

---

## E4 · Proyección

**T-16** `[L]` Botón «mostrar en la pantalla» que llama a `POST /v1/mostrar`.
*Hecho cuando:* con un vídeo de 300 MB la interfaz responde en menos de un
segundo.
*Origen:* CA-06.1.

**T-17** `[L]` Mostrar el `motivo` del componente tal cual al profesor.
*Hecho cuando:* al pedir una referencia inexistente aparece «Ese material no
está instalado en este equipo.» literal, sin envolverlo en un error técnico.
*Origen:* CA-06.2, artículo 14.

---

## E5 · Generación

**T-18** `[L]` `ObservadorDeGeneracion`: sondeo de `/v1/salud` cada 30 s.
*Hecho cuando:* instalar un paquete se refleja en el LMS en menos de un minuto
sin recargar el catálogo entero.
*Origen:* CA-11.1, D-3.

**T-19** `[L]` Marca de «desactualizado» en las listas abiertas, con refresco.
*Hecho cuando:* CA-11.2 se comprueba con una lista abierta mientras se desactiva
una asignatura.
*Origen:* CA-11.2. **Depende de C-5** para el caso de reinicio.

---

## E6 · Exámenes

**T-20** `[C]` `GET /v1/evaluacion/{ref}`: preguntas visibles + rúbrica.
*Hecho cuando:* devuelve `pregunta_ref, orden, tipo, enunciado, peso,
dificultad, retroalimentacion` y la rúbrica; **y una prueba por reflexión
confirma que el DTO no tiene ninguna propiedad cuyo nombre contenga `clave`,
`respuesta_correcta` o `solucion`.**
*Origen:* HU-08, artículo 3.

**T-21** `[C]` `POST /v1/banco/{ref}/extraer`: extracción según la columna
`reglas`.
*Hecho cuando:* respeta `extraer` y `por_dificultad`; dos llamadas seguidas dan
selecciones distintas; si la mezcla pide más preguntas de una dificultad de las
que hay, el `motivo` lo dice en castellano.
*Origen:* CA-08.2, CA-08.3, C-3.

**T-22** `[C]` `POST /v1/comprobar` sobre `LecturaDeManifiesto.Acierta()`.
*Hecho cuando:* devuelve `{acierta}` y la retroalimentación de la pregunta;
`«  1867  »` y `«1867»` dan el mismo resultado; **no escribe nada en ninguna
tabla.**
*Origen:* HU-09, CA-09.2.

**T-23** `[C]` Prueba de tiempo constante en `/v1/comprobar`.
*Hecho cuando:* la diferencia de tiempo entre una respuesta que falla en el
primer carácter y una que falla en el último no es estadísticamente
distinguible.
*Origen:* CA-09.3, artículo 3.

**T-24** `[L]` `MontadorDeExamen`: de una evaluación o un banco a un examen del
LMS.
*Hecho cuando:* un examen montado desde un banco da preguntas distintas a dos
alumnos, y el LMS guarda qué le tocó a cada uno.
*Origen:* HU-08, D-5.

**T-25** `[L]` `CalificadorAutomatico` y cola de preguntas abiertas.
*Hecho cuando:* las autocalificables quedan puntuadas y las abiertas quedan
pendientes con su rúbrica a la vista.
*Origen:* CA-09.1, CA-09.4.

**T-26** `[L]` Aviso cuando una evaluación trae preguntas abiertas sin rúbrica.
*Hecho cuando:* el profesor ve el aviso antes de asignar el examen.
*Origen:* CA-08.5.

---

## E7 · Reparto

**T-27** `[C]` `GET /v1/medio/{ref}`: dirección de un solo uso del servidor de
medios.
*Hecho cuando:* la dirección sirve por rangos, deja de funcionar al cerrar la
sesión, y en ningún momento hay una copia descifrada en disco.
*Origen:* C-1, D-2, CA-07.6.

**T-28** `[L]` `RetransmisorDeMedios`: loopback → LAN con soporte de rangos.
*Hecho cuando:* una tableta reproduce un vídeo y puede adelantar sin descargarlo
entero.
*Origen:* HU-07.

**T-29** `[L]` Autorización del reparto por alumno y grupo.
*Hecho cuando:* un alumno de otro grupo recibe `403`, y una dirección de medio no
funciona sin vale de sesión.
*Origen:* CA-07.1, CA-07.4. **Bloqueada por C-6.**

**T-30** `[L]` `RepartoDeClase` y empuje por WebSocket.
*Hecho cuando:* CA-07.2 (tableta que llega tarde) y CA-07.3 (retirada) se
comprueban con dos tabletas.
*Origen:* HU-07, D-6.

**T-31** `[L]` Prueba de carga: 30 clientes con el mismo vídeo.
*Hecho cuando:* reproducción fluida en los 30, con la medida de memoria y CPU del
Backend anotada.
*Origen:* CA-07.5. **Si falla, vuelve a C-1**: la retransmisión centralizada
sería la decisión equivocada y hay que replantearla, no parchearla.

**T-32** `[L]` Retirar de las tabletas lo que deja de estar disponible.
*Hecho cuando:* desactivar por política un material repartido lo quita de las
tabletas en menos de un minuto.
*Origen:* CA-11.3.

---

## E8 · Repaso

**T-33** `[C]` `POST /v1/repaso`: escribe en `m08_repaso_sesion` y
`m08_repaso_consumo`.
*Hecho cuando:* acepta `persona_id` nulo, y una prueba cuenta filas para
confirmar que **no** se escribe ninguna calificación.
*Origen:* HU-10, artículos 5 y 12.

**T-34** `[L]` Registrar apertura y tiempo desde la tableta.
*Hecho cuando:* CA-10.1 y CA-10.3 se comprueban con un alumno identificado y
otro sin identificar.
*Origen:* HU-10.

**T-35** `[L]` Integrar los ganchos `window.avacom.progreso` y
`window.avacom.terminado` de las lecciones HTML.
*Hecho cuando:* el progreso de una lección generada llega al LMS, **y la misma
página abierta suelta en un navegador sigue funcionando igual**.
*Origen:* C-8, sección 7 de [ESTANDAR-CONTENIDO.txt](ESTANDAR-CONTENIDO.txt).

---

## Transversales

**T-36** `[A]` Actualizar [CONTRATO-LMS.txt](CONTRATO-LMS.txt): los diez tipos
reales, los seis puntos de enlace nuevos, `generacion` y `capacidades`.
*Hecho cuando:* el documento describe lo que el código hace, y no al revés.
*Origen:* C-2, D-3, artículo 15.

**T-37** `[C]` Ampliar
[ApiLocalTests.cs](app-biblioteca/tests/Avacom.Contenido.Tests/ApiLocalTests.cs)
con los seis puntos nuevos, cubriendo 401, 403 y 404 en cada uno.
*Hecho cuando:* la suite pasa y ningún nombre de campo existente ha cambiado.
*Origen:* artículo 15.

**T-38** `[C]` Prueba que fija que los puntos nuevos **no** escuchan fuera de
loopback.
*Hecho cuando:* falla si alguien sustituye `IPAddress.Loopback` por
`IPAddress.Any`.
*Origen:* artículo 2.

**T-39** `[L]` Componente falso del contenido para las pruebas del LMS.
*Hecho cuando:* la suite del LMS corre sin `AVACOM-Contenido` instalado.
*Origen:* práctico.

**T-40** `[A]` Prueba de punta a punta en un aula de mentira: un profesor, dos
tabletas, un vídeo, un examen.
*Hecho cuando:* pasa **con el cable de red desconectado**.
*Origen:* artículo 1.

---

## Resumen y orden

| Etapa | Tareas | Bloqueos |
|---|---|---|
| E1 Enlace | T-01…T-06 | C-5 en T-05 |
| E2 Catálogo | T-07…T-12 | C-2 en T-08 |
| E3 Planificación | T-13…T-15 | — |
| E4 Proyección | T-16…T-17 | — |
| E5 Generación | T-18…T-19 | C-5 en T-19 |
| E6 Exámenes | T-20…T-26 | C-3 |
| E7 Reparto | T-27…T-32 | **C-1 y C-6** |
| E8 Repaso | T-33…T-35 | C-7 |
| Transversales | T-36…T-40 | — |

**Camino más corto a algo utilizable:** T-01 → T-06, T-07 → T-12, T-16, T-17.
Con eso un profesor ya busca material y lo proyecta en la pantalla del aula.

---
---

# FASE 6 · IMPLEMENTACIÓN

```
/speckit.implement
```

**Qué produce:** el código, paso a paso.
**Qué NO hace:** redefinir el alcance. Si en la fase 6 aparece la tentación de
añadir algo, vuelve a la fase 2.

---

## 6.1 · Cómo trabaja el agente

1. **Una tarea a la vez**, en el orden de la fase 5.
2. **Antes de tocar nada, leer el archivo entero.** Los comentarios de este
   código llevan el motivo de cada decisión junto a la línea que la aplica. Un
   cambio que contradice un comentario es un cambio que hay que discutir, no
   hacer.
3. **La prueba primero** cuando la tarea tiene criterio verificable.
4. **No se toca ninguna prueba existente para que vuelva a pasar.** Si una prueba
   de [ApiLocalTests.cs](app-biblioteca/tests/Avacom.Contenido.Tests/ApiLocalTests.cs)
   falla, la respuesta correcta es revertir el cambio o subir la versión del
   contrato con acuerdo de los dos equipos.
5. **Parar y preguntar** si una tarea obliga a romper un artículo de la fase 1.
   Ese es el momento de la conversación, no después.

---

## 6.2 · Preparar el entorno

Requisitos, tal como los declara [LEEME.txt](LEEME.txt):

- **SDK de .NET 10** — `dotnet --version`
- **Carga de trabajo de MAUI** — `dotnet workload install maui`
- **Python 3.10+** con `cryptography`, `pillow`, `reportlab` — solo para armar
  paquetes de prueba; en el aula no hay Python
- **Windows 10 1809 o superior** — por debajo, el componente de navegación
  incrustado no es fiable, y de él dependen el visor de documentos y el de
  material interactivo
- **Disco local.** SQLite necesita bloqueo de archivos; en una unidad de red da
  error de entrada y salida

Comprobar que todo funciona antes de escribir una línea:

```bash
cd C:\projects\contenido-01\AVACOM_CONTENIDO_VERSION02 && .\PROBAR-TODO.cmd
```

Siete etapas; para en la primera que falle diciendo cuál es. Si termina en
verde, hay una base sana sobre la que construir.

Levantar el componente para desarrollar contra él:

```bash
cd C:\projects\contenido-01\AVACOM_CONTENIDO_VERSION02 && .\EJECUTAR-APLICACION.cmd
```

Comprobar que dejó su nota:

```bash
type %ProgramData%\AVACOM\contenido\enlace.json
```

---

## 6.3 · Contenido de prueba

Ya hay dos especificaciones listas en `paquetes/specs/`:
`spec_co_secundaria.json` (matemáticas de grado 8, con evaluación de ocho
reactivos y rúbrica) y `spec_co_preescolar.json`. Sirven para desarrollar sin
producir contenido nuevo.

Para hacer uno propio, el flujo completo está en
[COMO-CARGAR-CONTENIDO.txt](paquetes/COMO-CARGAR-CONTENIDO.txt):

```bash
py -3 avacom_recolector.py revisar "..\contenido\CO\secundaria\09\humanidades"
```

```bash
py -3 avacom_recolector.py armar "..\contenido\CO\secundaria\09\humanidades"
```

**El paso 3 es el último momento en que se puede mirar el contenido en claro.**
Después queda cifrado y solo se abre en un aula con licencia.

---

## 6.4 · Guía por etapa

### E1 · Enlace

**Empezar por T-02**, no por T-01. El caso «no hay contenido» es el que más se
olvida y el que más rompe en un aula recién montada. Escribirlo primero obliga a
que el resto se construya alrededor de él en vez de encima.

El ejemplo de cliente en C# está al final de
[CONTRATO-LMS.txt](CONTRATO-LMS.txt) y es la forma canónica de leer la nota,
validar el contrato y montar el `HttpClient`. Copiarlo y tiparlo.

Punto donde se falla: llamar a `PuntoDeEnlace.Leer()` una vez al arrancar y
guardar el puerto para siempre. El puerto cambia en cada arranque del
componente.

---

### E2 · Catálogo

Antes de escribir los DTOs, leer los nombres de campo en la prueba
`El_catalogo_trae_los_campos_que_el_LMS_necesita`. Esa lista **es** el contrato.

Punto donde se falla: modelar la taxonomía como una jerarquía fija de tres o
cuatro clases. No lo es. Un solo tipo `NodoTaxonomia` recursivo, y la
profundidad se descubre recorriendo hasta que un padre no devuelve hijos.

Para T-09, el contador de generación tiene que subir en **cuatro** sitios:
instalar, desinstalar, cambiar política y reconstruir índice. Buscarlos en
[Instalador.cs](app-biblioteca/src/Avacom.Contenido/Indice/Instalador.cs) y
[Politica.cs](app-biblioteca/src/Avacom.Contenido/Indice/Politica.cs). Si se
olvida uno, el fallo es sutil: el LMS se queda desactualizado solo en ese caso.

---

### E3 · Planificación

Tarea corta y de trampa fácil. Al pintar una unidad hay una llamada por material
anclado a `GET /v1/elemento/{ref}` para resolver el título. La tentación de
guardar el título «para no llamar tantas veces» es exactamente lo que el
artículo 6 prohíbe. Si el rendimiento molesta, la salida es pedir el catálogo
filtrado por `taxonomia_ref` en una sola llamada, no cachear títulos.

---

### E4 · Proyección

La más pequeña de todas. Un `POST` y pintar el `motivo`.

Punto donde se falla: esperar a que el material cargue. La respuesta llega en
cuanto la petición se acepta, no cuando el vídeo termina de cargarse. Ver el
comentario de
[PuenteConElLms.cs](app-biblioteca/src/Avacom.Biblioteca.App/Paquetes/PuenteConElLms.cs).

---

### E5 · Generación

Sondeo cada 30 segundos de una respuesta de doscientos bytes. No usar un
temporizador por pantalla abierta: uno solo para toda la aplicación, que emita
un evento.

---

### E6 · Exámenes

**Escribir T-23 (la prueba de tiempo constante) antes que T-22.** Es el artículo
3 hecho prueba, y si se escribe después, se escribe para que pase.

Al implementar `/v1/comprobar` en el componente, **no** añadir un registro que
apunte la respuesta enviada. Parece inocente y es una fuga: la clave se
reconstruye juntando los intentos que acertaron.

Para T-21, las reglas del banco están en la columna `reglas` de `p_elemento`
como JSON, con la forma que produce
[avacom_recolector.py](paquetes/avacom_recolector.py) al leer `extraer:` y
`por_dificultad:`. Los avisos que ya emite el recolector cuando la mezcla pide
más de lo que hay son el texto que debe devolver el `motivo`.

---

### E7 · Reparto

La etapa cara, y la que puede obligar a volver atrás.

**Hacer T-31 (la prueba de carga) pronto**, aunque esté al final de la lista. Si
30 clientes con el mismo vídeo no van fluidos, la decisión C-1 era la
equivocada, y eso hay que saberlo antes de construir T-28 a T-30 encima.

Punto donde se falla, y está documentado en
[LEEME.txt](LEEME.txt): un flujo de medio tiene que quedarse con su **propia
copia** de la clave del paquete. El gestor cierra manifiestos cuando pasa de
cuatro abiertos y al cerrarlos borra la clave con ceros; si el flujo compartiera
esa referencia, la reproducción moriría en cuanto se abriera un quinto paquete.
Con treinta tabletas y varios paquetes en juego, esto deja de ser hipotético.

---

### E8 · Repaso

Los ganchos `window.avacom.progreso` y `window.avacom.terminado` que emiten las
lecciones HTML van dentro de un `if` a propósito: la página tiene que seguir
funcionando abierta suelta en un navegador. **No convertirlos en obligatorios.**

---

## 6.5 · Lista de comprobación antes de dar por cerrada la integración

Cada línea es un artículo de la fase 1 hecho prueba ejecutable.

- [ ] La suite completa pasa **con el cable de red desconectado** *(art. 1)*
- [ ] `Solo_escucha_en_el_propio_equipo` sigue en verde *(art. 2)*
- [ ] Ninguna respuesta de la API contiene `clave_respuesta` — comprobado por
      reflexión sobre los DTOs *(art. 3)*
- [ ] El LMS no abre `indice.db` en ningún sitio — comprobado por búsqueda de
      texto *(art. 4)*
- [ ] No hay tabla nueva de alumnos ni notas en
      [contenido.sql](esquema/contenido.sql) *(art. 5)*
- [ ] `unidad_material` no tiene columna de título *(art. 6)*
- [ ] Un material desactivado por política no aparece por ninguna vía *(art. 7)*
- [ ] No hay caché de catálogo de más de un minuto *(art. 8)*
- [ ] El LMS arranca y funciona con el componente apagado *(art. 9)*
- [ ] Un contrato desconocido detiene la conversación con un mensaje claro *(art. 10)*
- [ ] No existe punto de enlace de instalación ni de política *(art. 11)*
- [ ] Ninguna fila de calificación se escribe en el componente — comprobado
      contando filas *(art. 12)*
- [ ] Nada por debajo de 20 px ni áreas táctiles menores de 64 px *(art. 13)*
- [ ] Todo `motivo` se puede enseñar a un profesor tal cual *(art. 14)*
- [ ] Ninguna prueba existente se modificó para que volviera a pasar *(art. 15)*
- [ ] [CONTRATO-LMS.txt](CONTRATO-LMS.txt) describe lo que el código hace *(T-36)*

---
---

# ANEXOS

## A · Contrato v1 · referencia rápida

**Descubrimiento** — `%ProgramData%\AVACOM\contenido\enlace.json`

```json
{ "Contrato": 1, "Puerto": 51234, "Ficha": "9f3c…64 hex", "Proceso": 8412 }
```

Ausente = el componente no corre. Estado **normal**, no error.

**Todas las peticiones** — `X-Avacom-Ficha: <ficha>` · respuestas
`application/json; charset=utf-8`

| Método y ruta | Respuesta | Errores |
|---|---|---|
| `GET /v1/salud` | `componente, contrato, elementos, paquetes, politicas` | 401 |
| `GET /v1/catalogo[?nivel&grado&asignatura&tipo&taxonomia_ref]` | `{elementos:[…]}` | 401 |
| `GET /v1/taxonomia[?padre]` | `{nodos:[…]}` | 401 |
| `GET /v1/elemento/{ref}` | el objeto de elemento | 401, 403, 404 |
| `POST /v1/mostrar` `{elemento_ref}` | `{aceptado}` o `{aceptado,motivo}` | 400, 401, 409 |

**Objeto de elemento**
`ref, tipo, titulo, nivel, grado, asignatura, idioma, taxonomia_ref, version,
duracion_seg, paquete, huella`

**Objeto de nodo**
`ref, padre, tipo, codigo, nombre, orden, pais, nivel`

**Lo que NO sale, y no debe salir:** la ruta del paquete en disco y el
identificador interno de instalación. Hay una prueba que lo fija
(`El_catalogo_NO_dice_donde_vive_el_archivo`).

---

## B · Tipos de elemento

**Los diez que acepta el manifiesto** (`CHECK` de `p_elemento`):

```
documento   imagen   video   audio   leccion
actividad   evaluacion   banco   interactivo   scorm
```

`CONTRATO-LMS.txt` documenta hoy solo ocho: faltan `banco` y `scorm`. Ver C-2 y
T-36.

**El LMS trata este conjunto como abierto** (RN-06). Un tipo desconocido se
muestra, no se descarta.

---

## C · Códigos de estado

| Código | Significa | Qué hace el LMS |
|---|---|---|
| 200 | Correcto | Seguir |
| 400 | Petición mal formada | Error de programación del LMS. Registrar |
| 401 | Ficha ausente o inválida | Releer `enlace.json` y reintentar **una vez** |
| 403 | La política de la escuela no lo permite | Tratar igual que 404 de cara al profesor |
| 404 | No está instalado, o el punto de enlace no existe | Si es de elemento: ancla rota. Si es de ruta: capacidad no soportada |
| 409 | No se pudo mostrar | Enseñar el `motivo` tal cual |

**403 y 404 se tratan igual de cara al profesor** por el artículo 7: distinguir
«no está» de «está pero no puedes» le dice al profesor que existe material que
no puede ver, y eso es exactamente lo que la política quiere evitar.

---

## D · Documentos de referencia

| Documento | Para quién |
|---|---|
| [ESTANDAR-CONTENIDO.txt](ESTANDAR-CONTENIDO.txt) | Equipo de contenido. Estructura de carpetas, guiones, evaluaciones, bancos, interactivos |
| [CONTRATO-LMS.txt](CONTRATO-LMS.txt) | Equipo de LMS. El contrato v1 completo |
| [INVENTARIO.txt](INVENTARIO.txt) | Quien desarrolla el componente. Qué es cada archivo y por qué |
| [LEEME.txt](LEEME.txt) | Arquitectura, decisiones tomadas, diagnóstico de fallos |
| [esquema/LEEME.txt](esquema/LEEME.txt) | Las ocho tablas y las tres vistas |
| [paquetes/COMO-CARGAR-CONTENIDO.txt](paquetes/COMO-CARGAR-CONTENIDO.txt) | Quien publica y firma |

---

## E · Estado del documento

| Fase | Estado |
|---|---|
| 1 · Constitución | **Completa.** 15 artículos, cada uno con su motivo |
| 2 · Especificación | **Completa.** 11 historias, 10 reglas de negocio |
| 3 · Clarificación | **Pendiente.** 6 propuestas por confirmar, **4 preguntas abiertas** |
| 4 · Plan | **Borrador.** Asume las propuestas de la fase 3. Se revisa al cerrarlas |
| 5 · Tareas | **Borrador.** 40 tareas. T-05, T-08, T-19, T-29 bloqueadas |
| 6 · Implementación | **No iniciada** |

**El siguiente paso es la fase 3.** Concretamente, responder las cuatro
`[ABIERTA]`:

- **C-5** ¿Qué pasa si el componente se reinicia a mitad de clase?
- **C-6** ¿Quién autoriza a un alumno concreto a ver un material concreto?
- **C-9** ¿Entra SCORM en este alcance?
- **C-10** ¿El LMS necesita saber para qué equipo está emitida la licencia?

**C-6 es la más urgente**: bloquea la etapa E7 entera, que es la más cara, y su
respuesta puede cambiar el diseño del Backend de OPS Master más allá de esta
integración.
