using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Hosting;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Views;
using Xunit;

namespace AvatarFormsApp.Tests.Views;

public class AvatarPageTest : IAsyncLifetime
{
    private DispatcherQueueController? _controller;
    private DispatcherQueue? _queue;

    public async Task InitializeAsync()
    {
        try
        {
            var bootstrapType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap");
            bootstrapType?
                .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, new[] { typeof(uint) })?
                .Invoke(null, new object[] { 0x00010007u });
        }
        catch { }

        _controller = DispatcherQueueController.CreateOnDedicatedThread();
        _queue = _controller.DispatcherQueue;

        // Initialize WinUI XAML framework ON the dedicated thread
        // so SolidColorBrush, Run, etc. can be created without COMException
        var tcs = new TaskCompletionSource();
        _queue.TryEnqueue(() =>
        {
            try { WindowsXamlManager.InitializeForCurrentThread(); }
            catch { }
            tcs.SetResult();
        });
        await tcs.Task;
    }

    public async Task DisposeAsync()
    {
        if (_controller != null)
            await _controller.ShutdownQueueAsync();
    }

    // Creates AvatarPage WITHOUT calling constructor — skips InitializeComponent + App.GetService
    private static AvatarPage CreateUninitializedPage()
        => (AvatarPage)RuntimeHelpers.GetUninitializedObject(typeof(AvatarPage));

    // Creates page with all fake services injected via reflection
    private static AvatarPage CreatePageWithFakeServices()
    {
        var page = CreateUninitializedPage();
        var t = typeof(AvatarPage);
        t.GetField("_pythonProcessService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, new FakePythonForPage());
        t.GetField("_llamafileProcessService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, new FakeLlamafileForPage());
        t.GetField("_responseAPIService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, new FakeResponseForPage());
        t.GetField("_localSettingsService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, new FakeLocalSettingsForPage());
        Action<string> noOp = _ => { };
        t.GetField("_onPythonOutput", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, noOp);
        t.GetField("_onPythonError", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, noOp);
        t.GetField("_onLlamaOutput", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, noOp);
        return page;
    }

    private async Task OnUiThread(Action action)
    {
        var tcs = new TaskCompletionSource();
        _queue!.TryEnqueue(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        await tcs.Task;
    }

    private async Task OnUiThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        _queue!.TryEnqueue(async () =>
        {
            try { await action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        await tcs.Task;
    }

    private static T GetField<T>(AvatarPage page, string name)
        => (T)typeof(AvatarPage).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;

    private static void SetField(AvatarPage page, string name, object? value)
        => typeof(AvatarPage).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, value);

    private static object? Invoke(AvatarPage page, string name, params object?[] args)
        => typeof(AvatarPage).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(page, args);

    // ── AnsiCodeToBrush ───────────────────────────────────────────────────────

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Black_For_30()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var result = Invoke(page, "AnsiCodeToBrush", "30");
            Assert.NotNull(result);
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)result!;
            Assert.Equal(Microsoft.UI.Colors.Black, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_DarkRed_For_31()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "31")!;
            Assert.Equal(Microsoft.UI.Colors.DarkRed, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_DarkGreen_For_32()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "32")!;
            Assert.Equal(Microsoft.UI.Colors.DarkGreen, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Goldenrod_For_33()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "33")!;
            Assert.Equal(Microsoft.UI.Colors.Goldenrod, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_DarkBlue_For_34()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "34")!;
            Assert.Equal(Microsoft.UI.Colors.DarkBlue, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_DarkMagenta_For_35()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "35")!;
            Assert.Equal(Microsoft.UI.Colors.DarkMagenta, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_DarkCyan_For_36()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "36")!;
            Assert.Equal(Microsoft.UI.Colors.DarkCyan, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_LightGray_For_37()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "37")!;
            Assert.Equal(Microsoft.UI.Colors.LightGray, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Gray_For_90()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "90")!;
            Assert.Equal(Microsoft.UI.Colors.Gray, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Red_For_91()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "91")!;
            Assert.Equal(Microsoft.UI.Colors.Red, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Green_For_92()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "92")!;
            Assert.Equal(Microsoft.UI.Colors.Green, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Yellow_For_93()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "93")!;
            Assert.Equal(Microsoft.UI.Colors.Yellow, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Blue_For_94()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "94")!;
            Assert.Equal(Microsoft.UI.Colors.Blue, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Magenta_For_95()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "95")!;
            Assert.Equal(Microsoft.UI.Colors.Magenta, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_Cyan_For_96()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "96")!;
            Assert.Equal(Microsoft.UI.Colors.Cyan, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_White_For_97()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "97")!;
            Assert.Equal(Microsoft.UI.Colors.White, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_Returns_White_For_Reset()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Invoke(page, "AnsiCodeToBrush", "0")!;
            Assert.Equal(Microsoft.UI.Colors.White, brush.Color);
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_ReturnsNull_ForUnknownCode()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            Assert.Null(Invoke(page, "AnsiCodeToBrush", "99"));
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_ReturnsNull_ForEmptyCode()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            Assert.Null(Invoke(page, "AnsiCodeToBrush", ""));
        });
    }

    // ── ParseAnsiRuns ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAnsiRuns_PlainText_ReturnsSingleRun()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                Invoke(page, "ParseAnsiRuns", "Hello World")!).ToList();
            Assert.Single(runs);
            Assert.Equal("Hello World", runs[0].Text);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_ColorCode_SplitsCorrectly()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                Invoke(page, "ParseAnsiRuns", "\u001B[31mRed\u001B[0mNormal")!).ToList();
            Assert.True(runs.Count >= 2);
            Assert.Equal("Red", runs[0].Text);
            Assert.Equal("Normal", runs[1].Text);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_MultipleColors_AllSegmentsPresent()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                Invoke(page, "ParseAnsiRuns", "\u001B[32mGreen\u001B[31mRed\u001B[0mWhite")!).ToList();
            Assert.Equal(3, runs.Count);
            Assert.Equal("Green", runs[0].Text);
            Assert.Equal("Red", runs[1].Text);
            Assert.Equal("White", runs[2].Text);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_EmptyString_ReturnsEmpty()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                Invoke(page, "ParseAnsiRuns", "")!).ToList();
            Assert.Empty(runs);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_OnlyColorCode_NoText_ReturnsEmpty()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                Invoke(page, "ParseAnsiRuns", "\u001B[31m")!).ToList();
            Assert.Empty(runs);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_TextBeforeCode_IncludedInFirstRun()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                Invoke(page, "ParseAnsiRuns", "before\u001B[31mafter")!).ToList();
            Assert.Equal(2, runs.Count);
            Assert.Equal("before", runs[0].Text);
            Assert.Equal("after", runs[1].Text);
        });
    }

    // ── SendMessage — IsRunning=false early return ────────────────────────────

    [Fact]
    public async Task SendMessage_DoesNotThrow_WhenNotRunning()
    {
        await OnUiThread(() =>
        {
            var page = CreatePageWithFakeServices();
            var ex = Record.Exception(() => Invoke(page, "SendMessage"));
            Assert.Null(ex);
        });
    }

    // ── OnConsoleToggleChanged — null guard ───────────────────────────────────

    [Fact]
    public async Task OnConsoleToggleChanged_DoesNotThrow_WhenControlsNull()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var ex = Record.Exception(() => Invoke(page, "OnConsoleToggleChanged", null!, null!));
            Assert.Null(ex);
        });
    }

    // ── OnSettingsToggleChanged — null guard ──────────────────────────────────

    [Fact]
    public async Task OnSettingsToggleChanged_DoesNotThrow_WhenControlsNull()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var ex = Record.Exception(() => Invoke(page, "OnSettingsToggleChanged", null!, null!));
            Assert.Null(ex);
        });
    }

    // ── OnCloseSettingsClicked — null guard ───────────────────────────────────

    [Fact]
    public async Task OnCloseSettingsClicked_DoesNotThrow_WhenControlsNull()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var ex = Record.Exception(() => Invoke(page, "OnCloseSettingsClicked", null!, null!));
            Assert.Null(ex);
        });
    }

    // ── OnAvatarSelectionChanged — null guard ─────────────────────────────────

    [Fact]
    public async Task OnAvatarSelectionChanged_DoesNotThrow_WhenControlsNull()
    {
        await OnUiThread(() =>
        {
            var page = CreatePageWithFakeServices();
            var ex = Record.Exception(() => Invoke(page, "OnAvatarSelectionChanged", null!, null!));
            Assert.Null(ex);
        });
    }

    // ── OnInputTextChanged — empty method ─────────────────────────────────────

    [Fact]
    public async Task OnInputTextChanged_DoesNotThrow()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            var ex = Record.Exception(() => Invoke(page, "OnInputTextChanged", null!, null!));
            Assert.Null(ex);
        });
    }

    // ── StopVoiceInput — null speechRecognizer ────────────────────────────────

    [Fact]
    public async Task StopVoiceInput_DoesNotThrow_WhenSpeechRecognizerNull()
    {
        await OnUiThreadAsync(async () =>
        {
            var page = CreateUninitializedPage();
            var task = (Task)typeof(AvatarPage)
                .GetMethod("StopVoiceInput", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(page, null)!;
            await task;
        });
    }

    // ── StopAllServicesAsync — fake services ──────────────────────────────────

    [Fact]
    public async Task StopAllServicesAsync_DoesNotThrow_WithFakeServices()
    {
        await OnUiThreadAsync(async () =>
        {
            var page = CreatePageWithFakeServices();
            var task = (Task)typeof(AvatarPage)
                .GetMethod("StopAllServicesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(page, null)!;
            await task;
        });
    }

    [Fact]
    public async Task StopAllServicesAsync_SetsIsAvatarInitialized_False()
    {
        await OnUiThreadAsync(async () =>
        {
            var page = CreatePageWithFakeServices();
            SetField(page, "_isAvatarInitialized", true);
            var task = (Task)typeof(AvatarPage)
                .GetMethod("StopAllServicesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(page, null)!;
            await task;
            Assert.False(GetField<bool>(page, "_isAvatarInitialized"));
        });
    }

    // ── Field defaults ────────────────────────────────────────────────────────

    [Fact]
    public async Task UninitializedPage_IsMicEnabled_DefaultsFalse()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            Assert.False(GetField<bool>(page, "_isMicEnabled"));
        });
    }

    [Fact]
    public async Task UninitializedPage_IsAvatarInitialized_DefaultsFalse()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            Assert.False(GetField<bool>(page, "_isAvatarInitialized"));
        });
    }

    [Fact]
    public async Task FieldInjection_SelectedAvatar_CanBeSetAndRead()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            SetField(page, "_selectedAvatar", "david");
            Assert.Equal("david", GetField<string>(page, "_selectedAvatar"));
        });
    }

    [Fact]
    public async Task FieldInjection_AutoSendEnabled_CanBeSetAndRead()
    {
        await OnUiThread(() =>
        {
            var page = CreateUninitializedPage();
            SetField(page, "_autoSendEnabled", true);
            Assert.True(GetField<bool>(page, "_autoSendEnabled"));
        });
    }

    [Fact]
    public async Task OnSendClicked_DoesNotThrow_WhenNotRunning()
    {
        await OnUiThread(() =>
        {
            var page = CreatePageWithFakeServices();
            var ex = Record.Exception(() => Invoke(page, "OnSendClicked", null!, null!));
            Assert.Null(ex);
        });
    }
}

