# HTTP Connection Issue - Fixed ✅

## Problem
```
Exception thrown: 'System.Net.Http.HttpRequestException'
HTTP error sending questionnaire: No connection could be made because the target machine actively refused it. (localhost:8882)
```

## Root Causes

### 1. **Argument Parser Bug in Python** ❌
**File:** `Backend\main.py` line 231

**Was:**
```python
parser.add_argument("http_port", type=int, default=8882, ...)  # Positional argument
```

**Problem:** Python expected `http_port` as a positional argument: `python main.py 8882`  
**But C# was sending:** `--http_port 8882` (optional argument)

**Fixed to:**
```python
parser.add_argument("--http_port", type=int, default=8882, ...)  # Optional argument with --
```

### 2. **Timing Issue** ⏱️
**Problem:** C# sent HTTP request immediately after starting Python process, before Flask server was ready

**Fixed:** Added retry logic in `QuestionnaireAPIService.cs`
- Retries up to 10 times
- 500ms delay between attempts
- Total max wait: 5 seconds

### 3. **Flask Startup Delay** ⏳
**Problem:** Flask needs a moment to bind to port 8882

**Fixed:** Added 0.5s sleep in `api.py` after starting Flask thread

## Changes Made

### C# - `QuestionnaireAPIService.cs`
```csharp
// Added retry loop with exponential backoff
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try {
        var response = await _httpClient.PostAsync(url, jsonContent);
        // ...
    }
    catch (HttpRequestException ex) when (attempt < maxRetries)
    {
        await Task.Delay(delayMs);  // Wait 500ms and retry
    }
}
```

### Python - `main.py`
```python
# Line 231: Added -- prefix to make it an optional argument
parser.add_argument("--http_port", type=int, default=8882, ...)
```

### Python - `api.py`
```python
def start_http_server(port=8882):
    # ... start thread ...
    time.sleep(0.5)  # Give Flask time to bind to port
```

## Testing

1. ✅ Start your application
2. ✅ Load a questionnaire
3. ✅ Click "Start Avatar"
4. ✅ Watch Debug output for:
   ```
   Attempt 1/10: Sending questionnaire to http://localhost:8882/questionnaire
   Successfully sent questionnaire 'Your Questionnaire Name' to backend
   ```

## Expected Flow

1. C# starts Python backend with `--local --llama_port 8081 -p 8883 --http_port 8882`
2. Python `main.py` parses arguments correctly
3. Python starts Flask HTTP server on port 8882 (takes ~0.5s)
4. C# waits 500ms, attempts to send questionnaire
5. If connection refused, retry up to 10 times
6. Once Flask is ready, request succeeds
7. Python receives questionnaire data, signals event
8. Interview begins with your questionnaire questions!

## Verification Commands

### Test Python argument parsing:
```bash
cd Backend
python main.py --local --llama_port 8081 -p 8883 --http_port 8882
```

### Test HTTP endpoint manually:
```bash
curl -X POST http://localhost:8882/questionnaire ^
  -H "Content-Type: application/json" ^
  -d "{\"questions\":[\"Test Q1\",\"Test Q2\"],\"description\":\"Test questionnaire\"}"
```

Should return:
```json
{"status": "success", "message": "Questionnaire received"}
```
