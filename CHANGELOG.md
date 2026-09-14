# Cambios por versión

Escrito para quien instala y para quien integra, no solo para quien programa.
Las fechas son de corte de cada versión.

## Sin publicar

Cambios en `main` que todavía no forman parte de un instalable.

### Añadido

- API local: `GET /v1/cursos` y `GET /v1/curso/{curso_ref}`. Un curso es un
  paquete instalado; su referencia estable es la clave del paquete.
- API local: campo `capacidades` en `GET /v1/salud`, y las cinco capacidades
  opcionales `medio`, `leccion`, `evaluacion`, `comprobar` y `voz`, con sus
  rutas. Se publican solo cuando la aplicación inyecta una fuente de contenido.
- API local: `HEAD` y rangos en `GET /v1/medio/{ref}`, y archivos internos de
  un interactivo por `GET /v1/medio/{ref}/ruta`.
- Documentación técnica reorganizada en `specs/`, numerada en orden de lectura,
  con el contrato, las llamadas a la API, el formato de curso, el análisis del
  formato v2 y los posibles cambios para la conexión con el LMS.
- Propuesta, todavía no implementada, para pasar los archivos de texto del
  curso a JSON con validación por esquema (`specs/03-archivos-JSON.md`).
- `README.md`, `CONTRIBUTING.md`, plantilla de pull request y este archivo.

### Cambiado

- `CONTRATO-LMS.txt`, `LEEME.txt` y `api_calls.md` se retiran de la raíz. Su
  contenido está en `README.md`, `specs/01-Contrato-Biblioteca-LMS.md` y
  `specs/04-api-calls.md`.
- Los binarios del instalador (`installer/payload/`, `installer/dist/`) dejan
  de estar versionados. Ya estaban en `.gitignore`; ahora también fuera del
  índice. El `.exe` de cada versión se publica en Releases.

### Conocido y pendiente

- `GET /v1/curso/{ref}` devuelve dos niveles y ordena los ítems por título. El
  contrato del LMS pide tres niveles y orden real. Ver
  `specs/09-posibles-cambios-conexion-lms.md`.
- `GET /v1/evaluacion/{ref}` no devuelve opciones porque el paquete no las
  guarda. Ver `specs/03-archivos-JSON.md`.
- Las capacidades `banco` y `repaso` que el LMS espera no existen.

## 0.1.0 · septiembre de 2026

Primera versión instalable.

### Añadido

- Aplicación de escritorio AVACOM Biblioteca para Windows, sin empaquetar
  (`WindowsPackageType = None`), autocontenida.
- Motor `Avacom.Contenido`: cifrado por bloques AES-256-GCM compatible byte a
  byte con las herramientas en Python, licencia por equipo con X25519 y
  Ed25519, lectura de paquetes con las seis comprobaciones antes de descifrar,
  índice como proyección reconstruible, servidor de medios en `127.0.0.1` con
  descifrado al vuelo por rangos.
- API local de solo lectura para el LMS, contrato 1: salud, catálogo,
  taxonomía, elemento y mostrar. Descubrimiento por nota de enlace en
  `%ProgramData%\AVACOM\contenido\enlace.json`.
- Herramientas de contenido en Python: recolector de carpetas, generador de
  lecciones, empaquetador, publicador y criptografía.
- Formato de curso basado en carpetas y archivos de texto, con plantilla y un
  curso de ejemplo.
- Instalador con Inno Setup, con comprobación de ruta máxima, coexistencia con
  AVACOM OPS en `%ProgramData%\AVACOM\` y verificación de que no se cuela ningún
  artefacto MSIX. Variante de demostración con contenido, que no se distribuye
  a colegios.
- Prueba completa en siete etapas (`PROBAR-TODO.cmd`) y comprobación de punta a
  punta por consola en doce pasos.
