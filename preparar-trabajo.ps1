<#
    AVACOM - prepara una carpeta de trabajo lista para probar el componente

        .\preparar-trabajo.ps1 [carpeta]

    Deja dentro: los dos paquetes publicados y cifrados, la licencia del equipo,
    el par de claves del equipo y el esquema del componente.

    Nota: todo se construye en una carpeta temporal local y se copia al final.
    SQLite necesita bloqueo de archivos y en unidades de red falla.
#>

param([string]$Destino)

$ErrorActionPreference = "Stop"
$Raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Destino) { $Destino = Join-Path $Raiz "trabajo" }


# ---------------------------------------------------------------------------
# Como se invoca Python.
#
# El lanzador "py" es lo que instala python.org y es lo mas fiable, porque
# elige la version correcta aunque haya varias. Si no esta, se prueba "python"
# a secas, que es lo que deja la Tienda de Windows.
# ---------------------------------------------------------------------------
$Py = $null
if (Get-Command py -ErrorAction SilentlyContinue) {
    $Py = @("py", "-3")
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $Py = @("python")
} else {
    throw "No se encontro Python 3. Instalarlo desde python.org marcando 'Add Python to PATH'."
}

function Invocar-Python {
    param([Parameter(ValueFromRemainingArguments = $true)] [string[]] $Argumentos)
    $exe = $Py[0]
    $pre = @()
    if ($Py.Count -gt 1) { $pre = $Py[1..($Py.Count - 1)] }
    & $exe @pre @Argumentos
    if ($LASTEXITCODE -ne 0) { throw "fallo al ejecutar: $exe $pre $Argumentos" }
}

function Sangrar {
    process { "    $_" }
}


$Tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("avacom-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $Tmp -Force | Out-Null
$Anterior = Get-Location

try {
    Set-Location (Join-Path $Raiz "paquetes")

    Write-Host "0 - medios de muestra"
    if (Test-Path "materiales\lamina-granja.png") {
        Write-Host "    ya estan en paquetes\materiales"
    } else {
        Invocar-Python generar_medios.py | Sangrar
    }

    Write-Host "1 - par de claves del emisor"
    if (Test-Path "claves\emisor_privada.pem") {
        Write-Host "    ya existe"
    } else {
        Invocar-Python avacom_empaquetador.py claves | Out-Null
        Write-Host "    generado (claves de desarrollo, ver LEEME.txt)"
    }

    Write-Host "2 - se construyen los dos paquetes de ejemplo"
    Invocar-Python avacom_empaquetador.py ejemplos (Join-Path $Tmp "claro") |
        Where-Object { $_ -match '^(Paquete|ACEPTADO)' } | Sangrar

    Write-Host "3 - el equipo del aula genera su par de claves"
    Invocar-Python avacom_publicar.py nodo (Join-Path $Tmp "nodo") |
        Where-Object { $_ -match 'publica' } | Sangrar

    Write-Host "4 - se publican cifrados y firmados"
    foreach ($p in (Get-ChildItem (Join-Path $Tmp "claro") -Directory)) {
        Invocar-Python avacom_publicar.py publicar $p.FullName (Join-Path $Tmp "pub") |
            Where-Object { $_ -match '^(Publicado|  manifiesto)' } | Sangrar
    }

    Write-Host "5 - se emite la licencia de este equipo"
    $publicados = @((Get-ChildItem (Join-Path $Tmp "pub") -Directory).FullName)
    # Sin esto, una carpeta vacia emite una licencia con cero paquetes
    # autorizados sin error, y el fallo aparece cuatro etapas mas tarde
    # disfrazado de "este equipo no tiene licencia para ...".
    if ($publicados.Count -eq 0) { throw "no se publico ningun paquete: no hay nada que licenciar" }
    Invocar-Python avacom_publicar.py licencia (Join-Path $Tmp "nodo") (Join-Path $Tmp "lic") @publicados |
        Sangrar

    Set-Location $Raiz

    Write-Host "6 - se copia todo a la carpeta de trabajo"
    if (Test-Path $Destino) { Remove-Item $Destino -Recurse -Force }
    New-Item -ItemType Directory -Path $Destino -Force | Out-Null
    Copy-Item (Join-Path $Tmp "pub")  $Destino -Recurse
    Copy-Item (Join-Path $Tmp "lic")  $Destino -Recurse
    Copy-Item (Join-Path $Tmp "nodo") $Destino -Recurse
    New-Item -ItemType Directory -Path (Join-Path $Destino "esquema") -Force | Out-Null
    Copy-Item (Join-Path $Raiz "esquema\contenido.sql") (Join-Path $Destino "esquema")

    # La clave del paquete se queda fuera. Solo debe existir donde se publica:
    # si viajara con el paquete, cifrarlo no habria servido de nada.
    Get-ChildItem $Destino -Recurse -Filter "K_PKG_NO_DISTRIBUIR.hex" | Remove-Item -Force

    Write-Host "    $Destino"
    Write-Host ""
    Write-Host "Listo."
}
catch {
    # El lanzador de doble clic mira el codigo de salida. Sin este exit, un
    # error terminante no controlado deja un codigo que depende de la version de
    # PowerShell, y el .cmd seguiria adelante como si nada.
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    Set-Location $Anterior
    if (Test-Path $Tmp) { Remove-Item $Tmp -Recurse -Force -ErrorAction SilentlyContinue }
    exit 1
}
finally {
    Set-Location $Anterior
    if (Test-Path $Tmp) { Remove-Item $Tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
