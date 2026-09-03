@echo off
rem ---------------------------------------------------------------------------
rem AVACOM Biblioteca - probar la API, con doble clic
rem
rem Existe para el nodo principal, que no tiene teclado. Un .ps1 con doble clic
rem se abre en el Bloc de notas en vez de ejecutarse, y pedirle a alguien que
rem escriba una linea de PowerShell delante de una pantalla de 86 pulgadas sin
rem teclado no es una opcion. Esto lo lanza y punto.
rem
rem -STA hace falta para poder abrir una ventana desde PowerShell.
rem -ExecutionPolicy Bypass se aplica solo a este proceso y a nada mas.
rem ---------------------------------------------------------------------------
powershell -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0probar-api.ps1"
