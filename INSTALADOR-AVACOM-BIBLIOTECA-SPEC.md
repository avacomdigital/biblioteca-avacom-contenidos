# AVACOM Biblioteca · Spec Driven Dev — Instalador para `/dist`

> Mismo formato que el spec del instalador de AVACOM OPS Master, adaptado a lo
> que este proyecto (`AVACOM_CONTENIDO_VERSION02`) realmente es. No es un
> ejercicio simétrico: **AVACOM Biblioteca no tiene backend, no tiene
> servicio de Windows y no expone ningún puerto de red.** Eso cambia media
> especificación, y está explicado en la sección 0 para que no se copie por
> costumbre algo que no aplica aquí.

---

## 0 · Lo que hace que este instalador sea distinto al de OPS Master

Antes de las reglas, cuatro hechos verificados en el código que gobiernan
todo lo demás:

1. **La app se compila deliberadamente SIN empaquetar**
   (`WindowsPackageType=None` en
   [Avacom.Biblioteca.App.csproj](app-biblioteca/src/Avacom.Biblioteca.App/Avacom.Biblioteca.App.csproj)).
   El motivo está escrito en el propio archivo: una app empaquetada corre
   dentro del aislamiento de red de Windows, que bloquea las conexiones al
   propio equipo — y el servidor de medios de este componente escucha
   justo ahí, en `127.0.0.1`. **Un instalador que produzca un MSIX/AppX
   reintroduce exactamente el bug que esta decisión evita.** El instalador
   tiene que copiar archivos a una carpeta, no empaquetar.

2. **No hay backend ni servicio.** La API HTTP
   ([ApiLocal.cs](app-biblioteca/src/Avacom.Contenido/Api/ApiLocal.cs)) vive
   **dentro del mismo proceso** de la app MAUI, se enciende cuando el
   profesor abre la pestaña Catálogo, y muere cuando cierra la app. No hay
   nada que registrar como `AVACOMBibliotecaBackend`, y no hay nada que
   arranque con Windows.

3. **El puerto es efímero y solo loopback.** `ApiLocal` y
   `ServidorDeMedios` piden el puerto 0 (que el sistema elige) y escuchan en
   `IPAddress.Loopback`. **No hace falta ninguna regla de Firewall.** Si en
   algún momento futuro esto cambiara (por ejemplo, si la retransmisión a
   tabletas terminara viviendo aquí en vez de en el backend de OPS Master),
   eso es un cambio de arquitectura que se decide aparte — no algo que este
   instalador deba anticipar.

