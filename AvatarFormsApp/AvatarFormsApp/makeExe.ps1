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

# --- If signtool not found, change the version 10.0.26100.0 to the latest one in the directory path before it ---
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

Write-Host "--- Digitally Signing Installer ---" -ForegroundColor Cyan
& $signtool sign /f $pfxPath /p $password /fd sha256 /t http://timestamp.digicert.com /v $setupExe

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Success: Signed installer created at $setupExe" -ForegroundColor Green
} else {
    Write-Host "❌ Signing Failed! Check the errors above." -ForegroundColor Red
}
