<p align="center">
  <img
    src="https://www.avacomworld.com/SVG/avacom-logo-horizontal.svg"
    alt="AVACOM Logo"
    width="250"
  />
</p>

<h1 align="center">AVACOM - Biblioteca v.0.1 </h1>
<p align="center">
  <a href="https://www.python.org/downloads/">
    <img src="https://img.shields.io/badge/Python-3.12.10-3776AB?logo=python&amp;logoColor=white" alt="Python 3.12.10" />
  </a>
  <a href="https://dotnet.microsoft.com/">
    <img src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&amp;logoColor=white" alt=".NET 9.0" />
  </a>
  <img src="https://img.shields.io/badge/status-prototype-F59E0B" alt="Status: prototype" />
  <img src="https://img.shields.io/badge/platform-Windows%2011%20(OPS%20AVACOM)-0078D4?logo=windows11&amp;logoColor=white" alt="Windows 11 - OPS AVACOM" />
  <img src="https://img.shields.io/badge/platform-Android%2014-3DDC84?logo=android&amp;logoColor=white" alt="Android 14" />
</p>

La biblioteca de contenido educativo de AVACOM. Una aplicación de escritorio
que corre en el equipo maestro del aula, dentro de la pantalla interactiva de
86 pulgadas, con un solo trabajo: guardar el material educativo de forma que
nadie pueda copiarlo, y mostrarlo en clase.

**No es el LMS.** El LMS, AVACOM OPS, lleva alumnos, grupos, exámenes y
calificaciones, y se desarrolla en otro repositorio. Los dos productos se hablan
por una API local de solo lectura que escucha únicamente en el propio equipo.
Esa separación es deliberada y está documentada en [specs/00-Introduccion.md](specs/00-Introduccion.md).

**El aula no tiene internet.** Todo lo que hay aquí funciona con el cable de red
desconectado, y esa condición manda sobre cualquier otra decisión.

---

## Índice

