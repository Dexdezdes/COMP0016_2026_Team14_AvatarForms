# AvatarFormsApp Packaging Guide

This guide explains how to package and distribute your AvatarFormsApp application.

## Prerequisites

- Visual Studio 2022 or later
- .NET 10 SDK
- Windows 10/11 SDK (19041 or later)

## Option 1: MSIX Package (Recommended)

MSIX is the modern Windows app packaging format that provides automatic updates, clean installation/uninstallation, and Microsoft Store distribution.

### Steps:

1. **Open Developer PowerShell** or Command Prompt

2. **Publish the app as MSIX:**
   ```powershell
   cd AvatarFormsApp
   dotnet publish -c Release -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifierOverride=win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
   ```

3. **Find the package:**
   - Navigate to: `AvatarFormsApp\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\`
   - Look for the `.msix` file

4. **Sign the package (for distribution):**
   ```powershell
   # Create a test certificate (one-time)
   New-SelfSignedCertificate -Type Custom -Subject "CN=Group14" -KeyUsage DigitalSignature -FriendlyName "AvatarFormsApp Signing Certificate" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

   # Sign the MSIX
   SignTool sign /fd SHA256 /a /f YourCertificate.pfx /p YourPassword AvatarFormsApp.msix
   ```

### Installation:
- Double-click the `.msix` file
- Users may need to install the certificate first for sideloading

---

## Option 2: Self-Contained Deployment (Folder + EXE)

This creates a folder with all dependencies and a single executable.

### Steps:

1. **Open PowerShell** in the solution directory

2. **Publish as self-contained:**
   ```powershell
   cd AvatarFormsApp
   dotnet publish -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RuntimeIdentifierOverride=win-x64
   ```

3. **Find the output:**
   - Navigate to: `AvatarFormsApp\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\`
   - This folder contains everything needed to run the app

4. **Important: Copy Python Environment**
   - Your app requires the Python virtual environment (`env` folder)
   - Copy the `env` folder from your project root to the publish folder:
   ```powershell
   Copy-Item -Path "..\..\env" -Destination "bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\env" -Recurse
   ```

5. **Copy Additional Resources** (if not already included):
   - HeadTTS folder
   - Cloud folder
   - Local folder
   - TalkingHead folder

### Distribution:
1. Zip the entire `publish` folder
2. Users can extract and run `AvatarFormsApp.exe`

---

## Option 3: Single File Executable (Advanced)

Create a single executable with embedded dependencies (note: this may not work well with WinUI 3).

```powershell
cd AvatarFormsApp
dotnet publish -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:RuntimeIdentifierOverride=win-x64
```

**Warning:** Single-file publishing may have issues with:
- WebView2 runtime
- Python subprocess execution
- XAML resources

---

## Automated Build Scripts

### Windows (PowerShell)

Use the provided `build-package.ps1` script to automate the packaging process.

### Usage:
```powershell
.\build-package.ps1 -PackageType "msix"  # For MSIX package
.\build-package.ps1 -PackageType "folder"  # For folder deployment
```

---

## Distribution Checklist

Before distributing your app, ensure:

- [ ] WebView2 Runtime is installed on target machines (or bundle it)
- [ ] Python virtual environment (`env` folder) is included
- [ ] All Python scripts (agent.py, cloud_prototype.py) are present
- [ ] HeadTTS web content is included
- [ ] Speech recognition permissions are documented for users
- [ ] Microsoft Visual C++ Redistributable is installed (for self-contained)

---

## Troubleshooting

### "Application failed to start"
- Ensure WebView2 Runtime is installed: https://developer.microsoft.com/microsoft-edge/webview2/

### "Python script not found"
- Verify the `env`, `Local`, and `Cloud` folders are in the same directory as the executable

### "Speech recognition not working"
- Users must enable "Online Speech Recognition" in Windows Privacy Settings

---

## File Size Considerations

- **MSIX Package**: ~150-300 MB (compressed)
- **Self-Contained Folder**: ~400-600 MB (with Python environment)
- **Framework-Dependent**: ~50-100 MB (requires .NET 10 installed on target machine)

For smaller distribution, consider framework-dependent deployment and require users to install .NET 10 Runtime.
