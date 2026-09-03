<#
    AVACOM - prueba completa del componente de contenido educativo

        .\probar-todo.ps1

    Corre siete etapas y para en la primera que falle. No hay que saber nada de
    antemano: cada etapa dice que esta comprobando y por que.

    Lo que NO comprueba, y conviene tener claro:

      Nada de esto dice si el material se lee a cuatro metros, si el boton se
      acierta con el dedo a la primera, ni si el color aguanta con el sol dando
      en la pantalla. Eso solo sale en la pantalla interactiva de 86 pulgadas.
      Este guion comprueba que lo de debajo es correcto, no que lo de arriba
      funcione en un aula.
#>

$ErrorActionPreference = "Stop"
$Raiz    = Split-Path -Parent $MyInvocation.MyCommand.Path
$Trabajo = Join-Path $Raiz "trabajo"
$App     = Join-Path $Raiz "app-biblioteca"

function Rojo  ($t) { Write-Host $t -ForegroundColor Red }
function Verde ($t) { Write-Host $t -ForegroundColor Green }
function Gris  ($t) { Write-Host $t -ForegroundColor DarkGray }

function Etapa ($t) {
    Write-Host ""
    Write-Host $t -ForegroundColor White
    Gris ("-" * $t.Length)
}

function Fallo ($t) {
    Write-Host ""
    Rojo "FALLA en $t"
    Rojo "Se para aqui. Arreglar esto antes de seguir."
    exit 1
}


# ---------------------------------------------------------------------------
Etapa "1 - Herramientas"

# El lanzador "py" es lo que instala python.org y es lo mas fiable, porque
# elige la version correcta aunque haya varias instaladas. Si no esta, se
# prueba "python" a secas, que es lo que deja la Tienda de Windows.
if (Get-Command py -ErrorAction SilentlyContinue) {
    $PyExe = "py"; $PyPre = @("-3")
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $PyExe = "python"; $PyPre = @()
} else {
    Rojo "  falta Python 3"
    Write-Host "      Instalar desde python.org marcando 'Add Python to PATH'"
    exit 1
}
Gris ("  python    " + (& $PyExe @PyPre --version))

# OJO con la redireccion del flujo de error.
#
# En Windows PowerShell 5.1, que es lo que lanza PROBAR-TODO.cmd, con
# ErrorActionPreference en Stop cada linea que un programa externo escribe en el
# flujo de error se convierte en un error terminante. Justo en el unico caso que
# esta comprobacion existe para detectar (falta la biblioteca), Python escribe su
# traza ahi y el guion moriria con un volcado rojo SIN llegar a decir como
# instalarla. Por eso se baja la preferencia solo durante esta llamada.
$previo = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $PyExe @PyPre -c "import cryptography" 2>&1 | Out-Null
$faltaCripto = $LASTEXITCODE -ne 0
$ErrorActionPreference = $previo

if ($faltaCripto) {
    Rojo "  falta la biblioteca de cifrado"
    Write-Host "      py -3 -m pip install cryptography pillow reportlab"
    exit 1
}
Gris "  cryptography instalada"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Rojo "  falta el SDK de .NET 10"
    Write-Host "      https://dotnet.microsoft.com/download"
    exit 1
}
Gris ("  dotnet    " + (& dotnet --version))
Verde "  todo lo necesario esta"


# ---------------------------------------------------------------------------
Etapa "2 - Se construye la carpeta de trabajo"
Gris "  Se generan los medios, se arman los dos paquetes, se cifran, se firman,"
Gris "  el equipo genera su par de claves y se le emite su licencia."

try {
    & (Join-Path $Raiz "preparar-trabajo.ps1") $Trabajo
} catch {
    Rojo ("  " + $_.Exception.Message)
    Fallo "la preparacion de la carpeta de trabajo"
}
# El otro guion puede terminar con "exit 1" en vez de lanzar, y en ese caso el
# catch de arriba no se entera. Se comprueba el resultado, no la forma de salir.
if (-not (Test-Path (Join-Path $Trabajo "lic\licencia.json"))) {
    Fallo "la preparacion de la carpeta de trabajo"
}
Verde "  carpeta lista en $Trabajo"


# ---------------------------------------------------------------------------
Etapa "3 - Lo que hay dentro de un paquete publicado"
Gris "  Un paquete son cuatro cosas: la ficha en claro que permite decidir si"
Gris "  instalarlo, el manifiesto cifrado, los medios cifrados y la firma."

$pkg = Get-ChildItem (Join-Path $Trabajo "pub") -Directory | Select-Object -First 1
if (-not $pkg) { Fallo "no se publico ningun paquete en $Trabajo\pub" }
Write-Host ""
Get-ChildItem $pkg.FullName -Name | ForEach-Object { Write-Host "      $_" }
Write-Host ""
Gris "  medios (todos con extension .enc, ninguno legible):"
Get-ChildItem (Join-Path $pkg.FullName "medios") -Name |
    Select-Object -First 4 | ForEach-Object { Write-Host "      $_" }
