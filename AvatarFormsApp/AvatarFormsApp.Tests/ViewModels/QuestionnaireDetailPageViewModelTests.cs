using System.Collections.ObjectModel;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Contracts.ViewModels;
using AvatarFormsApp.Models;
using AvatarFormsApp.ViewModels;
using Moq;
using Xunit;
using HarmonyLib;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Hosting;
using System.Reflection;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using AvatarFormsApp.Views;

namespace AvatarFormsApp.Tests.ViewModels;



public class AvatarPageTest : IAsyncLifetime
{
    private DispatcherQueueController? _controller;
    private DispatcherQueue? _queue;
    private static Harmony? _harmony;

    public async Task InitializeAsync()
    {
        // Fixes "Class not registered" by loading the WinUI 3 runtime factory
        Bootstrap.Initialize(0x00010007); // 1.7 version of the SDK

        _controller = DispatcherQueueController.CreateOnDedicatedThread();
        _queue = _controller.DispatcherQueue;

        var tcs = new TaskCompletionSource();
        _queue.TryEnqueue(() =>
        {
            _harmony = new Harmony("avatarpage.tests");
            // ... (rest of your Harmony patching)
            tcs.SetResult();
        });
        await tcs.Task;
    }

    public async Task DisposeAsync()
    {
        _harmony?.UnpatchAll("avatarpage.tests");
        if (_controller != null)
            await _controller.ShutdownQueueAsync();
    }

