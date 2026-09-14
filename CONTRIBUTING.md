# Cómo se trabaja en este repositorio

Para quien va a tocar el código o la documentación. Es corto a propósito: son
las reglas que, si se rompen, cuestan una tarde a otra persona.

---

## 1 · Ramas

| Rama | Qué es | Quién escribe |
|---|---|---|
| `main` | Lo que se puede compilar, probar e instalar en cualquier momento | Solo por pull request aprobado |
| `feature/<que-hace>` | Una funcionalidad nueva | Quien la desarrolla |
| `fix/<que-arregla>` | Una corrección | Quien la arregla |
| `docs/<que-documenta>` | Solo documentación | Quien la escribe |

Nombres en minúsculas, palabras separadas por guiones, en español y sin tildes:
`feature/opciones-en-evaluacion`, `fix/orden-de-items-en-curso`.

Una rama vive lo que tarda su pull request. Una rama de tres semanas es una
señal de que el cambio hay que partirlo.

## 2 · Commits

El historial ya tiene una convención y se conserva: **prefijo, dos puntos, y
qué cambia**, en español y en una línea.

```
Feature: API local publica las capacidades opcionales
Bugfix: Los items de una seccion se ordenan por su numero, no por titulo
Docs: Contrato con el LMS movido a specs/01
Refactor: FuenteDeContenido separa la API del gestor de paquetes
Test: Vectores de cifrado para el formato de paquete 2
```

| Prefijo | Cuándo |
|---|---|
| `Feature` | Algo nuevo que antes no se podía hacer |
| `Bugfix` | Algo que hacía lo que no debía |
| `Docs` | Solo documentación |
| `Refactor` | Cambia la forma, no el comportamiento. Las pruebas no se tocan |
| `Test` | Solo pruebas |
| `Build` | Compilación, instalador, dependencias |

Cuerpo del commit cuando haga falta explicar **por qué**, no qué: el qué ya
está en el diff. Un commit que cambia el contrato de la API lo dice en el
cuerpo y nombra el documento de `specs/` que actualiza.

Un commit, un cambio. Mezclar una corrección con una funcionalidad hace que la
corrección no se pueda revertir sola.

## 3 · Pull requests

Se abre contra `main` con la plantilla de `.github/PULL_REQUEST_TEMPLATE.md`,
que pide tres cosas: qué cambia, por qué, y cómo se comprobó.

Antes de pedir revisión, quien abre el pull request ha ejecutado:

```bash
cd app-biblioteca
dotnet test tests\Avacom.Contenido.Tests
```

Y si el cambio toca el cifrado, los paquetes o la API local, también la prueba
completa desde la raíz:

```bash
PROBAR-TODO.cmd
```

Un pull request que rompe una prueba no se revisa hasta que la arregle o
explique en el cuerpo por qué la prueba estaba mal. **La regla de la casa: si
una prueba del contrato falla, el arreglo es subir la versión del contrato, no
tocar la prueba para que vuelva a pasar.**

## 4 · Qué se revisa

Quien revisa mira esto, en este orden. Lo demás es estilo y se comenta, no se
bloquea.

| # | Pregunta | Dónde suele fallar |
|---|---|---|
| 1 | ¿La clave de respuesta sigue sin poder salir? | Cualquier tipo nuevo que cruce a la interfaz o a la API con un campo donde quepa |
| 2 | ¿El cifrado sigue siendo compatible byte a byte con Python? | `Canonico.cs`, `CifradoArchivo.cs`, `Licencia.cs`. Las pruebas de compatibilidad tienen que seguir en verde |
| 3 | ¿La política de la escuela se aplica antes de entregar un byte? | Toda ruta nueva de la API y todo camino nuevo hacia `ResolutorDeMedios` |
| 4 | ¿Sigue escuchando solo en `127.0.0.1`? | `ApiLocal`, `ServidorDeMedios`. Hay una prueba, tiene que seguir |
| 5 | ¿Cambia la forma de alguna respuesta de la API? | Si rompe a quien ya la lee, sube `PuntoDeEnlace.Contrato` y se actualizan `specs/01` y `specs/04`. Si solo añade campos, no sube |
| 6 | ¿El índice sigue siendo una proyección? | Cualquier columna nueva del índice que no salga de un manifiesto lo convierte en un segundo catálogo |
| 7 | ¿Los comentarios explican por qué, no qué? | Un comentario que repite la línea de abajo sobra |
| 8 | ¿Está en español? | Nombres de clases, métodos, variables y comentarios. Es deliberado |
| 9 | ¿La documentación de `specs/` sigue diciendo la verdad? | Todo cambio de comportamiento visible actualiza el documento que lo describe en el mismo pull request |

Un revisor aprueba cuando puede explicar el cambio con sus palabras. Si no
puede, pide que se explique en el pull request, no que se le explique en
persona: la explicación tiene que quedar escrita.

## 5 · Documentación

- La técnica vive en `specs/`, numerada en orden de lectura. Un documento
  nuevo toma el siguiente número libre y se añade a la tabla de `README.md` y
  de `specs/00-Introduccion.md`.
- Cada documento dice en su cabecera si describe **lo que existe** o **lo que
  se propone**, y desde qué fecha. Una propuesta que se implementa cambia su
  estado en el mismo pull request que la implementa.
- Lo que se entrega suelto a otro equipo (`ESTANDAR-CONTENIDO.txt`) se escribe
  para quien no programa: tablas, ejemplos con archivos reales, nada de código.
- En español, con tildes. Frases cortas.

## 6 · Lo que nunca se sube

`.gitignore` ya lo excluye, pero conviene saber por qué:

| Qué | Por qué |
|---|---|
| `paquetes/claves/` | La clave privada del emisor firma el catálogo entero de AVACOM |
| `trabajo/` | Claves, licencia y paquetes de **un** equipo concreto. Es material de prueba |
| `installer/payload/`, `installer/dist/` | Generados. El `.exe` pesa unos 80 MB y se publica en Releases |
| `bin/`, `obj/`, `*.db` | Generados |

Si `git status` muestra algo de esa lista como modificado, es que alguien lo
subió antes de la regla. Se retira del índice con `git rm --cached` y se dice en
el pull request.

## 7 · Versiones y releases

- La versión visible está en dos sitios que tienen que coincidir:
  `ApplicationDisplayVersion` en el `.csproj` de la aplicación y `#define
  Version` en `installer/avacom-biblioteca.iss`.
- Cada versión tiene su entrada en `CHANGELOG.md`, escrita para quien instala,
  no para quien programa.
- El instalador se construye con `installer\construir.ps1` desde `main`, se
  calcula su SHA-256 y se sube como *asset* del Release junto con el hash. El
  `.exe` no entra en el repositorio.