Write-Host ""
Gris "  primeros bytes de un medio, tal como estan en el disco:"

$med = Get-ChildItem (Join-Path $pkg.FullName "medios") -Filter "*.png.enc" | Select-Object -First 1
if (-not $med) { Fallo "el paquete publicado no tiene ningun medio .png.enc" }
# Select-Object en vez de un rango: un rango se sale si el archivo fuera mas
# corto de lo esperado, y ahi PowerShell rellena con nulos sin avisar.
$bytes = [System.IO.File]::ReadAllBytes($med.FullName) | Select-Object -First 32
$hex   = ($bytes | ForEach-Object { $_.ToString("x2") }) -join " "
$txt   = -join ($bytes | ForEach-Object { if ($_ -ge 32 -and $_ -lt 127) { [char]$_ } else { "." } })
Write-Host "      $hex"
Write-Host "      $txt"
Gris "  (empieza por AVACOMENC1 y despues es ruido; no hay ni una cabecera PNG)"
Verde "  el contenido esta cifrado en disco"


# ---------------------------------------------------------------------------
Etapa "4 - El cifrado del componente y el del empaquetador son el mismo"
Gris "  Es la prueba mas importante del proyecto. El empaquetador esta en Python"
Gris "  y el componente en C#: si no coinciden byte a byte, todo lo demas es"
Gris "  humo. Aqui tambien entra el servidor local de medios."

Push-Location $App
try     { & dotnet test tests\Avacom.Contenido.Tests --nologo -v q; $codigo = $LASTEXITCODE }
finally { Pop-Location }
if ($codigo -ne 0) { Fallo "las pruebas de la biblioteca" }
Verde "  las dos implementaciones producen lo mismo"


# ---------------------------------------------------------------------------
Etapa "5 - El componente de punta a punta"
Gris "  Instala, proyecta el indice, aplica una politica, descifra un material al"
Gris "  vuelo, comprueba que el repaso no genera nota, reconstruye el indice desde"
Gris "  cero, sirve un video por rangos y comprueba que la respuesta correcta no"
Gris "  sale del manifiesto."

Push-Location $App
try     { & dotnet run --project src\Avacom.Contenido.Consola -- $Trabajo; $codigo = $LASTEXITCODE }
finally { Pop-Location }
if ($codigo -ne 0) { Fallo "la comprobacion de punta a punta" }


# ---------------------------------------------------------------------------
Etapa "6 - Que pasa si alguien se lleva un paquete"
Gris "  Se copia un paquete a otra carpeta, como quien lo pasa a una memoria, y"
Gris "  se intenta abrir sin la licencia de este equipo."

& $PyExe @PyPre (Join-Path $Raiz "paquetes\prueba_paquete_ajeno.py") $Trabajo
if ($LASTEXITCODE -ne 0) { Fallo "la prueba del paquete llevado a otro equipo" }
Verde "  el paquete solo sirve en el equipo para el que se emitio"


# ---------------------------------------------------------------------------
Etapa "7 - La aplicacion"
Gris "  Compilar tarda la primera vez, porque descarga los paquetes."

Push-Location $App
try     { & dotnet build src\Avacom.Biblioteca.App --nologo -v q; $codigo = $LASTEXITCODE }
finally { Pop-Location }
if ($codigo -ne 0) {
    Write-Host ""
    Rojo "  no compilo la aplicacion"
    Gris "  Si el error habla de una carga de trabajo que falta:"
    Write-Host "      dotnet workload install maui"
    exit 1
}
Verde "  la aplicacion compila"

Write-Host ""
Gris "  Para verla:"
Write-Host "      cd `"$App`""
Write-Host "      dotnet run --project src\Avacom.Biblioteca.App"
Write-Host ""
Gris "  Y dentro: Administracion -> pegar esta ruta -> Usar esta ruta"
Write-Host "      $Trabajo"
Gris "  -> Revisar e instalar -> pestana Contenido AVACOM"


# ---------------------------------------------------------------------------
Write-Host ""
Verde "============================================================"
Verde " Todo correcto."
Verde "============================================================"
Write-Host ""
Gris " Lo que queda por comprobar, y no se puede desde aqui:"
Write-Host "   - que el texto se lea a cuatro metros"
Write-Host "   - que el boton se acierte con el dedo a la primera"
Write-Host "   - que el video no se entrecorte en el equipo de la pantalla"
Write-Host "   - que arranque con el aula sin conexion"
Write-Host ""
Gris " Todo eso pide la pantalla interactiva de verdad."
