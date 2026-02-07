[Setup]
; --- Basic Metadata ---
AppId={{8B321945-C943-4C21-9B3E-B68C134017F8}}
AppName=Avatarformsapp
AppVersion=1.0
AppPublisher=Group14
DefaultDirName={autopf}\Avatarformsapp
DefaultGroupName=Avatarformsapp
UninstallDisplayIcon={app}\Avatarformsapp.exe
OutputBaseFilename=Avatarformsapp_Setup

; --- High-Capacity Configuration ---
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra
LZMAUseSeparateProcess=yes

; --- UI/Security ---
PrivilegesRequired=admin
SetupIconFile=Assets\Icons\favicon.ico

[Files]
; 1. Small files + solidbreak 
; FIX: The 'solidbreak' flag tells Inno Setup "Put the UI files in a fast-access block"
Source: ".\BuildOutput\*"; DestDir: "{app}"; Excludes: "*.llamafile"; Flags: ignoreversion recursesubdirs createallsubdirs solidbreak

; 2. The Heavy Model (Stored, not compressed)
; This stays at the end so it doesn't block the installer from opening.
Source: ".\BuildOutput\Llamafile\google_gemma-3-4b-it-Q6_K.llamafile"; DestDir: "{app}\Llamafile"; Flags: nocompression ignoreversion

[Icons]
Name: "{group}\Avatarformsapp"; Filename: "{app}\Avatarformsapp.exe"
Name: "{commondesktop}\Avatarformsapp"; Filename: "{app}\Avatarformsapp.exe"

[Run]
Filename: "{app}\Avatarformsapp.exe"; Description: "{cm:LaunchProgram,Avatarformsapp}"; Flags: nowait postinstall skipifsilent