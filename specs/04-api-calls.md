# 04 · Llamadas a la API local · Lo que consume el LMS

| Campo | Valor |
|---|---|
| Estado | **Vigente.** Contrato 1. Cada ruta se comprobó contra el despachador de la API y sus pruebas |
| Ámbito | Las doce rutas que publica AVACOM Biblioteca en `127.0.0.1` y cómo las consume el backend del LMS |
| Audiencia | Equipo del LMS |
| Fecha de corte | 14 de septiembre de 2026 |
| Sustituye a | `api_calls.md`, que se retiró de la raíz. Su sección «Lo que la API todavía no expone» estaba desactualizada: las cinco capacidades opcionales ya existen |
| Reglas y motivos | [01-Contrato-Biblioteca-LMS.md](01-Contrato-Biblioteca-LMS.md). Esto es la versión práctica |

Tres cosas antes de empezar:

- **No hay un servicio aparte.** La API vive dentro del proceso de la
  aplicación de escritorio. Se enciende al abrir la pestaña «Contenido AVACOM»
  con licencia cargada y muere al cerrarla.
- **Solo escucha en `127.0.0.1`**, en un puerto que el sistema elige en cada
  arranque. Ninguna tableta la alcanza y no hay que abrir nada en el cortafuegos.
- **No escribe nada por orden del LMS.** Los dos `POST` que existen no cambian
  ningún dato de la Biblioteca.

---

## 1 · Encontrar la API

```
%ProgramData%\AVACOM\contenido\enlace.json
{"Contrato":1,"Puerto":51234,"Ficha":"9f3c…(64 hex)","Proceso":8412}
```

| Situación | Qué hacer |
|---|---|
| No existe | La Biblioteca no está corriendo. Estado normal: mostrar «sin contenido» y seguir |
| `Contrato` distinto de `1` | No llamar. Avisar de que hay que actualizar el LMS |
| Existe pero nadie responde en el puerto | La aplicación se cerró de golpe. Tratar igual que si no existiera |
| Existe y el proceso `Proceso` ya no vive | Ídem. En Windows, comprobarlo con un handle de solo consulta, nunca con una señal que pueda terminar el proceso |

Se relee en **cada** petición. La `Ficha` va en la cabecera `X-Avacom-Ficha`.
Todas las respuestas llevan `Cache-Control: no-store` y cierran la conexión.

---

## 2 · `GET /v1/salud` · estado y capacidades

```json
{"componente":"avacom-contenido","contrato":1,
 "elementos":10,"paquetes":2,"politicas":0,
 "huella_catalogo":"9f3c1a2b4d5e6f70",
 "cursos":2,
 "capacidades":["curso","medio","leccion","evaluacion","comprobar","voz"]}
```

| Campo | Para qué sirve |
|---|---|
| `contrato` | Debe coincidir con el de la nota |
| `huella_catalogo` | Dieciséis caracteres que resumen lo que el LMS vería ahora. Se guarda y se compara por igualdad. No es un contador |
| `capacidades` | Lo que este componente sabe hacer. **Se consulta antes de usar cualquier ruta de la sección 5.** Lo que no está, no se simula |
| `elementos`, `paquetes`, `politicas`, `cursos` | Conteos informativos. Son el último recurso como señal de cambio si faltara la huella |

### 2.1 · Las capacidades, en detalle

Es un **campo**, no una ruta. No existe `/v1/capacidades`.

| Capacidad | Habilita | Qué hace el LMS si falta |
|---|---|---|
| `curso` | `/v1/cursos`, `/v1/curso/{ref}` | Se queda con el catálogo plano de la sección 4 |
| `medio` | `/v1/medio/{ref}` | No puede retransmitir material a las tabletas |
| `leccion` | `/v1/leccion/{ref}` | Muestra la lección como un ítem sin pasos |
| `evaluacion` | `/v1/evaluacion/{ref}` | No entrega preguntas. 501 explicativo hacia arriba |
| `comprobar` | `/v1/comprobar` | No produce veredicto ni nota. El intento queda pendiente de corrección |
| `voz` | `/v1/voz/{ref}` | No ofrece el botón de escuchar |