4. **Hay un punto de encuentro deliberado con OPS Master, y no es un
   conflicto: es el contrato.** El componente escribe
   `%ProgramData%\AVACOM\contenido\enlace.json` a propósito, para que el
   backend de OPS Master (que puede correr con otra cuenta) lo encuentre. El
   instalador **no debe tratar `%ProgramData%\AVACOM\` como territorio
   exclusivo** de un solo producto: la tiene que crear con permisos que
   ambos productos puedan leer, y no debe borrarla al desinstalar si el otro
   producto sigue instalado.

---

## 1 · Requerimiento

Producir una versión instalable de AVACOM Biblioteca en `/dist`, para
Windows, al estilo wizard (sin comandos que el usuario tenga que escribir),
que coexista en el mismo equipo con AVACOM OPS Master sin pisar archivos,
configuración, procesos, puertos, logs ni accesos directos.

---

## 2 · Constitución

1. **No se modifica el comportamiento funcional existente** de AVACOM
   Biblioteca. El instalador resuelve distribución, instalación,
   configuración inicial, ejecución y desinstalación — nada más.
2. **No se produce un paquete MSIX/AppX ni se activa
   `WindowsPackageType`.** El resultado de `dotnet publish` se copia tal
   cual a la carpeta de instalación. Ver punto 0.1.
3. **Ruta propia, nunca compartida con OPS Master:**
   `C:\Program Files\AVACOM\Biblioteca\`. Ningún archivo de programa se
   escribe fuera de ahí, salvo lo del punto siguiente.
4. **`%ProgramData%\AVACOM\` se trata como territorio compartido, no
   propio.** El instalador puede crear `%ProgramData%\AVACOM\contenido\`
   (donde el componente deja `enlace.json` al arrancar) si no existe, con
   permisos de lectura para todos los usuarios locales, pero:
   - no coloca ahí archivos de programa;
   - no la borra al desinstalar mientras exista una instalación de OPS
     Master (comprobable por su propia clave de registro/carpeta);
   - no asume que es el único producto que escribe dentro.
5. **No hay servicio de Windows que instalar, ni puerto que abrir en el
   Firewall.** Ver puntos 0.2 y 0.3. Si una revisión futura de este
   instalador cree lo contrario, es señal de que la arquitectura cambió y
   hay que volver a esta especificación antes de tocar el instalador.
6. **El instalador no provee licencia ni claves de nodo ni paquetes de
   contenido.** Emitir la licencia de un equipo, generar su par de claves y
   publicar paquetes es un proceso del equipo técnico de contenido
   (`paquetes/COMO-CARGAR-CONTENIDO.txt`), deliberadamente separado y fuera
   de este instalador. El instalador entrega la aplicación vacía; el
   contenido y la licencia del aula llegan después, por el canal que ya
   existe.
7. **Python no se distribuye ni se invoca.** `paquetes/*.py` es
   herramienta del equipo de contenido. El aula no tiene Python
   (`LEEME.txt`, sección "Lo que hace falta instalar"), y el instalador no
   lo cambia.
8. **Todo lo que el instalador ejecute internamente (scripts `.bat`,
   PowerShell, comandos del sistema) corre sin pedir nada por teclado ni
   por consola.** El asistente wizard es la única interfaz.
9. **Sin internet.** Ni para descargar dependencias, ni para validar nada.
   El aula no tiene conexión y el instalador tiene que funcionar igual.
10. **Windows 10 versión 1809 (10.0.17763.0) como mínimo**, tal como fija
    el propio `.csproj` (`SupportedOSPlatformVersion` /
    `TargetPlatformMinVersion`). Por debajo de esa versión el componente de
    navegación incrustado no es fiable, y de él dependen el visor de
    documentos y el de material interactivo — el instalador debe
    comprobarlo y negarse a instalar si no se cumple, con un mensaje que lo
    diga.
11. **La app self-contained pesa lo que pesa** (del orden de 150-200 MB:
    hay medido 155 MB en una compilación Debug local), porque
    `WindowsAppSDKSelfContained=true` mete dentro del propio instalador el
    tiempo de ejecución del SDK de aplicaciones de Windows. Eso es
    deliberado — así el equipo maestro no depende de nada preinstalado — y
    el instalador debe comprobar espacio en disco acorde a ese tamaño real,
    no a una estimación optimista.

---

## 3 · Especificación

### 3.1 · Componentes a instalar

```
AVACOM Biblioteca
│
├── Aplicación
│   └── Avacom.Biblioteca.App (MAUI, self-contained win-x64, sin empaquetar)
│       + Avacom.Contenido (el motor: cifrado, paquetes, indice, medios)
│
├── Esquema
│   └── esquema\contenido.sql   ← ver 3.2, es lo unico de "trabajo\" que SI se distribuye
│
├── Config
│   └── nada que el instalador deba escribir; la app recuerda su carpeta de
│       trabajo en las Preferences del usuario, tal como hoy
│
├── Logs
│   └── esta version de la app no escribe log propio a disco. Si mas
│       adelante lo hace, ese log vive dentro de la carpeta de instalacion o
│       en %LOCALAPPDATA%, nunca mezclado con los logs de OPS Master
│
└── Uninstaller
```

No hay carpeta de "Backend" ni de "Runtime" independiente del propio
publish: el runtime ya viene dentro por ser self-contained. No hay carpeta
de "Servicios".

El usuario no necesita saber nada de esta estructura para usar el producto.

### 3.2 · El caso especial de `esquema\contenido.sql`

Hoy, `EstadoDelNodo.AsegurarEsquema()`
([EstadoDelNodo.cs:128-142](app-biblioteca/src/Avacom.Biblioteca.App/Paquetes/EstadoDelNodo.cs))
busca `esquema\contenido.sql` **dentro de la carpeta de trabajo que elige el
administrador** (hoy, en desarrollo, algo como
`C:\projects\...\trabajo\esquema\contenido.sql`), no dentro de la carpeta de
instalación de la app. En un equipo de aula, esa carpeta de trabajo la
prepara el equipo técnico junto con la licencia y las claves del nodo — el
instalador no la crea.

Lo único que el instalador puede y debe hacer, sin tocar ese mecanismo: **incluir una copia de `esquema\contenido.sql`** dentro de su propio paquete
(por ejemplo en `C:\Program Files\AVACOM\Biblioteca\esquema\`), para que si
el equipo técnico monta la carpeta de trabajo del aula y se le olvida ese
archivo, esté disponible para copiarlo — no para que la app lo lea de ahí
automáticamente, porque **eso sí sería cambiar comportamiento funcional**
(artículo 1 de la constitución) y necesitaría su propio spec.

### 3.3 · Experiencia del Install Wizard

```
Bienvenido
   ↓
Información AVACOM Biblioteca
   ↓
Installation Directory
   ↓
System Validation
   ↓
Ready to Install
   ↓
Installing
   ↓
Installation Completed
```

Nótese: **no hay pantalla de "Backend Configuration"**. No hay backend que
configurar. Si en el futuro se añade un paso de "carpeta de trabajo
inicial", es una decisión de producto nueva — hoy la app ya resuelve eso
sola, con su propia pantalla de Administración, y duplicar esa lógica en el
instalador violaría el artículo 1.

**Pantalla 1 — Bienvenido**

**Pantalla 2 — Información**

```
AVACOM Biblioteca

This wizard will install AVACOM Biblioteca, the encrypted content
library for this classroom's master computer, on this computer.
```

Sin detalles técnicos (cifrado, SQLite, WinUI) expuestos al profesor.

**Pantalla 3 — Ruta de instalación**

Ruta sugerida: `C:\Program Files\AVACOM\Biblioteca\`

Si se detecta una instalación de AVACOM OPS Master en
`C:\Program Files\AVACOM\OPS Master\`, **no es un conflicto ni motivo de
aviso** — es exactamente la coexistencia esperada. El instalador no debe ni
insinuar que hay un problema.

**Pantalla 4 — Validaciones**

Antes de instalar, comprobar automáticamente:

- Windows 10 1809+ / Windows 11 (artículo 10)
- arquitectura x64
- espacio disponible (acorde al tamaño real self-contained, artículo 11)
- permisos de escritura en `Program Files` y en `%ProgramData%\AVACOM\`
- instalación previa de AVACOM Biblioteca (para ofrecer actualizar/reparar,
  no reinstalar a ciegas encima)
- procesos activos de `Avacom.Biblioteca.App.exe` (si esta corriendo, pedir
  cerrarla antes de continuar — no matarla por su cuenta)

**Lo que NO hay que validar, a diferencia de OPS Master**: puerto 8000 (no
existe ese puerto aquí), ni nada de Firewall, ni nada de un runtime de
Python/Django.

**Pantalla 5 — Ready to Install / Installing / Completed**

Igual que cualquier instalador de escritorio estándar. Al terminar, crear el
acceso directo del menú de inicio / escritorio apuntando al `.exe` publicado
— nunca a un lanzador de MSIX.

### 3.4 · Preparación del binario a distribuir

El instalador **no compila nada**: empaqueta el resultado de un
`dotnet publish` hecho antes, en modo Release:

```
dotnet publish src\Avacom.Biblioteca.App -c Release -r win-x64 --self-contained true -o dist\payload
```

Esto ya respeta lo que el `.csproj` tiene decidido
(`WindowsAppSDKSelfContained`, `RuntimeIdentifier=win-x64`,
`PublishReadyToRun` en Release). El instalador toma `dist\payload\**` tal
cual y lo copia a la carpeta de instalación elegida. No se toca ni una
línea del `.csproj` para producir el instalador — eso violaría el artículo
1 de la constitución (comportamiento funcional intacto) y además es
exactamente el tipo de cambio de versiones de paquete que ya causó horas de
depuración en esta misma sesión.

### 3.5 · Desinstalación

- Borra la carpeta de instalación (`C:\Program Files\AVACOM\Biblioteca\`)
  y los accesos directos.
- **No borra** `%LOCALAPPDATA%\AVACOM\world.avacom.biblioteca\` (el índice
  SQLite del equipo, con lo que esté instalado) **sin preguntar**. Es el
  catálogo cifrado del aula, no una caché desechable — perderlo sin aviso
  es peor que dejar basura en disco.
- **No borra** `%ProgramData%\AVACOM\contenido\enlace.json` si el proceso
  sigue corriendo (no debería, si se pidió cerrar la app antes) ni la
  carpeta `%ProgramData%\AVACOM\` si detecta que OPS Master sigue instalado
  (artículo 4).
- No toca la carpeta de trabajo (`lic\`, `nodo\`, `pub\`, `esquema\`) que el
  administrador haya elegido: esa carpeta es del equipo técnico de
  contenido, vive donde el administrador la puso, y el instalador nunca supo
  dónde está.

### 3.6 · Prueba de que el instalador cumple el objetivo

No basta con que el instalador termine "correctamente". La prueba mínima
que hay que correr después de instalar, y que esta sesión demostró que
**no es trivial** (una app que abre una vez y muere en el segundo arranque
es exactamente el tipo de fallo que un check superficial no detecta):

1. Instalar en una máquina limpia (o una VM), sin el SDK de .NET ni el
   workload de MAUI presentes.
2. Abrir la app **dos veces seguidas**, cerrándola entre medio. Las dos
   veces tiene que abrir la ventana. (El bug real que se corrigió en este
   proyecto — un contador de identificador que reiniciaba en cada arranque
   y hacía chocar una clave primaria de SQLite — solo se manifestaba a
   partir del segundo arranque.)
3. Con la app abierta en la pestaña Catálogo, comprobar que
   `%ProgramData%\AVACOM\contenido\enlace.json` existe y que
   `GET /v1/salud` responde con la ficha de ese archivo.
4. Desinstalar, y confirmar que `Program Files\AVACOM\Biblioteca\`
   desaparece pero `%LOCALAPPDATA%\AVACOM\...` sigue ahí (o se preguntó
   antes de borrarlo, según lo que se haya decidido en 3.5).
5. Si en la misma máquina se instala también AVACOM OPS Master, repetir el
   paso 3 y confirmar que ambos productos leen la misma nota sin que
   ninguno de los dos la haya sobrescrito de forma incompatible.

---

## 4 · Clarificación (decisiones que faltan, no las tomo por mi cuenta)

1. **¿Qué tecnología de instalador?** (Inno Setup, WiX/MSI, NSIS...)
   Cualquiera sirve mientras produzca una copia de carpeta, no un MSIX. Inno
   Setup es la opción más simple para un caso sin servicio ni Firewall ni
   variables de entorno que generar.
2. **¿El instalador debe ofrecer elegir/crear la carpeta de trabajo durante
   la instalación**, o eso se deja enteramente para después, dentro de la
   propia pantalla de Administración de la app, como hoy? La especificación
   de arriba asume lo segundo (artículo 1), pero es una decisión de
   producto y conviene confirmarla antes de construir el wizard.
3. **¿Se firma el instalador y el `.exe`** (Authenticode)? No está definido
   en ningún documento existente del proyecto. Sin firma, Windows
   SmartScreen puede advertir al instalar en equipos nuevos.
4. **Nombre del emisor / metadatos del instalador** (compañía, versión
   visible al usuario) — hoy `ApplicationDisplayVersion` es `0.1` en el
   `.csproj`; confirmar si el instalador debe mostrar esa misma versión o
   una propia de empaquetado.

---

## 5 · Tarea

Construir el instalador de AVACOM Biblioteca para `/dist`, wizard, sin
backend ni servicio ni puerto que exponer, en ruta propia
(`C:\Program Files\AVACOM\Biblioteca\`), compatible con la coexistencia de
AVACOM OPS Master en el mismo equipo tal como la define su propia
constitución, sin producir un paquete MSIX/AppX, y sin tocar ninguna línea
de código funcional de la aplicación.
