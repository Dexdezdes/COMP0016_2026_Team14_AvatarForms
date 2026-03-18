using System.Reflection;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Services;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class LlamafileProcessServiceTests : IDisposable
{
    private readonly LlamafileProcessService _sut = new();
    private readonly string _backendDir = Path.Combine(AppContext.BaseDirectory, "Backend");

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_backendDir))
            Directory.Delete(_backendDir, recursive: true);
    }

    private bool InvokeShouldFilterLog(string logLine)
    {
        var method = typeof(LlamafileProcessService)
            .GetMethod("ShouldFilterLog", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object[] { logLine })!;
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

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow_WhenNeverStarted()
    {
        var sut = new LlamafileProcessService();
        var ex = Record.Exception(() => sut.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenCalledTwice()
    {
        var sut = new LlamafileProcessService();
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

    // ── StartAsync — filesystem branches ─────────────────────────────────────

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenBackendFolderMissing()
    {
        if (Directory.Exists(_backendDir))
            Directory.Delete(_backendDir, recursive: true);

        var outputs = new List<string>();
        _sut.OutputReceived += msg => outputs.Add(msg);

        var result = await _sut.StartAsync();

        Assert.False(result);
        Assert.Contains(outputs, o => o.Contains("Backend folder not found"));
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenNoLlamafileInBackend()
    {
        Directory.CreateDirectory(_backendDir);

        var outputs = new List<string>();
        _sut.OutputReceived += msg => outputs.Add(msg);

        var result = await _sut.StartAsync();

        Assert.False(result);
        Assert.Contains(outputs, o => o.Contains("No .llamafile found"));
    }

    [Fact]
    public async Task StartAsync_FiresOutputReceived_WhenBackendMissing()
    {
        if (Directory.Exists(_backendDir))
            Directory.Delete(_backendDir, recursive: true);

        string? captured = null;
        _sut.OutputReceived += msg => captured = msg;

        await _sut.StartAsync();

        Assert.NotNull(captured);
    }

    [Fact]
    public async Task StartAsync_ReturnsTrue_WhenAlreadyRunning()
    {
        var field = typeof(LlamafileProcessService)
            .GetField("_llamafileProcess", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ping",
            Arguments = "-n 3 127.0.0.1",
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

    // ── ShouldFilterLog ───────────────────────────────────────────────────────

    [Fact]
    public void ShouldFilterLog_ReturnsTrue_ForJsonInfoLog()
    {
        Assert.True(InvokeShouldFilterLog("{\"level\":\"INFO\",\"message\":\"something\"}"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsTrue_WithLeadingWhitespace()
    {
        Assert.True(InvokeShouldFilterLog("   {\"level\":\"INFO\",\"msg\":\"test\"}"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForJsonErrorLog()
    {
        Assert.False(InvokeShouldFilterLog("{\"level\":\"ERROR\",\"message\":\"failed\"}"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForJsonWarningLog()
    {
        Assert.False(InvokeShouldFilterLog("{\"level\":\"WARNING\",\"message\":\"watch out\"}"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForPlainText()
    {
        Assert.False(InvokeShouldFilterLog("Server started on port 8081"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForEmptyString()
    {
        Assert.False(InvokeShouldFilterLog(string.Empty));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForInfoWordInPlainText()
    {
        Assert.False(InvokeShouldFilterLog("INFO: llamafile server ready"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForJsonWithoutLevelKey()
    {
        Assert.False(InvokeShouldFilterLog("{\"message\":\"no level key here\"}"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsTrue_ForMinimalInfoJson()
    {
        Assert.True(InvokeShouldFilterLog("{\"level\":\"INFO\"}"));
    }

    [Fact]
    public void ShouldFilterLog_ReturnsFalse_ForInfoLevelInMiddleOfText()
    {
        Assert.False(InvokeShouldFilterLog("some text {\"level\":\"INFO\"} more text"));
    }
}
