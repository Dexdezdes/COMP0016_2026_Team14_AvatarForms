namespace AvatarFormsApp.Contracts.Services;

public interface ILlamafileProcessService : IDisposable
{
    bool IsRunning { get; }

    /// <summary>
    /// Starts the llamafile server process and waits until the port is ready.
    /// </summary>
    /// <returns>True if the server started and is accepting connections.</returns>
    Task<bool> StartAsync();

    /// <summary>
    /// Stops the llamafile server process.
    /// </summary>
    void Stop();

    /// <summary>
    /// Raised when the process writes to stdout or stderr.
    /// </summary>
    event Action<string>? OutputReceived;
}
