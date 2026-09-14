# 01 · Contrato entre AVACOM Biblioteca y el LMS

| Campo | Valor |
|---|---|
| Estado | **Vigente.** Contrato versión 1. Lo que sigue se comprobó en el código y está fijado por pruebas |
| Ámbito | Cómo se protege el contenido, cómo se encuentra la Biblioteca, qué publica y cómo condiciona al LMS |
| Audiencia | Equipo del LMS y equipo de la Biblioteca |
| Fecha de corte | 14 de septiembre de 2026 |
| Sustituye a | `CONTRATO-LMS.txt`, que se retiró de la raíz del repositorio. Su contenido está aquí y en [04-api-calls.md](04-api-calls.md) |

La primera mitad explica el cifrado y la licencia, porque son lo que hace
necesario el contrato: el LMS no puede leer un archivo por su cuenta y por eso
tiene que preguntar. La segunda mitad es el contrato en sí y lo que impone al
consumo.

---

## 1 · Alcance

Este documento compromete tres cosas y solo tres:

1. La forma del paquete publicado y de la licencia, que es lo que el equipo
   técnico de contenido produce y la Biblioteca consume.
2. La forma de la nota de enlace y de las respuestas de la API local, que es lo
   que la Biblioteca produce y el LMS consume.
3. Las reglas que el LMS tiene que respetar para que lo anterior no se rompa.

No compromete el esquema interno de la Biblioteca, la forma de sus tablas ni
dónde guarda nada. Nada de eso es visible desde fuera y puede cambiar sin aviso.

---

## 2 · Cifrado y licencia

### 2.1 · Dos cosas distintas que se confunden

| | Qué garantiza | Qué no garantiza | Algoritmo |
|---|---|---|---|
| **Firma** | Que el paquete lo emitió AVACOM y nadie lo alteró | No oculta nada | Ed25519 sobre la serialización canónica |
| **Cifrado** | Que el contenido es ilegible sin la clave | No demuestra quién lo hizo | AES-256-GCM por bloques de 1 MB |

Las huellas de todo son BLAKE2b de 256 bits, en hexadecimal minúscula.

### 2.2 · El paquete publicado

Una carpeta con cuatro cosas. Se llama `avacom-<clave>-v<versión>`.

| Archivo | Qué es | ¿Legible sin licencia? |
|---|---|---|
| `formato.json` | La ficha en claro: versión del formato, clave del paquete, vitrina, clave pública del emisor y el bloque firmado con las huellas | **Sí.** Es lo único, y está así para que un técnico sepa qué tiene en la mano sin abrirlo |
| `manifiesto.enc` | La base SQLite del paquete, cifrada entera: estructura curricular, elementos, secuencias, preguntas, rúbricas, voces y **las claves de respuesta** | No |
| `medios/<huella>.<ext>.enc` | Cada archivo de medios, cifrado por separado con su propia clave derivada | No |
| `firma.sig` | Firma Ed25519, 64 bytes, sobre el bloque `payload_firmado` de la ficha | No aplica |

La ficha, tal como la produce el publicador:

| Campo | Contenido |
|---|---|
| `formato_version` | `2`. El formato 1 era en claro y ya no se acepta |
| `clave_paquete` | La referencia estable del curso. Es lo que el LMS recibe como `curso_ref` |
| `version` | Texto. Sube al republicar |
| `cifrado` | Informativo: algoritmo, tamaño de bloque y derivación |
| `emisor`, `clave_publica` | Quién firma y con qué clave pública Ed25519 |
| `vitrina` | País, nivel, grado, asignatura, idioma, título y número de elementos. Es lo que la Biblioteca muestra en la lista de cursos |
| `payload_firmado` | Clave, versión, formato, huella del manifiesto cifrado y el inventario de medios, cada uno con su nombre, bytes en claro y huella del cifrado |

### 2.3 · La cadena de claves

