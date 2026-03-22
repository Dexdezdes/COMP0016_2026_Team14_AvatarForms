# AvatarForms

AvatarForms is a Windows application for conversion of digital forms into 3D avatars with a fully local AI interview pipeline.

## Project Structure

```
.
├── AvatarFormsApp/    # Backend (.NET 10), Frontend (WinUI), Database (SQLite).
├── Backend/           # Python AI agent orchestration layer with APIs (HTTP + WebSocket)
├── HeadTTS/           # Open-sourced JavaScript TTS solution by @met4citizen 
├── .gitignore         # Git ignore configuration
└── README.MD          # This file
```

## Current Project Configuration

1. SDK: `Uno.Sdk` `6.5.31`

2. .NET: `net10.0-windows10.0.19041.0` (Windows)

3. Frontend packages include:
   - `Uno.WinUI` `5.2.161`
   - `Uno.Material.WinUI` `5.3.1`
   - `Uno.Toolkit.WinUI` `8.3.2`
   - `CommunityToolkit.Mvvm` `8.4.0`
   - `Microsoft.EntityFrameworkCore.Sqlite` `10.0.2`

The application copies `Backend/**/*` and `HeadTTS/**/*` into build output and manages services within the app.

## Requirements

1. Visual Studio 2022 or newer (with Windows App SDK/WinUI workload) or VS Code + .NET tooling
2. .NET 10 SDK
3. Windows 10/11 SDK `10.0.19041.0` or newer
4. Python 3.x
5. A `.llamafile` model placed in `Backend/` (Qwen3_4B_Q6_K is recommended)
 
### Note

"Enable long paths" option must be enabled to overcome Windows 260-character limit for file paths, which NuGet packages often break.

#### **Option A:** Admin PowerShell

```powershell
New-ItemProperty -Path "HKLM:\System\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force
```

#### **Option B:** Windows Settings

1. Navigate to Settings > System > Advanced.

2. Make sure the "Enable long paths" option is enabled.


## Setup

1. Clone the repository.
2. From the repository root:

```powershell
cd Backend
python -m venv env
.\env\Scripts\activate
pip install -r requirements.txt
```
3. Compile the Python into an executable:

```powershell
pyinstaller main.spec
```
4. Ensure at least one `.llamafile` exists in `Backend/`. `Qwen3_4B_Q6_K` is recommended. Can be downloaded from Hugging Face: https://huggingface.co/mozilla-ai/Qwen3-4B-llamafile/tree/main.

## How To Run

From repository root, build the solution:

```powershell
dotnet build .\AvatarFormsApp\AvatarFormsApp.sln
```

Then run from Visual Studio or CLI:

```powershell
dotnet run --project .\AvatarFormsApp\AvatarFormsApp\AvatarFormsApp.csproj -f net10.0-windows10.0.19041.0
```

You can also use workspace tasks:
- `build`
- `publish`
- `watch`

## Runtime

When you start an interview flow in the app:
- `LlamafileProcessService` starts the `.llamafile` server on port `8081`.
- `PythonProcessService` starts the Python backend using the compiled `Backend/dist/main/main.exe`.
- `ResponseAPIService` starts the response API server for response collection on port `5000`.
- `QuestionnaireAPIService` sends in questionnaire data to the questionnaire API server started in `api.py` on port `8882`.
- The Python backend initialises WebSocket API for real-time speech relay between the Python agent orchestration layer and HeadTTS (3D avatar rendering + TTS layer) on port `8083`.
- Avatar is loaded through WebView2.

## Environment Variables

- Cloud mode requires `FIREWORKS_API_KEY` (read by Python backend).
- Local mode is the default app flow and uses `--local` with the `.llamafile` server.

## Troubleshooting

- No AI model starts:
	- Verify a `*.llamafile` exists in `Backend`.
- Python backend fails:
	- Verify `Backend\env\Scripts\python.exe` exists and packages are installed from `requirements.txt`.
- Build/path issues on Windows:
	- Enable long paths and restore NuGet packages.
- Web avatar does not render:
	- Ensure WebView2 runtime is installed and `HeadTTS` files are present in output.

## Packaging

For packaging/distribution details, see:
- `AvatarFormsApp/PACKAGING_GUIDE.md`

## Additional Development References

- WinUI control/design reference:
	- WinUI Gallery: https://www.microsoft.com/store/productId/9P3JFPWWDZRC
- Unpackaged deployment reference:
	- https://docs.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps