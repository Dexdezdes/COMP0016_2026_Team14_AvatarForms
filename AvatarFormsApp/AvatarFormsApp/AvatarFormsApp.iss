[Setup]
; --- Basic Metadata ---
AppId={{8B321945-C943-4C21-9B3E-B68C134017F8}
AppName=Avatarformsapp
AppVersion=1.0
AppPublisher=Group14
DefaultDirName={autopf}\Avatarformsapp
DefaultGroupName=Avatarformsapp
UninstallDisplayIcon={app}\Avatarformsapp.exe
OutputBaseFilename=Avatarformsapp_Setup

; --- High-Capacity Configuration ---
; Essential for 3.4GB payload to avoid 32-bit pointer overflows
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra
; Ensures the installer doesn't trigger "Out of memory" on extraction
LZMAUseSeparateProcess=yes

; --- UI/Security ---
PrivilegesRequired=admin
SetupIconFile=AppIcon.ico

[Files]
; Source points to the 'BuildOutput' folder created by your PS script
Source: ".\BuildOutput\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Avatarformsapp"; Filename: "{app}\Avatarformsapp.exe"
Name: "{commondesktop}\Avatarformsapp"; Filename: "{app}\Avatarformsapp.exe"

[Run]
Filename: "{app}\Avatarformsapp.exe"; Description: "{cm:LaunchProgram,Avatarformsapp}"; Flags: nowait postinstall skipifsilent