```
   K_pkg ── 256 bits al azar, una por paquete, generada al publicar
     │
     ├─► HKDF-SHA256(K_pkg, info = "avacom-archivo:" + etiqueta) ─► clave de CADA archivo
     │       etiqueta = nombre del archivo en claro, sin .enc; "manifiesto" para el manifiesto
     │       Comprometer un archivo no compromete los demás
     │
     └─► NO viaja dentro del paquete. Nunca.
         Viaja dentro de la LICENCIA, envuelta para UN equipo:
             X25519(efímera, pública del equipo) ─► HKDF-SHA256(info = "avacom-envoltura-clave-paquete") ─► KEK
             AES-256-GCM(KEK) sobre K_pkg
```

Consecuencia: copiar la carpeta del paquete a una memoria USB y llevársela a
otro equipo no sirve de nada. Sin la clave privada de ese equipo, la clave del
paquete no se puede desenvolver y los medios son ruido.

### 2.4 · El formato de un archivo cifrado

Compatible byte a byte entre la herramienta en Python que publica y el lector
en C# que abre. Hay una prueba con vectores fijos que lo comprueba.

| Bytes | Campo |
|---|---|
| 10 | La marca `AVACOMENC1` |
| 4 | Tamaño de bloque, entero sin signo, orden de bytes pequeño. Hoy 1 048 576 |
| 8 | Longitud del contenido en claro |
| 8 | Base del nonce, aleatoria por archivo |
| Por cada bloque | 4 bytes de longitud del bloque cifrado, y luego el bloque con su etiqueta de autenticación de 16 bytes |

El nonce de cada bloque es la base de 8 bytes más el índice del bloque en 4
bytes. El dato asociado autenticado es la marca de formato. Un bloque no se
puede mover de sitio dentro del archivo sin que se note.

Se cifra por bloques y no de una pieza a propósito: adelantar un vídeo no
obliga a descifrar los cien megabytes anteriores.

### 2.5 · La licencia

Un archivo JSON firmado, uno por equipo. Manipular cualquier campo, incluida la
fecha de vencimiento, invalida la firma.

| Campo | Contenido |
|---|---|
| `cuerpo.instalacion` | Identificador de la instalación a la que se emite |
| `cuerpo.nodo_publica` | Clave pública X25519 del equipo |
| `cuerpo.vence_en` | Milisegundos desde 1970. Pasada esa fecha ningún paquete abre |
| `cuerpo.paquetes.<clave>` | Por cada paquete autorizado: su `version` y su `envoltura` con `efimera`, `nonce` y `clave_envuelta` |
| `firma` | Ed25519 del emisor sobre la serialización canónica del cuerpo |
| `emisor_publica` | Clave pública del emisor, para verificar la firma |

**La serialización canónica** es el punto más frágil de todo el sistema: claves
ordenadas por orden de bytes y sin espacios, idéntica en Python y en C#. Si
difiere en un byte, ninguna firma valida. Hay una clase dedicada a esto y una
prueba que la fija.

### 2.6 · Las seis comprobaciones antes de abrir nada

La Biblioteca las hace en orden, todas, antes de copiar o descifrar un solo
byte. Si una falla, el paquete no se instala y se dice por qué.

| # | Comprobación | Qué detecta |
|---|---|---|
| 1 | `formato_version` es el soportado | Un paquete de otra generación del formato |
| 2 | La firma del bloque firmado es válida, y el emisor es el esperado | Un paquete alterado o firmado por otro |
| 3 | La huella del manifiesto cifrado coincide con la firmada | Un manifiesto sustituido |
| 4 | Cada medio del inventario existe y su huella coincide | Un medio faltante o alterado |
| Licencia | Vigente, incluye este paquete y fue emitida para este equipo | Copia a otro equipo, licencia vencida, paquete no autorizado |
| 5 | El manifiesto declara tantos elementos como tiene | Un manifiesto truncado |
| 6 | Ningún elemento cuelga de un nodo curricular inexistente | Un paquete inconsistente |

### 2.7 · Qué pasa después de abrir

