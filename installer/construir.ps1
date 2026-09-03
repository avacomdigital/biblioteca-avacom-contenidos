<#
    AVACOM Biblioteca - construye el instalador

        .\construir.ps1              publica y compila
        .\construir.ps1 -SoloCompilar   reutiliza el payload ya publicado

    Deja el instalador en   installer\dist\AVACOM-Biblioteca-<version>-setup.exe

    Requiere Inno Setup 6 (ISCC.exe). Si no esta, se instala con:
        winget install --id JRSoftware.InnoSetup
#>

<#
    -ConContenidoDemo  mete dentro del instalador la carpeta trabajo\ del repo:
                       esquema, licencia, CLAVE PRIVADA DEL NODO y los paquetes
                       de ejemplo. Sirve para demostrar el producto sin tener
                       que provisionar nada.

                       NO se usa para lo que se entrega a un colegio. La clave
                       privada del nodo iria dentro del .exe, y cualquiera que
                       lo tuviera podria descifrar esos paquetes: el modelo de
                       una licencia por equipo dejaria de existir.

                       Por eso hay que pedirlo a mano. Sin el interruptor sale
                       un instalador limpio, solo con la aplicacion.
#>
param([switch]$SoloCompilar, [switch]$ConContenidoDemo)

$ErrorActionPreference = "Stop"
$Aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$Raiz = Split-Path -Parent $Aqui

function Buscar-ISCC {
    # Inno Setup se instala por usuario o en Program Files segun como se haya
    # puesto. Se miran los tres sitios en vez de asumir uno.
    $candidatos = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidatos) { if (Test-Path $c) { return $c } }
    throw "No se encontro ISCC.exe (Inno Setup 6). Instalalo con: winget install --id JRSoftware.InnoSetup"
}

$iscc = Buscar-ISCC
Write-Host "Inno Setup: $iscc" -ForegroundColor DarkGray

# ---------------------------------------------------------------- 1. payload

if (-not $SoloCompilar) {
    Write-Host ""
    Write-Host "1 - publicando la aplicacion (Release, self-contained, win-x64)"
    Write-Host "    tarda varios minutos: compila ReadyToRun" -ForegroundColor DarkGray

    # Si la aplicacion esta abierta, el publish falla a mitad dejando el payload
    # incompleto, y el instalador saldria roto sin avisar.
    if (Get-Process -Name "Avacom.Biblioteca.App" -ErrorAction SilentlyContinue) {
        throw "AVACOM Biblioteca esta abierta. Cierrala antes de construir."
    }

    $payload = Join-Path $Aqui "payload"
    if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }

    Push-Location (Join-Path $Raiz "app-biblioteca")
    try {
        # Se publica con las mismas propiedades que ya decide el .csproj. No se
        # pasa nada que las contradiga: lo que se prueba en desarrollo tiene que
        # ser lo que se distribuye.
        & dotnet publish src\Avacom.Biblioteca.App -c Release -r win-x64 --self-contained true -o $payload
        if ($LASTEXITCODE -ne 0) { throw "fallo dotnet publish" }
    } finally { Pop-Location }

    # Comprobacion de que no se colo un empaquetado MSIX. Si esto salta, alguien
    # cambio WindowsPackageType y la aplicacion quedaria sin poder hablar consigo
    # misma por loopback: ni video, ni API para OPS Master.
    $msix = Get-ChildItem $payload -Recurse -Include *.msix, *.appx, AppxManifest.xml -ErrorAction SilentlyContinue
    if ($msix) { throw "El payload trae artefactos MSIX/AppX. Revisa WindowsPackageType en el .csproj." }

    $n = (Get-ChildItem $payload -Recurse -File).Count
    $mb = [math]::Round((Get-ChildItem $payload -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
    Write-Host "    payload: $n archivos, $mb MB" -ForegroundColor DarkGray
}

# --------------------------------------------------------------- 2. compilar

Write-Host ""
Write-Host "2 - compilando el instalador"

$dist = Join-Path $Aqui "dist"
New-Item -ItemType Directory -Path $dist -Force | Out-Null

$definiciones = @()
if ($ConContenidoDemo) {
    $trabajo = Join-Path $Raiz "trabajo"
    if (-not (Test-Path (Join-Path $trabajo "lic\licencia.json"))) {
        throw "No hay carpeta trabajo\ preparada. Generala antes con: .\preparar-trabajo.ps1"
    }

    Write-Host ""
    Write-Host "   AVISO: este instalador llevara dentro la clave privada del nodo" -ForegroundColor Yellow
    Write-Host "   y los paquetes de ejemplo. Es para demostrar el producto." -ForegroundColor Yellow
    Write-Host "   No se entrega a un colegio." -ForegroundColor Yellow

    $definiciones += "/DConContenidoDemo"
}

& $iscc "/Q" @definiciones (Join-Path $Aqui "avacom-biblioteca.iss")
if ($LASTEXITCODE -ne 0) { throw "fallo la compilacion del instalador" }

Write-Host ""
Get-ChildItem $dist -Filter "*.exe" | ForEach-Object {
    Write-Host ("Listo: {0}  ({1} MB)" -f $_.FullName, [math]::Round($_.Length / 1MB)) -ForegroundColor Green
}
