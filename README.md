# AvatarForms

This project uses a C# WinUI 3 frontend and a Python-based AI backend powered by Ollama.

## Setup Instructions

To get the AI components running on your local machine, follow these steps:

### 1. Install Ollama
Download and install Ollama from [ollama.com](https://ollama.com).
Once installed, pull the required model (Dex uses gemma3:1b because of Ram constraints change local_prototype.py if needed):
```bash
ollama pull gemma3:1b
```

### 2. Import the github project into Visual Studio
Just choose clone from github and paste the url of this repository.

### 3. Set Up Python Environment
In the AvatarFormsApp directory just below the AvatarForms root folder
Create the python virtual environment:

```bash
python -m venv env
```

Activate the environment

```bash
# Windows
.\env\Scripts\activate
# Mac
source env/bin/activate
```

Install dependencies (Just Ollama and dotnet for local and requests for cloud)

```bash
pip install -r requirements.txt
```

### 4. Add cloud ai token
Add a file called "token.txt" in the Cloud folder where the cloud_prototype.py file is located and paste your token there.
The cloud_prototype.py file currently uses firework.ai as the cloud ai provider.

### 5. Allow long paths
Windows has a 260-character limit for file paths, and NuGet packages often break this. 

Need to open powershell as admin then do
```bash
New-ItemProperty -Path "HKLM:\System\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force
```

After running the command, right click the solution 'AvatarformsApp' and select the restore nuget packages in Visual studio.

### 6. Run app in Visual Studio
Choose to run AvatarFormsApp.sln 
Use Debug and x64

## MacBook special (replaces step 5 and 6 which are windows steps)
Because of the MacOS security, there are steps to follow to launch the app.

### 5. Kill the old app first (just in case)
```bash
killall AvatarFormsApp || true
```

### 6. Build the updated code
```bash
dotnet build -f net10.0-maccatalyst
```

### 7. Strip the security flags (requires password)
```bash
sudo xattr -cr bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/AvatarFormsApp.app
```

### 8. Launch
```bash
open -n bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/AvatarFormsApp.app
```
