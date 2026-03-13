using System.Reflection;
using AvatarFormsApp.Services;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class PythonProcessServiceTests : IDisposable
{
    private readonly PythonProcessService _sut = new();
    private readonly string _backendDir = Path.Combine(AppContext.BaseDirectory, "Backend");

    public void Dispose()
    {
        _sut.Dispose();

        // FIX: Check if directory exists before deleting, and wrap in try-catch
        // to handle cases where the OS hasn't released the file lock yet.
        if (Directory.Exists(_backendDir))
        {
            try
            {
                Directory.Delete(_backendDir, recursive: true);
            }
            catch (IOException)
            {
                // If the folder is locked by another process, we ignore it 
                // to prevent the test runner from crashing.
            }
        }
    }

    private string InvokeBuildArguments(bool useLocal, int llamaPort, int websocketPort, int httpPort, int responsePort)
    {
        var method = typeof(PythonProcessService)
            .GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { useLocal, llamaPort, websocketPort, httpPort, responsePort })!;
    }

    private string InvokeGetPythonPath()
    {
        var method = typeof(PythonProcessService)
            .GetMethod("GetPythonPath", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (string)method.Invoke(_sut, null)!;
    }

    // ── IsRunning ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsRunning_ReturnsFalse_WhenNeverStarted()
    {
        Assert.False(_sut.IsRunning);
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Stop_DoesNotThrow_WhenNeverStarted()
    {
        var ex = Record.Exception(() => _sut.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_DoesNotThrow_WhenCalledMultipleTimes()
    {
        _sut.Stop();
        _sut.Stop();
        var ex = Record.Exception(() => _sut.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void IsRunning_ReturnsFalse_AfterStop()
    {
        _sut.Stop();
        Assert.False(_sut.IsRunning);
    }

    // ── SendInput ─────────────────────────────────────────────────────────────

    [Fact]
    public void SendInput_DoesNotThrow_WhenNotRunning()
    {
        var ex = Record.Exception(() => _sut.SendInput("hello"));
        Assert.Null(ex);
    }

    [Fact]
    public void SendInput_DoesNotThrow_WhenMessageIsEmpty()
    {
        var ex = Record.Exception(() => _sut.SendInput(string.Empty));
        Assert.Null(ex);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow_WhenNeverStarted()
    {
        var sut = new PythonProcessService();
        var ex = Record.Exception(() => sut.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenCalledTwice()
    {
        var sut = new PythonProcessService();
        sut.Dispose();
        var ex = Record.Exception(() => sut.Dispose());
        Assert.Null(ex);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public void OutputReceived_CanSubscribeAndUnsubscribe()
    {
        Action<string> handler = msg => { };
        _sut.OutputReceived += handler;
        var ex = Record.Exception(() => _sut.OutputReceived -= handler);
        Assert.Null(ex);
    }

    [Fact]
    public void ErrorReceived_CanSubscribeAndUnsubscribe()
    {
        Action<string> handler = msg => { };
        _sut.ErrorReceived += handler;
        var ex = Record.Exception(() => _sut.ErrorReceived -= handler);
        Assert.Null(ex);
    }

    // ── StartAsync — filesystem branches ─────────────────────────────────────

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenNoExeAndNoScript()
    {
        // Ensure directory is clean and exists
        if (Directory.Exists(_backendDir)) Directory.Delete(_backendDir, true);
        Directory.CreateDirectory(_backendDir);

        var errors = new List<string>();
        _sut.ErrorReceived += msg => errors.Add(msg);

        var result = await _sut.StartAsync();

        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Neither compiled executable nor main.py found"));
    }

    [Fact]
    public async Task StartAsync_FiresErrorReceived_WhenScriptMissing()
    {
        if (!Directory.Exists(_backendDir)) Directory.CreateDirectory(_backendDir);

        string? captured = null;
        _sut.ErrorReceived += msg => captured = msg;

        await _sut.StartAsync();

        Assert.NotNull(captured);
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenBackendFolderHasNoFiles()
    {
        if (!Directory.Exists(_backendDir)) Directory.CreateDirectory(_backendDir);

        var result = await _sut.StartAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task StartAsync_ReturnsTrue_WhenAlreadyRunning()
    {
        var field = typeof(PythonProcessService)
            .GetField("_pythonProcess", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Use a cross-platform command that stays alive for a few seconds
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ping",
            Arguments = OperatingSystem.IsWindows() ? "-n 5 127.0.0.1" : "-c 5 127.0.0.1",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        var proc = System.Diagnostics.Process.Start(psi)!;
        field.SetValue(_sut, proc);

        try
        {
            var outputs = new List<string>();
            _sut.OutputReceived += msg => outputs.Add(msg);

            var result = await _sut.StartAsync();

            Assert.True(result);
            Assert.Contains(outputs, o => o.Contains("already running"));
        }
        finally
        {
            try { proc.Kill(); } catch { }
            proc.Dispose();
            field.SetValue(_sut, null);
        }
    }

    // ── BuildArguments ────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_DefaultPorts_ReturnsOnlyLocal()
    {
        Assert.Equal("--local", InvokeBuildArguments(true, 8081, 8883, 8882, 5000));
    }

    [Fact]
    public void BuildArguments_UseLocalFalse_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, InvokeBuildArguments(false, 8081, 8883, 8882, 5000));
    }

    [Fact]
    public void BuildArguments_CustomLlamaPort_IncludesFlag()
    {
        Assert.Contains("--llama_port 9000", InvokeBuildArguments(false, 9000, 8883, 8882, 5000));
    }

    [Fact]
    public void BuildArguments_CustomWebsocketPort_IncludesFlag()
    {
        Assert.Contains("-p 9001", InvokeBuildArguments(false, 8081, 9001, 8882, 5000));
    }

    [Fact]
    public void BuildArguments_CustomHttpPort_IncludesFlag()
    {
        Assert.Contains("--http_port 9002", InvokeBuildArguments(false, 8081, 8883, 9002, 5000));
    }

    [Fact]
    public void BuildArguments_CustomResponsePort_IncludesFlag()
    {
        Assert.Contains("--response_port 9003", InvokeBuildArguments(false, 8081, 8883, 8882, 9003));
    }

    [Fact]
    public void BuildArguments_AllCustomPorts_IncludesAllFlags()
    {
        var result = InvokeBuildArguments(true, 9000, 9001, 9002, 9003);
        Assert.Contains("--local", result);
        Assert.Contains("--llama_port 9000", result);
        Assert.Contains("-p 9001", result);
        Assert.Contains("--http_port 9002", result);
        Assert.Contains("--response_port 9003", result);
    }

    [Fact]
    public void BuildArguments_DefaultPorts_ContainsNoPortFlags()
    {
        var result = InvokeBuildArguments(false, 8081, 8883, 8882, 5000);
        Assert.DoesNotContain("--llama_port", result);
        Assert.DoesNotContain("-p ", result);
        Assert.DoesNotContain("--http_port", result);
        Assert.DoesNotContain("--response_port", result);
    }

    // ── GetPythonPath ─────────────────────────────────────────────────────────

    [Fact]
    public void GetPythonPath_ReturnsNonEmptyString()
    {
        Assert.NotEmpty(InvokeGetPythonPath());
    }

    [Fact]
    public void GetPythonPath_ReturnsSameValue_WhenCalledTwice()
    {
        Assert.Equal(InvokeGetPythonPath(), InvokeGetPythonPath());
    }

    [Fact]
    public void GetPythonPath_FallsBackToPython_WhenNoVenvFound()
    {
        var result = InvokeGetPythonPath();
        Assert.True(result == "python" || result.EndsWith("python.exe", StringComparison.OrdinalIgnoreCase) || result.EndsWith("python3", StringComparison.OrdinalIgnoreCase));
    }
}
