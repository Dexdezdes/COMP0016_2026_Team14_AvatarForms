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

    public async Task<bool> StartAsync(bool useLocal = true, int llamaPort = 8081, int websocketPort = 8883, int httpPort = 8882)
    {
        if (IsRunning)
        {
            OutputReceived?.Invoke("[PYTHON] Process already running");
            return true;
        }

        try
        {
            // Build command-line arguments
            string arguments = BuildArguments(useLocal, llamaPort, websocketPort, httpPort);

            // First, try to find the compiled executable
            string backendDir = Path.Combine(AppContext.BaseDirectory, "Backend");
            string compiledExePath = Path.Combine(backendDir, "dist", "main", "main.exe");
            string? workingDirectory = null;
            
            ProcessStartInfo start;

            if (File.Exists(compiledExePath))
            {
                // Use compiled executable
                OutputReceived?.Invoke($"[SUCCESS] Using compiled executable: {compiledExePath}");
                OutputReceived?.Invoke($"[INFO] Arguments: {arguments}");
                workingDirectory = Path.GetDirectoryName(compiledExePath);
                
                start = new ProcessStartInfo
                {
                    FileName = compiledExePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else
            {
                // Fall back to running Python script directly
                string scriptPath = Path.Combine(backendDir, "main.py");
                if (!File.Exists(scriptPath))
                {
                    ErrorReceived?.Invoke("[ERROR] Neither compiled executable nor main.py found in Backend folder");
                    return false;
                }

                OutputReceived?.Invoke($"[WARNING] Compiled executable not found, using Python script: {scriptPath}");
                OutputReceived?.Invoke($"[INFO] Arguments: {arguments}");
                string pythonExe = GetPythonPath();
                workingDirectory = Path.GetDirectoryName(scriptPath);

                start = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"-u \"{scriptPath}\" {arguments}",
                    WorkingDirectory = workingDirectory,
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

            OutputReceived?.Invoke("[AI] Python backend process started");

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

    private static string BuildArguments(bool useLocal, int llamaPort, int websocketPort, int httpPort)
    {
        var args = new List<string>();

        if (useLocal)
        {
            args.Add("--local");
        }

        if (llamaPort != 8081)
        {
            args.Add($"--llama_port {llamaPort}");
        }

        if (websocketPort != 8883)
        {
            args.Add($"-p {websocketPort}");
        }

        if (httpPort != 8882)
        {
            args.Add($"--http_port {httpPort}");
        }

        return string.Join(" ", args);
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

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
