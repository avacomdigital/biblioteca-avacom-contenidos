; ============================================================================
;  AVACOM Biblioteca - instalador para el equipo maestro del aula
;
;  Construir con:   .\construir.ps1        (desde installer\)
;
;  DECISIONES QUE NO SE PUEDEN DESHACER SIN ROMPER EL PRODUCTO
;
;  1. Esto instala una COPIA DE CARPETA, no un paquete MSIX/AppX, y es
;     deliberado. La aplicacion se compila con WindowsPackageType=None porque
;     una app empaquetada corre dentro del aislamiento de red de Windows, que
;     bloquea las conexiones al propio equipo. El servidor de medios y la API
;     local escuchan justo ahi, en 127.0.0.1. Empaquetarla la dejaria sin
;     reproducir video y sin poder hablar con AVACOM OPS Master.
;
;  2. No se instala ningun servicio de Windows y no se abre ningun puerto en
;     el Firewall. La API vive dentro del proceso de la aplicacion, escucha
;     solo en loopback y pide un puerto efimero que el sistema elige en cada
;     arranque. No hay nada fijo que declarar.
;
;  3. %ProgramData%\AVACOM\ es territorio COMPARTIDO con AVACOM OPS Master.
;     Ahi la aplicacion deja enlace.json, que es como el backend de OPS Master
;     la encuentra. Ni se llena de archivos de programa, ni se borra al
;     desinstalar si OPS Master sigue en el equipo.
; ============================================================================

#define Nombre        "AVACOM Biblioteca"
#define Version       "0.1.0"
#define Editor        "AVACOM"
#define Ejecutable    "Avacom.Biblioteca.App.exe"

; Espacio real que ocupa el payload publicado (Release, self-contained,
; ReadyToRun). Medido, no estimado: 290 MB. Se pide margen por encima porque
; una instalacion que llena el disco al ultimo byte deja el equipo inservible.
#define EspacioMinimoMB 700

; El archivo con el nombre mas largo del payload ocupa 75 caracteres. Windows
; corta en 260, asi que la carpeta de instalacion no puede pasar de 180 y
; dejar margen. La ruta por defecto usa 36, de sobra.
;
; Esto NO es teorico: probando este instalador en una carpeta temporal de ruta
; larga, los 629 archivos se copiaron sin un solo error y despues la aplicacion
; arrancaba y se cerraba sola, sin ventana, sin mensaje y sin registro de crash
; en el visor de sucesos. Exactamente el tipo de fallo que nadie sabe atribuir.
#define RutaMaximaChars 180

