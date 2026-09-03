<#
    AVACOM Biblioteca - prueba de la API local, en ventana

    NO se ejecuta a mano: se lanza con doble clic en PROBAR-API.cmd.

    Para que sirve
    --------------
    Comprueba, desde fuera y sin abrir ninguna otra aplicacion, que la API local
    esta viva y devolviendo el catalogo. Es exactamente lo que hara el backend de
    AVACOM OPS Master: leer la nota del enlace, comprobar la version del
    contrato, y preguntar.

    Por que es una ventana y no una consola
    ---------------------------------------
    El nodo principal no tiene teclado. Todo tiene que hacerse con el dedo, y
    leerse desde lejos. Por eso los tamanos de aqui abajo son los mismos que se
    impone el resto del producto: nada por debajo de 20 px, botones de 64 px de
    alto, y ninguna informacion importante escondida detras de un :hover, que en
    tactil no existe.
#>

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

# --------------------------------------------------------------- apariencia

$Papel   = [System.Drawing.Color]::FromArgb(233, 237, 243)
$Tinta   = [System.Drawing.Color]::FromArgb(29, 29, 31)
$Suave   = [System.Drawing.Color]::FromArgb(110, 110, 115)
$Rojo    = [System.Drawing.Color]::FromArgb(229, 38, 43)
$Verde   = [System.Drawing.Color]::FromArgb(31, 138, 76)
$Ambar   = [System.Drawing.Color]::FromArgb(176, 122, 0)

function Fuente([int]$px, [bool]$negrita = $false) {
    $estilo = if ($negrita) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    New-Object System.Drawing.Font("Segoe UI", $px, $estilo)
}

# ------------------------------------------------------------------ consulta

function Leer-Enlace {
    # Misma nota y mismo sitio que leera OPS Master. Que no exista es un estado
    # NORMAL: significa que la aplicacion no esta abierta en el Catalogo.
    $ruta = Join-Path $env:ProgramData "AVACOM\contenido\enlace.json"
    if (-not (Test-Path $ruta)) { return $null }
    try { return Get-Content $ruta -Raw -ErrorAction Stop | ConvertFrom-Json } catch { return $null }
}

function Consultar {
    <#  Devuelve un objeto con todo lo que la ventana necesita pintar.
        Nunca lanza: un fallo aqui tiene que verse en pantalla, no cerrar la
        ventana en las narices de quien esta probando.  #>
    $r = [ordered]@{
        Estado = "mal"; Titulo = ""; Detalle = ""; Linea = ""; Elementos = @()
    }

    $nota = Leer-Enlace
    if (-not $nota) {
        $r.Estado  = "apagada"
        $r.Titulo  = "La biblioteca no esta publicando"
        $r.Detalle = "No hay nota de enlace. Abre AVACOM Biblioteca y entra a la pestana " +
                     "Contenido AVACOM: la API se enciende ahi.`n`n" +
                     "Para OPS Master esto no es un error, es " +
                     "'todavia no hay contenido en este equipo'."
        return $r
    }

    # El LMS tiene que comprobar esto ANTES de hablar. Un numero que no entiende
    # significa parar y decirlo, no intentarlo igual y fallar raro mas adelante.
    if ($nota.Contrato -ne 1) {
        $r.Estado  = "mal"
        $r.Titulo  = "Version de contrato desconocida"
        $r.Detalle = "La biblioteca habla el contrato $($nota.Contrato) y esta prueba entiende el 1."
        return $r
    }

    $base = "http://127.0.0.1:$($nota.Puerto)"
    $cab  = @{ "X-Avacom-Ficha" = $nota.Ficha }

    try {
        $salud = Invoke-RestMethod "$base/v1/salud" -Headers $cab -TimeoutSec 10
    } catch {
        $r.Estado  = "mal"
        $r.Titulo  = "La nota existe pero nadie responde"
        $r.Detalle = "Puerto $($nota.Puerto), proceso $($nota.Proceso).`n`n" +
                     "Suele pasar cuando la aplicacion se cerro de golpe: la nota queda " +
                     "apuntando a un puerto que ya murio. Abre la aplicacion otra vez."
        return $r
    }

    try {
        $cat = Invoke-RestMethod "$base/v1/catalogo" -Headers $cab -TimeoutSec 20
    } catch {
        $r.Estado  = "mal"
        $r.Titulo  = "Salud responde pero el catalogo no"
        $r.Detalle = "$($_.Exception.Message)"
        return $r
    }

    $r.Elementos = @($cat.elementos)
    $r.Linea = "puerto $($nota.Puerto)   ·   contrato $($nota.Contrato)   ·   huella $($salud.huella_catalogo)"

    if ($r.Elementos.Count -eq 0) {
        # La API funciona; lo que falta es contenido. Son dos problemas
        # distintos y conviene que en pantalla no se confundan.
        $r.Estado  = "vacia"
        $r.Titulo  = "La API responde, pero no hay contenido"
        $r.Detalle = "$($salud.paquetes) paquetes instalados, $($salud.politicas) politicas activas.`n`n" +
                     "Si esperabas ver material: en Administracion, carga la carpeta de " +
                     "trabajo y pulsa Revisar e instalar. OPS Master vera exactamente esta " +
                     "misma lista vacia."
    } else {
        $r.Estado  = "bien"
        $r.Titulo  = "La API responde correctamente"
        $r.Detalle = "$($r.Elementos.Count) elementos disponibles   ·   " +
                     "$($salud.paquetes) paquetes   ·   $($salud.politicas) politicas activas"
    }
    return $r
}

