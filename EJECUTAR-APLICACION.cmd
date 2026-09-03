@echo off
rem ---------------------------------------------------------------------------
rem AVACOM - arranca la aplicacion
rem
rem La primera vez tarda: descarga los paquetes y compila. Despues es rapido.
rem Si la carpeta de trabajo no existe todavia, la prepara antes.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

if not exist "%~dp0trabajo\lic\licencia.json" (
  echo No hay carpeta de trabajo. Se prepara primero.
  echo.
  rem Se llama con -Command y no con -File para que el codigo de salida del
  rem guion llegue hasta aqui de forma fiable en cualquier version de PowerShell.
  powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0preparar-trabajo.ps1'; exit $LASTEXITCODE"
  if errorlevel 1 goto :fin
  echo.
)

rem Se entra en app-biblioteca antes de llamar a dotnet: el global.json que fija
rem la version del SDK vive ahi, y dotnet lo busca desde el directorio actual.
rem Lanzandolo desde la raiz, ese anclaje se perderia sin decir nada.
pushd "%~dp0app-biblioteca"
dotnet run --project src\Avacom.Biblioteca.App
popd

:fin
echo.
pause