Un componente antiguo que no declare `capacidades` equivale a la lista vacía.

Dos cosas que el LMS espera y **no** están en la lista, y que la Biblioteca no
implementa: `banco` y `repaso`. Sus rutas no existen. Ver
[09-posibles-cambios-conexion-lms.md](09-posibles-cambios-conexion-lms.md).

---

## 3 · Los cursos · capacidad `curso`

### 3.1 · `GET /v1/cursos`

```json
{"huella_catalogo":"675d25df86c03a1e",
 "cursos":[
  {"curso_ref":"co-secundaria-8-matematicas",
   "titulo":"Matemáticas · Grado 8",
   "version_vigente":"1",
   "pais":"CO","nivel":"secundaria","grado":"8",
   "asignatura":"Matemáticas","idioma":"es",
   "lecciones":1,"elementos":5,
   "actualizado_en":1757000000000}
 ]}
```

| Campo | Qué es |
|---|---|
| `curso_ref` | **La referencia estable.** Es la clave del paquete. No cambia al republicar. Es lo que el LMS guarda en el expediente |
| `titulo` | Texto para pantalla. Nunca identificador: cambia entre versiones |
| `version_vigente` | Texto. La versión del paquete instalado |
| `pais`, `nivel`, `grado`, `asignatura`, `idioma` | Para agrupar y filtrar. En preescolar `asignatura` trae la actividad rectora y `grado` una palabra |
| `lecciones`, `elementos` | Cuántas secuencias de tema y cuántos materiales visibles tiene |
| `actualizado_en` | Momento de la instalación, en milisegundos desde 1970 |

Un curso es un paquete instalado. La lista ya está filtrada por la política de
la escuela. **Un curso sin nada visible no se ofrece.**

### 3.2 · `GET /v1/curso/{curso_ref}`

```json
{"curso_ref":"co-secundaria-8-matematicas",
 "titulo":"Matemáticas · Grado 8","version":"1",
 "nivel":"secundaria","grado":"8","asignatura":"Matemáticas","idioma":"es",
 "huella":"675d25df86c03a1e",
 "secciones":[
  {"codigo":"co-sec-mat-var-e1",
   "titulo":"Identifico relaciones entre propiedades de las gráficas…",
   "tipo":"estandar","orden":1,
   "items":[
     {"orden":1,"tipo":"evaluacion","elemento_ref":"co-sec-mat-eval-funcion",
      "titulo":"Evaluación · Función lineal","version":"1","duracion_seg":null},
     {"orden":2,"tipo":"leccion","elemento_ref":"co-sec-mat-lec-funcion",
      "titulo":"Función lineal y razón de cambio","version":"1","duracion_seg":null}
   ]}
 ]}
```

| Campo de la sección | Qué es |
|---|---|
| `codigo` | El nodo del árbol curricular. **Estable entre versiones**: se guarda este código, no el título ni la posición |
| `titulo` | Nombre del nodo tal como lo llama el marco |
| `tipo` | El nivel del marco: `estandar`, `tema`, `pensamiento`, `experiencia`… Sirve para una etiqueta, **no** para decidir lógica |
| `orden` | Posición del nodo **entre sus hermanos**. Dos nodos de ramas distintas pueden compartir número |
| `items` | Los materiales visibles colgados directamente de ese nodo |

| Campo del ítem | Qué es |
|---|---|
| `elemento_ref` + `version` | Lo que el LMS guarda al asociar un material |
| `tipo` | `leccion`, `video`, `audio`, `imagen`, `documento`, `interactivo`, `actividad`, `evaluacion`, `banco`, `scorm`. Conjunto **abierto**: un tipo desconocido se muestra, no se descarta |
| `titulo` | Texto para pantalla |
| `duracion_seg` | Solo si el contenido la declaró. Puede ser `null` |
| `orden` | Posición dentro de la sección. **Hoy es alfabética por título.** El orden pedagógico va dentro de la lección, sección 5.2 |

