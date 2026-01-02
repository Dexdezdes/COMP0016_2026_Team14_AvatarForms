# AvatarForms
# Avatar Forms Project

This project uses a C# WinUI 3 frontend and a Python-based AI backend powered by Ollama.

## Setup Instructions

To get the AI components running on your local machine, follow these steps:

### 1. Install Ollama
Download and install Ollama from [ollama.com](https://ollama.com).
Once installed, pull the required model (Dex uses gemma3:1b because of Ram constraints change loacl_prototype.py if needed):
```bash
ollama pull gemma3:1b```

### 2. Import the github project into Visual Studio
Just choose clone from github and paste the url of this repository.

### 3. Set Up Python Environment
In the AvatarFormsApp directory just below the AvatarForms root folder
Create the python virtual environment:

```bash
python -m venv env
```

# Activate the environment

```bash
.\env\Scripts\activate
```

# Install dependencies (Just Ollama and dotnet, same as the original version)

```bash
pip install -r requirements.txt
```

### 4. Run app in Visual Studio
Choose to run AvatarFormsApp.slnx 