- **El manifiesto descifrado nunca toca el disco.** Se abre en una base SQLite
  en memoria y se descarta al cerrar. Lleva las claves de respuesta de todos
  los exámenes del año.
- **La clave del paquete se borra con ceros** al cerrar el lector.
- **Los medios se descifran al vuelo, bloque a bloque**, desde el archivo
  cifrado. Nunca hay una copia en claro, ni en disco ni completa en memoria.
- **La clave de respuesta solo se puede comparar.** El tipo que llega a la
  interfaz y a la API no tiene ningún campo donde meterla, y la comparación es
  en tiempo constante.

### 2.8 · Qué significa todo esto para el LMS

| Hecho | Consecuencia para el LMS |
|---|---|
| El material está cifrado y la clave vive en la Biblioteca | El LMS **no abre archivos**. Pide bytes descifrados por `/v1/medio` y los retransmite |
| La clave de respuesta nunca sale | El LMS envía la respuesta del alumno a `/v1/comprobar` y guarda el veredicto. No tiene ninguna columna donde guardar una clave, y no es un olvido |
| La política de la escuela se aplica en la Biblioteca antes de entregar un byte | El LMS no puede saltársela pidiendo por referencia directa: recibe 403 |
| La licencia es por equipo | El LMS no necesita saber nada de licencias. Si un paquete no abre, simplemente no aparece en el catálogo |
| `formato_version` y `contrato` son cosas distintas | El primero versiona el paquete y lo comprueba la Biblioteca al instalar. El segundo versiona la API y lo comprueba el LMS al hablar. Pueden subir por separado |

### 2.9 · Lo que el cifrado no resuelve

Quien controle el equipo del aula mientras funciona puede llegar a la clave en
memoria. El cifrado en reposo sube muchísimo el listón y no lo vuelve
imposible. La defensa que lo acompaña es el cifrado de disco y el arranque
verificado del equipo, que están fuera de este repositorio.

---

## 3 · El contrato

### 3.1 · Descubrimiento: la nota de enlace

La API vive dentro del proceso de la aplicación de escritorio. Se enciende al
abrir la pestaña «Contenido AVACOM» con licencia cargada y muere al cerrarla.
No hay servicio ni proceso aparte.

Al encenderse escribe, y al apagarse borra:

```
%ProgramData%\AVACOM\contenido\enlace.json
{"Contrato":1,"Puerto":51234,"Ficha":"…64 caracteres hexadecimales…","Proceso":8412}
```

| Campo | Qué hacer con él |
|---|---|
| `Contrato` | Comprobar que es `1`. Si es otro número, no llamar y avisar |
| `Puerto` | La dirección base es `http://127.0.0.1:{Puerto}` |
| `Ficha` | 32 bytes aleatorios en hexadecimal. Va en la cabecera `X-Avacom-Ficha` de **cada** petición |
| `Proceso` | Identificador del proceso, para comprobar que sigue vivo sin matarlo |

Está en datos de programa y no en el perfil del usuario porque el backend del
LMS puede correr con otra cuenta. La ficha no protege secretos, que ya van
cifrados; existe para que solo un programa de este equipo con permiso de
lectura sobre esa carpeta pueda preguntar.

### 3.2 · Transporte

| Regla | Valor |
|---|---|
| Dirección | Solo `127.0.0.1`. La API se niega a escuchar en otra interfaz y hay una prueba que lo fija |
| Puerto | Lo elige el sistema en cada arranque |
| Autenticación | `X-Avacom-Ficha` en cada petición. Sin ella o con una incorrecta, 401 y nada más. Comparación en tiempo constante |
| Métodos | `GET`, `HEAD` y dos `POST` que no escriben nada en la Biblioteca |
| Respuestas | `application/json; charset=utf-8`, salvo los bytes de un medio |
| Caché | `Cache-Control: no-store` en todas. Una capa intermedia que cachee anula la señal de cambio |
| Conexión | Se cierra tras cada respuesta |
| Tiempo de espera recomendado | 3 segundos |