Tres límites de esta respuesta que el LMS tiene que conocer:

- **No trae el padre de cada sección.** El árbol completo se reconstruye con
  `/v1/taxonomia`. La respuesta es una lista plana de nodos con material.
- **Una sección sin materiales visibles no se devuelve.**
- **Tiene dos niveles**, secciones e ítems. El contrato del LMS pide tres. Es el
  mayor trabajo pendiente y está en el documento 09.

`404` si la referencia no existe.

---

## 4 · Navegación fina · sin capacidad

Las cinco rutas originales. No dependen de `capacidades`.

### 4.1 · `GET /v1/catalogo`

```
GET /v1/catalogo
GET /v1/catalogo?nivel=secundaria&grado=8&asignatura=Matemáticas&tipo=video
GET /v1/catalogo?taxonomia_ref=co-sec-mat-var
```

Devuelve `{"elementos":[…]}`. Los cinco filtros son opcionales y se combinan.
Los valores se escriben tal como vienen en las respuestas.

Campos de un elemento: `ref`, `tipo`, `titulo`, `nivel`, `grado`, `asignatura`,
`idioma`, `taxonomia_ref`, `version`, `duracion_seg`, `paquete`, `huella`.
**Ninguno dice dónde vive el archivo en disco**, y hay una prueba que lo fija.

### 4.2 · `GET /v1/taxonomia`

```
GET /v1/taxonomia            la raíz
GET /v1/taxonomia?padre=ref  los hijos de un nodo
```

Devuelve `{"nodos":[…]}`. Campos: `ref`, `padre`, `tipo`, `codigo`, `nombre`,
`orden`, `pais`, `nivel`. Se recorre hasta que un padre no devuelve hijos.
**No asumir cuántos niveles hay**: preescolar y secundaria tienen cadenas
distintas, y es a propósito.

### 4.3 · `GET /v1/elemento/{ref}`

El mismo objeto del catálogo, para una referencia. `404` no instalado, `403` lo
desactivó la escuela.

### 4.4 · `POST /v1/mostrar`

```
→ {"elemento_ref":"co-sec-mat-video-pendiente"}
← 200 {"aceptado":true}
← 409 {"aceptado":false,"motivo":"Ese material no esta instalado en este equipo."}
```

Pide que el material aparezca en la pantalla del aula. Responde al **aceptar**,
no al terminar de cargar. El `motivo` está escrito para enseñárselo a una
persona tal cual. `400` si el cuerpo no es JSON o falta la referencia.

---

## 5 · Contenido descifrado · capacidades opcionales

Todas aplican la política antes de responder: `403` para lo desactivado, `404`
para lo que no está. Todas exigen la ficha.

### 5.1 · `GET /v1/medio/{ref}` · capacidad `medio`

Los bytes descifrados de un material, con su `Content-Type`.

| Detalle | Comportamiento |
|---|---|
| Rangos | Respeta `Range`. Un vídeo se puede adelantar: se descifra solo el bloque pedido |
| `HEAD` | Devuelve las cabeceras sin cuerpo. Sirve para conocer tamaño y tipo |
| Interactivos | `GET /v1/medio/{ref}` sirve el `index.html`. `GET /v1/medio/{ref}/ruta/interna.js` sirve un archivo de dentro del comprimido. `404` si no existe |
| Elemento sin archivo | Una lección es estructura, no tiene archivo: `409` |
| Ruta interna en algo que no es interactivo | `404` |

El backend del LMS consume esto por loopback y **retransmite** a las tabletas
por su propio puerto. La Biblioteca nunca ve una tableta.

### 5.2 · `GET /v1/leccion/{ref}` · capacidad `leccion`

