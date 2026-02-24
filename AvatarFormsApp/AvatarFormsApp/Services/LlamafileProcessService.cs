using System.Diagnostics;
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

            var start = new ProcessStartInfo
            {
                FileName = llamafilePath,
                Arguments = $"--server --host 127.0.0.1 --port {Port} --ctx-size 4096 -ngl 9999 --nobrowser",
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
