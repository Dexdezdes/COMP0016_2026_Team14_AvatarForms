namespace AvatarFormsApp.Contracts.Services;

public interface IPythonProcessService : IDisposable
{
    bool IsRunning { get; }

    /// <summary>
    /// Starts the Python backend process (main.py).
    /// Requires the llamafile server to be running first.
    /// </summary>
    /// <returns>True if the process started successfully.</returns>
    Task<bool> StartAsync();

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
