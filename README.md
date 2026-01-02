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
.\env\Scripts\activate
```

Install dependencies (Just Ollama and dotnet for local and requests for cloud)

```bash
pip install -r requirements.txt
```

### 4. Add cloud ai token
Add a file called "token.txt" in the Cloud folder where the cloud_prototype.py file is located and paste your token there.
The cloud_prototype.py file currently uses firework.ai as the cloud ai provider.

### 5. Run app in Visual Studio
Choose to run AvatarFormsApp.slnx 