```json
{"elemento_ref":"co-sec-mat-lec-funcion",
 "titulo":"Función lineal y razón de cambio","tipo":"leccion","version":"1",
 "duracion_seg":null,
 "pasos":[
  {"orden":1,"elemento_ref":"co-sec-mat-doc-funcion","titulo":"La función lineal",
   "tipo":"documento","nota":null,"disponible":true},
  {"orden":2,"elemento_ref":"co-sec-mat-video-pendiente","titulo":"Qué significa la pendiente",
   "tipo":"video","nota":"Lectura previa","disponible":true}
 ]}
```

Es el **orden pedagógico** del tema, el que da el número de cada archivo. Un
paso puede apuntar a algo que la escuela desactivó o que ya no está: entonces
viene con `disponible: false` y el LMS no lo ofrece. `409` si la referencia no
es de una lección.

### 5.3 · `GET /v1/evaluacion/{ref}` · capacidad `evaluacion`

```json
{"elemento_ref":"co-sec-mat-eval-funcion",
 "titulo":"Evaluación · Función lineal","tipo":"evaluacion","version":"1",
 "voz":false,
 "preguntas":[
  {"ref":"co-sec-mat-eval-funcion-q1","orden":1,"tipo":"opcion_unica",
   "enunciado":"¿Cuál es la pendiente de la recta y = 3x - 5?",
   "peso":1,"dificultad":"baja","corregible":true,"voz":false},
  {"ref":"co-sec-mat-eval-funcion-q2","orden":2,"tipo":"abierta",
   "enunciado":"Explica qué representa la pendiente…",
   "peso":2,"dificultad":"alta","corregible":false,"voz":false}
 ]}
```

| Campo | Qué es |
|---|---|
| `ref` | La `pregunta_ref` que el LMS guarda y envía a comprobar |
| `corregible` | `true` si la pregunta tiene clave y se puede comprobar. Una abierta la califica el docente con la rúbrica |
| `voz` | Si hay instrucción hablada para el elemento o para esa pregunta |

**Lo que no trae, a propósito:** la clave de respuesta y la retroalimentación.
La retroalimentación llega por `/v1/comprobar`, y solo al acertar.

**Lo que no trae, por un límite del formato:** las opciones. El paquete guarda
la respuesta pero no las opciones entre las que elegir. El alumno escribe y el
sistema compara. Resolverlo es la propuesta de
[03-archivos-JSON.md](03-archivos-JSON.md).

`409` si el elemento no es una evaluación ni una actividad.

### 5.4 · `POST /v1/comprobar` · capacidad `comprobar`

```
→ {"elemento_ref":"co-sec-mat-eval-funcion","pregunta_ref":"co-sec-mat-eval-funcion-q1","respuesta":"3"}
← 200 {"acierta":true,"retroalimentacion":"La pendiente es el número que multiplica a x."}
← 200 {"acierta":false,"retroalimentacion":null}
```

| Situación | Código |
|---|---|
| Falta `elemento_ref` o `pregunta_ref`, o el cuerpo no es JSON | 400 |
| La pregunta no existe en ese elemento | 404 |
| La pregunta es abierta | 409 con el motivo: la califica el docente con la rúbrica |

La comparación se hace donde vive la clave, en tiempo constante, sin distinguir
mayúsculas ni espacios sobrantes. **No escribe nada.** El LMS guarda el veredicto
y la retroalimentación en su expediente; no tiene dónde guardar la clave, y no
es un olvido. `respuesta` puede ser una cadena o cualquier JSON: se compara su
texto.

### 5.5 · `GET /v1/voz/{ref}[/pregunta_ref]` · capacidad `voz`

Los bytes del audio con la instrucción hablada del elemento entero, o de una
pregunta concreta si se añade su referencia. `404` si no la hay. Es para
preescolar, donde el niño no lee.

---

## 6 · Mantenerse al día

