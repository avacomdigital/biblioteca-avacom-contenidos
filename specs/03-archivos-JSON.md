# 03 · Archivos JSON para el formato de curso

| Campo | Valor |
|---|---|
| Estado | **PROPUESTA · NO IMPLEMENTADA.** Nada de lo que sigue existe en el código. Hoy los archivos de un curso son de texto plano y la herramienta que los lee solo entiende texto plano |
| Ámbito | Los archivos de datos que el equipo de contenido escribe dentro de la carpeta de un curso |
| Audiencia | Equipo técnico de la Biblioteca y equipo de contenido |
| Fecha | 14 de septiembre de 2026 |
| Depende de | Las decisiones abiertas de la sección 6. No se empieza sin cerrarlas |

> **Aviso que no se puede perder.** Este documento describe un cambio que
> **todavía no se ha hecho**. Quien cree cursos hoy debe seguir el formato de
> texto de [02-formato-de-curso.md](02-formato-de-curso.md). Quien lea este
> documento dentro de seis meses debe comprobar en el código si el recolector
> ya acepta JSON antes de dar nada por hecho.

---

## 1 · El problema

Hoy un curso se escribe en cinco archivos de texto con una gramática propia:
`curso.txt`, `indice.txt`, `leccion.txt`, los archivos con «evaluacion» en el
nombre y los archivos con «banco» en el nombre. La herramienta los lee línea a
línea, con reglas del tipo «lo que va antes de los dos puntos es la clave».

Ese diseño se eligió a propósito para que el equipo de contenido no tuviera que
escribir JSON ni saber programar, y ha funcionado. Pero tiene tres costes que
el análisis del formato v2 dejó a la vista, y que están documentados en
[08-analisis-formato-v2.md](08-analisis-formato-v2.md):

| Coste | Ejemplo real |
|---|---|
| **No hay forma de validar la forma.** Una clave desconocida no da error: se ignora o se convierte en un párrafo | Las instrucciones para el docente escritas como `PROFESOR:` se mostraron al alumno |
| **La gramática es implícita.** Cada lector la reimplementa y cualquier variación pasa en silencio | Las opciones `A)` `B)` `C)` de un cuestionario se descartaron sin aviso |
| **No hay integridad del archivo fuente.** Nada detecta que un archivo se editó a mano después de revisarlo, ni distingue una versión de otra | Un `peso: 1.5` se convierte en `1` sin que nadie lo sepa |

A eso se suma la motivación que originó esta propuesta: proteger los archivos
del curso de personas que no deberían tener acceso a su contenido.

### 1.1 · Lo que JSON resuelve y lo que no

Conviene ser exactos aquí, porque una expectativa equivocada produce un cambio
caro que no protege nada.

| Necesidad | ¿La resuelve pasar a JSON? | Qué la resuelve de verdad |
|---|---|---|
| Validar la forma de cada archivo antes de empaquetar | **Sí**, con un esquema JSON por tipo de archivo | Esta propuesta |
| Que una clave desconocida sea un error y no un párrafo | **Sí** | Esta propuesta |
| Detectar que un archivo cambió después de revisarlo | **Sí**, con una huella por archivo y una huella del curso | Esta propuesta, sección 3.7 |
| Que el contenido en el aula sea ilegible sin licencia | **No.** JSON es texto plano igual que `.txt` | **Ya está resuelto**: el paquete publicado va cifrado y firmado, y el aula nunca recibe los archivos fuente. Ver [01-Contrato-Biblioteca-LMS.md](01-Contrato-Biblioteca-LMS.md) |
| Que los archivos fuente no se lean en el equipo del equipo de contenido | **No.** Eso es control de acceso a carpetas, no formato | Permisos del sistema de archivos, o cifrar el archivo comprimido con el que se entrega el curso |

La frontera queda así: **los archivos fuente son material de trabajo del
equipo de contenido, y siempre estarán en claro donde se editan.** Lo que
protege al curso en el aula es el paquete cifrado, y eso no cambia con esta
propuesta. Lo que esta propuesta añade es integridad y validación de los
fuentes, y la posibilidad de detectar una edición no autorizada.

---

## 2 · Alcance

### 2.1 · Qué cambia

| Archivo de hoy | Archivo propuesto | Qué gana |
|---|---|---|
| `curso.txt` | `curso.json` | Validación de campos, versión como entero, clave del paquete normalizada |
| `indice.txt` | `indice.json` | Árbol explícito en lugar de sangría; código obligatorio en los nodos que reciben material |
| `leccion.txt` | `leccion.json` | Título, descripción y, por primera vez, notas por paso |
| `*evaluacion*.txt` | `evaluacion.json` | Preguntas con **opciones**, tipo declarado, peso entero y rúbrica estructurada |
| `*banco*.txt` | `banco.json` | Mismo cuerpo que la evaluación más las reglas de extracción como objeto |

