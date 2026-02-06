# --- 1. BUILD CONFIGURATION ---
$AppPath = "$PSScriptRoot\BuildOutput"

Write-Host "--- Starting Production Build: Avatarformsapp ---" -ForegroundColor Cyan

dotnet publish -c Release `
  -f net10.0-windows10.0.19041.0 `
  -r win-x64 `
  --self-contained true `
  /p:Platform=x64 `
  /p:PlatformTarget=x64 `
  /p:WindowsPackageType=None `
  /p:IncrementalBuild=false `
  /p:RuntimeIdentifier=win-x64 `
  -o $AppPath

Write-Host "Build Complete. Files located in: $AppPath" -ForegroundColor Green

# --- 2. COMPILE INSTALLER ---
Write-Host "--- Bundling into Single EXE ---" -ForegroundColor Cyan
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ".\Avatarformsapp.iss"

# --- 3. SIGN THE FINAL EXE ---
# Note: Requires signtool.exe (part of Windows SDK)
$setupExe = ".\Output\Avatarformsapp_Setup.exe"
$pfxPath = ".\Group14Key.pfx"
$password = "avatarforms"

Write-Host "--- Digitally Signing Installer ---" -ForegroundColor Cyan
& "signtool.exe" sign /f $pfxPath /p $password /t http://timestamp.digicert.com /v $setupExe

Write-Host "Success: Signed installer created at $setupExe" -ForegroundColor Green