### 3.3 · Versionado

El número de contrato sube **solo** cuando cambia la forma de una respuesta de
manera que rompa a quien ya la lee. Añadir un campo o una ruta no lo sube: el
LMS debe ignorar los campos que no conozca.

Los nombres de campo están fijados por las pruebas de la Biblioteca. Si alguien
los cambia, esas pruebas fallan, y el arreglo es subir el contrato, no tocar la
prueba.

### 3.4 · Capacidades

`capacidades` es un **campo de la respuesta de `/v1/salud`**, no una ruta.
Declara lo que esta versión del componente sabe hacer.

| Capacidad | Ruta que habilita | Hoy |
|---|---|---|
| `curso` | `/v1/cursos`, `/v1/curso/{ref}` | Siempre declarada |
| `medio` | `/v1/medio/{ref}` | Declarada en la aplicación |
| `leccion` | `/v1/leccion/{ref}` | Declarada en la aplicación |
| `evaluacion` | `/v1/evaluacion/{ref}` | Declarada en la aplicación |
| `comprobar` | `/v1/comprobar` | Declarada en la aplicación |
| `voz` | `/v1/voz/{ref}` | Declarada en la aplicación |

Reglas:

- Un componente que no declare `capacidades` equivale a la lista vacía.
- El LMS consulta la lista antes de usar cualquier ruta que no sea de las cinco
  originales. Lo que no está, **no se simula**: se degrada con una explicación.
- Aquí solo se declara lo que responde de verdad. Declarar una capacidad que no
  está implementada es peor que no declararla, porque el LMS la usaría.
- Las cinco opcionales dependen de que la aplicación inyecte una fuente de
  contenido. En la aplicación real siempre está; en las pruebas de la
  biblioteca sin interfaz puede no estarlo, y entonces la salud declara solo
  `curso` y las rutas opcionales responden 404.

Lo que la lista **no** dice, y conviene tener presente: describe el componente,
no el catálogo instalado. Un aula con solo cursos sin evaluaciones seguirá
declarando `evaluacion`. La propuesta para cerrarlo está en
[09-posibles-cambios-conexion-lms.md](09-posibles-cambios-conexion-lms.md).

### 3.5 · Las rutas

Resumen. La forma exacta de cada una está en [04-api-calls.md](04-api-calls.md).

| Ruta | Verbo | Capacidad | Qué devuelve |
|---|---|---|---|
| `/v1/salud` | GET | — | Componente, contrato, contadores, huella y capacidades |
| `/v1/catalogo` | GET | — | Elementos visibles, con cinco filtros combinables |
| `/v1/taxonomia` | GET | — | Raíz del árbol curricular, o los hijos de un padre |
| `/v1/elemento/{ref}` | GET | — | Un elemento suelto |
| `/v1/mostrar` | POST | — | Pide que un material aparezca en la pantalla del aula |
| `/v1/cursos` | GET | `curso` | Los cursos ofrecidos a este equipo |
| `/v1/curso/{curso_ref}` | GET | `curso` | Las secciones de un curso con sus materiales |
| `/v1/medio/{ref}[/ruta]` | GET, HEAD | `medio` | Los bytes descifrados de un material, por rangos. En un interactivo, un archivo interno |
| `/v1/leccion/{ref}` | GET | `leccion` | Los pasos de una lección, en orden |
| `/v1/evaluacion/{ref}` | GET | `evaluacion` | Las preguntas, sin clave ni retroalimentación |
| `/v1/comprobar` | POST | `comprobar` | Veredicto de una respuesta, y la retroalimentación solo al acertar |
| `/v1/voz/{ref}[/pregunta]` | GET | `voz` | La instrucción hablada de un elemento o de una pregunta |

### 3.6 · La señal de cambio

`huella_catalogo` son dieciséis caracteres derivados de lo que el LMS vería
ahora si pidiera el catálogo. Cambia cuando se instala o retira un paquete,
cuando la escuela desactiva o reactiva algo por política y cuando se publica
una versión nueva. No cambia por consultar, mostrar ni registrar uso.

