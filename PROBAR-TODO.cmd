@echo off
rem ---------------------------------------------------------------------------
rem AVACOM - lanzador para doble clic
rem
rem Corre la prueba completa. Existe solo para no tener que abrir PowerShell ni
rem pelearse con la politica de ejecucion de guiones: -ExecutionPolicy Bypass
rem se aplica a este proceso y a nada mas.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0probar-todo.ps1"
echo.
pause