# ------------------------------------------------------------------ ventana

$v = New-Object System.Windows.Forms.Form
$v.Text = "AVACOM Biblioteca · prueba de la API"
$v.Size = New-Object System.Drawing.Size(1180, 860)
$v.StartPosition = "CenterScreen"
$v.BackColor = $Papel
$v.MinimumSize = New-Object System.Drawing.Size(900, 700)

$lblTitulo = New-Object System.Windows.Forms.Label
$lblTitulo.Font = Fuente 26 $true
$lblTitulo.ForeColor = $Tinta
$lblTitulo.Location = New-Object System.Drawing.Point(36, 28)
$lblTitulo.Size = New-Object System.Drawing.Size(1090, 46)
$v.Controls.Add($lblTitulo)

$lblDetalle = New-Object System.Windows.Forms.Label
$lblDetalle.Font = Fuente 15
$lblDetalle.ForeColor = $Suave
$lblDetalle.Location = New-Object System.Drawing.Point(36, 80)
$lblDetalle.Size = New-Object System.Drawing.Size(1090, 110)
$v.Controls.Add($lblDetalle)

$lblLinea = New-Object System.Windows.Forms.Label
$lblLinea.Font = Fuente 13
$lblLinea.ForeColor = $Suave
$lblLinea.Location = New-Object System.Drawing.Point(36, 196)
$lblLinea.Size = New-Object System.Drawing.Size(1090, 30)
$v.Controls.Add($lblLinea)

$lblLista = New-Object System.Windows.Forms.Label
$lblLista.Font = Fuente 13 $true
$lblLista.ForeColor = $Tinta
$lblLista.Text = "GET /v1/catalogo"
$lblLista.Location = New-Object System.Drawing.Point(36, 236)
$lblLista.Size = New-Object System.Drawing.Size(600, 28)
$v.Controls.Add($lblLista)

$lista = New-Object System.Windows.Forms.ListView
$lista.View = [System.Windows.Forms.View]::Details
$lista.FullRowSelect = $true
$lista.GridLines = $false
$lista.Font = Fuente 14
$lista.Location = New-Object System.Drawing.Point(36, 272)
$lista.Size = New-Object System.Drawing.Size(1090, 420)
$lista.Anchor = "Top,Left,Right,Bottom"
[void]$lista.Columns.Add("Tipo", 150)
[void]$lista.Columns.Add("Titulo", 430)
[void]$lista.Columns.Add("Nivel", 150)
[void]$lista.Columns.Add("Grado", 120)
[void]$lista.Columns.Add("Asignatura", 220)
$v.Controls.Add($lista)