    // Replaces entire constructor — sets fake services, skips InitializeComponent
    private static bool ConstructorPrefix(AvatarPage __instance)
    {
        var t = typeof(AvatarPage);
        t.GetField("_pythonProcessService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, new FakePythonForPage());
        t.GetField("_llamafileProcessService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, new FakeLlamafileForPage());
        t.GetField("_responseAPIService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, new FakeResponseForPage());
        t.GetField("_localSettingsService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, new FakeLocalSettingsForPage());
        t.GetField("_selectedAvatar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, "julia");
        t.GetField("_autoSendEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, true);

        Action<string> noOp = _ => { };
        t.GetField("_onPythonOutput", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, noOp);
        t.GetField("_onPythonError", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, noOp);
        t.GetField("_onLlamaOutput", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(__instance, noOp);

        return false; // Skip original constructor entirely
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

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnsiCodeToBrush_ReturnsNonNull_ForAllKnownCodes()
    {
        await OnUiThread(() =>
        {
            var page = new AvatarPage();
            var method = typeof(AvatarPage)
                .GetMethod("AnsiCodeToBrush", BindingFlags.NonPublic | BindingFlags.Instance)!;

            foreach (var code in new[] { "30","31","32","33","34","35","36","37",
                                         "90","91","92","93","94","95","96","97","0" })
            {
                var result = method.Invoke(page, new object[] { code });
                Assert.NotNull(result);
            }
        });
    }

    [Fact]
    public async Task AnsiCodeToBrush_ReturnsNull_ForUnknownCode()
    {
        await OnUiThread(() =>
        {
            var page = new AvatarPage();
            var method = typeof(AvatarPage)
                .GetMethod("AnsiCodeToBrush", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var result = method.Invoke(page, new object[] { "99" });
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_PlainText_ReturnsSingleRun()
    {
        await OnUiThread(() =>
        {
            var page = new AvatarPage();
            var method = typeof(AvatarPage)
                .GetMethod("ParseAnsiRuns", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                method.Invoke(page, new object[] { "Hello World" })!).ToList();

            Assert.Single(runs);
            Assert.Equal("Hello World", runs[0].Text);
        });
    }

    [Fact]
    public async Task ParseAnsiRuns_ColorCode_SplitsCorrectly()
    {
        await OnUiThread(() =>
        {
            var page = new AvatarPage();
            var method = typeof(AvatarPage)
                .GetMethod("ParseAnsiRuns", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                method.Invoke(page, new object[] { "\u001B[31mRed\u001B[0mNormal" })!).ToList();

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
            var page = new AvatarPage();
            var method = typeof(AvatarPage)
                .GetMethod("ParseAnsiRuns", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                method.Invoke(page, new object[] { "\u001B[32mGreen\u001B[31mRed\u001B[0mWhite" })!).ToList();

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
            var page = new AvatarPage();
            var method = typeof(AvatarPage)
                .GetMethod("ParseAnsiRuns", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var runs = ((IEnumerable<Microsoft.UI.Xaml.Documents.Run>)
                method.Invoke(page, new object[] { "" })!).ToList();

            Assert.Empty(runs);
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


public class QuestionnaireDetailPageViewModelTests
{
    private readonly Mock<IQuestionnaireService> _mockQService = new();
    private readonly Mock<INavigationService> _mockNavService = new();
    private readonly Mock<ILlamafileProcessService> _mockLlamaService = new();
    private readonly Mock<IPythonProcessService> _mockPythonService = new();
    private readonly Mock<IQuestionnaireAPIService> _mockQApiService = new();
    private readonly Mock<IResponseAPIService> _mockResponseApiService = new();

    private QuestionnaireDetailPageViewModel GetViewModel()
    {
        return new QuestionnaireDetailPageViewModel(
            _mockQService.Object,
            _mockNavService.Object,
            _mockLlamaService.Object,
            _mockPythonService.Object,
            _mockQApiService.Object,
            _mockResponseApiService.Object);
    }

    private Questionnaire CreateValidQuestionnaire(string id)
    {
        return new Questionnaire
        {
            Id = id,
            Name = "Test Questionnaire",
            OwnerId = "user-1",
            Questions = new List<Question>
            {
                new Question { Id = "q1", Text = "Q1", Order = 1, QuestionnaireId = id }
            }
        };
    }

    // ── Loading Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadQuestionnaireAsync_Success_SetsProperties()
    {
        var vm = GetViewModel();
        var questionnaire = CreateValidQuestionnaire("test-id");
        _mockQService.Setup(s => s.GetByIdAsync("test-id")).ReturnsAsync(questionnaire);

        await vm.LoadQuestionnaireAsync("test-id");

        Assert.Equal("Test Questionnaire", vm.PageTitle);
        Assert.Equal(questionnaire, vm.Questionnaire);
        Assert.Single(vm.Questions);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadQuestionnaireAsync_HandlesException()
    {
        var vm = GetViewModel();
        _mockQService.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ThrowsAsync(new Exception("DB Error"));

        // Should not crash, just set IsLoading to false
        await vm.LoadQuestionnaireAsync("any");

        Assert.False(vm.IsLoading);
    }

    // ── Navigation & Backend Flow Tests ──────────────────────────────────────

    [Fact]
    public async Task NavigateToAvatarAsync_FullSuccess_Navigates()
    {
        var vm = GetViewModel();
        vm.Questionnaire = CreateValidQuestionnaire("q1");

        // Setup success for all steps
        _mockLlamaService.Setup(s => s.StartAsync()).ReturnsAsync(true);
        _mockPythonService.Setup(s => s.StartAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                         .ReturnsAsync(true);
        _mockQApiService.Setup(s => s.SendQuestionnaireAsync(It.IsAny<string>(), It.IsAny<int>()))
                        .ReturnsAsync(true);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        _mockNavService.Verify(n => n.NavigateTo(nameof(AvatarPageViewModel)), Times.Once);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_IfLlamafileFails()
    {
        var vm = GetViewModel();
        _mockLlamaService.Setup(s => s.StartAsync()).ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Contains("Failed to start llamafile", vm.StatusMessage);
        _mockNavService.Verify(n => n.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_IfPythonFails()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true); // Already running
        _mockPythonService.Setup(s => s.StartAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                         .ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Contains("Failed to start avatar", vm.StatusMessage);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_IfNoQuestionnaireLoaded()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true);
        _mockResponseApiService.SetupGet(s => s.IsRunning).Returns(true);
        _mockPythonService.SetupGet(s => s.IsRunning).Returns(true);
        vm.Questionnaire = null;

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Equal("No questionnaire uploaded.", vm.StatusMessage);
    }

    // ── Event Handling Tests ────────────────────────────────────────────────

    [Fact]
    public void OnAllResponsesReceived_StopsServer()
    {
        var vm = GetViewModel();

        // Trigger the event via the mock
        _mockResponseApiService.Raise(m => m.AllResponsesReceived += null);

        // We use Task.Delay(500) in the code, so we need a tiny wait in the test 
        // to verify the async call inside the void event handler.
        Thread.Sleep(600);

        _mockResponseApiService.Verify(s => s.StopServerAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void OnNavigatedFrom_UnsubscribesFromEvent()
    {
        var vm = GetViewModel();

        vm.OnNavigatedFrom();

        // After unsubscription, raising the event should not trigger another StopServerAsync call
        _mockResponseApiService.Raise(m => m.AllResponsesReceived += null);

        Thread.Sleep(100);
        _mockResponseApiService.Verify(s => s.StopServerAsync(), Times.Never);
    }

    [Fact]
    public void CanNavigateToAvatar_ReturnsFalse_WhileStarting()
    {
        var vm = GetViewModel();
        vm.IsStartingBackend = true;

        var canExecute = vm.NavigateToAvatarCommand.CanExecute(null);

        Assert.False(canExecute);
    }
}
