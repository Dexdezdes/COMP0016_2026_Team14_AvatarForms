namespace AvatarFormsApp.Contracts.Services;

public interface IPythonProcessService : IDisposable
{
    bool IsRunning { get; }

    /// <summary>
    /// Starts the Python backend process (main.py or compiled main.exe).
    /// Requires the llamafile server to be running first if using --local mode.
    /// </summary>
    /// <param name="useLocal">Whether to use local LLaMA model instead of Fireworks API</param>
    /// <param name="llamaPort">Port for local LLaMA server (default: 8081)</param>
    /// <param name="websocketPort">Port for WebSocket server (default: 8883)</param>
    /// <returns>True if the process started successfully.</returns>
    Task<bool> StartAsync(bool useLocal = true, int llamaPort = 8081, int websocketPort = 8883);

    /// <summary>
    /// Stops the Python backend process.
    /// </summary>
    void Stop();

    /// <summary>
    /// Sends a line of text to the Python process via stdin.
    /// </summary>
    void SendInput(string message);

    /// <summary>
    /// Raised when the process writes to stdout.
    /// </summary>
    event Action<string>? OutputReceived;

    /// <summary>
    /// Raised when the process writes to stderr.
    /// </summary>
    event Action<string>? ErrorReceived;
}