function Boton($texto, $x, $ancho, $color) {
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $texto
    $b.Font = Fuente 15 $true
    # 70 px de alto: por debajo de eso no se acierta con el dedo a la primera.
    $b.Size = New-Object System.Drawing.Size($ancho, 70)
    $b.Location = New-Object System.Drawing.Point($x, 712)
    $b.Anchor = "Bottom,Left"
    $b.FlatStyle = "Flat"
    $b.FlatAppearance.BorderSize = 0
    $b.BackColor = $color
    $b.ForeColor = [System.Drawing.Color]::White
    $b.Cursor = "Hand"
    return $b
}

$btnProbar = Boton "PROBAR DE NUEVO" 36 340 $Rojo
$btnCopiar = Boton "COPIAR RESULTADO" 396 340 ([System.Drawing.Color]::FromArgb(90, 96, 108))
$btnCerrar = Boton "CERRAR" 756 200 ([System.Drawing.Color]::FromArgb(140, 146, 158))
$v.Controls.AddRange(@($btnProbar, $btnCopiar, $btnCerrar))

# ------------------------------------------------------------------ pintado

$script:ultimo = $null

function Pintar {
    $btnProbar.Enabled = $false
    $lblTitulo.Text = "Consultando..."
    $lblTitulo.ForeColor = $Suave
    $lblDetalle.Text = ""
    $lblLinea.Text = ""
    $lista.Items.Clear()
    $v.Refresh()

    $r = Consultar
    $script:ultimo = $r

    $lblTitulo.Text  = $r.Titulo
    $lblDetalle.Text = $r.Detalle
    $lblLinea.Text   = $r.Linea
    $lblTitulo.ForeColor = switch ($r.Estado) {
        "bien"    { $Verde }
        "vacia"   { $Ambar }
        "apagada" { $Ambar }
        default   { $Rojo }
    }

    foreach ($e in ($r.Elementos | Sort-Object nivel, asignatura, titulo)) {
        $it = New-Object System.Windows.Forms.ListViewItem($e.tipo)
        [void]$it.SubItems.Add([string]$e.titulo)
        [void]$it.SubItems.Add([string]$e.nivel)
        [void]$it.SubItems.Add([string]$e.grado)
        [void]$it.SubItems.Add([string]$e.asignatura)
        [void]$lista.Items.Add($it)
    }
    $lblLista.Text = "GET /v1/catalogo   —   $($r.Elementos.Count) elementos"
    $btnProbar.Enabled = $true
}

$btnProbar.Add_Click({ Pintar })
$btnCerrar.Add_Click({ $v.Close() })

$btnCopiar.Add_Click({
    # Sin teclado no se puede seleccionar y copiar a mano. Esto deja el
    # resultado entero en el portapapeles para pegarlo en un correo o un chat.
    $r = $script:ultimo
    if (-not $r) { return }
    $t = "AVACOM Biblioteca - prueba de la API - $(Get-Date -Format 'yyyy-MM-dd HH:mm')`r`n" +
         "$($r.Titulo)`r`n$($r.Detalle)`r`n$($r.Linea)`r`n`r`n"
    foreach ($e in ($r.Elementos | Sort-Object nivel, asignatura, titulo)) {
        $t += "{0,-12} {1,-46} {2,-12} {3,-10} {4}`r`n" -f $e.tipo, $e.titulo, $e.nivel, $e.grado, $e.asignatura
    }
    Set-Clipboard -Value $t
    $btnCopiar.Text = "COPIADO"
    $v.Refresh()
    Start-Sleep -Milliseconds 700
    $btnCopiar.Text = "COPIAR RESULTADO"
})

$v.Add_Shown({ $v.Activate(); Pintar })
[void]$v.ShowDialog()