```
   cada pocos segundos: GET /v1/salud  (unos doscientos bytes)
       └─ huella_catalogo distinta ─► GET /v1/cursos, y los /v1/curso/{ref} en pantalla
```

La huella cambia al instalar o retirar un paquete, al cambiar la política de la
escuela y al publicar una versión nueva. No cambia por consultar, mostrar ni
registrar uso. Una caché en memoria invalidada con la huella es lo máximo que
el LMS debe guardar.

---

## 7 · Códigos de respuesta

| Código | Significa | Qué hace el LMS |
|---|---|---|
| 200 | Correcto | Seguir |
| 206 | Rango parcial de un medio | Seguir |
| 400 | Cuerpo mal formado o falta un campo obligatorio | Error de programación: registrar |
| 401 | Falta la ficha o es incorrecta | Releer la nota y reintentar una vez |
| 403 | La escuela lo desactivó | De cara al docente, igual que 404 |
| 404 | No instalado, referencia inexistente, o **ruta que no existe en esta versión** | Si es de ruta, es una capacidad no declarada: comprobar `capacidades` |
| 409 | No se pudo mostrar; el elemento no es del tipo pedido; pregunta abierta | Enseñar el motivo tal cual |

La Biblioteca no devuelve 501. Es el backend del LMS quien traduce «capacidad
no declarada» a 501 hacia arriba, «Biblioteca ausente» a 503 y cualquier otro
error de la Biblioteca a 502.

---

## 8 · Reglas que hay que respetar

1. **Guardar referencias, nunca títulos:** `curso_ref`, el `codigo` de la
   sección, `elemento_ref` con su `version` y `pregunta_ref`.
2. **Lo que no llega, no existe.** Lo desactivado no viene marcado.
3. **No guardar un catálogo propio.** Una caché en memoria con la huella es el
   máximo.
4. **No abrir archivos por cuenta del LMS.** Los bytes se piden por `/v1/medio`.
5. **Sin contenido se sigue funcionando.**
6. **Comprobar el contrato antes de hablar** e **ignorar los campos que no se
   conozcan.**
7. **Consultar `capacidades` antes de cualquier ruta de la sección 5**, y no
   simular lo que falte.

---

## 9 · Ejemplos

**PowerShell**, para probar a mano en el equipo del aula:

```powershell
$nota = Get-Content "$env:ProgramData\AVACOM\contenido\enlace.json" -Raw | ConvertFrom-Json
if ($nota.Contrato -ne 1) { throw "Contrato $($nota.Contrato) desconocido" }
$base = "http://127.0.0.1:$($nota.Puerto)"
$cab  = @{ "X-Avacom-Ficha" = $nota.Ficha }

$salud = Invoke-RestMethod "$base/v1/salud" -Headers $cab
$salud
if ($salud.capacidades -contains "curso") {
  Invoke-RestMethod "$base/v1/cursos" -Headers $cab | Select-Object -ExpandProperty cursos
  Invoke-RestMethod "$base/v1/curso/co-secundaria-8-matematicas" -Headers $cab | ConvertTo-Json -Depth 6
}
if ($salud.capacidades -contains "evaluacion") {
  Invoke-RestMethod "$base/v1/evaluacion/co-sec-mat-eval-funcion" -Headers $cab | ConvertTo-Json -Depth 4
}
if ($salud.capacidades -contains "comprobar") {
  $cuerpo = @{ elemento_ref = "co-sec-mat-eval-funcion"; pregunta_ref = "co-sec-mat-eval-funcion-q1"; respuesta = "3" } | ConvertTo-Json
  Invoke-RestMethod "$base/v1/comprobar" -Method Post -Headers $cab -ContentType "application/json" -Body $cuerpo
}
```

**Python**, sin dependencias externas, como lo hace el backend del LMS:

