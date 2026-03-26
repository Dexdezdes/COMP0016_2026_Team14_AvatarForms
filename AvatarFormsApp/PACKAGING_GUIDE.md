# AvatarFormsApp Packaging Guide

This guide explains how to package and distribute your AvatarFormsApp application.

## Prerequisites

- Visual Studio 2022 or later
- .NET 10 SDK
- Windows 10/11 SDK (19041 or later)

## Self-Contained Deployment (Folder + EXE)

This creates a folder with all dependencies and a single executable.

### Steps:

1. **Open PowerShell** in the solution directory

2. **Publish as self-contained:**
   ```powershell
   cd AvatarFormsApp
   dotnet publish -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RuntimeIdentifierOverride=win-x64 -p:SatelliteResourceLanguages=en-US
   ```

3. **Find the output:**
   - Navigate to: `AvatarFormsApp\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\`
   - This folder contains everything needed to run the app

### Distribution:
1. Zip the entire `publish` folder
2. Users can extract and run `AvatarFormsApp.exe`

---

## Distribution Checklist

Before distributing your app, ensure:

- [ ] WebView2 Runtime is installed on target machines (or bundle it)
- [ ] Microsoft Visual C++ Redistributable is installed
---

## Troubleshooting

### "Application failed to start"
- Ensure WebView2 Runtime is installed: https://developer.microsoft.com/microsoft-edge/webview2/

### "Speech recognition not working"
- Users must enable "Online Speech Recognition" in Windows Privacy Settings

---

## File Size Considerations

- **Self-Contained Folder**: ~400-600 MB (with Python runtime application)

For smaller distribution, consider framework-dependent deployment and require users to install .NET 10 Runtime.