### 2.2 · Qué no cambia

| Se conserva | Por qué |
|---|---|
| La estructura de carpetas: ruta por país, nivel, grado y materia; una carpeta por tema; materiales numerados | Sigue siendo el formato. País, nivel y grado siguen saliendo de la ruta |
| `guion.txt`, el texto de una lección completa | Es prosa con medios intercalados, escrita por pedagogos. JSON la haría ilegible para quien la escribe. Su gramática se revisa aparte, en la sección 6 |
| El paquete publicado, el manifiesto, el cifrado y la firma | No se tocan. Esta propuesta termina donde empieza el empaquetador |
| El contrato con el LMS | No cambia ninguna respuesta de la API. Lo que sí hace es **posible** publicar opciones y puntaje máximo, que hoy no existen en el paquete |

---

## 3 · Especificación

### 3.1 · Reglas comunes a todos los archivos

| Regla | Valor |
|---|---|
| Codificación | UTF-8 sin marca de orden de bytes |
| Nombres de campo | Minúsculas, palabras separadas por guion bajo, sin tildes |
| Campo `formato` | Entero, obligatorio en todos. Empieza en `1`. Sube cuando cambie la forma de manera que rompa a quien la lee |
| Esquemas | Un archivo de esquema JSON por tipo, en `esquema/json/`. La herramienta valida contra él antes de hacer nada más |
| Campo desconocido | **Error**, no aviso. Es el cambio de fondo respecto al texto |
| Números | Enteros donde hoy son enteros. Un peso con decimales es un error, no se redondea |
| Referencias | Toda referencia se normaliza a caracteres ASCII minúsculos, dígitos y guiones. La clave del paquete también |

### 3.2 · `curso.json`

```json
{
  "formato": 1,
  "titulo": "Literatura colombiana del siglo XIX",
  "materia": "Humanidades · Lengua Castellana",
  "idioma": "es",
  "version": 1,
  "descripcion": "Romanticismo y Modernismo. Ley 115 artículo 23.",
  "marco": "EBC Lenguaje 8-9"
}
```

| Campo | Obligatorio | Nota |
|---|---|---|
| `titulo`, `materia` | Sí | Igual que hoy |
| `idioma` | No, `es` | Código de dos letras |
| `version` | No, `1` | **Entero.** Hoy es texto libre y se ha visto `1.0` |
| `descripcion` | **Sí** | Hoy es recomendada y nadie avisa si falta. Pasa a obligatoria: es lo único que dice a qué marco responde el curso |
| `marco` | No | Nuevo. Nombre corto del marco curricular. El contrato del LMS lo pide |
| `clave`, `emisor` | No | Igual que hoy. La clave se normaliza a ASCII |

País, nivel y grado siguen sin escribirse: salen de la ruta. Un campo `pais`,
`nivel` o `grado` en este archivo es un error.

### 3.3 · `indice.json`

El árbol se escribe como árbol, no con sangría.

