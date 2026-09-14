## Qué cambia

<!-- Una o dos frases. Lo que hace ahora que antes no hacía, o lo que dejó de hacer mal. -->

## Por qué

<!-- El motivo. Si hay una decisión de diseño, aquí es donde se escribe para que quede. -->

## Cómo se comprobó

- [ ] `dotnet test tests\Avacom.Contenido.Tests` en verde
- [ ] `PROBAR-TODO.cmd` en verde (obligatorio si toca cifrado, paquetes o API local)
- [ ] Probado en la aplicación, con dos arranques seguidos si toca el índice o el arranque

## Lista de revisión

- [ ] La clave de respuesta sigue sin poder salir de la Biblioteca
- [ ] El cifrado sigue siendo compatible byte a byte con las herramientas en Python
- [ ] La política de la escuela se aplica antes de entregar un solo byte
- [ ] Solo se escucha en `127.0.0.1`
- [ ] Si cambia la forma de una respuesta de la API: sube `PuntoDeEnlace.Contrato` y se actualizan `specs/01` y `specs/04`
- [ ] El índice sigue siendo una proyección de los manifiestos
- [ ] La documentación de `specs/` que describe este comportamiento está actualizada
- [ ] Ningún archivo generado, clave ni binario del instalador entra en el commit

## Documentos de `specs/` afectados

<!-- Lista, o «ninguno». -->
