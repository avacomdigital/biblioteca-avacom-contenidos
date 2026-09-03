<#
    AVACOM - consulta la API local del componente de contenido

        .\consultar-api.ps1              una foto del estado actual
        .\consultar-api.ps1 -Vigilar     se queda mirando y avisa cuando cambia

    Hace lo mismo que hara el LMS: lee la nota del enlace, comprueba la version
    del contrato, y pregunta. Sirve para ver desde fuera lo que AVACOM OPS Master
    vera, sin tener que abrir ninguna otra aplicacion.

    El modo -Vigilar es el que conviene para la prueba en vivo: se deja corriendo,
    se retira un paquete en la aplicacion, y aqui se ve el cambio al instante.
#>

param([switch]$Vigilar, [int]$CadaSegundos = 3)

$ErrorActionPreference = "Stop"

function Leer-Enlace {
    # Mismo sitio y mismo formato que usara el LMS. Si no existe, el componente
    # no esta corriendo, y eso es un estado normal, no un error.
    $ruta = Join-Path $env:ProgramData "AVACOM\contenido\enlace.json"
    if (-not (Test-Path $ruta)) { return $null }
    try { return Get-Content $ruta -Raw | ConvertFrom-Json } catch { return $null }
}

function Preguntar {
    param($nota, $ruta)
    $r = Invoke-RestMethod -Uri "http://127.0.0.1:$($nota.Puerto)$ruta" `
                           -Headers @{ "X-Avacom-Ficha" = $nota.Ficha } `
                           -TimeoutSec 10
    return $r
}

function Foto {
    $nota = Leer-Enlace
    if (-not $nota) {
        Write-Host "No hay componente de contenido corriendo." -ForegroundColor Yellow
        Write-Host "  (falta $env:ProgramData\AVACOM\contenido\enlace.json)"
        Write-Host "  Abre AVACOM Biblioteca y entra a la pestana 'Contenido AVACOM'."
        return $null
    }

    # El LMS tiene que hacer esta comprobacion antes de hablar. Un numero que no
    # entiende significa parar y decirlo, no intentarlo igual.
    if ($nota.Contrato -ne 1) {
        Write-Host "Contrato $($nota.Contrato): esta herramienta solo entiende el 1." -ForegroundColor Red
        return $null
    }

    $salud = Preguntar $nota "/v1/salud"
    $cat   = Preguntar $nota "/v1/catalogo"

    Write-Host ""
    Write-Host "puerto $($nota.Puerto)  ·  contrato $($nota.Contrato)  ·  proceso $($nota.Proceso)" -ForegroundColor DarkGray
    Write-Host "huella  $($salud.huella_catalogo)" -ForegroundColor Cyan
    Write-Host "$($salud.elementos) elementos  ·  $($salud.paquetes) paquetes  ·  $($salud.politicas) politicas activas"
    Write-Host ""

    if ($cat.elementos.Count -eq 0) {
        Write-Host "  (el catalogo esta vacio: el LMS no vera nada)" -ForegroundColor Yellow
    } else {
        # Write-Host y no salida al pipeline: esta funcion DEVUELVE la huella, y
        # cualquier cosa que se escriba al pipeline se mezclaria con ella.
        $cat.elementos | Sort-Object nivel, asignatura, titulo | ForEach-Object {
            Write-Host ("  {0,-12} {1,-46} {2,-12} {3}" -f $_.tipo, $_.titulo, $_.nivel, $_.asignatura)
        }
    }
    return $salud.huella_catalogo
}

if (-not $Vigilar) { Foto | Out-Null; return }

Write-Host "Vigilando. Retira un paquete en AVACOM Biblioteca y mira lo que pasa aqui."
Write-Host "Ctrl+C para parar." -ForegroundColor DarkGray
$anterior = Foto

while ($true) {
    Start-Sleep -Seconds $CadaSegundos
    $nota = Leer-Enlace
    if (-not $nota) {
        if ($anterior -ne $null) {
            Write-Host ""
            Write-Host "[$(Get-Date -f HH:mm:ss)] el componente se cerro" -ForegroundColor Yellow
            $anterior = $null
        }
        continue
    }
    try { $ahora = (Preguntar $nota "/v1/salud").huella_catalogo } catch { continue }

    if ($ahora -ne $anterior) {
        Write-Host ""
        Write-Host "[$(Get-Date -f HH:mm:ss)] CAMBIO EL CATALOGO  ($anterior -> $ahora)" -ForegroundColor Green
        $anterior = Foto
    }
}
