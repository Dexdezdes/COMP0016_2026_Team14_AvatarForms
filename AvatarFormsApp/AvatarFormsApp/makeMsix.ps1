# --- 1. SETUP ---
$scriptRoot = $PSScriptRoot
$pfxPath    = Join-Path $scriptRoot "Group14Key.pfx"
$password   = "avatarforms"

Write-Host "--- Importing Certificate to User Store ---" -ForegroundColor Cyan
# Convert password to SecureString (required for Import-PfxCertificate)
$securePass = ConvertTo-SecureString -String $password -AsPlainText -Force

# Import the PFX into the CurrentUser\My (Personal) store
# This allows the build tools to find it without needing the password passed to MSBuild
$cert = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePass

if (-not $cert) {
    Write-Error "Failed to import certificate. Check the file path and password."
    exit 1
}

$thumbprint = $cert.Thumbprint
Write-Host "Certificate Imported. Thumbprint: $thumbprint" -ForegroundColor Green

# --- 2. UNO-MANAGED PUBLISH ---
Write-Host "--- Starting Uno-Managed Production Build ---" -ForegroundColor Cyan

# CHANGE:
# 1. Removed PackageCertificateKeyFile & PackageCertificatePassword
# 2. Added PackageCertificateThumbprint
dotnet publish -c Release `
  -f net10.0-windows10.0.19041.0 `
  -r win-x64 `
  --self-contained true `
  /p:Platform=x64 `
  /p:PlatformTarget=x64 `
  /p:WindowsPackageType=MSIX `
  /p:GenerateAppxPackageOnBuild=true `
  /p:AppxPackageSigningEnabled=true `
  /p:PackageCertificateThumbprint="$thumbprint" `
  /p:AppxPackageDir="AppPackages\" `
  /p:UapAppxPackageBuildMode=SideloadOnly `
  /p:IncrementalBuild=false `
  /p:AppxBundle=Never `
  /p:RuntimeIdentifier=win-x64
