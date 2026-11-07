; Dusk installer (Inno Setup 6). Installeert voor de huidige gebruiker, zonder beheerdersrechten.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{5B7D2C41-9E3A-4F6B-8D1C-2A9E7F4B3C60}
AppName=Dusk
AppVersion={#AppVersion}
AppVerName=Dusk {#AppVersion}
AppPublisher=Rida Abouhayan
AppPublisherURL=https://github.com/ridaabouhayan-hue/dusk
DefaultDirName={localappdata}\Programs\Dusk
DefaultGroupName=Dusk
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=..\out
OutputBaseFilename=Dusk-Setup
SetupIconFile=..\assets\Dusk.ico
UninstallDisplayIcon={app}\Dusk.exe
UninstallDisplayName=Dusk
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
SetupMutex=DuskSetup

[Languages]
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
nl.Autostart=Starten met Windows (aanbevolen, dan werkt de sneltoets altijd)
en.Autostart=Start with Windows (recommended, so the shortcut always works)
fr.Autostart=Démarrer avec Windows (recommandé, le raccourci marche toujours)
es.Autostart=Iniciar con Windows (recomendado, el atajo siempre funciona)
nl.Desktop=Snelkoppeling op het bureaublad
en.Desktop=Desktop shortcut
fr.Desktop=Raccourci sur le bureau
es.Desktop=Acceso directo en el escritorio
nl.OpenNow=Dusk openen
en.OpenNow=Open Dusk
fr.OpenNow=Ouvrir Dusk
es.OpenNow=Abrir Dusk
nl.AskDeleteData=Wil je ook de instellingen en het logboek van Dusk verwijderen?
en.AskDeleteData=Do you also want to delete the Dusk settings and log?
fr.AskDeleteData=Voulez-vous aussi supprimer les paramètres et le journal de Dusk ?
es.AskDeleteData=¿Quieres borrar también los ajustes y el registro de Dusk?

[Tasks]
Name: "autostart"; Description: "{cm:Autostart}"
Name: "desktop"; Description: "{cm:Desktop}"; Flags: unchecked

[Files]
Source: "..\out\Dusk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Dusk"; Filename: "{app}\Dusk.exe"
Name: "{userdesktop}\Dusk"; Filename: "{app}\Dusk.exe"; Tasks: desktop

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Dusk"; ValueData: """{app}\Dusk.exe"" --opstarten"; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Dusk"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\Dusk.exe"; Description: "{cm:OpenNow}"; Flags: nowait postinstall skipifsilent
; bijwerken vanuit Dusk (met /BIJWERKEN): Dusk daarna vanzelf weer starten, stil in de taakbalk
Filename: "{app}\Dusk.exe"; Parameters: "--opstarten"; Flags: nowait; Check: IsUpdateRun

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM Dusk.exe /F"; Flags: runhidden; RunOnceId: "StopDusk"

[Code]
var
  Installed: Boolean;

// Is deze installer gestart door "bijwerken met een klik" in Dusk?
function IsUpdateRun: Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 1 to ParamCount do
    if CompareText(ParamStr(i), '/BIJWERKEN') = 0 then Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then Installed := True;
end;

// Mislukt het bijwerken (virusscanner, volle schijf): Dusk toch weer starten, de oude versie staat er nog
procedure DeinitializeSetup;
var
  rc: Integer;
  exe: String;
begin
  if IsUpdateRun and (not Installed) then
  begin
    exe := ExpandConstant('{localappdata}\Programs\Dusk\Dusk.exe');
    if FileExists(exe) then Exec(exe, '--opstarten', '', SW_SHOWNORMAL, ewNoWait, rc);
  end;
end;

// Na het verwijderen: vragen of de instellingen in %APPDATA%\Dusk ook weg mogen. Bij stil verwijderen nooit.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  dataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    dataDir := ExpandConstant('{userappdata}\Dusk');
    if DirExists(dataDir) and (not UninstallSilent) then
      if MsgBox(CustomMessage('AskDeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(dataDir, True, True, True);
  end;
end;