// ── Fake services ─────────────────────────────────────────────────────────────

internal class FakePythonForPage : IPythonProcessService
{
    public bool IsRunning => false;
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;
    public Task<bool> StartAsync(bool useLocal = true, int llamaPort = 8081,
        int websocketPort = 8883, int httpPort = 8882, int responsePort = 5000)
        => Task.FromResult(false);
    public void SendInput(string message) { }
    public void Stop() { }
    public void Dispose() { }
}

internal class FakeLlamafileForPage : ILlamafileProcessService
{
    public bool IsRunning => false;
    public event Action<string>? OutputReceived;
    public Task<bool> StartAsync() => Task.FromResult(false);
    public void Stop() { }
    public void Dispose() { }
}

internal class FakeResponseForPage : IResponseAPIService
{
    public bool IsRunning => false;
    public event Action? AllResponsesReceived;
    public Task StartServerAsync(int port = 5000) => Task.CompletedTask;
    public Task StopServerAsync() => Task.CompletedTask;
    public void SetExpectedQuestionCount(int count) { }
}

internal class FakeLocalSettingsForPage : ILocalSettingsService
{
    public Task<T?> ReadSettingAsync<T>(string key) => Task.FromResult(default(T?));
    public Task SaveSettingAsync<T>(string key, T value) => Task.CompletedTask;
}