Se compara por igualdad y nada más. **No es un contador.** El equipo del LMS
pidió un contador monótono llamado `generacion`; la Biblioteca publica la
huella en su lugar porque se deriva del estado y no hay que acordarse de
incrementarla en cada sitio que toca el catálogo. Si hace falta de verdad un
número que crezca, es una decisión con esquema detrás y hay que acordarla.

### 3.7 · La política de la escuela

El administrador puede desactivar por asignatura, nivel, grado, paquete, nodo
curricular o elemento. Eso se aplica **encima** del índice, sin modificarlo, y
**antes** de responder cualquier petición: catálogo, curso, elemento suelto,
medio, lección, evaluación y voz.

Lo desactivado no llega atenuado ni con una marca: no llega. Si el LMS pide por
referencia directa algo desactivado, recibe 403. Una sección sin materiales
visibles no se devuelve, y un curso sin nada visible no se ofrece.

### 3.8 · Las referencias estables

| Referencia | Qué identifica | Estable entre versiones |
|---|---|---|
| `curso_ref` | Un curso. Es la clave del paquete | Sí. Se forma con país, nivel, grado y materia y nunca cambia al republicar |
| `codigo` de una sección | Un nodo del árbol curricular. Es su `taxonomia_ref` | Sí, mientras el índice del curso conserve su código oficial |
| `elemento_ref` + `version` | Un material concreto | La referencia sale del título. Cambiar el título crea un material nuevo |
| `pregunta_ref` | Una pregunta dentro de una evaluación | Sí, mientras no se reordenen las preguntas |

---

## 4 · Cómo condiciona esto el consumo por parte del LMS

Cada regla del contrato tiene una consecuencia concreta en el código del LMS.

| Regla del contrato | Consecuencia en el LMS |
|---|---|
| La nota se relee en cada petición | No existe una variable que guarde puerto ni ficha entre llamadas |
| La ausencia de la Biblioteca es normal | La función de estado nunca lanza. Devuelve `disponible: false` con motivo y sugerencia. Hacia arriba es un 503, no un 500 |
| Solo loopback, tiempo corto | Un único cliente HTTP en todo el LMS apunta a `127.0.0.1`, con 3 segundos de espera. Si aparece otro, está mal |
| Capacidades antes de rutas opcionales | Cada ruta opcional exige su capacidad y responde 501 explicativo si falta. Sin `comprobar` no hay nota: el intento queda pendiente, no se inventa |
| Guardar referencias, nunca títulos | Las tablas del expediente tienen `curso_ref`, `leccion_codigo`, `evaluacion_ref`, `pregunta_ref` y `elemento_ref` con versión. El título solo se conserva como rótulo histórico que no se refresca y no decide nada |
| Lo que no llega, no existe | El LMS no marca lo desactivado: no lo tiene. Si lo tuviera, acabaría enseñándolo |
| No guardar catálogo propio | Ninguna tabla de curso, sección, lección, ítem, pregunta ni opción en el LMS. La estructura se resuelve en vivo por referencia. Una caché en memoria invalidada con la huella es lo máximo |
| La huella se compara por igualdad | El LMS guarda la última huella y recarga solo cuando cambia. Preferencia: huella; a falta de ella, contadores |
| La clave nunca sale | Una prueba del LMS recorre cada respuesta al estudiante buscando `clave`, `correcta` y variantes, y falla si aparecen. El esquema no tiene ninguna columna capaz de contener una clave |
| No hay ruta de escritura | El LMS no intenta instalar, desinstalar ni cambiar políticas. Sus propias rutas antiguas de administración de cursos responden un rechazo que nombra al dueño |
| «No se pudo comprobar» no es «no está» | La revisión de disponibilidad no escribe nada si no hubo catálogo válido. Una referencia sin oferta consultable se responde con la última revisión conocida y su fecha |

---

