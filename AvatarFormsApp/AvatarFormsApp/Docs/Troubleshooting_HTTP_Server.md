# Troubleshooting: HTTP Server Not Starting

## Current Issue
All 10 retry attempts failing - Flask HTTP server on port 8882 never becomes available.

## Diagnostic Steps Added

### 1. **Added Python Output Logging**
The ViewModel now subscribes to Python process output:
```csharp
_pythonProcessService.OutputReceived += (output) =>
{
    System.Diagnostics.Debug.WriteLine($"[PYTHON] {output}");
};
```

### 2. **Added 2-Second Delay**
After starting Python backend, wait 2 seconds before sending questionnaire:
```csharp
await Task.Delay(2000); // Give Flask time to start
```

## What to Check Next

### Run the app again and look for these in Debug Output:

#### ✅ **Success Pattern:**
```
[PYTHON] HTTP server started on http://localhost:8882
[PYTHON]  * Running on http://0.0.0.0:8882
[PYTHON]  * Running on http://127.0.0.1:8882
Attempt 1/10: Sending questionnaire to http://localhost:8882/questionnaire
Successfully sent questionnaire...
```

#### ❌ **Failure Patterns:**

**Pattern 1: No Python output at all**
- Python process isn't starting
- Check: Is `main.py` in the Backend folder?
- Check: Is Python environment set up?

**Pattern 2: Python starts but no HTTP server message**
```
[PYTHON] Some output...
[PYTHON ERROR] ModuleNotFoundError: No module named 'flask'
```
- **Fix:** Install Flask: `pip install flask`

**Pattern 3: Python starts but errors**
```
[PYTHON ERROR] ImportError: cannot import name 'start_http_server' from 'api'
```
- **Fix:** Check that `api.py` exists in Backend folder

**Pattern 4: Port already in use**
```
[PYTHON ERROR] OSError: [WinError 10048] Only one usage of each socket address...
```
- **Fix:** Port 8882 already in use, kill the process or use different port

## Required Files Check

Make sure these files exist in `Backend\` folder:
- ✅ `main.py` - Entry point
- ✅ `api.py` - HTTP server with Flask
- ✅ `sockets.py` - WebSocket server
- ✅ `formatting.py` - Color output utilities
- ✅ `agents.py` - AI agents

## Install Flask

If Flask is not installed, run:
```bash
cd Backend
pip install flask
```

Or create `requirements.txt`:
```txt
flask>=3.0.0
websockets>=12.0
python-dotenv>=1.0.0
```

Then:
```bash
pip install -r requirements.txt
```

## Manual Test

Test if Python backend starts manually:
```bash
cd Backend
python main.py --local --llama_port 8081 -p 8883 --http_port 8882
```

You should see:
```
HTTP server started on http://localhost:8882
 * Running on all addresses (0.0.0.0)
 * Running on http://127.0.0.1:8882
 * Running on http://192.168.x.x:8882
WebSocket server started on port 8883.
Waiting for questionnaire data from frontend...
```

Then test HTTP endpoint:
```powershell
curl -X POST http://localhost:8882/questionnaire `
  -H "Content-Type: application/json" `
  -d '{"questions":["Q1","Q2"],"description":"Test"}'
```

Should return:
```json
{"message":"Questionnaire received","status":"success"}
```

## Next Run

After these changes:
1. **Rebuild** the C# application
2. **Run** again
3. **Check Debug Output** for `[PYTHON]` messages
4. **Report** what you see in the Python output

The debug output will now show exactly what's happening (or not happening) with the Python backend!