```python
import json, os, urllib.request

def leer_enlace():
    ruta = os.path.join(os.environ["ProgramData"], "AVACOM", "contenido", "enlace.json")
    if not os.path.exists(ruta):
        return None                                    # sin contenido: estado normal
    with open(ruta, encoding="utf-8") as f:
        nota = json.load(f)
    if nota["Contrato"] != 1:
        raise RuntimeError(f"contrato {nota['Contrato']} desconocido")
    return nota

def pedir(nota, ruta, cuerpo=None):
    datos = json.dumps(cuerpo, ensure_ascii=False).encode("utf-8") if cuerpo is not None else None
    cab = {"X-Avacom-Ficha": nota["Ficha"], "Accept": "application/json"}
    if datos is not None:
        cab["Content-Type"] = "application/json"
    peticion = urllib.request.Request(f"http://127.0.0.1:{nota['Puerto']}{ruta}", data=datos, headers=cab)
    with urllib.request.urlopen(peticion, timeout=3) as r:  # 4xx lanza HTTPError
        return json.load(r)

nota = leer_enlace()
if nota:
    salud = pedir(nota, "/v1/salud")
    caps = set(salud.get("capacidades", []))
    if "curso" in caps:
        for curso in pedir(nota, "/v1/cursos")["cursos"]:
            print(curso["curso_ref"], "·", curso["titulo"])
    if "evaluacion" in caps and "comprobar" in caps:
        ev = pedir(nota, "/v1/evaluacion/co-sec-mat-eval-funcion")
        primera = next(p for p in ev["preguntas"] if p["corregible"])
        veredicto = pedir(nota, "/v1/comprobar",
                          {"elemento_ref": ev["elemento_ref"], "pregunta_ref": primera["ref"], "respuesta": "3"})
        print(veredicto)
```

---

## 10 · Cómo arrancar la Biblioteca para probar

La API no se arranca sola: se enciende dentro de la aplicación.

| Paso | Cómo |
|---|---|
| Con doble clic | `EJECUTAR-APLICACION.cmd` en la raíz. Si no hay carpeta `trabajo`, la prepara primero |
| A mano | Desde `app-biblioteca`, porque ahí está el `global.json`: `dotnet run --project src\Avacom.Biblioteca.App` |
| En la aplicación, la primera vez | Pestaña **Administración**: elegir `trabajo\lic\licencia.json`, «Revisar e instalar». Luego pestaña **Contenido AVACOM**: ahí se enciende la API y aparece la nota |
| Comprobar desde fuera | `powershell -ExecutionPolicy Bypass -File consultar-api.ps1`, o con `-Vigilar` para quedarse mirando la huella. `PROBAR-API.cmd` hace lo mismo en una ventana, para el equipo sin teclado |
| Pruebas del contrato | Desde `app-biblioteca`: `dotnet test tests\Avacom.Contenido.Tests`. No necesitan la aplicación abierta |

En un aula con la Biblioteca instalada no hay `dotnet`: se abre la aplicación
desde el menú de inicio y la API se enciende igual al entrar a «Contenido
AVACOM».

---

## 11 · Lo que la API todavía no expone

| Qué | Estado | Dónde se sigue |
|---|---|---|
| Extracción de un banco por persona | No existe. El LMS la espera como capacidad `banco` | Documento 09, PC-02 |
| Registro de repaso desde el LMS | No existe. El LMS la espera como capacidad `repaso` | Documento 09, PC-02 |
| Opciones de cada pregunta | El paquete no las guarda | Documento 03 |
| Puntaje máximo de una evaluación | No existe en el paquete | Documento 09, PC-24 |
| Padre de cada sección en `/v1/curso` | No se devuelve | Documento 09, PC-14 |
| Orden real de los ítems de una sección | Alfabético por título | Documento 09, PC-16 |
| `/v1/curso/{ref}/manifiesto` | No existe | Documento 09, PC-19 |
| Instalar, desinstalar, cambiar políticas | No existe ni existirá por esta vía | Es del administrador, en la aplicación |