[Setup]
; AppId propio y distinto del de OPS Master: si coincidieran, desinstalar uno
; borraria la entrada del otro en Programas y caracteristicas.
AppId={{A7F3C2E1-5B4D-4E8A-9C1F-2D6B8E0A4C71}
AppName={#Nombre}
AppVersion={#Version}
AppPublisher={#Editor}
VersionInfoVersion={#Version}

; Ruta propia. Nunca se comparte carpeta de aplicacion con OPS Master, que
; vive en C:\Program Files\AVACOM\OPS Master\.
DefaultDirName={autopf}\AVACOM\Biblioteca
DefaultGroupName=AVACOM
DisableProgramGroupPage=yes
DisableWelcomePage=no

; Program Files necesita elevacion. El wizard la pide una sola vez, al abrir.
PrivilegesRequired=admin

; Solo x64: el .csproj fija RuntimeIdentifier=win-x64 y el payload es nativo.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809. Por debajo, el componente de navegacion incrustado no es
; fiable, y de el dependen el visor de documentos y el de material interactivo.
MinVersion=10.0.17763

OutputDir=dist
; El instalador con contenido de demostracion se llama distinto A PROPOSITO.
; Los dos archivos acaban en la misma carpeta y se parecen; si solo cambiara
; lo de dentro, tarde o temprano alguien manda el equivocado a un colegio.
#ifdef ConContenidoDemo
OutputBaseFilename=AVACOM-Biblioteca-{#Version}-DEMO-con-contenido
#else
OutputBaseFilename=AVACOM-Biblioteca-{#Version}-setup
#endif
Compression=lzma2/max
SolidCompression=yes

; La aplicacion no se cierra sola: si esta abierta, se pide cerrarla. Matar el
; proceso del profesor a mitad de una clase no es cosa de un instalador.
CloseApplications=no

UninstallDisplayName={#Nombre}
UninstallDisplayIcon={app}\{#Ejecutable}
WizardStyle=modern

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "escritorio"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; El payload entero, tal como lo dejo dotnet publish. El instalador no compila
; nada ni reordena nada: lo que se probo es exactamente lo que se copia.
Source: "payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; El esquema del componente viaja con la aplicacion como copia de respaldo.
; OJO: la aplicacion NO lo lee de aqui. Lo busca dentro de la carpeta de
; trabajo que elige el administrador (con lic\, nodo\ y pub\), que la prepara
; el equipo tecnico de contenido. Esta copia esta para que, si al montar esa
; carpeta falta el esquema, no haya que ir a buscarlo a otro equipo.
Source: "..\esquema\contenido.sql"; DestDir: "{app}\esquema"; Flags: ignoreversion

#ifdef ConContenidoDemo
; ---------------------------------------------------------------------------
; CARPETA DE TRABAJO DE DEMOSTRACION
;
; Sin una carpeta de trabajo (licencia, clave del nodo, esquema y paquetes) la
; aplicacion instala pero no puede abrir nada, y OPS Master no ve ni un
; elemento en el catalogo. Esto la trae hecha para poder demostrar el producto
; de punta a punta sin provisionar nada.
;
; Lo que va aqui dentro incluye nodo\nodo_privada.bin, que es LA CLAVE PRIVADA
; DEL NODO. En un equipo de aula de verdad esa clave se genera en la propia
; maquina y no sale de ahi; aqui viaja dentro del .exe, y por eso este
; instalador no es el que se entrega a un colegio.
;
; Va a ProgramData y no a Program Files para que un administrador pueda
; reemplazar los paquetes despues sin pelearse con los permisos, y en un
; subdirectorio propio (Biblioteca\) para no mezclarse con lo de OPS Master.
; ---------------------------------------------------------------------------
;
; uninsneveruninstall NO es opcional aqui.
;
; Todo lo que se declara en [Files] lo borra Inno al desinstalar, por su cuenta
; y antes de que corra una sola linea de [Code]. Sin este flag, la pregunta de
; mas abajo ("quieres borrar la carpeta de trabajo?") no servia de nada: los
; archivos ya no estaban cuando se hacia. Comprobado: se perdian la licencia y
; los paquetes en cada desinstalacion, incluso respondiendo que no.
;
; Con el flag, Inno los deja quietos y quien decide es el codigo: pregunta si
; hay alguien mirando, y conserva si la desinstalacion es silenciosa.
Source: "..\trabajo\*"; DestDir: "{commonappdata}\AVACOM\Biblioteca\trabajo"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall
#endif

[Dirs]
; El punto de encuentro con AVACOM OPS Master.
;
; PERMISOS: users-modify, no solo lectura. La aplicacion corre como el usuario
; interactivo (el profesor, que normalmente no es administrador) y ESCRIBE
; enlace.json aqui en cada arranque. Con permisos de solo lectura el archivo no
; llegaria a crearse, la nota nunca apareceria, y OPS Master concluiria que no
; hay contenido instalado, sin ningun error visible que explicara por que.
Name: "{commonappdata}\AVACOM"; Permissions: users-modify
Name: "{commonappdata}\AVACOM\contenido"; Permissions: users-modify

#ifdef ConContenidoDemo
; Subdirectorio propio de Biblioteca dentro del territorio compartido. La
; aplicacion solo LEE de aqui, pero se deja con permiso de modificacion para
; que un administrador pueda reemplazar los paquetes sin elevar nada.
Name: "{commonappdata}\AVACOM\Biblioteca"; Permissions: users-modify
#endif

[Icons]
Name: "{group}\{#Nombre}"; Filename: "{app}\{#Ejecutable}"
Name: "{autodesktop}\{#Nombre}"; Filename: "{app}\{#Ejecutable}"; Tasks: escritorio

[Run]
Filename: "{app}\{#Ejecutable}"; Description: "{cm:LaunchProgram,{#Nombre}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  PaginaValidacion: TOutputMsgMemoWizardPage;
  ValidacionFallo: Boolean;

// --------------------------------------------------------------- utilidades

function AppEstaCorriendo(): Boolean;
begin
  // La ventana principal se llama exactamente asi. Es suficiente para el caso
  // que importa: el profesor la dejo abierta y vamos a reemplazar sus archivos.
  Result := FindWindowByWindowName('AVACOM Biblioteca') <> 0;
end;

function OpsMasterInstalado(): Boolean;
begin
  // Se miran los DOS sitios. Comprobar solo Program Files no basta: en una
  // maquina real se encontro %ProgramData%\AVACOM\OPS Master con datos del
  // otro producto mientras Program Files no tenia nada, porque OPS Master
  // puede estar instalado en otra ruta o haber dejado su configuracion ahi.
  // Con la comprobacion incompleta, este desinstalador habria intentado
  // llevarse la carpeta compartida con los datos del vecino dentro.
  Result := DirExists(ExpandConstant('{autopf}\AVACOM\OPS Master'))
         or DirExists(ExpandConstant('{commonappdata}\AVACOM\OPS Master'));
end;

function EspacioLibreMB(): Int64;
var
  Libre, Total: Int64;
begin
  Result := -1;
  if GetSpaceOnDisk64(ExtractFileDrive(ExpandConstant('{app}')), Libre, Total) then
    Result := Libre / 1048576;
end;

// ----------------------------------------------------- pantalla de validacion

procedure InicializarValidacion();
var
  S: String;
  Libre: Int64;
begin
  ValidacionFallo := False;
  S := '';

  // Sistema operativo y arquitectura los comprueba Inno antes de llegar aqui
  // (MinVersion y ArchitecturesAllowed), asi que si estamos en esta pantalla
  // ya pasaron. Se informan igual para que quede constancia en pantalla.
  S := S + 'Sistema operativo    Windows ' + GetWindowsVersionString + '  [correcto]' + #13#10;
  S := S + 'Arquitectura         x64  [correcto]' + #13#10;

  Libre := EspacioLibreMB();
  if Libre < 0 then
    S := S + 'Espacio en disco     no se pudo comprobar' + #13#10
  else if Libre < {#EspacioMinimoMB} then
  begin
    S := S + 'Espacio en disco     ' + IntToStr(Libre) + ' MB libres, hacen falta {#EspacioMinimoMB} MB  [INSUFICIENTE]' + #13#10;
    ValidacionFallo := True;
  end
  else
    S := S + 'Espacio en disco     ' + IntToStr(Libre) + ' MB libres  [correcto]' + #13#10;

  if IsAdminInstallMode then
    S := S + 'Permisos             administrador  [correcto]' + #13#10
  else
  begin
    S := S + 'Permisos             faltan permisos de administrador  [INSUFICIENTE]' + #13#10;
    ValidacionFallo := True;
  end;

  if AppEstaCorriendo() then
  begin
    S := S + 'AVACOM Biblioteca    ESTA ABIERTA  [hay que cerrarla]' + #13#10;
    ValidacionFallo := True;
  end
  else
    S := S + 'AVACOM Biblioteca    no esta abierta  [correcto]' + #13#10;

  if DirExists(ExpandConstant('{app}')) then
    S := S + 'Instalacion previa   se encontro una, se actualizara' + #13#10
  else
    S := S + 'Instalacion previa   ninguna, sera una instalacion nueva' + #13#10;

  // Ver el comentario de RutaMaximaChars arriba. Una ruta demasiado larga no
  // da error al copiar: da una aplicacion que no abre y no dice por que.
  if Length(ExpandConstant('{app}')) > {#RutaMaximaChars} then
  begin
    S := S + 'Ruta de instalacion  ' + IntToStr(Length(ExpandConstant('{app}')))
           + ' caracteres, el maximo es {#RutaMaximaChars}  [DEMASIADO LARGA]' + #13#10;
    ValidacionFallo := True;
  end
  else
    S := S + 'Ruta de instalacion  ' + IntToStr(Length(ExpandConstant('{app}'))) + ' caracteres  [correcto]' + #13#10;

  // La coexistencia con OPS Master es lo esperado, no un conflicto. Se dice en
  // positivo a proposito: si el instalador insinuara un problema, alguien
  // acabaria desinstalando el otro producto para "arreglarlo".
  if OpsMasterInstalado() then
    S := S + 'AVACOM OPS Master    instalado en este equipo, convivira sin problema' + #13#10;

  S := S + #13#10;
  if ValidacionFallo then
    S := S + 'Hay algo que resolver antes de continuar. Corrigelo y pulsa Atras y luego Siguiente para volver a comprobar.'
  else
    S := S + 'Todo listo para instalar.';

  PaginaValidacion.RichEditViewer.Text := S;
end;

// ------------------------------------------------------------------- eventos

procedure InitializeWizard();
begin
  PaginaValidacion := CreateOutputMsgMemoPage(wpSelectDir,
    'Comprobacion del sistema',
    'Se revisa que este equipo pueda ejecutar AVACOM Biblioteca',
    'Resultado de las comprobaciones:',
    '');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PaginaValidacion.ID then
    InicializarValidacion();

#ifdef ConContenidoDemo
  // La ultima pantalla dice donde quedo la carpeta de trabajo. Sin ese dato el
  // administrador tiene que adivinarlo, y la aplicacion no muestra ni un
  // material hasta que se le indica.
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption :=
      WizardForm.FinishedLabel.Caption + #13#10#13#10 +
      'Esta version trae contenido de demostracion.' + #13#10#13#10 +
      'Al abrir la aplicacion, ve a Administracion, pega esta ruta y pulsa ' +
      '"Usar esta ruta":' + #13#10#13#10 +
      ExpandConstant('{commonappdata}\AVACOM\Biblioteca\trabajo') + #13#10#13#10 +
      'Despues pulsa "Revisar e instalar" y entra a Contenido AVACOM.';
#endif
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = PaginaValidacion.ID) and ValidacionFallo then
  begin
    if AppEstaCorriendo() then
      MsgBox('AVACOM Biblioteca esta abierta.' + #13#10#13#10 +
             'Cierrala y vuelve a intentarlo. El instalador no la cierra por su cuenta ' +
             'para no interrumpir una clase en curso.', mbError, MB_OK)
    else if Length(ExpandConstant('{app}')) > {#RutaMaximaChars} then
      MsgBox('La ruta de instalacion es demasiado larga.' + #13#10#13#10 +
             'Elige una mas corta, como C:\Program Files\AVACOM\Biblioteca. ' +
             'Con una ruta larga los archivos se copian bien pero la aplicacion ' +
             'no llega a abrir, y no da ningun mensaje que lo explique.', mbError, MB_OK)
    else
      MsgBox('Falta resolver algo de la lista antes de continuar.', mbError, MB_OK);
    Result := False;
  end;
end;

// ---------------------------------------------------------- desinstalacion

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DatosUsuario: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  // El indice del equipo NO se borra sin preguntar. Ahi esta el catalogo de lo
  // que el aula tiene instalado; perderlo por descuido obliga a reinstalar
  // todos los paquetes, y eso en un colegio es una manana de trabajo.
  //
  // EN MODO SILENCIOSO NO SE PREGUNTA Y NO SE BORRA. Con /SUPPRESSMSGBOXES un
  // MB_YESNO se auto-responde que SI, asi que preguntar sin comprobar esto
  // borraba los datos en toda desinstalacion automatizada. Comprobado: paso.
  DatosUsuario := ExpandConstant('{localappdata}\AVACOM\world.avacom.biblioteca');
  if DirExists(DatosUsuario) and (not UninstallSilent()) then
  begin
    if MsgBox('Se desinstalo AVACOM Biblioteca.' + #13#10#13#10 +
              'En este equipo queda el indice del contenido instalado. Si vas a ' +
              'volver a instalar la aplicacion, conservalo y no habra que ' +
              'reinstalar los paquetes.' + #13#10#13#10 +
              'Quieres borrarlo tambien?', mbConfirmation, MB_YESNO) = IDYES then
      DelTree(DatosUsuario, True, True, True);
  end;

#ifdef ConContenidoDemo
  // La carpeta de trabajo tampoco se borra sin preguntar: el administrador
  // pudo haber reemplazado los paquetes de demostracion por los de verdad,
  // y ahi dentro esta la clave privada de este nodo.
  // Mismo motivo que arriba: en silencio se conserva.
  if DirExists(ExpandConstant('{commonappdata}\AVACOM\Biblioteca')) and (not UninstallSilent()) then
  begin
    if MsgBox('Tambien quedo la carpeta de trabajo (licencia, claves de este ' +
              'equipo y paquetes de contenido).' + #13#10#13#10 +
              'Quieres borrarla?', mbConfirmation, MB_YESNO) = IDYES then
      DelTree(ExpandConstant('{commonappdata}\AVACOM\Biblioteca'), True, True, True);
  end;
#endif

  // El punto de encuentro solo se retira si el otro producto ya no esta. Si
  // OPS Master sigue instalado y le quitamos la carpeta, su backend deja de
  // poder leer la nota el dia que se reinstale la biblioteca.
  if not OpsMasterInstalado() then
  begin
    DeleteFile(ExpandConstant('{commonappdata}\AVACOM\contenido\enlace.json'));
    RemoveDir(ExpandConstant('{commonappdata}\AVACOM\contenido'));
    RemoveDir(ExpandConstant('{commonappdata}\AVACOM'));
  end;
end;
