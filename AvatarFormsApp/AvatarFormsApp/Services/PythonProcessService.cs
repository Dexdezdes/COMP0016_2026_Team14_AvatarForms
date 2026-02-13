using System.Diagnostics;
using System.Text.RegularExpressions;
using AvatarFormsApp.Contracts.Services;

namespace AvatarFormsApp.Services;

public class PythonProcessService : IPythonProcessService
{
    private Process? _pythonProcess;
    private string? _cachedPythonPath;

    public bool IsRunning => _pythonProcess is not null && !_pythonProcess.HasExited;

    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;

    public async Task<bool> StartAsync()
    {
        if (IsRunning)
        {
            OutputReceived?.Invoke("[PYTHON] Process already running");
            return true;
        }

        try
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "Backend", "main.py");
            if (!File.Exists(scriptPath))
            {
                ErrorReceived?.Invoke("[ERROR] main.py not found in Backend folder");
                return false;
            }

            string pythonExe = GetPythonPath();

            var start = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"-u \"{scriptPath}\"",
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (pythonExe.Contains("env"))
            {
                string? venvRoot = Path.GetDirectoryName(Path.GetDirectoryName(pythonExe));
                if (venvRoot != null)
                {
                    string sitePackages = Path.Combine(venvRoot, "Lib", "site-packages");
                    start.EnvironmentVariables["PYTHONPATH"] = sitePackages;
                }
            }

            _pythonProcess = new Process { StartInfo = start, EnableRaisingEvents = true };

            _pythonProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OutputReceived?.Invoke(e.Data);
            };

            _pythonProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string cleaned = Regex.Replace(e.Data, @"\x1B\[[^@-~]*[@-~]", string.Empty);
                    ErrorReceived?.Invoke($"[PYTHON ERROR]: {cleaned}");
                }
            };

            _pythonProcess.Start();
            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();

            OutputReceived?.Invoke("[AI] Python process started");

            // Give the HTTP API server a moment to start (it runs on port 8083)
            bool ready = await WaitForPortAsync(8083);
            if (!ready)
            {
                ErrorReceived?.Invoke("[WARNING] Python HTTP API may not be ready yet");
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke($"[RUNTIME ERROR]: {ex.Message}");
            return false;
        }
    }

    public void SendInput(string message)
    {
        if (_pythonProcess is null || _pythonProcess.HasExited) return;

        _pythonProcess.StandardInput.WriteLine(message);
        _pythonProcess.StandardInput.Flush();
    }

    public void Stop()
    {
        if (_pythonProcess is not null && !_pythonProcess.HasExited)
        {
            try { _pythonProcess.Kill(entireProcessTree: true); } catch { }
        }
        _pythonProcess = null;
    }

    private string GetPythonPath()
    {
        if (!string.IsNullOrEmpty(_cachedPythonPath)) return _cachedPythonPath;

        string baseDir = AppContext.BaseDirectory;
        for (int i = 0; i <= 9; i++)
        {
            string probePath = Path.GetFullPath(Path.Combine(baseDir, new string('.', i * 3).Replace("...", "../"), "env/Scripts/python.exe"));
            if (File.Exists(probePath))
            {
                OutputReceived?.Invoke($"[SUCCESS] Python found at: {probePath}");
                return _cachedPythonPath = probePath;
            }
        }

        string buildVenvPath = Path.Combine(baseDir, "env", "Scripts", "python.exe");
        if (File.Exists(buildVenvPath)) return _cachedPythonPath = buildVenvPath;

        string sourceVenvPath = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\env\\Scripts\\python.exe"));
        if (File.Exists(sourceVenvPath)) return _cachedPythonPath = sourceVenvPath;

        OutputReceived?.Invoke("[WARNING] Virtual env not found. Falling back to system 'python'");
        return _cachedPythonPath = "python";
    }

    private static async Task<bool> WaitForPortAsync(int port, int maxRetries = 15, int delayMs = 1000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", port);
                return true;
            }
            catch
            {
                await Task.Delay(delayMs);
            }
        }
        return false;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