## 5 · Las pruebas que fijan el contrato

El contrato existe como código ejecutable en dos archivos de pruebas de la
Biblioteca. Estos son sus nombres, que se leen como frases:

**Contrato base**

- La salud dice quién es y qué contrato habla, y declara las capacidades.
- El catálogo trae los campos que el LMS necesita y no dice dónde vive el archivo.
- Los filtros funcionan y se combinan.
- La taxonomía se recorre de arriba abajo.
- Un elemento suelto se resuelve por referencia; una referencia inexistente da 404.
- Lo que la escuela desactivó desaparece para el LMS.
- El LMS puede pedir que se muestre un material; sin referencia da 400.
- Los cursos traen lo que el LMS necesita; un curso sin nada visible no se ofrece; un curso se abre por su referencia; ninguno revela dónde vive el paquete.
- Retirar un paquete lo quita del catálogo de inmediato y deja la salud coherente.
- La huella no cambia si no cambia nada; cambia al retirar y al desactivar por política.
- Las respuestas prohíben cachear.
- Sin ficha no se responde nada; con una inventada tampoco.
- El LMS no puede escribir nada.
- Solo escucha en el propio equipo.

**Capacidades opcionales**

- Con fuente la salud declara las capacidades nuevas; sin fuente las rutas no existen y la salud no las declara.
- El medio entrega los bytes con su tipo y respeta los rangos; HEAD da las cabeceras sin cuerpo.
- Un interactivo sirve su página de entrada por defecto y sus archivos internos.
- Una lección no tiene archivo y da 409; lo desconocido da 404.
- La política también protege los bytes.
- La lección trae sus pasos en orden.
- La evaluación trae las preguntas sin clave ni retroalimentación; un documento no es una evaluación.
- Comprobar devuelve el veredicto, y la retroalimentación solo al acertar; una pregunta abierta no se comprueba; sin datos da 400 y con pregunta desconocida 404.
- La voz de una pregunta se sirve como audio.
- Las capacidades siguen exigiendo la ficha.

---

## 6 · Lo que el contrato no ofrece, a propósito

- Ninguna ruta para instalar, desinstalar ni cambiar políticas. Instalar implica
  verificar una firma, comprobar la licencia y proyectar el índice; si eso se
  pudiera disparar desde fuera, se dispararía a mitad de una clase.
- Ninguna respuesta que diga dónde vive un archivo en disco.
- Ninguna respuesta que incluya una clave de respuesta.
- Ningún acceso a la base de datos de la Biblioteca ni a sus paquetes.

---

## 7 · Distancia entre lo que el LMS espera y lo que hay

El equipo del LMS escribió su propio contrato mínimo. Estas son las
diferencias con lo que hoy publica la Biblioteca. El detalle y las
recomendaciones están en [09-posibles-cambios-conexion-lms.md](09-posibles-cambios-conexion-lms.md).

| El LMS espera | La Biblioteca publica | Estado |
|---|---|---|
| Capacidades `banco` y `repaso` | No existen | 501 permanente en el LMS. Decidir si se implementan o se retiran |
| Un contador `generacion` | `huella_catalogo` | Funciona por el segundo nivel de preferencia. Cerrar la discrepancia en el documento del LMS |
| Árbol de tres niveles: secciones, lecciones, ítems | Dos niveles: secciones e ítems | El progreso del alumno se escribe por lección. Es el mayor trabajo pendiente |
| `padre` en cada sección del curso | No se devuelve | El árbol solo se puede reconstruir con `/v1/taxonomia` |
| Opciones de cada pregunta | No se guardan en el paquete | Solo se publica el enunciado. El alumno escribe la respuesta |
| `puntaje_maximo` por evaluación | No existe | Lo más parecido es la suma de los pesos |
| `calificable` por lección | No existe | Es lo que permitiría decir que una lección no tiene evaluación |
| `/v1/curso/{ref}/manifiesto` | No existe | Hoy hay que traerse el árbol entero para saber si cambió |