- [Estructura del proyecto](#estructura-del-proyecto)
- [Requisitos](#requisitos)
- [Arranque rápido](#arranque-rápido)
- [Comandos de .NET](#comandos-de-net)
- [Herramientas de contenido](#herramientas-de-contenido)
- [El instalador](#el-instalador)
- [Documentación](#documentación)
- [Cómo está hecho, en cinco minutos](#cómo-está-hecho-en-cinco-minutos)
- [Decisiones que ya están tomadas](#decisiones-que-ya-están-tomadas)
- [Si algo falla](#si-algo-falla)
- [Contribuir](#contribuir)

---

## Estructura del proyecto

```
AVACOM_CONTENIDO_VERSION02/
├── README.md                        este archivo
├── CONTRIBUTING.md                  cómo se trabaja en este repositorio
├── CHANGELOG.md                     qué cambió en cada versión
├── ESTANDAR-CONTENIDO.txt           estándar para el equipo de contenido (se entrega suelto)
├── INVENTARIO.txt                   qué es cada archivo y por qué está
│
├── specs/                           documentación técnica, en formato spec-driven
│   ├── 00-Introduccion.md           la conexión Biblioteca ↔ LMS en su conjunto
│   ├── 01-Contrato-Biblioteca-LMS.md   cifrado, licencia y el contrato de la API
│   ├── 02-formato-de-curso.md       la guía completa del formato de curso
│   ├── 03-archivos-JSON.md          PROPUESTA no implementada: archivos de texto → JSON
│   ├── 04-api-calls.md              cada ruta de la API, con la lista de capacidades
│   ├── 05-resumen-formato-curso.md  versión corta del formato, con errores que evitar
│   ├── 06-ejemplo-curso-matematicas.md
│   ├── 07-ejemplo-curso-exploracion.md
│   ├── 08-analisis-formato-v2.md    qué cambia un curso recibido en formato nuevo
│   ├── 09-posibles-cambios-conexion-lms.md   impacto del formato v2 en el LMS
│   ├── 10-instalador.md             especificación del instalador
│   ├── 11-spec-driven-integracion-lms-v03.md   histórico, parcialmente superado
│   └── presentacion-formato-curso.html        presentación del formato para el equipo de contenido
│
├── app-biblioteca/                  la aplicación y su motor · lo que se compila
│   ├── AvacomBiblioteca.sln
│   ├── global.json                  fija el SDK de .NET · dotnet se ejecuta desde aquí
│   ├── LEEME.txt                    mapa del código, archivo por archivo
│   ├── src/
│   │   ├── Avacom.Contenido/        el motor: cifrado, paquetes, índice, medios, API local
│   │   ├── Avacom.Contenido.Consola/   comprobación de punta a punta, sin interfaz
│   │   └── Avacom.Biblioteca.App/   la aplicación MAUI para Windows
│   └── tests/
│       └── Avacom.Contenido.Tests/  compatibilidad de cifrado, servidor de medios, contrato de la API
│
├── contenido/                       donde trabaja el equipo de contenido
│   ├── PLANTILLA/                   se copia para empezar un curso nuevo
│   └── CO/secundaria/09/humanidades/   curso de ejemplo escrito con carpetas
│
├── paquetes/                        herramientas en Python del equipo de contenido
│   ├── avacom_recolector.py         carpetas → especificación · comandos revisar y armar
│   ├── avacom_leccion.py            guion.txt → página de la lección
│   ├── avacom_empaquetador.py       especificación → paquete en claro
│   ├── avacom_publicar.py           cifra, firma y emite la licencia del equipo
│   ├── avacom_cripto.py             Ed25519, AES-256-GCM, X25519
│   ├── specs/                       especificaciones de los dos paquetes de ejemplo
│   └── LEEME.txt · COMO-CARGAR-CONTENIDO.txt
│
├── esquema/
│   └── contenido.sql                ocho tablas y tres vistas · todo lo que el componente escribe en disco
│
├── installer/                       Inno Setup · el .exe NO se versiona, va en Releases
│   ├── avacom-biblioteca.iss
│   ├── construir.ps1
│   └── LEEME.txt
│
├── trabajo/                         se genera al probar · paquetes, licencia y claves de ESTE equipo · ignorado por git
│
├── PROBAR-TODO.cmd · probar-todo.ps1          prueba completa en siete etapas
├── EJECUTAR-APLICACION.cmd                    arranca la aplicación
├── preparar-trabajo.ps1                       construye la carpeta trabajo/
├── PROBAR-API.cmd · probar-api.ps1            prueba la API local en una ventana táctil
└── consultar-api.ps1                          consulta la API local desde consola
```

Para generar este árbol en la propia máquina, desde la raíz:

```bash
tree -L 2 -I 'bin|obj|trabajo|payload|dist' --dirsfirst
```

En PowerShell, que no trae `tree` con filtros, el equivalente es `tree /F /A`
y quitar a mano `bin`, `obj` y `trabajo`.

---

## Requisitos

| Qué | Versión | Para qué |
|---|---|---|
| Windows | 10 versión 1809 o superior, x64 | El componente de navegación incrustado no es fiable en versiones anteriores |
| SDK de .NET | La que fija `app-biblioteca/global.json` (hoy 10.0.100, con avance a la última característica) | Compilar y ejecutar la aplicación |
| Carga de trabajo MAUI | `dotnet workload install maui` | La aplicación de escritorio |
| Python | 3.12 o superior, con «Add Python to PATH» | Solo para el equipo de contenido: armar y firmar paquetes. **En el aula no hay Python** |
| Paquetes de Python | `py -3 -m pip install cryptography pillow reportlab` | Cifrado, medios de muestra |
| Inno Setup 6 | `winget install --id JRSoftware.InnoSetup` | Solo para construir el instalador |
| FFmpeg | Opcional | Solo para regenerar los vídeos de muestra |

Comprobar la versión del SDK con:

```bash
dotnet --version
```

No hace falta instalar el tiempo de ejecución del SDK de aplicaciones de
Windows: la aplicación se compila autocontenida. Pesa más y a cambio se lleva a
un aula sin preparar nada.

---

## Arranque rápido

**Probar que todo funciona**, la primera vez en una máquina. Corre siete etapas
y para en la primera que falle diciendo cuál es:

```bash
PROBAR-TODO.cmd
```

| Etapa | Qué comprueba |
|---|---|
| 1 | Que están las herramientas |
| 2 | Que se construye la carpeta de trabajo: paquetes, claves del equipo y licencia |
| 3 | Lo que hay dentro de un paquete publicado |
| 4 | Que el cifrado del componente y el del empaquetador son el mismo, byte a byte |
| 5 | El componente de punta a punta, sin interfaz |
| 6 | Qué pasa si alguien se lleva un paquete a otro equipo |
| 7 | Que la aplicación compila |

**Arrancar la aplicación.** Si la carpeta `trabajo` no existe, la prepara antes:

```bash
EJECUTAR-APLICACION.cmd
```

Dentro de la aplicación, la primera vez: pestaña **Administración**, elegir
`trabajo\lic\licencia.json`, «Revisar e instalar». Después, pestaña **Contenido
AVACOM**: ahí se enciende la API local y aparece la nota de enlace que el LMS
lee.

---

## Comandos de .NET

Todos se ejecutan **desde `app-biblioteca`**, porque ahí vive el `global.json`
que fija la versión del SDK. Lanzados desde la raíz, ese anclaje se pierde sin
avisar.

```bash
cd app-biblioteca
```

| Para qué | Comando |
|---|---|
| Restaurar paquetes | `dotnet restore` |
| Compilar toda la solución en Debug | `dotnet build` |
| Compilar en Release | `dotnet build -c Release` |
| Ejecutar la aplicación | `dotnet run --project src\Avacom.Biblioteca.App` |
| Ejecutar las pruebas | `dotnet test tests\Avacom.Contenido.Tests` |
| Comprobar el motor de punta a punta, sin interfaz | `dotnet run --project src\Avacom.Contenido.Consola -- ..\trabajo` |
| Publicar el binario que empaqueta el instalador | `dotnet publish src\Avacom.Biblioteca.App -c Release -r win-x64 --self-contained true -o ..\installer\payload` |

La primera compilación tarda unos minutos: descarga los paquetes y compila. La
publicación en Release compila ReadyToRun y tarda más.

**Las pruebas que más importan** están en `CompatibilidadCriptoTests`: comparan
contra vectores fijos que produjo el empaquetador en Python. Si fallan, el
componente y el empaquetador han dejado de hablar el mismo idioma y nada más va
a funcionar.

---

## Herramientas de contenido

Son de Python y las usa el equipo de contenido, nunca el aula. Se ejecutan desde
la raíz del proyecto.

| Paso | Comando | Qué hace |
|---|---|---|
| Revisar un curso | `py -3 paquetes\avacom_recolector.py revisar "contenido\CO\secundaria\09\humanidades"` | Dice qué falta y qué conviene arreglar. Se repite hasta que salga limpio |
| Armar | `py -3 paquetes\avacom_recolector.py armar "contenido\CO\secundaria\09\humanidades"` | Genera la especificación y copia los medios a `paquetes\specs` |
| Construir | `py -3 paquetes\avacom_empaquetador.py ejemplos C:\avacom\claro` | Construye en claro todas las especificaciones de `paquetes\specs` |
| Verificar | `py -3 paquetes\avacom_empaquetador.py verificar C:\avacom\claro\<paquete>` | **Último momento** en que el contenido se puede leer en claro |
| Publicar | `py -3 paquetes\avacom_publicar.py publicar C:\avacom\claro\<paquete> C:\avacom\pub` | Cifra cada archivo, cifra el manifiesto y firma |
| Licencia del equipo | `py -3 paquetes\avacom_publicar.py licencia <nodo> <destino> <paquete>...` | Una por equipo, con la clave de cada paquete envuelta para él |

El formato que estas herramientas leen está en
[specs/02-formato-de-curso.md](specs/02-formato-de-curso.md). El flujo completo
del lado técnico está en `paquetes/COMO-CARGAR-CONTENIDO.txt`.

---

## El instalador

Un solo archivo `.exe` construido con Inno Setup. El profesor hace doble clic.

```bash
powershell -ExecutionPolicy Bypass -File installer\construir.ps1
```

Publica la aplicación en Release y la comprime. Deja el resultado en
`installer\dist\AVACOM-Biblioteca-0.1.0-setup.exe`, unos 80 MB. Si ya se
publicó antes y solo cambió el guion del instalador, `-SoloCompilar` reutiliza
el payload.

**El instalador no está en el repositorio.** Pesa unos 80 MB y GitHub advierte a
partir de 50 MB y rechaza archivos de más de 100 MB. Además cada versión añadiría
otro tanto al historial para siempre. Por eso:

- `installer/payload/` y `installer/dist/` están en `.gitignore` y **no se versionan**.
- El `.exe` de cada versión se publica como *asset* de un **Release de GitHub**,
  con su hash SHA-256 al lado para poder verificarlo.
- Lo que sí está en el repositorio es todo lo necesario para reconstruirlo:
  el guion `.iss`, el script `construir.ps1` y el código fuente.

Existe un interruptor `-ConContenidoDemo` que mete dentro del instalador la
carpeta `trabajo` completa, **incluida la clave privada del equipo de
desarrollo**. Sirve para demostrar el producto sin provisionar nada. **No se
usa para lo que se entrega a un colegio**, porque rompería el modelo de una
licencia por equipo. Ver `installer/LEEME.txt`.

Tres decisiones del instalador que no se pueden deshacer sin romper el producto:
no se empaqueta como MSIX, la ruta de instalación no puede superar 180
caracteres, y `%ProgramData%\AVACOM\` es territorio compartido con AVACOM OPS.
Los motivos están en [specs/10-instalador.md](specs/10-instalador.md).

---

## Documentación

Toda la documentación técnica vive en `specs/`, numerada en orden de lectura.
Cada documento dice en su cabecera si describe lo que existe o lo que se
propone.

| Documento | Para quién | Qué contiene |
|---|---|---|
| [00 · Introducción](specs/00-Introduccion.md) | Todos | La conexión Biblioteca ↔ LMS en su conjunto |
| [01 · Contrato](specs/01-Contrato-Biblioteca-LMS.md) | LMS y Biblioteca | Cifrado, licencia y el contrato de la API |
| [02 · Formato de curso](specs/02-formato-de-curso.md) | Equipo de contenido | La guía completa del formato |
| [03 · Archivos JSON](specs/03-archivos-JSON.md) | Equipo técnico | **Propuesta, no implementada** |
| [04 · Llamadas a la API](specs/04-api-calls.md) | LMS | Cada ruta, con la lista de capacidades |
| [05 · Resumen del formato](specs/05-resumen-formato-curso.md) | Equipo de contenido | Versión corta, con errores que evitar |
| [06](specs/06-ejemplo-curso-matematicas.md) · [07](specs/07-ejemplo-curso-exploracion.md) | Equipo de contenido | Dos cursos reales explicados |
| [08 · Formato v2](specs/08-analisis-formato-v2.md) | Ambos equipos | Análisis de un curso recibido en formato nuevo |
| [09 · Posibles cambios](specs/09-posibles-cambios-conexion-lms.md) | Ambos equipos | Impacto del formato v2 en la conexión con el LMS |
| [10 · Instalador](specs/10-instalador.md) | Quien distribuye | Especificación, ya implementada |
| [11 · Integración LMS v03](specs/11-spec-driven-integracion-lms-v03.md) | Histórico | La especificación original, parcialmente superada |

Fuera de `specs/`, tres documentos se entregan sueltos a quien los necesita:
`ESTANDAR-CONTENIDO.txt` al equipo de contenido, `app-biblioteca/LEEME.txt` al
que va a tocar el código, e `INVENTARIO.txt` a quien quiera saber qué es cada
archivo.

---

## Cómo está hecho, en cinco minutos

Hay tres piezas y conviene entender la relación antes de tocar código.

**El paquete.** Una carpeta con cuatro cosas: `formato.json`, la ficha en claro
y lo único legible sin licencia; `manifiesto.enc`, la base de datos del paquete
cifrada entera, con la estructura, los elementos, las preguntas y las claves de
respuesta; `medios/`, cada archivo cifrado por separado con nombre de huella; y
`firma.sig`, la firma Ed25519 sobre las huellas de todo lo anterior.

**El índice.** Una base SQLite en el equipo con los metadatos de lo instalado.
Ni un byte de contenido. Es una **proyección** de los manifiestos, no un
catálogo propio: si se borra, se reconstruye escaneando los paquetes y da
exactamente lo mismo. La etapa 5 de la prueba lo demuestra.

**La licencia.** Un archivo firmado que, para cada paquete autorizado, lleva su
clave de cifrado envuelta para la clave pública de **un** equipo concreto.
Copiar un paquete a otra máquina no sirve: sin la clave privada de ese equipo,
los medios son ruido. La etapa 6 lo demuestra.

**Reproducir vídeo cifrado.** El reproductor quiere una dirección, no un flujo
de bytes. Escribir el vídeo descifrado a un archivo temporal dejaría el
contenido en claro en disco. Lo que se hace: un servidor diminuto que escucha
solo en `127.0.0.1`, con una ficha aleatoria por material, que descifra cada
bloque de un megabyte en el momento en que el reproductor lo pide por rango. Al
cerrar el visor la ficha se anula. Por eso se cifra por bloques y no de una
pieza.

---

## Decisiones que ya están tomadas

Cada una tiene su motivo escrito en el código, junto a la línea que la aplica.

| Decisión | Por qué |
|---|---|
| La aplicación se compila **sin empaquetar** (`WindowsPackageType = None`) | Empaquetada corre dentro del aislamiento de red de Windows, que bloquea las conexiones al propio equipo. El servidor de medios y la API local escuchan justo ahí |
| El índice es una proyección, nunca una fuente de verdad | Si el índice y el manifiesto discrepan, el que se equivoca es el índice |
| La clave de respuesta no sale del manifiesto | El tipo que llega a la interfaz y a la API no tiene ni un campo donde meterla. Solo se compara, en tiempo constante |
| El manifiesto descifrado nunca se queda en disco | Lleva las claves de respuesta de todos los exámenes del año |
| El modo repaso no genera nota | Deja constancia de que se abrió un material y nada más. La nota es del LMS |
| Todo en español, incluidos los nombres de clases y métodos | El equipo es hispanohablante y el dominio también. Mezclar `Package` con `paquete` cuesta más de lo que parece |
| Nada por debajo de 20 puntos, áreas táctiles grandes, tipografía del sistema | La pantalla mide 86 pulgadas, el último alumno está a cuatro metros, y una fuente que no carga sin conexión deja la interfaz ilegible |

Lo que está declarado y todavía sin hacer: el visor de cursos SCORM, que espera
a que se cierre quién registra tiempo y calificación; y el editor de contenido
propio. Las dos pestañas lo dicen en pantalla.

---

## Si algo falla

| Síntoma | Qué mirar |
|---|---|
| El restore falla por una versión de paquete | Las versiones del csproj usan comodines. Resolver la última con `dotnet add package <nombre>` |
| Falta una carga de trabajo | `dotnet workload install maui` |
| Las pruebas de compatibilidad de cifrado fallan | Casi siempre es la serialización canónica del JSON, que tiene que coincidir byte a byte con la del empaquetador. Ver `Canonico.cs` |
| Un medio da error de autenticación al abrirlo | La etiqueta con la que se deriva su clave: es el nombre en claro, sin el `.enc`. La regla vive en un solo sitio, `ResolutorDeMedios.Etiqueta` |
| Un vídeo se corta a la mitad | El flujo tiene que quedarse con su propia copia de la clave del paquete. Ver `StreamDeMedio` |
| El componente dice que no hay licencia para un paquete | La licencia se emite para un equipo concreto. Si se regeneraron las claves, `preparar-trabajo.ps1` la vuelve a emitir |
| SQLite da un error de entrada y salida | Casi siempre es una unidad de red. SQLite necesita bloqueo de archivos: trabajar en disco local |
| La aplicación instalada abre una vez y no la segunda | Probar siempre dos arranques seguidos. Hubo un bug real que solo aparecía a partir del segundo |

---

## Contribuir

Las ramas, el formato de los commits, la plantilla de pull request y lo que se
revisa antes de aprobar están en [CONTRIBUTING.md](CONTRIBUTING.md). Lo que
cambió en cada versión, en [CHANGELOG.md](CHANGELOG.md).

Tres cosas que **nunca** se suben al repositorio, y que `.gitignore` ya excluye:
la clave privada del emisor en `paquetes/claves/`, la carpeta `trabajo/` con las
claves y la licencia de un equipo concreto, y los binarios del instalador. La
clave privada del emisor firma el catálogo entero de AVACOM: no se copia a un
portátil, no entra en ningún repositorio y no viaja en un correo.