```json
{
  "formato": 1,
  "nodos": [
    {
      "tipo": "area",
      "codigo": "L115-A7",
      "nombre": "Humanidades, lengua castellana e idiomas extranjeros",
      "hijos": [
        {
          "tipo": "factor",
          "codigo": "EBC-LIT",
          "nombre": "Literatura",
          "hijos": [
            {
              "tipo": "estandar",
              "codigo": "EBC-9-LIT-01",
              "nombre": "Determino en las obras literarias latinoamericanas...",
              "hijos": [
                { "tipo": "tema", "codigo": "DBA-9-03", "nombre": "El Romanticismo en la novela colombiana" },
                { "tipo": "tema", "codigo": "DBA-9-04", "nombre": "El Modernismo y la renovación del lenguaje" }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

| Regla | Por qué |
|---|---|
| `tipo` es libre, pero debe ser el nombre real del nivel en el marco oficial | Igual que hoy. El LMS lo usa como etiqueta, no como lógica |
| `codigo` es **obligatorio en todo nodo que vaya a recibir material** y en todo nodo de tipo `leccion` si se adopta el formato v2 | Hoy es opcional y se ha visto un índice entero sin códigos. Sin código, el enganche de las carpetas falla y el progreso del alumno no sobrevive a un cambio de nombre |
| `objetivo` es opcional por nodo | Hoy solo existe en la especificación técnica |
| Un `codigo` repetido es un error | Hoy se desambigua añadiendo el número de línea, en silencio |

### 3.4 · `leccion.json`

Opcional, dentro de la carpeta de un tema.

```json
{
  "formato": 1,
  "titulo": "Función lineal y razón de cambio",
  "descripcion": "Una sesión de cincuenta minutos.",
  "notas": {
    "01": "Lectura previa",
    "99": "Cierre de la sesión"
  }
}
```

`notas` es nuevo: una nota para el docente por número de material. Hoy solo
existe en la especificación técnica.

### 3.5 · `evaluacion.json`

Es el archivo que más gana, porque resuelve el problema de fondo que
[09-posibles-cambios-conexion-lms.md](09-posibles-cambios-conexion-lms.md)
identifica como crítico: el paquete no guarda opciones.

```json
{
  "formato": 1,
  "titulo": "Evaluación · Función lineal",
  "preguntas": [
    {
      "tipo": "opcion_unica",
      "enunciado": "¿Qué palabra usas para hablar de ti mismo?",
      "opciones": [
        { "texto": "You" },
        { "texto": "I", "correcta": true },
        { "texto": "He" }
      ],
      "peso": 1,
      "dificultad": "baja",
      "retro": "I significa «yo» y siempre se escribe con mayúscula."
    },
    {
      "tipo": "respuesta_corta",
      "enunciado": "¿Cuál es la pendiente de la recta y = 3x - 5?",
      "respuesta": "3",
      "peso": 1,
      "dificultad": "baja"
    },
    {
      "tipo": "abierta",
      "enunciado": "Explica qué representa la pendiente en la vida diaria.",
      "peso": 2,
      "dificultad": "alta"
    }
  ],
  "rubrica": [
    { "criterio": "Interpretación", "espera": "Relaciona la pendiente con una razón de cambio real", "peso": 2 },
    { "criterio": "Argumentación", "espera": "Justifica con lenguaje matemático apropiado", "peso": 1 }
  ]
}
```

| Regla | Nota |
|---|---|
| `tipo` es obligatorio y cerrado: `opcion_unica`, `opcion_multiple`, `verdadero_falso`, `respuesta_corta`, `numerica`, `abierta` | Unifica el vocabulario de las dos vías de hoy, que difieren |
| Con `opciones`, la correcta se marca **en la propia opción**, nunca por letra ni por posición | Reordenar las opciones no cambia la respuesta. Es lo que el formato v2 hacía mal |
| `respuesta_corta` y `numerica` llevan `respuesta` | Se compara como hoy: sin distinguir mayúsculas ni espacios sobrantes |
| `abierta` no lleva respuesta y **exige** `rubrica` | Hoy es un aviso. Pasa a error |
| `peso` es entero | Hoy un decimal se convierte en 1 en silencio |
| `puntaje_maximo` opcional a nivel de evaluación | Si no se escribe, es la suma de los pesos. Es lo que el LMS necesita para calcular la nota |

Lo que va al paquete: el enunciado, las opciones **sin la marca de correcta**,
y la clave de respuesta en la columna que ya existe y que nunca sale. Publicar
las opciones por la API exige una tabla nueva en el manifiesto; está en la
sección 5.

### 3.6 · `banco.json`

El mismo cuerpo que la evaluación, más las reglas como objeto:

```json
{
  "formato": 1,
  "titulo": "Banco · Función lineal",
  "extraer": 4,
  "por_dificultad": { "baja": 1, "media": 2, "alta": 1 },
  "preguntas": [ ]
}
```

Las comprobaciones de hoy se conservan y pasan de aviso a error: al menos el
doble de preguntas de las que se extraen, y de cada dificultad tantas como pide
la mezcla.

### 3.7 · Integridad de los fuentes

Es la parte que responde a la motivación original, dentro de lo que el formato
puede responder.

| Pieza | Qué es |
|---|---|
| `huellas.json` en la raíz del curso | Generado por la herramienta al revisar. Una huella BLAKE2b-256 por cada archivo del curso, más una huella del conjunto |
| Comprobación al armar | Si un archivo no coincide con su huella, la herramienta se detiene y dice cuál. Es la forma de saber que algo se editó después de la última revisión |
| Firma opcional del conjunto | El revisor pedagógico puede firmar `huellas.json` con una clave propia. Al armar se comprueba la firma. Así «revisado» deja de ser una palabra y pasa a ser un hecho verificable |

Lo que esto **no** hace: ocultar el contenido. Para entregar un curso a otra
persona sin que lo lea un tercero, se comprime y se cifra el archivo entregado
con una herramienta corriente. Eso es transporte, no formato, y queda fuera.

---

## 4 · Impacto

| Dónde | Qué cambia | Tamaño |
|---|---|---|
| Recolector | Leer JSON y validar contra esquema antes de recorrer nada. Mantener el lector de texto durante la transición | Medio |
| Conversor nuevo | Un comando `convertir` que pasa un curso de texto a JSON, para migrar los cursos existentes sin reescribirlos a mano | Pequeño |
| Esquemas | Cinco archivos de esquema JSON en `esquema/json/` | Pequeño |
| Plantilla | `contenido/PLANTILLA` con los cinco archivos en JSON y sus comentarios convertidos a un `LEEME` | Pequeño |
| Empaquetador y manifiesto | Tabla nueva `p_opcion` para guardar las opciones sin la marca de correcta. Es lo que después permite publicarlas por la API | Medio |
| API local | Publicar `opciones` en `/v1/evaluacion/{ref}` y `puntaje_maximo`. Añadir campos no sube el contrato | Pequeño |
| Documentación | El 02 y el 05 pasan a describir JSON; la presentación se regenera | Medio |
| LMS | Nada obligatorio. Gana opciones y puntaje máximo, que su contrato ya pedía | Ninguno |

---

## 5 · Compatibilidad y migración

1. **Las dos vías conviven** durante una versión. La herramienta lee JSON si
   existe y texto si no. Un curso no puede mezclar las dos: es un error.
2. **El conversor** produce JSON a partir del texto y deja el texto intacto.
   Se revisa el resultado y se borra el texto a mano.
3. **Los paquetes ya publicados no se tocan.** Esta propuesta cambia los
   fuentes, no el paquete. Un curso republicado desde JSON conserva su clave y
   sube su versión, como siempre.
4. **La versión del formato de paquete no cambia** mientras no se añada la tabla
   de opciones. En cuanto se añada, sube, y hay que desplegar la Biblioteca en
   las aulas antes de publicar el primer paquete con ella.

---

## 6 · Clarificación · decisiones que hay que tomar antes de empezar

Ninguna se responde aquí por inferencia. Cada una cambia el plan.

| ID | Pregunta | Por qué bloquea |
|---|---|---|
| J-01 | ¿Quién escribe el JSON: el equipo de contenido a mano, o un editor que lo genera? | Si es a mano, la ganancia de validación se paga con más errores de sintaxis. Si es un editor, hay que construirlo primero |
| J-02 | ¿`guion.txt` se queda como texto, o se define `guion.json` con bloques? | Texto es legible para quien lo escribe; JSON permite validar las etiquetas nuevas del formato v2. Recomendación: texto con gramática cerrada y validada |
| J-03 | ¿La evaluación del formato v2 vive en `evaluacion.json` o dentro del guion? | Es la decisión Q-39 del documento 09. Sin ella no se sabe qué archivo lleva las preguntas |
| J-04 | ¿Se firma `huellas.json`? ¿Con qué clave y quién la custodia? | Es la única parte de esta propuesta que convierte «revisado» en verificable |
| J-05 | ¿Se migran los tres cursos existentes con el conversor o se reescriben? | Cambia el alcance de las pruebas |
| J-06 | ¿Los campos desconocidos son error en todos los archivos, o aviso en los de contenido pedagógico? | Error es más seguro y más molesto. Recomendación: error |

---

## 7 · Tareas, en orden

Solo se ejecutan con J-01 a J-06 cerradas.

| ID | Tarea | Verificación |
|---|---|---|
| T-J01 | Escribir los cinco esquemas JSON | Un archivo de ejemplo válido por esquema, y uno inválido por cada regla que rechace |
| T-J02 | Lector de JSON en el recolector, con validación previa | El curso de ejemplo en JSON produce la misma especificación que el de texto |
| T-J03 | Conversor de texto a JSON | Convertir los tres cursos del repositorio y comparar especificaciones |
| T-J04 | Huellas por archivo y comprobación al armar | Editar un archivo tras revisar hace que armar se detenga y nombre el archivo |
| T-J05 | Tabla de opciones en el manifiesto y en el empaquetador | El paquete construido tiene las opciones sin la marca de correcta |
| T-J06 | Publicar opciones y puntaje máximo en la API | La prueba de la evaluación comprueba que ninguna opción trae indicador de corrección |
| T-J07 | Plantilla y documentación | El 02, el 05 y la plantilla describen JSON; este documento pasa a **Implementado** |

---

## 8 · Lo que este documento no cambia

- El paquete publicado, su cifrado, su firma y su licencia.
- El contrato con el LMS. Solo añade campos, que no suben la versión.
- La estructura de carpetas y la regla de que país, nivel y grado salen de la ruta.
- La regla de que la clave de respuesta nunca sale de la Biblioteca.
