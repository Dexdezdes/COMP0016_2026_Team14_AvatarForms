using System.Diagnostics;
using System.Management;
using AvatarFormsApp.Contracts.Services;

namespace AvatarFormsApp.Services;

public class LlamafileProcessService : ILlamafileProcessService
{
    private Process? _llamafileProcess;
    private const int Port = 8081;
    private const int MaxPortRetries = 30;
    private const int PortRetryDelayMs = 1000;

    public bool IsRunning => _llamafileProcess is not null && !_llamafileProcess.HasExited;

    public event Action<string>? OutputReceived;

    public async Task<bool> StartAsync()
    {
        if (IsRunning)
        {
            OutputReceived?.Invoke("[LLAMAFILE] Server already running");
            return true;
        }

        try
        {
            string backendPath = Path.Combine(AppContext.BaseDirectory, "Backend");
            if (!Directory.Exists(backendPath))
            {
                OutputReceived?.Invoke("[ERROR] Backend folder not found");
                return false;
            }

            var llamafilePath = Directory.GetFiles(backendPath, "*.llamafile").FirstOrDefault();
            if (string.IsNullOrEmpty(llamafilePath))
            {
                OutputReceived?.Invoke("[ERROR] No .llamafile found in Backend folder");
                return false;
            }

            OutputReceived?.Invoke($"[LLAMAFILE] Found: {llamafilePath}");

            var (gpuName, gpuMem) = GetGpuInfo();
            System.Diagnostics.Debug.WriteLine($"Detected GPU: {gpuName} with {gpuMem} MB VRAM");
            var gpuArgs = 0; // Default to CPU
            var contextArgs = 1024; // Default to 1K context window for CPU
            if (gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                || gpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            {
                if (gpuMem > 6144) // 6GB+ VRAM
                {
                    gpuArgs = 9999;
                    contextArgs = 4096;
                }
                else if (gpuMem > 4096) // 4GB+ VRAM
                {
                    gpuArgs = 9999;
                    contextArgs = 2048;
                }
            }
            else
            {
                OutputReceived?.Invoke($"[LLAMAFILE] No compatible GPU detected, using CPU with limited context");
            }

            var start = new ProcessStartInfo
            {
                FileName = llamafilePath,
                Arguments = $"--server --host 127.0.0.1 --port {Port} --ctx-size {contextArgs} -ngl {gpuArgs} --nobrowser",
                WorkingDirectory = Path.GetDirectoryName(llamafilePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _llamafileProcess = new Process { StartInfo = start, EnableRaisingEvents = true };

            _llamafileProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && !ShouldFilterLog(e.Data))
                    OutputReceived?.Invoke(e.Data);
            };

            _llamafileProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && !ShouldFilterLog(e.Data))
                    OutputReceived?.Invoke($"[LLAMAFILE STDERR] {e.Data}");
            };

            _llamafileProcess.Start();
            _llamafileProcess.BeginOutputReadLine();
            _llamafileProcess.BeginErrorReadLine();

            OutputReceived?.Invoke($"[LLAMAFILE] Server process started on port {Port}");

            // Wait for the port to become available
            bool ready = await WaitForPortAsync(Port);
            if (!ready)
            {
                OutputReceived?.Invoke("[ERROR] Llamafile server did not start in time");
                Stop();
                return false;
            }

            OutputReceived?.Invoke("[LLAMAFILE] Server is ready and accepting connections");
            return true;
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke($"[LLAMAFILE ERROR] Failed to start: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        if (_llamafileProcess is not null && !_llamafileProcess.HasExited)
        {
            try { _llamafileProcess.Kill(entireProcessTree: true); } catch { }
        }
        _llamafileProcess = null;
    }

    private (string type, long mem) GetGpuInfo()
    {
        string gpuName = "No GPU";
        long gpuMem = 0;

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (key != null)
            {
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (subKeyName.StartsWith("0"))
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var name = subKey?.GetValue("HardwareInformation.AdapterString");
                        var mem = subKey?.GetValue("HardwareInformation.qwMemorySize");
                        if (mem != null)
                        {
                            long vram = Convert.ToInt64(mem) / (1024 * 1024);
                            if (vram > gpuMem)
                            {
                                gpuName = name?.ToString() ?? gpuName;
                                gpuMem = vram;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get GPU Info: {ex.Message}");
        }

        return (gpuName, gpuMem);
    }

    private static async Task<bool> WaitForPortAsync(int port)
    {
        for (int i = 0; i < MaxPortRetries; i++)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", port);
                return true;
            }
            catch
            {
                await Task.Delay(PortRetryDelayMs);
            }
        }
        return false;
    }

    private static bool ShouldFilterLog(string logLine)
    {
        // Filter out JSON-formatted INFO logs from llamafile
        if (logLine.TrimStart().StartsWith("{") && logLine.Contains("\"level\":\"INFO\""))
        {
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
